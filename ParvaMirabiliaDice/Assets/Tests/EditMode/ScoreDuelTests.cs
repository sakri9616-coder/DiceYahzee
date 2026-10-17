using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    public sealed class ScoreDuelTests
    {
        [TestCase(2, 2, 1, 4, 6, 3)]
        [TestCase(1, 2, 3, 4, 6, 15)]
        [TestCase(1, 2, 3, 5, 6, 24)]
        [TestCase(5, 5, 5, 2, 2, 31)]
        public void BasicEnemyKeepsOnlyVisibleGroupsStraightsOrHighFaces(int a, int b, int c, int d, int e, int mask)
        {
            var available = new bool[9];
            for (int i = 0; i < 9; i++) available[i] = true;
            var plan = typeof(ScoreDuel).GetMethod("BasicHoldMask", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(plan.Invoke(null, new object[] { new[] { a, b, c, d, e }, available }), Is.EqualTo(mask));
        }

        [TestCase(NpcPersonality.Balanced, 1)]
        [TestCase(NpcPersonality.Aggressive, 2)]
        [TestCase(NpcPersonality.Careful, 2)]
        [TestCase(NpcPersonality.Expert, 2)]
        public void NpcHistoryReplaysOnlyRealRandomDrawsAndRetainsHeldDice(NpcPersonality personality, int limit)
        {
            for (int seed = 0; seed < 12; seed++)
            {
                var game = new ScoreDuel(seed, personality);
                var random = new SeededRandom(unchecked(seed ^ (int)0xA837CF13));
                for (int round = 0; round < 3 && game.Phase != MatchPhase.Finished; round++)
                {
                    var rolls = game.PlannedNpcRolls;
                    Assert.That(rolls.Count, Is.InRange(1, limit + 1));
                    Assert.That(rolls[0].HeldMask, Is.Zero);
                    Assert.Throws<NotSupportedException>(() => ((IList<NpcRollSnapshot>)rolls).Clear());
                    for (int step = 0; step < rolls.Count; step++)
                    {
                        Assert.Throws<NotSupportedException>(() => ((IList<int>)rolls[step].Dice)[0] = 6);
                        for (int die = 0; die < 5; die++)
                        {
                            int expected = step > 0 && (rolls[step].HeldMask & (1 << die)) != 0
                                ? rolls[step - 1].Dice[die] : random.Range(1, 7);
                            Assert.That(rolls[step].Dice[die], Is.EqualTo(expected));
                            Assert.That(expected, Is.InRange(1, 6));
                        }
                    }
                    Assert.That(game.PlannedNpcDice, Is.EqualTo(rolls[rolls.Count - 1].Dice));
                    game.Roll();
                    var receipt = game.Commit(ScoreRules.Best(game.PlayerDice, Available(game)).Kind);
                    Assert.That(receipt.NpcRolls, Is.SameAs(rolls));
                    Assert.That(receipt.NpcRerollsUsed, Is.EqualTo(rolls.Count - 1));
                    if (game.Phase != MatchPhase.Finished) game.NextRound();
                    Assert.That(receipt.NpcDice, Is.EqualTo(rolls[rolls.Count - 1].Dice));
                }
            }
        }

        [Test]
        public void ExpertRetainsThePreviousExactBalancedPolicy()
        {
            var available = new bool[9];
            for (int i = 0; i < 9; i++) available[i] = true;
            for (int seed = 0; seed < 12; seed++)
            {
                var random = new SeededRandom(unchecked(seed ^ (int)0xA837CF13));
                var expected = new int[5];
                for (int i = 0; i < 5; i++) expected[i] = random.Range(1, 7);
                for (int reroll = 0; reroll < 2; reroll++)
                {
                    var held = ScoreRules.SuggestHeld(expected, available, 2 - reroll);
                    bool all = true;
                    for (int i = 0; i < 5; i++) all &= held[i];
                    if (all) break;
                    for (int i = 0; i < 5; i++) if (!held[i]) expected[i] = random.Range(1, 7);
                }
                Assert.That(new ScoreDuel(seed, NpcPersonality.Expert).PlannedNpcDice, Is.EqualTo(expected));
            }
        }

        [TestCase(ScoreHandKind.Pair, 6)]
        [TestCase(ScoreHandKind.Three, 8)]
        [TestCase(ScoreHandKind.Four, 12)]
        public void MatchingFacesHaveTheSameMultiplierWhileRawFaceValuesStillMatter(ScoreHandKind kind, int expected)
        {
            int count = kind == ScoreHandKind.Pair ? 2 : kind == ScoreHandKind.Three ? 3 : kind == ScoreHandKind.Four ? 4 : 5;
            var low = new[] { 2, 3, 4, 5, 6 };
            var high = new[] { 1, 2, 3, 4, 5 };
            for (int i = 0; i < count; i++) { low[i] = 1; high[i] = 6; }
            var lowOffer = ScoreRules.Evaluate(kind, low);
            var highOffer = ScoreRules.Evaluate(kind, high);
            Assert.That(ScoreRules.MultiplierQuarters(kind), Is.EqualTo(expected));
            Assert.That(lowOffer.Score, Is.EqualTo(lowOffer.BaseScore * expected / 4));
            Assert.That(highOffer.Score, Is.EqualTo(highOffer.BaseScore * expected / 4));
            Assert.That(highOffer.Score, Is.GreaterThan(lowOffer.Score));
        }

        [Test]
        public void DistinctPairsAndExactFullHouseDoNotAcceptOneLargeGroup()
        {
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.TwoPair, new[] { 1, 1, 2, 2, 3 }).Score, Is.EqualTo(58));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.FullHouse, new[] { 1, 1, 1, 2, 2 }).Score, Is.EqualTo(67));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.FullHouse, new[] { 6, 6, 6, 5, 5 }).Score, Is.EqualTo(120));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.TwoPair, new[] { 1, 1, 1, 1, 2 }).IsFormed, Is.False);
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.FullHouse, new[] { 1, 1, 1, 1, 1 }).IsFormed, Is.False);
        }

        [Test]
        public void StraightMasksContainOneOfEachNecessaryFaceAndNeverDuplicates()
        {
            var small = ScoreRules.Evaluate(ScoreHandKind.SmallStraight, new[] { 2, 3, 3, 4, 5 });
            Assert.That(small.Score, Is.EqualTo(74));
            Assert.That(SplitDecision.CountBits(small.Mask), Is.EqualTo(4));
            Assert.That((small.Mask & 2) != 0, Is.Not.EqualTo((small.Mask & 4) != 0));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.LargeStraight, new[] { 2, 3, 3, 4, 5 }).Score, Is.EqualTo(37));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.LargeStraight, new[] { 6, 2, 5, 3, 4 }).Score, Is.EqualTo(100));
        }

        [Test]
        public void ChanceIsASingleUseSafetyNetRatherThanAnAlwaysAvailableAttack()
        {
            var game = new ScoreDuel(713);
            game.Roll();
            SetDice(game, 3, 3, 3, 3, 3);
            var receipt = game.Commit(ScoreHandKind.Chance);
            Assert.That(receipt.PlayerScore, Is.EqualTo(35));
            Assert.That(game.PlayerUsed[(int)ScoreHandKind.Chance], Is.True);
            game.NextRound(); game.Roll();
            int playerHp = game.PlayerHp, npcHp = game.NpcHp;
            Assert.Throws<InvalidOperationException>(() => game.Commit(ScoreHandKind.Chance));
            Assert.That(game.PlayerHp, Is.EqualTo(playerHp));
            Assert.That(game.NpcHp, Is.EqualTo(npcHp));
            Assert.That(game.History.Count, Is.EqualTo(1));
            Assert.That(game.Offers[(int)ScoreHandKind.Chance].IsAvailable, Is.False);
        }

        [Test]
        public void AnUnformedCategoryKeepsTheBaseAndDiceTotalAndConsumesItsSlot()
        {
            var game = new ScoreDuel(75);
            game.Roll(); SetDice(game, 1, 2, 3, 5, 6);
            var offer = game.Offers[(int)ScoreHandKind.Five];
            Assert.That(offer.IsAvailable, Is.True);
            Assert.That(offer.IsFormed, Is.False);
            Assert.That(offer.Score, Is.EqualTo(37));
            Assert.That(offer.BonusScore, Is.Zero);
            var receipt = game.Commit(ScoreHandKind.Five);
            Assert.That(receipt.PlayerScore, Is.EqualTo(37));
            Assert.That(game.PlayerUsed[(int)ScoreHandKind.Five], Is.True);
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(new ScoreForecast(37, game.Intent.Score).DamageToPlayer));
        }

        [Test]
        public void HoldsRerollsAndIllegalOperationsCannotSpendExtraRolls()
        {
            var game = new ScoreDuel(33);
            Assert.Throws<InvalidOperationException>(() => game.Commit(ScoreHandKind.Chance));
            game.Roll();
            var before = (int[])game.PlayerDice.Clone();
            game.SetHeld(new[] { true, true, true, true, true });
            Assert.Throws<InvalidOperationException>(() => game.Reroll());
            Assert.That(game.RerollsLeft, Is.EqualTo(2));
            game.SetHeld(new[] { true, false, false, false, false });
            Assert.That(game.Reroll(), Is.EqualTo(4));
            Assert.That(game.PlayerDice[0], Is.EqualTo(before[0]));
            game.Reroll();
            Assert.That(game.RerollsLeft, Is.Zero);
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.AwaitingSplit));
            Assert.Throws<InvalidOperationException>(() => game.Reroll());
            game.Commit(ScoreHandKind.Chance);
            Assert.That(game.LastRound.PlayerRerollsUsed, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => game.Commit(ScoreHandKind.Pair));
        }

        [Test]
        public void NpcDiceArePublicReadOnlyAndLockedBeforeThePlayerRolls()
        {
            var game = new ScoreDuel(104);
            var intent = game.Intent;
            Assert.That(intent, Is.Not.Null);
            Assert.That(game.NpcDice, Is.Null);
            var planned = new int[5];
            for (int i = 0; i < 5; i++) planned[i] = game.PlannedNpcDice[i];
            Assert.That(ScoreRules.Evaluate(intent.Kind, planned).Score, Is.EqualTo(intent.Score));
            Assert.Throws<NotSupportedException>(() => ((IList<int>)game.PlannedNpcDice)[0] = 1);
            game.Roll(); game.Reroll();
            Assert.That(game.Intent, Is.SameAs(intent));
            Assert.That(game.PlannedNpcDice, Is.EqualTo(planned));
            Assert.That(game.NpcDice, Is.Null);
            SetDice(game, 6, 6, 6, 6, 6);
            var forecast = game.Forecast(ScoreHandKind.Five);
            var receipt = game.Commit(ScoreHandKind.Five);
            Assert.That(receipt.NpcKind, Is.EqualTo(intent.Kind));
            Assert.That(ScoreRules.Evaluate(receipt.NpcKind, receipt.NpcDice).Score, Is.EqualTo(intent.Score));
            Assert.That(receipt.DamageToNpc, Is.EqualTo(forecast.DamageToNpc));
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(forecast.DamageToPlayer));
            Assert.That(receipt.PlayerBaseScore + receipt.PlayerBonusScore, Is.EqualTo(receipt.PlayerScore));
            Assert.That(receipt.NpcDice, Is.EqualTo(planned));
            Assert.That(game.NpcDice, Is.Not.Null);
            Assert.That(game.NpcUsed[(int)intent.Kind], Is.True);
        }

        [Test]
        public void BothSidesTakeTheirAttackLessTheSameAbsorption()
        {
            var forward = new ScoreForecast(44, 24);
            var reverse = new ScoreForecast(24, 44);
            var tie = new ScoreForecast(32, 32);
            Assert.That(forward.DamageToNpc, Is.EqualTo(32));
            Assert.That(forward.DamageToPlayer, Is.EqualTo(12));
            Assert.That(reverse.DamageToPlayer, Is.EqualTo(32));
            Assert.That(reverse.DamageToNpc, Is.EqualTo(12));
            Assert.That(tie.DamageToNpc, Is.EqualTo(16));
            Assert.That(tie.DamageToPlayer, Is.EqualTo(16));
        }

        [Test]
        public void IdenticalHandsContinueUntilDoubleKnockoutWithoutAHpJudgement()
        {
            var game = new ScoreDuel(509, npcStartingHp: ScoreDuel.StartingHp);
            while (game.Phase != MatchPhase.Finished)
            {
                game.Roll(); MirrorNpcHand(game);
                game.Commit(game.Intent.Kind);
                if (game.Phase != MatchPhase.Finished) game.NextRound();
            }
            Assert.That(game.Outcome.Result, Is.EqualTo(MatchResult.Draw));
            Assert.That(game.Outcome.Rounds, Is.EqualTo(game.Round));
            Assert.That(game.PlayerHp, Is.Zero);
            Assert.That(game.NpcHp, Is.Zero);
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.Finished));
            Assert.Throws<InvalidOperationException>(() => game.NextRound());
        }

        [Test]
        public void ZeroHpEndsImmediatelyAndNormalMatchesFinishByTheSafetyBound()
        {
            var game = new ScoreDuel(991);
            game.Roll(); MirrorNpcHand(game);
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, 1);
            game.Commit(game.Intent.Kind);
            Assert.That(game.Outcome.Result, Is.EqualTo(MatchResult.NpcWin));
            Assert.That(game.Outcome.Rounds, Is.EqualTo(1));
            Assert.That(game.PlayerHp, Is.Zero);
            Assert.That(game.Outcome.Reason, Is.EqualTo("あなたの体力が0になりました"));
            for (int seed = 0; seed < 30; seed++)
            {
                game = new ScoreDuel(seed);
                while (game.Phase != MatchPhase.Finished)
                {
                    Assert.That(game.Round, Is.LessThanOrEqualTo(ScoreDuel.SafetyRoundBound));
                    game.Roll();
                    game.Commit(ScoreRules.Best(game.PlayerDice, Available(game)).Kind);
                    if (game.Phase != MatchPhase.Finished) game.NextRound();
                }
                Assert.That(game.PlayerHp == 0 || game.NpcHp == 0, Is.True);
            }
        }

        [Test]
        public void NpcFutureDiceAreIndependentOfHumanRerollConsumption()
        {
            var early = new ScoreDuel(818);
            var many = new ScoreDuel(818);
            foreach (var duel in new[] { early, many })
            {
                typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(duel, 1000);
                typeof(ScoreDuel).GetProperty("NpcHp").SetValue(duel, 1000);
            }
            for (int round = 0; round < 6; round++)
            {
                Assert.That(early.Intent.Kind, Is.EqualTo(many.Intent.Kind));
                Assert.That(early.Intent.Score, Is.EqualTo(many.Intent.Score));
                early.Roll(); many.Roll(); many.Reroll(); many.Reroll();
                MirrorNpcHand(early); MirrorNpcHand(many);
                early.Commit(early.Intent.Kind); many.Commit(many.Intent.Kind);
                Assert.That(early.NpcDice, Is.EqualTo(many.NpcDice));
                if (round < 5) { early.NextRound(); many.NextRound(); }
            }
        }

        [Test]
        public void HighDurabilityDifficultyRaisesOnlyTheEnemyHealth()
        {
            var standard = new ScoreDuel(104);
            Assert.That(standard.NpcStartingHp, Is.EqualTo(ScoreDuel.DefaultNpcStartingHp));
            Assert.That(standard.NpcHp, Is.EqualTo(ScoreDuel.DefaultNpcStartingHp));

            var tough = new ScoreDuel(104, NpcPersonality.Expert, ScoreDuel.EnhancedStartingHp);
            Assert.That(tough.PlayerHp, Is.EqualTo(ScoreDuel.StartingHp));
            Assert.That(tough.NpcMaxHp, Is.EqualTo(ScoreDuel.EnhancedStartingHp));
            Assert.That(tough.NpcHp, Is.EqualTo(ScoreDuel.EnhancedStartingHp));
            Assert.That(tough.NpcHp, Is.GreaterThan(tough.PlayerHp));
            // The same seed and personality still plan the same enemy hand; only the pool moved.
            Assert.That(tough.Intent.Score, Is.EqualTo(new ScoreDuel(104, NpcPersonality.Expert).Intent.Score));

            // A rematch keeps the chosen pool instead of silently shrinking it back.
            tough.Begin();
            Assert.That(tough.NpcHp, Is.EqualTo(ScoreDuel.EnhancedStartingHp));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreDuel(1, NpcPersonality.Balanced, 0));
        }

        [Test]
        public void ReceiptsKeepTheirDiceAndResetClearsScorecardsAndResult()
        {
            var game = new ScoreDuel(92);
            game.Roll(); MirrorNpcHand(game); game.Commit(game.Intent.Kind);
            var receipt = game.LastRound;
            int value = receipt.PlayerDice[0];
            game.PlayerDice[0] = 6;
            Assert.That(receipt.PlayerDice[0], Is.EqualTo(value));
            game.NextRound(); game.Roll();
            Assert.That(receipt.PlayerDice[0], Is.EqualTo(value));
            game.Begin();
            Assert.That(game.History.Count, Is.Zero);
            Assert.That(game.Outcome, Is.Null);
            Assert.That(game.LastRound, Is.Null);
            Assert.That(game.NpcDice, Is.Null);
            Assert.That(game.PlayerHp, Is.EqualTo(300));
            Assert.That(game.NpcHp, Is.EqualTo(250));
            for (int i = 0; i < ScoreRules.HandCount; i++)
            {
                Assert.That(game.PlayerUsed[i], Is.False);
                Assert.That(game.NpcUsed[i], Is.False);
            }
        }

        [Test]
        public void PatternSuggestionPreservesAStrongLowGroupAndNeverChangesTheInputDice()
        {
            var dice = new[] { 1, 1, 1, 1, 6 };
            var available = new bool[9];
            available[(int)ScoreHandKind.Four] = available[(int)ScoreHandKind.Five] = true;
            var held = ScoreRules.SuggestHeld(dice, available, 1, 72);
            Assert.That(held, Is.EqualTo(new[] { true, true, true, true, false }));
            Assert.That(dice, Is.EqualTo(new[] { 1, 1, 1, 1, 6 }));
            Assert.That(ScoreRules.SuggestHeld(dice, available, 0), Is.EqualTo(new[] { true, true, true, true, true }));
            Assert.That(ScoreRules.Best(dice, available).Kind, Is.EqualTo(ScoreHandKind.Four));
        }

        [Test]
        public void ExactPolicyKeepsAFullHouseWithOneRerollButChasesFourWithTwo()
        {
            var available = new[] { true, true, true, true, true, true, true, true, true };
            var dice = new[] { 6, 6, 6, 4, 4 };

            // The decision is pure score expected value. The full house already banked here is
            // worth 115, but a formed four of a kind (144) and a formed yahtzee (fixed 150) both
            // pay more, so rerolling the two fours is only a question of how many shots remain.
            var fullHouse = ScoreRules.Evaluate(ScoreHandKind.FullHouse, dice);
            var four = ScoreRules.Evaluate(ScoreHandKind.Four, new[] { 6, 6, 6, 6, 4 });
            var yahtzee = ScoreRules.Evaluate(ScoreHandKind.Five, new[] { 6, 6, 6, 6, 6 });
            Assert.That(fullHouse.Score, Is.EqualTo(115));
            Assert.That(four.Score, Is.EqualTo(144));
            Assert.That(yahtzee.Score, Is.EqualTo(150));

            // One reroll left: a single shot at a fourth six averages below the banked 115
            // (exact expected value 107.5), so every die stays.
            Assert.That(ScoreRules.SuggestHeld(dice, available, 1),
                Is.EqualTo(new[] { true, true, true, true, true }));

            // Two rerolls left: the extra attempt lifts the expected score above the banked 115
            // (exact expected value 119.4), so only the three sixes stay and both fours are rerolled.
            Assert.That(ScoreRules.SuggestHeld(dice, available, 2),
                Is.EqualTo(new[] { true, true, true, false, false }));

            // With no rerolls left there is nothing to chase.
            Assert.That(ScoreRules.SuggestHeld(dice, available, 0),
                Is.EqualTo(new[] { true, true, true, true, true }));

            // The exact plan is seed-independent; the parameter only exists for the callers.
            Assert.That(ScoreRules.SuggestHeld(dice, available, 2, 655),
                Is.EqualTo(ScoreRules.SuggestHeld(dice, available, 2, -99)));
            Assert.That(ScoreRules.SuggestHeld(dice, available, 2, 655),
                Is.EqualTo(ScoreRules.SuggestHeld(dice, available, 2)));
        }

        [Test]
        public void EachExhaustedScorecardRefreshesIndependentlyBeforeTheNextTurn()
        {
            var game = new ScoreDuel(509);
            game.Roll(); MirrorNpcHand(game); game.Commit(game.Intent.Kind);
            var used = (bool[])typeof(ScoreDuel).GetField("_playerUsed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            for (int i = 0; i < used.Length; i++) used[i] = true;
            var npcBefore = new bool[9];
            for (int i = 0; i < 9; i++) npcBefore[i] = game.NpcUsed[i];
            game.NextRound();
            for (int i = 0; i < 9; i++)
            {
                Assert.That(game.PlayerUsed[i], Is.False);
                Assert.That(game.NpcUsed[i], Is.EqualTo(npcBefore[i]));
            }
        }

        [Test]
        public void NineConsumedCategoriesRefreshWithoutForcingAMatchResult()
        {
            var game = new ScoreDuel(509);
            // Isolate category lifecycle from the guaranteed knockout bound.
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, 1000);
            typeof(ScoreDuel).GetProperty("NpcHp").SetValue(game, 1000);
            for (int round = 1; round <= 9; round++)
            {
                game.Roll(); MirrorNpcHand(game); game.Commit(game.Intent.Kind); game.NextRound();
            }
            Assert.That(game.Round, Is.EqualTo(10));
            Assert.That(game.Outcome, Is.Null);
            for (int i = 0; i < 9; i++)
            {
                Assert.That(game.PlayerUsed[i], Is.False);
                Assert.That(game.NpcUsed[i], Is.False);
            }
        }

        [Test]
        public void SaveUsesNormalDamageThenRestoresDiceAndHolds()
        {
            var game = new ScoreDuel(104);
            game.Roll(); SetDice(game, 6, 6, 6, 6, 6);
            var held = new[] { true, true, true, false, false };
            game.SetHeld(held);
            int enemyScore = game.Intent.Score;
            var forecast = game.Forecast(ScoreHandKind.Three, true);
            var receipt = game.SaveHand(ScoreHandKind.Three);
            Assert.That(receipt.IsSaved, Is.True);
            Assert.That(receipt.PlayerScore, Is.EqualTo(100));
            Assert.That(receipt.DamageToNpc, Is.EqualTo(100 - Math.Min(100, enemyScore) / 2));
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(enemyScore - Math.Min(100, enemyScore) / 2));
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(forecast.DamageToPlayer));
            Assert.That(game.PlayerUsed[(int)ScoreHandKind.Three], Is.True);
            Assert.That(game.NpcUsed[(int)receipt.NpcKind], Is.True);
            Assert.That(game.SavedHandUsed, Is.True);
            Assert.That(game.HasSavedHand, Is.True);
            game.PlayerDice[0] = 1; game.Held[0] = false;
            game.NextRound();
            Assert.That(game.HasSavedHand, Is.False);
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.AwaitingReroll));
            Assert.That(game.RerollsLeft, Is.EqualTo(2));
            Assert.That(game.PlayerDice, Is.EqualTo(new[] { 6, 6, 6, 6, 6 }));
            Assert.That(game.Held, Is.EqualTo(held));
            Assert.Throws<InvalidOperationException>(() => game.Roll());
            Assert.Throws<InvalidOperationException>(() => game.SaveHand(ScoreHandKind.Five));
            Assert.That(game.Forecast(ScoreHandKind.Five).Score, Is.EqualTo(150));
            Assert.That(game.Reroll(), Is.EqualTo(2));
            Assert.That(game.PlayerDice[0], Is.EqualTo(6));
        }

        [Test]
        public void SaveDoubleKnockoutUsesRawHpAndDeathDiscardsTheStoredDice()
        {
            var game = new ScoreDuel(104);
            game.Roll();
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, 1);
            typeof(ScoreDuel).GetProperty("NpcHp").SetValue(game, 1);
            MirrorNpcHand(game);
            game.SaveHand(game.Intent.Kind);
            Assert.That(game.PlayerHp, Is.Zero);
            Assert.That(game.NpcHp, Is.Zero);
            Assert.That(game.Outcome.Result, Is.EqualTo(MatchResult.Draw));
            Assert.That(game.HasSavedHand, Is.False);
            Assert.Throws<InvalidOperationException>(() => game.NextRound());
            game.Begin();
            Assert.That(game.SavedHandUsed, Is.False);
            Assert.That(game.HasSavedHand, Is.False);
        }

        [Test]
        public void EveryOrderedHandUsesTheDocumentedMultiplierAndDamagePreservesTheScoreDifference()
        {
            var quarters = new[] { 6, 8, 8, 10, 8, 12, 10, 12, 4 };
            var dice = new int[5];
            for (int code = 0; code < 7776; code++)
            {
                int value = code, sum = 0;
                for (int i = 0; i < 5; i++) { dice[i] = value % 6 + 1; value /= 6; sum += dice[i]; }
                var counts = new int[7];
                foreach (int face in dice) counts[face]++;
                int pairs = 0, maximum = 0, run = 0, longestRun = 0;
                for (int face = 1; face <= 6; face++)
                {
                    if (counts[face] >= 2) pairs++;
                    maximum = Math.Max(maximum, counts[face]);
                    run = counts[face] > 0 ? run + 1 : 0; longestRun = Math.Max(longestRun, run);
                }
                var formed = new[] { maximum >= 2, pairs >= 2, maximum >= 3,
                    maximum == 3 && pairs == 2, longestRun >= 4, maximum >= 4, longestRun == 5, maximum == 5, true };
                for (int kind = 0; kind < 9; kind++)
                {
                    var offer = ScoreRules.Evaluate((ScoreHandKind)kind, dice);
                    Assert.That(offer.BaseScore, Is.EqualTo(20 + sum));
                    Assert.That(offer.IsFormed, Is.EqualTo(formed[kind]));
                    Assert.That(offer.Score, Is.EqualTo(kind == 7 && formed[kind] ? 150 : (20 + sum) * (formed[kind] ? quarters[kind] : 4) / 4));
                    Assert.That(offer.Score, Is.EqualTo(offer.BaseScore + offer.BonusScore));
                }
            }
            for (int player = 25; player <= 162; player++)
                for (int npc = 25; npc <= 162; npc++)
                {
                    var forecast = new ScoreForecast(player, npc);
                    Assert.That(forecast.DamageToNpc - forecast.DamageToPlayer, Is.EqualTo(player - npc));
                    Assert.That(forecast.DamageToNpc, Is.GreaterThanOrEqualTo(13));
                    Assert.That(forecast.DamageToPlayer, Is.GreaterThanOrEqualTo(13));
                    Assert.That(player - forecast.DamageToNpc, Is.EqualTo(npc - forecast.DamageToPlayer));
                }
            Assert.That(ScoreDuel.SafetyRoundBound, Is.EqualTo(24));
        }

        [TestCase(30, 10, MatchResult.PlayerWin, -8, -22)]
        [TestCase(20, 20, MatchResult.NpcWin, -18, -12)]
        [TestCase(16, 10, MatchResult.Draw, -22, -22)]
        public void SimultaneousKnockoutUsesRawHpIncludingThePriorAdvantage(int playerHp, int npcHp,
            MatchResult result, int rawPlayer, int rawNpc)
        {
            var game = new ScoreDuel(104);
            game.Roll();
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, playerHp);
            typeof(ScoreDuel).GetProperty("NpcHp").SetValue(game, npcHp);
            SetDice(game, 2, 2, 2, 3, 3); // base32, three =>56
            typeof(ScoreIntent).GetProperty("Score").SetValue(game.Intent, 70);
            var receipt = game.Commit(ScoreHandKind.Three);
            Assert.That(receipt.PlayerRawHpAfter, Is.EqualTo(rawPlayer));
            Assert.That(receipt.NpcRawHpAfter, Is.EqualTo(rawNpc));
            Assert.That(game.PlayerHp, Is.Zero); Assert.That(game.NpcHp, Is.Zero);
            Assert.That(game.Outcome.Result, Is.EqualTo(result));
            Assert.That(game.Outcome.Reason, Is.EqualTo("同時KO"));
        }

        [TestCase(0, 250)]
        [TestCase(1, 350)]
        [TestCase(2, 400)]
        [TestCase(3, 450)]
        [TestCase(4, 550)]
        public void DifficultyChangesOnlyEnemyHp(int tier, int hp)
        {
            var game = new ScoreDuel(104, NpcPersonality.Expert, ScoreDuel.NpcHpForDifficulty(tier));
            Assert.That(game.PlayerHp, Is.EqualTo(300));
            Assert.That(game.NpcHp, Is.EqualTo(hp));
            Assert.That(game.Personality, Is.EqualTo(NpcPersonality.Expert));
        }

        [Test]
        public void ChanceReversesDamageAndCanBeCombinedWithCarry()
        {
            var game = new ScoreDuel(104);
            game.Roll(); SetDice(game, 1, 1, 1, 1, 1);
            typeof(ScoreIntent).GetProperty("Score").SetValue(game.Intent, 101);
            var preview = game.Forecast(ScoreHandKind.Chance, true);
            var receipt = game.SaveHand(ScoreHandKind.Chance);
            Assert.That(preview.IsReversed, Is.True);
            Assert.That(receipt.IsReversed, Is.True);
            Assert.That(receipt.IsSaved, Is.True);
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(13));
            Assert.That(receipt.DamageToNpc, Is.EqualTo(89));
            Assert.That(game.PlayerUsed[(int)ScoreHandKind.Chance], Is.True);
            game.NextRound();
            Assert.That(game.PlayerDice, Is.EqualTo(new[] { 1, 1, 1, 1, 1 }));
        }

        [Test]
        public void FormedYahtzeeIsFixedAndItsExtraRerollExpiresAfterNextRound()
        {
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.Five, new[] { 1, 1, 1, 1, 1 }).Score, Is.EqualTo(150));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.Five, new[] { 6, 6, 6, 6, 6 }).Score, Is.EqualTo(150));
            var game = new ScoreDuel(104, npcStartingHp: 10000);
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, 10000);
            game.Roll(); SetDice(game, 6, 6, 6, 6, 6); game.Commit(ScoreHandKind.Five);
            game.NextRound(); Assert.That(game.RerollsLeft, Is.EqualTo(3));
            game.Roll(); game.Reroll(); game.Reroll(); game.Reroll();
            Assert.That(game.Phase, Is.EqualTo(MatchPhase.AwaitingSplit));
            Assert.Throws<InvalidOperationException>(() => game.Reroll());
            game.Commit(ScoreHandKind.Pair);
            Assert.That(game.LastRound.PlayerRerollsUsed, Is.EqualTo(3));
            game.NextRound(); Assert.That(game.RerollsLeft, Is.EqualTo(2));
            game.Roll(); game.Commit(ScoreHandKind.Three);
            game.Begin(); Assert.That(game.RerollsLeft, Is.EqualTo(2));
        }

        [Test]
        public void YahtzeeCanAwardAgainAfterNineRolesRefresh()
        {
            var game = new ScoreDuel(104, npcStartingHp: 10000);
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, 10000);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                game.Roll(); SetDice(game, 2, 2, 2, 2, 2); game.Commit(ScoreHandKind.Five);
                game.NextRound(); Assert.That(game.RerollsLeft, Is.EqualTo(3));
                for (int kind = 0; kind < ScoreRules.HandCount; kind++)
                {
                    if (kind == (int)ScoreHandKind.Five) continue;
                    game.Roll(); game.Commit((ScoreHandKind)kind); game.NextRound();
                }
                Assert.That(game.PlayerUsed[(int)ScoreHandKind.Five], Is.False);
                Assert.That(game.RerollsLeft, Is.EqualTo(2));
            }
        }

        [Test]
        public void NpcChanceCategoryDoesNotDeclareThePlayerChanceEffect()
        {
            var game = new ScoreDuel(104);
            game.Roll(); SetDice(game, 6, 6, 6, 6, 6);
            typeof(ScoreIntent).GetProperty("Kind").SetValue(game.Intent, ScoreHandKind.Chance);
            typeof(ScoreIntent).GetProperty("Score").SetValue(game.Intent, 51);
            var receipt = game.Commit(ScoreHandKind.Five);
            Assert.That(receipt.IsReversed, Is.False);
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(26));
            Assert.That(receipt.DamageToNpc, Is.EqualTo(125));
        }

        private static bool[] Available(ScoreDuel game)
        {
            var available = new bool[9];
            for (int i = 0; i < 9; i++) available[i] = !game.PlayerUsed[i];
            return available;
        }
        private static void SetDice(ScoreDuel game, params int[] dice) { Array.Copy(dice, game.PlayerDice, 5); }
        private static void MirrorNpcHand(ScoreDuel game)
        {
            for (int i = 0; i < 5; i++) game.PlayerDice[i] = game.PlannedNpcDice[i];
        }
    }
}



