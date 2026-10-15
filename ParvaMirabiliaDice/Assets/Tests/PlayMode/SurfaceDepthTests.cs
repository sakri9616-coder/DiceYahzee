using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ParvaMirabilia.Tests.PlayMode
{
    /// <summary>
    /// Whether the screen has any material at all: light, shadow and a corner that agrees with
    /// the next widget's corner.
    ///
    /// The screenshot review scored every screen 3 out of 10 on texture and depth, and the three
    /// reviewer agents agreed on the reason without knowing each other: the whole game is one
    /// flat fill. Panels and dice are flat rectangles, the only shadow in the project belongs to
    /// the dice, and the corner a bar rounds is 16 units while the corner the result frame rounds
    /// is nothing at all. None of that is visible to a test that only checks geometry, because a
    /// flat rectangle is perfectly good geometry.
    ///
    /// So the material is measured here: the shadow has to be *behind* what it belongs to (which
    /// is a real question - uGUI's Shadow is another quad in the same mesh, and if it landed after
    /// the face the buttons would be wearing a second face), the light has to reach zero before
    /// the bottom of the plate (a highlight with a bottom edge is a seam), and every surface has
    /// to take the same corner.
    /// </summary>
    public sealed class SurfaceDepthTests
    {
        private const float TopLight = 0.10f;

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
        public IEnumerator EveryFaceCarriesTheSameDepth()
        {
            StartDuel();
            EnsureEventSystem();
            yield return null;
            Canvas.ForceUpdateCanvases();

            foreach (string name in new[] { "Commit", "Reroll", "Yaku0" })
            {
                RectTransform faceRect = ButtonNamed(name);
                var face = faceRect.GetComponent<Image>();
                Assert.That(face, Is.Not.Null, name + " has no face");

                var shadow = face.GetComponent<Shadow>();
                Assert.That(shadow, Is.Not.Null,
                    name + " is a flat plate on the field: a button with no depth is a rectangle " +
                    "somebody drew, not a thing on the table");

                Assert.That(shadow.effectDistance, Is.EqualTo(new Vector2(0f, -4f)),
                    name + " has a shadow that does not fall, which flattens it");
                Assert.That(shadow.effectColor.a, Is.GreaterThan(0.3f).And.LessThan(0.7f),
                    name + " has a shadow that is either invisible or a stain");
            }
        }

        /// <summary>
        /// The one thing about a shadow that cannot be reasoned into place: uGUI draws a Shadow by
        /// appending a copy of the graphic's own quad to the same mesh, and which quad ends up on
        /// top decides whether the shadow is a shadow or a second face. This reads the mesh and
        /// finds out.
        /// </summary>
        [UnityTest]
        public IEnumerator TheShadowDrawsBehindTheFaceItBelongsTo()
        {
            StartDuel();
            EnsureEventSystem();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var face = ButtonNamed("Commit").GetComponent<Image>();
            var shadow = face.GetComponent<Shadow>();
            Assert.That(shadow, Is.Not.Null, "Commit carries no shadow to inspect");

            var mesh = face.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null, "the face produced no mesh");
            var verts = mesh.vertices;
            Assert.That(verts.Length, Is.GreaterThanOrEqualTo(8),
                "the mesh holds " + verts.Length + " vertices, so the shadow was never drawn as " +
                "part of it - it cannot be behind the face either");

            // Shadow expands the graphic into a triangle stream (six vertices per quad).
            // Comparing four vertices at opposite ends compares different parts of the face.
            int half = verts.Length / 2;
            Assert.That(verts.Length % 2, Is.Zero);
            float shadowY = AverageY(verts, 0, half);
            float faceY = AverageY(verts, half, half);
            float fall = faceY - shadowY;
            Assert.That(fall, Is.GreaterThan(0f),
                "the first quad in the mesh sits above the face, so the shadow is drawn over it " +
                "and the button wears a second, darker face");
            Assert.That(fall, Is.EqualTo(Mathf.Abs(shadow.effectDistance.y)).Within(1.5f),
                "the offset quad sits " + fall.ToString("0.0") + " units away from the face, not " +
                "the " + shadow.effectDistance.y + " units the shadow asks for");

            for (int i = 0; i < half; i++)
            {
                Assert.That(verts[i + half].y - verts[i].y,
                    Is.EqualTo(-shadow.effectDistance.y).Within(0.001f));
                Assert.That(verts[i + half].x - verts[i].x,
                    Is.EqualTo(-shadow.effectDistance.x).Within(0.001f));
            }

            // A shadow that is not the shape of what cast it is a smudge.
            Assert.That(shadow.useGraphicAlpha, Is.True,
                "the shadow ignores the face's alpha, so it leaks outside the rounded corners");
        }

        /// <summary>
        /// The disabled face is the one number the code documents and the render never produced.
        /// The state tint multiplies the face, and it used to multiply it by grey: #262642 came
        /// out at 1.00:1 against the screen behind it, which is an absence rather than a dimmed
        /// action, and the comment above it still claimed 1.26:1. The tint is now out of the way
        /// and the dimming is done by the face itself, so the rendered face is measured here
        /// rather than the colour that was assigned to it.
        /// </summary>
        [UnityTest]
        public IEnumerator ADisabledFace_StillCarriesItsSilhouette()
        {
            StartDuel();
            EnsureEventSystem();
            yield return null;

            Button commit = ButtonNamed("Commit").GetComponent<Button>();
            SetButtonEnabled(commit, false);
            Assert.That(commit.interactable, Is.False, "the button was not actually greyed out");

            var face = commit.GetComponent<Image>();
            Color rendered = Multiply(face.color, commit.colors.disabledColor);
            float ratio = OverContrast(rendered, new Color32(0x12, 0x12, 0x2A, 0xFF));
            Assert.That(ratio, Is.GreaterThanOrEqualTo(1.2f),
                "the greyed-out button renders at " + ratio.ToString("0.00") + ":1 against the " +
                "screen, so the silhouette it documents is not the one it draws - a disabled " +
                "action disappears instead of dimming");

            SetButtonEnabled(commit, true);
        }

        [UnityTest]
        public IEnumerator EveryPanel_TakesTheSameCornerAsEveryOtherPanel()
        {
            StartDuel();
            EnsureEventSystem();
            yield return null;

            // A bar that rounds its corner while the frame around it does not is the half-applied
            // rule a reader notices and cannot name.
            AssertPanelIsRounded("NpcBar");
            AssertPanelIsRounded("RulesPanel");
            AssertPanelIsRounded("ResultPanel");
        }

        /// <summary>
        /// Light that stops before the bottom edge is a surface. Light that runs the full height
        /// with an edge at the bottom is a second rectangle stacked on the first, and it is the
        /// difference between glass and a sticker.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLightOnAPlate_StopsBeforeTheBottomEdge()
        {
            StartDuel();
            EnsureEventSystem();
            yield return null;

            var gloss = Find(ButtonNamed("Commit"), "Gloss");
            Assert.That(gloss, Is.Not.Null, "no light was laid over the button");

            var image = gloss.GetComponent<Image>();
            Assert.That(image.sprite, Is.Not.Null, "the light has nothing to draw");

            Texture2D texture = image.sprite.texture;
            int width = texture.width;
            int height = texture.height;

            float top = texture.GetPixel(width / 2, height - 2).a;
            float middle = texture.GetPixel(width / 2, height / 2).a;
            float bottom = texture.GetPixel(width / 2, 1).a;
            float corner = texture.GetPixel(0, 0).a;

            Assert.That(top, Is.GreaterThan(TopLight),
                "the light is " + top.ToString("0.00") + " strong at the top of the plate, which " +
                "is not enough to read as a lit surface at all");
            Assert.That(middle, Is.LessThan(0.01f),
                "the light reaches " + middle.ToString("0.00") + " in the middle of the plate, " +
                "so it is a wash rather than a highlight");
            Assert.That(bottom, Is.LessThan(0.01f),
                "the light is still " + bottom.ToString("0.00") + " strong at the bottom edge, " +
                "which puts a visible seam across the plate");
            Assert.That(corner, Is.LessThan(0.01f),
                "the light ignores the rounded corner, so it shines out of the sides");
        }

        /// <summary>
        /// A health bar that is resized instead of cropped grows square corners over the track's
        /// rounded ones, so the two halves of one bar disagree about what shape a bar is. Cropping
        /// keeps the fill the material it was drawn with.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHealthBar_IsCroppedRatherThanResized()
        {
            StartDuel();
            EnsureEventSystem();
            yield return null;

            var fill = Field<Image>("_npcHpFill");
            Assert.That(fill, Is.Not.Null, "no health bar fill to inspect");

            Assert.That(fill.transform.parent, Is.Not.Null, "the fill is not inside its track");
            float tracked = fill.transform.parent.GetComponent<RectTransform>().rect.width;
            Assert.That(fill.rectTransform.rect.width, Is.EqualTo(tracked).Within(0.5f),
                "the fill is " + fill.rectTransform.rect.width.ToString("0.0") + " of " +
                tracked.ToString("0.0") + ": it has been resized, so its corners will not match " +
                "the track's at any value of health");
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled),
                "the fill is not cropped, so it cannot keep its shape");
            Assert.That(fill.sprite, Is.Not.Null, "the fill has no material of its own");
            Assert.That(fill.fillAmount, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f),
                "the fill reports " + fill.fillAmount + " of a bar, which is no bar at all");
        }

        /// <summary>
        /// A die gives no sign that it will answer until the click has already decided something.
        /// The lift is that sign, and it only counts if it comes back down: an effect that does
        /// not return to exactly where it started is the failure the rest of this suite is built
        /// to catch.
        /// </summary>
        [UnityTest]
        public IEnumerator ADieLiftsForThePointer_AndComesBack()
        {
            StartDuel();
            EnsureEventSystem();
            yield return new WaitForSeconds(1.6f);      // the opening throw has settled

            RectTransform die = Find(BattleRoot(), "PlayerDie0");
            Assert.That(die, Is.Not.Null, "no die to point at");
            Vector3 resting = die.localScale;

            Execute(die.gameObject, ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(die.localScale.x, Is.GreaterThan(resting.x + 0.01f),
                "the die did not answer the pointer, so a player has no way to learn it is " +
                "there to be picked up before clicking");

            Execute(die.gameObject, ExecuteEvents.pointerExitHandler);
            yield return null;
            Assert.That(die.localScale.x, Is.EqualTo(resting.x).Within(0.0005f),
                "the die stayed up after the pointer left it: the lift is " +
                die.localScale.x + " instead of " + resting.x);
        }

        /// <summary>A die that cannot be clicked must not answer the pointer either.</summary>
        [UnityTest]
        public IEnumerator ADieThatCannotBeClicked_DoesNotAnswerThePointer()
        {
            StartDuel();
            EnsureEventSystem();
            yield return new WaitForSeconds(1.6f);

            RectTransform die = Find(BattleRoot(), "NpcDie0");
            Assert.That(die, Is.Not.Null, "no hidden die to point at");
            Vector3 resting = die.localScale;

            Execute(die.gameObject, ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(die.localScale.x, Is.EqualTo(resting.x).Within(0.0005f),
                "the opponent's die lifted under the pointer, offering a click it will not take");

            Execute(die.gameObject, ExecuteEvents.pointerExitHandler);
        }

        // ------------------------------------------------------------------ helpers
        private void AssertPanelIsRounded(string name)
        {
            RectTransform panel = Find(RootFor(name), name);
            Assert.That(panel, Is.Not.Null, name + " is missing");

            var image = panel.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, name + " is not drawn");
            Assert.That(image.sprite, Is.Not.Null,
                name + " is a flat square, and a square corner is what makes code-drawn UI read " +
                "as a debug layout");

            Texture2D texture = image.sprite.texture;
            float corner = texture.GetPixel(0, 0).a;
            float middle = texture.GetPixel(texture.width / 2, texture.height / 2).a;

            Assert.That(corner, Is.LessThan(0.1f),
                name + " has " + corner.ToString("0.00") + " alpha in its corner: it is not " +
                "rounded, while the button next to it is");
            Assert.That(middle, Is.GreaterThan(0.9f),
                name + " is transparent where it should be solid");
        }

        private RectTransform RootFor(string name)
        {
            // The rules and the result screen are built once and hidden; their panels still draw,
            // so the material they carry is still theirs.
            if (name == "RulesPanel")
                return RulesRoot();
            if (name == "ResultPanel")
                return ResultRoot();
            return BattleRoot();
        }

        private void Execute<T>(GameObject target, ExecuteEvents.EventFunction<T> handler)
            where T : IEventSystemHandler
        {
            var data = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(target, data, handler);
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
                return;
            var host = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void SetButtonEnabled(Button button, bool enabled)
        {
            Type factory = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                factory = assembly.GetType("ParvaMirabilia.Presentation.UiFactory", false);
                if (factory != null)
                    break;
            }
            Assert.That(factory, Is.Not.Null, "UiFactory was not found");

            var set = factory.GetMethod("SetButtonEnabled", BindingFlags.Public | BindingFlags.Static);
            Assert.That(set, Is.Not.Null, "UiFactory.SetButtonEnabled was not found");
            set.Invoke(null, new object[] { button, enabled });
        }

        private RectTransform ButtonNamed(string name)
        {
            RectTransform rect = Find(BattleRoot(), name);
            Assert.That(rect, Is.Not.Null, name + " is missing from the battle screen");
            return rect;
        }

        private static RectTransform Find(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root as RectTransform;
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform hit = Find(root.GetChild(i), name);
                if (hit != null)
                    return hit;
            }
            return null;
        }

        private RectTransform BattleRoot()
        {
            return Root("_battleRoot");
        }

        private RectTransform RulesRoot()
        {
            return Root("_rulesRoot");
        }

        private RectTransform ResultRoot()
        {
            return Root("_resultRoot");
        }

        private RectTransform Root(string fieldName)
        {
            var field = typeof(GameFlow).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "GameFlow." + fieldName + " is missing");
            var value = field.GetValue(_flow) as RectTransform;
            Assert.That(value, Is.Not.Null, "GameFlow." + fieldName + " is empty");
            return value;
        }

        private T Field<T>(string fieldName) where T : class
        {
            var field = typeof(GameFlow).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "GameFlow." + fieldName + " is missing");
            return field.GetValue(_flow) as T;
        }

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
            typeof(GameFlow).GetMethod("StartMatch",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_flow, null);
        }

        private static float AverageY(Vector3[] verts, int start, int count)
        {
            float total = 0f;
            for (int i = 0; i < count; i++)
                total += verts[start + i].y;
            return total / count;
        }

        /// <summary>What a colour becomes once the state tint has multiplied into it.</summary>
        private static Color Multiply(Color face, Color tint)
        {
            return new Color(face.r * tint.r, face.g * tint.g, face.b * tint.b, 1f);
        }

        private static Color Over(Color front)
        {
            if (front.a >= 0.999f)
                return front;
            Color backdrop = new Color32(0x12, 0x12, 0x2A, 0xFF);
            return front * front.a + backdrop * (1f - front.a);
        }

        private static float OverContrast(Color front, Color backdrop)
        {
            return Contrast(Over(front), Over(backdrop));
        }

        private static float Contrast(Color one, Color two)
        {
            float a = Luminance(one);
            float b = Luminance(two);
            float lighter = Mathf.Max(a, b);
            float darker = Mathf.Min(a, b);
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        private static float Luminance(Color color)
        {
            return 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) +
                   0.0722f * Linear(color.b);
        }

        private static float Linear(float channel)
        {
            return channel <= 0.03928f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
