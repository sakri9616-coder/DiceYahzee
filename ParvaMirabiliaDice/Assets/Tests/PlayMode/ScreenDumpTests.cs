using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// Writes what the screens actually are, so the layout can be judged as a picture rather than
    /// as a list of assertions.
    ///
    /// Every other test on this screen asks a question that can be answered with a number - does
    /// it overlap, does it fit, does the throw settle - and a screen can pass all of them while
    /// still being unpleasant to look at. This file turns the running screen into geometry:
    /// where each element sits in the design canvas, how big it is, what colour it is, what it
    /// says and at what size. The output is rendered to an image and reviewed against the
    /// conventions of games that are already known to be well made, which is the review the
    /// assertions above cannot perform.
    ///
    /// It writes reports the way the sweep tests do; it is a measurement, not a screenshot, and
    /// it deliberately has no dependency on a display.
    /// </summary>
    public sealed class ScreenDumpTests
    {
        /// <summary>The design canvas the screen is laid out against - see <see cref="GameFlow"/>.</summary>
        private const float HalfWidth = 640f;
        private const float HalfHeight = 360f;

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
        public IEnumerator Screens_AreDumpedAsGeometryForVisualReview()
        {
            StartDuel();
            RestartUntilTheHandOffers(4);   // the widest yaku row, which is the row that can break
            yield return new WaitForSeconds(1.2f);          // the opening throw has settled

            int total = Dump("battle", "_battleRoot");

            // A marked die is a different picture from an unmarked one - it changes a die's face
            // colour, its ring and its badge - so both states are worth keeping.
            Method("OnPlayerDieClicked").Invoke(_flow, new object[] { 1 });
            yield return null;
            total += Dump("battle-marked", "_battleRoot");

            Method("SetRulesVisible").Invoke(_flow, new object[] { true });
            yield return null;
            total += Dump("rules", "_rulesRoot");

            Assert.That(total, Is.GreaterThan(40),
                "almost nothing was captured - the screens are empty or the walk is broken");
        }

        [UnityTest]
        public IEnumerator TitleScreen_IsDumpedAsGeometryForVisualReview()
        {
            var host = new GameObject("GameFlowUnderTest");
            _flow = host.AddComponent<GameFlow>();
            yield return null;

            int total = Dump("title", "_titleRoot");
            Assert.That(total, Is.GreaterThan(5), "the title screen captured almost nothing");
        }

        // ------------------------------------------------------------------ capture
        /// <summary>
        /// Re-deals until this hand may declare <paramref name="wanted"/> yaku. How many buttons
        /// are on screen depends entirely on the dice, so a screen captured from whatever hand
        /// came up first is usually the sparse one - and the row that has to be reviewed is the
        /// full row, because that is the row that can run off the canvas. Four is the most a
        /// five-dice hand can ever offer (a pair and the two straights cannot coexist).
        /// </summary>
        private void RestartUntilTheHandOffers(int wanted)
        {
            var engineField = Field("_engine");
            var resolve = Method("ResolveConfig");

            for (int attempt = 0; attempt < 400; attempt++)
            {
                Method("StartMatch").Invoke(_flow, null);
                var engine = engineField.GetValue(_flow) as MatchEngine;
                var config = resolve.Invoke(_flow, null) as GameConfig;
                if (engine == null || engine.PlayerDice == null || config == null)
                    continue;

                if (Yaku.Evaluate(engine.PlayerDice, config).Length >= wanted)
                    return;
            }

            Assert.Fail("no hand offering " + wanted + " yaku came up in 400 deals");
        }

        private int Dump(string screen, string rootField)
        {
            var root = Field(rootField).GetValue(_flow) as RectTransform;
            Assert.That(root, Is.Not.Null, rootField + " is missing");

            Canvas canvas = root.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null, screen + " is not under a canvas");
            Transform canvasTransform = canvas.transform;

            Rect canvasBox = new Rect(-HalfWidth, -HalfHeight, HalfWidth * 2f, HalfHeight * 2f);
            var elements = new List<string>();
            Walk(root, canvasTransform, screen, elements);

            var json = new StringBuilder(elements.Count * 260 + 160);
            json.Append("{\n");
            json.Append("  \"screen\": \"").Append(screen).Append("\",\n");
            json.Append("  \"canvas\": { \"width\": ").Append(canvasBox.width.ToString(CultureInfo.InvariantCulture))
                .Append(", \"height\": ").Append(canvasBox.height.ToString(CultureInfo.InvariantCulture))
                .Append(" },\n");
            json.Append("  \"elements\": [\n");
            for (int i = 0; i < elements.Count; i++)
            {
                json.Append("    ").Append(elements[i]);
                json.Append(i + 1 < elements.Count ? ",\n" : "\n");
            }
            json.Append("  ]\n}\n");

            string logsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "logs"));
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, "screen-" + screen + ".json"),
                json.ToString(), new UTF8Encoding(false));

            return elements.Count;
        }

        /// <summary>
        /// Depth-first, which is the order the canvas paints in: a parent is drawn before its
        /// children and an earlier sibling before a later one, so appending in this order gives
        /// the renderer a stack it can simply paint top to bottom.
        /// </summary>
        private void Walk(RectTransform node, Transform canvasTransform, string path,
            List<string> into)
        {
            Rect box = BoxInCanvas(node, canvasTransform);
            bool visible = node.gameObject.activeInHierarchy;

            var fields = new List<string>(8);
            fields.Add("\"name\": " + Quote(node.name));
            fields.Add("\"path\": " + Quote(path));
            fields.Add("\"visible\": " + (visible ? "true" : "false"));
            fields.Add("\"x\": " + Num(box.xMin));
            fields.Add("\"y\": " + Num(box.yMin));
            fields.Add("\"w\": " + Num(box.width));
            fields.Add("\"h\": " + Num(box.height));

            Image image = node.GetComponent<Image>();
            if (image != null)
            {
                fields.Add("\"fill\": " + Quote(Color(image.color)));
                fields.Add("\"imageOn\": " + (image.enabled ? "true" : "false"));
                fields.Add("\"sprite\": " + Quote(image.sprite != null ? image.sprite.name : ""));
            }

            Text text = node.GetComponent<Text>();
            if (text != null)
            {
                fields.Add("\"text\": " + Quote(text.text));
                fields.Add("\"textOn\": " + (text.enabled ? "true" : "false"));
                fields.Add("\"fontSize\": " + text.fontSize.ToString(CultureInfo.InvariantCulture));
                fields.Add("\"fontColor\": " + Quote(Color(text.color)));
                fields.Add("\"align\": " + Quote(text.alignment.ToString()));
                fields.Add("\"bestFit\": " + (text.resizeTextForBestFit ? "true" : "false"));
                if (text.resizeTextForBestFit)
                    fields.Add("\"bestFitMin\": " + text.resizeTextMinSize);
                fields.Add("\"lineSpacing\": " + Num(text.lineSpacing));
                fields.Add("\"wrap\": " + Quote(text.horizontalOverflow.ToString()));
            }

            Button button = node.GetComponent<Button>();
            if (button != null)
                fields.Add("\"interactable\": " + (button.interactable ? "true" : "false"));

            CanvasGroup group = node.GetComponent<CanvasGroup>();
            if (group != null && group.alpha < 1f)
                fields.Add("\"groupAlpha\": " + Num(group.alpha));

            into.Add("{ " + string.Join(", ", fields.ToArray()) + " }");

            for (int i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i) as RectTransform;
                if (child != null)
                    Walk(child, canvasTransform, path + "/" + child.name, into);
            }
        }

        /// <summary>The child's box in canvas space, derived from world corners so stretched
        /// elements (the full-bleed bars) are measured the same way centred ones are.</summary>
        private static Rect BoxInCanvas(RectTransform node, Transform canvasTransform)
        {
            var corners = new Vector3[4];
            node.GetWorldCorners(corners);

            Vector3 min = canvasTransform.InverseTransformPoint(corners[0]);   // bottom left
            Vector3 max = canvasTransform.InverseTransformPoint(corners[2]);   // top right
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static string Color(Color color)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}",
                Mathf.RoundToInt(color.r * 255f), Mathf.RoundToInt(color.g * 255f),
                Mathf.RoundToInt(color.b * 255f), Mathf.RoundToInt(color.a * 255f));
        }

        private static string Num(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Quote(string value)
        {
            var sb = new StringBuilder((value ?? "").Length + 2);
            sb.Append('"');
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\')
                    sb.Append('\\').Append(c);
                else if (c == '\n')
                    sb.Append("\\n");
                else if (c == '\r')
                    sb.Append("\\r");
                else if (c == '\t')
                    sb.Append("\\t");
                else if (c < ' ')
                    sb.Append("\\u").Append(((int)c).ToString("x4"));
                else
                    sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
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
            // Static as well as instance: ResolveConfig is static, and a lookup that misses it
            // returns null rather than failing, which surfaces as a bare NullReferenceException
            // at the call site instead of as a missing method.
            return typeof(GameFlow).GetMethod(name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
        }

        private FieldInfo Field(string name)
        {
            return typeof(GameFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
