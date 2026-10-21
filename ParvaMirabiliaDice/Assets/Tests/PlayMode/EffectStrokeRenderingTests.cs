using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ParvaMirabilia.Quality.Tests
{
    /// <summary>
    /// Pixel-level regression for the stroke effects. The smoke runs verify data (stroke counts
    /// and alphas) but not pixels, so a stroke mesh could vanish in editor play with every check
    /// still green. The cause was one step outside the effect code: uGUI creates a Graphic's
    /// CanvasRenderer lazily on its first rebuild, and on a custom Graphic subclass that lazy add
    /// can hand back a renderer that is already gone. The Graphic caches the dead wrapper, every
    /// later rebuild bails out at "canvasRenderer == null", and the mesh is never drawn; built-in
    /// Images and player builds take a different path, which is why only the custom strokes
    /// disappeared and only in the editor. UiFactory.AddVertexGraphic creates the renderer up
    /// front, and these probes render the production construction path offscreen and assert on
    /// the read-back pixels.
    /// </summary>
    public sealed class EffectStrokeRenderingTests
    {
        private GameObject _cameraObject;
        private GameObject _canvasObject;
        private Camera _camera;
        private RenderTexture _target;

        private static readonly Type StrokeType =
            typeof(ParvaMirabilia.Presentation.GameFlow).Assembly.GetType(
                "ParvaMirabilia.Presentation.ScoreHandEffectGraphic", true);

        private static readonly Type FactoryType =
            typeof(ParvaMirabilia.Presentation.GameFlow).Assembly.GetType(
                "ParvaMirabilia.Presentation.UiFactory", true);

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("StrokeProbeCamera", typeof(Camera));
            _camera = _cameraObject.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 64f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 1f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 50f;
            _target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _target;

            _canvasObject = new GameObject("StrokeProbeCanvas", typeof(Canvas));
            var canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 5f;
            ((RectTransform)_canvasObject.transform).sizeDelta = new Vector2(128f, 128f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasObject != null) Object.Destroy(_canvasObject);
            if (_cameraObject != null) Object.Destroy(_cameraObject);
            if (_target != null) { _target.Release(); Object.Destroy(_target); }
        }

        [UnityTest]
        public System.Collections.IEnumerator TheFactoryStrokes_ReachTheRenderedPixels()
        {
            GameObject host = Anchor("StrokeProbeFx");
            Component strokes = AttachStroke(host);

            Assert.That(host.GetComponent<CanvasRenderer>(), Is.Not.Null,
                "the effect factory must attach a live CanvasRenderer instead of leaving it to " +
                "the lazy path that can cache a dead one");

            StrokeType.GetMethod("Line").Invoke(strokes, new object[]
            {
                new Vector2(-80f, 0f), new Vector2(80f, 0f), 20f, new Color(1f, 0f, 0f, 1f)
            });
            StrokeType.GetMethod("Flush").Invoke(strokes, null);

            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(ReadVertsDirty((Graphic)strokes), Is.False,
                "the forced update must consume the stroke graphic's dirty flag, proving Rebuild " +
                "ran instead of bailing out on a missing CanvasRenderer");
            CanvasRenderer renderer = host.GetComponent<CanvasRenderer>();
            Assert.That(renderer, Is.Not.Null, "the CanvasRenderer must survive the rebuild");
            Assert.That(renderer.cull, Is.False, "the stroke renderer must not be culled");

            _camera.Render();

            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target;
                Texture2D read = new Texture2D(128, 128, TextureFormat.RGB24, false);
                read.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                read.Apply();
                Color center = read.GetPixel(64, 64);
                Object.Destroy(read);
                Assert.That(center.r, Is.GreaterThan(0.5f),
                    "the stroked line's interior must read as red on the rendered pixels");
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator APlainSpriteImage_RendersThroughTheSamePath()
        {
            GameObject control = Anchor("ControlImage");
            RectTransform rect = (RectTransform)control.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(40f, 40f);
            rect.anchoredPosition = Vector2.zero;
            Image image = control.AddComponent<Image>();
            var white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color32[] fill = new Color32[16];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(255, 255, 255, 255);
            white.SetPixels32(fill);
            white.Apply();
            image.sprite = Sprite.Create(white, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 32f);
            image.color = new Color(0f, 1f, 0f, 1f);

            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            _camera.Render();

            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target;
                Texture2D read = new Texture2D(128, 128, TextureFormat.RGB24, false);
                read.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                read.Apply();
                Color center = read.GetPixel(64, 64);
                Color outside = read.GetPixel(96, 64);
                Object.Destroy(read);
                Assert.That(center.g, Is.GreaterThan(0.5f),
                    "the control Image must be visible through the same render path");
                Assert.That(outside.g, Is.LessThan(0.2f),
                    "the control Image must be limited to its declared rectangle");
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private GameObject Anchor(string name)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(_canvasObject.transform, false);
            RectTransform rect = (RectTransform)host.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return host;
        }

        private static Component AttachStroke(GameObject host)
        {
            var add = FactoryType.GetMethod("AddVertexGraphic",
                BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(StrokeType);
            return (Component)add.Invoke(null, new object[] { host });
        }

        private static bool ReadVertsDirty(Graphic graphic)
        {
            return (bool)typeof(Graphic)
                .GetField("m_VertsDirty", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(graphic);
        }
    }
}
