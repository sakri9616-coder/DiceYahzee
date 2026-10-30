using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>Local material styling for DiceYahzee, using imported Kenney CC0 sprites.</summary>
    internal static class DiceYahzeeVisuals
    {
        public static readonly Color Background = UiFactory.FromHex(0x10, 0x1B, 0x24);
        public static readonly Color Panel = UiFactory.FromHex(0x20, 0x30, 0x39);
        public static readonly Color Parchment = UiFactory.FromHex(0xE8, 0xE1, 0xD1);
        public static readonly Color Brass = UiFactory.FromHex(0xCB, 0xA8, 0x6A);
        public static readonly Color Ink = UiFactory.FromHex(0x18, 0x25, 0x2C);
        public static readonly Color MutedText = UiFactory.FromHex(0xAB, 0xB8, 0xBB);
        private const string Art = "Art/DiceYahzee/";
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Sprite LoadSprite(string resource)
        {
            if (string.IsNullOrEmpty(resource)) return null;
            if (!Sprites.TryGetValue(resource, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>(resource);
                if (sprite != null) Sprites[resource] = sprite;
            }
            return sprite;
        }

        public static void StylePanel(Image image, bool parchment = false)
        {
            if (image == null) return;
            ApplySprite(image, Art + "panel-flat");
            image.color = parchment ? Parchment : Panel;
            UiFactory.AddDropShadow(image, new Vector2(0f, -3f), 0.18f);
        }

        public static void StyleButton(Button button, bool primary = false, bool quiet = false)
        {
            if (button == null) return;
            Image image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            Color face = primary ? Brass : quiet ? Panel : UiFactory.FromHex(0x34, 0x48, 0x51);
            Color label = primary ? Ink : Parchment;
            if (image != null)
            {
                ApplySprite(image, Art + (quiet ? "panel-flat" : "button-primary"));
                image.color = face;
                Shadow shadow = image.GetComponent<Shadow>();
                if (shadow != null) shadow.enabled = primary;
                if (primary)
                {
                    shadow = UiFactory.AddDropShadow(image, new Vector2(0f, -2f), 0.22f);
                    shadow.enabled = true;
                }
            }
            Transform gloss = button.transform.Find("Gloss");
            if (gloss != null) gloss.gameObject.SetActive(false);
            Text caption = button.GetComponentInChildren<Text>();
            if (caption != null) caption.color = label;
            ButtonPalette palette = button.GetComponent<ButtonPalette>();
            if (palette == null) palette = button.gameObject.AddComponent<ButtonPalette>();
            palette.Face = face;
            palette.Label = label;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.10f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.83f, 0.85f, 0.85f, 1f);
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            UiFactory.SetButtonEnabled(button, button.interactable);
            ButtonJuice.Attach(button);
            FocusFrame.Attach(button);
        }

        public static void StyleDie(Transform die)
        {
            if (die == null) return;
            Image face = ChildImage(die, "Face");
            if (face == null) return;
            ApplySprite(face, Art + "panel-flat");
            Image ring = ChildImage(die, "Ring");
            if (ring != null)
            {
                ApplySprite(ring, Art + "panel-flat");
                // The keep ring shows as a band around the face; 5 units a side reads at a glance.
                ring.rectTransform.sizeDelta = face.rectTransform.sizeDelta + Vector2.one * 10;
            }
            Image shadow = ChildImage(die, "Shadow");
            if (shadow != null)
                // A soft contact shadow: a die rests on the leather instead of on a darker tile.
                Surfaces.ApplySoftShadow(shadow, face.rectTransform.sizeDelta.x, 0.46f);
            // Lit from above: the face brightens toward its top edge and deepens toward its foot.
            TopLitShade faceLight = Surfaces.Shade<TopLitShade>(face);
            faceLight.Lift = .05f;
            faceLight.Sink = .13f;
            // Extra decoration never changes the original face, pips, hit target or root geometry.
            Image side = ChildImage(die, "MaterialSide");
            if (side == null)
            {
                RectTransform rect = UiFactory.CreateRect("MaterialSide", die,
                    face.rectTransform.sizeDelta, new Vector2(0f, -3f));
                side = rect.gameObject.AddComponent<Image>();
                side.raycastTarget = false;
            }
            ApplySprite(side, Art + "panel-flat");
            side.color = UiFactory.FromHex(0xA7, 0x9D, 0x89);
            side.rectTransform.anchoredPosition = new Vector2(0f, -Mathf.Max(2f, face.rectTransform.sizeDelta.x * .035f));
            TopLitShade sideLight = Surfaces.Shade<TopLitShade>(side);
            sideLight.Lift = 0f;
            sideLight.Sink = .28f;
            int faceIndex = face.transform.GetSiblingIndex();
            if (side.transform.GetSiblingIndex() < faceIndex) faceIndex--;
            side.transform.SetSiblingIndex(faceIndex);
            Outline edge = face.GetComponent<Outline>();
            if (edge == null) edge = face.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.24f, 0.20f, 0.14f, 0.24f);
            edge.effectDistance = new Vector2(0.65f, -0.65f);
            edge.useGraphicAlpha = true;
        }

        public static Image Icon(string name, Transform parent, string resource,
            Vector2 pos, Vector2 size, Color color)
        {
            RectTransform rect = UiFactory.CreateRect(name, parent, size, pos);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = LoadSprite(resource);
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = image.sprite != null;
            return image;
        }

        private static Image ChildImage(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static void ApplySprite(Image image, string resource)
        {
            Sprite sprite = LoadSprite(resource);
            if (sprite == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 2.5f;
        }
    }
}
