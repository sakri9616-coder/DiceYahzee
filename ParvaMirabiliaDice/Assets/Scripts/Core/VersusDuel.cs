using System;
using System.Collections.Generic;

namespace ParvaMirabilia.Core
{
    public enum VersusPhase { NotStarted, Playing, Resolved, Finished, Interrupted, ChoosingRoles }

    /// <summary>A detached snapshot. Mutating its arrays cannot alter the host's game.</summary>
    public sealed class VersusPlayer
    {
        public int Hp { get; internal set; }
        public int[] Dice { get; internal set; }
        public bool[] Used { get; internal set; }
        public int[] CategoryScores { get; internal set; }
        public int HeldMask { get; internal set; }
        public int RerollsLeft { get; internal set; }
        public long LastSequence { get; internal set; }
        public bool Submitted { get; internal set; }
        public bool RerollsFinished { get; internal set; }
        public bool CarryUsed { get; internal set; }
        public bool Carry { get; internal set; }
        /// <summary>True only between a formed Five submission and the immediately following round's rerolls. Never shown to the opponent before resolution.</summary>
        public bool NextRerollBonus { get; internal set; }
        public ScoreHandKind Kind { get; internal set; }
        public int Score { get; internal set; }
        internal VersusPlayer Copy(bool reveal)
        {
            var copy = (VersusPlayer)MemberwiseClone();
            copy.Dice = (int[])Dice.Clone();
            copy.Used = (bool[])Used.Clone();
            copy.CategoryScores = (int[])CategoryScores.Clone();
            if (!reveal && Submitted) copy.CategoryScores[(int)Kind] = 0;
            if (!reveal) { if (Submitted) copy.Used[(int)Kind] = false; if (Carry) copy.CarryUsed = false; copy.HeldMask = 0; copy.Kind = default(ScoreHandKind); copy.Score = 0; copy.Carry = false; copy.NextRerollBonus = false; }
            return copy;
        }
    }

    public sealed class VersusRound
    {
        public int Round { get; internal set; }
        public int[] Scores { get; internal set; }
        public int[] Damage { get; internal set; }
        public int[] RawHpAfter { get; internal set; }
        public bool[] Carry { get; internal set; }
    }

    /// <summary>Host-only simultaneous duel. Connection identity is validated by the transport before calling commands.</summary>
    public sealed class VersusDuel
    {
        private readonly Random random;
        private readonly int startingHp;
        /// <summary>Normal rerolls per round. A formed Five grants one more on the next round only.</summary>
        public const int BaseRerolls = 2;
        public const int MaxRerolls = 3;
        private readonly VersusPlayer[] players = new VersusPlayer[2];
        private readonly int[][] revealedDice = new int[2][];
        public VersusPhase Phase { get; private set; }
        public int Round { get; private set; }
        /// <summary>-1 while undecided, 0/1 winner, 2 draw.</summary>
        public int Winner { get; private set; } = -1;
        public VersusRound LastRound { get; private set; }
        public IReadOnlyList<VersusPlayer> Players { get { return Snapshot(-1); } }

        public VersusDuel(int seed, int startingHp = ScoreDuel.StartingHp)
        {
            if (startingHp <= 0) throw new ArgumentOutOfRangeException(nameof(startingHp));
            random = new Random(seed); this.startingHp = startingHp;
            for (int i = 0; i < 2; i++) players[i] = new VersusPlayer { Hp = startingHp, Dice = new int[5], Used = new bool[ScoreRules.HandCount], CategoryScores = new int[ScoreRules.HandCount] };
        }
        /// <summary>Opponent rerolls reveal together at the role phase; submissions remain private until resolution.</summary>
        public IReadOnlyList<VersusPlayer> Snapshot(int viewer)
        {
            if (viewer < -1 || viewer > 1) throw new ArgumentOutOfRangeException(nameof(viewer));
            bool revealAll = viewer == -1 || Phase == VersusPhase.Resolved || Phase == VersusPhase.Finished;
            var result = new[] { players[0].Copy(revealAll || viewer == 0), players[1].Copy(revealAll || viewer == 1) };
            if (viewer >= 0 && Phase == VersusPhase.Playing && revealedDice[1 - viewer] != null)
                result[1 - viewer].Dice = (int[])revealedDice[1 - viewer].Clone();
            return Array.AsReadOnly(result);
        }
        public void Begin()
        {
            if (Phase != VersusPhase.NotStarted) throw new InvalidOperationException("Already started");
            Round = 1; Prepare();
        }
        public bool TryReroll(int player, int round, long sequence, int heldMask, out string error)
        {
            if (!Validate(player, round, sequence, heldMask, out error)) return false;
            var p = players[player];
            if (Phase != VersusPhase.Playing || p.RerollsFinished) { error = "Reroll phase ended"; return false; }
            if (p.RerollsLeft == 0) { error = "No rerolls left"; return false; }
            if (heldMask == 31) { error = "At least one die must roll"; return false; }
            p.HeldMask = heldMask;
            for (int i = 0; i < 5; i++) if ((heldMask & (1 << i)) == 0) p.Dice[i] = random.Next(1, 7);
            p.RerollsLeft--; p.LastSequence = sequence;
            if (p.RerollsLeft == 0) p.RerollsFinished = true;
            AdvanceRerolls(); return true;
        }
        public bool TryFinishRerolls(int player, int round, long sequence, int heldMask, out string error)
        {
            if (!Validate(player, round, sequence, heldMask, out error)) return false;
            var p = players[player];
            if (Phase != VersusPhase.Playing || p.RerollsFinished) { error = "Reroll phase ended"; return false; }
            p.HeldMask = heldMask; p.RerollsFinished = true; p.LastSequence = sequence;
            AdvanceRerolls(); return true;
        }
        private void AdvanceRerolls()
        {
            if (players[0].RerollsFinished && players[1].RerollsFinished) Phase = VersusPhase.ChoosingRoles;
        }
        public bool TrySubmit(int player, int round, long sequence, ScoreHandKind kind, bool carry, int heldMask, out string error)
        {
            if (!Validate(player, round, sequence, heldMask, out error)) return false;
            var p = players[player];
            if (Phase != VersusPhase.ChoosingRoles) { error = "Rerolls not finished"; return false; }
            if ((int)kind < 0 || (int)kind >= ScoreRules.HandCount) { error = "Invalid role"; return false; }
            if (p.Used[(int)kind]) { error = "Role already used"; return false; }
            if (carry && p.CarryUsed) { error = "Carry already used"; return false; }
            var offer = ScoreRules.Evaluate(kind, p.Dice);
            p.HeldMask = heldMask; p.Kind = kind; p.Score = offer.Score;
            p.Used[(int)kind] = true; p.Carry = carry; p.CarryUsed |= carry; p.Submitted = true; p.LastSequence = sequence;
            p.CategoryScores[(int)kind] = offer.Score;
            // Only a genuinely formed Five (five of a kind) earns the extra reroll; an unformed declaration does not.
            p.NextRerollBonus = kind == ScoreHandKind.Five && offer.IsFormed;
            if (players[0].Submitted && players[1].Submitted) Resolve();
            return true;
        }
        private bool Validate(int player, int round, long sequence, int heldMask, out string error)
        {
            error = null;
            if (player < 0 || player > 1) error = "Invalid player";
            else if (Phase != VersusPhase.Playing && Phase != VersusPhase.ChoosingRoles) error = "Not accepting actions";
            else if (round != Round) error = "Wrong round";
            else if (sequence <= players[player].LastSequence) error = "Stale action";
            else if (players[player].Submitted) error = "Already submitted";
            else if (heldMask < 0 || heldMask > 31) error = "Invalid hold mask";
            return error == null;
        }
        private void Resolve()
        {
            bool reversed = (players[0].Kind == ScoreHandKind.Chance) != (players[1].Kind == ScoreHandKind.Chance);
            var forecast = new ScoreForecast(players[0].Score, players[1].Score, false, reversed);
            var receipt = new VersusRound { Round = Round, Scores = new[] { players[0].Score, players[1].Score }, Damage = new int[2], RawHpAfter = new int[2], Carry = new[] { players[0].Carry, players[1].Carry } };
            for (int i = 0; i < 2; i++)
            {
                receipt.Damage[i] = i == 0 ? forecast.DamageToPlayer : forecast.DamageToNpc;
                receipt.RawHpAfter[i] = players[i].Hp - receipt.Damage[i];
                players[i].Hp = Math.Max(0, receipt.RawHpAfter[i]);
            }
            LastRound = receipt; Phase = VersusPhase.Resolved;
            if (players[0].Hp == 0 || players[1].Hp == 0)
            {
                Phase = VersusPhase.Finished;
                Winner = receipt.RawHpAfter[0] == receipt.RawHpAfter[1] ? 2 : receipt.RawHpAfter[0] > receipt.RawHpAfter[1] ? 0 : 1;
            }
        }
        public void NextRound()
        {
            if (Phase != VersusPhase.Resolved) throw new InvalidOperationException("Round has not resolved");
            Round++; Prepare();
        }
        private void Prepare()
        {
            foreach (var p in players)
            {
                bool all = true; foreach (bool used in p.Used) all &= used;
                if (all) { Array.Clear(p.Used, 0, p.Used.Length); Array.Clear(p.CategoryScores, 0, p.CategoryScores.Length); }
                if (!p.Carry) { p.HeldMask = 0; for (int i = 0; i < 5; i++) p.Dice[i] = random.Next(1, 7); }
                p.Carry = false; p.Submitted = false; p.RerollsFinished = false; p.Score = 0;
                // Refresh happens above, so a formed Five declared just before a full inventory reset still pays out and can pay again.
                p.RerollsLeft = Math.Min(MaxRerolls, BaseRerolls + (p.NextRerollBonus ? 1 : 0));
                p.NextRerollBonus = false;
            }
            for (int i = 0; i < 2; i++) revealedDice[i] = (int[])players[i].Dice.Clone();
            Phase = VersusPhase.Playing;
        }
        public void Interrupt() { if (Phase != VersusPhase.Finished) Phase = VersusPhase.Interrupted; }
    }
}


