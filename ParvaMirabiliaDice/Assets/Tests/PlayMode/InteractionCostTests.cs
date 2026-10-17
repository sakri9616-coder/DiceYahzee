using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ParvaMirabilia.Tests.PlayMode
{
    /// <summary>
    /// Measures the real interaction cost of a duel by driving the shipped <see cref="GameFlow"/>
    /// through the same handlers a mouse click would reach, while <see cref="InputLog"/> records
    /// every input. This is what turns "a round takes N clicks" from an estimate read off the code
    /// into a number the build produced.
    ///
    /// Each policy expresses a player's *intent* (reroll anything weak, settle on a hand, change
    /// your mind once). Every UI version has to express that intent through its own controls, so
    /// the counts stay comparable when the interface changes.
    ///
    /// Comparability across UI generations does not come from this file re-running the old rules.
    /// The hand-built split no longer exists in the interface, so the v1/v2 rows cannot be
    /// produced by this build at all; they are carried as measured figures in
    /// logs/oplog-summary-splitrules.json, which this file deliberately never writes over
    /// semantics: the rows here are the same intents played on the declaration rules.
    ///
    /// Every loop is bounded. The first version of this test hung the editor for a quarter of an
    /// hour because a policy that made no progress re-rolled forever; a measurement that cannot
    /// fail loudly is not a measurement.
    /// </summary>
    public sealed class InteractionCostTests
    {
        /// <summary>
        /// Matches per policy. One match is a story: it can land on a short fight or a long one
        /// and the input count swings with it. Averaging several turns "this interface takes N
        /// clicks a round" from an anecdote into something a comparison can rest on.
        /// </summary>
        private const int RunsPerPolicy = 5;

        private GameFlow _flow;
        private MatchEngine _engine;
        private readonly List<PolicyResult> _results = new List<PolicyResult>();

        private sealed class PolicyResult
        {
            public string Policy;

            /// <summary>Matches folded into this row. One match is a story, not a measurement.</summary>
            public int Matches;
            public int Inputs;
            public int InputMin = int.MaxValue;
            public int InputMax;
            public int DieClicks;
            public int ButtonClicks;
            public int YakuClicks;
            public int MindChanges;
            public int Rounds;
            public int RejectedCommits;
            public readonly List<MatchResult> Results = new List<MatchResult>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameFlow.ConfigOverride = null;
            InputLog.Close();
            var flow = UnityEngine.Object.FindFirstObjectByType<GameFlow>();
            if (flow != null)
                UnityEngine.Object.Destroy(flow.gameObject);
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator InteractionCost_OfAWholeMatch()
        {
            // Muted: a headless run has no audio listener, and the per-clip warnings slow the
            // run down without telling us anything about interaction cost.
            PlayerPrefs.SetInt("pv.audioStep", 3);
            PlayerPrefs.Save();

            InputLog.Enabled = true;
            _flow = UnityEngine.Object.FindFirstObjectByType<GameFlow>();
            if (_flow == null)
            {
                var host = new GameObject("GameFlowUnderTest");
                _flow = host.AddComponent<GameFlow>();
            }

            yield return null;
            yield return null;

            // keepThreshold is the "reroll anything below this face" aim: 0 means never reroll,
            // 4 is the same threshold the NPC uses.
            for (int run = 0; run < RunsPerPolicy; run++)
            {
                PlayOneMatch("v3 そのまま(振り直しなし)", keepThreshold: 0, mindChanges: 0);
                PlayOneMatch("v3 標準(閾値4)", keepThreshold: 4, mindChanges: 0);
                PlayOneMatch("v3 標準(閾値4・迷い1回)", keepThreshold: 4, mindChanges: 1);
                Debug.Log("[InteractionCost] run " + (run + 1) + "/" + RunsPerPolicy + " done");
            }

            WriteSummary();
            Assert.That(_results.Count, Is.EqualTo(3));
            foreach (PolicyResult row in _results)
            {
                Assert.That(row.Matches, Is.EqualTo(RunsPerPolicy),
                    row.Policy + " must be sampled " + RunsPerPolicy + " times");
            }
            yield return null;
        }

        // ---------------------------------------------------------------- driving
        private void PlayOneMatch(string policy, int keepThreshold, int mindChanges)
        {
            _engine = null;
            int before = CountLogLines();
            int dieClicks = 0;
            int buttonClicks = 0;
            int yakuClicks = 0;
            int mind = 0;
            int rounds = 0;
            int rejected = 0;

            // Marked with the run number: the JSONL is read back when a per-policy figure looks
            // odd, and "which of the five matches was this" is the first question asked.
            PolicyResult row = RowFor(policy);
            InputLog.Record("policy_start", null, policy + " run=" + (row.Matches + 1));

            // A fresh match from wherever the last one stopped. The title button only exists on
            // the title screen, so reach the same handler the rematch button reaches; it is still
            // exactly one input, and it is the same code path a click takes.
            //
            // ConfigOverride stays null: this build always plays the declared-yaku rules, and
            // asking it to play something else would measure a rule set the interface cannot
            // express.
            GameFlow.ConfigOverride = null;
            Method("StartMatch").Invoke(_flow, null);
            buttonClicks++;
            _engine = Engine();

            int guard = 0;
            while (_engine.Outcome == null && rounds < _engine.Config.MaxRounds)
            {
                if (++guard > 200)
                    throw new InvalidOperationException("round loop made no progress");

                rounds++;

                // --- aim: reroll anything below the threshold. One click per die, and the mark
                // is exactly what the screen shows, so this count is the count a mouse would
                // produce rather than a simulation of one.
                int inner = 0;
                while (_engine.Phase == MatchPhase.AwaitingReroll && _engine.PlayerRerollsLeft > 0)
                {
                    if (++inner > 64)
                        throw new InvalidOperationException("reroll loop made no progress");

                    int marked = 0;
                    for (int i = 0; i < _engine.PlayerDice.Length; i++)
                    {
                        if (_engine.PlayerDice[i] < keepThreshold && !DieMarked(i))
                        {
                            Method("OnPlayerDieClicked").Invoke(_flow, new object[] { i });
                            dieClicks++;
                            marked++;
                        }
                    }

                    if (marked == 0)
                        break;

                    PressButton("_rerollButton");
                    buttonClicks++;
                }

                // --- the reported stress: changing your mind about which hand to declare, after
                // the dice have already been looked at. Under the split rules that was a mode
                // round-trip out of and back into the assignment; here it is one re-click on a
                // different yaku button, and the number below is that cost.
                if (mind < mindChanges)
                {
                    int index = OtherYakuIndex();
                    if (index >= 0)
                    {
                        PressYaku(index);
                        yakuClicks++;
                        buttonClicks++;
                        mind++;
                    }
                }

                PressButton("_commitButton");
                buttonClicks++;
                if (_engine.Outcome == null &&
                    (_engine.Phase == MatchPhase.AwaitingReroll || _engine.Phase == MatchPhase.AwaitingSplit))
                    rejected++;

                PressButton("_advanceButton");
                buttonClicks++;
            }

            MatchResult outcome = _engine.Outcome != null ? _engine.Outcome.Result : MatchResult.Draw;
            int inputs = CountLogLines() - before;
            row.Matches++;
            row.Inputs += inputs;
            row.InputMin = Math.Min(row.InputMin, inputs);
            row.InputMax = Math.Max(row.InputMax, inputs);
            row.DieClicks += dieClicks;
            row.ButtonClicks += buttonClicks;
            row.YakuClicks += yakuClicks;
            row.MindChanges += mind;
            row.Rounds += rounds;
            row.RejectedCommits += rejected;
            row.Results.Add(outcome);
            InputLog.Record("policy_end", null, policy + " run=" + row.Matches);
        }

        /// <summary>One row per policy, so several matches fold into a single comparable figure.</summary>
        private PolicyResult RowFor(string policy)
        {
            for (int i = 0; i < _results.Count; i++)
            {
                if (_results[i].Policy == policy)
                    return _results[i];
            }

            var row = new PolicyResult { Policy = policy };
            _results.Add(row);
            return row;
        }

        // ------------------------------------------------------------- reflection
        /// <summary>A different currently clickable role, or -1 when there is no such choice.</summary>
        private int OtherYakuIndex()
        {
            YakuKind current = SelectedYaku();
            YakuOffer[] offers = Yaku.Evaluate(_engine.PlayerDice, _engine.Config);
            var buttons = Field("_yakuButtons")?.GetValue(_flow) as List<Button>;
            if (buttons == null) return -1;

            int other = -1;
            int count = _engine.Config.TacticalEnabled ? buttons.Count : Math.Min(offers.Length, buttons.Count);
            for (int i = 0; i < count; i++)
            {
                var button = buttons[i];
                YakuKind kind = _engine.Config.TacticalEnabled ? (YakuKind)i : offers[i].Kind;
                if (kind != current && button.gameObject.activeInHierarchy && button.interactable)
                    other = i;
            }

            return other;
        }

        private YakuKind SelectedYaku()
        {
            var field = Field("_yakuOffer");
            if (field == null)
                return YakuKind.None;

            return ((YakuOffer)field.GetValue(_flow)).Kind;
        }

        private bool DieMarked(int index)
        {
            var field = Field("_rerollMarked");
            var marks = field != null ? field.GetValue(_flow) as bool[] : null;
            return marks != null && index < marks.Length && marks[index];
        }

        private MatchEngine Engine()
        {
            var field = Field("_engine");
            return field != null ? field.GetValue(_flow) as MatchEngine : null;
        }

        private void PressButton(string fieldName)
        {
            var field = Field(fieldName);
            var button = field != null ? field.GetValue(_flow) as Button : null;
            if (button == null)
                throw new InvalidOperationException("Button not found: " + fieldName);

            Press(button, fieldName);
        }

        /// <summary>
        /// Presses a yaku offer by index. The buttons exist per offer and are hidden when there is
        /// no such offer, so a policy that asks for a button the hand was never entitled to fails
        /// loudly here instead of quietly measuring nothing.
        /// </summary>
        private void PressYaku(int index)
        {
            var field = Field("_yakuButtons");
            var buttons = field != null ? field.GetValue(_flow) as List<Button> : null;
            if (buttons == null || index < 0 || index >= buttons.Count)
                throw new InvalidOperationException("Yaku button out of range: " + index);

            Press(buttons[index], "yaku[" + index + "]");
        }

        private static void Press(Button button, string what)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                throw new InvalidOperationException("Button was not clickable: " + what);

            // Exactly what a mouse click on the button does.
            button.onClick.Invoke();
        }

        private static FieldInfo Field(string name)
        {
            return typeof(GameFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static MethodInfo Method(string name)
        {
            return typeof(GameFlow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        // ------------------------------------------------------------------- output
        private static int CountLogLines()
        {
            string path = InputLog.Path;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return 0;

            int count = 0;
            // Share ReadWrite so the still-open InputLog writer keeps flushing while we total it.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line != null && line.TrimStart().StartsWith("{", StringComparison.Ordinal))
                        count++;
                }
            }

            return count;
        }

        private void WriteSummary()
        {
            string directory = Path.GetDirectoryName(InputLog.Path) ?? ".";
            string file = Path.Combine(directory, "oplog-summary.json");

            var builder = new StringBuilder();
            builder.Append("{\n  \"log\": \"").Append(Path.GetFileName(InputLog.Path)).Append("\",\n");
            builder.Append("  \"rules\": \"yaku\",\n");
            builder.Append("  \"runsPerPolicy\": ").Append(RunsPerPolicy).Append(",\n");
            builder.Append("  \"policies\": [\n");
            for (int i = 0; i < _results.Count; i++)
            {
                PolicyResult r = _results[i];
                int perMatch = r.Matches > 0 ? r.Inputs / r.Matches : 0;
                double perRound = r.Rounds > 0 ? Math.Round(r.Inputs / (double)r.Rounds, 2) : 0;
                builder.Append("    {\"policy\":\"").Append(r.Policy)
                    .Append("\",\"matches\":").Append(r.Matches)
                    .Append(",\"inputsAvg\":").Append(perMatch)
                    .Append(",\"inputsMin\":").Append(r.Matches > 0 ? r.InputMin : 0)
                    .Append(",\"inputsMax\":").Append(r.InputMax)
                    .Append(",\"roundsAvg\":").Append(r.Matches > 0 ? Math.Round(r.Rounds / (double)r.Matches, 2) : 0)
                    .Append(",\"perRound\":").Append(perRound)
                    .Append(",\"dieClicksAvg\":").Append(r.Matches > 0 ? Math.Round(r.DieClicks / (double)r.Matches, 2) : 0)
                    .Append(",\"buttonClicksAvg\":").Append(r.Matches > 0 ? Math.Round(r.ButtonClicks / (double)r.Matches, 2) : 0)
                    .Append(",\"yakuClicksAvg\":").Append(r.Matches > 0 ? Math.Round(r.YakuClicks / (double)r.Matches, 2) : 0)
                    .Append(",\"mindChangesAvg\":").Append(r.Matches > 0 ? Math.Round(r.MindChanges / (double)r.Matches, 2) : 0)
                    .Append(",\"rejectedCommits\":").Append(r.RejectedCommits)
                    .Append(",\"results\":[");
                for (int j = 0; j < r.Results.Count; j++)
                {
                    if (j > 0) builder.Append(',');
                    builder.Append('"').Append(r.Results[j]).Append('"');
                }
                builder.Append("]}");
                if (i < _results.Count - 1)
                    builder.Append(',');
                builder.Append('\n');
            }

            builder.Append("  ]\n}\n");
            File.WriteAllText(file, builder.ToString(), new UTF8Encoding(false));
            Debug.Log("[InteractionCost] " + file);
        }
    }
}
