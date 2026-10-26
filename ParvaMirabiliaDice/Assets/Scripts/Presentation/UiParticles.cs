using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// A bounded, pooled particle field drawn as one UI mesh: confetti, sparks, embers and dust.
    ///
    /// Every particle is a quad written straight into this graphic's vertices, so a burst of a
    /// hundred pieces costs one draw and no GameObjects. The pool is fixed at construction and a
    /// spawn into a full pool recycles the oldest piece, so no celebration can grow without bound.
    /// It is never a raycast target: nothing it draws can swallow a click meant for the table.
    /// </summary>
    internal sealed class UiParticles : MaskableGraphic
    {
        internal struct Particle
        {
            public Vector2 Position, Velocity;
            public Vector2 Size;
            public float Rotation, Spin;
            public float Age, Life;
            public float Gravity, Drag;
            /// <summary>Side-to-side drift amplitude in units per second; confetti flutters, sparks do not.</summary>
            public float Flutter, Phase;
            public Color32 Color;
            /// <summary>0..1: how long the piece holds full alpha before fading out.</summary>
            public float Hold;
            /// <summary>Spinning paper shows its edge: the quad's width breathes with this rate.</summary>
            public float Tumble;
        }

        private Particle[] _pool = new Particle[0];
        private int _count;
        private int _next;
        private bool _round;
        /// <summary>When set, the field wraps pieces at its edges instead of retiring them (ambient dust).</summary>
        public Rect? WrapBounds;

        public int Count => _count;

        public static UiParticles Create(string name, Transform parent, int capacity, bool round)
        {
            RectTransform rect = UiFactory.CreateStretch(name, parent);
            var field = UiFactory.AddVertexGraphic<UiParticles>(rect.gameObject);
            field._pool = new Particle[Mathf.Max(1, capacity)];
            field._round = round;
            field.raycastTarget = false;
            return field;
        }

        public override Texture mainTexture
            => _round ? UiFactory.CircleSprite.texture : s_WhiteTexture;

        public void Clear()
        {
            _count = 0;
            _next = 0;
            SetVerticesDirty();
        }

        public void Spawn(Particle particle)
        {
            if (_pool.Length == 0) return;
            if (particle.Life <= 0f) particle.Life = 1f;
            if (_count < _pool.Length)
            {
                _pool[_count++] = particle;
                return;
            }
            // Full: recycle the oldest slot in rotation, never grow.
            _pool[_next] = particle;
            _next = (_next + 1) % _pool.Length;
        }

        /// <summary>Advances every piece and retires the ones whose life is spent.</summary>
        public void Step(float dt)
        {
            if (_count == 0) return;
            for (int i = 0; i < _count; i++)
            {
                ref Particle p = ref _pool[i];
                p.Age += dt;
                float drag = Mathf.Exp(-p.Drag * dt);
                p.Velocity *= drag;
                p.Velocity.y -= p.Gravity * dt;
                p.Position += p.Velocity * dt;
                if (p.Flutter != 0f) p.Position.x += Mathf.Sin(p.Age * 5.3f + p.Phase) * p.Flutter * dt;
                p.Rotation += p.Spin * dt;
                if (WrapBounds.HasValue)
                {
                    Rect bounds = WrapBounds.Value;
                    if (p.Position.y > bounds.yMax) p.Position.y = bounds.yMin;
                    if (p.Position.y < bounds.yMin) p.Position.y = bounds.yMax;
                    if (p.Position.x > bounds.xMax) p.Position.x = bounds.xMin;
                    if (p.Position.x < bounds.xMin) p.Position.x = bounds.xMax;
                    if (p.Age >= p.Life) p.Age -= p.Life;
                }
            }
            if (!WrapBounds.HasValue)
            {
                int write = 0;
                for (int read = 0; read < _count; read++)
                    if (_pool[read].Age < _pool[read].Life)
                        _pool[write++] = _pool[read];
                _count = write;
                if (_next >= _count) _next = 0;
            }
            SetVerticesDirty();
        }

        private static float AlphaFor(in Particle p, bool looping)
        {
            float t = Mathf.Clamp01(p.Age / p.Life);
            if (looping)
                return Mathf.Sin(t * Mathf.PI);
            float fadeIn = Mathf.Clamp01(p.Age / .06f);
            float hold = Mathf.Clamp01(p.Hold);
            float fadeOut = t <= hold ? 1f : 1f - (t - hold) / Mathf.Max(.0001f, 1f - hold);
            return fadeIn * Mathf.Clamp01(fadeOut);
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            UIVertex vertex = UIVertex.simpleVert;
            bool looping = WrapBounds.HasValue;
            for (int i = 0; i < _count; i++)
            {
                Particle p = _pool[i];
                float alpha = AlphaFor(p, looping);
                if (alpha <= .003f) continue;
                Color32 color = p.Color;
                color.a = (byte)(color.a * alpha);
                float width = p.Size.x;
                if (p.Tumble != 0f) width *= .25f + .75f * Mathf.Abs(Mathf.Cos(p.Age * p.Tumble + p.Phase));
                float rad = p.Rotation * Mathf.Deg2Rad;
                Vector2 ax = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (width * .5f);
                Vector2 ay = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (p.Size.y * .5f);
                int first = helper.currentVertCount;
                vertex.color = color;
                vertex.position = p.Position - ax - ay; vertex.uv0 = new Vector2(0, 0); helper.AddVert(vertex);
                vertex.position = p.Position - ax + ay; vertex.uv0 = new Vector2(0, 1); helper.AddVert(vertex);
                vertex.position = p.Position + ax + ay; vertex.uv0 = new Vector2(1, 1); helper.AddVert(vertex);
                vertex.position = p.Position + ax - ay; vertex.uv0 = new Vector2(1, 0); helper.AddVert(vertex);
                helper.AddTriangle(first, first + 1, first + 2);
                helper.AddTriangle(first + 2, first + 3, first);
            }
        }
    }

    /// <summary>
    /// A fan of soft light rays turning slowly behind a headline. Each ray is a triangle whose tip
    /// sits at the centre at full colour and whose base fades to nothing, so the fan reads as light
    /// rather than as a pinwheel of shapes.
    /// </summary>
    internal sealed class RayBurst : MaskableGraphic
    {
        public int Rays = 16;
        public float Radius = 520f;
        public float Angle;
        public float Width = .11f;
        public Color Core = new Color(1f, .86f, .52f, .16f);
        public float Scale = 1f;

        public static RayBurst Create(string name, Transform parent, Vector2 position)
        {
            RectTransform rect = UiFactory.CreateRect(name, parent, new Vector2(10, 10), position);
            var burst = UiFactory.AddVertexGraphic<RayBurst>(rect.gameObject);
            burst.raycastTarget = false;
            return burst;
        }

        public void Redraw() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (Scale <= .001f || Core.a <= .001f) return;
            UIVertex vertex = UIVertex.simpleVert;
            Color32 tip = Core;
            Color32 edge = Core; edge.a = 0;
            for (int i = 0; i < Rays; i++)
            {
                float baseAngle = Angle * Mathf.Deg2Rad + i * Mathf.PI * 2f / Rays;
                // Alternate long and short rays so the fan never reads as a regular gear.
                float reach = Radius * Scale * (i % 2 == 0 ? 1f : .72f);
                float half = Width * (i % 2 == 0 ? 1f : .7f);
                Vector2 a = new Vector2(Mathf.Cos(baseAngle - half), Mathf.Sin(baseAngle - half)) * reach;
                Vector2 b = new Vector2(Mathf.Cos(baseAngle + half), Mathf.Sin(baseAngle + half)) * reach;
                int first = helper.currentVertCount;
                vertex.color = tip; vertex.position = Vector2.zero; helper.AddVert(vertex);
                vertex.color = edge; vertex.position = a; helper.AddVert(vertex);
                vertex.position = b; helper.AddVert(vertex);
                helper.AddTriangle(first, first + 1, first + 2);
            }
        }
    }
}
