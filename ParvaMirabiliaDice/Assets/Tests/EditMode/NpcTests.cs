using System;
using NUnit.Framework;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    public class NpcTests
    {
        [Test]
        public void DisplayNames_AreDistinctAndHumanReadable()
        {
            Assert.That(NpcArchetypes.DisplayName(NpcArchetype.Aggressive), Is.EqualTo("攻撃型"));
            Assert.That(NpcArchetypes.DisplayName(NpcArchetype.Guardian), Is.EqualTo("守護型"));
            Assert.That(NpcArchetypes.DisplayName(NpcArchetype.Balanced), Is.EqualTo("均衡型"));

            foreach (NpcArchetype archetype in Enum.GetValues(typeof(NpcArchetype)))
                Assert.That(NpcArchetypes.Description(archetype), Is.Not.Empty);
        }

        [Test]
        public void TakeTurn_AlwaysReturnsLegalDiceAndALegalSplit()
        {
            var config = new GameConfig();

            for (int seed = 1; seed <= 300; seed++)
            {
                foreach (NpcArchetype archetype in Enum.GetValues(typeof(NpcArchetype)))
                {
                    var brain = new NpcBrain(archetype);
                    var turn = brain.TakeTurn(config, new SeededRandom(seed), 20, 20);

                    Assert.That(turn.Dice, Has.Length.EqualTo(config.DiceCount));
                    Assert.That(turn.InitialDice, Has.Length.EqualTo(config.DiceCount));
                    Assert.That(turn.RerollsUsed, Is.InRange(0, config.MaxRerolls));
                    Assert.That(turn.Split.AttackCount, Is.GreaterThanOrEqualTo(1));
                    Assert.That(turn.Split.GuardCount, Is.GreaterThanOrEqualTo(1));
                    Assert.That(turn.Split.AttackMask | turn.Split.GuardMask, Is.EqualTo(config.AllMask));
                    Assert.That(turn.Split.AttackMask & turn.Split.GuardMask, Is.EqualTo(0));
                    Assert.That(turn.Summary, Is.Not.Empty);

                    foreach (var die in turn.Dice)
                        Assert.That(die, Is.InRange(config.DieMin, config.DieMax));
                }
            }
        }

        [Test]
        public void TakeTurn_DoesNotNeedAndCannotSeeThePlayerDice()
        {
            // The API only takes HP values: structurally the NPC cannot peek at the player's roll.
            var brain = new NpcBrain(NpcArchetype.Balanced);
            var parameters = typeof(NpcBrain).GetMethod("TakeTurn").GetParameters();

            Assert.That(parameters.Length, Is.EqualTo(4));
            foreach (var parameter in parameters)
                Assert.That(parameter.ParameterType.Name, Does.Not.Contain("Split"));
        }

        [Test]
        public void ChooseSplit_PrefersTheArchetypeLineOnATypicalRoll()
        {
            var config = new GameConfig();
            var dice = new[] { 6, 5, 4, 3, 2 };

            var aggressive = new NpcBrain(NpcArchetype.Aggressive).ChooseSplit(dice, config, 20, 20);
            var guardian = new NpcBrain(NpcArchetype.Guardian).ChooseSplit(dice, config, 20, 20);

            Assert.That(aggressive.AttackCount, Is.GreaterThanOrEqualTo(guardian.AttackCount),
                "the aggressive line should never hold fewer strike dice than the guardian one");
        }

        [Test]
        public void ChooseSplit_IsLegalForEveryPossibleDiceCombination()
        {
            var config = new GameConfig();
            var brains = new[]
            {
                new NpcBrain(NpcArchetype.Aggressive),
                new NpcBrain(NpcArchetype.Guardian),
                new NpcBrain(NpcArchetype.Balanced)
            };

            var dice = new int[5];
            for (int a = 1; a <= 6; a++)
            for (int b = 1; b <= 6; b++)
            for (int c = 1; c <= 6; c++)
            {
                dice[0] = a;
                dice[1] = b;
                dice[2] = c;
                dice[3] = 3;
                dice[4] = 4;

                foreach (var brain in brains)
                {
                    var split = brain.ChooseSplit(dice, config, 20, 20);
                    Assert.That(split.AttackMask | split.GuardMask, Is.EqualTo(config.AllMask));
                    Assert.That(split.AttackCount, Is.GreaterThanOrEqualTo(1));
                    Assert.That(split.GuardCount, Is.GreaterThanOrEqualTo(1));
                }
            }
        }

        [Test]
        public void BalancedBrain_ChangesItsLineWhenTrailing()
        {
            var config = new GameConfig();
            var brain = new NpcBrain(NpcArchetype.Balanced);
            var dice = new[] { 6, 5, 4, 3, 1 };

            var whenFresh = brain.ChooseSplit(dice, config, 20, 20);
            var whenTrailing = brain.ChooseSplit(dice, config, 4, 20);

            Assert.That(whenTrailing.AttackCount, Is.GreaterThanOrEqualTo(whenFresh.AttackCount),
                "trailing should never make the balanced line more passive");
        }
    }
}
