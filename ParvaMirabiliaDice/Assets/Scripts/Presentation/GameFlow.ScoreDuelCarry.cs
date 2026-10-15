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
        private const float SDCarryToggleDuration = .34f;

        private void SDBuildCarryPresentation()
        {
            _sdCarryRack = UiFactory.CreateRect("CarryRack", _sdSaveButton.transform,
                new Vector2(150, 22), new Vector2(0, -15));
            _sdCarryRackGroup = _sdCarryRack.gameObject.AddComponent<CanvasGroup>();
            _sdCarryRackGroup.blocksRaycasts = false;
            var bed = UiFactory.CreatePanel("RackBed", _sdCarryRack, new Vector2(150, 22), Vector2.zero,
                new Color(0.08f, .14f, .17f, .22f));
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
                var held = stored ? _sdEngine.LastRound.PlayerHeld : _sdEngine.Held;
                for (int i = 0; i < 5; i++)
                {
                    _sdCarryDice[i].SetValue(faces[i]);
                    _sdCarryDice[i].SetTint(DiceTint.Plain, held[i]);
                }
            }
            var face = _sdSaveButton.GetComponent<Image>();
            var palette = _sdSaveButton.GetComponent<ButtonPalette>();
            Color fill = _sdSaveArmed ? new Color32(237, 194, 105, 255) : DiceYahzeeVisuals.Brass;
            if (!_sdSaveButton.interactable) fill = new Color32(92, 100, 100, 255);
            face.color = fill;
            palette.Face = fill;
            Color ink = _sdSaveButton.interactable ? SDPaperInk : SDMuted;
            _sdSaveButton.GetComponentInChildren<Text>().color = ink;
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
                var center = new Vector2(0, -232);
                Color ink = _sdSaveArmed ? SDPositive : SDRailMuted;
                ink.a = (1 - t) * .85f;
                _sdCarryEffect.Ring(center, 28 + ease * 52, 2.6f, ink, 30);
                _sdCarryEffect.Ring(center, 18 + ease * 30, 1.6f, ink, 24);
                _sdCarryEffect.Flush();
                if (_sdCarryRack != null)
                    _sdCarryRack.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * .08f);
            }
            if (t < 1) return;
            _sdCarryToggleAge = -1;
            if (_sdCarryRack != null) _sdCarryRack.localScale = Vector3.one;
            SDDrawCarrySelection();
        }

        private void SDDrawCarrySelection()
        {
            if (_sdCarryEffect == null) return;
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
