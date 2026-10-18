using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private RectTransform _sdCarryRack;
        private CanvasGroup _sdCarryRackGroup;
        private readonly DiceView[] _sdCarryDice = new DiceView[5];
        private ScoreHandEffectGraphic _sdCarryEffect;
        private float _sdCarryReturnAge = -1;
        private float _sdCarryToggleAge = -1;
        private const float SDCarryToggleDuration = .56f;

        private void SDBuildCarryPresentation()
        {
            // The once-per-match choice sits on the central action axis between score and HP.
            var buttonRect = (RectTransform)_sdSaveButton.transform;
            // The 36px player HP bar spans up to y=-260; sitting at -230 leaves the 48px
            // carry button a clear ~6px gap above it instead of a 1px overlap.
            buttonRect.anchoredPosition = new Vector2(0, -230);
            buttonRect.sizeDelta = new Vector2(204, 48);
            // The shared button factory centers its floppy icon; this partial only owns
            // the button rectangle and the rack below it.
            _sdCarryRack = UiFactory.CreateRect("CarryRack", _sdSaveButton.transform,
                new Vector2(160, 20), new Vector2(0, -13));
            _sdCarryRackGroup = _sdCarryRack.gameObject.AddComponent<CanvasGroup>();
            _sdCarryRackGroup.blocksRaycasts = false;
            // A deeper tray than the old 0.22 wash: the rack's own gold marks (the keep ring on
            // every die, and the standing frame the armed state draws around the row) are gold on a
            // gold button face, close enough in luminance to vanish there. Setting the row into a
            // shallow dark bed is what lets ivory faces and gold rings stay apart from the brass.
            var bed = UiFactory.CreatePanel("RackBed", _sdCarryRack, new Vector2(160, 20), Vector2.zero,
                new Color(0.06f, .11f, .14f, .5f));
            bed.raycastTarget = false;
            for (int i = 0; i < 5; i++)
            {
                _sdCarryDice[i] = new DiceView(_sdCarryRack, i, 19, "CarryDie" + i, new Color32(255, 249, 232, 255));
                _sdCarryDice[i].SetPosition(new Vector2((i - 2) * 27, 0));
                _sdCarryDice[i].SetInteractable(false);
                foreach (Graphic graphic in _sdCarryRack.Find("CarryDie" + i).GetComponentsInChildren<Graphic>())
                    graphic.raycastTarget = false;
            }
            var effectRoot = UiFactory.CreateStretch("CarryEffect", _sdBattle);
            _sdCarryEffect = effectRoot.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _sdCarryEffect.raycastTarget = false;
            // Everything the HUD builds after this point is a later sibling and draws over the
            // stroke: the dice row and the role panel with its score, the HP bars and the labels
            // that get raised last, the hold locks, the 1100x500 impact geometry and the incoming
            // damage line, both of which span the carry button's band. The toggle is a transient
            // stroke over a button the player is already looking at, so it is raised once here
            // rather than racing the rest of the build for depth.
            effectRoot.SetAsLastSibling();
            _sdCarryRack.gameObject.SetActive(false);
        }

        private void SDRefreshCarryPresentation()
        {
            if (_sdCarryRack == null || _sdEngine == null) return;
            bool stored = _sdCommitted && _sdEngine.LastRound != null && _sdEngine.LastRound.IsSaved;
            bool returning = _sdCarryReturnAge >= 0;
            bool visible = (_sdSaveArmed && !_sdRolling) || stored || returning;
            _sdCarryRack.gameObject.SetActive(visible);
            if (visible)
            {
                var faces = stored ? _sdEngine.LastRound.PlayerDice : _sdEngine.PlayerDice;
                for (int i = 0; i < 5; i++)
                {
                    _sdCarryDice[i].SetValue(faces[i]);
                    // The rule carries the whole hand - every die and every hold goes to the next
                    // turn - so ringing only the engine's held dice would promise a smaller
                    // selection than the save actually makes. The engine's own holds still decide
                    // what next turn rerolls; on this rack, all five read as kept.
                    _sdCarryDice[i].SetTint(DiceTint.Plain, true);
                }
            }
            var face = _sdSaveButton.GetComponent<Image>();
            var palette = _sdSaveButton.GetComponent<ButtonPalette>();
            Color fill = _sdSaveArmed ? new Color32(237, 194, 105, 255) : DiceYahzeeVisuals.Brass;
            if (!_sdSaveButton.interactable) fill = new Color32(92, 100, 100, 255);
            face.color = fill;
            palette.Face = fill;
            Color ink = _sdSaveButton.interactable ? SDPaperInk : SDMuted;
            var label = _sdSaveButton.GetComponentInChildren<Text>();
            label.color = ink;
            label.text = "";
            _sdSaveSymbol.color = ink;
            _sdSaveButton.transform.Find("SaveCount").GetComponent<Text>().color = ink;
            if (!returning && _sdCarryToggleAge < 0) SDDrawCarrySelection();
        }

        /// <summary>
        /// A short pulse on the carry action so arming and disarming read as a deliberate state
        /// change rather than an instant colour swap. Gold marks "on", muted marks "off".
        /// </summary>
        private void SDBeginCarryToggle()
        {
            _sdCarryToggleAge = _sdReducedMotion ? SDCarryToggleDuration : 0;
            SDTickCarryToggle(0);
        }

        private void SDTickCarryToggle(float dt)
        {
            if (_sdCarryToggleAge < 0 || SDModalOpen || _sdCarryEffect == null) return;
            _sdCarryToggleAge += dt;
            float t = Mathf.Clamp01(_sdCarryToggleAge / SDCarryToggleDuration);
            float ease = 1 - (1 - t) * (1 - t);
            if (!_sdReducedMotion)
            {
                _sdCarryEffect.Clear();
                Vector2 center = _sdBattle.InverseTransformPoint(_sdSaveButton.transform.position);
                Color ink = _sdSaveArmed ? SDPositive : SDRailMuted;
                ink.a = (1 - t) * .85f;
                _sdCarryEffect.Ellipse(center, 104 + ease * 12, 25 + ease * 3, 2.6f, ink, 32);
                if (_sdSaveArmed)
                {
                    // A bounded set of sparks travels from the hand into the stored-hand rack.
                    for (int i = 0; i < 20; i++)
                    {
                        float travel = Mathf.Clamp01((t - (i % 4) * .045f) / .82f);
                        float arc = (i % 4) * Mathf.PI * .5f + .4f;
                        Vector2 source = new Vector2(SDHandCenterX + (i % 5 - 2) * 132, SDPlayerDiceY) +
                            new Vector2(Mathf.Cos(arc), Mathf.Sin(arc)) * 62;
                        Vector2 destination = center + new Vector2((i % 5 - 2) * 27, -15);
                        Vector2 head = Vector2.Lerp(source, destination, travel * travel);
                        head.y += Mathf.Sin(travel * Mathf.PI) * (18 + i % 3 * 8);
                        Vector2 direction = (destination - source).normalized;
                        Color spark = SDPositive;
                        spark.a = Mathf.Sin(travel * Mathf.PI) * .95f;
                        _sdCarryEffect.Line(head - direction * (5 + 10 * travel), head, 3.5f, spark);
                        _sdCarryEffect.Line(head + Vector2.left * 3, head + Vector2.right * 3, 2, spark);
                    }
                }
                _sdCarryEffect.Flush();
                if (_sdCarryRack != null)
                {
                    _sdCarryRack.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * .08f);
                    // The online table's declaration tell carried over: the racked hand shivers on
                    // the same two axes while the carry is being taken. The landing below writes
                    // the exact resting positions back, so nothing can drift.
                    for (int i = 0; i < 5; i++)
                    {
                        var die = (RectTransform)_sdCarryRack.Find("CarryDie" + i);
                        die.anchoredPosition = new Vector2((i - 2) * 27, 0) + SDChargeOffset(_sdCarryToggleAge, i, 1 - t);
                    }
                }
            }
            if (t < 1) return;
            _sdCarryToggleAge = -1;
            if (_sdCarryRack != null)
            {
                _sdCarryRack.localScale = Vector3.one;
                for (int i = 0; i < 5; i++)
                    ((RectTransform)_sdCarryRack.Find("CarryDie" + i)).anchoredPosition = new Vector2((i - 2) * 27, 0);
            }
            SDDrawCarrySelection();
        }

        private void SDDrawCarrySelection()
        {
            if (_sdCarryEffect == null) return;
            // The toggle owns the stroke while it plays: this box is repainted on every HUD
            // refresh, and one landing mid-animation would paste itself over the flying sparks.
            // The toggle draws the box itself the moment it lands, so nothing is lost by waiting.
            if (_sdCarryToggleAge >= 0) return;
            _sdCarryEffect.Clear();
            if (_sdSaveArmed && !_sdCommitted && !_sdRolling && !SDModalOpen)
            {
                Color ink = SDPositive; ink.a = .72f;
                float left = -333, right = 333, top = 38, bottom = -92;
                float length = 21;
                _sdCarryEffect.Line(new Vector2(left, top - length), new Vector2(left, top), 2.4f, ink);
                _sdCarryEffect.Line(new Vector2(left, top), new Vector2(left + length, top), 2.4f, ink);
                _sdCarryEffect.Line(new Vector2(right - length, top), new Vector2(right, top), 2.4f, ink);
                _sdCarryEffect.Line(new Vector2(right, top), new Vector2(right, top - length), 2.4f, ink);
                _sdCarryEffect.Line(new Vector2(left, bottom + length), new Vector2(left, bottom), 2.4f, ink);
                _sdCarryEffect.Line(new Vector2(left, bottom), new Vector2(left + length, bottom), 2.4f, ink);
                _sdCarryEffect.Line(new Vector2(right - length, bottom), new Vector2(right, bottom), 2.4f, ink);
                _sdCarryEffect.Line(new Vector2(right, bottom), new Vector2(right, bottom + length), 2.4f, ink);
                // The rack states the armed hand on its own, because the toggle that would say it
                // with sparks is skipped entirely under reduced motion. A gold capsule around the
                // whole row (not a die at a time) is the same sentence the sparks deliver: these
                // five travel, all of them. The per-die keeps come from the slab rings, which at
                // this size separate cleanly above their 27-unit pitch.
                if (_sdCarryRack != null && _sdCarryRack.gameObject.activeSelf)
                {
                    Color keep = UiFactory.Gold; keep.a = .85f;
                    Vector2 rack = _sdBattle.InverseTransformPoint(_sdCarryRack.position);
                    Vector2 half = _sdCarryRack.rect.size * .5f;
                    _sdCarryEffect.Ellipse(rack, half.x + 8, half.y + 7, 2.4f, keep, 28);
                }
            }
            _sdCarryEffect.Flush();
        }

        private void SDBeginCarryReturn()
        {
            _sdCarryReturnAge = _sdReducedMotion ? .35f : 0;
            _sdCarryRackGroup.alpha = 1;
            SDRefreshCarryPresentation();
            for (int i = 0; i < 5; i++) if (_sdEngine.Held[i] && !_sdReducedMotion) _sdDice[i].Punch();
        }

        private void SDTickCarryReturn(float dt)
        {
            if (_sdCarryReturnAge < 0 || SDModalOpen) return;
            _sdCarryReturnAge += dt;
            float t = Mathf.Clamp01(_sdCarryReturnAge / .35f);
            _sdCarryRackGroup.alpha = 1 - t;
            _sdCarryEffect.Clear();
            if (!_sdReducedMotion)
            {
                Color ink = SDPositive; ink.a = (1 - t) * .55f;
                for (int i = 0; i < 5; i++)
                {
                    var source = _sdBattle.InverseTransformPoint(_sdCarryRack.Find("CarryDie" + i).position);
                    Vector2 destination = new Vector2(SDHandCenterX + (i - 2) * 132, SDPlayerDiceY);
                    Vector2 head = Vector2.Lerp(source, destination, Mathf.SmoothStep(0, 1, t));
                    _sdCarryEffect.Line(Vector2.Lerp(source, head, .82f), head, 3, ink);
                    _sdCarryEffect.Ring(head, 5 + t * 7, 2, ink, 16);
                }
            }
            _sdCarryEffect.Flush();
            if (t < 1) return;
            _sdCarryReturnAge = -1;
            _sdCarryRackGroup.alpha = 1;
            _sdCarryRack.gameObject.SetActive(false);
            SDDrawCarrySelection();
        }

        private void SDResetCarryPresentation()
        {
            _sdCarryReturnAge = -1;
            _sdCarryToggleAge = -1;
            if (_sdCarryRack != null) _sdCarryRack.localScale = Vector3.one;
            if (_sdCarryRackGroup != null) _sdCarryRackGroup.alpha = 1;
            if (_sdCarryRack != null) _sdCarryRack.gameObject.SetActive(false);
            if (_sdCarryEffect != null) { _sdCarryEffect.Clear(); _sdCarryEffect.Flush(); }
        }
    }
}
