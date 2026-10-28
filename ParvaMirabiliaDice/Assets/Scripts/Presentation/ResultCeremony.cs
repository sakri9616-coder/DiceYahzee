using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// The end of a match as a moment rather than a form: the verdict slams onto the table, light
    /// opens behind it, and the room answers - confetti for a win, slow ash for a loss, a fountain
    /// of gold for the draw this game calls a miracle. Everything under the verdict then arrives in
    /// turn, so the eye reads verdict, then numbers, then the next choice.
    ///
    /// Only presentation lives here. The verdict's words, colours and every button belong to the
    /// screen that owns them; the ceremony moves them and gives them back at rest. Buttons stay
    /// clickable through the whole entrance - a player who already knows they want a rematch is
    /// never made to wait for the fireworks.
    /// </summary>
    internal sealed class ResultCeremony : MonoBehaviour
    {
        public enum Mood { Victory, Defeat, Miracle, Neutral }

        private const float SlamDuration = .42f;
        private const float StaggerStart = .38f;
        private const float StaggerStep = .075f;
        private const float RiseDuration = .34f;
        private const float RiseDistance = 26f;

        private RectTransform _root;
        private Text _title;
        private RayBurst _rays;
        private UiParticles _paper;
        private UiParticles _sparks;
        private Image _glow;
        private Mood _mood;
        private float _age = -1f;
        private float _emitClock;
        private System.Random _random = new System.Random();
        private readonly List<Entrant> _entrants = new List<Entrant>();
        private readonly List<Counter> _counters = new List<Counter>();

        private struct Entrant
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 Home;
            public float Delay;
        }

        private sealed class Counter
        {
            public Text Label;
            public string Prefix, Suffix;
            public int Target;
            public float Delay;
            public int LastShown = -1;
        }

        public float Age => _age;
        public bool Playing => _age >= 0f;

        public static ResultCeremony Attach(RectTransform root, Text title)
        {
            var ceremony = root.gameObject.AddComponent<ResultCeremony>();
            ceremony.Construct(root, title);
            return ceremony;
        }

        private void Construct(RectTransform root, Text title)
        {
            _root = root;
            _title = title;
            Vector2 titleAt = title != null ? title.rectTransform.anchoredPosition : new Vector2(0, 200);
            // Behind everything the screen owns, in front of any opaque backdrop it starts with.
            int behind = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform rect = root.GetChild(i) as RectTransform;
                if (rect != null && rect.GetComponent<Graphic>() != null &&
                    rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one)
                    behind = i + 1;
                else break;
            }
            RectTransform glowRect = UiFactory.CreateRect("CeremonyGlow", root, new Vector2(900, 420), titleAt);
            _glow = glowRect.gameObject.AddComponent<Image>();
            _glow.sprite = TableAmbience.LampSprite;
            _glow.raycastTarget = false;
            _glow.color = Color.clear;
            glowRect.SetSiblingIndex(behind);
            _rays = RayBurst.Create("CeremonyRays", root, titleAt);
            _rays.transform.SetSiblingIndex(behind + 1);
            _sparks = UiParticles.Create("CeremonySparks", root, 140, true);
            _sparks.transform.SetSiblingIndex(behind + 2);
            // Paper falls in front of the numbers but never over the buttons: it is placed just
            // before the first button the screen owns.
            _paper = UiParticles.Create("CeremonyPaper", root, 220, false);
            int firstButton = root.childCount - 1;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).GetComponent<Button>() != null) { firstButton = i; break; }
            _paper.transform.SetSiblingIndex(Mathf.Max(behind + 3, firstButton));
            Hide();
        }

        /// <summary>Counts a figure up from zero after the given delay, keeping its surrounding words.</summary>
        public void CountUp(Text label, string prefix, int target, string suffix, float delay)
        {
            if (label == null) return;
            _counters.Add(new Counter { Label = label, Prefix = prefix, Suffix = suffix, Target = target, Delay = delay });
        }

        public void Play(Mood mood)
        {
            _mood = mood;
            _age = 0f;
            _emitClock = 0f;
            _random = new System.Random(System.Environment.TickCount);
            _paper.Clear();
            _sparks.Clear();
            CaptureEntrants();
            Color tint = mood == Mood.Victory ? new Color(1f, .84f, .48f, 1f) :
                mood == Mood.Miracle ? new Color(1f, .9f, .42f, 1f) :
                mood == Mood.Defeat ? new Color(.52f, .66f, .74f, 1f) : new Color(.8f, .82f, .8f, 1f);
            _rays.Core = new Color(tint.r, tint.g, tint.b, mood == Mood.Defeat ? .07f : .15f);
            _rays.Rays = mood == Mood.Miracle ? 24 : 16;
            _rays.Width = mood == Mood.Miracle ? .08f : .11f;
            _rays.gameObject.SetActive(mood != Mood.Neutral);
            _glow.gameObject.SetActive(mood != Mood.Neutral);
            if (!Juice.ReducedMotion) Burst(mood);
            // A synthesised sting layered over the recorded win/lose clip.
            if (mood == Mood.Victory || mood == Mood.Miracle) GameAudio.Play("fanfare");
            else if (mood == Mood.Defeat) GameAudio.Play("toll");
            Sample();
        }

        /// <summary>Settles everything at rest: final positions, full alpha, final figures.</summary>
        public void Hide()
        {
            _age = -1f;
            RestoreEntrants();
            _counters.Clear();
            if (_rays != null) _rays.gameObject.SetActive(false);
            if (_glow != null) _glow.color = Color.clear;
            if (_paper != null) _paper.Clear();
            if (_sparks != null) _sparks.Clear();
            if (_title != null)
            {
                _title.rectTransform.localScale = Vector3.one;
                Color c = _title.color; c.a = 1f; _title.color = c;
            }
        }

        private void OnDisable()
        {
            // Leaving the screen mid-entrance must never strand a button invisible or displaced.
            if (_age >= 0f) Hide();
        }

        private void CaptureEntrants()
        {
            RestoreEntrants();
            _entrants.Clear();
            float delay = StaggerStart;
            for (int i = 0; i < _root.childCount; i++)
            {
                var rect = _root.GetChild(i) as RectTransform;
                if (rect == null || !rect.gameObject.activeSelf) continue;
                if (_title != null && rect == _title.rectTransform) continue;
                if (rect == (RectTransform)_rays.transform || rect == (RectTransform)_paper.transform ||
                    rect == (RectTransform)_sparks.transform || rect == _glow.rectTransform) continue;
                if (rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one) continue;  // backdrops stay put
                var group = rect.GetComponent<CanvasGroup>();
                if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
                _entrants.Add(new Entrant { Rect = rect, Group = group, Home = rect.anchoredPosition, Delay = delay });
                delay += StaggerStep;
            }
        }

        private void RestoreEntrants()
        {
            foreach (Entrant entrant in _entrants)
            {
                if (entrant.Rect == null) continue;
                entrant.Rect.anchoredPosition = entrant.Home;
                if (entrant.Group != null) entrant.Group.alpha = 1f;
            }
            _entrants.Clear();
            foreach (Counter counter in _counters)
                if (counter.Label != null) counter.Label.text = counter.Prefix + counter.Target + counter.Suffix;
        }

        private void Update()
        {
            if (_age < 0f) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            _age += dt;
            if (!Juice.ReducedMotion)
            {
                _paper.Step(dt);
                _sparks.Step(dt);
                Emit(dt);
            }
            Sample();
        }

        private void Sample()
        {
            bool reduced = Juice.ReducedMotion;
            // The verdict: arrives oversized, slams down past its size and settles.
            if (_title != null)
            {
                float slam = reduced ? 1f : Mathf.Clamp01(_age / SlamDuration);
                float scale = reduced ? 1f : Mathf.LerpUnclamped(2.3f, 1f, Juice.EaseOutBack(slam, 1.2f));
                _title.rectTransform.localScale = Vector3.one * scale;
                Color c = _title.color;
                c.a = reduced ? 1f : Mathf.Clamp01(_age / .1f);
                _title.color = c;
            }
            // Light opens as the verdict lands and keeps turning slowly while the screen is up.
            float open = reduced ? 1f : Juice.EaseOutCubic(Mathf.Clamp01((_age - .12f) / .5f));
            _rays.Scale = open;
            _rays.Angle = reduced ? 0f : _age * (_mood == Mood.Miracle ? 9f : 5f);
            _rays.Redraw();
            float flare = reduced ? 0f : Mathf.Exp(-Mathf.Max(0f, _age - .3f) * 5f) * Mathf.Clamp01(_age / .3f);
            Color glow = _rays.Core;
            glow.a = _mood == Mood.Neutral ? 0f : .16f * open + .30f * flare;
            _glow.color = glow;
            _glow.rectTransform.localScale = new Vector3(1f + .25f * flare, 1f + .25f * flare, 1f);

            foreach (Entrant entrant in _entrants)
            {
                if (entrant.Rect == null) continue;
                float t = reduced ? 1f : Mathf.Clamp01((_age - entrant.Delay) / RiseDuration);
                float eased = Juice.EaseOutCubic(t);
                entrant.Rect.anchoredPosition = entrant.Home + Vector2.down * (RiseDistance * (1f - eased));
                entrant.Group.alpha = t;
            }
            foreach (Counter counter in _counters)
            {
                if (counter.Label == null) continue;
                float t = reduced ? 1f : Mathf.Clamp01((_age - counter.Delay) / .8f);
                int shown = Mathf.RoundToInt(counter.Target * Juice.EaseOutCubic(t));
                counter.Label.text = counter.Prefix + shown + counter.Suffix;
                // Ticks follow the figure as it climbs; the first sample only sets the baseline.
                if (shown != counter.LastShown && counter.LastShown >= 0 && !reduced) GameAudio.PlayTick(t);
                counter.LastShown = shown;
            }
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private static readonly Color32[] Confetti =
        {
            new Color32(240, 205, 132, 255), new Color32(255, 244, 214, 255), new Color32(104, 196, 176, 255),
            new Color32(232, 120, 106, 255), new Color32(203, 168, 106, 255), new Color32(150, 190, 236, 255)
        };

        private void Burst(Mood mood)
        {
            Vector2 at = _title != null ? _title.rectTransform.anchoredPosition : Vector2.zero;
            if (mood == Mood.Victory || mood == Mood.Miracle)
            {
                // Two cannons below the screen's edges, aimed inwards and up.
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 70; i++)
                        SpawnPaper(new Vector2(side * 660f, -380f),
                            new Vector2(-side * Range(220f, 620f), Range(560f, 1050f)), Range(2.6f, 4.2f));
                // A ring of sparks from the verdict itself.
                for (int i = 0; i < 46; i++)
                {
                    float angle = Range(0f, Mathf.PI * 2f);
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .7f);
                    float speed = Range(260f, 620f);
                    _sparks.Spawn(new UiParticles.Particle
                    {
                        Position = at + dir * 30f, Velocity = dir * speed,
                        Size = Vector2.one * Range(4f, 9f), Life = Range(.6f, 1.2f), Hold = .35f,
                        Drag = 2.6f, Gravity = 120f, Color = new Color32(255, 236, 180, 255)
                    });
                }
            }
            else if (mood == Mood.Defeat)
            {
                for (int i = 0; i < 26; i++) SpawnAsh(Range(-640f, 640f), Range(-300f, 380f));
            }
        }

        private void SpawnPaper(Vector2 from, Vector2 velocity, float life)
        {
            float w = Range(7f, 13f);
            _paper.Spawn(new UiParticles.Particle
            {
                Position = from, Velocity = velocity, Size = new Vector2(w, w * Range(.45f, .7f)),
                Rotation = Range(0f, 360f), Spin = Range(-540f, 540f), Life = life, Hold = .78f,
                Gravity = 620f, Drag = 1.9f, Flutter = Range(30f, 70f), Phase = Range(0f, 6.3f),
                Tumble = Range(6f, 14f), Color = Confetti[_random.Next(Confetti.Length)]
            });
        }

        private void SpawnAsh(float x, float y)
        {
            byte alpha = (byte)_random.Next(60, 140);
            _sparks.Spawn(new UiParticles.Particle
            {
                Position = new Vector2(x, y), Velocity = new Vector2(Range(-8f, 8f), Range(-38f, -18f)),
                Size = Vector2.one * Range(2.5f, 5.5f), Life = Range(4f, 7f), Hold = .7f,
                Flutter = Range(10f, 24f), Phase = Range(0f, 6.3f),
                Color = new Color32(168, 186, 194, alpha)
            });
        }

        /// <summary>The steady weather after the burst: a light drizzle of gold, or ash.</summary>
        private void Emit(float dt)
        {
            if (_mood == Mood.Neutral) return;
            // The celebration rains for a few seconds and then lets the room be quiet again.
            if (_mood != Mood.Defeat && _age > 4.5f) return;
            _emitClock += dt;
            float interval = _mood == Mood.Defeat ? .16f : .05f;
            while (_emitClock >= interval)
            {
                _emitClock -= interval;
                if (_mood == Mood.Defeat) SpawnAsh(Range(-660f, 660f), 390f);
                else
                {
                    SpawnPaper(new Vector2(Range(-640f, 640f), 390f), new Vector2(Range(-30f, 30f), Range(-80f, -20f)), Range(3.5f, 5f));
                    if (_mood == Mood.Miracle && _random.NextDouble() < .6)
                        _sparks.Spawn(new UiParticles.Particle
                        {
                            Position = new Vector2(Range(-120f, 120f), -360f),
                            Velocity = new Vector2(Range(-160f, 160f), Range(520f, 760f)),
                            Size = Vector2.one * Range(3f, 7f), Life = Range(1.2f, 1.8f), Hold = .5f,
                            Gravity = 380f, Drag = .6f, Color = new Color32(255, 222, 120, 255)
                        });
                }
            }
        }
    }
}
