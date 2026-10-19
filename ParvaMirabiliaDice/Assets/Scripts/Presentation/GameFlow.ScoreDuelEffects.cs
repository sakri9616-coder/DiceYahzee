using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        // Single-hand formation adapter. The single score adapter owns one shared
        // ScoreFormationPresenter so the online own-hand path can attach a second
        // presenter over its own dice without duplicating the recipe.
        private ScoreFormationPresenter _sdFxPresenter;
        private readonly RectTransform[] _sdFxDice = new RectTransform[5];
        private readonly Vector3[] _sdFxBaseScale = new Vector3[5];
        // Compatibility snapshots mirrored from the presenter per public test contract.
        private readonly Vector2[] _sdFxCenters = new Vector2[5];
        private RectTransform _sdFxRoot;
        private ScoreHandEffectGraphic _sdFxGeometry;
        private Image[] _sdFxSparks = new Image[0];
        private Image _sdFxTraceHead;
        private int[] _sdFxPath = new int[5];
        private int[] _sdFxGroups = new int[2];
        private int _sdFxPathCount;
        private int _sdFxSparkCount;
        private ScoreHandKind _sdFxLastKind;
        private int _sdFxTier, _sdFxMask;
        private float _sdFxAge = -1, _sdFxDuration;
        private bool _sdFxActive;

        // Chance particles have their own lifetime so formation effects cannot leave them behind.
        private RectTransform _sdChanceGlowRoot;
        private ScoreHandEffectGraphic _sdChanceGlowGeometry;
        private float _sdChanceGlowAge;
        private RectTransform _onlineChanceGlowRoot;
        private ScoreHandEffectGraphic _onlineChanceGlowGeometry;
        private float _onlineChanceGlowAge;
        private static readonly Color SDFormationColor = new Color32(133, 237, 216, 255);
        private static readonly Color SDFormationPairColor = new Color32(247, 219, 165, 255);

        private void SDBuildHandEffects()
        {
            if (_sdFxPresenter != null || _sdBattle == null) return;
            int firstDie = _sdBattle.childCount;
            for (int i = 0; i < 5; i++)
            {
                _sdFxDice[i] = _sdBattle.Find("Die" + i) as RectTransform;
                if (_sdFxDice[i] == null) continue;
                _sdFxBaseScale[i] = _sdFxDice[i].localScale;
                firstDie = Mathf.Min(firstDie, _sdFxDice[i].GetSiblingIndex());
            }
            _sdFxPresenter = new ScoreFormationPresenter(
                _sdBattle, _sdFxDice, SDIsReducedMotion, firstDie);
            _sdFxRoot = _sdFxPresenter.Root;
            _sdFxGeometry = _sdFxPresenter.Geometry;
            _sdFxSparks = _sdFxPresenter.Sparks;
            _sdFxTraceHead = _sdFxPresenter.TraceHead;
            _sdFxPath = _sdFxPresenter.Path;
            _sdFxGroups = _sdFxPresenter.Groups;
            // Chance stays behind the dice and cannot intercept a pointer gesture.
            _sdChanceGlowRoot = UiFactory.CreateStretch("ChanceGlow", _sdBattle);
            _sdChanceGlowGeometry = _sdChanceGlowRoot.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _sdChanceGlowGeometry.raycastTarget = false;
            _sdChanceGlowRoot.SetSiblingIndex(firstDie);
            _sdChanceGlowRoot.gameObject.SetActive(false);
        }

        private bool SDIsReducedMotion() => _sdReducedMotion;

        private static int SDHandEffectTier(ScoreHandKind kind) => ScoreFormationPresenter.TierFor(kind);

        private static float SDPulse(float progress, float peak, float width)
            => ScoreFormationPresenter.Pulse(progress, peak, width);

        private int[] _sdFxValuesBuffer = new int[5];

        private void SDPlayHandEffects(ScoreOffer offer)
        {
            SDResetHandEffects();
            if (_sdFxPresenter == null || _sdDice == null || _sdDice.Count < 5) return;
            if (SDModalOpen) return;
            for (int i = 0; i < 5; i++) _sdFxValuesBuffer[i] = _sdDice[i].Value;
            _sdFxPresenter.Play(offer, _sdFxValuesBuffer);
            SyncSingleFormationSnapshot();
        }

        private void SDTickHandEffects(float deltaTime)
        {
            if (_sdFxPresenter == null) return;
            if (_sdFxPresenter.Active &&
                (SDModalOpen || _sdRolling || _sdCommitted || _sdBattle == null || !_sdBattle.gameObject.activeInHierarchy))
            {
                SDResetHandEffects();
                return;
            }
            _sdFxPresenter.Tick(deltaTime);
            SyncSingleFormationSnapshot();
        }

        private void SyncSingleFormationSnapshot()
        {
            if (_sdFxPresenter == null) return;
            _sdFxActive = _sdFxPresenter.Active;
            _sdFxAge = _sdFxPresenter.Age;
            _sdFxDuration = _sdFxPresenter.Duration;
            _sdFxTier = _sdFxPresenter.Tier;
            _sdFxMask = _sdFxPresenter.Mask;
            _sdFxLastKind = _sdFxPresenter.Kind;
            _sdFxPathCount = _sdFxPresenter.PathCount;
            _sdFxSparkCount = _sdFxPresenter.SparkCount;
            Vector2[] centers = _sdFxPresenter.Centers;
            for (int i = 0; i < 5; i++) _sdFxCenters[i] = centers[i];
        }

        /// <summary>
        /// A bounded set of faint crimson UI particles surrounds each die while Chance is selected.
        /// It follows the physical die transforms and pauses to a static glow in reduced motion.
        /// </summary>
        private void SDTickChanceGlow(float dt)
        {
            if (_sdChanceGlowRoot == null) return;
            if (_sdSelectedKind != ScoreHandKind.Chance || !SDCanPlan || _sdFxActive)
            {
                if (_sdChanceGlowRoot.gameObject.activeSelf) SDResetChanceGlow();
                return;
            }
            _sdChanceGlowAge += Mathf.Max(0, dt);
            SDDrawChanceParticles(_sdChanceGlowRoot, _sdChanceGlowGeometry, _sdFxDice, _sdChanceGlowAge);
            _sdChanceGlowRoot.gameObject.SetActive(true);
        }

        private void SDBuildOnlineChanceGlow()
        {
            if (_onlineChanceGlowRoot != null || _onlineRoot == null) return;
            _onlineChanceGlowRoot = UiFactory.CreateStretch("OnlineChanceGlow", _onlineRoot);
            _onlineChanceGlowGeometry = _onlineChanceGlowRoot.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _onlineChanceGlowGeometry.raycastTarget = false;
            if (_onlineOwnDice[0] != null) _onlineChanceGlowRoot.SetSiblingIndex(_onlineOwnDice[0].GetSiblingIndex());
            _onlineChanceGlowRoot.gameObject.SetActive(false);
        }

        private void SDTickOnlineChanceGlow(float dt)
        {
            if (_onlineChanceGlowRoot == null) return;
            bool selected = _onlineState != null && !_onlineStopped && !OnlineHelpOpen &&
                _onlineState.phase != (int)VersusPhase.Finished &&
                _onlineState.players[OnlineMe].submitted == false && _onlineKind == (int)ScoreHandKind.Chance &&
                OnlineCanChoose;
            if (!selected) { SDResetOnlineChanceGlow(); return; }
            _onlineChanceGlowAge += Mathf.Max(0, dt);
            SDDrawChanceParticles(_onlineChanceGlowRoot, _onlineChanceGlowGeometry, _onlineOwnDice, _onlineChanceGlowAge);
            _onlineChanceGlowRoot.gameObject.SetActive(true);
        }

        private void SDResetOnlineChanceGlow()
        {
            _onlineChanceGlowAge = 0;
            if (_onlineChanceGlowRoot != null) _onlineChanceGlowRoot.gameObject.SetActive(false);
        }

        private void SDDrawChanceParticles(RectTransform root, ScoreHandEffectGraphic graphic,
            RectTransform[] dice, float age)
        {
            graphic.Clear();
            // Roughly twice the prior apparent brightness: thicker rings and stronger alpha,
            // but still soft glowing halos rather than solid disks.
            Color inner = new Color(.97f, .16f, .21f, .28f);
            Color middle = new Color(1f, .11f, .18f, .18f);
            Color outer = new Color(1f, .08f, .14f, .10f);
            for (int i = 0; i < 5; i++)
            {
                if (dice[i] == null) continue;
                Vector2 center = root.InverseTransformPoint(dice[i].TransformPoint(Vector3.zero));
                float radius = dice[i].rect.width * .5f + 6f;
                float breathe = _sdReducedMotion ? 0 : Mathf.Sin(age * 2.3f + i * .8f) * 1.5f;
                graphic.Ring(center, radius + breathe, 9, inner, 24);
                graphic.Ring(center, radius + 7 + breathe, 11, middle, 24);
                graphic.Ring(center, radius + 15 + breathe, 14, outer, 24);
                if (_sdReducedMotion) continue;
                for (int p = 0; p < 4; p++)
                {
                    float phase = Mathf.Repeat(age * (.35f + p * .035f) + i * .17f + p * .23f, 1);
                    float angle = i * 1.4f + p * 1.57f + phase * .42f;
                    Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 at = center + radial * (radius + 5 + phase * 14);
                    Color particle = new Color(1, .23f, .28f, Mathf.Sin(phase * Mathf.PI) * .54f);
                    graphic.Line(at, at + radial * 2f, 3.5f + (1 - phase) * 2f, particle);
                }
            }
            graphic.Flush();
        }

        private void SDResetChanceGlow()
        {
            _sdChanceGlowAge = 0;
            if (_sdChanceGlowGeometry != null) { _sdChanceGlowGeometry.Clear(); _sdChanceGlowGeometry.Flush(); }
            if (_sdChanceGlowRoot != null) _sdChanceGlowRoot.gameObject.SetActive(false);
        }

        private void SDResetHandEffects()
        {
            _sdFxActive = false;
            _sdFxAge = -1;
            _sdFxPathCount = 0;
            _sdFxSparkCount = 0;
            if (_sdFxDice != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (_sdFxDice[i] == null) continue;
                    if (_sdFxBaseScale[i] != Vector3.zero) _sdFxDice[i].localScale = _sdFxBaseScale[i];
                    _sdFxCenters[i] = Vector2.zero;
                }
            }
            if (_sdFxPresenter != null) _sdFxPresenter.Reset();
            if (_sdFxRoot != null) _sdFxRoot.gameObject.SetActive(false);
            if (_sdFxTraceHead != null) _sdFxTraceHead.gameObject.SetActive(false);
            if (_sdFxSparks != null)
                for (int i = 0; i < _sdFxSparks.Length; i++) if (_sdFxSparks[i] != null) _sdFxSparks[i].gameObject.SetActive(false);
            if (_sdFxGeometry != null) { _sdFxGeometry.Clear(); _sdFxGeometry.Flush(); }
        }
    }

    /// <summary>A bounded stroke mesh for transient effects; never an input target.</summary>
    internal sealed class ScoreHandEffectGraphic : MaskableGraphic
    {
        private struct Stroke { public Vector2 A, B; public float Width; public Color32 Color; }
        private readonly Stroke[] _strokes = new Stroke[512];
        private int _count;
        public void Clear() { _count = 0; }
        public void Flush() { SetVerticesDirty(); }
        public void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            if (_count == _strokes.Length || (b - a).sqrMagnitude < .001f || color.a <= .001f) return;
            _strokes[_count++] = new Stroke { A = a, B = b, Width = width, Color = color };
        }
        public void Ring(Vector2 center, float radius, float width, Color color, int steps)
            => Ellipse(center, radius, radius, width, color, steps);
        public void Ellipse(Vector2 center, float radiusX, float radiusY, float width, Color color, int steps)
        {
            Vector2 previous = center + new Vector2(radiusX, 0);
            for (int i = 1; i <= steps; i++)
            {
                float angle = i * Mathf.PI * 2 / steps;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
                Line(previous, next, width, color);
                previous = next;
            }
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            UIVertex vertex = UIVertex.simpleVert;
            for (int i = 0; i < _count; i++)
            {
                Stroke stroke = _strokes[i];
                Vector2 delta = stroke.B - stroke.A;
                Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (stroke.Width * .5f);
                int first = helper.currentVertCount;
                vertex.color = stroke.Color;
                vertex.position = stroke.A + normal; helper.AddVert(vertex);
                vertex.position = stroke.B + normal; helper.AddVert(vertex);
                vertex.position = stroke.B - normal; helper.AddVert(vertex);
                vertex.position = stroke.A - normal; helper.AddVert(vertex);
                helper.AddTriangle(first, first + 1, first + 2);
                helper.AddTriangle(first + 2, first + 3, first);
            }
        }
    }

    /// <summary>
    /// Struck metal for a score numeral: a top-lit gold gradient is written into the glyph's own
    /// vertices, so the figure reads as a carved object rather than as flat paint. It runs as a
    /// mesh effect rather than as a second, tinted copy, which keeps any outline added afterwards
    /// seeing one tinted glyph to darken instead of two overlapping ones.
    /// </summary>
    internal sealed class SdMetallicTextEffect : BaseMeshEffect
    {
        private static readonly Color32 MetalTop = new Color32(255, 242, 204, 255);
        private static readonly Color32 MetalBottom = new Color32(172, 119, 28, 255);

        public override void ModifyMesh(VertexHelper helper)
        {
            if (helper == null || helper.currentVertCount == 0) return;
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
                Color32 metal = Color32.Lerp(MetalBottom, MetalTop, (vertex.position.y - min) / span);
                // A fade on the label must survive the recolour, so the glyph's alpha is kept.
                metal.a = (byte)(metal.a * vertex.color.a / 255);
                vertex.color = metal;
                helper.SetUIVertex(vertex, i);
            }
        }
    }
}
