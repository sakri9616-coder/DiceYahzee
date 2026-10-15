using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ParvaMirabilia.Tests.PlayMode
{
    /// <summary>
    /// Photographs the running screen.
    ///
    /// Every other test here answers a question with a number, and a screen can satisfy all of
    /// them while still looking wrong: a corner that should be rounded and is not, a label that
    /// is technically on the button and practically invisible, a row that fits the canvas and
    /// crowds everything around it. Those are only questions a picture answers, so the screen is
    /// rendered through a camera into a file that a reviewer can open.
    ///
    /// The canvas is moved to camera space for the duration of the shot and put back afterwards;
    /// the geometry is measured in ScreenDumpTests, and this is the same state measured.
    /// </summary>
    public sealed class ScreenCaptureTests
    {
        private GameFlow _flow;

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
        public IEnumerator BattleScreen_IsPhotographedAsItIsDrawn()
        {
            StartDuel();
            yield return new WaitForSeconds(1.2f);          // the opening throw has settled
            yield return Capture("battle");

            // The marked state colours a die face, a ring and a badge - a different picture from
            // the untouched one, and the one that has to say "this die is being thrown".
            Method("OnPlayerDieClicked").Invoke(_flow, new object[] { 1 });
            yield return null;
            yield return Capture("battle-marked");

            Method("SetRulesVisible").Invoke(_flow, new object[] { true });
            yield return null;
            yield return Capture("rules");

            // The rules scroll - 858 units of text in a 534 unit slot - so a second frame taken
            // at the bottom is the only way to photograph 【ダメージの計算】【勝敗の決め方】
            // 【コツ】, the three sections that used to be cut off and simply not drawn.
            var rulesRoot = RootOfScreen("rules");
            var rulesSlot = rulesRoot.Find("RulesBodySlot");
            var rulesScroll = rulesSlot != null ? rulesSlot.GetComponent<ScrollRect>() : null;
            Assert.That(rulesScroll, Is.Not.Null, "the rules body no longer scrolls");
            rulesScroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return Capture("rules-end", rulesRoot);
            rulesScroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();

            Method("SetRulesVisible").Invoke(_flow, new object[] { false });
        }

        [UnityTest]
        public IEnumerator TitleScreen_IsPhotographedAsItIsDrawn()
        {
            var host = new GameObject("GameFlowUnderTest");
            _flow = host.AddComponent<GameFlow>();
            yield return null;
            yield return Capture("title");
        }

        /// <summary>
        /// The sprite on its own, with no die or button around it to blame.
        ///
        /// This is the photograph that found the defect: nine-slicing the sprite made Unity
        /// scale the corner by the size of whatever it landed on, so a 320 x 120 probe came back
        /// as an ellipse and the dice came back as circles. Re-taken after the sprite was cut to
        /// each widget's own size, it is the one picture that shows the fix without having to
        /// read a die to see it.
        /// </summary>
        [UnityTest]
        public IEnumerator RoundedSprite_IsPhotographedOnItsOwn()
        {
            StartDuel();
            yield return null;

            var battleField = typeof(GameFlow).GetField("_battleRoot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var battleRoot = battleField.GetValue(_flow) as RectTransform;
            Assert.That(battleRoot, Is.Not.Null, "_battleRoot is missing");
            bool wasActive = battleRoot.gameObject.activeSelf;
            battleRoot.gameObject.SetActive(false);

            var canvasObject = new GameObject("ProbeCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                var probe = new GameObject("Probe", typeof(RectTransform), typeof(Image));
                probe.transform.SetParent(canvasObject.transform, false);
                var rect = probe.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(320f, 120f);
                rect.anchoredPosition = Vector2.zero;

                var image = probe.GetComponent<Image>();
                image.sprite = RoundedSprite(320, 120);
                image.color = new Color32(0xE8, 0xC8, 0x7A, 0xFF);

                yield return null;
                yield return Capture("probe", rect);
            }
            finally
            {
                battleRoot.gameObject.SetActive(wasActive);
                UnityEngine.Object.Destroy(canvasObject);
            }
        }

        // ---------------------------------------------------------------- capture
        private IEnumerator Capture(string screen)
        {
            return Capture(screen, RootOfScreen(screen));
        }

        private IEnumerator Capture(string screen, RectTransform root)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            int width = Screen.width;
            int height = Screen.height;
            if (width < 160 || height < 160)
            {
                // Better to say so than to write a black square and call it a screenshot.
                Debug.LogWarning("[CAPTURE] batch runner reports " + width + "x" + height +
                                 " - nothing to photograph");
                yield break;
            }

            Canvas canvas = root.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null, screen + " is not under a canvas");

            // Twice the runner's own resolution. The camera's framing is set in world units and
            // the aspect is unchanged, so nothing moves - the same screen is simply sampled more
            // densely, which is what makes a 6-unit corner readable in the file.
            int shotWidth = width * 2;
            int shotHeight = height * 2;
            var target = new RenderTexture(shotWidth, shotHeight, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("__ScreenCapture");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = height * 0.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // UiFactory is internal to the runtime assembly, so the backdrop is restated here:
            // the same 0x12122A the screen is designed against, so a transparent region of the
            // shot shows the background rather than black.
            camera.backgroundColor = new Color32(0x12, 0x12, 0x2A, 0xFF);
            camera.targetTexture = target;

            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 5f;
                Canvas.ForceUpdateCanvases();

                camera.Render();

                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = target;
                var pixels = new Texture2D(shotWidth, shotHeight, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, shotWidth, shotHeight), 0, 0);
                pixels.Apply();
                RenderTexture.active = previousActive;

                string logsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "logs"));
                Directory.CreateDirectory(logsDir);
                string path = Path.Combine(logsDir, "unity-" + screen + ".png");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                UnityEngine.Object.Destroy(pixels);
                Debug.Log("[CAPTURE] screen " + width + "x" + height + " -> " +
                          shotWidth + "x" + shotHeight + " -> " + path);
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                camera.targetTexture = null;
                UnityEngine.Object.Destroy(cameraObject);
                UnityEngine.Object.Destroy(target);
            }
        }

        private RectTransform RootOfScreen(string screen)
        {
            string field = screen == "title" ? "_titleRoot"
                : screen == "rules" ? "_rulesRoot" : "_battleRoot";
            var root = Field(field).GetValue(_flow) as RectTransform;
            Assert.That(root, Is.Not.Null, field + " is missing");
            return root;
        }

        // ----------------------------------------------------------------- helpers
        private void StartDuel()
        {
            PlayerPrefs.SetInt("pv.audioStep", 3);
            PlayerPrefs.Save();

            _flow = UnityEngine.Object.FindFirstObjectByType<GameFlow>();
            if (_flow == null)
            {
                var host = new GameObject("GameFlowUnderTest");
                _flow = host.AddComponent<GameFlow>();
            }

            GameFlow.ConfigOverride = null;
            Method("StartMatch").Invoke(_flow, null);
        }

        private MethodInfo Method(string name)
        {
            return typeof(GameFlow).GetMethod(name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
        }

        private FieldInfo Field(string name)
        {
            return typeof(GameFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        /// <summary>The generated rounded sprite, reached through the internal factory.</summary>
        private static Sprite RoundedSprite(int width, int height)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type factory = assembly.GetType("ParvaMirabilia.Presentation.UiFactory", false);
                if (factory == null)
                    continue;
                var method = factory.GetMethod("RoundedSprite",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(int), typeof(int) }, null);
                if (method != null)
                    return method.Invoke(null, new object[] { width, height }) as Sprite;
            }

            return null;
        }
    }
}
