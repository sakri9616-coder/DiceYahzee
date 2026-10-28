using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// The finishing blow, named. When the strike that ends the match reaches its bar, a band of
    /// light cuts across the table and the verdict is stamped onto it, oversized and tilted, then
    /// settles and waits for the click that opens the result screen.
    ///
    /// The match was already decided by the rules before this draws; the stamp only makes sure the
    /// player feels the moment the bar empties instead of discovering it on the next screen. It
    /// sits on its own canvas above the attack overlay and never takes input.
    /// </summary>
    internal sealed class KoStamp : MonoBehaviour
    {
        private const float SlamDuration = .36f;
        private const int SortOrder = 530;
        private Text _label;
        private Image _band;
        private Image _bandCore;
        private Image _shade;
        private float _age = -1f;
        private Color _color;

        public bool Showing => _age >= 0f;
        public string Text => _label != null ? _label.text : "";

        public static KoStamp Build(Transform parent)
        {
            RectTransform host = UiFactory.CreateStretch("KoStamp", parent);
            var canvas = host.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortOrder;
            var stamp = host.gameObject.AddComponent<KoStamp>();
            stamp.Construct(host);
            return stamp;
        }

        private void Construct(RectTransform host)
        {
            _shade = UiFactory.CreateStretchPanel("Shade", host, Color.clear);
            _shade.sprite = UiFactory.VignetteSprite;
            _shade.raycastTarget = false;
            _band = UiFactory.CreatePanel("Band", host, new Vector2(1600, 120), new Vector2(0, 30), Color.clear);
            _band.raycastTarget = false;
            _bandCore = UiFactory.CreatePanel("BandCore", host, new Vector2(1600, 6), new Vector2(0, 30), Color.clear);
            _bandCore.raycastTarget = false;
            _label = UiFactory.CreateText("Verdict", host, "", 132, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0, 30), new Vector2(1200, 200));
            _label.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? _label.font;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            var edge = _label.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(.03f, .06f, .07f, .95f);
            edge.effectDistance = new Vector2(4, -4);
            var drop = _label.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0, 0, 0, .55f);
            drop.effectDistance = new Vector2(0, -10);
            Hide();
        }

        public void Show(string verdict, Color color)
        {
            _color = color;
            _label.text = verdict;
            _age = 0f;
            gameObject.SetActive(true);
            GameAudio.Play("stamp");
            Sample();
        }

        public void Hide()
        {
            _age = -1f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_age < 0f) return;
            _age += Mathf.Min(Time.unscaledDeltaTime, .1f);
            Sample();
        }

        private void Sample()
        {
            bool reduced = Juice.ReducedMotion;
            float slam = reduced ? 1f : Mathf.Clamp01(_age / SlamDuration);
            float scale = reduced ? 1f : Mathf.LerpUnclamped(3.1f, 1f, Juice.EaseOutBack(slam, 1.4f));
            // The tilt eases out with the slam so the word lands crooked and rights itself a little.
            float tilt = reduced ? -4f : Mathf.Lerp(-14f, -5f, Juice.EaseOutCubic(slam));
            _label.rectTransform.localScale = Vector3.one * scale;
            _label.rectTransform.localRotation = Quaternion.Euler(0, 0, tilt);
            Color text = _color;
            text.a = reduced ? 1f : Mathf.Clamp01(_age / .08f);
            _label.color = text;

            // The band cuts across first, a hair ahead of the word, then dims to a resting stripe.
            float cut = reduced ? 1f : Juice.EaseOutCubic(Mathf.Clamp01(_age / .2f));
            float settle = reduced ? 0f : Mathf.Exp(-_age * 4f);
            _band.rectTransform.localScale = new Vector3(cut, 1f + .5f * settle, 1f);
            _band.rectTransform.localRotation = Quaternion.Euler(0, 0, tilt * .35f);
            _band.color = new Color(.02f, .04f, .05f, .62f * cut);
            _bandCore.rectTransform.localScale = new Vector3(cut, 1f + 2f * settle, 1f);
            _bandCore.rectTransform.localRotation = _band.rectTransform.localRotation;
            Color core = Color.Lerp(_color, Color.white, .55f * settle);
            core.a = .75f * cut;
            _bandCore.color = core;
            _shade.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_age / .25f));
        }
    }

    /// <summary>
    /// A freeze frame on the strike. The impact clock stops for a beat at the instant of contact,
    /// so the flash and the struck bar hold still long enough to register; the clock then runs at
    /// double speed until the frozen time is paid back, which reads as the recoil snapping loose.
    /// The impact therefore ends exactly when it would have without the freeze, so the moment a
    /// player may click on to the next round never moves. Once per impact.
    /// </summary>
    internal sealed class ImpactHitStop
    {
        /// <summary>The freeze is repaid at this many extra seconds of clock per real second.</summary>
        private const float Repay = 1f;
        private float _left;
        private float _debt;
        private bool _spent;

        public bool Holding => _left > 0f;

        public void Reset()
        {
            _left = 0f;
            _debt = 0f;
            _spent = false;
        }

        /// <summary>Weight of the freeze: heavier strikes and the finishing blow hold longer.</summary>
        public static float HoldFor(int tier, bool finishing)
        {
            if (Juice.ReducedMotion) return 0f;
            return .035f + .025f * Mathf.Clamp(tier, 0, 3) + (finishing ? .16f : 0f);
        }

        public float Advance(float age, float dt, float contact, float hold)
        {
            if (_left > 0f)
            {
                float use = Mathf.Min(_left, dt);
                _left -= use;
                _debt += use;
                dt -= use;
                if (dt <= 0f) return age;
            }
            if (_debt > 0f)
            {
                float extra = Mathf.Min(_debt, dt * Repay);
                _debt -= extra;
                dt += extra;
            }
            float next = age + dt;
            if (!_spent && hold > 0f && age < contact && next >= contact)
            {
                _spent = true;
                _left = hold;
                return contact;
            }
            return next;
        }
    }
}
