using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Tests
{
    public static class ScoreDuelBenchmark
    {
        [Serializable]
        public sealed class PolicyResult
        {
            public string npc;
            public string policy;
            public int matches;
            public int wins;
            public int draws;
            public int losses;
            public int rounds;
            public int zeroCount;
            public int unformedCount;
            public int playerScore;
            public int playerBaseScore;
            public int playerBonusScore;
            public int npcScore;
            public int npcUnformedCount;
            public int npcRerolls;
            public double meanNpcRoundScore;
            public int rerolls;
            public int saves;
            public int restores;
            public int maxRounds;
            public double meanRoundScore;
            public double meanRerolls;
            public int[] roleCounts = new int[9];
        }

        [Serializable]
        public sealed class Report
        {
            public string generatedUtc;
            public string method;
            public string[] roles = ScoreRules.Names;
            public int seedsPerPolicy;
            public long elapsedMilliseconds;
            public PolicyResult[] results;
        }

        // Also callable through Unity -executeMethod ParvaMirabilia.Tests.ScoreDuelBenchmark.Run.
        public static void Run()
        {
            var report = Measure(100);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var parent = Directory.GetParent(project);
            string workspace = parent.Name == ".validation" ? parent.Parent.FullName : parent.FullName;
            string output = Path.Combine(workspace, "logs", "score-duel-benchmark-v08-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            UnityEngine.Debug.Log("Score duel benchmark: " + output + " / " + report.elapsedMilliseconds + " ms");
        }

        public static Report Measure(int samples)
        {
            if (samples <= 0) throw new ArgumentOutOfRangeException(nameof(samples));
            ScoreRules.Evaluate(ScoreHandKind.Five, new[] { 1, 1, 1, 1, 1 });
            var timer = Stopwatch.StartNew();
            var results = new PolicyResult[Enum.GetValues(typeof(NpcPersonality)).Length * 4];
            int index = 0;
            foreach (NpcPersonality npc in Enum.GetValues(typeof(NpcPersonality)))
                for (int policy = 0; policy < 4; policy++)
                {
                    var result = new PolicyResult
                    {
                        npc = npc.ToString(), policy = policy == 0 ? "pattern" : policy == 1 ? "highFaces" : policy == 2 ? "earlyStop" : "saveAttack",
                        matches = samples
                    };
                    for (int seed = 0; seed < samples; seed++) Play(seed, npc, policy, result);
                    result.losses = samples - result.wins - result.draws;
                    result.meanRoundScore = (double)result.playerScore / result.rounds;
                    result.meanRerolls = (double)result.rerolls / result.rounds;
                    result.meanNpcRoundScore = (double)result.npcScore / result.rounds;
                    results[index++] = result;
                }
            timer.Stop();
            return new Report
            {
                generatedUtc = DateTime.UtcNow.ToString("o"), seedsPerPolicy = samples,
                elapsedMilliseconds = timer.ElapsedMilliseconds, results = results,
                method = "Actual ScoreDuel, seeds 0 through seedsPerPolicy-1 per personality and policy. Separate real NPC/human RNG streams; exact planning has no RNG. Balanced NPC uses one reroll with visible formed-hand/group/high-face holds; other NPCs retain the prior exact two-reroll policy. " +
                    "pattern: NPC's exact one/two-reroll pattern hold policy, then best unused category. " +
                    "highFaces: keep >=4, up to two rerolls until all held, then best unused category. " +
                    "earlyStop: commit best unused when its score meets the public NPC score; otherwise pattern holds. " +
                    "saveAttack: pattern holds; save once when a surviving full exchange secures a knockout unavailable normally, or when a multiplier adds >=12 points while trailing the NPC and leaves >=20 HP. " +
                    "All policies use floor((20+sum)*role multiplier), HP240, simultaneous damage after common floor(minScore/2) absorption, and raw remaining HP for double KO. Scorecards refresh only when exhausted. " +
                    "Equal seeds do not imply equal match duration; these bounded heuristic comparisons do not prove fun or optimal match strategy."
            };
        }

        private static void Play(int seed, NpcPersonality npc, int policy, PolicyResult result)
        {
            var game = new ScoreDuel(seed, npc);
            while (game.Phase != MatchPhase.Finished)
            {
                if (game.Phase == MatchPhase.AwaitingRoll) game.Roll();
                else result.restores++;
                var available = new bool[9];
                for (int i = 0; i < available.Length; i++) available[i] = !game.PlayerUsed[i];
                while (game.RerollsLeft > 0)
                {
                    if (policy == 2 && ScoreRules.Best(game.PlayerDice, available).Score >= game.Intent.Score) break;
                    bool[] held;
                    if (policy == 1)
                    {
                        held = new bool[5];
                        for (int i = 0; i < 5; i++) held[i] = game.PlayerDice[i] >= 4;
                    }
                    else held = ScoreRules.SuggestHeld(game.PlayerDice, available, game.RerollsLeft,
                        unchecked(seed * 97 + game.Round * 123 + game.RerollsLeft * 31), game.Round);
                    bool allHeld = true;
                    for (int i = 0; i < held.Length; i++) allHeld &= held[i];
                    if (allHeld) break;
                    game.SetHeld(held); game.Reroll();
                }
                var chosen = ScoreRules.Best(game.PlayerDice, available);
                bool save = policy == 3 && !game.SavedHandUsed && game.PlayerHp > game.Intent.Score &&
                    ((chosen.Score >= game.NpcHp && game.Forecast(chosen.Kind).DamageToNpc < game.NpcHp) ||
                    (chosen.BonusScore >= 12 && game.PlayerHp >= game.Intent.Score + 20 && game.Intent.Score >= chosen.Score));
                var receipt = save ? game.SaveHand(chosen.Kind) : game.Commit(chosen.Kind);
                result.rounds++;
                result.playerScore += receipt.PlayerScore;
                result.playerBaseScore += receipt.PlayerBaseScore;
                result.playerBonusScore += receipt.PlayerBonusScore;
                result.npcScore += receipt.NpcScore;
                result.npcRerolls += receipt.NpcRerollsUsed;
                if (!ScoreRules.Evaluate(receipt.NpcKind, receipt.NpcDice).IsFormed) result.npcUnformedCount++;
                if (receipt.IsSaved) result.saves++;
                result.rerolls += receipt.PlayerRerollsUsed;
                result.roleCounts[(int)receipt.PlayerKind]++;
                if (receipt.PlayerScore == 0) result.zeroCount++;
                if (!chosen.IsFormed) result.unformedCount++;
                if (game.Phase != MatchPhase.Finished) game.NextRound();
            }
            if (game.Outcome.Result == MatchResult.PlayerWin) result.wins++;
            if (game.Outcome.Result == MatchResult.Draw) result.draws++;
            result.maxRounds = Math.Max(result.maxRounds, game.Round);
        }
    }

    public sealed class ScoreDuelBenchmarkTests
    {
        [Test]
        public void ActualSixteenPolicyComparisonsWriteTheQualityBenchmark()
        {
            ScoreDuelBenchmark.Run();
        }

        [Test]
        public void SmallActualPolicyComparisonHasCompleteReceiptsAndFiniteScorecards()
        {
            var report = ScoreDuelBenchmark.Measure(2);
            Assert.That(report.results.Length, Is.EqualTo(16));
            foreach (var result in report.results)
            {
                Assert.That(result.wins + result.draws + result.losses, Is.EqualTo(2));
                Assert.That(result.rounds, Is.InRange(2, 2 * ScoreDuel.SafetyRoundBound));
                Assert.That(result.zeroCount, Is.Zero);
                Assert.That(result.playerScore, Is.EqualTo(result.playerBaseScore + result.playerBonusScore));
                Assert.That(result.rerolls, Is.InRange(0, result.rounds * 2));
                Assert.That(result.saves, Is.InRange(0, 2));
                Assert.That(result.restores, Is.InRange(0, result.saves));
                int roles = 0;
                foreach (int count in result.roleCounts) roles += count;
                Assert.That(roles, Is.EqualTo(result.rounds));
                Assert.That(result.roleCounts[(int)ScoreHandKind.Chance], Is.LessThanOrEqualTo(4));
                Assert.That(result.maxRounds, Is.LessThanOrEqualTo(ScoreDuel.SafetyRoundBound));
            }
        }
    }
}
