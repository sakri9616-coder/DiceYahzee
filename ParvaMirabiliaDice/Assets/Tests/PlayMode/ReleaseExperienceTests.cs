using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ParvaMirabilia.Tests.PlayMode
{
    public sealed class ReleaseExperienceTests
    {
        private GameFlow _flow;
        private MatchEngine Engine => Read<MatchEngine>("_engine");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Keep the v3 experience contract as an explicit baseline; DuelPresentationTests
            // cover the new fourth tutorial step and tactical default.
            GameFlow.ConfigOverride = () => new GameConfig().ApplyShippedPatternLayer();
            foreach (GameFlow old in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);
            yield return null;
            yield return null;
            _flow = new GameObject("ReleaseExperienceUnderTest").AddComponent<GameFlow>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameFlow.ConfigOverride = null;
            if (_flow != null) Object.Destroy(_flow.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PracticeTeachesSelectionAndRealDamage_WithoutMutatingTheLiveMatch()
        {
            Call("StartMatch");
            MatchEngine live = Engine;
            int[] before = (int[])live.PlayerDice.Clone();
            Call("OpenGuide");
            Assert.That(ButtonIn("_guideRoot", "GuideNext").interactable, Is.False);
            Call("AdvanceGuide");
            Assert.That(Read<int>("_guideStep"), Is.Zero);
            Call("OnPracticeDie", 4);
            Assert.That(ButtonIn("_guideRoot", "GuideNext").interactable, Is.True);
            Press("_guideRoot", "GuideNext");
            yield return null;
            Press("_guideRoot", "PracticeYaku2"); // large straight: 22 attack, 0 ward
            yield return null;
            Press("_guideRoot", "GuideNext");
            yield return null;
            string feedback = Read<Text>("_guideFeedback").text;
            Assert.That(feedback, Does.Contain("与えるダメージ 13"));
            Assert.That(feedback, Does.Contain("受けるダメージ 12"));
            Assert.That(Engine, Is.SameAs(live));
            CollectionAssert.AreEqual(before, live.PlayerDice);
            Assert.That(live.PlayerHp, Is.EqualTo(32));
            Press("_guideRoot", "GuideNext");
            Assert.That(Engine, Is.SameAs(live));
            Assert.That(Read<RectTransform>("_guideRoot").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator PracticeFromTheTitle_StartsANormalFreshDuel()
        {
            Call("OpenGuide");
            Call("OnPracticeDie", 4);
            Call("AdvanceGuide");
            Call("AdvanceGuide");
            Call("AdvanceGuide");
            Assert.That(Engine, Is.Not.Null);
            Assert.That(Engine.Phase, Is.EqualTo(MatchPhase.AwaitingReroll));
            Assert.That(Engine.PlayerRerollsLeft, Is.EqualTo(2));
            Assert.That(Engine.History.Count, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GuardPracticeIncludesPushback_WhenNeitherStrikeLands()
        {
            Call("OpenGuide");
            Call("OnPracticeDie", 4);
            Call("AdvanceGuide");
            yield return null;
            Press("_guideRoot", "PracticeYaku0");
            Press("_guideRoot", "GuideNext");
            Assert.That(Read<Text>("_guideFeedback").text, Does.Contain("与えるダメージ 0"));
            Assert.That(Read<Text>("_guideFeedback").text, Does.Contain("受けるダメージ 3"));
            Assert.That(Read<Text>("_guideFeedback").text, Does.Contain("相手の押し勝ち"));
        }

        [UnityTest]
        public IEnumerator EnterUsedToCloseAnOverlay_DoesNotAlsoCommitTheUnderlyingDuel()
        {
            Call("StartMatch");
            Call("SetRulesVisible", true);
            Press("_rulesRoot", "CloseRules");
            Call("HandleShortcut", KeyCode.Return);
            Assert.That(Engine.History.Count, Is.Zero);
            yield return null;
            Call("HandleShortcut", KeyCode.Return);
            Assert.That(Engine.History.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator QuickSelectionsOnlyMarkDice_AndTheRerollKeepsEveryOtherDie()
        {
            Call("StartMatch");
            int[] hand = { 1, 2, 3, 4, 6 };
            System.Array.Copy(hand, Engine.PlayerDice, 5);
            Call("RefreshBattle");
            Call("MarkLowDice");
            CollectionAssert.AreEqual(new[] { true, true, true, false, false }, Read<bool[]>("_rerollMarked"));
            CollectionAssert.AreEqual(hand, Engine.PlayerDice);
            Assert.That(Engine.PlayerRerollsLeft, Is.EqualTo(2));
            Call("MarkNearMiss");
            // Either end can become a large straight; the hint chooses the first valid end.
            CollectionAssert.AreEqual(new[] { true, false, false, false, false }, Read<bool[]>("_rerollMarked"));
            Call("DoReroll");
            for (int i = 1; i < 5; i++) Assert.That(Engine.PlayerDice[i], Is.EqualTo(hand[i]));
            Assert.That(Engine.PlayerRerollsLeft, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModalBlocksBattleActions_AndItsRaycastCoversTheDice()
        {
            Call("StartMatch");
            int[] before = (int[])Engine.PlayerDice.Clone();
            Call("OpenMenu");
            Call("OnPlayerDieClicked", 0);
            Call("DoReroll");
            Call("DoCommit");
            Call("DoAdvance");
            Call("HandleShortcut", KeyCode.Alpha2);
            Call("HandleShortcut", KeyCode.Space);
            Assert.That(Engine.History.Count, Is.Zero);
            Assert.That(Engine.Round, Is.EqualTo(1));
            Assert.That(Engine.PlayerRerollsLeft, Is.EqualTo(2));
            CollectionAssert.AreEqual(before, Engine.PlayerDice);
            CollectionAssert.AreEqual(new bool[5], Read<bool[]>("_rerollMarked"));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            RectTransform die = Read<RectTransform>("_battleRoot").Find("PlayerDie0") as RectTransform;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, die.position)
            };
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.transform.IsChildOf(Read<RectTransform>("_menuRoot")), Is.True);
            Call("HandleShortcut", KeyCode.Escape);
            Call("HandleShortcut", KeyCode.Alpha2);
            Assert.That(Read<bool[]>("_rerollMarked")[1], Is.True);
        }

        [UnityTest]
        public IEnumerator CancellingLeaveKeepsTheDuel_ConfirmingLeaveReturnsToTitle()
        {
            Call("StartMatch");
            MatchEngine original = Engine;
            Call("OpenMenu");
            Press("_menuRoot", "ReturnTitle");
            Assert.That(Engine, Is.SameAs(original));
            Call("HandleShortcut", KeyCode.Escape);
            Assert.That(Engine, Is.SameAs(original));
            Call("OpenMenu");
            Press("_menuRoot", "ReturnTitle");
            Press("_menuRoot", "ReturnTitle");
            Assert.That(Engine, Is.Null);
            Assert.That(Read<RectTransform>("_titleRoot").gameObject.activeSelf, Is.True);
            Call("StartMatch");
            Assert.That(Engine, Is.Not.SameAs(original));
            Assert.That(Engine.PlayerHp, Is.EqualTo(32));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HistoryOnlyShowsRevealedHands_AndTabCannotSelectBehindTheOverlay()
        {
            Call("StartMatch");
            Call("OpenJournal");
            Assert.That(Read<Text>("_journalBody").text, Does.Contain("まだありません"));
            Assert.That(Engine.NpcDice, Is.Null);
            Call("HandleShortcut", KeyCode.Tab);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform
                .IsChildOf(Read<RectTransform>("_journalRoot")), Is.True);
            Call("HandleShortcut", KeyCode.Escape);
            Call("DoCommit");
            RoundOutcome round = Engine.LastRound;
            Call("OpenJournal");
            string journal = Read<Text>("_journalBody").text;
            Assert.That(journal, Does.Contain(Yaku.DisplayName(round.NpcYaku)));
            Assert.That(journal, Does.Contain("受けたダメージ " + round.DamageToPlayer));
            Assert.That(journal, Does.Contain("与えたダメージ " + round.DamageToNpc));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryOpponentCompletesAndRematchesThroughTheActualButtons()
        {
            for (int opponent = 0; opponent < 3; opponent++)
            {
                Call("SelectOpponent", opponent);
                Press("_titleRoot", "Start");
                for (int round = 0; round < 10 && Engine.Outcome == null; round++)
                {
                    Press("_battleRoot", "Commit");
                    if (Engine.Outcome == null) Press("_battleRoot", "Advance");
                }
                Assert.That(Engine.Outcome, Is.Not.Null);
                Press("_battleRoot", "Advance");
                Assert.That(Read<RectTransform>("_resultRoot").gameObject.activeSelf, Is.True);
                Press("_resultRoot", "Rematch");
                Assert.That(Engine.Outcome, Is.Null);
                Assert.That(Engine.PlayerHp, Is.EqualTo(32));
                Call("OpenMenu");
                Call("ReturnToTitle");
                Call("ReturnToTitle");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DestroyingTheFlowRemovesItsCanvasAndButtons()
        {
            GameObject canvas = Read<GameObject>("_canvasRoot");
            Object.Destroy(_flow.gameObject);
            yield return null;
            yield return null;
            Assert.That(canvas == null, Is.True, "an orphan canvas would keep obsolete input alive");
        }

        private T Read<T>(string name) => (T)typeof(GameFlow).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_flow);
        private void Call(string name, params object[] args) => typeof(GameFlow).GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_flow, args);
        private Button ButtonIn(string root, string name) => Read<RectTransform>(root).Find(name).GetComponent<Button>();
        private void Press(string root, string name)
        {
            Button button = ButtonIn(root, name);
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, name + " cannot be pressed");
            button.onClick.Invoke();
        }
    }
}
