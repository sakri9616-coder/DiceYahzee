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
    /// Which yaku a hand is actually offered, and which of them a line takes.
    ///
    /// The ladder was built on a claim nobody had measured: that the printed yaku would be rare
    /// enough to feel like an event while the cheap ones kept every decision live. Rarity is
    /// cheap to work out by hand - 大階段 is 240 of 7776 hands - but <em>appeal</em> is not,
    /// because it depends on how the offers compare once the strike and the ward are weighed
    /// against each other. A yaku that exists and is never picked is decoration.
    ///
    /// This is the exhaustive count, not a sample: every hand, every archetype, three HP states,
    /// so the figures do not move between runs. It also guards the single fork still open on
    /// this design - per-yaku once-per-match slots were deferred on the condition that no one
    /// yaku comes to own more than half of all declarations.
    /// </summary>
    public sealed class YakuAppealTests
    {
        /// <summary>Every ordered five-dice hand, so nothing is estimated.</summary>
        private const int HandCount = 6 * 6 * 6 * 6 * 6;

        /// <summary>One declaration per hand, per archetype, per HP state.</summary>
        private const int DecisionCount = HandCount * 9;

        /// <summary>
        /// The deferred fork's trigger. Past this a hand has no real second answer, which is the
        /// thing the choice was for.
        /// </summary>
        private const double E1TriggerShare = 0.5;

        /// <summary>
        /// Index 0..6 in <see cref="YakuKind"/> order, taken from the screen's own names so the
        /// report can never drift from what a player actually reads on a button.
        /// </summary>
        private static readonly string[] KindNames =
        {
            Yaku.DisplayName(YakuKind.Guard), Yaku.DisplayName(YakuKind.Pair),
            Yaku.DisplayName(YakuKind.Triple), Yaku.DisplayName(YakuKind.Chance),
            Yaku.DisplayName(YakuKind.SmallRun), Yaku.DisplayName(YakuKind.LargeRun),
            Yaku.DisplayName(YakuKind.Five)
        };

        private static readonly NpcArchetype[] Archetypes =
        {
            NpcArchetype.Aggressive, NpcArchetype.Guardian, NpcArchetype.Balanced
        };

        private static readonly string[] ArchetypeNames = { "攻撃型", "守護型", "均衡型" };

        /// <summary>Even, well ahead, well behind - the three postures ResolveWeights reacts to.</summary>
        private static readonly int[,] HpStates =
        {
            { 16, 16 }, { 28, 8 }, { 8, 28 }
        };

        private static readonly string[] HpStateNames = { "均勢", "優勢", "劣勢" };

        private const string ReportPath = @"D:\MimoGame\logs\yaku-appeal-report.json";

        [Test]
        public void EveryYakuIsOfferedAndNoSingleYakuOwnsTheMatch()
        {
            var config = new GameConfig().ApplyShippedPatternLayer();

            // How often a yaku is on the table at all, counted over hands rather than over
            // decisions: a fact about the dice, which no policy can move.
            var offeredHands = new int[KindNames.Length];

            // How often a line takes it, overall and then split by posture, because "never taken"
            // and "not taken by a cautious line at even health" are different findings and only
            // one of them is a dead rung.
            var chosen = new int[KindNames.Length];
            var chosenByLine = new int[Archetypes.Length, HpStates.GetLength(0), KindNames.Length];
            var handsWithAChoice = 0;

            for (int a = 1; a <= 6; a++)
            for (int b = 1; b <= 6; b++)
            for (int c = 1; c <= 6; c++)
            for (int d = 1; d <= 6; d++)
            for (int e = 1; e <= 6; e++)
            {
                var dice = new[] { a, b, c, d, e };
                YakuOffer[] offers = Yaku.Evaluate(dice, config);

                if (offers.Length > 1)
                    handsWithAChoice++;

                var seen = new bool[KindNames.Length];
                for (int i = 0; i < offers.Length; i++)
                {
                    int kind = (int)offers[i].Kind;
                    if (kind >= 0 && kind < KindNames.Length)
                        seen[kind] = true;
                }

                for (int k = 0; k < seen.Length; k++)
                {
                    if (seen[k])
                        offeredHands[k]++;
                }

                for (int archetype = 0; archetype < Archetypes.Length; archetype++)
                {
                    var brain = new NpcBrain(Archetypes[archetype]);
                    for (int hp = 0; hp < HpStates.GetLength(0); hp++)
                    {
                        YakuOffer pick = brain.ChooseYaku(dice, config,
                            HpStates[hp, 0], HpStates[hp, 1]);
                        int kind = (int)pick.Kind;
                        if (kind < 0 || kind >= KindNames.Length)
                            continue;

                        chosen[kind]++;
                        chosenByLine[archetype, hp, kind]++;
                    }
                }
            }

            int declared = Sum(chosen);
            WriteReport(config, offeredHands, handsWithAChoice, chosen, chosenByLine, declared);

            // --- the sweep really was exhaustive. 守護 is unconditional, so it is on every hand.
            Assert.That(offeredHands[(int)YakuKind.Guard], Is.EqualTo(HandCount),
                "守護 is unconditional - if it is not on " + HandCount +
                " hands the sweep skipped some");
            Assert.That(declared, Is.EqualTo(DecisionCount),
                "every archetype and HP posture must have declared something");

            // --- reachability: a yaku nobody can hold is not a yaku.
            for (int k = 0; k < KindNames.Length; k++)
            {
                Assert.That(offeredHands[k], Is.GreaterThan(0),
                    KindNames[k] + " never appears on any hand, so the ladder has a dead rung");
            }

            // --- the prize has to pay at least sometimes. Taken as "some line wants it", not
            //     "every line wants it": a cautious line refusing an all-in is the model working.
            for (int k = (int)YakuKind.SmallRun; k <= (int)YakuKind.Five; k++)
            {
                Assert.That(chosen[k], Is.GreaterThan(0),
                    KindNames[k] + " is offered but never declared by any line in any posture - " +
                    "the prize never pays out");
            }

            // --- the fork: if one yaku owns more than half of all declarations, the other offers
            //     are being presented as a choice without behaving like one. 守護 is deliberately
            //     excluded: it is the refusal to answer rather than one answer among many, and
            //     the ceiling that applies to it is measured over real re-rolled turns in
            //     TypicalOpponentTests - this sweep visits every hand with equal weight, which no
            //     seat ever plays, so its guarding share is a property of the enumeration and not
            //     of the game.
            int topIndex = 1;                                    // 0 is 守護, which is excluded
            for (int k = 2; k < chosen.Length; k++)
            {
                if (chosen[k] > chosen[topIndex])
                    topIndex = k;
            }

            double topShare = (double)chosen[topIndex] / declared;
            Debug.Log("[YAKU] top declared yaku = " + KindNames[topIndex] + " " +
                      topShare.ToString("0.0000") + " (" + chosen[topIndex] + "/" + declared + ")");
            Debug.Log("[YAKU] 守護 share = " +
                      ((double)chosen[(int)YakuKind.Guard] / declared).ToString("0.0000"));

            Assert.That(topShare, Is.LessThan(E1TriggerShare),
                KindNames[topIndex] + " accounts for " + topShare.ToString("0.0000") +
                " of every declaration, past the " + E1TriggerShare.ToString("0.00") +
                " trigger - the offers have collapsed into one answer and per-yaku " +
                "once-per-match slots are owed");
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++)
                total += values[i];
            return total;
        }

        private static void WriteReport(GameConfig config, int[] offeredHands,
            int handsWithAChoice, int[] chosen, int[,,] chosenByLine, int declared)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"rules\": \"yaku\",");
            sb.AppendLine("  \"hands\": " + HandCount + ",");
            sb.AppendLine("  \"handsWithAChoice\": " + handsWithAChoice + ",");
            sb.AppendLine("  \"decisions\": " + declared + ",");
            sb.AppendLine("  \"e1TriggerShare\": " + E1TriggerShare.ToString("0.0000") + ",");
            sb.AppendLine("  \"config\": {");
            sb.AppendLine("    \"smallRunAttack\": " + config.YakuSmallRunAttack + ",");
            sb.AppendLine("    \"largeRunAttack\": " + config.YakuLargeRunAttack + ",");
            sb.AppendLine("    \"fiveAttack\": " + config.YakuFiveAttack);
            sb.AppendLine("  },");
            sb.AppendLine("  \"kinds\": [");

            int topIndex = 0;
            for (int k = 1; k < chosen.Length; k++)
            {
                if (chosen[k] > chosen[topIndex])
                    topIndex = k;
            }

            for (int k = 0; k < KindNames.Length; k++)
            {
                sb.Append("    { \"name\": \"" + KindNames[k] + "\"");
                sb.Append(", \"offeredHands\": " + offeredHands[k]);
                sb.Append(", \"offerShare\": " + ((double)offeredHands[k] / HandCount).ToString("0.0000"));
                sb.Append(", \"declared\": " + chosen[k]);
                sb.Append(", \"selectionShare\": " + ((double)chosen[k] / declared).ToString("0.0000"));
                sb.Append(", \"byLine\": [");
                bool firstCell = true;
                for (int line = 0; line < Archetypes.Length; line++)
                {
                    for (int hp = 0; hp < HpStates.GetLength(0); hp++)
                    {
                        int count = chosenByLine[line, hp, k];
                        if (!firstCell)
                            sb.Append(", ");
                        firstCell = false;

                        sb.Append("{\"line\": \"" + ArchetypeNames[line] + "/" + HpStateNames[hp] + "\"");
                        sb.Append(", \"declared\": " + count);
                        sb.Append(", \"share\": " + ((double)count / HandCount).ToString("0.0000"));
                        sb.Append("}");
                    }
                }
                sb.Append("]");
                sb.Append(k == topIndex ? ", \"top\": true" : "");
                sb.Append(k < KindNames.Length - 1 ? " }," : " }");
                sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[YAKU] report written to " + ReportPath);
        }
    }
}
