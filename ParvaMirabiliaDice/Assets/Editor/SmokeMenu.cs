using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParvaMirabilia.Editor
{
    /// <summary>
    /// Editor entry point for the opt-in visual smoke run. The smoke suite was player-only:
    /// its captures verify builds, while editor Play Mode had no visual verification at all.
    /// Publishing an output directory through the ArcaneSmokeOutput variable lets the same
    /// production smoke drive itself inside an editor play session and drop the same PNG
    /// evidence (attack phases, yaku effects, HP and layout) into D:\MimoGame\logs.
    /// </summary>
    public static class SmokeMenu
    {
        private const string Variable = "ArcaneSmokeOutput";

        [MenuItem("Parva Mirabilia/Smoke/Run ScoreDuel smoke in editor play")]
        public static void PlayScoreDuelSmoke()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop play mode first, then launch the smoke again.");
                return;
            }
            string workspace = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string output = Path.Combine(workspace, "logs",
                "editor-smoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(output);
            Environment.SetEnvironmentVariable(Variable, output);
            Debug.Log("Arcane editor smoke -> " + output);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Parva Mirabilia/Smoke/Clear smoke request")]
        public static void ClearSmokeRequest()
        {
            Environment.SetEnvironmentVariable(Variable, null);
            Debug.Log("Arcane smoke request cleared: ordinary play stays smoke-free.");
        }
    }
}
