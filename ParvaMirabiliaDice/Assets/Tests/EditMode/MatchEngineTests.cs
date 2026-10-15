using System;
using NUnit.Framework;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    public class MatchEngineTests
    {
        private static MatchEngine Create(int seed = 1, NpcArchetype npc = NpcArchetype.Balanced)
        {
            return new MatchEngine(new GameConfig(), new SeededRandom(seed), npc);
        }

        private static int[] Indices(int mask)
        {
            return MatchRunner.IndicesOf(mask);
        }

        [Test]
        public void NewMatch_StartsAtFullHpAndFirstRound()
        {
            var engine = Create();

            Assert.That(engine.Phase, Is.EqualTo(MatchPhase.AwaitingRoll));
            Assert.That(engine.PlayerHp, Is.EqualTo(20));
            Assert.That(engine.NpcHp, Is.EqualTo(20));
            Assert.That(engine.Round, Is.EqualTo(1));
            Assert.That(engine.PlayerRerollsLeft, Is.EqualTo(2));
            Assert.That(engine.Outcome, Is.Null);
        }

        [Test]
        public void Roll_ProducesFiveDiceInsideTheDieRange()
        {
            var engine = Create();
            engine.Roll();

            Assert.That(engine.PlayerDice, Has.Length.EqualTo(5));
            foreach (var die in engine.PlayerDice)
                Assert.That(die, Is.InRange(1, 6));
            Assert.That(engine.Phase, Is.EqualTo(MatchPhase.AwaitingReroll));
        }

        [Test]
        public void Roll_IsOnlyLegalBeforeTheDiceAreOnTheTable()
        {
            var engine = Create();
            engine.Roll();
            Assert.Throws<InvalidOperationException>(engine.Roll);
        }

        [Test]
        public void Reroll_ChangesOnlyUnheldDiceAndCostsOneCharge()
        {
            var rng = new CountingRandom(11);
            var engine = new MatchEngine(new GameConfig(), rng, NpcArchetype.Balanced);
            engine.Roll();

            int drawsBefore = rng.Calls;
            var before = (int[])engine.PlayerDice.Clone();
            engine.ToggleKeep(0);
            var held = engine.PlayerDice[0];

            engine.Reroll();

            Assert.That(engine.PlayerDice[0], Is.EqualTo(held), "held die must not change");
            Assert.That(engine.PlayerRerollsLeft, Is.EqualTo(1));
            Assert.That(engine.PlayerRerollsUsed, Is.EqualTo(1));
            Assert.That(rng.Calls - drawsBefore, Is.EqualTo(4), "exactly the four unheld dice are re-rolled");

            for (int i = 1; i < 5; i++)
                Assert.That(engine.PlayerDice[i], Is.InRange(1, 6));
        }

        [Test]
        public void Reroll_IsRejectedWhenEverythingIsHeld()
        {
            var engine = Create(3);
            engine.Roll();
            engine.SetKept(new[] { true, true, true, true, true });

            Assert.Throws<InvalidOperationException>(() => engine.Reroll());
            Assert.That(engine.PlayerRerollsLeft, Is.EqualTo(2), "a rejected re-roll must not spend a charge");
        }

        [Test]
        public void Reroll_IsRejectedAfterTheLimitAndLocksTheSplitPhase()
        {
            var engine = Create(4);
            engine.Roll();

            engine.Reroll();
            engine.Reroll();

            Assert.That(engine.Phase, Is.EqualTo(MatchPhase.AwaitingSplit));
            Assert.Throws<InvalidOperationException>(() => engine.Reroll());
        }

        [Test]
        public void Commit_BeforeRollingIsRejected()
        {
            var engine = Create();
            Assert.Throws<InvalidOperationException>(() => engine.CommitSplit(Indices(0b00111), Indices(0b11000)));
        }

        [Test]
        public void Commit_RejectsAnIllegalSplit()
        {
            var engine = Create(5);
            engine.Roll();

            Assert.Throws<ArgumentException>(() => engine.CommitSplit(new[] { 0, 1 }, new[] { 2, 3 }));
            Assert.Throws<ArgumentException>(() => engine.CommitSplit(new int[0], new[] { 0, 1, 2, 3, 4 }));
            Assert.That(engine.Phase, Is.EqualTo(MatchPhase.AwaitingReroll), "a rejected commit must not advance the round");
        }

        [Test]
        public void NpcDice_AreHiddenUntilTheRoundIsCommitted()
        {
            var engine = Create(6);
            engine.Roll();

            Assert.That(engine.NpcDice, Is.Null, "the NPC dice must not leak before the reveal");
        }

        [Test]
        public void Commit_ResolvesBothSidesSimultaneously()
        {
            var engine = Create(21);
            engine.Roll();
            engine.SetKept(new[] { false, false, false, false, false });

            var outcome = engine.CommitSplit(Indices(0b00111), Indices(0b11000));

            int expectedPlayerDamage = Math.Max(0, outcome.PlayerAttackSum - outcome.NpcGuardSum);
            int expectedNpcDamage = Math.Max(0, outcome.NpcAttackSum - outcome.PlayerGuardSum);

            // Stand-off rule: if neither strike got through, the higher roll total shoves back.
            bool standOff = expectedPlayerDamage == 0 && expectedNpcDamage == 0;
            int playerTotal = outcome.PlayerAttackSum + outcome.PlayerGuardSum;
            int npcTotal = outcome.NpcAttackSum + outcome.NpcGuardSum;
            if (standOff && playerTotal > npcTotal)
            {
                expectedPlayerDamage = new GameConfig().PushbackDamage;
                Assert.That(outcome.Pushback, Is.True, "a stand-off must be settled by push-back");
                Assert.That(outcome.PushbackWinner, Is.EqualTo(Side.Player));
            }
            else if (standOff && npcTotal > playerTotal)
            {
                expectedNpcDamage = new GameConfig().PushbackDamage;
                Assert.That(outcome.Pushback, Is.True, "a stand-off must be settled by push-back");
                Assert.That(outcome.PushbackWinner, Is.EqualTo(Side.Npc));
            }
            else if (!standOff)
            {
                Assert.That(outcome.Pushback, Is.False, "a landed strike is not a stand-off");
            }

            Assert.That(outcome.DamageToNpc, Is.EqualTo(expectedPlayerDamage));
            Assert.That(outcome.DamageToPlayer, Is.EqualTo(expectedNpcDamage));
            // the player's damage lands on the NPC and vice versa
            Assert.That(engine.NpcHp, Is.EqualTo(20 - expectedPlayerDamage));
            Assert.That(engine.PlayerHp, Is.EqualTo(20 - expectedNpcDamage));
            Assert.That(engine.Phase, Is.EqualTo(MatchPhase.Resolving));
            Assert.That(engine.NpcDice, Has.Length.EqualTo(5));
        }

        [Test]
        public void Commit_SplitSumsMatchTheDice()
        {
            var engine = Create(22);
            engine.Roll();

            var outcome = engine.CommitSplit(Indices(0b00011), Indices(0b11100));

            Assert.That(outcome.PlayerAttackSum + outcome.PlayerGuardSum, Is.EqualTo(SumOf(outcome.PlayerDice)));
            Assert.That(outcome.NpcAttackSum + outcome.NpcGuardSum, Is.EqualTo(SumOf(outcome.NpcDice)));
            Assert.That(outcome.PlayerSplit.AttackCount, Is.EqualTo(2));
            Assert.That(outcome.PlayerSplit.GuardCount, Is.EqualTo(3));
        }

        [Test]
        public void Standoff_PushbackGoesToTheStrongerRollTotal()
        {
            // Player rolls five 2s (total 10), the NPC rolls five 1s (total 5).
            // Neither strike can get through a ward, so the shove settles the round.
            var rng = new ScriptedRandom(new[] { 2, 2, 2, 2, 2 }, 1);
            var engine = new MatchEngine(new GameConfig(), rng, NpcArchetype.Guardian);
            engine.Roll();

            var outcome = engine.CommitSplit(Indices(0b00001), Indices(0b11110));

            Assert.That(outcome.PlayerAttackSum - outcome.NpcGuardSum, Is.LessThanOrEqualTo(0),
                "the strike must be blocked by the ward");
            Assert.That(outcome.NpcAttackSum - outcome.PlayerGuardSum, Is.LessThanOrEqualTo(0),
                "the counter must be blocked as well");
            Assert.That(outcome.Pushback, Is.True, "a stand-off must not pass for free");
            Assert.That(outcome.PushbackWinner, Is.EqualTo(Side.Player));
            Assert.That(outcome.DamageToNpc, Is.EqualTo(engine.Config.PushbackDamage),
                "the stronger roll total deals the shove damage");
            Assert.That(outcome.DamageToPlayer, Is.EqualTo(0));
            Assert.That(engine.PlayerHp, Is.EqualTo(20), "the winner of the shove takes nothing");
            Assert.That(engine.NpcHp, Is.EqualTo(20 - engine.Config.PushbackDamage));
        }

        [Test]
        public void Standoff_EqualTotalsLeaveBothSidesStanding()
        {
            var engine = new MatchEngine(new GameConfig(), new ScriptedRandom(new int[0], 1), NpcArchetype.Guardian);
            engine.Roll();

            var outcome = engine.CommitSplit(Indices(0b00001), Indices(0b11110));

            Assert.That(outcome.Pushback, Is.False, "there is no winner when the totals are level");
            Assert.That(outcome.DamageToNpc, Is.EqualTo(0));
            Assert.That(outcome.DamageToPlayer, Is.EqualTo(0));
            Assert.That(engine.PlayerHp, Is.EqualTo(20));
            Assert.That(engine.NpcHp, Is.EqualTo(20));
        }

        [Test]
        public void LandedStrike_IsNeverResolvedAsPushback()
        {
            // Five 6s split 4/1: the strike tears straight through the NPC's ward.
            var rng = new ScriptedRandom(new[] { 6, 6, 6, 6, 6 }, 1);
            var engine = new MatchEngine(new GameConfig(), rng, NpcArchetype.Guardian);
            engine.Roll();

            var outcome = engine.CommitSplit(Indices(0b01111), Indices(0b10000));

            Assert.That(outcome.Pushback, Is.False);
            Assert.That(outcome.DamageToNpc, Is.EqualTo(outcome.PlayerAttackSum - outcome.NpcGuardSum));
            Assert.That(engine.NpcHp, Is.EqualTo(20 - outcome.DamageToNpc));
        }

        [Test]
        public void NextRound_AdvancesAndResetsTheTable()
        {
            var engine = Create(23);
            engine.Roll();
            engine.CommitSplit(Indices(0b00111), Indices(0b11000));
            Assert.Throws<InvalidOperationException>(() => engine.CommitSplit(Indices(0b00111), Indices(0b11000)));

            engine.NextRound();

            Assert.That(engine.Round, Is.EqualTo(2));
            Assert.That(engine.Phase, Is.EqualTo(MatchPhase.AwaitingRoll));
            Assert.That(engine.PlayerRerollsLeft, Is.EqualTo(2));
            Assert.That(engine.PlayerDice, Is.All.EqualTo(0), "the next round must start with no dice");
            Assert.That(engine.NpcDice, Is.Null);
        }

        [Test]
        public void FullMatch_AlwaysProducesAResult()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var engine = Create(seed, NpcArchetype.Balanced);
                var outcome = MatchRunner.Run(engine, NpcArchetype.Aggressive);

                Assert.That(outcome, Is.Not.Null, "seed " + seed + " produced no result");
                Assert.That(outcome.Rounds, Is.InRange(1, new GameConfig().MaxRounds));
                Assert.That(engine.Phase, Is.EqualTo(MatchPhase.Finished));

                bool playerLost = engine.PlayerHp <= 0;
                bool npcLost = engine.NpcHp <= 0;
                if (playerLost && npcLost)
                {
                    // Double knockout is settled by cumulative damage, then by the final round,
                    // and only falls back to a draw when both tie-breaks are level - which the
                    // rules allow, so a drawn double knockout must still be level on damage.
                    if (outcome.Result == MatchResult.Draw)
                    {
                        Assert.That(engine.PlayerDamageDealt, Is.EqualTo(engine.NpcDamageDealt),
                            "a drawn double knockout means the damage tie-break was level (seed " + seed + ")");
                    }
                }
                else if (playerLost || npcLost)
                {
                    Assert.That(outcome.Result, Is.Not.EqualTo(MatchResult.Draw),
                        "a single knockout must not be scored as a draw (seed " + seed + ")");
                }
            }
        }

        [Test]
        public void FullMatch_IsDeterministicForTheSameSeed()
        {
            var first = Create(777, NpcArchetype.Guardian);
            var second = Create(777, NpcArchetype.Guardian);

            var a = MatchRunner.Run(first, NpcArchetype.Aggressive);
            var b = MatchRunner.Run(second, NpcArchetype.Aggressive);

            Assert.That(a.Result, Is.EqualTo(b.Result));
            Assert.That(a.Rounds, Is.EqualTo(b.Rounds));
            Assert.That(first.PlayerHp, Is.EqualTo(second.PlayerHp));
            Assert.That(first.NpcHp, Is.EqualTo(second.NpcHp));
            Assert.That(first.History.Count, Is.EqualTo(second.History.Count));

            for (int i = 0; i < first.History.Count; i++)
            {
                Assert.That(first.History[i].DamageToNpc, Is.EqualTo(second.History[i].DamageToNpc), "round " + i);
                Assert.That(first.History[i].DamageToPlayer, Is.EqualTo(second.History[i].DamageToPlayer), "round " + i);
            }
        }

        [Test]
        public void BeginMatch_ResetsEverythingForARematch()
        {
            var engine = Create(888, NpcArchetype.Balanced);
            MatchRunner.Run(engine, NpcArchetype.Guardian);
            Assert.That(engine.Outcome, Is.Not.Null);

            engine.BeginMatch();

            Assert.That(engine.Outcome, Is.Null);
            Assert.That(engine.PlayerHp, Is.EqualTo(20));
            Assert.That(engine.NpcHp, Is.EqualTo(20));
            Assert.That(engine.Round, Is.EqualTo(1));
            Assert.That(engine.PlayerDamageDealt, Is.EqualTo(0));
            Assert.That(engine.NpcDamageDealt, Is.EqualTo(0));
            Assert.That(engine.History, Is.Empty);
            Assert.That(engine.Phase, Is.EqualTo(MatchPhase.AwaitingRoll));
        }

        [Test]
        public void BeginMatch_StartsAFreshMatchInsteadOfReplayingTheOldOne()
        {
            var engine = Create(20261009, NpcArchetype.Aggressive);
            MatchRunner.Run(engine, NpcArchetype.Balanced);
            var firstRoundDice = (int[])engine.History[0].PlayerDice.Clone();
            var firstRounds = engine.Round;

            engine.BeginMatch();
            MatchRunner.Run(engine, NpcArchetype.Balanced);

            var secondRoundDice = engine.History[0].PlayerDice;

            Assert.That(engine.Round, Is.GreaterThanOrEqualTo(1));
            Assert.That(engine.History.Count, Is.GreaterThan(0));
            bool identicalOpening = true;
            for (int i = 0; i < firstRoundDice.Length; i++)
            {
                if (firstRoundDice[i] != secondRoundDice[i])
                    identicalOpening = false;
            }
            Assert.That(identicalOpening, Is.False,
                "a rematch must not replay the previous match dice for dice (both matches were "
                + firstRounds + " and " + engine.Round + " rounds)");
        }

        [Test]
        public void TooManyRounds_EndsOnHpAndNeverRunsForever()
        {
            var config = new GameConfig { MaxRounds = 3 };
            var engine = new MatchEngine(config, new SeededRandom(31337), NpcArchetype.Balanced);

            var outcome = MatchRunner.Run(engine, NpcArchetype.Guardian);

            Assert.That(engine.Round, Is.LessThanOrEqualTo(config.MaxRounds));
            Assert.That(outcome, Is.Not.Null);
        }

        private static int SumOf(int[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++)
                total += values[i];
            return total;
        }
    }
}
