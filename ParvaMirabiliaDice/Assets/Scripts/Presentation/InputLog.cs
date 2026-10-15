using System;
using System.Globalization;
using System.IO;
using System.Text;
using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Records every player input together with the state it happened in, so the click cost of a
    /// round can be measured on the real build instead of being estimated from the code.
    ///
    /// This is an observation tool only: it never changes game state, never blocks, and a failed
    /// write is swallowed so a full disk cannot break a duel. The log lives next to the project
    /// under <c>logs/</c> and one file is opened per play session.
    /// </summary>
    public static class InputLog
    {
        private static StreamWriter _writer;
        private static string _path = "";
        private static bool _enabled = Application.isEditor || Debug.isDebugBuild;

        /// <summary>File the current session is writing to, or "" when nothing has been logged yet.</summary>
        public static string Path
        {
            get { return _path; }
        }

        public static bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>Appends one line. Safe to call from anywhere, including from finally blocks.</summary>
        public static void Record(string action, MatchEngineInfo engine, string detail)
        {
            if (!_enabled)
                return;

            try
            {
                EnsureOpen();

                var builder = new StringBuilder(160);
                builder.Append("{\"t\":")
                    .Append(Time.realtimeSinceStartup.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(",\"frame\":").Append(Time.frameCount)
                    .Append(",\"action\":\"").Append(Escape(action)).Append('"');

                if (engine != null)
                {
                    builder.Append(",\"phase\":").Append(engine.Phase == null
                        ? "null"
                        : '"' + Escape(engine.Phase.Value.ToString()) + '"');
                    builder.Append(",\"round\":").Append(engine.Round);
                    builder.Append(",\"split\":").Append(engine.SplitMode ? "true" : "false");
                    builder.Append(",\"rerolls\":").Append(engine.RerollsLeft);
                    builder.Append(",\"php\":").Append(engine.PlayerHp);
                    builder.Append(",\"nhp\":").Append(engine.NpcHp);
                }

                if (!string.IsNullOrEmpty(detail))
                    builder.Append(",\"detail\":\"").Append(Escape(detail)).Append('"');

                builder.Append('}');

                _writer.WriteLine(builder.ToString());
                _writer.Flush();
            }
            catch
            {
                // Observation must never cost the player their match.
                _enabled = false;
            }
        }

        public static void Close()
        {
            if (_writer == null)
                return;

            try
            {
                _writer.Flush();
                _writer.Dispose();
            }
            catch
            {
                // ignored
            }

            _writer = null;
        }

        private static void EnsureOpen()
        {
            if (_writer != null)
                return;

            // Application.dataPath is <project>/Assets, so "../../logs" is the sibling logs folder.
            string directory = Application.isEditor
                ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "logs"))
                : System.IO.Path.Combine(Application.persistentDataPath, "logs");
            Directory.CreateDirectory(directory);

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            _path = System.IO.Path.Combine(directory, "oplog-" + stamp + ".jsonl");

            // ReadWrite sharing: the test harness totals the log while this writer is still open,
            // and a plain StreamWriter holds the file with FileShare.Read, which makes the next
            // Flush throw a sharing violation.
            var stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(stream, new UTF8Encoding(false));
            _writer.WriteLine("# ARCANE DUEL input log. One JSON object per player input.");
            _writer.Flush();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    /// <summary>Snapshot of the engine state at the moment of an input, kept tiny on purpose.</summary>
    public sealed class MatchEngineInfo
    {
        public MatchPhase? Phase;
        public int Round;
        public bool SplitMode;
        public int RerollsLeft;
        public int PlayerHp;
        public int NpcHp;
    }
}
