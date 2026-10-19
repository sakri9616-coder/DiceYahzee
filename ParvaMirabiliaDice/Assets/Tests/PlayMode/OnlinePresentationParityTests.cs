using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ParvaMirabilia.Quality.Tests
{
    public sealed class OnlinePresentationParityTests
    {
        private GameFlow flow;
        private DuelNetworkSession session;
        private string clipboard;
        private int screenWidth, screenHeight;
        private bool fullscreen;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Type OnlineStateType = typeof(GameFlow).GetNestedType("OnlineState", BindingFlags.NonPublic);

        private T Read<T>(string name) => (T)typeof(GameFlow).GetField(name, Private).GetValue(flow);
        private void Set(string name, object value) => typeof(GameFlow).GetField(name, Private).SetValue(flow, value);
        private object Call(string name, params object[] values) => typeof(GameFlow).GetMethod(name, Private).Invoke(flow, values);

        [UnitySetUp] public IEnumerator SetUp()
        {
            clipboard = GUIUtility.systemCopyBuffer;
            screenWidth = Screen.width; screenHeight = Screen.height; fullscreen = Screen.fullScreen;
            GameFlow.ConfigOverride = null; GameFlow.LegacyPresentationForTests = false; GameFlow.SeedOverride = () => 617;
            foreach (var old in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            Screen.SetResolution(1280, 720, false);
            yield return null; yield return null;
            flow = new GameObject("OnlinePresentationParityUnderTest").AddComponent<GameFlow>();
            yield return null;
            Call("OpenOnline");
            session = flow.gameObject.AddComponent<DuelNetworkSession>();
            SetSessionProperty("IsHost", true);
            SetSessionProperty("IsReady", true);
            Set("_onlineSession", session);
            Set("_onlineFormationReducedMotion", new Func<bool>(() => false));
            yield return null;
            Canvas.ForceUpdateCanvases();
            flow.enabled = false;
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            GUIUtility.systemCopyBuffer = clipboard;
            Screen.SetResolution(screenWidth, screenHeight, fullscreen);
            GameFlow.ConfigOverride = null; GameFlow.SeedOverride = null; GameFlow.LegacyPresentationForTests = false;
            for (int i = 0; i < spawned.Count; i++) if (spawned[i] != null) Object.Destroy(spawned[i]);
            spawned.Clear();
            if (flow != null) Object.Destroy(flow.gameObject);
            yield return null; yield return null;
        }

        private void SetSessionProperty(string name, object value)
        {
            var prop = typeof(DuelNetworkSession).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(prop, Is.Not.Null, "DuelNetworkSession." + name);
            prop.SetValue(session, value);
        }

        private static int PhaseValue(string name)
        {
            return (int)Enum.Parse(typeof(VersusPhase), name);
        }

        private static string JsonArray(int[] values) => "[" + string.Join(",", values) + "]";

        private static object NewState(int round, string phase, int winner, int match, int revision,
            int[] ownDice, int[] enemyDice, int ownHp, int enemyHp,
            bool ownSubmitted = false, int ownKind = 0, int ownScore = 0, int enemyKind = 0, int enemyScore = 0)
        {
            int phaseValue = PhaseValue(phase);
            string used = "[false,false,false,false,false,false,false,false,false]";
            string json =
                "{\"round\":" + round +
                ",\"phase\":" + phaseValue +
                ",\"winner\":" + winner +
                ",\"revision\":" + revision +
                ",\"match\":" + match +
                ",\"players\":[" +
                    "{\"hp\":" + ownHp + ",\"rerolls\":2,\"kind\":" + ownKind + ",\"score\":" + ownScore +
                    ",\"held\":0,\"dice\":" + JsonArray(ownDice) + ",\"used\":" + used +
                    ",\"submitted\":" + (ownSubmitted ? "true" : "false") +
                    ",\"carry\":false,\"carryUsed\":false,\"rerollsFinished\":false,\"nextBonus\":false,\"carried\":false}," +
                    "{\"hp\":" + enemyHp + ",\"rerolls\":2,\"kind\":" + enemyKind + ",\"score\":" + enemyScore +
                    ",\"held\":0,\"dice\":" + JsonArray(enemyDice) + ",\"used\":" + used +
                    ",\"submitted\":false,\"carry\":false,\"carryUsed\":false,\"rerollsFinished\":false,\"nextBonus\":false,\"carried\":false}" +
                "],\"damage\":[0,0],\"ready\":[false,false],\"rematchReady\":[false,false]}";
            return JsonUtility.FromJson(json, OnlineStateType);
        }

        private object SetOnlineState(int round, string phase, int winner, int match, int revision,
            int[] ownDice, int[] enemyDice, int ownHp = 300, int enemyHp = 300,
            bool ownSubmitted = false, int ownKind = 0, int ownScore = 0, int enemyKind = 0, int enemyScore = 0)
        {
            var state = NewState(round, phase, winner, match, revision, ownDice, enemyDice, ownHp, enemyHp,
                ownSubmitted, ownKind, ownScore, enemyKind, enemyScore);
            Set("_onlineState", state);
            Set("_onlineMatch", match);
            Set("_onlineKind", ownKind == (int)ScoreHandKind.Chance ? (int)ScoreHandKind.Pair : ownKind);
            Set("_onlinePending", false);
            Set("_onlineCommitQueued", false);
            Set("_onlineStopped", false);
            Set("_onlineDragging", false);
            Set("_onlineAiming", false);
            Set("_onlineImpactAge", -1f);
            Set("_onlineLeaving", false);
            Set("_onlineConnecting", false);
            var help = Read<RectTransform>("_onlineHelp");
            if (help != null) help.gameObject.SetActive(false);
            return state;
        }

        private static void SetMember(object obj, string name, object value)
        {
            var field = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "field " + name);
            field.SetValue(obj, value);
        }

        private static object GetMember(object obj, string name)
        {
            var field = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "field " + name);
            return field.GetValue(obj);
        }

        private static void SetPlayerField(object state, int index, string name, object value)
        {
            var players = (Array)GetMember(state, "players");
            var player = players.GetValue(index);
            SetMember(player, name, value);
        }

        private static int ScoreFor(ScoreHandKind kind, int[] dice) => ScoreRules.Evaluate(kind, dice).Score;

        private int VisualWinner(ScoreHandKind ownKind, int[] ownDice, ScoreHandKind enemyKind, int[] enemyDice)
        {
            int ownScore = ScoreFor(ownKind, ownDice);
            int enemyScore = ScoreFor(enemyKind, enemyDice);
            var state = SetOnlineState(1, "Resolved", 0, 1, 1, ownDice, enemyDice,
                300, 300, false, (int)ownKind, ownScore, (int)enemyKind, enemyScore);
            Call("BeginOnlineAttack", state);
            return Read<int>("_onlineImpactWinner");
        }

        private static T FormationProp<T>(object presenter, string name)
        {
            var prop = presenter.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(prop, Is.Not.Null, "ScoreFormationPresenter." + name);
            return (T)prop.GetValue(presenter);
        }

        private object CreateDirectPresenter(int[] values)
        {
            var canvasGo = new GameObject("DirectFormationCanvas", typeof(Canvas), typeof(RectTransform));
            spawned.Add(canvasGo);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("DirectFormation", typeof(RectTransform));
            root.transform.SetParent(canvasGo.transform, false);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(1000, 1000);
            var dice = new RectTransform[5];
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject("DirectDie" + i, typeof(RectTransform));
                go.transform.SetParent(rootRt, false);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(80, 80);
                rt.anchoredPosition = new Vector2((i - 2) * 100, 0);
                dice[i] = rt;
            }
            var type = typeof(GameFlow).Assembly.GetType("ParvaMirabilia.Presentation.ScoreFormationPresenter");
            Assert.That(type, Is.Not.Null, "ScoreFormationPresenter type found");
            var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
                new[] { typeof(Transform), typeof(RectTransform[]), typeof(Func<bool>), typeof(int) }, null);
            Assert.That(ctor, Is.Not.Null, "ScoreFormationPresenter constructor found");
            return ctor.Invoke(new object[] { rootRt, dice, new Func<bool>(() => false), 0 });
        }

        private static void PlayFormation(object presenter, ScoreOffer offer, int[] values)
        {
            var method = presenter.GetType().GetMethod("Play", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "ScoreFormationPresenter.Play found");
            method.Invoke(presenter, new object[] { offer, values });
        }

        [UnityTest] public IEnumerator OnlineChanceXorDrivesVisualWinnerAndTwoChanceUsesResolvedScores()
        {
            var chanceDice = new[] { 1, 2, 3, 4, 5 };
            var pairDice = new[] { 6, 6, 1, 1, 1 };
            int chanceScore = ScoreFor(ScoreHandKind.Chance, chanceDice);
            int pairScore = ScoreFor(ScoreHandKind.Pair, pairDice);
            Assert.That(pairScore, Is.GreaterThan(chanceScore), "setup: pair must outscore chance");

            Assert.That(VisualWinner(ScoreHandKind.Chance, chanceDice, ScoreHandKind.Pair, pairDice), Is.EqualTo(0),
                "own Chance XOR makes own the visual winner even with a lower raw score");
            Assert.That(VisualWinner(ScoreHandKind.Pair, pairDice, ScoreHandKind.Chance, chanceDice), Is.EqualTo(1),
                "enemy Chance XOR makes enemy the visual winner even with a lower raw score");

            // Chance scores are face-sum based and are never zero; two Chance declarations
            // cancel the XOR and fall back to raw order. Use unequal real Chance draws so the
            // two directions prove the fallback is order-sensitive rather than a fixed value.
            var highChance = new[] { 6, 6, 6, 6, 5 };
            var lowChance = new[] { 1, 2, 3, 4, 5 };
            int highChanceScore = ScoreFor(ScoreHandKind.Chance, highChance);
            int lowChanceScore = ScoreFor(ScoreHandKind.Chance, lowChance);
            Assert.That(highChanceScore, Is.GreaterThan(lowChanceScore), "setup: high Chance must outscore low Chance");
            Assert.That(VisualWinner(ScoreHandKind.Chance, highChance, ScoreHandKind.Chance, lowChance), Is.EqualTo(0),
                "two Chance keeps raw order: higher own score wins");
            Assert.That(VisualWinner(ScoreHandKind.Chance, lowChance, ScoreHandKind.Chance, highChance), Is.EqualTo(1),
                "two Chance keeps raw order: higher enemy score wins even reversed");

            Assert.That(VisualWinner(ScoreHandKind.Pair, pairDice, ScoreHandKind.Pair, pairDice), Is.EqualTo(-1),
                "tied scores have no visual winner");
            yield return null;
        }

        [UnityTest] public IEnumerator OnlineImpactRetainsBeforeHpUntilContactThenDrains()
        {
            var dice = new[] { 1, 2, 3, 4, 5 };
            var enemyDice = new[] { 6, 6, 6, 6, 6 };
            SetOnlineState(1, "ChoosingRoles", 0, 1, 1, dice, enemyDice, 300, 300);
            Call("RefreshOnline");
            var hpText = Read<Text>("_onlineOwnHpText");
            string beforeText = hpText.text;
            Assert.That(beforeText, Does.Contain("300"), "full HP reading before the strike");

            // next.damage drives the in-bar red drop label; own 50 / enemy 100 makes the own
            // drain a real 50-point move that the test can observe against the pre-strike line.
            var next = NewState(1, "Resolved", 0, 1, 2, dice, enemyDice, 250, 200);
            SetMember(next, "damage", new[] { 50, 100 });
            Call("ReceiveOnlineSnapshot", JsonUtility.ToJson(next));
            Assert.That(Read<float>("_onlineImpactAge"), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Read<int>("_onlineBeforeOwn"), Is.EqualTo(300), "pre-strike HP captured");
            Assert.That(Read<int>("_onlineBeforeEnemy"), Is.EqualTo(300), "pre-strike enemy HP captured");

            Set("_onlineImpactAge", 0.1f);
            Call("UpdateOnlineImpact");
            Assert.That(hpText.text, Is.EqualTo(beforeText),
                "HP reading must not move before the .64s contact");

            Set("_onlineImpactAge", 0.70f);
            Call("UpdateOnlineImpact");
            Assert.That(hpText.text, Is.Not.EqualTo(beforeText), "HP reading drains at contact");
            yield return null;
        }

        [UnityTest] public IEnumerator OnlineSnapshotStartsImpactOnlyOnPhaseTransition()
        {
            var dice = new[] { 1, 2, 3, 4, 5 };
            var enemyDice = new[] { 6, 6, 6, 6, 6 };
            SetOnlineState(1, "ChoosingRoles", 0, 1, 1, dice, enemyDice, 300, 300);
            var resolved = NewState(1, "Resolved", 0, 1, 2, dice, enemyDice, 250, 200);
            Call("ReceiveOnlineSnapshot", JsonUtility.ToJson(resolved));
            Assert.That(Read<float>("_onlineImpactAge"), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Read<int>("_onlineBeforeOwn"), Is.EqualTo(300));

            Set("_onlineImpactAge", 0.2f);
            var resolvedAgain = NewState(1, "Resolved", 0, 1, 3, dice, enemyDice, 250, 200);
            Call("ReceiveOnlineSnapshot", JsonUtility.ToJson(resolvedAgain));
            Assert.That(Read<float>("_onlineImpactAge"), Is.EqualTo(0.2f).Within(0.001f),
                "a same-phase snapshot must not restart the impact");
            yield return null;
        }

        [UnityTest] public IEnumerator OnlineFormationFullHouseMatchesSingleRecipeAndIgnoresEnemyDice()
        {
            // Genuine strongest 3+2 FullHouse: three of a kind on the face that spends the
            // most, paired with a different face. Enemy dice are intentionally different to
            // prove the online cue reads only the own hand.
            var dice = new[] { 3, 3, 3, 4, 4 };
            var enemy = new[] { 6, 6, 6, 6, 6 };
            SetOnlineState(1, "Playing", 0, 1, 1, dice, enemy);
            Call("SDTickOnlineFormation", 0f);
            object online = Read<object>("_onlineFormationPresenter");
            Assert.That(online, Is.Not.Null);
            Assert.That(FormationProp<bool>(online, "Active"), Is.True);
            Assert.That(FormationProp<ScoreHandKind>(online, "Kind"), Is.EqualTo(ScoreHandKind.FullHouse));
            Assert.That(FormationProp<int>(online, "Tier"), Is.EqualTo(2));
            Assert.That(FormationProp<int>(online, "Mask"), Is.EqualTo(31));
            var groups = FormationProp<int[]>(online, "Groups");
            Assert.That(groups[0], Is.EqualTo(0b00111), "three-of-a-kind group uses physical positions 0..2");
            Assert.That(groups[1], Is.EqualTo(0b11000), "pair group uses physical positions 3..4");

            var offer = ScoreRules.Evaluate(ScoreHandKind.FullHouse, dice);
            Assert.That(offer.IsFormed, Is.True, "setup: dice form a FullHouse");
            var direct = CreateDirectPresenter(dice);
            PlayFormation(direct, offer, dice);
            Assert.That(FormationProp<ScoreHandKind>(direct, "Kind"), Is.EqualTo(FormationProp<ScoreHandKind>(online, "Kind")));
            Assert.That(FormationProp<int>(direct, "Tier"), Is.EqualTo(FormationProp<int>(online, "Tier")));
            Assert.That(FormationProp<int[]>(direct, "Path"), Is.EqualTo(FormationProp<int[]>(online, "Path")));
            Assert.That(FormationProp<int[]>(direct, "Groups"), Is.EqualTo(FormationProp<int[]>(online, "Groups")));
            yield return null;
        }

        [UnityTest] public IEnumerator OnlineFormationScrambledStraightUsesFaceOrderPathLikeSinglePresenter()
        {
            var dice = new[] { 5, 2, 4, 1, 3 };
            SetOnlineState(1, "Playing", 0, 1, 1, dice, new[] { 6, 6, 6, 6, 6 });
            Call("SDTickOnlineFormation", 0f);
            object online = Read<object>("_onlineFormationPresenter");
            Assert.That(online, Is.Not.Null);
            Assert.That(FormationProp<bool>(online, "Active"), Is.True);
            Assert.That(FormationProp<ScoreHandKind>(online, "Kind"), Is.EqualTo(ScoreHandKind.LargeStraight));
            Assert.That(FormationProp<int>(online, "Tier"), Is.EqualTo(3));
            var path = FormationProp<int[]>(online, "Path");
            Assert.That(path, Is.EqualTo(new[] { 3, 1, 4, 2, 0 }),
                "scrambled physical dice must trace by face 1..5");

            var offer = ScoreRules.Evaluate(ScoreHandKind.LargeStraight, dice);
            var direct = CreateDirectPresenter(dice);
            PlayFormation(direct, offer, dice);
            Assert.That(FormationProp<ScoreHandKind>(direct, "Kind"), Is.EqualTo(FormationProp<ScoreHandKind>(online, "Kind")));
            Assert.That(FormationProp<int>(direct, "Tier"), Is.EqualTo(FormationProp<int>(online, "Tier")));
            Assert.That(FormationProp<int[]>(direct, "Path"), Is.EqualTo(FormationProp<int[]>(online, "Path")));
            Assert.That(FormationProp<int[]>(direct, "Groups"), Is.EqualTo(FormationProp<int[]>(online, "Groups")));
            yield return null;
        }

        [UnityTest] public IEnumerator OnlineFormationSuppressesSubmittedChanceAndDragThenAllowsNewRound()
        {
            var dice = new[] { 3, 3, 3, 2, 2 };
            var state = SetOnlineState(1, "Playing", 0, 1, 1, dice, new[] { 6, 6, 6, 6, 6 });
            Call("SDTickOnlineFormation", 0f);
            object presenter = Read<object>("_onlineFormationPresenter");
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.True);

            SetPlayerField(state, 0, "submitted", true);
            Call("SDTickOnlineFormation", 0f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.False, "submitted suppresses active formation");
            SetPlayerField(state, 0, "submitted", false);
            Call("SDTickOnlineFormation", 1f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.False, "identical same round must not replay");

            SetMember(state, "round", 2);
            Call("SDTickOnlineFormation", 0f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.True, "new round re-eligible");

            Set("_onlineKind", (int)ScoreHandKind.Chance);
            Call("SDTickOnlineFormation", 0f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.False, "Chance suppresses active formation");
            Set("_onlineKind", (int)ScoreHandKind.Pair);
            Call("SDTickOnlineFormation", 1f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.False, "same round after Chance must not replay");

            SetMember(state, "round", 3);
            Call("SDTickOnlineFormation", 0f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.True);
            Set("_onlineDragging", true);
            Call("SDTickOnlineFormation", 0f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.False, "drag suppresses active formation");
            Set("_onlineDragging", false);
            Call("SDTickOnlineFormation", 1f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.False, "same round after drag must not replay");
            yield return null;
        }

        [UnityTest] public IEnumerator OnlineFormationResetsOnHelpRoundAndDispose()
        {
            var dice = new[] { 3, 3, 3, 2, 2 };
            var state = SetOnlineState(1, "Playing", 0, 1, 1, dice, new[] { 6, 6, 6, 6, 6 });
            Call("SDTickOnlineFormation", 0f);
            object presenter = Read<object>("_onlineFormationPresenter");
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.True);

            Call("OpenOnlineHelp");
            Call("SDTickOnlineFormation", 0f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.False, "help resets active formation");

            Call("CloseOnlineHelp");
            Call("SDTickOnlineFormation", 1f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.False, "same round after help must not replay");

            SetMember(state, "round", 2);
            Call("SDTickOnlineFormation", 0f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.True, "new round after help re-eligible");

            Set("_onlineState", null);
            Call("SDTickOnlineFormation", 0f);
            Assert.That(FormationProp<bool>(presenter, "Active"), Is.False, "dispose (null state) resets formation");
            yield return null;
        }
    }
}
