using System;
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
    /// <summary>
    /// Display-level coverage for the two rare online result cues: the Chance swap
    /// announcement while it can still be changed and after it lands, and the
    /// simultaneous-knockout "奇跡" title. Each test drives a real VersusDuel through
    /// the real host serialization (OnlineJson) and the real client receive path
    /// (ReceiveOnlineSnapshot), so a genuine connection only adds the transport that
    /// is already covered by OnlineProtocolTests.
    /// </summary>
    public sealed class OnlineResultDisplayTests
    {
        private GameFlow _flow;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Text ChanceCue => Read<Text>("_onlineChanceCue");
        private Text DrawMiracle => Read<Text>("_onlineDrawMiracle");
        private Text Status => Read<Text>("_onlineStatus");

        [UnitySetUp] public IEnumerator SetUp()
        {
            GameFlow.ConfigOverride = null;
            GameFlow.LegacyPresentationForTests = false;
            GameFlow.SeedOverride = () => 617;
            foreach (var old in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            Screen.SetResolution(1280, 720, false);
            yield return null;
            yield return null;
            _flow = new GameObject("OnlineResultUnderTest").AddComponent<GameFlow>();
            yield return null;
            Call("OpenOnline");
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            GameFlow.ConfigOverride = null;
            GameFlow.SeedOverride = null;
            GameFlow.LegacyPresentationForTests = false;
            if (_flow != null) Object.Destroy(_flow.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChanceCueNamesTheSwapWhileDeclaredAndAgainOnceItLands()
        {
            var duel = new VersusDuel(2027, 60);
            duel.Begin();
            FinishRerolls(duel);
            // Round still undecided and nothing declared: no cue may tease a swap.
            Publish(duel);
            yield return null;
            Assert.That(ChanceCue.gameObject.activeSelf, Is.False);
            Assert.That(ChanceCue.text, Is.Empty);

            // My own Chance declaration is visible to me at once and names the swap.
            Assert.That(duel.TrySubmit(0, 1, 11, ScoreHandKind.Chance, false, 0, out string mineError), Is.True, mineError);
            Publish(duel);
            yield return null;
            Assert.That(ChanceCue.gameObject.activeSelf, Is.True, "declaring Chance must warn that the damage target will swap");
            Assert.That(ChanceCue.text, Is.EqualTo("チャンス：被害の受け手を入れ替える"));

            // Exactly one side declared Chance when the round lands: the swap has happened.
            Assert.That(duel.TrySubmit(1, 1, 11, ScoreHandKind.Pair, false, 0, out string enemyError), Is.True, enemyError);
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.Resolved));
            Publish(duel);
            yield return null;
            Assert.That(ChanceCue.gameObject.activeSelf, Is.True);
            Assert.That(ChanceCue.text, Is.EqualTo("被害の受け手が入れ替わった"));

            // Both sides declaring Chance cancels the swap: nobody's damage target moves.
            var even = new VersusDuel(2028, 60);
            even.Begin();
            FinishRerolls(even);
            Assert.That(even.TrySubmit(0, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(even.TrySubmit(1, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(even.Phase, Is.EqualTo(VersusPhase.Resolved));
            Publish(even);
            yield return null;
            Assert.That(ChanceCue.gameObject.activeSelf, Is.False, "a matched pair of declarations changes nobody's target");
            Assert.That(ChanceCue.text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EqualKnockoutDamageShowsTheMiracleTitleAndMismatchDoesNot()
        {
            // Identical hands on both seats: equal scores, equal damages, equal raw health.
            var drawn = new VersusDuel(8, 1);
            drawn.Begin();
            SetDice(drawn, 0, 3, 3, 3, 3, 3);
            SetDice(drawn, 1, 3, 3, 3, 3, 3);
            FinishRerolls(drawn);
            Assert.That(drawn.TrySubmit(0, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(drawn.TrySubmit(1, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(drawn.Phase, Is.EqualTo(VersusPhase.Finished));
            Assert.That(drawn.Winner, Is.EqualTo(2));

            Publish(drawn);
            yield return null;
            Assert.That(DrawMiracle.gameObject.activeSelf, Is.True, "an equal raw-health knockout deserves the 奇跡 title");
            Assert.That(DrawMiracle.text, Is.EqualTo("奇跡"));
            Assert.That(Status.text, Is.EqualTo("引き分け"));
            Assert.That(ChanceCue.gameObject.activeSelf, Is.False, "a matched pair of Chance declarations is not a swap");

            // One die higher breaks the tie: player 0 wins, no miracle, plain victory text.
            var decided = new VersusDuel(8, 1);
            decided.Begin();
            SetDice(decided, 0, 4, 4, 4, 4, 4);
            SetDice(decided, 1, 3, 3, 3, 3, 3);
            FinishRerolls(decided);
            Assert.That(decided.TrySubmit(0, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(decided.TrySubmit(1, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(decided.Phase, Is.EqualTo(VersusPhase.Finished));
            Assert.That(decided.Winner, Is.EqualTo(0));

            Publish(decided);
            yield return null;
            Assert.That(DrawMiracle.gameObject.activeSelf, Is.False);
            Assert.That(Status.text, Is.EqualTo("あなたの勝利"));
        }

        private static void FinishRerolls(VersusDuel duel)
        {
            Assert.That(duel.TryFinishRerolls(0, 1, 10, 0, out string error0), Is.True, error0);
            Assert.That(duel.TryFinishRerolls(1, 1, 10, 0, out string error1), Is.True, error1);
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.ChoosingRoles));
        }

        /// <summary>Overwrites a seat's live dice, bypassing the private roll stream, so the
        /// two rare result paths are pinned instead of left to a lucky seed. It does not touch
        /// VersusDuel's booked revealedDice copies, so assertions must only trust these dice
        /// from the ChooseRoles phase onward, when the engine serves live rolls again.</summary>
        private static void SetDice(VersusDuel duel, int player, params int[] dice)
        {
            var players = (VersusPlayer[])typeof(VersusDuel)
                .GetField("players", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duel);
            Array.Copy(dice, players[player].Dice, dice.Length);
        }

        /// <summary>Sends the duel through the host's real serialization and the client's real
        /// receive path, viewed from seat 0 (OnlineMe is 0 while no session object exists).</summary>
        private void Publish(VersusDuel duel)
        {
            Write("_onlineDuel", duel);
            Write("_onlineRevision", Read<long>("_onlineRevision") + 1);
            string json = (string)typeof(GameFlow).GetMethod("OnlineJson", Private).Invoke(_flow, new object[] { 0 });
            typeof(GameFlow).GetMethod("ReceiveOnlineSnapshot", Private).Invoke(_flow, new object[] { json });
        }

        private T Read<T>(string name) => (T)typeof(GameFlow).GetField(name, Private).GetValue(_flow);
        private void Write(string name, object value) => typeof(GameFlow).GetField(name, Private).SetValue(_flow, value);
        private void Call(string method, params object[] args) => typeof(GameFlow).GetMethod(method, Private).Invoke(_flow, args);
    }
}
