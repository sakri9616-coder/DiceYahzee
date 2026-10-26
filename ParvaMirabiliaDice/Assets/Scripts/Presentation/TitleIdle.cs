using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// The title screen at rest: the sample dice float gently over their own shadows, out of step
    /// with each other, and a glint crosses the logo now and then. A still title reads as a menu;
    /// a breathing one reads as a game waiting to be picked up.
    ///
    /// Only the dice's visible parts move - the shadow and the die's own rectangle stay home, so
    /// the layout the screen was measured at is the layout it keeps.
    /// </summary>
    internal sealed class TitleIdle : MonoBehaviour
    {
        private readonly List<Floater> _dice = new List<Floater>();
        private ShimmerTextEffect _shimmer;
        private float _age;

        private struct Floater
        {
            public RectTransform[] Parts;
            public Vector2[] Homes;
            public Image Shadow;
            public Color ShadowColor;
            public float Phase;
        }

        public static TitleIdle Attach(RectTransform root, Text logo, IEnumerable<Transform> dice)
        {
            var idle = root.gameObject.AddComponent<TitleIdle>();
            if (logo != null) idle._shimmer = logo.gameObject.AddComponent<ShimmerTextEffect>();
            int index = 0;
            foreach (Transform die in dice)
            {
                if (die == null) continue;
                var parts = new List<RectTransform>();
                var homes = new List<Vector2>();
                Image shadow = null;
                foreach (Transform child in die)
                {
                    if (child.name == "Shadow") { shadow = child.GetComponent<Image>(); continue; }
                    var rect = child as RectTransform;
                    if (rect == null) continue;
                    parts.Add(rect);
                    homes.Add(rect.anchoredPosition);
                }
                idle._dice.Add(new Floater
                {
                    Parts = parts.ToArray(), Homes = homes.ToArray(), Shadow = shadow,
                    ShadowColor = shadow != null ? shadow.color : Color.clear, Phase = index++ * 1.31f
                });
            }
            return idle;
        }

        private void OnDisable() => Rest();

        private void Rest()
        {
            foreach (Floater die in _dice)
            {
                for (int i = 0; i < die.Parts.Length; i++)
                    if (die.Parts[i] != null)
                    {
                        die.Parts[i].anchoredPosition = die.Homes[i];
                        die.Parts[i].localRotation = Quaternion.identity;
                    }
                if (die.Shadow != null) die.Shadow.color = die.ShadowColor;
            }
            if (_shimmer != null) _shimmer.Band = -1f;
        }

        private void Update()
        {
            if (Juice.ReducedMotion) { Rest(); return; }
            _age += Mathf.Min(Time.unscaledDeltaTime, .1f);
            foreach (Floater die in _dice)
            {
                float lift = (Mathf.Sin(_age * 1.6f + die.Phase) * .5f + .5f) * 7f;
                float tilt = Mathf.Sin(_age * 1.1f + die.Phase * 1.7f) * 2.2f;
                for (int i = 0; i < die.Parts.Length; i++)
                {
                    if (die.Parts[i] == null) continue;
                    die.Parts[i].anchoredPosition = die.Homes[i] + Vector2.up * lift;
                    die.Parts[i].localRotation = Quaternion.Euler(0, 0, tilt);
                }
                if (die.Shadow != null)
                {
                    Color shade = die.ShadowColor;
                    shade.a *= 1f - lift / 7f * .35f;
                    die.Shadow.color = shade;
                }
            }
            // A glint every five seconds, crossing the logo in under one.
            if (_shimmer != null)
            {
                float cycle = Mathf.Repeat(_age, 5f);
                _shimmer.Band = cycle < .9f ? cycle / .9f : -1f;
            }
        }
    }

    /// <summary>
    /// A diagonal band of light that sweeps across a label's glyphs. <see cref="Band"/> runs 0..1
    /// across the label; any negative value turns the glint off.
    /// </summary>
    internal sealed class ShimmerTextEffect : BaseMeshEffect
    {
        private float _band = -1f;

        public float Band
        {
            get => _band;
            set
            {
                if (Mathf.Approximately(_band, value)) return;
                _band = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper == null || helper.currentVertCount == 0 || _band < 0f) return;
            float min = float.MaxValue, max = float.MinValue;
            UIVertex vertex = UIVertex.simpleVert;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                min = Mathf.Min(min, vertex.position.x);
                max = Mathf.Max(max, vertex.position.x);
            }
            float span = Mathf.Max(1f, max - min);
            float centre = Mathf.Lerp(min - span * .2f, max + span * .2f, _band);
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                // Slanted: the band leans with the y position so it reads as light, not a column.
                float x = vertex.position.x + vertex.position.y * .35f;
                float d = (x - centre) / (span * .09f);
                float glint = Mathf.Exp(-d * d) * .85f;
                Color32 lit = Color32.Lerp(vertex.color, new Color32(255, 250, 232, vertex.color.a), glint);
                vertex.color = lit;
                helper.SetUIVertex(vertex, i);
            }
        }
    }
}
