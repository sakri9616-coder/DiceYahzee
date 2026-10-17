using System;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    public sealed class VersusDuelTests
    {
        private static void FinishRerolls(VersusDuel duel, long sequence = 10)
        {
            Assert.That(duel.TryFinishRerolls(0, duel.Round, sequence, 0, out _), Is.True);
            Assert.That(duel.TryFinishRerolls(1, duel.Round, sequence, 0, out _), Is.True);
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.ChoosingRoles));
        }

        /// <summary>Overwrites the host's live dice for a seat, bypassing the private roll stream.</summary>
        private static void SetDice(VersusDuel duel, int player, params int[] dice)
        {
            var players = (VersusPlayer[])typeof(VersusDuel)
                .GetField("players", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(duel);
            Array.Copy(dice, players[player].Dice, dice.Length);
        }

        /// <summary>Finishes rerolls for both seats and submits one legal role each so the round resolves.</summary>
        private static void SubmitRound(VersusDuel duel, ScoreHandKind kind0, ScoreHandKind kind1,
            int[] dice0 = null, int[] dice1 = null)
        {
            int round = duel.Round;
            long sequence = round * 100L;
            if (dice0 != null) SetDice(duel, 0, dice0);
            if (dice1 != null) SetDice(duel, 1, dice1);
            Assert.That(duel.TryFinishRerolls(0, round, sequence, 0, out _), Is.True);
            Assert.That(duel.TryFinishRerolls(1, round, sequence, 0, out _), Is.True);
            Assert.That(duel.TrySubmit(0, round, sequence + 1, kind0, false, 0, out _), Is.True);
            Assert.That(duel.TrySubmit(1, round, sequence + 1, kind1, false, 0, out _), Is.True);
        }

        [Test]
        public void InitialDiceVisibleThenRerollsRevealOnlyWhenBothFinish()
        {
            var duel = new VersusDuel(5); duel.Begin();
            int[] initial = duel.Players[0].Dice;
            Assert.That(duel.Snapshot(1)[0].Dice, Is.EqualTo(initial));
            Assert.That(duel.TrySubmit(0, 1, 1, ScoreHandKind.Pair, false, 0, out _), Is.False);
            Assert.That(duel.TryReroll(0, 1, 1, 0, out _), Is.True);
            Assert.That(duel.Snapshot(1)[0].Dice, Is.EqualTo(initial));
            Assert.That(duel.Snapshot(0)[0].Dice, Is.EqualTo(duel.Players[0].Dice));
            Assert.That(duel.TryFinishRerolls(0, 1, 2, 0, out _), Is.True);
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.Playing));
            Assert.That(duel.TryReroll(0, 1, 3, 0, out _), Is.False);
            Assert.That(duel.TryFinishRerolls(0, 1, 3, 0, out _), Is.False);
            Assert.That(duel.TrySubmit(0, 1, 3, ScoreHandKind.Pair, false, 0, out _), Is.False);
            Assert.That(duel.TryReroll(1, 1, 1, 0, out _), Is.True);
            Assert.That(duel.TryReroll(1, 1, 2, 0, out _), Is.True);
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.ChoosingRoles));
            Assert.That(duel.Snapshot(1)[0].Dice, Is.EqualTo(duel.Players[0].Dice));
            Assert.That(duel.Snapshot(0)[1].Dice, Is.EqualTo(duel.Players[1].Dice));
            Assert.That(duel.TryReroll(0, 1, 3, 0, out _), Is.False);
        }

        [Test]
        public void BothSubmitBeforeAnyDamageAndCarryPreservesDiceWithoutPenalty()
        {
            var duel = new VersusDuel(42); duel.Begin(); FinishRerolls(duel);
            int score0 = ScoreRules.Evaluate(ScoreHandKind.Chance, duel.Players[0].Dice).Score;
            int score1 = ScoreRules.Evaluate(ScoreHandKind.Chance, duel.Players[1].Dice).Score;
            Assert.That(duel.TrySubmit(0, 1, 11, ScoreHandKind.Chance, true, 5, out _), Is.True);
            Assert.That(duel.Players[0].Hp, Is.EqualTo(ScoreDuel.StartingHp));
            Assert.That(duel.TryReroll(0, 1, 12, 0, out _), Is.False);
            Assert.That(duel.TrySubmit(0, 1, 12, ScoreHandKind.Pair, false, 0, out _), Is.False);
            Assert.That(duel.TrySubmit(1, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(duel.LastRound.Damage[0], Is.EqualTo(score1 - Math.Min(score0, score1) / 2));
            Assert.That(duel.LastRound.Damage[1], Is.EqualTo(score0 - Math.Min(score0, score1) / 2));
            int[] carried = duel.Players[0].Dice;
            duel.NextRound();
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.Playing));
            Assert.That(duel.Players[0].RerollsFinished, Is.False);
            Assert.That(duel.Players[0].Dice, Is.EqualTo(carried));
            Assert.That(duel.Players[0].HeldMask, Is.EqualTo(5));
            FinishRerolls(duel, 12);
            Assert.That(duel.TrySubmit(0, 2, 13, ScoreHandKind.Pair, true, 0, out _), Is.False);
            Assert.That(duel.TrySubmit(0, 2, 13, ScoreHandKind.Chance, false, 0, out _), Is.False);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ChanceDeclarationsSwapDamageFormulaWithParity(bool chance0, bool chance1)
        {
            var duel = new VersusDuel(17); duel.Begin(); FinishRerolls(duel);
            var kind0 = chance0 ? ScoreHandKind.Chance : ScoreHandKind.Pair;
            var kind1 = chance1 ? ScoreHandKind.Chance : ScoreHandKind.Pair;
            int score0 = ScoreRules.Evaluate(kind0, duel.Players[0].Dice).Score;
            int score1 = ScoreRules.Evaluate(kind1, duel.Players[1].Dice).Score;
            Assert.That(duel.TrySubmit(0, 1, 11, kind0, true, 0, out _), Is.True);
            Assert.That(duel.TrySubmit(1, 1, 11, kind1, false, 0, out _), Is.True);
            int absorption = Math.Min(score0, score1) / 2;
            Assert.That(duel.LastRound.Damage[0], Is.EqualTo((chance0 != chance1 ? score0 : score1) - absorption));
            Assert.That(duel.LastRound.Damage[1], Is.EqualTo((chance0 != chance1 ? score1 : score0) - absorption));
        }

        [Test]
        public void InvalidCommandsAreAtomicAndSequencesRejectReplays()
        {
            var duel = new VersusDuel(5); duel.Begin();
            var original = duel.Players[0].Dice;
            Assert.That(duel.TryReroll(-1, 1, 1, 0, out _), Is.False);
            Assert.That(duel.TryReroll(0, 2, 1, 0, out _), Is.False);
            Assert.That(duel.TryReroll(0, 1, 1, 32, out _), Is.False);
            Assert.That(duel.TryReroll(0, 1, 1, 31, out _), Is.False);
            Assert.That(duel.Players[0].Dice, Is.EqualTo(original));
            Assert.That(duel.TryReroll(0, 1, 1, 1, out _), Is.True);
            Assert.That(duel.Players[0].Dice[0], Is.EqualTo(original[0]));
            Assert.That(duel.TryReroll(0, 1, 1, 0, out _), Is.False);
            Assert.That(duel.TryReroll(0, 1, 2, 0, out _), Is.True);
            Assert.That(duel.TryReroll(0, 1, 3, 0, out _), Is.False);
            Assert.That(duel.TryFinishRerolls(1, 1, 1, 0, out _), Is.True);
            Assert.That(duel.TrySubmit(0, 1, 3, (ScoreHandKind)99, false, 0, out _), Is.False);
            Assert.That(duel.TrySubmit(0, 1, 3, ScoreHandKind.Pair, false, 0, out _), Is.True);
        }

        [Test]
        public void SubmittedRoleIsPrivateButBothFinalHandsStayVisibleAndDetached()
        {
            var duel = new VersusDuel(5); duel.Begin(); FinishRerolls(duel);
            int face = duel.Players[0].Dice[0];
            duel.Players[0].Dice[0] = 99; duel.Players[0].Used[0] = true;
            Assert.That(duel.Players[0].Dice[0], Is.EqualTo(face));
            Assert.That(duel.TrySubmit(0, 1, 11, ScoreHandKind.Pair, true, 0, out _), Is.True);
            Assert.That(duel.Snapshot(1)[0].Submitted, Is.True);
            Assert.That(duel.Snapshot(1)[0].Dice, Is.EqualTo(duel.Players[0].Dice));
            Assert.That(duel.Snapshot(1)[0].Score, Is.Zero);
            Assert.That(duel.Snapshot(1)[0].Carry, Is.False);
            Assert.That(duel.Snapshot(1)[0].CarryUsed, Is.False);
            Assert.That(duel.Snapshot(1)[0].Used[(int)ScoreHandKind.Pair], Is.False);
            Assert.That(duel.Snapshot(0)[0].Used[(int)ScoreHandKind.Pair], Is.True);
        }

        [Test]
        public void SimultaneousKnockoutUsesRawHpAndFinishedRejectsActions()
        {
            var duel = new VersusDuel(8, 1); duel.Begin(); FinishRerolls(duel);
            duel.TrySubmit(0, 1, 11, ScoreHandKind.Chance, false, 0, out _);
            duel.TrySubmit(1, 1, 11, ScoreHandKind.Chance, false, 0, out _);
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.Finished));
            Assert.That(duel.Players[0].Hp, Is.Zero); Assert.That(duel.Players[1].Hp, Is.Zero);
            int[] raw = duel.LastRound.RawHpAfter;
            Assert.That(duel.Winner, Is.EqualTo(raw[0] == raw[1] ? 2 : raw[0] > raw[1] ? 0 : 1));
            Assert.Throws<InvalidOperationException>(() => duel.NextRound());
            Assert.That(duel.TryReroll(0, 1, 12, 0, out _), Is.False);
        }

        [Test]
        public void DisconnectFreezesPlanning()
        {
            var duel = new VersusDuel(7); duel.Begin(); duel.Interrupt();
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.Interrupted));
            Assert.That(duel.TrySubmit(0, 1, 1, ScoreHandKind.Chance, false, 0, out _), Is.False);
            Assert.That(duel.TryReroll(1, 1, 1, 0, out _), Is.False);
            Assert.That(duel.TryFinishRerolls(1, 1, 1, 0, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void FormedFiveGrantsOneExtraRerollToThatSeatOnlyForTheNextRound(int seat)
        {
            var duel = new VersusDuel(11, 10000); duel.Begin();
            int[] five = { 6, 6, 6, 6, 6 };
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.Five, five).IsFormed, Is.True);
            SubmitRound(duel,
                seat == 0 ? ScoreHandKind.Five : ScoreHandKind.Pair,
                seat == 1 ? ScoreHandKind.Five : ScoreHandKind.Pair,
                seat == 0 ? five : null,
                seat == 1 ? five : null);

            duel.NextRound();
            Assert.That(duel.Players[seat].RerollsLeft, Is.EqualTo(VersusDuel.MaxRerolls));
            Assert.That(duel.Players[1 - seat].RerollsLeft, Is.EqualTo(VersusDuel.BaseRerolls),
                "the seat that did not form a Five keeps the normal charge");
            // The online snapshot carries the extra charge for the owner and in the opponent's view.
            Assert.That(duel.Snapshot(-1)[seat].RerollsLeft, Is.EqualTo(VersusDuel.MaxRerolls));
            Assert.That(duel.Snapshot(1 - seat)[seat].RerollsLeft, Is.EqualTo(VersusDuel.MaxRerolls));

            // The host accepts exactly three legal rerolls and rejects a fourth.
            int round = duel.Round;
            long sequence = round * 100L;
            Assert.That(duel.TryReroll(seat, round, sequence + 1, 0, out _), Is.True);
            Assert.That(duel.Players[seat].RerollsLeft, Is.EqualTo(2));
            Assert.That(duel.TryReroll(seat, round, sequence + 2, 0, out _), Is.True);
            Assert.That(duel.Players[seat].RerollsLeft, Is.EqualTo(1));
            Assert.That(duel.TryReroll(seat, round, sequence + 3, 0, out _), Is.True);
            Assert.That(duel.Players[seat].RerollsLeft, Is.Zero);
            Assert.That(duel.Players[seat].RerollsFinished, Is.True);
            Assert.That(duel.TryReroll(seat, round, sequence + 4, 0, out _), Is.False);

            // Finish the round; the following round returns to the normal two charges.
            Assert.That(duel.TryFinishRerolls(1 - seat, round, sequence + 5, 0, out _), Is.True);
            Assert.That(duel.TrySubmit(0, round, sequence + 6,
                seat == 0 ? ScoreHandKind.Chance : ScoreHandKind.Three, false, 0, out _), Is.True);
            Assert.That(duel.TrySubmit(1, round, sequence + 6,
                seat == 1 ? ScoreHandKind.Chance : ScoreHandKind.Three, false, 0, out _), Is.True);
            duel.NextRound();
            Assert.That(duel.Players[seat].RerollsLeft, Is.EqualTo(VersusDuel.BaseRerolls),
                "the extra reroll must not carry beyond the immediately following round");
        }

        [Test]
        public void DeclaringAnUnformedFiveDoesNotGrantTheExtraReroll()
        {
            int[] flat = { 1, 2, 3, 4, 6 };
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.Five, flat).IsFormed, Is.False);
            var duel = new VersusDuel(11, 10000); duel.Begin();
            SubmitRound(duel, ScoreHandKind.Five, ScoreHandKind.Pair, flat);
            Assert.That(duel.Players[0].NextRerollBonus, Is.False);
            duel.NextRound();
            Assert.That(duel.Players[0].RerollsLeft, Is.EqualTo(VersusDuel.BaseRerolls));
        }

        [Test]
        public void PendingFormedFiveBonusStaysPrivateUntilResolution()
        {
            var duel = new VersusDuel(11, 10000); duel.Begin();
            SetDice(duel, 0, 4, 4, 4, 4, 4);
            Assert.That(duel.TryFinishRerolls(0, 1, 100, 0, out _), Is.True);
            Assert.That(duel.TryFinishRerolls(1, 1, 100, 0, out _), Is.True);
            Assert.That(duel.TrySubmit(0, 1, 101, ScoreHandKind.Five, false, 0, out _), Is.True);
            Assert.That(duel.Phase, Is.EqualTo(VersusPhase.ChoosingRoles));
            Assert.That(duel.Snapshot(0)[0].NextRerollBonus, Is.True);
            Assert.That(duel.Snapshot(1)[0].NextRerollBonus, Is.False);
            Assert.That(duel.TrySubmit(1, 1, 101, ScoreHandKind.Pair, false, 0, out _), Is.True);
            Assert.That(duel.Snapshot(1)[0].NextRerollBonus, Is.True);
        }

        [Test]
        public void FormedFiveCanAwardAgainAfterNineRoleInventoryRefresh()
        {
            var duel = new VersusDuel(23, 10000); duel.Begin();
            // Round 1: a formed Five earns the bonus for round 2.
            SubmitRound(duel, ScoreHandKind.Five, ScoreHandKind.Pair, new[] { 2, 2, 2, 2, 2 });
            duel.NextRound();
            Assert.That(duel.Players[0].RerollsLeft, Is.EqualTo(VersusDuel.MaxRerolls));

            // Spend the eight non-Five categories over rounds 2..9 so the nine-slot inventory resets.
            var others = new[]
            {
                ScoreHandKind.Pair, ScoreHandKind.TwoPair, ScoreHandKind.Three, ScoreHandKind.FullHouse,
                ScoreHandKind.SmallStraight, ScoreHandKind.Four, ScoreHandKind.LargeStraight, ScoreHandKind.Chance
            };
            for (int i = 0; i < others.Length; i++)
            {
                var enemy = (ScoreHandKind)((duel.Round - 1) % ScoreRules.HandCount);
                SubmitRound(duel, others[i], enemy);
                duel.NextRound();
            }
            Assert.That(duel.Players[0].Used[(int)ScoreHandKind.Five], Is.False, "the inventory must have refreshed");
            Assert.That(duel.Players[0].RerollsLeft, Is.EqualTo(VersusDuel.BaseRerolls), "the old bonus must not linger");

            // Round 10: a formed Five after the refresh pays out again.
            SubmitRound(duel, ScoreHandKind.Five, ScoreHandKind.Pair, new[] { 5, 5, 5, 5, 5 });
            duel.NextRound();
            Assert.That(duel.Players[0].RerollsLeft, Is.EqualTo(VersusDuel.MaxRerolls));
        }

        [Test]
        public void FormedFiveAwardsEvenWhenItCompletesTheInventoryAndTriggersRefresh()
        {
            var duel = new VersusDuel(31, 10000); duel.Begin();
            var firstEight = new[]
            {
                ScoreHandKind.Pair, ScoreHandKind.TwoPair, ScoreHandKind.Three, ScoreHandKind.FullHouse,
                ScoreHandKind.SmallStraight, ScoreHandKind.Four, ScoreHandKind.LargeStraight, ScoreHandKind.Chance
            };
            for (int i = 0; i < firstEight.Length; i++)
            {
                var enemy = (ScoreHandKind)((duel.Round - 1) % ScoreRules.HandCount);
                SubmitRound(duel, firstEight[i], enemy);
                duel.NextRound();
            }
            // Round 9: Five is the ninth and final category, so the very next Prepare resets the inventory.
            SubmitRound(duel, ScoreHandKind.Five,
                (ScoreHandKind)((duel.Round - 1) % ScoreRules.HandCount), new[] { 6, 6, 6, 6, 6 });
            duel.NextRound();
            Assert.That(duel.Players[0].Used[(int)ScoreHandKind.Five], Is.False, "the inventory was reset");
            Assert.That(duel.Players[0].RerollsLeft, Is.EqualTo(VersusDuel.MaxRerolls),
                "the refresh must not swallow the earned reroll");
        }

        [Test]
        public void IdenticalKnockoutDamageIsTheMiracleDrawWhileAnyMismatchDecides()
        {
            // Both seats mirror the same hand, so both scores, both damages and therefore both
            // raw knockout readings land exactly even: winner 2 is the draw, not a coin flip.
            var drawn = new VersusDuel(8, 1); drawn.Begin();
            SetDice(drawn, 0, 3, 3, 3, 3, 3);
            SetDice(drawn, 1, 3, 3, 3, 3, 3);
            FinishRerolls(drawn);
            Assert.That(drawn.TrySubmit(0, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(drawn.TrySubmit(1, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(drawn.Phase, Is.EqualTo(VersusPhase.Finished));
            Assert.That(drawn.LastRound.RawHpAfter[0], Is.EqualTo(drawn.LastRound.RawHpAfter[1]));
            Assert.That(drawn.Winner, Is.EqualTo(2), "equal raw health on a simultaneous knockout is a draw");
            Assert.Throws<InvalidOperationException>(() => drawn.NextRound());

            // The mirror image with one die higher breaks the tie: the bigger blow wins it.
            var decided = new VersusDuel(8, 1); decided.Begin();
            SetDice(decided, 0, 4, 4, 4, 4, 4);
            SetDice(decided, 1, 3, 3, 3, 3, 3);
            FinishRerolls(decided);
            Assert.That(decided.TrySubmit(0, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(decided.TrySubmit(1, 1, 11, ScoreHandKind.Chance, false, 0, out _), Is.True);
            Assert.That(decided.Phase, Is.EqualTo(VersusPhase.Finished));
            Assert.That(decided.LastRound.RawHpAfter[0], Is.GreaterThan(decided.LastRound.RawHpAfter[1]));
            Assert.That(decided.Winner, Is.EqualTo(0), "the seat left with the higher raw health wins");
        }
    }
}
