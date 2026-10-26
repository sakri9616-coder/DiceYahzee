using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// The air above the table: one warm lamp pooled over the middle, dark falling away to the
    /// corners, and a little dust turning slowly in the light.
    ///
    /// The table used to be a single even field of dark teal, so the eye had nowhere to rest and
    /// the dice sat on it like stickers. A pool of light gives the play area a centre and the dust
    /// tells the player that the room is alive even while nobody is touching anything. Both stay
    /// far below the contrast of any text or die: this is a backdrop, never a signal. In reduced
    /// motion the dust holds still and the lamp stops breathing.
    /// </summary>
    internal sealed class TableAmbience : MonoBehaviour
    {
        private const int MoteCount = 34;
        private static Sprite _lampSprite;
        private Image _lamp;
        private Image _edge;
        private UiParticles _motes;
        private float _age;

        public static TableAmbience Build(Transform parent)
        {
            RectTransform host = UiFactory.CreateStretch("TableAmbience", parent);
            var ambience = host.gameObject.AddComponent<TableAmbience>();
            ambience.Construct(host);
            return ambience;
        }

        private void Construct(RectTransform host)
        {
            // A lamp pool wider than tall, warm against the cold leather.
            RectTransform lampRect = UiFactory.CreateRect("Lamp", host, new Vector2(1500, 980), new Vector2(0, -10));
            _lamp = lampRect.gameObject.AddComponent<Image>();
            _lamp.sprite = LampSprite;
            _lamp.color = new Color(1f, .82f, .58f, .085f);
            _lamp.raycastTarget = false;

            // A second, deeper falloff at the corners: the room beyond the lamp.
            _edge = UiFactory.CreateStretchPanel("Falloff", host, new Color(0, 0, 0, .34f));
            _edge.sprite = UiFactory.VignetteSprite;
            _edge.raycastTarget = false;
            RectTransform edgeRect = _edge.rectTransform;
            edgeRect.offsetMin = new Vector2(-120, -80);
            edgeRect.offsetMax = new Vector2(120, 80);

            _motes = UiParticles.Create("Dust", host, MoteCount, true);
            _motes.WrapBounds = new Rect(-700, -380, 1400, 760);
            var random = new System.Random(7031);
            for (int i = 0; i < MoteCount; i++)
            {
                float size = 1.6f + (float)random.NextDouble() * 3.2f;
                byte alpha = (byte)(28 + random.Next(70));
                _motes.Spawn(new UiParticles.Particle
                {
                    Position = new Vector2(-700 + (float)random.NextDouble() * 1400, -380 + (float)random.NextDouble() * 760),
                    Velocity = new Vector2(-2f + (float)random.NextDouble() * 4f, 3f + (float)random.NextDouble() * 9f),
                    Size = new Vector2(size, size),
                    Life = 6f + (float)random.NextDouble() * 8f,
                    Age = (float)random.NextDouble() * 6f,
                    Flutter = 3f + (float)random.NextDouble() * 6f,
                    Phase = (float)random.NextDouble() * 6.28f,
                    Color = new Color32(255, 232, 196, alpha)
                });
            }
            _motes.Step(0f);
        }

        private void Update()
        {
            if (Juice.ReducedMotion) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            _age += dt;
            _motes.Step(dt);
            // The lamp breathes over seven seconds, too slowly to be seen as a pulse.
            Color lamp = _lamp.color;
            lamp.a = .085f + .012f * Mathf.Sin(_age * .9f);
            _lamp.color = lamp;
        }

        /// <summary>A soft warm disc that falls off smoothly to nothing at its rim.</summary>
        internal static Sprite LampSprite
        {
            get
            {
                if (_lampSprite != null) return _lampSprite;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                float centre = (size - 1) * .5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - centre) / centre, dy = (y - centre) / centre;
                        float reach = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                        float alpha = Mathf.Exp(-reach * reach * 3.2f) * (1f - reach * reach);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                texture.SetPixels(pixels);
                texture.Apply();
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                _lampSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
                return _lampSprite;
            }
        }
    }
}
