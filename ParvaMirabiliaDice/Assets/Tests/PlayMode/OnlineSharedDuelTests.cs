using System.Collections;
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
    public sealed class OnlineSharedDuelTests
    {
        private GameFlow flow;
        private DuelNetworkSession session;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private T Read<T>(string name) => (T)typeof(GameFlow).GetField(name, Private).GetValue(flow);
        private void Set(string name, object value) => typeof(GameFlow).GetField(name, Private).SetValue(flow, value);
        private object Call(string name, params object[] values) => typeof(GameFlow).GetMethod(name, Private).Invoke(flow, values);
        private void Command(int player, DuelNetworkCommand command) => typeof(DuelNetworkSession).GetMethod("AcceptCommand", Private).Invoke(session, new object[] {player, command});
        private void Publish(VersusDuel duel)
        {
            Set("_onlineDuel", duel);
            Call("PublishOnline");
        }

        [UnitySetUp] public IEnumerator SetUp()
        {
            GameFlow.ConfigOverride = null; GameFlow.LegacyPresentationForTests = false;
            GameFlow.SeedOverride = () => 617;
            foreach (var old in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null; yield return null;
            flow = new GameObject("SharedOnlineUnderTest").AddComponent<GameFlow>();
            yield return null;
            Call("OpenOnline");
            session = flow.gameObject.AddComponent<DuelNetworkSession>();
            typeof(DuelNetworkSession).GetProperty("IsHost").SetValue(session, true);
            typeof(DuelNetworkSession).GetProperty("IsReady").SetValue(session, true);
            session.CommandReceived += (player, command) => Call("ReceiveOnlineCommand", player, command);
            session.SnapshotReceived += json => Call("ReceiveOnlineSnapshot", json);
            Set("_onlineSession", session);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            GameFlow.SeedOverride = null;
            if (flow != null) Object.Destroy(flow.gameObject);
            yield return null; yield return null;
        }

        private VersusDuel Finished()
        {
            var duel = new VersusDuel(617, 1); duel.Begin();
            Assert.That(duel.TryFinishRerolls(0, 1, 1, 0, out _), Is.True);
            Assert.That(duel.TryFinishRerolls(1, 1, 1, 0, out _), Is.True);
            Assert.That(duel.TrySubmit(0, 1, 2, ScoreHandKind.Pair, false, 0, out _), Is.True);
            Assert.That(duel.TrySubmit(1, 1, 2, ScoreHandKind.Three, false, 0, out _), Is.True);
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.Finished));
            Publish(duel);
            return duel;
        }

        [UnityTest] public IEnumerator OwnKeptRerollsPreviewGreenHealWhileOpponentStaysPrivate()
        {
            var duel = new VersusDuel(617);
            duel.Begin();
            var players = (VersusPlayer[])typeof(VersusDuel).GetField("players", Private).GetValue(duel);
            typeof(VersusPlayer).GetProperty("Hp").SetValue(players[0], 1400);
            typeof(VersusPlayer).GetProperty("Hp").SetValue(players[1], 1400);
            Publish(duel);
            Call("RefreshOnline");
            var own = Read<Text>("_onlineOwnHpText");
            var enemy = Read<Text>("_onlineEnemyHpText");
            Assert.That(ScoreDuelHealthLine.Heal(own), Is.EqualTo(80),
                "the online owner previews the health two kept rerolls would restore");
            Assert.That(own.text, Does.Contain(ScoreDuelHealthLine.Green));
            Assert.That(ScoreDuelHealthLine.Heal(enemy), Is.Zero,
                "the opponent's kept rerolls stay private until the round resolves");
            yield return null;
        }

        [UnityTest] public IEnumerator RematchNeedsBothSeatsAndRejectsThePreviousMatchCommands()
        {
            var old = Finished();
            Set("_onlineResultsRevealed", true); Call("RefreshOnline");
            Read<Button>("_onlineRematch").onClick.Invoke();
            Assert.That(Read<VersusDuel>("_onlineDuel"), Is.SameAs(old), "one consent must not reset a duel");
            Assert.That(Read<Text>("_onlineResultDetail").text, Does.Contain("再戦を待っています"));
            Assert.That(Read<Button>("_onlineRematch").interactable, Is.False);
            Command(1, new DuelNetworkCommand {Action = DuelNetworkAction.Rematch, Round = 1, Sequence = 10, MatchGeneration = 0});
            var fresh = Read<VersusDuel>("_onlineDuel");
            Assert.That(fresh, Is.Not.SameAs(old));
            Assert.That(fresh.Phase, Is.EqualTo(VersusPhase.Playing));
            Assert.That(fresh.Round, Is.EqualTo(1));
            Assert.That(fresh.Players[0].Hp, Is.EqualTo(ScoreDuel.StartingHp));
            Assert.That(fresh.Players[0].CarryUsed, Is.False);
            Assert.That(fresh.Players[0].Used, Is.All.False);
            Assert.That(Read<int>("_onlineMatch"), Is.EqualTo(1));
            Assert.That(Read<long>("_onlineSequence"), Is.EqualTo(1), "local command sequence survives rematch");
            Assert.That(Read<RectTransform>("_onlineResult").gameObject.activeSelf, Is.False);
            Command(1, new DuelNetworkCommand {Action = DuelNetworkAction.Reroll, Round = 1, Sequence = 11, MatchGeneration = 0});
            Assert.That(fresh.Players[1].RerollsLeft, Is.EqualTo(2), "stale previous-match action is ignored");
            Command(1, new DuelNetworkCommand {Action = DuelNetworkAction.Reroll, Round = 1, Sequence = 12, MatchGeneration = 1});
            Assert.That(fresh.Players[1].RerollsLeft, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator ResultReturnsToAnInteractiveOnlineLobbyAndClearsTheOldSession()
        {
            Finished();
            Assert.That(Read<RectTransform>("_onlineResult").gameObject.activeSelf, Is.False);
            Set("_onlineImpactAge", .1f); Call("RefreshOnline");
            Assert.That(Read<Button>("_onlineRematch").gameObject.activeInHierarchy, Is.False);
            Set("_onlineImpactAge", -1f); Set("_onlineResultsRevealed", true); Call("RefreshOnline");
            Assert.That(Read<Button>("_onlineReturnLobby").gameObject.activeInHierarchy, Is.True);
            Read<Button>("_onlineReturnLobby").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(Read<DuelNetworkSession>("_onlineSession"), Is.Null);
            Assert.That(Read<RectTransform>("_onlineRoot").gameObject.activeInHierarchy, Is.True);
            Assert.That(Read<Button>("_onlineCreate").IsInteractable(), Is.True);
            Assert.That(Read<InputField>("_onlineCode").IsInteractable(), Is.True);
            Assert.That(Read<RectTransform>("_onlineResult").gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator SoloGestureCanCommitBeforeOpponentFinishesRerolling()
        {
            var duel = new VersusDuel(617); duel.Begin(); Publish(duel);
            var rollingProp = typeof(GameFlow).GetProperty("OnlineRolling", Private);
            float deadline = Time.realtimeSinceStartup + 2f;
            while ((bool)rollingProp.GetValue(flow) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That((bool)rollingProp.GetValue(flow), Is.False, "the online roll must settle before the gesture");
            Canvas.ForceUpdateCanvases();
            var own = Read<RectTransform[]>("_onlineOwnDice")[0];
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, own.position);
            float scale = own.GetComponentInParent<Canvas>().scaleFactor;
            Call("OnlinePointerBegin", 0, start);
            Call("OnlinePointerEnd", start + Vector2.down * 48 * scale);
            Assert.That(duel.Players[0].RerollsFinished, Is.True);
            Assert.That(duel.Players[0].Submitted, Is.False, "submission waits until the opponent finishes");
            Assert.That(Read<bool>("_onlineCommitQueued"), Is.True);
            Command(1, new DuelNetworkCommand {Action = DuelNetworkAction.FinishRerolls, Round = 1, Sequence = 10, MatchGeneration = 0});
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.ChoosingRoles));
            Assert.That(duel.Players[0].Submitted, Is.True, "the original gesture commits exactly once when role phase opens");
            Assert.That(Read<bool>("_onlineCommitQueued"), Is.False);
        }

        [UnityTest] public IEnumerator OnlineUsesSoloDiceFontsAndFormulaAndTheChargeRemainsCompact()
        {
            var duel = new VersusDuel(617); duel.Begin(); Publish(duel);
            yield return new WaitForSecondsRealtime(.5f);
            var dice = Read<RectTransform[]>("_onlineOwnDice");
            Assert.That(dice[0].sizeDelta, Is.EqualTo(new Vector2(112, 112)));
            Assert.That(Read<Text>("_onlineScore").font, Is.SameAs(Read<Text>("_sdScoreNumber").font));
            Assert.That(Read<Text>("_onlineFormula").text, Does.Contain(" × "),
                "the shared formula is the dice total times the multiplier");
            Assert.That(Read<Text>("_onlineFormula").text, Does.Not.Contain("+"),
                "the old base-plus-total shape is retired");
            Command(0, new DuelNetworkCommand {Action = DuelNetworkAction.FinishRerolls, Round = 1, Sequence = 10});
            Command(1, new DuelNetworkCommand {Action = DuelNetworkAction.FinishRerolls, Round = 1, Sequence = 10});
            Command(0, new DuelNetworkCommand {Action = DuelNetworkAction.Submit, Round = 1, Sequence = 11, Kind = 0});
            foreach (var marker in Read<Image[]>("_onlineRoleMarkers"))
                Assert.That(marker.gameObject.activeSelf, Is.False, "a committed hand must not highlight a different unused role");
            Vector2 home = dice[0].anchoredPosition;
            for (int n = 0; n < 12; n++)
            {
                yield return null;
                Assert.That(Vector2.Distance(dice[0].anchoredPosition, home), Is.LessThan(3.9f), "energy must stay in a compact envelope");
            }
            Set("_sdReducedMotion", true); Call("UpdateOnlineWaitingDice");
            Assert.That(dice[0].localScale, Is.EqualTo(Vector3.one));
            Assert.That(dice[0].localEulerAngles, Is.EqualTo(Vector3.zero));
        }
    }
}
