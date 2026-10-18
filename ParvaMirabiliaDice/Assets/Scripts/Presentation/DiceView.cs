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

        /// <summary>How far a die rises when the pointer is on it. Six percent: a lift, not a jump.</summary>
        private const float HoverLift = 1.06f;

        private bool _rolling;
        private float _rollElapsed;
        private float _rollLandAt;
        private float _scrambleClock;
        private float _punchElapsed = -1f;
        private bool _hovered;
        private readonly Vector3 _baseScale;

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
            _rolling = true;
            _rollElapsed = 0f;
            _scrambleClock = 0f;
            _rollLandAt = Mathf.Max(0f, stagger) + Mathf.Max(0f, scramble);
            ShowScrambleFace();
        }

        /// <summary>Advances the throw and the landing punch. Called once a frame by the screen.</summary>
        public void Tick(float deltaTime)
        {
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
                    return;

                _rolling = false;
                Render(Value);
                _punchElapsed = 0f;
                return;
            }

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

        /// <summary>A small pop used for a die being marked, or thrown and settled.</summary>
        public void Punch()
        {
            _punchElapsed = 0f;
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
