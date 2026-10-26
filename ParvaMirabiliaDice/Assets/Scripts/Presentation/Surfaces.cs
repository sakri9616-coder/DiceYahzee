using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Material for the flat shapes the screens are made of: a soft contact shadow, a top-lit
    /// shade across a surface, and the light a curved bar catches. None of it changes a colour a
    /// screen sets or a rectangle a screen measures - each effect only reshades the vertices the
    /// graphic already produces.
    /// </summary>
    internal static class Surfaces
    {
        private const int ShadowTexture = 96;
        private const int ShadowFalloff = 30;
        private static Sprite _softShadow;

        /// <summary>
        /// A rounded block blurred at its edge, sliced so the blur keeps its width on any size.
        /// The hard-edged shadow it replaces read as a second, darker tile under every die.
        /// </summary>
        public static Sprite SoftShadow
        {
            get
            {
                if (_softShadow != null) return _softShadow;
                const int size = ShadowTexture;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                float inner = ShadowFalloff;
                float max = size - 1 - ShadowFalloff;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(0f, Mathf.Max(inner - x, x - max));
                        float dy = Mathf.Max(0f, Mathf.Max(inner - y, y - max));
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / ShadowFalloff;
                        float alpha = Mathf.Clamp01(1f - d);
                        alpha = alpha * alpha * (3f - 2f * alpha);
                        pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
                    }
                texture.SetPixels(pixels);
                texture.Apply();
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                float border = ShadowFalloff + 2;
                _softShadow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f,
                    0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                return _softShadow;
            }
        }

        /// <summary>
        /// Lays a soft contact shadow under a die-sized object. The blur scales with the object so
        /// a 44-unit reveal die and a 112-unit hand die cast the same-looking shadow.
        /// </summary>
        public static void ApplySoftShadow(Image shadow, float objectSize, float alpha)
        {
            if (shadow == null) return;
            float blur = Mathf.Clamp(objectSize * .14f, 5f, 16f);
            shadow.sprite = SoftShadow;
            shadow.type = Image.Type.Sliced;
            shadow.fillCenter = true;
            // The sprite's falloff spans 30 texels at 100 per unit; this maps it to `blur` units.
            shadow.pixelsPerUnitMultiplier = ShadowFalloff / blur / 100f;
            shadow.rectTransform.sizeDelta = Vector2.one * (objectSize + blur * 1.2f);
            shadow.rectTransform.anchoredPosition = new Vector2(objectSize * .03f, -objectSize * .07f);
            shadow.color = new Color(0f, 0f, 0f, alpha);
        }

        private static readonly System.Collections.Generic.Dictionary<long, Sprite> _rings =
            new System.Collections.Generic.Dictionary<long, Sprite>();

        /// <summary>A one-unit rounded hairline of the exact size it will be drawn at.</summary>
        public static Sprite RingSprite(int width, int height, int radius)
        {
            width = Mathf.Clamp(width, 8, 1024);
            height = Mathf.Clamp(height, 8, 1024);
            radius = Mathf.Clamp(radius, 2, Mathf.Min(width, height) / 2 - 1);
            long key = ((long)radius << 40) | ((long)height << 20) | (uint)width;
            if (_rings.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float cx = Mathf.Clamp(x + .5f, radius, width - radius);
                    float cy = Mathf.Clamp(y + .5f, radius, height - radius);
                    float dx = x + .5f - cx, dy = y + .5f - cy;
                    float inCorner = (dx != 0f || dy != 0f) ? radius - Mathf.Sqrt(dx * dx + dy * dy)
                        : Mathf.Min(Mathf.Min(x + .5f, width - x - .5f), Mathf.Min(y + .5f, height - y - .5f));
                    // Distance inward from the rounded edge; the line is the first unit of it.
                    float alpha = Mathf.Clamp01(1.2f - Mathf.Abs(inCorner - .7f));
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 1f);
            _rings[key] = sprite;
            return sprite;
        }

        public static T Shade<T>(Graphic graphic) where T : BaseMeshEffect
        {
            if (graphic == null) return null;
            T effect = graphic.GetComponent<T>();
            if (effect == null) effect = graphic.gameObject.AddComponent<T>();
            graphic.SetVerticesDirty();
            return effect;
        }
    }

    /// <summary>
    /// Light from above across one surface: the top edge a little brighter, the bottom a little
    /// deeper. The graphic's own colour stays the colour every screen and test reads; only the
    /// vertices it produces are reshaded.
    /// </summary>
    internal sealed class TopLitShade : BaseMeshEffect
    {
        public float Lift = .06f;
        public float Sink = .10f;

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper == null || helper.currentVertCount == 0) return;
            float min = float.MaxValue, max = float.MinValue;
            UIVertex vertex = UIVertex.simpleVert;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                min = Mathf.Min(min, vertex.position.y);
                max = Mathf.Max(max, vertex.position.y);
            }
            float span = max - min;
            if (span < .001f) return;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                float t = (vertex.position.y - min) / span;           // 0 bottom, 1 top
                Color c = vertex.color;
                Color shaded = t >= .5f
                    ? Color.Lerp(c, Color.white, Lift * (t - .5f) * 2f)
                    : Color.Lerp(c, Color.black, Sink * (.5f - t) * 2f);
                shaded.a = c.a;
                vertex.color = shaded;
                helper.SetUIVertex(vertex, i);
            }
        }
    }

    /// <summary>
    /// The light a rounded bar catches: a bright band just above its middle and a shadowed
    /// underside, so a flat pill reads as a glass tube of colour. Works on any simple mesh by
    /// inserting a seam at the highlight so the band stays crisp on four-vertex quads too.
    /// </summary>
    internal sealed class TubeShade : BaseMeshEffect
    {
        public float Highlight = .26f;
        public float Underside = .22f;

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper == null) return;
            // A simple image is one quad; split it into three bands so the shading has rows.
            if (helper.currentVertCount != 4)
            {
                ShadeInPlace(helper);
                return;
            }
            var corners = new UIVertex[4];
            for (int i = 0; i < 4; i++) helper.PopulateUIVertex(ref corners[i], i);
            // Simple image order: 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right.
            UIVertex bl = corners[0], tl = corners[1], tr = corners[2], br = corners[3];
            float[] rows = { 0f, .45f, .72f, 1f };
            helper.Clear();
            for (int r = 0; r < rows.Length; r++)
            {
                float t = rows[r];
                helper.AddVert(Shade(Mix(bl, tl, t), t));
                helper.AddVert(Shade(Mix(br, tr, t), t));
            }
            for (int r = 0; r < rows.Length - 1; r++)
            {
                int a = r * 2;
                helper.AddTriangle(a, a + 2, a + 3);
                helper.AddTriangle(a + 3, a + 1, a);
            }
        }

        private void ShadeInPlace(VertexHelper helper)
        {
            float min = float.MaxValue, max = float.MinValue;
            UIVertex vertex = UIVertex.simpleVert;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                min = Mathf.Min(min, vertex.position.y);
                max = Mathf.Max(max, vertex.position.y);
            }
            float span = max - min;
            if (span < .001f) return;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                helper.SetUIVertex(Shade(vertex, (vertex.position.y - min) / span), i);
            }
        }

        private static UIVertex Mix(UIVertex a, UIVertex b, float t)
        {
            UIVertex v = a;
            v.position = Vector3.Lerp(a.position, b.position, t);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
            v.color = Color32.Lerp(a.color, b.color, t);
            return v;
        }

        private UIVertex Shade(UIVertex v, float t)
        {
            Color c = v.color;
            // Peak light at .72 of the height, falling to the base colour at the top edge.
            float light = t <= .72f ? Mathf.Clamp01((t - .45f) / .27f) : Mathf.Clamp01((1f - t) / .28f) * .55f + .45f;
            float dark = Mathf.Clamp01((.45f - t) / .45f);
            Color shaded = Color.Lerp(c, Color.white, Highlight * Mathf.Max(0f, light));
            shaded = Color.Lerp(shaded, Color.black, Underside * dark);
            shaded.a = c.a;
            v.color = shaded;
            return v;
        }
    }
}
