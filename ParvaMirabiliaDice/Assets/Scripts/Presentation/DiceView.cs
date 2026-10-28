using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>How a die should be tinted for the current state.</summary>
    internal enum DiceTint
    {
        Plain = 0,
        Attack = 1,
        Guard = 2,
        /// <summary>Marked for the next re-roll, not yet on either side.</summary>
        Reroll = 3
    }

    /// <summary>
    /// One die: a square face with six possible pip layouts. No sprite sheet - the pips are
    /// runtime generated circles placed on a 3x3 grid, so the project needs no image assets.
    /// </summary>
    internal sealed class DiceView
    {
        private static readonly int[][] PipLayout =
        {
            new int[0],                      // 0 - unused
            new[] { 0 },                     // 1
            new[] { 1, 6 },                  // 2
            new[] { 1, 0, 6 },               // 3
            new[] { 1, 2, 5, 6 },            // 4
            new[] { 1, 2, 0, 5, 6 },         // 5
            new[] { 1, 2, 3, 4, 5, 6 }       // 6
        };

        private static readonly Vector2[] PipSlots =
        {
            new Vector2(0.50f, 0.50f),  // 0 centre
            new Vector2(0.25f, 0.75f),  // 1 top left
            new Vector2(0.75f, 0.75f),  // 2 top right
            new Vector2(0.25f, 0.50f),  // 3 middle left
            new Vector2(0.75f, 0.50f),  // 4 middle right
            new Vector2(0.25f, 0.25f),  // 5 bottom left
            new Vector2(0.75f, 0.25f)   // 6 bottom right
        };

        private readonly Image _face;
        private readonly Image _ring;
        private readonly Image _shadow;
        // Captured at the start of each throw rather than at construction: the material pass
        // restyles the shadow after the die is built, and the throw must hand back that one.
        private Vector2 _shadowHome;
        private Color _shadowColor;
        private bool _shadowMoved;
        private readonly Image[] _pips = new Image[7];
        private readonly Text _hiddenLabel;
        private readonly Text _badge;
        private readonly Button _button;
        private readonly float _size;
        private readonly Color _plainFace;

        // ---- motion ---------------------------------------------------------
        // A die that simply changes value is information; a die that tumbles and lands is an
        // event. The throw is the moment the whole round turns on, so it gets the time it needs:
        // faces spin for a beat, then each die lands a little after the one before it, which is
        // what makes five dice read as a throw instead of five numbers being assigned.
        private const float ScrambleInterval = 0.07f;
        private const float PunchDuration = 0.16f;
        private const float PunchPeak = 1.16f;

        // A throw is a body in the air, not a face being redrawn: while it scrambles the die hops
        // twice, tilts and lets its shadow fall away beneath it, and on landing it squashes into
        // the table and springs back. Purely visual - only the die's visible parts move, so the root's
        // place, scale and rotation stay with the screens and presenters that already drive them.
        private const float TumbleHop = .12f;
        private const float TumbleTilt = 16f;
        private const float LandDuration = .30f;
        private float _landElapsed = -1f;
        private RectTransform[] _body;

        /// <summary>How far a die rises when the pointer is on it. Six percent: a lift, not a jump.</summary>
        private const float HoverLift = 1.06f;

        private bool _rolling;
        private float _rollElapsed;
        private float _rollLandAt;
        private float _scrambleClock;
        private float _punchElapsed = -1f;
        private bool _hovered;
        private readonly Vector3 _baseScale;

        // ---- halo -------------------------------------------------------------
        // A soft plate of light behind the die that blooms outward and fades: gold when the die
        // is kept, a dim inward breath when it is let go, and a faint white ring of dust when a
        // throw lands. It is created on first use, sits behind the ring, never takes a click and
        // is inactive at rest; under reduced motion it never shows.
        private Image _halo;
        private float _haloElapsed = -1f, _haloDuration = 1f;
        private float _haloFrom = 1f, _haloTo = 1f, _haloAlpha;
        private Color _haloColor;
        private bool _keepShown;
        private bool _keepChangeOn;
        private int _keepChangeFrame = -1, _punchFrame = -1;

        public int Index { get; private set; }
        public int Value { get; private set; }

        /// <summary>True while this die is still deciding what face it is.</summary>
        public bool IsRolling { get { return _rolling; } }

        public event Action<int> Clicked;

        public DiceView(Transform parent, int index, float size, string name, Color? plainFace = null)
        {
            Index = index;
            _size = size;
            _plainFace = plainFace ?? UiFactory.DieFace;

            // The caller names the die, because the two rows are siblings under the same screen
            // and "Die0" twice in one hierarchy makes every lookup after it a guess.
            var root = UiFactory.CreateRect(name, parent, new Vector2(size, size), Vector2.zero);
            _button = root.gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None;
            int captured = index;
            _button.onClick.AddListener(() =>
            {
                if (Clicked != null)
                    Clicked(captured);
            });

            // The pointer lands on the ring, not on the root, so the probe sits on the root and
            // takes the event as it bubbles up. Without it a die gives no sign that it is there
            // to be picked up until the instant it is clicked, which is no sign at all.
            var probe = root.gameObject.AddComponent<HoverProbe>();
            probe.Owner = this;

            // The ring is its own object rather than a Graphic on the root: every Graphic on an
            // object shares that object's RectTransform, so putting it on the root stretched the
            // die to fill the canvas - all five dice then overlapped and only the last one ever
            // received a click. As a child it is created before the face, so it draws behind it
            // and still gives the outline for free; it is also the die's only raycast target,
            // because the face, pips and label all opt out.
            // A die is an object sitting on a table, not a panel: an offset shadow is the cheapest
            // thing that makes it one, and every dice game worth playing gives its dice one. It
            // carries the ring's silhouette and is nudged down and right, so what shows is the
            // die's own shadow rather than a halo around it.
            float shadowSize = size + 10f;
            var shadowRect = UiFactory.CreateRect("Shadow", root, new Vector2(shadowSize, shadowSize),
                new Vector2(3f, -6f));
            var shadow = shadowRect.gameObject.AddComponent<Image>();
            int shadowSpriteSize = size < 48f ? 116 : Mathf.RoundToInt(shadowSize);
            shadow.sprite = UiFactory.RoundedSprite(shadowSpriteSize, shadowSpriteSize);
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            shadow.raycastTarget = false;
            _shadow = shadow;
            _shadowHome = shadowRect.anchoredPosition;
            _shadowColor = shadow.color;

            // A hand die's ring pads it by 5 units per side; for the 19-unit carry dice that
            // reaches past their 27-unit pitch and fuses the whole rack into one band. Tiny
            // dice pad by 3 units instead, so five keeps read as five separate gold caps.
            float ringPad = size < 32f ? 3f : 5f;
            var ringRect = UiFactory.CreateRect("Ring", root, new Vector2(size + ringPad * 2f, size + ringPad * 2f), Vector2.zero);
            _ring = ringRect.gameObject.AddComponent<Image>();
            _ring.color = new Color(0f, 0f, 0f, 0f);
            _ring.raycastTarget = true;
            _ring.sprite = UiFactory.RoundedSprite(shadowSpriteSize, shadowSpriteSize);

            var faceRect = UiFactory.CreateRect("Face", root, new Vector2(size, size), Vector2.zero);
            _face = faceRect.gameObject.AddComponent<Image>();
            _face.color = _plainFace;
            _face.raycastTarget = false;
            // Rounded like every die in every dice game worth playing: a square die is the tell
            // that the screen was drawn by code rather than designed.
            // Keep the corner proportion at small reveal sizes; a fixed 20-unit radius
            // otherwise turns a 32-unit die into a disc.
            int faceSpriteSize = size < 48f ? 106 : Mathf.RoundToInt(size);
            _face.sprite = UiFactory.RoundedSprite(faceSpriteSize, faceSpriteSize);

            float pipSize = Mathf.Max(size < 48f ? 3f : 8f, size * 0.17f);
            for (int i = 0; i < _pips.Length; i++)
            {
                Vector2 slot = PipSlots[i];
                Vector2 position = new Vector2(
                    (slot.x - 0.5f) * size,
                    (slot.y - 0.5f) * size);

                var pipRect = UiFactory.CreateRect("Pip" + i, faceRect, new Vector2(pipSize, pipSize), position);
                _pips[i] = pipRect.gameObject.AddComponent<Image>();
                _pips[i].sprite = UiFactory.CircleSprite;
                _pips[i].color = UiFactory.Pip;
                _pips[i].raycastTarget = false;
                _pips[i].enabled = false;
            }

            _hiddenLabel = UiFactory.CreateText("Hidden", faceRect, "?", Mathf.RoundToInt(size * 0.5f),
                TextAnchor.MiddleCenter, UiFactory.PanelLight, Vector2.zero, new Vector2(size, size));
            _hiddenLabel.enabled = false;

            // Attack/guard is also spelled out here: red versus blue alone would be unreadable
            // for a colour-blind player, so every tinted die carries its own character.
            //
            // The badge sits under the pip grid rather than at the foot of the die: at -0.4 of the
            // size the glyph landed 1 unit from the edge and read as damage to the die, which the
            // screenshot pass picked out immediately.
            _badge = UiFactory.CreateText("Badge", faceRect, "", Mathf.Max(11, Mathf.RoundToInt(size * 0.18f)),
                TextAnchor.MiddleCenter, UiFactory.Text, new Vector2(0f, -size * 0.34f),
                new Vector2(size, size * 0.24f));
            _badge.enabled = false;

            _baseScale = root.localScale;
            SetHidden();
        }

        // ---------------------------------------------------------------- motion
        /// <summary>
        /// Starts the throw. The face spins immediately and lands after
        /// <paramref name="stagger"/> + <paramref name="scramble"/> seconds, so a row of dice
        /// settles left to right rather than all at once.
        ///
        /// The synchronised first frame matters: the true value arrives with the rules, and a
        /// single frame of it showing before the spin begins would give the whole throw away.
        /// </summary>
        public void BeginRoll(float stagger, float scramble)
        {
            if (!_rolling && _shadow != null)
            {
                _shadowHome = _shadow.rectTransform.anchoredPosition;
                _shadowColor = _shadow.color;
            }
            _rolling = true;
            _rollElapsed = 0f;
            _scrambleClock = 0f;
            _rollLandAt = Mathf.Max(0f, stagger) + Mathf.Max(0f, scramble);
            ShowScrambleFace();
        }

        /// <summary>Advances the throw and the landing punch. Called once a frame by the screen.</summary>
        public void Tick(float deltaTime)
        {
            TickHalo(deltaTime);
            if (_rolling)
            {
                _rollElapsed += deltaTime;
                _scrambleClock += deltaTime;
                if (_scrambleClock >= ScrambleInterval)
                {
                    _scrambleClock = 0f;
                    ShowScrambleFace();
                }

                if (_rollElapsed < _rollLandAt)
                {
                    ApplyTumble();
                    return;
                }

                _rolling = false;
                Render(Value);
                RestTumble();
                _punchElapsed = 0f;
                _landElapsed = Juice.ReducedMotion ? -1f : 0f;
                StartHalo(new Color(1f, .97f, .88f), 1f, 1.3f, .30f, .26f);
                return;
            }

            TickLanding(deltaTime);
            if (_punchElapsed < 0f)
                return;

            _punchElapsed += deltaTime;
            float t = Mathf.Clamp01(_punchElapsed / PunchDuration);
            float eased = 1f - (1f - t) * (1f - t);          // fast out, settles
            transform.localScale = _baseScale * Mathf.Lerp(PunchPeak, 1f, eased);
            if (t >= 1f)
            {
                _punchElapsed = -1f;
                // Hand the scale back to the pointer: a die that was under the cursor when it
                // landed has to come back up, or the lift quietly disappears mid-round.
                ApplyLift();
            }
        }

        /// <summary>
        /// The parts a player sees - ring, face, material side - as opposed to the root and its
        /// shadow. Tumble and squash move only these, so the root's scale, rotation and place stay
        /// owned by the screens and presenters that already drive them.
        /// </summary>
        private RectTransform[] Body
        {
            get
            {
                if (_body != null) return _body;
                var parts = new System.Collections.Generic.List<RectTransform>();
                foreach (Transform child in transform)
                    if (child.name != "Shadow" && child.name != "KeepHalo" && child is RectTransform rect) parts.Add(rect);
                _body = parts.ToArray();
                return _body;
            }
        }

        private void SetBody(Vector3 scale, float tilt)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, tilt);
            foreach (RectTransform part in Body)
            {
                part.localScale = scale;
                part.localRotation = rotation;
            }
        }

        /// <summary>Squash into the table on landing, then a damped spring back to square.</summary>
        private void TickLanding(float deltaTime)
        {
            if (_landElapsed < 0f) return;
            _landElapsed += deltaTime;
            float land = Mathf.Clamp01(_landElapsed / LandDuration);
            float give = 1f - Juice.EaseOutElastic(land);
            SetBody(new Vector3(1f + .13f * give, 1f - .11f * give, 1f), 0f);
            if (land >= 1f)
            {
                _landElapsed = -1f;
                SetBody(Vector3.one, 0f);
            }
        }

        /// <summary>The die in the air: two hops that shrink as it loses height, and a tilt that settles.</summary>
        private void ApplyTumble()
        {
            if (Juice.ReducedMotion) return;
            float progress = _rollLandAt > 0f ? Mathf.Clamp01(_rollElapsed / _rollLandAt) : 1f;
            float decay = Mathf.Pow(1f - progress, .6f);
            float height = Mathf.Abs(Mathf.Sin(progress * Mathf.PI * 2f)) * decay;
            SetBody(Vector3.one * (1f + TumbleHop * height),
                Juice.Wobble(_rollElapsed * 2.2f, Index * 1.7f) * TumbleTilt * decay);
            if (_shadow != null)
            {
                _shadowMoved = true;
                _shadow.rectTransform.anchoredPosition = _shadowHome + new Vector2(5f, -9f) * height;
                Color shade = _shadowColor;
                shade.a *= 1f - .45f * height;
                _shadow.color = shade;
            }
        }

        private void RestTumble()
        {
            _landElapsed = -1f;
            SetBody(Vector3.one, 0f);
            if (_shadow == null || !_shadowMoved) return;
            _shadowMoved = false;
            _shadow.rectTransform.anchoredPosition = _shadowHome;
            _shadow.color = _shadowColor;
        }

        /// <summary>A small pop used for a die being marked, or thrown and settled.</summary>
        public void Punch()
        {
            _punchElapsed = 0f;
            _punchFrame = Time.frameCount;
            if (_keepChangeFrame == _punchFrame) StartKeepHalo();
        }

        /// <summary>
        /// The keep mark and the click's punch arrive in the same frame only when a player toggled
        /// this die; a refresh that merely repaints the ring never blooms.
        /// </summary>
        private void StartKeepHalo()
        {
            _keepChangeFrame = -1;
            if (_keepChangeOn) StartHalo(UiFactory.Gold, 1f, 1.55f, .9f, .36f);
            else StartHalo(UiFactory.GoldDim, 1.2f, .98f, .45f, .2f);
        }

        private void StartHalo(Color color, float from, float to, float alpha, float duration)
        {
            if (Juice.ReducedMotion) { StopHalo(); return; }
            if (_halo == null)
            {
                float pad = _size < 32f ? 3f : 5f;
                var rect = UiFactory.CreateRect("KeepHalo", transform, new Vector2(_size + pad * 2f, _size + pad * 2f), Vector2.zero);
                rect.SetSiblingIndex(Mathf.Min(1, rect.parent.childCount - 1));
                _halo = rect.gameObject.AddComponent<Image>();
                _halo.sprite = _ring.sprite;
                _halo.raycastTarget = false;
            }
            _haloColor = color;
            _haloFrom = from;
            _haloTo = to;
            _haloAlpha = alpha;
            _haloDuration = Mathf.Max(.01f, duration);
            _haloElapsed = 0f;
            _halo.gameObject.SetActive(true);
            TickHalo(0f);
        }

        private void TickHalo(float deltaTime)
        {
            if (_haloElapsed < 0f || _halo == null) return;
            if (Juice.ReducedMotion) { StopHalo(); return; }
            _haloElapsed += deltaTime;
            float t = Mathf.Clamp01(_haloElapsed / _haloDuration);
            _halo.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(_haloFrom, _haloTo, Juice.EaseOutCubic(t));
            Color color = _haloColor;
            color.a = _haloAlpha * (1f - t) * (1f - t);
            _halo.color = color;
            if (t >= 1f) StopHalo();
        }

        private void StopHalo()
        {
            _haloElapsed = -1f;
            if (_halo != null) _halo.gameObject.SetActive(false);
        }

        private void ShowScrambleFace()
        {
            int face = UnityEngine.Random.Range(1, 7);
            if (face == Value)
                face = face % 6 + 1;
            Render(face);
        }

        private Transform transform
        {
            get { return _button.transform; }
        }

        public void SetValue(int value)
        {
            Value = value;
            if (_rolling)
                return;                     // the throw owns the face until it lands

            Render(value);
        }

        private void Render(int value)
        {
            _hiddenLabel.enabled = false;
            int[] layout = PipLayout[Mathf.Clamp(value, 1, 6)];
            for (int i = 0; i < _pips.Length; i++)
            {
                bool on = Array.IndexOf(layout, i) >= 0;
                _pips[i].enabled = on;
                _pips[i].rectTransform.gameObject.SetActive(on);
            }
        }

        /// <summary>Marks the die as not yet revealed (the NPC side before the commit).</summary>
        public void SetHidden()
        {
            Value = 0;
            // Hiding outranks any throw still in flight: a hidden die must not carry a stale
            // spin, and it must not settle at full size a frame later.
            _rolling = false;
            _punchElapsed = -1f;
            _hovered = false;
            transform.localScale = _baseScale;
            RestTumble();
            StopHalo();
            _keepShown = false;
            _keepChangeFrame = -1;

            for (int i = 0; i < _pips.Length; i++)
                _pips[i].enabled = false;
            _hiddenLabel.enabled = true;
            _badge.enabled = false;
            _face.color = _plainFace;
            _ring.color = new Color(0f, 0f, 0f, 0f);
        }

        public void SetTint(DiceTint tint, bool keepHighlight)
        {
            switch (tint)
            {
                case DiceTint.Attack:
                    _face.color = UiFactory.FromHex(0xF7, 0xD6, 0xD4);
                    _ring.color = UiFactory.AttackRed;
                    break;
                case DiceTint.Guard:
                    _face.color = UiFactory.FromHex(0xD6, 0xE6, 0xF7);
                    _ring.color = UiFactory.GuardBlue;
                    break;
                case DiceTint.Reroll:
                    // Deliberately not a side colour: this die is on neither team until the player
                    // puts it somewhere, so it must read as "still undecided".
                    _face.color = UiFactory.FromHex(0xE8, 0xE2, 0xD2);
                    _ring.color = UiFactory.GoldDim;
                    break;
                default:
                    _face.color = _plainFace;
                    _ring.color = keepHighlight ? UiFactory.Gold : new Color(0f, 0f, 0f, 0f);
                    break;
            }

            // The tint must not carry the meaning on its own: the badge repeats it as text, so an
            // attack die and a guard die stay apart for a colour-blind player too.
            if (tint == DiceTint.Attack)
            {
                _badge.text = "攻";
                _badge.color = UiFactory.FromHex(0x9B, 0x1C, 0x1C);
                _badge.enabled = true;
            }
            else if (tint == DiceTint.Guard)
            {
                _badge.text = "守";
                _badge.color = UiFactory.FromHex(0x1C, 0x4E, 0x9B);
                _badge.enabled = true;
            }
            else if (tint == DiceTint.Reroll)
            {
                _badge.text = "振";
                _badge.color = UiFactory.FromHex(0x6B, 0x63, 0x50);
                _badge.enabled = true;
            }
            else
            {
                _badge.enabled = false;
            }

            if (keepHighlight && tint != DiceTint.Plain)
                _ring.color = UiFactory.Gold;

            if (keepHighlight != _keepShown)
            {
                _keepShown = keepHighlight;
                _keepChangeOn = keepHighlight;
                _keepChangeFrame = Time.frameCount;
                if (_punchFrame == _keepChangeFrame) StartKeepHalo();
            }
        }

        public void SetInteractable(bool interactable)
        {
            _button.interactable = interactable;
            // A die that cannot be clicked must not swallow the click either.
            _ring.raycastTarget = interactable;
            if (!interactable && _hovered)
            {
                _hovered = false;
                ApplyLift();
            }
        }

        // ---------------------------------------------------------------- pointer
        /// <summary>
        /// The pointer arrived on the die, or left it.
        ///
        /// A die is the one object on the screen a player reaches for before anything else, and
        /// while it is unmarked it gives no sign that it will respond at all: the ring is clear
        /// and the die is the same colour as every other die. The lift answers "yes, that" before
        /// the click decides anything, and it costs nothing to read for someone who never hovers.
        /// </summary>
        public void SetHovered(bool hovered)
        {
            // A die that cannot be picked up must not pretend it can be.
            if (hovered && !_button.interactable)
                hovered = false;
            if (_hovered == hovered)
                return;
            _hovered = hovered;
            ApplyLift();
        }

        private void ApplyLift()
        {
            // The landing punch outranks the lift: a die in mid-air is already moving, and two
            // scales competing for one transform reads as a stutter.
            float lift = _hovered && _punchElapsed < 0f ? HoverLift : 1f;
            transform.localScale = _baseScale * lift;
        }

        public void SetPosition(Vector2 position)
        {
            var rect = _button.transform as RectTransform;
            if (rect != null)
                rect.anchoredPosition = position;
        }

        /// <summary>
        /// Carries the pointer's arrival to a die that is only a plain object. The ring is the
        /// raycast target and the root is the thing that can be scaled, so the probe lives on the
        /// root and takes the event as it bubbles up from the ring.
        /// </summary>
        private sealed class HoverProbe : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public DiceView Owner;

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (Owner != null)
                    Owner.SetHovered(true);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (Owner != null)
                    Owner.SetHovered(false);
            }
        }
    }
}
