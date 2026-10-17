using System;
using System.Collections.Generic;

namespace ParvaMirabilia.Core
{
    /// <summary>
    /// Exact finite-dice planning over 252 five-die histograms and 462 possible holds.
    /// The 4368 weighted edges are generated once. Each inventory/personality policy
    /// then needs two short dynamic-programming passes rather than sampled guesses.
    /// </summary>
    internal static class ScoreHoldPolicyExact
    {
        private sealed class Policy
        {
            internal int[] One;
            internal int[] Two;
            internal int[] Three;
        }

        private static readonly int[] Factorial = { 1, 1, 2, 6, 24, 120 };
        private static readonly int[][][] Vectors = BuildVectors();
        private static readonly int[][] States = Vectors[5];
        private static readonly int[][] Keeps = BuildKeeps();
        private static readonly int[] StateIndexes = BuildStateIndexes();
        private static readonly int[][] Compatible = BuildCompatible();
        private static readonly int[][] Next = new int[Keeps.Length][];
        private static readonly double[][] Probability = new double[Keeps.Length][];
        private static readonly int[] CanonicalCodes = BuildCanonicalCodes();
        private static readonly Dictionary<int, Policy> Cache = new Dictionary<int, Policy>();

        static ScoreHoldPolicyExact() { BuildTransitions(); }

        internal static int Plan(int[] dice, bool[] available, int rerollsLeft, int round, NpcPersonality personality)
        {
            int inventory = 0;
            for (int i = 0; i < available.Length; i++) if (available[i]) inventory |= 1 << i;
            if (inventory == 0) throw new InvalidOperationException("未使用の役がありません");
            int key = inventory | (round < 5 ? 0 : 1 << 9) | ((int)personality << 10);
            Policy policy;
            lock (Cache)
            {
                if (!Cache.TryGetValue(key, out policy))
                {
                    policy = Solve(available, round, personality); Cache.Add(key, policy);
                }
            }
            var counts = new int[6];
            for (int i = 0; i < dice.Length; i++) counts[dice[i] - 1]++;
            int state = StateIndexes[Code(counts)];
            int keep = rerollsLeft >= 3 ? policy.Three[state] : rerollsLeft == 2 ? policy.Two[state] : policy.One[state];
            // -1 means stop; holding all is never used as a dummy consumed reroll.
            if (keep < 0) return 31;
            var remaining = (int[])Keeps[keep].Clone();
            int mask = 0;
            for (int i = 0; i < 5; i++)
            {
                int face = dice[i] - 1;
                if (remaining[face] > 0) { remaining[face]--; mask |= 1 << i; }
            }
            return mask;
        }

        private static Policy Solve(bool[] available, int round, NpcPersonality personality)
        {
            var stop = new double[States.Length];
            for (int state = 0; state < stop.Length; state++)
                stop[state] = ScoreHoldPolicy.Utility(
                    ScoreHoldPolicy.Select(CanonicalCodes[state], available, round, personality), round, personality);
            var oneValues = new double[States.Length];
            var policy = new Policy { One = new int[States.Length], Two = new int[States.Length], Three = new int[States.Length] };
            Pass(stop, stop, oneValues, policy.One);
            var twoValues = new double[States.Length];
            Pass(oneValues, stop, twoValues, policy.Two);
            Pass(twoValues, stop, new double[States.Length], policy.Three);
            return policy;
        }

        private static void Pass(double[] continuation, double[] stop, double[] values, int[] choice)
        {
            var expected = new double[Keeps.Length];
            for (int keep = 0; keep < Keeps.Length; keep++)
                for (int edge = 0; edge < Next[keep].Length; edge++)
                    expected[keep] += continuation[Next[keep][edge]] * Probability[keep][edge];
            for (int state = 0; state < States.Length; state++)
            {
                double best = stop[state];
                int bestKeep = -1;
                for (int i = 0; i < Compatible[state].Length; i++)
                {
                    int keep = Compatible[state][i];
                    // A five-die hold is equivalent to stopping; exclude delayed rerolls.
                    if (Next[keep].Length == 1) continue;
                    if (expected[keep] > best + 0.0000001)
                    {
                        best = expected[keep]; bestKeep = keep;
                    }
                }
                values[state] = best; choice[state] = bestKeep;
            }
        }

        private static int[][][] BuildVectors()
        {
            var result = new int[6][][];
            for (int n = 0; n <= 5; n++)
            {
                var list = new List<int[]>();
                AddVector(list, new int[6], 0, n);
                result[n] = list.ToArray();
            }
            return result;
        }

        private static void AddVector(List<int[]> list, int[] vector, int face, int remaining)
        {
            if (face == 5)
            {
                vector[face] = remaining; list.Add((int[])vector.Clone()); return;
            }
            for (int n = 0; n <= remaining; n++)
            {
                vector[face] = n; AddVector(list, vector, face + 1, remaining - n);
            }
        }

        private static int[][] BuildKeeps()
        {
            var keeps = new List<int[]>();
            for (int n = 0; n <= 5; n++) keeps.AddRange(Vectors[n]);
            return keeps.ToArray();
        }

        private static int[] BuildStateIndexes()
        {
            var indexes = new int[46656];
            for (int state = 0; state < States.Length; state++) indexes[Code(States[state])] = state;
            return indexes;
        }

        private static int[][] BuildCompatible()
        {
            var result = new int[States.Length][];
            for (int state = 0; state < States.Length; state++)
            {
                var compatible = new List<int>();
                for (int keep = 0; keep < Keeps.Length; keep++)
                {
                    bool fits = true;
                    for (int face = 0; face < 6; face++) fits &= Keeps[keep][face] <= States[state][face];
                    if (fits) compatible.Add(keep);
                }
                result[state] = compatible.ToArray();
            }
            return result;
        }

        private static int[] BuildCanonicalCodes()
        {
            var result = new int[States.Length];
            for (int state = 0; state < States.Length; state++)
            {
                int code = 0;
                for (int face = 0; face < 6; face++)
                    for (int n = 0; n < States[state][face]; n++) code = code * 6 + face;
                result[state] = code;
            }
            return result;
        }

        private static void BuildTransitions()
        {
            var combined = new int[6];
            for (int keep = 0; keep < Keeps.Length; keep++)
            {
                int held = 0;
                for (int face = 0; face < 6; face++) held += Keeps[keep][face];
                int rolled = 5 - held;
                var outcomes = Vectors[rolled];
                Next[keep] = new int[outcomes.Length];
                Probability[keep] = new double[outcomes.Length];
                for (int outcome = 0; outcome < outcomes.Length; outcome++)
                {
                    int denominator = 1;
                    for (int face = 0; face < 6; face++)
                    {
                        combined[face] = Keeps[keep][face] + outcomes[outcome][face];
                        denominator *= Factorial[outcomes[outcome][face]];
                    }
                    Next[keep][outcome] = StateIndexes[Code(combined)];
                    Probability[keep][outcome] = (double)Factorial[rolled] / denominator / Math.Pow(6, rolled);
                }
            }
        }

        private static int Code(int[] counts)
        {
            int code = 0;
            for (int i = 0; i < 6; i++) code = code * 6 + counts[i];
            return code;
        }
    }
}
