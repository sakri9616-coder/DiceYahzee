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
    /// Side-by-side comparison of candidate pattern-layer settings.
    ///
    /// The first measurement of the shipped pattern layer (logs/simulation-report-formations.json)
    /// showed the pair bonus firing on 61-97% of rounds and carrying 24-43% of all damage, which
    /// halved the average match from 4.65 rounds to 2.37. A pattern that almost always happens is
    /// not a "hand worth building" - it is a tax. The numbers below exist to choose settings by
    /// evidence rather than by feel, and to keep the chosen ones from drifting back.
    ///
    /// Targets, taken from DESIGN_REVIEW_V2 section 6:
    ///   averageRounds      ~4-4.6 (a match needs a middle to have decisions in it)
    ///   drawShare          not worse than the 0.0133 baseline
    ///   roundLimitShare    not worse than the 0.0707 baseline
    ///   cashedShare        clearly below 1: the pattern must be something you build, not something you are handed
    /// </summary>
    public class FormationSweepTests
    {
        private const int MatchesPerSeatOrder = 250;
        private const int BaseSeed = 20261010;
        private const string ReportName = "formation-sweep.json";

        private sealed class Candidate
        {
            public string Label;
            public bool Formations;
            public int PairBonus;
            public int TripleBonus;
            public int RunBonus;
            public int RunLength;
            public int StartingHp;
        }

        private sealed class Accum
        {
            public int Matches;
            public int Draws;
            public int AtLimit;
            public int Rounds;
            public int Damage;
            public int BonusDamage;
            public int FormationRolls;
            public int FormationCashed;
            public int Wins;
        }

        [Test]
        public void FormationSweep_WritesCandidateBalances()
        {
            var candidates = new[]
            {
                new Candidate { Label = "v1-baseline",          Formations = false, PairBonus = 2, TripleBonus = 5, RunBonus = 4, RunLength = 3, StartingHp = 20 },
                new Candidate { Label = "current p2 run3 hp20",  Formations = true,  PairBonus = 2, TripleBonus = 5, RunBonus = 4, RunLength = 3, StartingHp = 20 },
                new Candidate { Label = "noPair run3 hp20",      Formations = true,  PairBonus = 0, TripleBonus = 5, RunBonus = 4, RunLength = 3, StartingHp = 20 },
                new Candidate { Label = "noPair run4 hp20",      Formations = true,  PairBonus = 0, TripleBonus = 5, RunBonus = 5, RunLength = 4, StartingHp = 20 },
                new Candidate { Label = "noPair run4 hp26",      Formations = true,  PairBonus = 0, TripleBonus = 5, RunBonus = 5, RunLength = 4, StartingHp = 26 },
                new Candidate { Label = "SHIPPED noPair run4 hp32", Formations = true,  PairBonus = 0, TripleBonus = 5, RunBonus = 5, RunLength = 4, StartingHp = 32 },
                new Candidate { Label = "pair1 run4 hp26",       Formations = true,  PairBonus = 1, TripleBonus = 5, RunBonus = 5, RunLength = 4, StartingHp = 26 },
                new Candidate { Label = "noPair run4 tri7 hp30", Formations = true,  PairBonus = 0, TripleBonus = 7, RunBonus = 6, RunLength = 4, StartingHp = 30 },
            };

            var rows = new List<string>();
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"generatedAt\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\",");
            json.AppendLine("  \"matchesPerSeatOrder\": " + MatchesPerSeatOrder + ",");
            json.AppendLine("  \"candidates\": [");

            for (int c = 0; c < candidates.Length; c++)
            {
                var result = Measure(candidates[c]);
                string line = string.Format(
                    "{0,-22} run={1} hp={2,2} rounds={3,5:N2} dmg={4,6:N1} draw={5,6:N4} limit={6,6:N4} cash={7,6:N3} bonus={8,6:N3} first={9,5:N3}",
                    result.Label, result.RunLen, result.Hp, result.AverageRounds, result.DamagePerMatch,
                    result.DrawShare, result.RoundLimitShare, result.CashedShare,
                    result.BonusShare, result.FirstSeatShare);
                rows.Add(line);
                json.AppendLine("    {");
                json.AppendLine("      \"label\": \"" + result.Label + "\",");
                json.AppendLine("      \"formations\": " + (result.Formations ? "true" : "false") + ",");
                json.AppendLine("      \"pairBonus\": " + result.Pair + ",");
                json.AppendLine("      \"tripleBonus\": " + result.Triple + ",");
                json.AppendLine("      \"runBonus\": " + result.Run + ",");
                json.AppendLine("      \"runLength\": " + result.RunLen + ",");
                json.AppendLine("      \"startingHp\": " + result.Hp + ",");
                json.AppendLine("      \"averageRounds\": " + result.AverageRounds.ToString("0.00") + ",");
                json.AppendLine("      \"damagePerMatch\": " + result.DamagePerMatch.ToString("0.00") + ",");
                json.AppendLine("      \"drawShare\": " + result.DrawShare.ToString("0.0000") + ",");
                json.AppendLine("      \"roundLimitShare\": " + result.RoundLimitShare.ToString("0.0000") + ",");
                json.AppendLine("      \"formationCashedShare\": " + result.CashedShare.ToString("0.0000") + ",");
                json.AppendLine("      \"bonusDamageShare\": " + result.BonusShare.ToString("0.0000") + ",");
                json.AppendLine("      \"firstSeatWinShare\": " + result.FirstSeatShare.ToString("0.0000"));
                json.Append("    }");
                json.AppendLine(c < candidates.Length - 1 ? "," : "");
            }

            json.AppendLine("  ]");
            json.AppendLine("}");

            var logsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "logs"));
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, ReportName), json.ToString(), new UTF8Encoding(false));

            foreach (string row in rows)
                Debug.Log("[FormationSweep] " + row);

            Assert.That(rows.Count, Is.EqualTo(candidates.Length));
        }

        private sealed class Measured
        {
            public string Label;
            public bool Formations;
            public int Pair, Triple, Run, RunLen, Hp;
            public double AverageRounds, DamagePerMatch, DrawShare, RoundLimitShare;
            public double CashedShare, BonusShare, FirstSeatShare;
        }

        private static Measured Measure(Candidate candidate)
        {
            var archetypes = (NpcArchetype[])Enum.GetValues(typeof(NpcArchetype));
            var config = new GameConfig
            {
                FormationsEnabled = candidate.Formations,
                PairBonus = candidate.PairBonus,
                TripleBonus = candidate.TripleBonus,
                RunBonus = candidate.RunBonus,
                RunLength = candidate.RunLength,
                StartingHp = candidate.StartingHp
            };

            var totals = new Accum();
            // Win share of the seat that plays first, measured only over mirror pairings: the
            // only place where a seat advantage cannot be mistaken for an archetype advantage.
            var mirror = new Acc2();

            int seedCursor = BaseSeed;
            for (int i = 0; i < archetypes.Length; i++)
            {
                for (int j = i; j < archetypes.Length; j++)
                {
                    var a = new Accum();
                    var b = new Accum();
                    RunSeat(config, archetypes[i], archetypes[j], ref seedCursor, a, b, totals);
                    RunSeat(config, archetypes[j], archetypes[i], ref seedCursor, b, a, totals);

                    if (archetypes[i] == archetypes[j])
                    {
                        // Two seat orders of the same archetype: 500 matches, of which a.Wins and
                        // b.Wins are exactly the ones the first seat took.
                        mirror.Matches += MatchesPerSeatOrder * 2;
                        mirror.First += a.Wins + b.Wins;
                    }
                }
            }

            var result = new Measured
            {
                Label = candidate.Label,
                Formations = candidate.Formations,
                Pair = candidate.PairBonus,
                Triple = candidate.TripleBonus,
                Run = candidate.RunBonus,
                RunLen = candidate.RunLength,
                Hp = candidate.StartingHp,
                AverageRounds = totals.Matches == 0 ? 0 : (double)totals.Rounds / totals.Matches,
                DamagePerMatch = totals.Matches == 0 ? 0 : (double)totals.Damage / totals.Matches,
                DrawShare = totals.Matches == 0 ? 0 : (double)totals.Draws / totals.Matches,
                RoundLimitShare = totals.Matches == 0 ? 0 : (double)totals.AtLimit / totals.Matches,
                CashedShare = totals.FormationRolls == 0 ? 0 : (double)totals.FormationCashed / totals.FormationRolls,
                BonusShare = totals.Damage == 0 ? 0 : (double)totals.BonusDamage / totals.Damage,
                FirstSeatShare = mirror.Matches == 0 ? 0 : (double)mirror.First / mirror.Matches
            };
            return result;
        }

        private sealed class Acc2
        {
            public int Matches;
            public int First;
        }

        private static void RunSeat(GameConfig config, NpcArchetype playerLine, NpcArchetype npcLine,
            ref int seedCursor, Accum player, Accum npc, Accum totals)
        {
            for (int m = 0; m < MatchesPerSeatOrder; m++)
            {
                int seed = seedCursor++;
                var engine = new MatchEngine(config, new SeededRandom(seed), npcLine);
                var outcome = MatchRunner.Run(engine, playerLine);

                totals.Matches++;
                player.Matches++;
                npc.Matches++;
                totals.Rounds += engine.History.Count;
                if (outcome.Rounds >= config.MaxRounds) totals.AtLimit++;
                if (outcome.Result == MatchResult.Draw) totals.Draws++;
                else if (outcome.Result == MatchResult.PlayerWin) player.Wins++;

                foreach (var round in engine.History)
                {
                    totals.Damage += round.DamageToNpc + round.DamageToPlayer;
                    totals.BonusDamage += round.PlayerBonus + round.NpcBonus;
                    totals.FormationRolls += 2;
                    if (round.PlayerFormationCashed) totals.FormationCashed++;
                    if (round.NpcFormationCashed) totals.FormationCashed++;
                    player.Damage += round.DamageToNpc;
                    npc.Damage += round.DamageToPlayer;
                }
            }
        }
    }
}
