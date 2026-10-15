using System;
using System.Collections.Generic;

namespace ParvaMirabilia.Core
{
    public enum Side
    {
        Player = 0,
        Npc = 1
    }

    public enum MatchPhase
    {
        /// <summary>BeginMatch has not run yet.</summary>
        NotStarted = 0,
        /// <summary>Round started, no dice on the table yet.</summary>
        AwaitingRoll = 1,
        /// <summary>Dice rolled, the player may re-roll (up to the limit) or commit.</summary>
        AwaitingReroll = 2,
        /// <summary>Re-roll limit reached, only committing is left.</summary>
        AwaitingSplit = 3,
        /// <summary>Round resolved, the player must press on to the next round.</summary>
        Resolving = 4,
        /// <summary>The match produced a result.</summary>
        Finished = 5
    }

    public enum MatchResult
    {
        PlayerWin = 0,
        NpcWin = 1,
        Draw = 2
    }

    /// <summary>
    /// Allocation of one side's dice between strike (attack) and ward (guard).
    /// Masks are bit flags over the dice index (bit i = die i).
    /// </summary>
    public readonly struct SplitDecision
    {
        public readonly int AttackMask;
        public readonly int GuardMask;

        public SplitDecision(int attackMask, int guardMask)
        {
            AttackMask = attackMask;
            GuardMask = guardMask;
        }

        public int AttackCount
        {
            get { return CountBits(AttackMask); }
        }

        public int GuardCount
        {
            get { return CountBits(GuardMask); }
        }

        /// <summary>
        /// Validates an explicit index list split and converts it to masks.
        /// Every die must appear exactly once across both sides, and neither side may be empty.
        /// </summary>
        public static SplitDecision Validate(int[] attackIndices, int[] guardIndices, int diceCount)
        {
            if (attackIndices == null)
                throw new ArgumentException("攻撃側のダイスが指定されていません");
            if (guardIndices == null)
                throw new ArgumentException("守護側のダイスが指定されていません");
            if (attackIndices.Length == 0)
                throw new ArgumentException("攻撃側には1個以上のダイスが必要です");
            if (guardIndices.Length == 0)
                throw new ArgumentException("守護側には1個以上のダイスが必要です");

            int seen = 0;
            for (int i = 0; i < attackIndices.Length; i++)
                seen = AddIndex(seen, attackIndices[i], diceCount, "攻撃");
            for (int i = 0; i < guardIndices.Length; i++)
                seen = AddIndex(seen, guardIndices[i], diceCount, "守護");

            int all = (1 << diceCount) - 1;
            if (seen != all)
                throw new ArgumentException("すべてのダイスを攻撃と守護に1つずつ割り当ててください");

            int attackMask = 0;
            for (int i = 0; i < attackIndices.Length; i++)
                attackMask |= 1 << attackIndices[i];

            return new SplitDecision(attackMask, all & ~attackMask);
        }

        private static int AddIndex(int seen, int index, int diceCount, string label)
        {
            if (index < 0 || index >= diceCount)
                throw new ArgumentException(label + "側のダイス番号が範囲外です: " + index);
            int bit = 1 << index;
            if ((seen & bit) != 0)
                throw new ArgumentException("同じダイス（" + index + "）が重複して指定されました");
            return seen | bit;
        }

        public static int CountBits(int mask)
        {
            int count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }
            return count;
        }

        public override string ToString()
        {
            return "攻" + AttackCount + "/守" + GuardCount;
        }
    }

    /// <summary>Everything the UI needs to show one resolved round.</summary>
    public sealed class RoundOutcome
    {
        public int Round;
        public int[] PlayerDice = new int[0];
        public int[] NpcDice = new int[0];
        public bool[] PlayerKept = new bool[0];
        public int PlayerRerollsUsed;
        public int NpcRerollsUsed;
        public SplitDecision PlayerSplit;
        public SplitDecision NpcSplit;
        public int PlayerAttackSum;
        public int PlayerGuardSum;
        public int NpcAttackSum;
        public int NpcGuardSum;
        public int DamageToNpc;
        public int DamageToPlayer;

        /// <summary>The pattern each side rolled. Empty when formations are switched off.</summary>
        public Formation PlayerFormation = Formation.None;
        public Formation NpcFormation = Formation.None;

        /// <summary>
        /// The yaku each side declared. <see cref="YakuKind.None"/> on the hand-driven split path,
        /// so a replay of either rule set reads correctly.
        ///
        /// The opponent's yaku is only ever known here, after the reveal - which is the whole
        /// point of the simultaneous commit and the reason no part of this game may be sold as
        /// "reading" the other side (STEP5_REVIEW.md V5-3).
        /// </summary>
        public YakuKind PlayerYaku = YakuKind.None;
        public YakuKind NpcYaku = YakuKind.None;

        /// <summary>True when every die of the formation was committed to the strike side.</summary>
        public bool PlayerFormationCashed;
        public bool NpcFormationCashed;

        /// <summary>Extra damage actually paid out by a cashed formation.</summary>
        public int PlayerBonus;
        public int NpcBonus;

        /// <summary>True when neither strike landed and the stand-off was settled by push-back.</summary>
        public bool Pushback;

        /// <summary>The side whose roll total was higher; only meaningful when Pushback is true.</summary>
        public Side PushbackWinner;
        public int PlayerHpAfter;
        public int NpcHpAfter;
        public string NpcSummary = "";
        public bool PlayerEmpowered;
        public bool NpcEmpowered;
        public int PlayerManaGained;
        public int NpcManaGained;
        public int PlayerManaAfter;
        public int NpcManaAfter;
    }

    public sealed class MatchOutcome
    {
        public MatchResult Result;
        public int Rounds;
        public string Reason = "";

        public static MatchOutcome Create(MatchResult result, int rounds, string reason)
        {
            return new MatchOutcome { Result = result, Rounds = rounds, Reason = reason };
        }
    }

    public static class MatchResults
    {
        public static string DisplayName(MatchResult result)
        {
            switch (result)
            {
                case MatchResult.PlayerWin: return "勝利";
                case MatchResult.NpcWin: return "敗北";
                default: return "引き分け";
            }
        }
    }

    public enum FormationKind
    {
        None = 0,
        /// <summary>Two dice showing the same face.</summary>
        Pair = 1,
        /// <summary>Three dice showing the same face.</summary>
        Triple = 2,
        /// <summary>Three dice on consecutive faces (for example 4-5-6).</summary>
        Run = 3
    }

    /// <summary>
    /// A pattern read out of one side's five dice.
    ///
    /// Exactly one formation is reported per hand - the strongest one - because stacking several
    /// patterns would turn a five-dice round back into the arithmetic the player complained about.
    ///
    /// A formation only pays out when all of its dice sit on the strike side. That single rule is
    /// what stops the split from collapsing into one number: two splits with the same strike sum
    /// are worth different amounts as soon as one of them carries a formation and the other does
    /// not, so *which* dice go forward starts to matter.
    /// </summary>
    public sealed class Formation
    {
        public static readonly Formation None = new Formation(FormationKind.None, 0, new int[0]);

        public FormationKind Kind { get; private set; }

        /// <summary>Flat damage added on top of the strike, paid even when the strike did not land.</summary>
        public int Bonus { get; private set; }

        /// <summary>Indices into the dice array that make up this formation.</summary>
        public int[] Members { get; private set; }

        public Formation(FormationKind kind, int bonus, int[] members)
        {
            Kind = kind;
            Bonus = bonus;
            Members = members ?? new int[0];
        }

        public bool Exists
        {
            get { return Kind != FormationKind.None; }
        }

        public bool IsMember(int index)
        {
            for (int i = 0; i < Members.Length; i++)
            {
                if (Members[i] == index)
                    return true;
            }

            return false;
        }

        /// <summary>True when every die of the formation has been committed to the strike side.</summary>
        public bool IsCashedBy(int attackMask)
        {
            if (!Exists)
                return false;

            for (int i = 0; i < Members.Length; i++)
            {
                if ((attackMask & (1 << Members[i])) == 0)
                    return false;
            }

            return true;
        }

        public string DisplayName()
        {
            switch (Kind)
            {
                case FormationKind.Pair: return "ペア";
                case FormationKind.Triple: return "スリーカインド";
                case FormationKind.Run: return "ストレート";
                default: return "なし";
            }
        }
    }
}
