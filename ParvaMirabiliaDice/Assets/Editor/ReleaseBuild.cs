using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParvaMirabilia.Editor
{
    public static class ReleaseBuild
    {
        [MenuItem("Parva Mirabilia/Build Web release")]
        public static void Web()
        {
            string workspace = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string output = Path.Combine(workspace, "Builds", "Web-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-arcaneBuildPath") output = Path.GetFullPath(args[i + 1]);

            var compression = PlayerSettings.WebGL.compressionFormat;
            bool fallback = PlayerSettings.WebGL.decompressionFallback;
            string template = PlayerSettings.WebGL.template;
            try
            {
                // Uncompressed files work on ordinary static hosting without custom
                // Content-Encoding headers or a JavaScript decompression fallback.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.template = "APPLICATION:Default";
                Directory.CreateDirectory(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/Scenes/Main.unity" },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.StrictMode
                });
                BuildSummary summary = report.summary;
                string evidence = "{\"result\":\"" + summary.result + "\",\"errors\":" + summary.totalErrors +
                    ",\"warnings\":" + summary.totalWarnings + ",\"bytes\":" + summary.totalSize +
                    ",\"unity\":\"" + Application.unityVersion + "\",\"version\":\"" + Application.version +
                    "\",\"development\":false,\"compression\":\"Disabled\",\"target\":\"WebGL\"}";
                File.WriteAllText(Path.Combine(output, "build-report.json"), evidence);
                Debug.Log("[ARCANE BUILD] " + evidence);
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new Exception("Web release build failed: " + summary.result);
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                PlayerSettings.WebGL.template = template;
                AssetDatabase.SaveAssets();
            }
        }

        [MenuItem("Parva Mirabilia/Build Windows release")]
        public static void Windows()
        {
            string workspace = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string output = Path.Combine(workspace, "Builds", "Windows", "DiceYahzee.exe");
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-arcaneBuildPath") output = Path.GetFullPath(args[i + 1]);
            // The UI needs no DX12 features. DX11 also handles window resizing reliably in
            // the actual-player diagnostics, where automatic DX12 stalled on this machine.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
            });
            BuildSummary summary = report.summary;
            string evidence = "{\"result\":\"" + summary.result + "\",\"errors\":" + summary.totalErrors +
                ",\"warnings\":" + summary.totalWarnings + ",\"bytes\":" + summary.totalSize +
                ",\"unity\":\"" + Application.unityVersion + "\",\"version\":\"" + Application.version +
                "\",\"development\":false,\"graphics\":\"" + PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64)[0] +
                "\",\"target\":\"StandaloneWindows64\"}";
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-report.json"), evidence);
            Debug.Log("[ARCANE BUILD] " + evidence);
            if (summary.result != BuildResult.Succeeded) throw new Exception("Windows release build failed: " + summary.result);
        }
    }
}
