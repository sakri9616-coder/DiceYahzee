using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// A soft wash behind a role row while the pointer rests on it. The rail rows paint their own
    /// face almost fully transparent, so the button's colour-tint highlight multiplied nothing and
    /// a hovered row looked exactly like an idle one. The wash sits behind the row's text, never
    /// takes a raycast and only shows while the row can actually be chosen.
    /// </summary>
    internal sealed class RowHoverWash : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color Wash = new Color(1f, .93f, .78f, 1f);
        private const float HoverAlpha = .13f;
        private Selectable _selectable;
        private Image _wash;
        private bool _hovered;
        private float _alpha;

        public static void Attach(Selectable selectable)
        {
            if (selectable == null || selectable.GetComponent<RowHoverWash>() != null) return;
            var hover = selectable.gameObject.AddComponent<RowHoverWash>();
            hover._selectable = selectable;
            RectTransform rect = UiFactory.CreateStretch("HoverWash", selectable.transform);
            rect.offsetMin = new Vector2(-3, 1);
            rect.offsetMax = new Vector2(3, -1);
            rect.SetAsFirstSibling();
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiFactory.RoundedSprite(180, 32);
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            Color clear = Wash; clear.a = 0f;
            image.color = clear;
            hover._wash = image;
        }

        public void OnPointerEnter(PointerEventData eventData) { _hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; }

        private void OnDisable()
        {
            _hovered = false;
            _alpha = 0f;
            Apply();
        }

        private void LateUpdate()
        {
            if (_wash == null) return;
            bool live = _selectable == null || _selectable.IsInteractable();
            float target = _hovered && live ? HoverAlpha : 0f;
            if (Mathf.Approximately(_alpha, target)) return;
            _alpha = Juice.ReducedMotion ? target
                : Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime * 1.2f);
            Apply();
        }

        private void Apply()
        {
            if (_wash == null) return;
            Color color = Wash; color.a = _alpha;
            _wash.color = color;
            _wash.enabled = _alpha > .001f;
        }
    }

    /// <summary>
    /// A clear frame around the button that keyboard navigation (Tab) has selected. A mouse click
    /// also selects a button in uGUI, so the frame only appears while the last input came from the
    /// keyboard; clicking anywhere hides it again. Purely visual: no scale, no flash.
    /// </summary>
    internal sealed class FocusFrame : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private static readonly Color FrameColor = new Color32(133, 237, 216, 255);
        private static int _modeFrame = -1;
        private static bool _keyboardMode;
        private Image _outer;
        private bool _selected;

        public static void Attach(Selectable selectable)
        {
            if (selectable == null || selectable.GetComponent<FocusFrame>() != null) return;
            selectable.gameObject.AddComponent<FocusFrame>();
        }

        private static bool KeyboardMode
        {
            get
            {
                if (_modeFrame == Time.frameCount) return _keyboardMode;
                _modeFrame = Time.frameCount;
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                    _keyboardMode = false;
                else if (Input.anyKeyDown)
                    _keyboardMode = true;
                return _keyboardMode;
            }
        }

        public void OnSelect(BaseEventData eventData) { _selected = true; }
        public void OnDeselect(BaseEventData eventData) { _selected = false; Show(false); }

        private void OnDisable()
        {
            _selected = false;
            Show(false);
        }

        private void LateUpdate()
        {
            if (!_selected) return;
            Show(KeyboardMode);
        }

        private void Show(bool visible)
        {
            if (visible && _outer == null) Build();
            if (_outer != null && _outer.gameObject.activeSelf != visible) _outer.gameObject.SetActive(visible);
        }

        private void Build()
        {
            var rect = transform as RectTransform;
            if (rect == null) return;
            Vector2 size = rect.rect.size;
            _outer = MakeRing("FocusFrame", transform, size + Vector2.one * 10f, .95f);
            MakeRing("FocusFrameInner", _outer.transform, size + Vector2.one * 8f, .55f);
            _outer.gameObject.SetActive(false);
        }

        private static Image MakeRing(string name, Transform parent, Vector2 size, float alpha)
        {
            Color color = FrameColor; color.a = alpha;
            Image ring = UiFactory.CreatePanel(name, parent, size, Vector2.zero, color);
            ring.sprite = Surfaces.RingSprite(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), 8);
            ring.type = Image.Type.Simple;
            ring.raycastTarget = false;
            ring.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return ring;
        }
    }

    /// <summary>
    /// A soft dark plate kept directly behind a transient label (the formation toast) so the
    /// words stay legible when enlarged dice, rings and sparks pass under them. It follows the
    /// label's text, alpha and scale, and never takes a raycast.
    /// </summary>
    internal sealed class TextBackplate : MonoBehaviour
    {
        private Text _text;
        private Image _plate;
        private string _measured;
        private const float Strength = .62f;

        public static void Attach(Text text)
        {
            if (text == null || text.GetComponent<TextBackplate>() != null || text.transform.parent == null) return;
            var backplate = text.gameObject.AddComponent<TextBackplate>();
            backplate._text = text;
            Image plate = UiFactory.CreatePanel(text.name + "Plate", text.transform.parent,
                new Vector2(200, 46), text.rectTransform.anchoredPosition, new Color(0f, 0f, 0f, 0f));
            plate.sprite = Surfaces.SoftShadow;
            plate.type = Image.Type.Sliced;
            // About 16 units of falloff on each edge (32-texel border at 100 per unit).
            plate.pixelsPerUnitMultiplier = .02f;
            plate.raycastTarget = false;
            plate.gameObject.SetActive(false);
            backplate._plate = plate;
        }

        private void OnDisable()
        {
            if (_plate != null) _plate.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_plate == null || _text == null) return;
            bool visible = !string.IsNullOrEmpty(_text.text) && _text.enabled && _text.color.a > .01f;
            if (_plate.gameObject.activeSelf != visible) _plate.gameObject.SetActive(visible);
            if (!visible) return;
            RectTransform plate = _plate.rectTransform, label = _text.rectTransform;
            if (_measured != _text.text)
            {
                _measured = _text.text;
                float width = Mathf.Min(label.sizeDelta.x, _text.preferredWidth) + 64f;
                plate.sizeDelta = new Vector2(width, Mathf.Max(48f, _text.fontSize * 2.4f));
            }
            plate.anchoredPosition = label.anchoredPosition;
            plate.localScale = label.localScale;
            _plate.color = new Color(0f, 0f, 0f, _text.color.a * Strength);
            int at = label.GetSiblingIndex(), mine = plate.GetSiblingIndex();
            if (plate.parent == label.parent && mine != at - 1)
                plate.SetSiblingIndex(mine < at ? at - 1 : at);
        }
    }
}
