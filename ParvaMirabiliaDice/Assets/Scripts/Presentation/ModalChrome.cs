using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Dresses a modal as a card laid on the table rather than a grey rectangle: rounded, lit from
    /// above, framed with a brass hairline, its title set between two short rules. It opens with a
    /// short settle - fade in, rising a few units into place - so a menu arrives instead of
    /// blinking on. Buttons are live from the first frame; the entrance is decoration only.
    /// </summary>
    internal sealed class ModalChrome : MonoBehaviour
    {
        private const float EntranceDuration = .22f;
        private static readonly Color Brass = new Color(.80f, .66f, .42f, 1f);
        private CanvasGroup _group;
        private RectTransform _panel;
        private Text _title;
        private RectTransform _ruleLeft, _ruleRight, _gemLeft, _gemRight;
        private Vector2 _panelHome;
        private float _age = -1f;

        public static ModalChrome Dress(RectTransform root, Image panel, Text title)
        {
            if (root == null || panel == null) return null;
            var chrome = root.gameObject.GetComponent<ModalChrome>();
            if (chrome != null) return chrome;
            chrome = root.gameObject.AddComponent<ModalChrome>();
            chrome.Build(root, panel, title);
            return chrome;
        }

        private void Build(RectTransform root, Image panel, Text title)
        {
            _group = root.GetComponent<CanvasGroup>();
            if (_group == null) _group = root.gameObject.AddComponent<CanvasGroup>();
            _panel = panel.rectTransform;
            _panelHome = _panel.anchoredPosition;
            _title = title;

            Vector2 size = _panel.sizeDelta;
            panel.sprite = UiFactory.RoundedSprite(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            panel.type = Image.Type.Simple;
            Surfaces.Shade<TopLitShade>(panel).Lift = .07f;
            var rim = panel.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(Brass.r, Brass.g, Brass.b, .34f);
            rim.effectDistance = new Vector2(1.2f, -1.2f);
            rim.useGraphicAlpha = true;

            // An inner hairline a few units inside the edge: the second line is what makes a
            // frame read as a frame and not as a stroke.
            RectTransform innerRect = UiFactory.CreateRect("InnerFrame", panel.transform, size - Vector2.one * 18f, Vector2.zero);
            var inner = innerRect.gameObject.AddComponent<Image>();
            inner.sprite = Surfaces.RingSprite(Mathf.RoundToInt(size.x - 18f), Mathf.RoundToInt(size.y - 18f), 12);
            inner.color = new Color(Brass.r, Brass.g, Brass.b, .22f);
            inner.raycastTarget = false;

            if (_title != null)
            {
                Transform parent = _title.transform.parent;
                Vector2 at = _title.rectTransform.anchoredPosition;
                _ruleLeft = Rule("TitleRuleLeft", parent, at);
                _ruleRight = Rule("TitleRuleRight", parent, at);
                _gemLeft = Gem("TitleGemLeft", parent, at);
                _gemRight = Gem("TitleGemRight", parent, at);
            }
        }

        private static RectTransform Rule(string name, Transform parent, Vector2 at)
        {
            Image rule = UiFactory.CreatePanel(name, parent, new Vector2(70, 1.5f), at, new Color(Brass.r, Brass.g, Brass.b, .55f));
            rule.sprite = null;
            rule.raycastTarget = false;
            return rule.rectTransform;
        }

        private static RectTransform Gem(string name, Transform parent, Vector2 at)
        {
            Image gem = UiFactory.CreatePanel(name, parent, new Vector2(6, 6), at, Brass);
            gem.sprite = null;
            gem.raycastTarget = false;
            gem.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            return gem.rectTransform;
        }

        private void OnEnable()
        {
            if (_group == null) return;
            PlaceOrnaments();
            _age = Juice.ReducedMotion ? -1f : 0f;
            Sample();
        }

        private void OnDisable()
        {
            _age = -1f;
            if (_group != null) _group.alpha = 1f;
            if (_panel != null) _panel.anchoredPosition = _panelHome;
        }

        /// <summary>The title can change between openings, so the rules are measured each time.</summary>
        private void PlaceOrnaments()
        {
            if (_title == null || _ruleLeft == null) return;
            float half = Mathf.Min(_title.preferredWidth, _title.rectTransform.rect.width) * .5f;
            Vector2 at = _title.rectTransform.anchoredPosition;
            _ruleLeft.anchoredPosition = at + new Vector2(-(half + 22f + 35f), 0);
            _ruleRight.anchoredPosition = at + new Vector2(half + 22f + 35f, 0);
            _gemLeft.anchoredPosition = at + new Vector2(-(half + 14f), 0);
            _gemRight.anchoredPosition = at + new Vector2(half + 14f, 0);
        }

        private void Update()
        {
            if (_age < 0f) return;
            _age += Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (_age >= EntranceDuration) _age = -1f;
            Sample();
        }

        private void Sample()
        {
            if (_age < 0f)
            {
                _group.alpha = 1f;
                _panel.anchoredPosition = _panelHome;
                return;
            }
            float t = Mathf.Clamp01(_age / EntranceDuration);
            _group.alpha = Juice.EaseOutCubic(Mathf.Clamp01(t * 1.6f));
            _panel.anchoredPosition = _panelHome + Vector2.down * (14f * (1f - Juice.EaseOutCubic(t)));
        }
    }

    /// <summary>
    /// The round counter at the top of the table announces each new round: it swells, lit
    /// brass, with a short rule drawing out beneath it, then shrinks back into the quiet label it
    /// always is. It reuses the label the screen already owns, so nothing new crowds the centre
    /// of the table while the throw is in the air, and it never takes input.
    /// </summary>
    internal sealed class RoundBanner : MonoBehaviour
    {
        private const float Life = 1.3f;
        private const int PeakSize = 30;
        private static readonly Color Lit = new Color(1f, .90f, .66f, 1f);
        private Text _label;
        private Color _rest;
        private int _restSize;
        private Image _rule;
        private float _age = -1f;

        public static RoundBanner Attach(Text label)
        {
            var banner = label.gameObject.AddComponent<RoundBanner>();
            banner._label = label;
            banner._rest = label.color;
            banner._restSize = label.fontSize;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform rect = label.rectTransform;
            banner._rule = UiFactory.CreatePanel("RoundRule", rect.parent, new Vector2(220, 1.5f),
                rect.anchoredPosition + Vector2.down * 18f, Color.clear);
            banner._rule.sprite = null;
            banner._rule.raycastTarget = false;
            return banner;
        }

        public void Show()
        {
            _age = Juice.ReducedMotion ? -1f : 0f;
            if (_age < 0f) Rest(); else Sample();
        }

        private void OnDisable() { if (_age >= 0f) Rest(); }

        private void Rest()
        {
            _age = -1f;
            _label.color = _rest;
            _label.fontSize = _restSize;
            _rule.color = Color.clear;
        }

        private void Update()
        {
            if (_age < 0f) return;
            _age += Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (_age >= Life) { Rest(); return; }
            Sample();
        }

        private void Sample()
        {
            // The size is set in whole points rather than scaled, so the glyphs stay crisp.
            float swell = Juice.EaseOutBack(Mathf.Clamp01(_age / .32f), 1.3f);
            float shrink = Juice.EaseInOutSine(Mathf.Clamp01((_age - .7f) / .5f));
            float size = Mathf.LerpUnclamped(_restSize, PeakSize, swell) * (1f - shrink) + _restSize * shrink;
            _label.fontSize = Mathf.Max(1, Mathf.RoundToInt(size));
            _label.color = Color.Lerp(Lit, _rest, shrink);
            float reach = Juice.EaseOutCubic(Mathf.Clamp01((_age - .08f) / .4f));
            _rule.rectTransform.localScale = new Vector3(reach, 1f, 1f);
            Color rule = new Color(.80f, .66f, .42f, .7f * (1f - shrink));
            _rule.color = rule;
        }
    }
}
