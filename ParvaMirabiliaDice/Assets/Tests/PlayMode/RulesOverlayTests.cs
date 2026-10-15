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
    /// Covers the layout of the rules overlay, which is the first screen a new player opens.
    ///
    /// The overlay used to be laid out by hand: a body box of a fixed height and a close button
    /// parked a fixed distance below it, both numbers chosen to fit the rules as they read at the
    /// time. The rules then grew, and because <c>UiFactory.CreateText</c> deliberately does not
    /// clip vertically, the body kept drawing - through the close button and out past the bottom
    /// of the frame. Nothing objected, because nothing in the layout knew how tall the text was.
    ///
    /// That is the failure these tests exist to prevent, and they check it structurally rather
    /// than by measuring glyphs: the body lives in a slot whose position is derived from the title
    /// above it and the button below it, and the slot is clipped. Font metrics decide how large
    /// the text ends up, never where it is allowed to reach.
    /// </summary>
    public sealed class RulesOverlayTests
    {
        private GameFlow _flow;
        private RectTransform _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Muted: a headless run has no audio listener, and the warnings add nothing here.
            PlayerPrefs.SetInt("pv.audioStep", 3);
            PlayerPrefs.Save();

            var host = new GameObject("RulesOverlayUnderTest");
            _flow = host.AddComponent<GameFlow>();
            yield return null;
            yield return null;

            GameFlow.ConfigOverride = null;
            Method("StartMatch").Invoke(_flow, null);
            yield return null;

            var root = Field("_rulesRoot").GetValue(_flow) as RectTransform;
            Assert.That(root, Is.Not.Null, "GameFlow._rulesRoot is missing");
            _root = root;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameFlow.ConfigOverride = null;
            if (_flow != null)
                Object.Destroy(_flow.gameObject);
            yield return null;
        }

        /// <summary>
        /// The reported bug, checked as geometry: the body must end above the close button, and
        /// every element must sit inside the frame. All of them are centred rects on the canvas,
        /// so their edges are derived from anchoredPosition and sizeDelta alone - the same numbers
        /// the layout writes, which is exactly what went wrong when they were written by hand.
        /// </summary>
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator RulesOverlay_KeepsBodyAboveCloseButtonAndInsideTheFrame()
        {
            Invoke("SetRulesVisible", true);
            Canvas.ForceUpdateCanvases();

            var panel = Child("RulesPanel");
            var title = Child("RulesTitle");
            var slot = Child("RulesBodySlot");
            var bar = Child("RulesScroll");
            var close = Child("CloseRules");

            var p = Edges(panel);
            var t = Edges(title);
            var s = Edges(slot);
            var g = Edges(bar);
            var c = Edges(close);

            // Y grows upward, so "does not overlap" means the body's bottom edge sits above the
            // button's top edge. This is the regression itself, measured on the numbers the
            // layout actually wrote.
            Assert.That(s.bottom, Is.GreaterThan(c.top),
                "the body reaches the close button - the last lines of the rules are unreadable");

            // Title above body, with the gap that separates them. Top-down ordering means no two
            // of the three can be moved independently and end up stacked.
            Assert.That(t.bottom, Is.GreaterThan(s.top),
                "the title overlaps the body");

            // The slot must have real room in it. A slot that collapsed to a sliver would still
            // satisfy every ordering rule above while forcing the best-fit font down to nothing.
            Assert.That(s.top - s.bottom, Is.GreaterThan(0f), "the body slot has no height");
            Assert.That(s.right - s.left, Is.GreaterThan(0f), "the body slot has no width");

            // Everything inside the frame.
            foreach (var (name, e) in new[]
                     {
                         ("RulesTitle", t), ("RulesBodySlot", s), ("RulesScroll", g), ("CloseRules", c)
                     })
            {
                Assert.That(e.left, Is.GreaterThanOrEqualTo(p.left), name + " spills past the frame's left edge");
                Assert.That(e.right, Is.LessThanOrEqualTo(p.right), name + " spills past the frame's right edge");
                Assert.That(e.top, Is.LessThanOrEqualTo(p.top), name + " spills past the frame's top edge");
                Assert.That(e.bottom, Is.GreaterThanOrEqualTo(p.bottom), name + " spills past the frame's bottom edge");
            }

            // ...and the frame itself stays on screen. The canvas is always CanvasHeight units
            // tall, so this is an absolute bound rather than an aspect-ratio-dependent one.
            Assert.That(p.top, Is.LessThanOrEqualTo(360f), "the frame runs off the top of the screen");
            Assert.That(p.bottom, Is.GreaterThanOrEqualTo(-360f), "the frame runs off the bottom of the screen");

            yield return null;
        }

        /// <summary>
        /// The second half of the fix: the slot clips. The rules text no longer fits - it is 858
        /// units of words in a 534 unit slot - so the clip fires on every single run, and what is
        /// inside it has to be scrollable rather than truncated. This pins the structure the
        /// scroll needs: a viewport that masks, a content that is taller than the viewport, and a
        /// text rect that starts at the top of that content. If any one of those is lost, the page
        /// ends early again and the last lines of the rules stop existing.
        /// </summary>
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator RulesOverlay_ClipsTheBodyToItsSlot()
        {
            Invoke("SetRulesVisible", true);
            Canvas.ForceUpdateCanvases();

            var slot = Child("RulesBodySlot");
            var mask = slot.GetComponent<RectMask2D>();
            Assert.That(mask, Is.Not.Null,
                "the body slot must clip: CreateText does not clip vertically, so nothing else stops an over-long rules text");

            var content = slot.Find("RulesContent") as RectTransform;
            Assert.That(content, Is.Not.Null, "the scrolling content must live inside the clipping slot");

            var body = content.Find("RulesBody") as RectTransform;
            Assert.That(body, Is.Not.Null, "the rules text must live inside the scrolling content");
            Assert.That(body.anchoredPosition, Is.EqualTo(Vector2.zero),
                "the text must start at the top of its content, or the scroll ends short of the last line");
            Assert.That(body.rect.width, Is.EqualTo(content.rect.width).Within(0.5f),
                "the text must be as wide as its content, or a line is cut on the side");

            // The content is deliberately no longer the slot's height: it is as tall as the words,
            // and the slot is what those words scroll through.
            Assert.That(content.rect.height, Is.GreaterThan(slot.rect.height),
                "the content is no taller than the viewport, so there is nothing to scroll and the " +
                "page ends where the viewport ends");

            var text = body.GetComponent<Text>();
            Assert.That(text, Is.Not.Null);
            Assert.That(text.resizeTextForBestFit, Is.False,
                "best fit never engaged - a shipped render was still cut mid-page - so the rules " +
                "must not depend on it any more");

            yield return null;
        }

        /// <summary>
        /// The clip test above proves the text cannot escape the frame. This one proves the
        /// opposite and more important thing: that everything the rules say can still be read.
        ///
        /// A real render of the shipped overlay was cut through the middle of a line, and
        /// 【ダメージの計算】【勝敗の決め方】【コツ】 - three of the six sections the rules print -
        /// were never drawn at all. Nothing else in this class could have seen that: the frame was
        /// tidy, the button was clear of the body, and the damage formula simply did not exist.
        /// The failure is a missing thing, not a misplaced one, so this checks reachability.
        /// </summary>
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator RulesOverlay_MakesEverySectionReachable()
        {
            Invoke("SetRulesVisible", true);
            Canvas.ForceUpdateCanvases();

            var slot = Child("RulesBodySlot");
            var content = slot.Find("RulesContent") as RectTransform;
            Assert.That(content, Is.Not.Null);
            var body = content.Find("RulesBody") as RectTransform;
            Assert.That(body, Is.Not.Null);
            var text = body.GetComponent<Text>();
            Assert.That(text, Is.Not.Null);

            foreach (var section in new[] { "【目的】", "【1ラウンドの流れ】", "【役】", "【ダメージの計算】", "【勝敗の決め方】", "【コツ】" })
                Assert.That(text.text, Does.Contain(section), "the rules text no longer prints " + section);

            var scroll = slot.GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null, "the rules body must scroll; clipping on its own hides rules");
            Assert.That(scroll.content, Is.EqualTo(content));
            Assert.That(scroll.viewport, Is.EqualTo(slot));
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.horizontal, Is.False);

            // The bar is part of the contract: an overflow nobody can see is the same as an
            // overflow nobody knows is there.
            var scrollbar = Child("RulesScroll").GetComponent<Scrollbar>();
            Assert.That(scrollbar, Is.Not.Null, "the scroll needs a bar that says there is more");
            Assert.That(scroll.verticalScrollbar, Is.EqualTo(scrollbar),
                "the bar and the scroller disagree, so the bar would lie about the position");

            Canvas.ForceUpdateCanvases();
            // The same measure GameFlow uses to size the content: the larger of Unity's preferred
            // height (wrapping ignored) and the sum of the line heights actually laid out at this
            // width. If the content is ever shorter than this, the last line can never be reached.
            float words = text.preferredHeight;
            if (text.cachedTextGenerator != null && text.cachedTextGenerator.lines.Count > 0)
            {
                float drawn = 0f;
                for (int i = 0; i < text.cachedTextGenerator.lines.Count; i++)
                    drawn += text.cachedTextGenerator.lines[i].height;
                words = Mathf.Max(words, drawn);
            }
            Debug.Log("[RULES-SCROLL] words=" + words + " content=" + content.rect.height +
                      " slot=" + slot.rect.height);
            Assert.That(content.rect.height, Is.GreaterThanOrEqualTo(words - 0.5f),
                "the scrollable region (" + content.rect.height + ") is shorter than the words (" + words +
                "): the end of the rules can never be scrolled into view");

            // Scroll to the end and confirm the last line lands inside the viewport rather than
            // stopping just below it.
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();

            float contentBottom = content.TransformPoint(content.rect.xMin, content.rect.yMin, 0f).y;
            float slotBottom = slot.TransformPoint(slot.rect.xMin, slot.rect.yMin, 0f).y;
            Assert.That(contentBottom, Is.GreaterThanOrEqualTo(slotBottom - 0.5f),
                "scrolling to the end still leaves the last line below the fold (content bottom " +
                contentBottom + ", slot bottom " + slotBottom + ")");
        }

        /// <summary>
        /// Opening and closing is not the bug, but a layout built by deriving one position from
        /// the next is only sound if it survives being shown. This pins the overlay to the real
        /// interaction rather than to whatever state Awake happened to leave it in.
        /// </summary>
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator RulesOverlay_OpensAndClosesOnDemand()
        {
            Assert.That(_root.gameObject.activeSelf, Is.False, "the rules start hidden");

            Invoke("SetRulesVisible", true);
            Assert.That(_root.gameObject.activeSelf, Is.True, "asking for the rules must show them");

            Invoke("SetRulesVisible", false);
            Assert.That(_root.gameObject.activeSelf, Is.False, "closing the rules must hide them");

            yield return null;
        }

        // ---------------------------------------------------------------- helpers
        private static MethodInfo Method(string name)
        {
            var method = typeof(GameFlow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "GameFlow." + name + " is missing");
            return method;
        }

        private void Invoke(string name, params object[] args)
        {
            Method(name).Invoke(_flow, args);
        }

        private static FieldInfo Field(string name)
        {
            var field = typeof(GameFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "GameFlow." + name + " is missing");
            return field;
        }

        private RectTransform Child(string name)
        {
            var found = _root.Find(name) as RectTransform;
            Assert.That(found, Is.Not.Null, "the rules overlay has no '" + name + "'");
            return found;
        }

        /// <summary>Canvas-space edges, from the same numbers the layout writes.</summary>
        private static (float left, float right, float top, float bottom) Edges(RectTransform rt)
        {
            Assert.That(rt.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                rt.name + " must be centred on the canvas for its position to be readable directly");
            Assert.That(rt.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                rt.name + " must pivot on its centre for its position to be readable directly");

            Vector2 size = rt.sizeDelta;
            float x = rt.anchoredPosition.x;
            float y = rt.anchoredPosition.y;
            return (x - size.x / 2f, x + size.x / 2f, y + size.y / 2f, y - size.y / 2f);
        }
    }
}
