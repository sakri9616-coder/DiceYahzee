using System;
using System.Collections.Generic;

namespace DiceAndScull
{
    public enum CardKind { Flower, Skull }
    public enum PlayerDie { Normal, Low, High }
    public enum MatchWinner { Undecided, Player, Environment, Draw }

    public sealed class Board
    {
        // Seats run clockwise: player, A (left), B (top), C (right).
        public readonly CardKind[][] Seats;

        public Board(CardKind[][] seats)
        {
            if (seats == null || seats.Length != 4) throw new ArgumentException("Four seats required.", nameof(seats));
            int count = seats[0] == null ? -1 : seats[0].Length;
            if (count < 1 || count > 7) throw new ArgumentOutOfRangeException(nameof(seats));
            Seats = new CardKind[4][];
            for (int i = 0; i < 4; i++)
            {
                if (seats[i] == null || seats[i].Length != count) throw new ArgumentException("Seat sizes must match.", nameof(seats));
                Seats[i] = (CardKind[])seats[i].Clone();
            }
        }
    }

    public readonly struct Reveal
    {
        public readonly int Seat;
        public readonly int Index;
        public readonly CardKind Card;
        public readonly int RunningScore;

        public Reveal(int seat, int index, CardKind card, int runningScore)
        {
            Seat = seat;
            Index = index;
            Card = card;
            RunningScore = runningScore;
        }
    }

    public sealed class RoundResult
    {
        public readonly Board Board;
        public readonly PlayerDie Die;
        public readonly int PlayerRoll;
        public readonly int EnvironmentRoll;
        public readonly Reveal[] PlayerReveals;
        public readonly Reveal[] EnvironmentReveals;
        public int PlayerPoints => PlayerReveals.Length == 0 ? 0 : PlayerReveals[PlayerReveals.Length - 1].RunningScore;
        public int EnvironmentPoints => EnvironmentReveals.Length == 0 ? 0 : EnvironmentReveals[EnvironmentReveals.Length - 1].RunningScore;

        public RoundResult(Board board, PlayerDie die, int playerRoll, int environmentRoll, Reveal[] playerReveals, Reveal[] environmentReveals)
        {
            Board = board;
            Die = die;
            PlayerRoll = playerRoll;
            EnvironmentRoll = environmentRoll;
            PlayerReveals = playerReveals;
            EnvironmentReveals = environmentReveals;
        }
    }

    public static class DiceRules
    {
        public static Board Deal(int count, Random random)
        {
            if (count < 1 || count > 7) throw new ArgumentOutOfRangeException(nameof(count));
            if (random == null) throw new ArgumentNullException(nameof(random));
            var seats = new CardKind[4][];
            var playerPool = Pool(5, 2);
            var environmentPool = Pool(15, 6);
            Shuffle(playerPool, random);
            Shuffle(environmentPool, random);
            seats[0] = playerPool.GetRange(0, count).ToArray();
            for (int seat = 1; seat < 4; seat++)
                seats[seat] = environmentPool.GetRange((seat - 1) * count, count).ToArray();
            return new Board(seats);
        }

        public static int Roll(PlayerDie die, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            switch (die)
            {
                case PlayerDie.Normal: return random.Next(1, 7);
                case PlayerDie.Low: return random.Next(1, 4);
                case PlayerDie.High: return random.Next(4, 7);
                default: throw new ArgumentOutOfRangeException(nameof(die));
            }
        }

        public static Reveal[] Evaluate(Board board, int roll, int startSeat, bool ownSeatFirst = false)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (roll < 1 || startSeat < 0 || startSeat > 3) throw new ArgumentOutOfRangeException();
            int[] next = new int[4];
            int available = board.Seats[0].Length * 4;
            int count = Math.Min(roll, available);
            var reveals = new List<Reveal>(count);
            int score = 0;
            int cursor = startSeat;
            for (int i = 0; i < count; i++)
            {
                int attempts = 0;
                while (next[cursor] == board.Seats[cursor].Length && attempts++ < 4)
                    cursor = (cursor + 1) % 4;
                int index = next[cursor]++;
                CardKind card = board.Seats[cursor][index];
                score = card == CardKind.Flower ? score + 1 : 0;
                reveals.Add(new Reveal(cursor, index, card, score));
                if (!ownSeatFirst || cursor != startSeat || next[cursor] == board.Seats[cursor].Length)
                    cursor = (cursor + 1) % 4;
            }
            return reveals.ToArray();
        }

        private static List<CardKind> Pool(int flowers, int skulls)
        {
            var cards = new List<CardKind>(flowers + skulls);
            for (int i = 0; i < flowers; i++) cards.Add(CardKind.Flower);
            for (int i = 0; i < skulls; i++) cards.Add(CardKind.Skull);
            return cards;
        }

        private static void Shuffle<T>(IList<T> items, Random random)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }
        }
    }

    public sealed class Match
    {
        private readonly Random random;
        private readonly HashSet<PlayerDie> used = new HashSet<PlayerDie>();
        private Board pendingBoard;
        public int Round { get; private set; }
        public int PlayerScore { get; private set; }
        public int EnvironmentScore { get; private set; }
        public bool IsComplete => Round == 3;
        public MatchWinner Winner => !IsComplete ? MatchWinner.Undecided : PlayerScore > EnvironmentScore ? MatchWinner.Player : PlayerScore < EnvironmentScore ? MatchWinner.Environment : MatchWinner.Draw;

        public Match(Random random) { this.random = random ?? throw new ArgumentNullException(nameof(random)); }
        public bool CanUse(PlayerDie die) => !IsComplete && Enum.IsDefined(typeof(PlayerDie), die) && !used.Contains(die);

        public Board Prepare(int count)
        {
            if (IsComplete) throw new InvalidOperationException("Match is complete.");
            pendingBoard = DiceRules.Deal(count, random);
            return pendingBoard;
        }

        public RoundResult Play(PlayerDie die)
        {
            if (IsComplete || !CanUse(die)) throw new InvalidOperationException("Die is unavailable.");
            if (pendingBoard == null) throw new InvalidOperationException("Prepare the board first.");
            Board board = pendingBoard;
            int playerRoll = DiceRules.Roll(die, random);
            int environmentRoll = DiceRules.Roll(PlayerDie.Normal, random);
            Reveal[] player = DiceRules.Evaluate(board, playerRoll, die == PlayerDie.Low ? 1 : 0, die == PlayerDie.High);
            Reveal[] environment = DiceRules.Evaluate(board, environmentRoll, 2);
            var result = new RoundResult(board, die, playerRoll, environmentRoll, player, environment);
            PlayerScore += result.PlayerPoints;
            EnvironmentScore += result.EnvironmentPoints;
            used.Add(die);
            Round++;
            pendingBoard = null;
            return result;
        }
    }
}
