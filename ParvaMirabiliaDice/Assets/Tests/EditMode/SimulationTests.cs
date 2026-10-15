using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Tests
{
    /// <summary>
    /// Headless match simulations between differently built NPCs.
    /// Writes D:/MimoGame/logs/simulation-report.json so the design review can quote real numbers.
    ///
    /// Metric definitions (kept explicit because a wrong denominator once made a 12-round average
    /// look like a 22-round one):
    ///   averageRounds  = total rounds / matches this line took part in
    ///   winShare       = wins / (matches - draws), i.e. share of decided matches
    ///   roundLimitShare= matches that ran into GameConfig.MaxRounds (stall indicator)
    /// </summary>
    public class SimulationTests
    {
        private const int MatchesPerSeatOrder = 250;
        private const int BaseSeed = 20261009;

        private sealed class LineStats
        {
            /// <summary>Matches this line took part in (both seat orders are counted).</summary>
            public int Matches;
            public int Wins;
            public int Draws;
            public int Rounds;
            public int DamageDealt;
            public int Rerolls;
            public readonly int[] AttackCountHistogram = new int[6];
            public readonly int[] GuardCountHistogram = new int[6];

            public double MostUsedAttackShare()
            {
                int total = 0;
                int max = 0;
                for (int i = 1; i <= 4; i++)
                {
                    total += AttackCountHistogram[i];
                    if (AttackCountHistogram[i] > max)
                        max = AttackCountHistogram[i];
                }
                return total == 0 ? 0.0 : (double)max / total;
            }

            /// <summary>Rounds played per match this line participated in.</summary>
            public double AverageRounds
            {
                get { return Matches == 0 ? 0 : (double)Rounds / Matches; }
            }

            /// <summary>Rolls in which this line held any pattern at all.</summary>
            public int FormationRolls;

            /// <summary>Rolls in which the pattern was actually put on the strike side.</summary>
            public int FormationCashed;

            /// <summary>Damage that came from a pattern rather than from punch-through.</summary>
            public int BonusDamage;

            public double FormationCashedShare
            {
                get { return FormationRolls == 0 ? 0 : (double)FormationCashed / FormationRolls; }
            }

            public double BonusDamageShare
            {
                get { return DamageDealt == 0 ? 0 : (double)BonusDamage / DamageDealt; }
            }

            /// <summary>Share of decided matches won (draws excluded).</summary>
            public double WinShare
            {
                get { return Matches - Draws == 0 ? 0 : (double)Wins / (Matches - Draws); }
            }

            public double DamagePerMatch
            {
                get { return Matches == 0 ? 0 : (double)DamageDealt / Matches; }
            }

            public double AverageRerollsPerRound
            {
                get { return Rounds == 0 ? 0 : (double)Rerolls / Rounds; }
            }
        }

        private sealed class PairingResult
        {
            public NpcArchetype LineA;
            public NpcArchetype LineB;
            public int Matches;
            public int Draws;

            /// <summary>Matches that ran into the round limit - the stall indicator.</summary>
            public int MatchesAtRoundLimit;

            /// <summary>Draws caused by both sides landing at 0 HP with level damage.</summary>
            public int DoubleKoDraws;

            /// <summary>Draws caused by running out of rounds with both sides still standing.</summary>
            public int TimeLimitDraws;

            /// <summary>
            /// Matches the seat that moves first won. Both seats are meant to run the same
            /// policy, so a number far from half means the two drivers have drifted apart again.
            /// </summary>
            public int FirstSeatWins;
            public readonly LineStats A = new LineStats();
            public readonly LineStats B = new LineStats();

            public double DrawShare
            {
                get { return Matches == 0 ? 0 : (double)Draws / Matches; }
            }

            public double RoundLimitShare
            {
                get { return Matches == 0 ? 0 : (double)MatchesAtRoundLimit / Matches; }
            }
        }

        [Test]
        public void ArchetypeRoundRobin_EveryMatchIsDecidedAndAReportIsWritten()
        {
            RunRoundRobin(false, "simulation-report.json");
        }

        /// <summary>
        /// The same round-robin with the pattern layer on - the rules the shipped build actually
        /// plays. A new damage source can quietly undo guarantees the old rules had (draw rate,
        /// seat fairness, stall rate, match length), so both rule sets run with the same seeds
        /// and the same assertions, and their reports sit next to each other.
        /// </summary>
        [Test]
        public void FormationRoundRobin_KeepsTheBalanceGuarantees()
        {
            RunRoundRobin(true, "simulation-report-formations.json");
        }

        private static void RunRoundRobin(bool formations, string reportName)
        {
            var archetypes = (NpcArchetype[])Enum.GetValues(typeof(NpcArchetype));
            var pairings = new List<PairingResult>();

            int seedCursor = BaseSeed;
            for (int i = 0; i < archetypes.Length; i++)
            {
                for (int j = i; j < archetypes.Length; j++)
                {
                    var pairing = new PairingResult { LineA = archetypes[i], LineB = archetypes[j] };
                    RunPairing(pairing, formations, ref seedCursor);
                    pairings.Add(pairing);
                }
            }

            int totalMatches = 0;
            int totalDraws = 0;
            int totalAtLimit = 0;
            foreach (var pairing in pairings)
            {
                totalMatches += pairing.Matches;
                totalDraws += pairing.Draws;
                totalAtLimit += pairing.MatchesAtRoundLimit;
                Assert.That(pairing.Draws, Is.LessThan(pairing.Matches),
                    "every pairing must contain at least one decided match");
            }

            double drawShare = (double)totalDraws / totalMatches;
            Assert.That(totalMatches, Is.EqualTo(archetypes.Length * (archetypes.Length + 1) / 2 * MatchesPerSeatOrder * 2));

            // The report is written before any assertion that could fail, because a balance bug
            // is exactly the case where the numbers matter most: an assertion that destroys the
            // evidence of the thing it is asserting about is useless for tuning.
            WriteReport(pairings, totalMatches, totalDraws, totalAtLimit, formations, reportName);

            Assert.That(drawShare, Is.LessThan(0.30), "draws are too frequent: " + drawShare.ToString("P1"));

            // The mirror pairings must be roughly fair between the two seats.
            foreach (var pairing in pairings)
            {
                if (pairing.LineA != pairing.LineB)
                    continue;
                double share = (double)pairing.A.Wins / pairing.Matches;
                Assert.That(share, Is.InRange(0.35, 0.65),
                    "mirror match for " + pairing.LineA + " is lopsided: " + share.ToString("P1"));
            }

            // The mirror assertion above is archetype-blind: with the same line on both seats it
            // reads the same number whichever seat did well. This one measures the seat itself.
            // Both seats run NpcBrain.PlanReroll and NpcBrain.ChooseSplit, so anything outside
            // the noise band means the two drivers have separated again - which happened once,
            // and handed 72% of mirror matches to the first seat before the sweep caught it.
            int firstSeatWins = 0;
            foreach (var pairing in pairings)
                firstSeatWins += pairing.FirstSeatWins;
            double firstSeatShare = (double)firstSeatWins / totalMatches;
            Assert.That(firstSeatShare, Is.InRange(0.45, 0.55),
                "seat bias: the first seat wins " + firstSeatShare.ToString("P1"));

            // Match length. Aggressive/Aggressive is the shortest matchup by construction, so the
            // overall average is the figure the design cares about (a fight with no middle has no
            // mid-game decision to enjoy), while the per-pairing floor only catches a matchup
            // that has collapsed into a single swing.
            double totalRounds = 0;
            foreach (var pairing in pairings)
                totalRounds += (pairing.A.AverageRounds + pairing.B.AverageRounds) * 0.5;
            double overallRounds = totalRounds / pairings.Count;
            Assert.That(overallRounds, Is.GreaterThanOrEqualTo(3.0),
                "matches average too few rounds: " + overallRounds.ToString("0.00"));
            foreach (var pairing in pairings)
            {
                double averageRounds = (pairing.A.AverageRounds + pairing.B.AverageRounds) * 0.5;
                Assert.That(averageRounds, Is.GreaterThanOrEqualTo(1.2),
                    pairing.LineA + "/" + pairing.LineB + " ends too fast: " + averageRounds.ToString("0.00"));
            }
        }

        private static void RunPairing(PairingResult pairing, bool formations, ref int seedCursor)
        {
            RunSeat(pairing, pairing.LineA, pairing.LineB, MatchesPerSeatOrder, ref seedCursor, pairing.A, pairing.B, formations);
            RunSeat(pairing, pairing.LineB, pairing.LineA, MatchesPerSeatOrder, ref seedCursor, pairing.B, pairing.A, formations);
        }

        private static void RunSeat(PairingResult pairing, NpcArchetype playerLine, NpcArchetype npcLine,
            int matches, ref int seedCursor, LineStats playerStats, LineStats npcStats, bool formations)
        {
            // The shipped rule set for the pattern layer, so what this test asserts is what the
            // build actually plays rather than a default the UI never uses.
            var config = formations
                ? new GameConfig().ApplyShippedPatternLayer()
                : new GameConfig();

            for (int m = 0; m < matches; m++)
            {
                int seed = seedCursor++;
                var engine = new MatchEngine(config, new SeededRandom(seed), npcLine);
                var outcome = MatchRunner.Run(engine, playerLine);

                pairing.Matches++;
                playerStats.Matches++;
                npcStats.Matches++;
                if (outcome.Rounds >= config.MaxRounds)
                    pairing.MatchesAtRoundLimit++;
                playerStats.Rounds += engine.History.Count;
                npcStats.Rounds += engine.History.Count;

                if (outcome.Result == MatchResult.Draw)
                {
                    pairing.Draws++;
                    playerStats.Draws++;
                    npcStats.Draws++;
                    // Both sides at 0 HP means the double-knockout branch ran; otherwise the
                    // round limit ran out with both fighters still standing.
                    bool bothDown = engine.PlayerHp <= 0 && engine.NpcHp <= 0;
                    if (bothDown) pairing.DoubleKoDraws++;
                    else pairing.TimeLimitDraws++;
                }
                else if (outcome.Result == MatchResult.PlayerWin)
                {
                    playerStats.Wins++;
                    pairing.FirstSeatWins++;
                }
                else
                {
                    npcStats.Wins++;
                }

                foreach (var round in engine.History)
                {
                    playerStats.AttackCountHistogram[round.PlayerSplit.AttackCount]++;
                    playerStats.GuardCountHistogram[round.PlayerSplit.GuardCount]++;
                    playerStats.Rerolls += round.PlayerRerollsUsed;
                    playerStats.DamageDealt += round.DamageToNpc;
                    playerStats.FormationRolls++;
                    if (round.PlayerFormationCashed) playerStats.FormationCashed++;
                    playerStats.BonusDamage += round.PlayerBonus;

                    npcStats.AttackCountHistogram[round.NpcSplit.AttackCount]++;
                    npcStats.GuardCountHistogram[round.NpcSplit.GuardCount]++;
                    npcStats.Rerolls += round.NpcRerollsUsed;
                    npcStats.DamageDealt += round.DamageToPlayer;
                    npcStats.FormationRolls++;
                    if (round.NpcFormationCashed) npcStats.FormationCashed++;
                    npcStats.BonusDamage += round.NpcBonus;
                }
            }
        }

        private static void WriteReport(List<PairingResult> pairings, int totalMatches, int totalDraws,
            int totalAtLimit, bool formations, string reportName)
        {
            var logsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "logs"));
            Directory.CreateDirectory(logsDir);
            var path = Path.Combine(logsDir, reportName);

            // One line per rule set, so the two reports can be diffed without reading pairings.
            double allRounds = 0, allMatches = 0, allDamage = 0;
            foreach (var p in pairings)
            {
                allRounds += p.A.Rounds + p.B.Rounds;
                allMatches += p.A.Matches + p.B.Matches;
                allDamage += p.A.DamageDealt + p.B.DamageDealt;
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"generatedAt\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\",");
            sb.AppendLine("  \"formations\": " + (formations ? "true" : "false") + ",");
            sb.AppendLine("  \"seedBase\": " + BaseSeed + ",");
            sb.AppendLine("  \"matchesPerSeatOrder\": " + MatchesPerSeatOrder + ",");
            sb.AppendLine("  \"totalMatches\": " + totalMatches + ",");
            int firstSeatWins = 0;
            foreach (var p in pairings)
                firstSeatWins += p.FirstSeatWins;
            sb.AppendLine("  \"firstSeatWinShare\": " + ((double)firstSeatWins / totalMatches).ToString("0.0000") + ",");
            sb.AppendLine("  \"averageRounds\": " + (allRounds / allMatches).ToString("0.00") + ",");
            sb.AppendLine("  \"damagePerMatch\": " + (allDamage / allMatches).ToString("0.00") + ",");
            sb.AppendLine("  \"totalDraws\": " + totalDraws + ",");
            sb.AppendLine("  \"drawShare\": " + ((double)totalDraws / totalMatches).ToString("0.0000") + ",");
            sb.AppendLine("  \"totalAtRoundLimit\": " + totalAtLimit + ",");
            sb.AppendLine("  \"roundLimitShare\": " + ((double)totalAtLimit / totalMatches).ToString("0.0000") + ",");
            sb.AppendLine("  \"metrics\": \"averageRounds=rounds/matches, winShare excludes draws, roundLimitShare=matches hitting MaxRounds\",");
            sb.AppendLine("  \"pairings\": [");

            for (int i = 0; i < pairings.Count; i++)
            {
                var p = pairings[i];
                sb.AppendLine("    {");
                sb.AppendLine("      \"lineA\": \"" + p.LineA + "\",");
                sb.AppendLine("      \"lineB\": \"" + p.LineB + "\",");
                sb.AppendLine("      \"matches\": " + p.Matches + ",");
                sb.AppendLine("      \"draws\": " + p.Draws + ",");
                sb.AppendLine("      \"drawShare\": " + p.DrawShare.ToString("0.0000") + ",");
                sb.AppendLine("      \"matchesAtRoundLimit\": " + p.MatchesAtRoundLimit + ",");
                sb.AppendLine("      \"roundLimitShare\": " + p.RoundLimitShare.ToString("0.0000") + ",");
                sb.AppendLine("      \"doubleKoDraws\": " + p.DoubleKoDraws + ",");
                sb.AppendLine("      \"timeLimitDraws\": " + p.TimeLimitDraws + ",");
                sb.AppendLine("      \"a\": " + LineJson(p.A) + ",");
                sb.AppendLine("      \"b\": " + LineJson(p.B));
                sb.Append("    }");
                sb.AppendLine(i < pairings.Count - 1 ? "," : "");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[SIM] report written to " + path + " (" + totalMatches + " matches)");
        }

        private static string LineJson(LineStats stats)
        {
            var sb = new StringBuilder();
            sb.Append("{\"matches\": " + stats.Matches);
            sb.Append(", \"wins\": " + stats.Wins);
            sb.Append(", \"draws\": " + stats.Draws);
            sb.Append(", \"winShare\": " + stats.WinShare.ToString("0.0000"));
            sb.Append(", \"averageRounds\": " + stats.AverageRounds.ToString("0.00"));
            sb.Append(", \"damagePerMatch\": " + stats.DamagePerMatch.ToString("0.00"));
            sb.Append(", \"averageRerollsPerRound\": " + stats.AverageRerollsPerRound.ToString("0.00"));
            sb.Append(", \"mostUsedAttackShare\": " + stats.MostUsedAttackShare().ToString("0.0000"));
            sb.Append(", \"attackHistogram\": [" + HistogramJson(stats.AttackCountHistogram, 1, 4) + "]");
            sb.Append(", \"guardHistogram\": [" + HistogramJson(stats.GuardCountHistogram, 1, 4) + "]");
            // Whether the pattern layer actually fires, and how much of the total damage it
            // carries. A pattern nobody ever cashes would be decoration, not a rule.
            sb.Append(", \"formationRolls\": " + stats.FormationRolls);
            sb.Append(", \"formationCashedShare\": " + stats.FormationCashedShare.ToString("0.0000"));
            sb.Append(", \"bonusDamageShare\": " + stats.BonusDamageShare.ToString("0.0000"));
            sb.Append("}");
            return sb.ToString();
        }

        private static string HistogramJson(int[] histogram, int from, int to)
        {
            var parts = new List<string>();
            for (int i = from; i <= to; i++)
                parts.Add(histogram[i].ToString());
            return string.Join(", ", parts.ToArray());
        }
    }
}
