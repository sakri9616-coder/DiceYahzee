using System;
using System.Collections;
using System.Collections.Generic;
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
    /// The battle screen checked by measurement rather than by eye.
    ///
    /// Nobody on this project can open the Game view, so every visual promise the screen makes
    /// has to be a number somebody can assert: the rows must not sit on top of each other, they
    /// must fit the canvas the screen was designed against, a throw must finish by itself and
    /// land on the value the rules decided, and an impact must leave the screen exactly where it
    /// found it. A shake that never settles or a die stuck mid-tumble are invisible failures -
    /// they only show up as a game that looks broken, which is the one thing this run cannot
    /// catch by looking.
    ///
    /// The bounds are stated against the design canvas (1280 x 720), not against whatever aspect
    /// the batch runner happens to use, so the result does not move when the test machine does.
    /// </summary>
    public sealed class BattleScreenTests
    {
        /// <summary>The canvas the screen is drawn against - <see cref="GameFlow"/> constants.</summary>
        private const float DesignHalfWidth = 640f;
        private const float DesignHalfHeight = 360f;

        /// <summary>Longest a throw can take: stagger for the last die plus the spin.</summary>
        private const float RollIsOverBy = 1.2f;

        /// <summary>Longest any impact effect can run: a floater is the slowest of them.</summary>
        private const float ImpactIsOverBy = 1.5f;

        private GameFlow _flow;
        private MatchEngine _engine;

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

        // ------------------------------------------------------------------ layout
        [UnityTest]
        public IEnumerator BattleScreen_StacksItsRowsWithoutOverlap()
        {
            StartDuel();
            yield return null;

            // One entry per horizontal band, top to bottom. Elements that deliberately sit on
            // top of another - the health flashes over their bars, the round label inside the top
            // bar - belong to the band they cover rather than to a band of their own.
            var bands = new[]
            {
                new[] { "TopBar", "Round", "Brand", "BattleRules" },
                new[] { "NpcName", "NpcHp" },
                new[] { "NpcBar" },
                new[] { "NpcDie0", "NpcDie4" },
                new[] { "Banner" },
                new[] { "PlayerDie0", "PlayerDie4" },
                new[] { "Yaku0", "Yaku1", "Yaku2", "Yaku3", "Yaku4", "Yaku5", "Yaku6" },
                new[] { "Info" },
                new[] { "PlayerName", "PlayerHp" },
                new[] { "PlayerBar" },
                new[] { "Hint" },
                new[] { "Reroll", "Commit", "Advance" },
                new[] { "PhaseNote" }
            };

            var union = new Rect[bands.Length];
            for (int i = 0; i < bands.Length; i++)
            {
                union[i] = Union(bands[i]);
                Assert.That(union[i].height, Is.GreaterThan(0f),
                    "band " + string.Join("/", bands[i]) + " is missing from the screen");
            }

            for (int i = 0; i + 1 < union.Length; i++)
            {
                Assert.That(union[i + 1].yMax, Is.LessThanOrEqualTo(union[i].yMin),
                    string.Join("/", bands[i]) + " at " + union[i] + " sits on top of " +
                    string.Join("/", bands[i + 1]) + " at " + union[i + 1]);
            }
        }

        [UnityTest]
        public IEnumerator BattleScreen_KeepsEveryElementOnTheDesignCanvas()
        {
            StartDuel();
            yield return null;

            // A name half off the left edge is a bug nobody can see until it ships, and the
            // screen is full of absolutely positioned text boxes that make it easy to write.
            foreach (string name in new[]
                     {
                         "Round", "Brand", "BattleRules",
                         "NpcName", "NpcHp", "NpcBar",
                         "Banner", "Info", "Hint", "PhaseNote",
                         "PlayerName", "PlayerHp", "PlayerBar",
                         "Reroll", "Commit", "Advance",
                         "NpcDie0", "PlayerDie0"
                     })
            {
                Rect rect = RectOf(name);
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(-DesignHalfWidth),
                    name + " starts " + rect.xMin + ", past the left edge");
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(DesignHalfWidth),
                    name + " ends " + rect.xMax + ", past the right edge");
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(-DesignHalfHeight),
                    name + " drops below the bottom edge");
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(DesignHalfHeight),
                    name + " rises above the top edge");
            }

            // The top bar runs the other way: it is a full-bleed band, wider than the screen on
            // purpose so it still reaches both edges when the window is stretched. What it must
            // never be is narrower than the canvas it covers.
            Rect band = RectOf("TopBar");
            Assert.That(band.xMin, Is.LessThanOrEqualTo(-DesignHalfWidth),
                "the top bar starts " + band.xMin + " and would leave a gap at the left edge");
            Assert.That(band.xMax, Is.GreaterThanOrEqualTo(DesignHalfWidth),
                "the top bar ends " + band.xMax + " and would leave a gap at the right edge");

            // The yaku row is the one element whose width depends on how many yaku this hand
            // holds, so it is measured after the layout rather than at build time.
            Rect row = YakuRowBounds();
            Assert.That(row.xMin, Is.GreaterThanOrEqualTo(-DesignHalfWidth),
                "the yaku row starts " + row.xMin);
            Assert.That(row.xMax, Is.LessThanOrEqualTo(DesignHalfWidth),
                "the yaku row ends " + row.xMax);
        }

        // -------------------------------------------------------------------- throw
        [UnityTest]
        public IEnumerator Throw_FinishesByItselfAndLandsOnTheEngineValue()
        {
            StartDuel();
            yield return null;

            Assert.That(DiceAreRolling(), Is.True,
                "the opening throw must start, or the screen is showing a settled hand it never animated");

            yield return new WaitForSeconds(RollIsOverBy);

            Assert.That(DiceAreRolling(), Is.False,
                "the throw must settle on its own - nothing in the screen is ever asked to finish it");
            Assert.That(DiceMatchEngine(), Is.True,
                "the animation must land on the value the rules decided, not on its own dice");
        }

        // ------------------------------------------------------------------ impact
        [UnityTest]
        public IEnumerator Impact_LeavesTheScreenExactlyWhereItFoundIt()
        {
            StartDuel();
            yield return new WaitForSeconds(RollIsOverBy);

            Press("_commitButton");

            Assert.That(FloaterCount(), Is.GreaterThan(0),
                "a committed round must put its damage on the screen");

            yield return new WaitForSeconds(ImpactIsOverBy);

            Assert.That(BattleRoot().anchoredPosition, Is.EqualTo(Vector2.zero),
                "the shake has to return the screen to rest, or every later round drifts");
            Assert.That(FloaterCount(), Is.EqualTo(0),
                "damage numbers must remove themselves, or they pile up over a whole match");
            Assert.That(FlashAlpha("NpcFlash"), Is.EqualTo(0f).Within(0.001f),
                "the struck bar must end unflashed");
            Assert.That(FlashAlpha("PlayerFlash"), Is.EqualTo(0f).Within(0.001f),
                "the struck bar must end unflashed");
            Assert.That(BannerPunchScale(), Is.EqualTo(1f).Within(0.001f),
                "the banner must settle back to its normal size");
        }

        // ------------------------------------------------------------------- hints
        [UnityTest]
        public IEnumerator NearMissLine_MatchesWhatTheRulesSayAboutThisHand()
        {
            StartDuel();
            yield return null;

            bool rerollsLeft = _engine.Phase == MatchPhase.AwaitingReroll &&
                               _engine.PlayerRerollsLeft > 0;
            YakuNearMiss[] misses = rerollsLeft
                ? Yaku.NearMisses(_engine.PlayerDice, _engine.Config)
                : new YakuNearMiss[0];

            string shown = TextOf("Hint");
            if (misses.Length == 0 || !rerollsLeft)
            {
                // Whatever else it may say, it may not promise a throw the player cannot make.
                Assert.That(shown, Does.Not.Contain("あと1回で"),
                    "the screen is offering a throw that is not on the table");
            }
            else
            {
                Assert.That(shown, Does.Contain("あと1回で"), "the offer must be stated");
                Assert.That(shown, Does.Contain(misses[0].DisplayName()),
                    "the offer must name the yaku the rules actually found");
            }
        }

        // ------------------------------------------------------------------- reroll
        /// <summary>
        /// The dice the player marks are the dice that get thrown. This is the one thing on this
        /// screen that a test can catch and an eye cannot be trusted with: the values land
        /// correctly either way, so an inverted mask still produces a legal hand - it just rolls
        /// the five dice the player decided to keep while the marked ones change face in silence.
        /// The player reads that as "it rerolled everything except what I chose".
        /// </summary>
        [UnityTest]
        public IEnumerator Reroll_ThrowsExactlyTheDiceThePlayerMarked()
        {
            StartDuel();
            yield return new WaitForSeconds(RollIsOverBy);   // the opening throw has settled

            Method("OnPlayerDieClicked").Invoke(_flow, new object[] { 0 });
            Method("OnPlayerDieClicked").Invoke(_flow, new object[] { 2 });
            Press("_rerollButton");

            Assert.That(ThrowingDice(), Is.EqualTo(new[] { 0, 2 }),
                "the throw must move the marked dice and only those - " +
                "the kept dice have to sit still, because stillness is what says 'this one survived'");
        }

        // ------------------------------------------------------------------ labels
        /// <summary>
        /// Every yaku name must print on one line inside its button. The names are Yahtzee's own
        /// (ヤッツィー, スリーカインド, 小ストレート...), which is the whole point of them - but the
        /// longest of them is seven full-width glyphs against a 140px button, and a name that
        /// wraps to two lines pushes the attack and ward values off the button.
        /// </summary>
        [UnityTest]
        public IEnumerator YakuButtons_PrintTheirNamesOnOneLine()
        {
            StartDuel();
            yield return null;

            int checkedButtons = 0;
            for (int i = 0; i < 7; i++)
            {
                RectTransform rect = Find(BattleRoot(), "Yaku" + i);
                if (rect == null || !rect.gameObject.activeSelf)
                    continue;

                Text caption = rect.GetComponentInChildren<Text>();
                Assert.That(caption, Is.Not.Null, "Yaku" + i + " has no label");

                // Measured with wrapping switched off. With Wrap on, preferredWidth is measured
                // against the box it is about to fail to fit, so the check would agree with the
                // very bug it exists to catch.
                HorizontalWrapMode wrap = caption.horizontalOverflow;
                caption.horizontalOverflow = HorizontalWrapMode.Overflow;
                float widest = caption.preferredWidth;
                caption.horizontalOverflow = wrap;
                checkedButtons++;

                Assert.That(widest, Is.LessThanOrEqualTo(rect.sizeDelta.x),
                    "Yaku" + i + " needs " + widest.ToString("0.0") + "px for '" + caption.text +
                    "' but the button is " + rect.sizeDelta.x + "px wide");
            }

            Assert.That(checkedButtons, Is.GreaterThan(0),
                "no yaku button is showing on a hand that must offer one");
        }

        // ----------------------------------------------------------------- helpers
        private void StartDuel()
        {
            PlayerPrefs.SetInt("pv.audioStep", 3);
            PlayerPrefs.Save();
            InputLog.Enabled = true;

            _flow = UnityEngine.Object.FindFirstObjectByType<GameFlow>();
            if (_flow == null)
            {
                var host = new GameObject("GameFlowUnderTest");
                _flow = host.AddComponent<GameFlow>();
            }

            GameFlow.ConfigOverride = null;
            Method("StartMatch").Invoke(_flow, null);
            _engine = Field("_engine").GetValue(_flow) as MatchEngine;
            Assert.That(_engine, Is.Not.Null, "the duel never started");
        }

        private void Press(string fieldName)
        {
            var button = Field(fieldName).GetValue(_flow) as Button;
            Assert.That(button, Is.Not.Null, fieldName + " is missing");
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True,
                fieldName + " was not clickable when the round needed it");
            button.onClick.Invoke();
        }

        private RectTransform BattleRoot()
        {
            return Field("_battleRoot").GetValue(_flow) as RectTransform;
        }

        private Rect Union(string[] names)
        {
            Rect result = new Rect(0f, 0f, 0f, 0f);
            bool any = false;
            for (int i = 0; i < names.Length; i++)
            {
                Rect rect = RectOf(names[i]);
                if (rect.width <= 0f && rect.height <= 0f)
                    continue;

                result = any ? Union(result, rect) : rect;
                any = true;
            }

            return any ? result : new Rect(0f, 0f, 0f, 0f);
        }

        private static Rect Union(Rect a, Rect b)
        {
            float minX = Mathf.Min(a.xMin, b.xMin);
            float minY = Mathf.Min(a.yMin, b.yMin);
            float maxX = Mathf.Max(a.xMax, b.xMax);
            float maxY = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private Rect YakuRowBounds()
        {
            var found = false;
            Rect result = new Rect(0f, 0f, 0f, 0f);
            for (int i = 0; i < 7; i++)
            {
                RectTransform rect = Find(BattleRoot(), "Yaku" + i);
                if (rect == null || !rect.gameObject.activeSelf)
                    continue;

                Rect box = BoxOf(rect);
                result = found ? Union(result, box) : box;
                found = true;
            }

            Assert.That(found, Is.True, "no yaku button is showing on a hand that must offer one");
            return result;
        }

        /// <summary>The box of an element, measured in the canvas space every child is laid out in.</summary>
        private Rect RectOf(string name)
        {
            return BoxOf(TransformOf(name));
        }

        private RectTransform TransformOf(string name)
        {
            RectTransform rect = Find(BattleRoot(), name);
            Assert.That(rect, Is.Not.Null, name + " is missing from the battle screen");
            return rect;
        }

        private static Rect BoxOf(RectTransform rect)
        {
            // Every element on this screen is built centred with its pivot in the middle, so its
            // offset from the (full-canvas) parent centre and its size give the box directly.
            Vector2 p = rect.anchoredPosition;
            Vector2 s = rect.sizeDelta;
            return new Rect(p.x - s.x * 0.5f, p.y - s.y * 0.5f, s.x, s.y);
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

        private string TextOf(string name)
        {
            Text label = TransformOf(name).GetComponent<Text>();
            Assert.That(label, Is.Not.Null, name + " is not a text element");
            return label.text;
        }

        private float BannerPunchScale()
        {
            var banner = Field("_bannerText").GetValue(_flow) as Text;
            return banner.rectTransform.localScale.x;
        }

        private float FlashAlpha(string name)
        {
            Image flash = TransformOf(name).GetComponent<Image>();
            Assert.That(flash, Is.Not.Null, name + " is not an image");
            return flash.color.a;
        }

        private int FloaterCount()
        {
            RectTransform root = BattleRoot();
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name.StartsWith("Float", StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        /// <summary>True while any die on either row is still choosing a face.</summary>
        private bool DiceAreRolling()
        {
            foreach (string listName in new[] { "_playerDice", "_npcDice" })
            {
                var list = Field(listName).GetValue(_flow) as System.Collections.IList;
                if (list == null)
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    object view = list[i];
                    var rolling = view.GetType().GetProperty("IsRolling");
                    if (rolling != null && (bool)rolling.GetValue(view, null))
                        return true;
                }
            }

            return false;
        }

        /// <summary>The player dice currently tumbling, in row order.</summary>
        private List<int> ThrowingDice()
        {
            var rolling = new List<int>();
            var list = Field("_playerDice").GetValue(_flow) as System.Collections.IList;
            if (list == null)
                return rolling;

            for (int i = 0; i < list.Count; i++)
            {
                object view = list[i];
                var isRolling = view.GetType().GetProperty("IsRolling");
                if (isRolling != null && (bool)isRolling.GetValue(view, null))
                    rolling.Add(i);
            }

            return rolling;
        }

        /// <summary>True when every player die shows exactly what the engine rolled.</summary>
        private bool DiceMatchEngine()
        {
            var list = Field("_playerDice").GetValue(_flow) as System.Collections.IList;
            if (list == null || list.Count != _engine.PlayerDice.Length)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                object view = list[i];
                var value = view.GetType().GetProperty("Value");
                if (value == null || (int)value.GetValue(view, null) != _engine.PlayerDice[i])
                    return false;
            }

            return true;
        }

        private FieldInfo Field(string name)
        {
            return typeof(GameFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private MethodInfo Method(string name)
        {
            return typeof(GameFlow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
