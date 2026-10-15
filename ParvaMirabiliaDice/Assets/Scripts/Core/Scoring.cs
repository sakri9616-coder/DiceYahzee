using System;

namespace ParvaMirabilia.Core
{
    /// <summary>Pure scoring helpers, kept free of any state so they are trivially testable.</summary>
    public static class Scoring
    {
        /// <summary>Sum of the values whose bit is set in <paramref name="mask"/>.</summary>
        public static int SumMask(int[] values, int mask)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            int total = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                    total += values[i];
            }
            return total;
        }

        /// <summary>Sum of every value.</summary>
        public static int Sum(int[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            int total = 0;
            for (int i = 0; i < values.Length; i++)
                total += values[i];
            return total;
        }

        /// <summary>
        /// Damage a strike deals: only the part that punches through the opposing ward,
        /// floored at zero. Symmetric for both sides, so the round resolves simultaneously.
        /// </summary>
        public static int Damage(int attackSum, int guardSum)
        {
            int damage = attackSum - guardSum;
            return damage > 0 ? damage : 0;
        }

        /// <summary>Shared by the match and practice: only a zero-damage exchange can push back.</summary>
        public static Side? ApplyPushback(int playerTotal, int npcTotal, int pushbackDamage,
            ref int damageToNpc, ref int damageToPlayer)
        {
            if (damageToNpc != 0 || damageToPlayer != 0 || playerTotal == npcTotal) return null;
            if (playerTotal > npcTotal)
            {
                damageToNpc = pushbackDamage;
                return Side.Player;
            }
            damageToPlayer = pushbackDamage;
            return Side.Npc;
        }

        /// <summary>
        /// The strongest pattern in a hand, or <see cref="Formation.None"/>.
        ///
        /// Exactly one formation is returned: a triple outranks a run, which outranks a pair.
        /// Returning several would make the player add things up again, which is the complaint
        /// this system exists to answer.
        ///
        /// Tie-breaks are deterministic (highest face first, then lowest die index) so the same
        /// dice always produce the same formation, for both the NPC and the tests.
        /// </summary>
        public static Formation FindFormation(int[] dice, GameConfig config)
        {
            if (dice == null)
                throw new ArgumentNullException(nameof(dice));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (!config.FormationsEnabled || dice.Length < 2)
                return Formation.None;

            int[] count = new int[7];

            for (int i = 0; i < dice.Length; i++)
            {
                int face = dice[i];
                if (face >= 1 && face <= 6)
                    count[face]++;
            }

            // --- triple: three of a kind, highest face first
            if (config.TripleBonus > 0)
            {
                for (int face = 6; face >= 1; face--)
                {
                    if (count[face] >= 3)
                        return new Formation(FormationKind.Triple, config.TripleBonus,
                            FirstIndicesOf(dice, face, 3));
                }
            }

            // --- run: consecutive faces, highest run first
            if (config.RunBonus > 0 && config.RunLength >= 3)
            {
                for (int low = 7 - config.RunLength; low >= 1; low--)
                {
                    bool complete = true;
                    for (int k = 0; k < config.RunLength; k++)
                    {
                        if (count[low + k] == 0)
                        {
                            complete = false;
                            break;
                        }
                    }

                    if (!complete)
                        continue;

                    var members = new int[config.RunLength];
                    for (int k = 0; k < config.RunLength; k++)
                        members[k] = FirstIndexOf(dice, low + k);

                    return new Formation(FormationKind.Run, config.RunBonus, members);
                }
            }

            // --- pair: two of a kind, highest face first
            if (config.PairBonus > 0)
            {
                for (int face = 6; face >= 1; face--)
                {
                    if (count[face] >= 2)
                        return new Formation(FormationKind.Pair, config.PairBonus,
                            FirstIndicesOf(dice, face, 2));
                }
            }

            return Formation.None;
        }

        private static int FirstIndexOf(int[] dice, int face)
        {
            for (int i = 0; i < dice.Length; i++)
            {
                if (dice[i] == face)
                    return i;
            }

            return 0;
        }

        private static int[] FirstIndicesOf(int[] dice, int face, int howMany)
        {
            var found = new int[howMany];
            int at = 0;
            for (int i = 0; i < dice.Length && at < howMany; i++)
            {
                if (dice[i] == face)
                    found[at++] = i;
            }

            return found;
        }
    }
}
