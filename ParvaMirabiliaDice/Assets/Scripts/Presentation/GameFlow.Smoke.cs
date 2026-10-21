using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        // Opt-in diagnostics for the real executable. Normal game startup never enters this path.
        private string _smokeOutput;
        private readonly List<string> _smokeChecks = new List<string>();
        private readonly List<string> _smokeErrors = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void EnableBackgroundSmokeIfRequested()
        {
            // Apply before the scene loads: a hidden window can pause before MonoBehaviour.Start.
            // Ordinary player startup continues to use the project's normal pause behavior.
            string[] args = Environment.GetCommandLineArgs();
            if (!Application.isEditor && (System.Array.IndexOf(args, "-arcaneSmokeOutput") >= 0 ||
                System.Array.IndexOf(args, "-diceNetHost") >= 0 || System.Array.IndexOf(args, "-diceNetJoin") >= 0))
                Application.runInBackground = true;
        }

        [Serializable]
        private sealed class SmokeReport
        {
            public bool passed;
            public string unity, version, graphics;
            public string[] checks, errors;
        }

        private void BeginPlayerSmokeIfRequested()
        {
            string requestedOutput = ResolveRequestedSmokeOutput();
            if (requestedOutput == null) return;
            _smokeOutput = requestedOutput;
            Directory.CreateDirectory(_smokeOutput);
            Application.logMessageReceived += ObserveSmokeError;
            StartCoroutine(SmokeGuard(ScoreDuelPresentationActive ? ScoreDuelPlayerSmoke() : PlayerSmoke()));
        }

        private static string ResolveRequestedSmokeOutput()
        {
            // Replayed players pass the command-line flag. Editor play sessions cannot pass
            // flags, so the launcher menu item publishes the requested directory through an
            // environment variable instead. With neither set, ordinary startup in build and
            // editor stays smoke-free exactly as before.
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-arcaneSmokeOutput")
                    return Path.GetFullPath(args[i + 1]);
            string fromLauncher = Environment.GetEnvironmentVariable("ArcaneSmokeOutput");
            if (!string.IsNullOrEmpty(fromLauncher))
                return Path.GetFullPath(fromLauncher);
            return null;
        }

        private void ObserveSmokeError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _smokeErrors.Add(message);
        }

        private IEnumerator SmokeGuard(IEnumerator routine)
        {
            while (true)
            {
                object next;
                try
                {
                    if (!routine.MoveNext()) break;
                    next = routine.Current;
                }
                catch (Exception error)
                {
                    _smokeErrors.Add(error.ToString());
                    break;
                }
                yield return next;
            }
            Application.logMessageReceived -= ObserveSmokeError;
            var report = new SmokeReport { passed = _smokeErrors.Count == 0, unity = Application.unityVersion,
                version = Application.version, graphics = SystemInfo.graphicsDeviceType.ToString(),
                checks = _smokeChecks.ToArray(), errors = _smokeErrors.ToArray() };
            File.WriteAllText(Path.Combine(_smokeOutput, "player-smoke.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }

        private IEnumerator PlayerSmoke()
        {
            yield return new WaitForSecondsRealtime(3f);
            yield return new WaitForEndOfFrame();
            SmokeCheck(_titleRoot.gameObject.activeSelf, "release starts on title");
            SmokeCapture("title");
            Click(_titleRoot, "Learn");
            yield return new WaitForEndOfFrame();
            SmokeCapture("practice-1");
            Click(_guideRoot, "PracticeDie4");
            Click(_guideRoot, "GuideNext");
            yield return new WaitForEndOfFrame();
            SmokeCapture("practice-2");
            Click(_guideRoot, "GuideNext");
            yield return new WaitForEndOfFrame();
            SmokeCapture("practice-3");
            Click(_guideRoot, "GuideNext");
            if (_guideRoot.gameObject.activeSelf)
            {
                yield return new WaitForEndOfFrame();
                SmokeCapture("practice-4");
                Click(_guideRoot, "GuideNext");
            }
            SmokeCheck(_engine.PlayerHp == 32 && _engine.History.Count == 0, "practice starts a fresh production match");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return new WaitForEndOfFrame();
            SmokeCapture("battle");
            OpenMenu();
            int[] before = (int[])_engine.PlayerDice.Clone();
            HandleShortcut(KeyCode.Space);
            HandleShortcut(KeyCode.Alpha1);
            DoCommit();
            SmokeCheck(_engine.History.Count == 0 && _engine.PlayerRerollsLeft == 2 &&
                Scoring.Sum(before) == Scoring.Sum(_engine.PlayerDice), "menu blocks gameplay and keyboard shortcuts");
            yield return new WaitForEndOfFrame();
            SmokeCapture("menu");
            if (_creditsRoot != null)
            {
                Click(_menuRoot, "Credits");
                DoCommit();
                SmokeCheck(_engine.History.Count == 0 && _creditsRoot.gameObject.activeInHierarchy,
                    "asset credits block battle input");
                yield return new WaitForEndOfFrame();
                SmokeCapture("credits");
                Click(_creditsRoot, "CloseCredits");
                OpenMenu();
                bool initialMotion = _reducedMotion;
                Click(_menuRoot, "Motion");
                SmokeCheck(_reducedMotion != initialMotion, "motion setting toggles through its real button");
                Click(_menuRoot, "Motion");
            }
            Click(_menuRoot, "Resume");
            Click(_battleRoot, "BattleRules");
            yield return new WaitForEndOfFrame();
            SmokeCapture("rules");
            Click(_rulesRoot, "CloseRules");

            // Rendering the actual executable at narrow and wide aspect ratios catches issues
            // that a design-canvas assertion alone cannot detect.
            UnityEngine.Screen.SetResolution(1024, 768, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForEndOfFrame();
            SmokeCapture("battle-4x3");
            UnityEngine.Screen.SetResolution(1920, 820, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForEndOfFrame();
            SmokeCapture("battle-wide");
            UnityEngine.Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.5f);

            for (int opponent = 0; opponent < Opponents.Length; opponent++)
            {
                ShowScreen(Screen.Title);
                Click(_titleRoot, "Opponent" + opponent);
                Click(_titleRoot, "Start");
                yield return new WaitForSecondsRealtime(0.65f);
                for (int round = 0; round < 10 && _engine.Outcome == null; round++)
                {
                    Click(_battleRoot, "Commit");
                    yield return new WaitForSecondsRealtime(0.5f);
                    if (opponent == 0 && round == 0)
                    {
                        yield return new WaitForEndOfFrame();
                        SmokeCapture("reveal");
                        Click(_battleRoot, "BattleHistory");
                        yield return new WaitForEndOfFrame();
                        SmokeCapture("history");
                        Click(_journalRoot, "CloseJournal");
                    }
                    if (_engine.Outcome == null) Click(_battleRoot, "Advance");
                    yield return new WaitForSecondsRealtime(0.65f);
                }
                SmokeCheck(_engine.Outcome != null, "opponent " + opponent + " finishes within 10 rounds");
                Click(_battleRoot, "Advance");
                yield return new WaitForEndOfFrame();
                SmokeCapture("result-" + opponent);
                Click(_resultRoot, "Rematch");
                SmokeCheck(_engine.PlayerHp == 32 && _engine.NpcHp == 32 && _engine.History.Count == 0,
                    "opponent " + opponent + " rematch resets HP and history");
            }
            OpenMenu();
            Click(_menuRoot, "ReturnTitle");
            SmokeCheck(_engine != null, "leaving a running duel requires confirmation");
            Click(_menuRoot, "ReturnTitle");
            SmokeCheck(_engine == null && _titleRoot.gameObject.activeSelf, "confirmed leave returns to title");
            SmokeCheck(!InputLog.Enabled, "release input logging is disabled");
            if (ResolveConfig().TacticalEnabled)
            {
                IEnumerator trial = SmokeTrial();
                while (trial.MoveNext()) yield return trial.Current;
            }
        }

        private static void Click(Transform root, string name)
        {
            var child = root.Find(name);
            var button = child == null ? null : child.GetComponent<Button>();
            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                throw new InvalidOperationException("Cannot press " + name);
            button.onClick.Invoke();
        }

        private void SmokeCheck(bool pass, string name)
        {
            if (!pass) throw new InvalidOperationException(name);
            _smokeChecks.Add(name);
        }

        private void SmokeCapture(string name)
        {
            // Hidden/minimized Windows players can have no screenshot back buffer. Render the
            // real player Canvas offscreen, just as the existing Editor capture suite does.
            int width = UnityEngine.Screen.width, height = UnityEngine.Screen.height;
            if (width < 640 || height < 360) throw new InvalidOperationException("Invalid viewport: " + name);
            Canvas canvas = _canvasRoot.GetComponent<Canvas>();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var cameraHost = new GameObject("SmokeCaptureCamera");
            Camera camera = cameraHost.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = height * 0.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiFactory.Background;
            camera.targetTexture = target;
            RenderMode mode = canvas.renderMode;
            Camera originalCamera = canvas.worldCamera;
            float plane = canvas.planeDistance;
            RenderTexture active = RenderTexture.active;
            Texture2D image = null;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 5;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(_smokeOutput, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                canvas.renderMode = mode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = plane;
                camera.targetTexture = null;
                if (image != null) Destroy(image);
                Destroy(cameraHost);
                target.Release();
                Destroy(target);
            }
        }
    }
}
