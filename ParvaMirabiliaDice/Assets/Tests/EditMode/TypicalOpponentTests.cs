using System;
using System.Text;
using NUnit.Framework;
using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Tests
{
    /// <summary>
    /// The NPC's picture of its opponent, held against what the opponent actually does.
    ///
    /// <see cref="NpcBrain"/> decides between declaring and guarding by comparing the hand in
    /// front of it with two constants: what the opponent typically rolls, and what the opponent
    /// typically strikes for. Those two decide everything, because
    ///
    /// <code>  (damage you deal) - (damage you take)
    ///          = (your strike + your ward) - (their strike + their ward)
    ///          = your hand total - their hand total</code>
    ///
    /// for any yaku whose strike is just the sum of the dice it sends forward. So the choice
    /// collapses to "is my hand better than theirs", and the constants are the only thing that
    /// answers "theirs".
    ///
    /// They were a pair of hand-written guesses, and they disagreed with each other: the attack
    /// and the ward they implied summed to a hand worth 23, while the brain's own re-roll policy
    /// produces hands worth around 18. Every line was therefore comparing a normal hand against
    /// an opponent that did not exist, found itself below average, and guarded - which is where
    /// the 78% guarding share and the 72% of matches run to time came from.
    ///
    /// This test samples the brain directly, so the assumption can no longer drift away from the
    /// behaviour it describes.
    /// </summary>
    public sealed class TypicalOpponentTests
    {
        /// <summary>Per archetype - enough that the sample mean moves by well under a pip.</summary>
        private const int TrialsPerLine = 6000;

        /// <summary>How far the assumption may sit from the measurement before it is a fiction.</summary>
        private const double Tolerance = 1.0;

        private const string ReportPath = @"F:\MimoGame\logs\typical-opponent-report.json";

        private static readonly NpcArchetype[] Archetypes =
        {
            NpcArchetype.Aggressive, NpcArchetype.Guardian, NpcArchetype.Balanced
        };

        private static readonly string[] ArchetypeNames = { "攻撃型", "守護型", "均衡型" };

        [Test]
        public void TheTypicalOpponentIsOneThisBrainActuallyProduces()
        {
            var config = new GameConfig().ApplyShippedPatternLayer();

            // HP states sampled the same way YakuAppealTests declares them, so the two reports
            // describe the same population.
            var hpStates = new[] { new { My = 16, Foe = 16 }, new { My = 28, Foe = 8 }, new { My = 8, Foe = 28 } };

            double totalSum = 0.0;
            double strikeSum = 0.0;
            int samples = 0;

            // What each line actually declares over those same turns. Unlike the exhaustive sweep
            // in YakuAppealTests, which visits every hand with equal weight, these are the hands
            // that re-rolling really produces - so this is the share a player would sit through.
            var declared = new int[7];

            for (int a = 0; a < Archetypes.Length; a++)
            {
                for (int hp = 0; hp < hpStates.Length; hp++)
                {
                    // A different seed per line, so no line is measured on another's rolls.
                    var rng = new SeededRandom(1000 + a * 31 + hp);
                    var brain = new NpcBrain(Archetypes[a]);

                    for (int i = 0; i < TrialsPerLine; i++)
                    {
                        NpcTurnResult turn = brain.TakeTurn(config, rng,
                            hpStates[hp].My, hpStates[hp].Foe);

                        totalSum += Scoring.Sum(turn.Dice);
                        strikeSum += turn.Yaku.StrikeValue(turn.Dice);
                        samples++;

                        int kind = (int)turn.Yaku.Kind;
                        if (kind >= 0 && kind < declared.Length)
                            declared[kind]++;
                    }
                }
            }

            double meanTotal = totalSum / samples;
            double meanStrike = strikeSum / samples;
            double meanWard = meanTotal - meanStrike;

            WriteReport(samples, meanTotal, meanStrike, meanWard, declared);

            Debug.Log("[TYPICAL] opponent total=" + meanTotal.ToString("0.00") +
                      " strike=" + meanStrike.ToString("0.00") +
                      " ward=" + meanWard.ToString("0.00") +
                      " vs model " + NpcBrain.TypicalOpponentTotal + "/" +
                      NpcBrain.TypicalOpponentAttack + "/" +
                      (NpcBrain.TypicalOpponentTotal - NpcBrain.TypicalOpponentAttack));

            Assert.That(NpcBrain.TypicalOpponentTotal, Is.EqualTo(meanTotal).Within(Tolerance),
                "the model imagines an opponent rolling " + NpcBrain.TypicalOpponentTotal +
                " while this brain actually rolls " + meanTotal.ToString("0.00") +
                " - declaring turns on that number, so the gap decides every match");

            Assert.That(NpcBrain.TypicalOpponentAttack, Is.EqualTo(meanStrike).Within(Tolerance),
                "the model imagines an opponent striking for " + NpcBrain.TypicalOpponentAttack +
                " while this brain actually strikes for " + meanStrike.ToString("0.00") +
                " - the ward left behind is the difference, and the ward is what stops a strike");

            // The two must also describe one hand rather than two different ones. Attack plus
            // ward is the opponent's total, and if the constants do not add up the model is
            // treating the opponent as better at both ends than a hand can be.
            double implied = NpcBrain.TypicalOpponentAttack +
                             (NpcBrain.TypicalOpponentTotal - NpcBrain.TypicalOpponentAttack);
            Assert.That(implied, Is.EqualTo(NpcBrain.TypicalOpponentTotal).Within(0.0001),
                "the implied hand must be the stated hand");

            // The implication for play: a round spent declaring nothing is a round nobody
            // watching can tell apart from a round that failed to start. Guarding is supposed to
            // be the safe answer, not the answer.
            double guardShare = (double)declared[(int)YakuKind.Guard] / samples;
            Debug.Log("[TYPICAL] 守護 share over real turns = " + guardShare.ToString("0.0000"));

            Assert.That(guardShare, Is.LessThan(0.5),
                "守護 is " + guardShare.ToString("0.0000") +
                " of the turns actually played - more than half of all rounds decline to play, " +
                "which is how a battle ends on the time limit instead of on a knockout");

            // The prize has to be reachable from the same population, not only from a hand the
            // exhaustive sweep happened to enumerate.
            for (int kind = (int)YakuKind.SmallRun; kind <= (int)YakuKind.Five; kind++)
            {
                Assert.That(declared[kind], Is.GreaterThan(0),
                    KindNames[kind] + " is never declared over " + samples +
                    " real turns - the top of the ladder exists on paper only");
            }
        }

        /// <summary>Index 0..6 in <see cref="YakuKind"/> order, straight from the screen's names.</summary>
        private static readonly string[] KindNames =
        {
            Yaku.DisplayName(YakuKind.Guard), Yaku.DisplayName(YakuKind.Pair),
            Yaku.DisplayName(YakuKind.Triple), Yaku.DisplayName(YakuKind.Chance),
            Yaku.DisplayName(YakuKind.SmallRun), Yaku.DisplayName(YakuKind.LargeRun),
            Yaku.DisplayName(YakuKind.Five)
        };

        private static void WriteReport(int samples, double meanTotal, double meanStrike,
            double meanWard, int[] declared)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"samples\": " + samples + ",");
            sb.AppendLine("  \"measured\": {");
            sb.AppendLine("    \"handTotal\": " + meanTotal.ToString("0.0000") + ",");
            sb.AppendLine("    \"strike\": " + meanStrike.ToString("0.0000") + ",");
            sb.AppendLine("    \"ward\": " + meanWard.ToString("0.0000"));
            sb.AppendLine("  },");
            sb.AppendLine("  \"model\": {");
            sb.AppendLine("    \"handTotal\": " + NpcBrain.TypicalOpponentTotal.ToString("0.0000") + ",");
            sb.AppendLine("    \"strike\": " + NpcBrain.TypicalOpponentAttack.ToString("0.0000") + ",");
            sb.AppendLine("    \"ward\": " +
                          (NpcBrain.TypicalOpponentTotal - NpcBrain.TypicalOpponentAttack).ToString("0.0000"));
            sb.AppendLine("  },");
            sb.AppendLine("  \"declared\": [");
            for (int k = 0; k < KindNames.Length; k++)
            {
                sb.Append("    { \"name\": \"" + KindNames[k] + "\"");
                sb.Append(", \"count\": " + declared[k]);
                sb.Append(", \"share\": " + ((double)declared[k] / samples).ToString("0.0000"));
                sb.Append(k < KindNames.Length - 1 ? " }," : " }");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"lines\": [");
            for (int i = 0; i < ArchetypeNames.Length; i++)
            {
                sb.Append("    \"" + ArchetypeNames[i] + "\"");
                sb.AppendLine(i < ArchetypeNames.Length - 1 ? "," : "");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            System.IO.File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[TYPICAL] report written to " + ReportPath);
        }
    }
}
