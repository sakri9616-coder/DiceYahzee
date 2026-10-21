using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Palette plus tiny factories for the 2D UI. Everything is built from code, so the project
    /// ships with no art assets: flat panels, a runtime generated circle sprite for the dice pips
    /// and text rendered with an operating system Japanese font.
    /// </summary>
    internal static class UiFactory
    {
        public static readonly Color Background = FromHex(0x12, 0x12, 0x2A);
        public static readonly Color Panel = FromHex(0x1D, 0x1D, 0x3A);
        public static readonly Color PanelLight = FromHex(0x2B, 0x2B, 0x52);
        public static readonly Color Gold = FromHex(0xE8, 0xC8, 0x7A);
        public static readonly Color GoldDim = FromHex(0x9A, 0x84, 0x54);
        public static readonly Color Text = FromHex(0xED, 0xED, 0xF2);
        public static readonly Color TextDim = FromHex(0xA0, 0xA0, 0xBC);
        public static readonly Color AttackRed = FromHex(0xC9, 0x4F, 0x4F);
        public static readonly Color GuardBlue = FromHex(0x4A, 0x8C, 0xD9);
        public static readonly Color HpGreen = FromHex(0x53, 0xC6, 0x6A);
        public static readonly Color HpRed = FromHex(0xD9, 0x53, 0x4F);
        public static readonly Color HpTrack = FromHex(0x2A, 0x2A, 0x44);
        public static readonly Color DieFace = FromHex(0xF4, 0xF1, 0xE6);
        public static readonly Color Pip = FromHex(0x1A, 0x1A, 0x2E);
        public static readonly Color ButtonFace = FromHex(0x33, 0x33, 0x5E);
        public static readonly Color ButtonFaceHot = FromHex(0x4A, 0x4A, 0x80);

        /// <summary>
        /// A disabled button has to be obviously *off* without becoming invisible.
        ///
        /// The face sits at 1.26:1 against the screen behind it, which is enough to keep the
        /// silhouette where the action lives, and the label at 5.32:1 against that face - above
        /// the 4.5:1 line a reader needs to name it. The previous label measured 2.70:1, so a
        /// greyed-out "確定する" kept its words and lost them: readable-disabled, measured by
        /// ReadabilityTests.
        /// </summary>
        public static readonly Color ButtonDisabled = FromHex(0x26, 0x26, 0x42);
        public static readonly Color ButtonDisabledText = FromHex(0x9A, 0x9A, 0xB4);

        private static Font _font;
        private static Sprite _circle;
        private static Sprite _vignette;
        private static readonly Dictionary<long, Sprite> _roundedBySize = new Dictionary<long, Sprite>();

        /// <summary>A Japanese font available on the current platform.</summary>
        public static Font TextFont
        {
            get
            {
                if (_font != null)
                    return _font;

#if UNITY_WEBGL && !UNITY_EDITOR
                // Browsers cannot resolve the Windows font names below. Use a
                // bundled face with full Japanese glyph coverage for all UI text.
                _font = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
                if (_font != null)
                    return _font;
#endif

                string[] candidates =
                {
                    "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo",
                    "MS Gothic", "Segoe UI", "Arial"
                };

                foreach (string name in candidates)
                {
                    Font font = Font.CreateDynamicFontFromOSFont(name, 24);
                    if (font != null)
                    {
                        _font = font;
                        return _font;
                    }
                }

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>A soft circle used for dice pips and dots; generated once, never downloaded.</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (_circle != null)
                    return _circle;

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = (size - 1) * 0.5f;
                float radius = center;
                var pixels = new Color[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;

                _circle = Sprite.Create(
                    texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
                return _circle;
            }
        }

        /// <summary>
        /// A soft vignette: clear in the middle, darkening towards the edges.
        ///
        /// The screens are otherwise one flat field of colour, and a flat field gives the eye
        /// nowhere to land - which is most of why code-drawn UI reads as a debug layout. Every
        /// game that puts dice on a table lights the middle of the table; this is that light,
        /// drawn as a radial falloff so it can never be mistaken for a panel.
        /// </summary>
        public static Sprite VignetteSprite
        {
            get
            {
                if (_vignette != null)
                    return _vignette;

                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                float centre = (size - 1) * 0.5f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - centre) / centre;
                        float dy = (y - centre) / centre;
                        // Zero at the middle, one at the edge, root two at the corners.
                        float reach = Mathf.Sqrt(dx * dx + dy * dy);
                        float t = Mathf.Clamp01((reach - 0.3f) / 1.1f);
                        float alpha = t * t * (3f - 2f * t) * 0.6f;
                        pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;

                _vignette = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f), 1f);
                return _vignette;
            }
        }

        /// <summary>How far a rounded corner reaches, in canvas units.</summary>
        private const int CornerUnits = 16;

        /// <summary>Textures larger than this are generated smaller and stretched back.</summary>
        private const int MaxSpriteEdge = 512;

        /// <summary>
        /// A rounded rectangle of the exact size of the widget that will draw it.
        ///
        /// The screens are made entirely of flat rectangles, and a square corner on a die is what
        /// makes code-drawn UI read as a debug layout rather than as a game: every dice game that
        /// is played for its own sake rounds its dice, because a die is an object and objects
        /// have edges.
        ///
        /// Nine-slicing was tried first and is the wrong tool here. Unity scales a sliced border
        /// by the size of the widget it lands on instead of holding it at the size the sprite was
        /// authored with, so a 16-unit border on a 64-pixel sprite became a corner of half
        /// whatever it was asked to cover: the dice photographed as circles and the buttons as
        /// ellipses (unity-probe.png is that failure on its own, no die in frame). Generating the
        /// sprite at the widget's own size leaves one pixel per unit and nothing to distort; the
        /// cost is one sprite per distinct size, which is what the cache is for.
        /// </summary>
        public static Sprite RoundedSprite(int width, int height)
        {
            int targetWidth = Mathf.Max(8, Mathf.RoundToInt(width));
            int targetHeight = Mathf.Max(8, Mathf.RoundToInt(height));

            // A panel as large as the screen would cost megabytes for one shape, so the texture is
            // capped and the corner scales down with it - a sprite of the same aspect ratio,
            // stretched back onto the widget, puts the corner back where it was asked for.
            int longest = Mathf.Max(targetWidth, targetHeight);
            float scale = longest > MaxSpriteEdge ? (float)MaxSpriteEdge / longest : 1f;
            int textureWidth = Mathf.Max(8, Mathf.RoundToInt(targetWidth * scale));
            int textureHeight = Mathf.Max(8, Mathf.RoundToInt(targetHeight * scale));
            int radius = Mathf.Clamp(Mathf.RoundToInt(CornerUnits * scale), 3,
                Mathf.Min(textureWidth, textureHeight) / 2 - 1);

            long key = ((long)radius << 32) | ((long)textureHeight << 16) | (uint)textureWidth;
            if (_roundedBySize.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            var texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            var pixels = new Color[textureWidth * textureHeight];

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    // Distance to the rectangle left once the four corners are cut away: zero in
                    // the middle, which keeps the flat sides flat.
                    float innerX = Mathf.Clamp(x, radius, textureWidth - 1 - radius);
                    float innerY = Mathf.Clamp(y, radius, textureHeight - 1 - radius);
                    float dx = x - innerX;
                    float dy = y - innerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * textureWidth + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;

            Sprite sprite = Sprite.Create(texture,
                new Rect(0f, 0f, textureWidth, textureHeight), new Vector2(0.5f, 0.5f), 1f);
            _roundedBySize[key] = sprite;
            return sprite;
        }

        /// <summary>How strong the light along a plate's top edge is, at its brightest.</summary>
        private const float GlossPeak = 0.18f;

        private static readonly Dictionary<long, Sprite> _glossBySize = new Dictionary<long, Sprite>();

        /// <summary>
        /// The light a rounded plate catches along its top edge, generated rather than drawn.
        ///
        /// A flat fill with a shadow under it is still a rectangle on a page. What makes a surface
        /// read as an object is that it is lit from above, so the top of the plate is brighter
        /// than its middle. The falloff reaches zero a little past halfway, which is what keeps it
        /// from reading as a second rectangle stacked on the first: a highlight with a visible
        /// bottom edge is a seam, and a seam is exactly the tell this project is trying to lose.
        /// </summary>
        public static Sprite GlossSprite(int width, int height)
        {
            int targetWidth = Mathf.Max(8, Mathf.RoundToInt(width));
            int targetHeight = Mathf.Max(8, Mathf.RoundToInt(height));

            int longest = Mathf.Max(targetWidth, targetHeight);
            float scale = longest > MaxSpriteEdge ? (float)MaxSpriteEdge / longest : 1f;
            int textureWidth = Mathf.Max(8, Mathf.RoundToInt(targetWidth * scale));
            int textureHeight = Mathf.Max(8, Mathf.RoundToInt(targetHeight * scale));
            int radius = Mathf.Clamp(Mathf.RoundToInt(CornerUnits * scale), 3,
                Mathf.Min(textureWidth, textureHeight) / 2 - 1);

            long key = ((long)radius << 32) | ((long)textureHeight << 16) | (uint)textureWidth;
            if (_glossBySize.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            var texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            var pixels = new Color[textureWidth * textureHeight];

            for (int y = 0; y < textureHeight; y++)
            {
                // Row zero is the bottom of the widget, so the top row is the last one.
                float fromTop = textureHeight <= 1 ? 0f : 1f - y / (float)(textureHeight - 1);
                float t = Mathf.Clamp01(fromTop / 0.55f);
                float light = (1f - t) * (1f - t) * GlossPeak;

                for (int x = 0; x < textureWidth; x++)
                {
                    float innerX = Mathf.Clamp(x, radius, textureWidth - 1 - radius);
                    float innerY = Mathf.Clamp(y, radius, textureHeight - 1 - radius);
                    float dx = x - innerX;
                    float dy = y - innerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float shape = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * textureWidth + x] = new Color(1f, 1f, 1f, shape * light);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;

            Sprite sprite = Sprite.Create(texture,
                new Rect(0f, 0f, textureWidth, textureHeight), new Vector2(0.5f, 0.5f), 1f);
            _glossBySize[key] = sprite;
            return sprite;
        }

        /// <summary>Lays the light over a face, as a child so it always draws on top of it.</summary>
        public static Image AddGloss(Image face, int width, int height)
        {
            if (face == null)
                return null;

            var rect = CreateRect("Gloss", face.rectTransform, new Vector2(width, height), Vector2.zero);
            var gloss = rect.gameObject.AddComponent<Image>();
            gloss.sprite = GlossSprite(width, height);
            gloss.color = Color.white;
            gloss.raycastTarget = false;
            return gloss;
        }

        /// <summary>
        /// A drop shadow built from the widget's own silhouette, so it needs no art and can never
        /// disagree with the shape it belongs to. It lives on the graphic itself rather than on a
        /// child, because a child would draw over the face it is supposed to be behind - which is
        /// why the dice, whose shadow had to be a sibling, are the one place that does it by hand.
        /// </summary>
        public static Shadow AddDropShadow(Graphic graphic, Vector2 distance, float alpha)
        {
            if (graphic == null)
                return null;

            Shadow shadow = graphic.GetComponent<Shadow>();
            if (shadow == null)
                shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
            return shadow;
        }

        public static Color FromHex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        /// <summary>Creates a centred rect of the given size at the given offset from screen centre.</summary>
        public static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        /// <summary>Creates a rect that stretches across the whole canvas.</summary>
        public static RectTransform CreateStretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>
        /// Attaches a vertex-built Graphic and makes sure it owns a live CanvasRenderer.
        ///
        /// uGUI creates a Graphic's CanvasRenderer lazily on its first rebuild. On a custom
        /// Graphic subclass that lazy add can hand back a renderer that is already gone; the
        /// Graphic then caches the dead wrapper, every later rebuild bails out at
        /// "canvasRenderer == null", and the mesh is never drawn. Built-in Images are unaffected,
        /// which is why the dice and labels kept rendering in editor play while the stroke rings,
        /// trail lines and shockwaves vanished. Adding the renderer here, and retrying once when
        /// the first add is discarded, keeps the mesh path the same in editor play and in player
        /// builds. Effect graphics are never input targets.
        /// </summary>
        public static T AddVertexGraphic<T>(GameObject host) where T : Graphic
        {
            var graphic = host.AddComponent<T>();
            graphic.raycastTarget = false;
            if (host.GetComponent<CanvasRenderer>() == null)
            {
                host.AddComponent<CanvasRenderer>();
                if (host.GetComponent<CanvasRenderer>() == null)
                    host.AddComponent<CanvasRenderer>();
            }
            return graphic;
        }

        /// <summary>
        /// A flat panel. It takes the same rounded corner as everything else: rounding the dice
        /// and the buttons while leaving the bars and the result frame square is the kind of
        /// half-applied rule a reader notices without being able to say why.
        /// </summary>
        public static Image CreatePanel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            var rect = CreateRect(name, parent, size, position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.sprite = RoundedSprite(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            return image;
        }

        public static Image CreateStretchPanel(string name, Transform parent, Color color)
        {
            var rect = CreateStretch(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(string name, Transform parent, string content, int size,
            TextAnchor anchor, Color color, Vector2 position, Vector2 box)
        {
            var rect = CreateRect(name, parent, box, position);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = TextFont;
            text.fontStyle = FontStyle.Bold;
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, Vector2 size, Vector2 position,
            string label, int fontSize, UnityAction onClick)
        {
            var rect = CreateRect(name, parent, size, position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonFace;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = FromHex(0xD8, 0xD8, 0xE8);
            colors.pressedColor = FromHex(0xB8, 0xB8, 0xD0);
            colors.disabledColor = Color.white;
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            image.sprite = RoundedSprite(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            AddDropShadow(image, new Vector2(0f, -4f), 0.45f);
            // Gloss is added before the label so the words sit on the material instead of
            // underneath it, and it fades to nothing well above the bottom edge so there is no
            // seam for the eye to catch on.
            AddGloss(image, Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));

            var caption = CreateText("Label", rect, label, fontSize, TextAnchor.MiddleCenter, Text,
                Vector2.zero, size);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Truncate;

            var palette = rect.gameObject.AddComponent<ButtonPalette>();
            palette.Face = image.color;
            palette.Label = caption.color;

            return button;
        }

        /// <summary>Styles a button as the primary (gold) action.</summary>
        public static void StylePrimary(Button button)
        {
            if (button == null)
                return;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = Gold;
            var caption = button.GetComponentInChildren<Text>();
            if (caption != null)
                caption.color = FromHex(0x24, 0x1E, 0x10);

            Remember(button, Gold, FromHex(0x24, 0x1E, 0x10));
        }

        /// <summary>Styles a button as a selected choice in a list of toggles.</summary>
        public static void StyleSelected(Button button, bool selected)
        {
            if (button == null)
                return;
            var image = button.GetComponent<Image>();
            Color face = selected ? Gold : ButtonFace;
            Color label = selected ? FromHex(0x24, 0x1E, 0x10) : Text;
            if (image != null)
                image.color = face;
            var caption = button.GetComponentInChildren<Text>();
            if (caption != null)
                caption.color = label;

            Remember(button, face, label);
        }

        /// <summary>
        /// Greys a button out, and puts it back exactly as its style last asked to be.
        ///
        /// Enabling used to stamp <see cref="ButtonFace"/> over whatever the button had been
        /// styled as, so the primary action - gold face, dark brown label - came back as a dark
        /// face with its dark label still on it. That combination measures about 1.3:1, which is
        /// not a contrast so much as a rumour: the game's main button was effectively blank, and
        /// it stayed that way on every screen because nothing on this project could see it.
        /// </summary>
        public static void SetButtonEnabled(Button button, bool enabled)
        {
            if (button == null)
                return;
            button.interactable = enabled;

            Color face = ButtonFace;
            Color label = Text;
            var palette = button.GetComponent<ButtonPalette>();
            if (palette != null)
            {
                face = palette.Face;
                label = palette.Label;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = enabled ? face : ButtonDisabled;

            var caption = button.GetComponentInChildren<Text>();
            if (caption != null)
                caption.color = enabled ? label : ButtonDisabledText;
        }

        private static void Remember(Button button, Color face, Color label)
        {
            var palette = button.GetComponent<ButtonPalette>();
            if (palette == null)
                palette = button.gameObject.AddComponent<ButtonPalette>();
            palette.Face = face;
            palette.Label = label;
        }
    }

    /// <summary>
    /// The colours a button should have while it may be pressed, recorded by whichever style was
    /// last applied to it, so that greying it out and bringing it back is a change of state
    /// rather than a change of identity.
    /// </summary>
    internal sealed class ButtonPalette : MonoBehaviour
    {
        public Color Face;
        public Color Label;
    }
}
