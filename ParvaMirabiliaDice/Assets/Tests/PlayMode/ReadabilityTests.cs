using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ParvaMirabilia.Tests.PlayMode
{
    /// <summary>
    /// Whether the buttons can be read, measured instead of guessed.
    ///
    /// The screenshot pass found the main action rendered as a dark label on a dark face - the
    /// pairing measured about 1.3:1, which means the words were there and nobody could see them.
    /// Nothing else in this suite could have caught that: a label can be present, positioned,
    /// non-empty and correctly spelled while being invisible, so the pairing itself is asserted
    /// here using the contrast formula the web uses for the same question (WCAG 2.1), which asks
    /// how much lighter the text is than what it sits on.
    ///
    /// 4.5:1 is the level a reader with normal vision needs for text this size; the enabled and
    /// the greyed-out state are both held to it, because a disabled action is still an action the
    /// player has to be able to name.
    /// </summary>
    public sealed class ReadabilityTests
    {
        private const float ReadableAt = 4.5f;

        /// <summary>The screen behind every button, used to flatten a translucent face.</summary>
        private static readonly Color Backdrop = new Color32(0x12, 0x12, 0x2A, 0xFF);

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
        public IEnumerator EveryActionButton_StatesItselfInReadableContrast()
        {
            StartDuel();
            yield return null;

            int checkedLabels = 0;
            for (int i = 0; i < 7; i++)
                checkedLabels += AssertReadable("Yaku" + i);

            checkedLabels += AssertReadable("Reroll");
            checkedLabels += AssertReadable("Commit");
            checkedLabels += AssertReadable("Advance");

            // Three yaku is the usual number, four the maximum - anything less than three means
            // the buttons were never looked for under the right name.
            Assert.That(checkedLabels, Is.GreaterThanOrEqualTo(5),
                "only " + checkedLabels + " button labels were found, so the check proved little");
        }

        [UnityTest]
        public IEnumerator GreyingAButtonOut_KeepsItsNameReadable()
        {
            StartDuel();
            yield return null;

            Button commit = ButtonNamed("Commit");
            Type uiFactory = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                uiFactory = assembly.GetType("ParvaMirabilia.Presentation.UiFactory", false);
                if (uiFactory != null)
                    break;
            }
            Assert.That(uiFactory, Is.Not.Null, "UiFactory was not found");

            var greyOut = uiFactory.GetMethod("SetButtonEnabled",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(greyOut, Is.Not.Null, "UiFactory.SetButtonEnabled was not found");

            try
            {
                greyOut.Invoke(null, new object[] { commit, false });
                Assert.That(commit.interactable, Is.False, "the button was not actually greyed out");
                AssertReadable("Commit");

                // ...and back: the state change must not cost the button its identity, which is
                // exactly the regression that blanked the primary action in the first place.
                greyOut.Invoke(null, new object[] { commit, true });
                AssertReadable("Commit");
            }
            finally
            {
                greyOut.Invoke(null, new object[] { commit, true });
            }
        }

        // ------------------------------------------------------------------ helpers
        private int AssertReadable(string name)
        {
            Button button = ButtonNamed(name);
            var face = button.GetComponent<Image>();
            Assert.That(face, Is.Not.Null, name + " has no face to read against");

            int count = 0;
            foreach (var label in button.GetComponentsInChildren<Text>(true))
            {
                if (string.IsNullOrEmpty(label.text) || !label.enabled)
                    continue;
                if (!label.gameObject.activeInHierarchy)
                    continue;

                float ratio = Contrast(Over(label.color), Over(face.color));
                Assert.That(ratio, Is.GreaterThanOrEqualTo(ReadableAt),
                    name + ' ' + label.text + " is " + ratio.ToString("0.00") + ":1 - " +
                    "a label under " + ReadableAt + ":1 is present but not readable");
                count++;
            }

            return count;
        }

        private Button ButtonNamed(string name)
        {
            RectTransform rect = Find(BattleRoot(), name);
            Assert.That(rect, Is.Not.Null, name + " is missing from the battle screen");
            var button = rect.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, name + " is not a button");
            return button;
        }

        private RectTransform BattleRoot()
        {
            var field = typeof(GameFlow).GetField("_battleRoot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var root = field.GetValue(_flow) as RectTransform;
            Assert.That(root, Is.Not.Null, "_battleRoot is missing");
            return root;
        }

        private static RectTransform Find(Transform root, string name)
        {
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

        // ------------------------------------------------------------------ colour
        /// <summary>Flattens a possibly translucent colour onto what the player actually sees.</summary>
        private static Color Over(Color front)
        {
            if (front.a >= 0.999f)
                return front;
            return front * front.a + Backdrop * (1f - front.a);
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
