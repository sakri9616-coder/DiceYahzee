using System;
using System.Linq;
using NUnit.Framework;

namespace DiceAndScull.Tests
{
    public class RulesTests
    {
        [Test]
        public void DealUsesFixedPoolsWithoutReplacement()
        {
            Board board = DiceRules.Deal(7, new Random(5));
            Assert.That(board.Seats[0].Count(x => x == CardKind.Flower), Is.EqualTo(5));
            Assert.That(board.Seats[0].Count(x => x == CardKind.Skull), Is.EqualTo(2));
            Assert.That(board.Seats.Skip(1).SelectMany(x => x).Count(x => x == CardKind.Flower), Is.EqualTo(15));
            Assert.That(board.Seats.Skip(1).SelectMany(x => x).Count(x => x == CardKind.Skull), Is.EqualTo(6));
        }

        [Test]
        public void SkullOnlyClearsTheCurrentEvaluation()
        {
            var board = new Board(new[]
            {
                new[] { CardKind.Flower, CardKind.Flower },
                new[] { CardKind.Skull, CardKind.Flower },
                new[] { CardKind.Flower, CardKind.Flower },
                new[] { CardKind.Flower, CardKind.Flower }
            });
            Reveal[] player = DiceRules.Evaluate(board, 6, 0);
            Reveal[] environment = DiceRules.Evaluate(board, 6, 2);
            Assert.That(player.Select(x => x.Seat), Is.EqualTo(new[] { 0, 1, 2, 3, 0, 1 }));
            Assert.That(player.Last().RunningScore, Is.EqualTo(4));
            Assert.That(environment.Last().RunningScore, Is.EqualTo(2));
        }

        [Test]
        public void HighDieClearsOwnSeatBeforeMovingClockwise()
        {
            var board = new Board(new[]
            {
                new[] { CardKind.Flower, CardKind.Skull },
                new[] { CardKind.Flower, CardKind.Flower },
                new[] { CardKind.Flower, CardKind.Flower },
                new[] { CardKind.Flower, CardKind.Flower }
            });
            Reveal[] reveals = DiceRules.Evaluate(board, 5, 0, true);
            Assert.That(reveals.Select(x => x.Seat), Is.EqualTo(new[] { 0, 0, 1, 2, 3 }));
            Assert.That(reveals.Last().RunningScore, Is.EqualTo(3));
        }

        [Test]
        public void RevealStopsWhenEverySeatRunsOut()
        {
            var seats = Enumerable.Range(0, 4).Select(_ => new[] { CardKind.Flower }).ToArray();
            Reveal[] reveals = DiceRules.Evaluate(new Board(seats), 6, 1);
            Assert.That(reveals.Length, Is.EqualTo(4));
            Assert.That(reveals.Last().RunningScore, Is.EqualTo(4));
        }

        [Test]
        public void EachDieIsAvailableOnceAcrossThreeRounds()
        {
            var match = new Match(new Random(8));
            match.Prepare(1);
            match.Play(PlayerDie.Low);
            Assert.That(match.CanUse(PlayerDie.Low), Is.False);
            match.Prepare(2);
            Assert.Throws<InvalidOperationException>(() => match.Play(PlayerDie.Low));
            match.Play(PlayerDie.Normal);
            match.Prepare(7);
            match.Play(PlayerDie.High);
            Assert.That(match.IsComplete, Is.True);
            Assert.That(match.Winner, Is.Not.EqualTo(MatchWinner.Undecided));
            Assert.Throws<InvalidOperationException>(() => match.Prepare(1));
        }

        [Test]
        public void LowDieStartsLeftAndEnvironmentStartsOpposite()
        {
            var match = new Match(new Random(19));
            match.Prepare(3);
            RoundResult result = match.Play(PlayerDie.Low);
            Assert.That(result.PlayerReveals[0].Seat, Is.EqualTo(1));
            Assert.That(result.EnvironmentReveals[0].Seat, Is.EqualTo(2));
            Assert.That(result.PlayerRoll, Is.InRange(1, 3));
            Assert.That(result.EnvironmentRoll, Is.InRange(1, 6));
        }
    }
}
