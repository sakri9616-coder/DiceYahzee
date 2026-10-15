using System;
using NUnit.Framework;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    public class ScoringTests
    {
        [TestCase(15, 21, 0, 0, 0, 3, "Npc")]
        [TestCase(21, 15, 0, 0, 3, 0, "Player")]
        [TestCase(21, 21, 0, 0, 0, 0, null)]
        [TestCase(15, 21, 1, 0, 1, 0, null)]
        [TestCase(21, 15, 0, 1, 0, 1, null)]
        [TestCase(21, 15, 7, 7, 7, 7, null)]
        public void Pushback_OnlySettlesAnExchangeWhereNeitherAttackLanded(int playerTotal,
            int npcTotal, int dealt, int taken, int expectedDealt, int expectedTaken, string winner)
        {
            Side? result = Scoring.ApplyPushback(playerTotal, npcTotal, 3, ref dealt, ref taken);
            Assert.That(dealt, Is.EqualTo(expectedDealt));
            Assert.That(taken, Is.EqualTo(expectedTaken));
            Assert.That(result.HasValue ? result.Value.ToString() : null, Is.EqualTo(winner));
        }

        [Test]
        public void Damage_OnlyCountsWhatPunchesThrough()
        {
            Assert.That(Scoring.Damage(10, 4), Is.EqualTo(6));
            Assert.That(Scoring.Damage(4, 10), Is.EqualTo(0));
            Assert.That(Scoring.Damage(5, 5), Is.EqualTo(0));
            Assert.That(Scoring.Damage(0, 0), Is.EqualTo(0));
            Assert.That(Scoring.Damage(1, 0), Is.EqualTo(1));
        }

        [Test]
        public void SumMask_AddsOnlyTheSelectedDice()
        {
            var dice = new[] { 6, 1, 5, 2, 4 };

            Assert.That(Scoring.SumMask(dice, 0b00001), Is.EqualTo(6));
            Assert.That(Scoring.SumMask(dice, 0b11111), Is.EqualTo(18));
            Assert.That(Scoring.SumMask(dice, 0b01010), Is.EqualTo(3));
            Assert.That(Scoring.SumMask(dice, 0), Is.EqualTo(0));
            Assert.That(Scoring.Sum(dice), Is.EqualTo(18));
        }

        [Test]
        public void Validate_AcceptsAFullCoveringSplit()
        {
            var split = SplitDecision.Validate(new[] { 0, 2 }, new[] { 1, 3, 4 }, 5);

            Assert.That(split.AttackMask, Is.EqualTo(0b00101));
            Assert.That(split.GuardMask, Is.EqualTo(0b11010));
            Assert.That(split.AttackCount, Is.EqualTo(2));
            Assert.That(split.GuardCount, Is.EqualTo(3));
        }

        [Test]
        public void Validate_RejectsEmptySide()
        {
            Assert.Throws<ArgumentException>(() => SplitDecision.Validate(new int[0], new[] { 0, 1, 2, 3, 4 }, 5));
            Assert.Throws<ArgumentException>(() => SplitDecision.Validate(new[] { 0, 1, 2, 3, 4 }, new int[0], 5));
        }

        [Test]
        public void Validate_RejectsDuplicateDie()
        {
            Assert.Throws<ArgumentException>(() => SplitDecision.Validate(new[] { 0, 1 }, new[] { 1, 2 }, 5));
        }

        [Test]
        public void Validate_RejectsMissingDie()
        {
            Assert.Throws<ArgumentException>(() => SplitDecision.Validate(new[] { 0, 1 }, new[] { 2, 3 }, 5));
        }

        [Test]
        public void Validate_RejectsOutOfRangeIndex()
        {
            Assert.Throws<ArgumentException>(() => SplitDecision.Validate(new[] { 0, 5 }, new[] { 1, 2, 3, 4 }, 5));
            Assert.Throws<ArgumentException>(() => SplitDecision.Validate(new[] { -1 }, new[] { 0, 1, 2, 3, 4 }, 5));
        }

        [Test]
        public void Validate_RejectsNullArguments()
        {
            Assert.Throws<ArgumentException>(() => SplitDecision.Validate(null, new[] { 0 }, 5));
            Assert.Throws<ArgumentException>(() => SplitDecision.Validate(new[] { 0 }, null, 5));
        }
    }
}
