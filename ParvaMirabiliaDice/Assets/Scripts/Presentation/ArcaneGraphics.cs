using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>Hand-built vector furniture; no bitmap generation or generative art.</summary>
    public sealed class ArcaneBackdrop : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Color ink = new Color(0.36f, 0.69f, 0.64f, 0.14f);
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = 260 + ring * 145;
                for (int i = 0; i < 120; i++)
                    Stroke(mesh, Polar(radius, i * 3), Polar(radius, (i + 1) * 3), 1.5f, ink);
                for (int i = 0; i < 24; i++)
                    Stroke(mesh, Polar(radius - 7, i * 15), Polar(radius + 7, i * 15), 1, ink);
            }
            for (int i = 0; i < 12; i++)
                Stroke(mesh, Polar(550, i * 30), Polar(550, i * 30 + 150), 1,
                    new Color(0.66f, 0.62f, 0.38f, 0.045f));
            var random = new System.Random(3107);
            for (int i = 0; i < 150; i++)
            {
                var point = new Vector2((float)random.NextDouble() * 1800 - 900,
                    (float)random.NextDouble() * 1000 - 500);
                float size = i % 7 == 0 ? 2 : 0.7f;
                Stroke(mesh, point + Vector2.left * size, point + Vector2.right * size, size,
                    new Color(0.85f, 0.77f, 0.52f, i % 7 == 0 ? 0.23f : 0.08f));
            }
        }

        private static Vector2 Polar(float radius, float degrees)
        { return new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius; }

        private static void Stroke(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * width * .5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(from - normal, tint, Vector2.zero);
            mesh.AddVert(from + normal, tint, Vector2.zero);
            mesh.AddVert(to + normal, tint, Vector2.zero);
            mesh.AddVert(to - normal, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }

    internal static class DuelArt
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static Font _displayFont;
        public static Font DisplayFont => _displayFont != null ? _displayFont :
            (_displayFont = Resources.Load<Font>("Fonts/AoboshiOne-Regular")) ?? UiFactory.TextFont;

        public static Sprite Load(string path, bool border = false)
        {
            if (Sprites.TryGetValue(path, out Sprite sprite) && sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>("Art/" + path);
            if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect,
                border ? new Vector4(12, 12, 12, 12) : Vector4.zero);
            Sprites[path] = sprite;
            return sprite;
        }

        public static Image Icon(Transform parent, string name, string icon, Vector2 pos, float size, Color tint)
        {
            Image image = UiFactory.CreatePanel(name, parent, Vector2.one * size, pos, tint);
            image.sprite = Load("Icons/" + icon);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public static Image Frame(Transform parent, Vector2 size, Color tint)
        {
            Image frame = UiFactory.CreatePanel("Engraving", parent, size, Vector2.zero, tint);
            frame.sprite = Load("Frames/engraved", true);
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            return frame;
        }
    }
}
