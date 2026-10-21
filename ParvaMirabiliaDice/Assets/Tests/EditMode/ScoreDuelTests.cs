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

        [TestCase(ScoreHandKind.Pair, 15)]
        [TestCase(ScoreHandKind.Three, 20)]
        [TestCase(ScoreHandKind.Four, 30)]
        public void MatchingFacesHaveTheSameMultiplierWhileRawFaceValuesStillMatter(ScoreHandKind kind, int expected)
        {
            int count = kind == ScoreHandKind.Pair ? 2 : kind == ScoreHandKind.Three ? 3 : kind == ScoreHandKind.Four ? 4 : 5;
            var low = new[] { 2, 3, 4, 5, 6 };
            var high = new[] { 1, 2, 3, 4, 5 };
            for (int i = 0; i < count; i++) { low[i] = 1; high[i] = 6; }
            var lowOffer = ScoreRules.Evaluate(kind, low);
            var highOffer = ScoreRules.Evaluate(kind, high);
            Assert.That(ScoreRules.Multiplier(kind), Is.EqualTo(expected));
            Assert.That(lowOffer.Score, Is.EqualTo(lowOffer.BaseScore * expected));
            Assert.That(highOffer.Score, Is.EqualTo(highOffer.BaseScore * expected));
            Assert.That(highOffer.Score, Is.GreaterThan(lowOffer.Score));
        }

        [Test]
        public void DistinctPairsAndExactFullHouseDoNotAcceptOneLargeGroup()
        {
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.TwoPair, new[] { 1, 1, 2, 2, 3 }).Score, Is.EqualTo(180));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.FullHouse, new[] { 1, 1, 1, 2, 2 }).Score, Is.EqualTo(175));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.FullHouse, new[] { 6, 6, 6, 5, 5 }).Score, Is.EqualTo(700));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.TwoPair, new[] { 1, 1, 1, 1, 2 }).IsFormed, Is.False);
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.FullHouse, new[] { 1, 1, 1, 1, 1 }).IsFormed, Is.False);
        }

        [Test]
        public void StraightMasksContainOneOfEachNecessaryFaceAndNeverDuplicates()
        {
            var small = ScoreRules.Evaluate(ScoreHandKind.SmallStraight, new[] { 2, 3, 3, 4, 5 });
            Assert.That(small.Score, Is.EqualTo(340));
            Assert.That(SplitDecision.CountBits(small.Mask), Is.EqualTo(4));
            Assert.That((small.Mask & 2) != 0, Is.Not.EqualTo((small.Mask & 4) != 0));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.LargeStraight, new[] { 2, 3, 3, 4, 5 }).Score, Is.EqualTo(170));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.LargeStraight, new[] { 6, 2, 5, 3, 4 }).Score, Is.EqualTo(500));
        }

        [Test]
        public void ChanceIsASingleUseSafetyNetRatherThanAnAlwaysAvailableAttack()
        {
            var game = new ScoreDuel(713);
            game.Roll();
            SetDice(game, 3, 3, 3, 3, 3);
            var receipt = game.Commit(ScoreHandKind.Chance);
            Assert.That(receipt.PlayerScore, Is.EqualTo(150));
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
        public void AnUnformedCategoryPaysTheDiceTotalAtTheFlatRateAndConsumesItsSlot()
        {
            var game = new ScoreDuel(75);
            game.Roll(); SetDice(game, 1, 2, 3, 5, 6);
            var offer = game.Offers[(int)ScoreHandKind.Five];
            Assert.That(offer.IsAvailable, Is.True);
            Assert.That(offer.IsFormed, Is.False);
            Assert.That(offer.BaseScore, Is.EqualTo(17));
            Assert.That(offer.Score, Is.EqualTo(170));
            Assert.That(offer.BonusScore, Is.EqualTo(offer.Score - offer.BaseScore));
            var receipt = game.Commit(ScoreHandKind.Five);
            Assert.That(receipt.PlayerScore, Is.EqualTo(170));
            Assert.That(game.PlayerUsed[(int)ScoreHandKind.Five], Is.True);
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(new ScoreForecast(170, game.Intent.Score).DamageToPlayer));
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
                game.Roll();
                // Both pools must enter the clash equal: spend the human rerolls and mark the
                // NPC's allowance spent so neither side heals from keeping a charge.
                SpendRerolls(game);
                MirrorNpcHand(game);
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
            game.Roll(); SpendRerolls(game); MirrorNpcHand(game);
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
                typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(duel, 3000);
                typeof(ScoreDuel).GetProperty("NpcHp").SetValue(duel, 3000);
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
            Assert.That(ScoreDuel.DefaultNpcStartingHp, Is.EqualTo(ScoreDuel.NpcHpForDifficulty(0)));
            Assert.That(ScoreDuel.EnhancedStartingHp, Is.EqualTo(ScoreDuel.NpcHpForDifficulty(4)));
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
            Assert.That(game.PlayerHp, Is.EqualTo(ScoreDuel.StartingHp));
            Assert.That(game.NpcHp, Is.EqualTo(ScoreDuel.DefaultNpcStartingHp));
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
            // worth 650, but a formed four of a kind (840) and a formed yahtzee (fixed 900) both
            // pay more, so rerolling the two fours is only a question of how many shots remain.
            var fullHouse = ScoreRules.Evaluate(ScoreHandKind.FullHouse, dice);
            var four = ScoreRules.Evaluate(ScoreHandKind.Four, new[] { 6, 6, 6, 6, 4 });
            var yahtzee = ScoreRules.Evaluate(ScoreHandKind.Five, new[] { 6, 6, 6, 6, 6 });
            Assert.That(fullHouse.Score, Is.EqualTo(650));
            Assert.That(four.Score, Is.EqualTo(840));
            Assert.That(yahtzee.Score, Is.EqualTo(900));

            // One reroll left: a single shot at a fourth six averages below the banked 650,
            // so every die stays.
            Assert.That(ScoreRules.SuggestHeld(dice, available, 1),
                Is.EqualTo(new[] { true, true, true, true, true }));

            // Two rerolls left: the extra attempt lifts the expected score above the banked 650,
            // so only the three sixes stay and both fours are rerolled.
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

            // When the rerolls are also health, stopping with the full house banks 80 more. The
            // added value lifts the stop above the two-shot chase, so the damaged NPC keeps it.
            Assert.That(ScoreRules.SuggestHeld(dice, available, 2, valueRerollHeal: true),
                Is.EqualTo(new[] { true, true, true, true, true }));
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
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, 3000);
            typeof(ScoreDuel).GetProperty("NpcHp").SetValue(game, 3000);
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
            Assert.That(receipt.PlayerScore, Is.EqualTo(600));
            Assert.That(receipt.DamageToNpc, Is.EqualTo(600 - Math.Min(600, enemyScore) / 2));
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(enemyScore - Math.Min(600, enemyScore) / 2));
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
            Assert.That(game.Forecast(ScoreHandKind.Five).Score, Is.EqualTo(900));
            Assert.That(game.Reroll(), Is.EqualTo(2));
            Assert.That(game.PlayerDice[0], Is.EqualTo(6));
        }

        [Test]
        public void SaveDoubleKnockoutUsesRawHpAndDeathDiscardsTheStoredDice()
        {
            var game = new ScoreDuel(104);
            game.Roll();
            SpendRerolls(game);
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
            var multipliers = new[] { 15, 20, 20, 25, 20, 30, 25, 30, 10 };
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
                    int expected = kind == (int)ScoreHandKind.Five && formed[kind] ? ScoreRules.YahtzeeScore
                        : sum * (formed[kind] ? multipliers[kind] : ScoreRules.UnformedMultiplier);
                    Assert.That(offer.BaseScore, Is.EqualTo(sum));
                    Assert.That(offer.IsFormed, Is.EqualTo(formed[kind]));
                    Assert.That(offer.Score, Is.EqualTo(expected));
                    Assert.That(offer.Score, Is.EqualTo(offer.BaseScore + offer.BonusScore));
                }
            }
            for (int player = 50; player <= 900; player += 5)
                for (int npc = 50; npc <= 900; npc += 5)
                {
                    var forecast = new ScoreForecast(player, npc);
                    Assert.That(forecast.DamageToNpc - forecast.DamageToPlayer, Is.EqualTo(player - npc));
                    Assert.That(forecast.DamageToNpc, Is.GreaterThanOrEqualTo(25));
                    Assert.That(forecast.DamageToPlayer, Is.GreaterThanOrEqualTo(25));
                    Assert.That(player - forecast.DamageToNpc, Is.EqualTo(npc - forecast.DamageToPlayer));
                }
            Assert.That(ScoreDuel.SafetyRoundBound, Is.EqualTo(110),
                "the guard uses the largest enemy pool and the slowest decided exchange");
        }

        [TestCase(30, 190, MatchResult.PlayerWin, -5, -15)]
        [TestCase(10, 200, MatchResult.NpcWin, -25, -5)]
        [TestCase(20, 190, MatchResult.Draw, -15, -15)]
        public void SimultaneousKnockoutUsesRawHpIncludingThePriorAdvantage(int playerHp, int npcHp,
            MatchResult result, int rawPlayer, int rawNpc)
        {
            var game = new ScoreDuel(104);
            game.Roll();
            // Spend every reroll so this test isolates the raw-HP tiebreak from the new
            // unspent-reroll healing; a kept reroll would raise both pools before the clash.
            SpendRerolls(game);
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, playerHp);
            typeof(ScoreDuel).GetProperty("NpcHp").SetValue(game, npcHp);
            SetDice(game, 2, 2, 2, 3, 3); // total 12, three of a kind => 240 against 70
            typeof(ScoreIntent).GetProperty("Score").SetValue(game.Intent, 70);
            var receipt = game.Commit(ScoreHandKind.Three);
            Assert.That(receipt.PlayerRawHpAfter, Is.EqualTo(rawPlayer));
            Assert.That(receipt.NpcRawHpAfter, Is.EqualTo(rawNpc));
            Assert.That(game.PlayerHp, Is.Zero); Assert.That(game.NpcHp, Is.Zero);
            Assert.That(game.Outcome.Result, Is.EqualTo(result));
            Assert.That(game.Outcome.Reason, Is.EqualTo("同時KO（残HP判定）"));
        }

        [TestCase(0, 1250)]
        [TestCase(1, 1750)]
        [TestCase(2, 2000)]
        [TestCase(3, 2250)]
        [TestCase(4, 2750)]
        public void DifficultyChangesOnlyEnemyHp(int tier, int hp)
        {
            var game = new ScoreDuel(104, NpcPersonality.Expert, ScoreDuel.NpcHpForDifficulty(tier));
            Assert.That(game.PlayerHp, Is.EqualTo(1500));
            Assert.That(game.NpcHp, Is.EqualTo(hp));
            Assert.That(game.Personality, Is.EqualTo(NpcPersonality.Expert));
        }

        [Test]
        public void ChanceReversesDamageAndCanBeCombinedWithCarry()
        {
            var game = new ScoreDuel(104);
            game.Roll(); SetDice(game, 1, 1, 1, 1, 1);
            typeof(ScoreIntent).GetProperty("Kind").SetValue(game.Intent, ScoreHandKind.Pair);
            typeof(ScoreIntent).GetProperty("Score").SetValue(game.Intent, 101);
            var preview = game.Forecast(ScoreHandKind.Chance, true);
            var receipt = game.SaveHand(ScoreHandKind.Chance);
            Assert.That(preview.IsReversed, Is.True);
            Assert.That(receipt.IsReversed, Is.True);
            Assert.That(receipt.IsSaved, Is.True);
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(25));
            Assert.That(receipt.DamageToNpc, Is.EqualTo(76));
            Assert.That(game.PlayerUsed[(int)ScoreHandKind.Chance], Is.True);
            game.NextRound();
            Assert.That(game.PlayerDice, Is.EqualTo(new[] { 1, 1, 1, 1, 1 }));
        }

        [Test]
        public void FormedYahtzeeIsFixedAndItsExtraRerollExpiresAfterNextRound()
        {
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.Five, new[] { 1, 1, 1, 1, 1 }).Score, Is.EqualTo(900));
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.Five, new[] { 6, 6, 6, 6, 6 }).Score, Is.EqualTo(900));
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
        public void NpcChanceDeclarationInvertsDamageLikeOnline()
        {
            // NPC Chance now fires the same XOR reversal as VersusDuel (user decision, 2026-10-18):
            // receivers swap, so the player's own (low-aimable) score becomes their incoming damage.
            var game = new ScoreDuel(104);
            game.Roll(); SetDice(game, 6, 6, 6, 6, 6);
            typeof(ScoreIntent).GetProperty("Kind").SetValue(game.Intent, ScoreHandKind.Chance);
            typeof(ScoreIntent).GetProperty("Score").SetValue(game.Intent, 51);
            var receipt = game.Commit(ScoreHandKind.Five);
            Assert.That(receipt.IsReversed, Is.True);
            Assert.That(receipt.DamageToPlayer, Is.EqualTo(875));
            Assert.That(receipt.DamageToNpc, Is.EqualTo(26));

            // Two Chances cancel out (XOR), matching the online resolution.
            var both = new ScoreDuel(104);
            both.Roll(); SetDice(both, 1, 1, 1, 1, 1);
            typeof(ScoreIntent).GetProperty("Kind").SetValue(both.Intent, ScoreHandKind.Chance);
            typeof(ScoreIntent).GetProperty("Score").SetValue(both.Intent, 51);
            var twoChances = both.Commit(ScoreHandKind.Chance);
            Assert.That(twoChances.IsReversed, Is.False);
            Assert.That(twoChances.DamageToPlayer, Is.EqualTo(26));
            Assert.That(twoChances.DamageToNpc, Is.EqualTo(25));
        }

        [Test]
        public void UnspentRerollsHealBeforeDamageAndCapAtTheMaximum()
        {
            var game = new ScoreDuel(321, npcStartingHp: 10000);
            game.Roll();
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, 1400);
            SpendNpcRerolls(game);
            SetDice(game, 1, 2, 3, 5, 6);
            var receipt = game.Commit(ScoreHandKind.Chance);
            Assert.That(receipt.PlayerHpBefore, Is.EqualTo(1400));
            Assert.That(receipt.PlayerHeal, Is.EqualTo(80), "two kept rerolls restore 40 each");
            Assert.That(receipt.PlayerRawHpAfter, Is.EqualTo(1480 - receipt.DamageToPlayer));
            Assert.That(game.PlayerHp, Is.EqualTo(receipt.PlayerRawHpAfter));

            var capped = new ScoreDuel(322);
            capped.Roll();
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(capped, 1490);
            SpendNpcRerolls(capped);
            SetDice(capped, 1, 2, 3, 5, 6);
            Assert.That(capped.Commit(ScoreHandKind.Chance).PlayerHeal, Is.EqualTo(10),
                "the heal stops at the maximum instead of banking the excess");
        }

        [Test]
        public void NpcHealUsesItsOwnUnspentRerollsBeforeDamage()
        {
            var game = new ScoreDuel(325, NpcPersonality.Expert, 10000);
            game.Roll();
            typeof(ScoreDuel).GetProperty("NpcHp").SetValue(game, 1000);
            // Keep the NPC's two-reroll allowance in hand so its side of the receipt exercises
            // the same heal path the player has, rather than only the human side.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ScoreDuel).GetField("_npcRoundRerolls", flags).SetValue(game, 2);
            typeof(ScoreDuel).GetField("_npcRerollsUsed", flags).SetValue(game, 0);
            MirrorNpcHand(game);
            var receipt = game.Commit(game.Intent.Kind);
            Assert.That(receipt.NpcRerollsUsed, Is.Zero);
            Assert.That(receipt.NpcHeal, Is.EqualTo(80), "the NPC keeps two rerolls and restores 40 each");
            Assert.That(receipt.NpcHpBefore, Is.EqualTo(1000));
            Assert.That(receipt.NpcRawHpAfter, Is.EqualTo(1080 - receipt.DamageToNpc));
            Assert.That(receipt.NpcHpAfter, Is.EqualTo(receipt.NpcRawHpAfter));
            Assert.That(game.NpcHp, Is.EqualTo(receipt.NpcHpAfter));
        }

        [Test]
        public void SpendingEveryRerollForgoesTheHeal()
        {
            var game = new ScoreDuel(323);
            game.Roll();
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, 1400);
            while (game.RerollsLeft > 0) game.Reroll();
            var receipt = game.Commit(ScoreHandKind.Chance);
            Assert.That(receipt.PlayerHeal, Is.Zero);
            Assert.That(game.PlayerHp, Is.EqualTo(Math.Max(0, 1400 - receipt.DamageToPlayer)));
        }

        [Test]
        public void YahtzeeBonusAddsAThirdHealTheNextRound()
        {
            var game = new ScoreDuel(324, npcStartingHp: 10000);
            game.Roll(); SetDice(game, 6, 6, 6, 6, 6); game.Commit(ScoreHandKind.Five);
            game.NextRound();
            Assert.That(game.RerollsLeft, Is.EqualTo(3));
            typeof(ScoreDuel).GetProperty("PlayerHp").SetValue(game, 1000);
            game.Roll(); SpendNpcRerolls(game);
            var receipt = game.Commit(ScoreHandKind.Chance);
            Assert.That(receipt.PlayerHeal, Is.EqualTo(120), "three kept rerolls restore 120");
        }

        private static void SpendNpcRerolls(ScoreDuel game)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object allowance = typeof(ScoreDuel).GetField("_npcRoundRerolls", flags).GetValue(game);
            typeof(ScoreDuel).GetField("_npcRerollsUsed", flags).SetValue(game, allowance);
        }

        private static void SpendRerolls(ScoreDuel game)
        {
            while (game.RerollsLeft > 0) game.Reroll();
            SpendNpcRerolls(game);
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



