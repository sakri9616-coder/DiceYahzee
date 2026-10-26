using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Easing curves and the shared motion switch for the feel layer.
    ///
    /// Everything in this layer is feedback riding on top of a rule that already happened: it may
    /// exaggerate briefly, it always returns to rest, and it never holds a decision hostage. The
    /// menu's "成立時の動き：控えめ" switch is the one place a player turns it down, so every
    /// effect reads <see cref="ReducedMotion"/> instead of keeping its own copy.
    /// </summary>
    internal static class Juice
    {
        public const string ReducedMotionKey = "arcane.reducedMotion";
        private static bool? _reduced;

        public static bool ReducedMotion
        {
            get
            {
                if (!_reduced.HasValue) _reduced = PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;
                return _reduced.Value;
            }
            set { _reduced = value; }
        }

        /// <summary>Overshoots about ten percent past one and settles back: the "pop".</summary>
        public static float EaseOutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
        }

        public static float EaseOutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) * .5f;

        /// <summary>A damped spring from 0 to 1; reads as a landing rather than a stop.</summary>
        public static float EaseOutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f || t >= 1f) return t;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - .75f) * (2f * Mathf.PI / 3f)) + 1f;
        }

        /// <summary>Layered sines at unrelated rates: a smooth noise that never buzzes.</summary>
        public static float Wobble(float time, float seed)
            => .6f * Mathf.Sin(time * 11f + seed) + .4f * Mathf.Sin(time * 23f + seed * 2.1f);
    }

    /// <summary>
    /// The pointer's handshake with a button: it leans in under the cursor and gives a little
    /// under the press, then springs back. A flat colour change says the button exists; the scale
    /// says it heard you. The scale is eased toward its target on unscaled time so a hit-stop or a
    /// pause can never leave a button stuck half pressed.
    /// </summary>
    internal sealed class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.035f;
        private const float PressScale = .955f;
        private const float Sharpness = 22f;
        private Selectable _selectable;
        private Vector3 _rest = Vector3.one;
        private bool _restCaptured, _hovered, _pressed;
        private float _current = 1f;

        public static void Attach(Selectable selectable)
        {
            if (selectable == null || selectable.GetComponent<ButtonJuice>() != null) return;
            selectable.gameObject.AddComponent<ButtonJuice>()._selectable = selectable;
        }

        private void OnEnable()
        {
            if (_selectable == null) _selectable = GetComponent<Selectable>();
            CaptureRest();
        }

        private void OnDisable()
        {
            _hovered = _pressed = false;
            _current = 1f;
            if (_restCaptured) transform.localScale = _rest;
        }

        private void CaptureRest()
        {
            if (_restCaptured) return;
            _rest = transform.localScale;
            _restCaptured = true;
        }

        public void OnPointerEnter(PointerEventData eventData) { _hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; _pressed = false; }
        public void OnPointerDown(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left) _pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { _pressed = false; }

        private void LateUpdate()
        {
            bool live = _selectable == null || _selectable.IsInteractable();
            float target = !live || Juice.ReducedMotion ? 1f : _pressed ? PressScale : _hovered ? HoverScale : 1f;
            if (Mathf.Abs(_current - target) < .0005f && Mathf.Approximately(_current, 1f) && target == 1f) return;
            _current = Mathf.Lerp(_current, target, 1f - Mathf.Exp(-Sharpness * Time.unscaledDeltaTime));
            if (Mathf.Abs(_current - target) < .0005f) _current = target;
            transform.localScale = _rest * _current;
        }
    }
}
