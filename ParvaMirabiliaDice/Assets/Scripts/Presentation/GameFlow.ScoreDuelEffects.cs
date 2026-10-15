using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private RectTransform _sdFxRoot;
        private ScoreHandEffectGraphic _sdFxGeometry;
        private readonly RectTransform[] _sdFxDice = new RectTransform[5];
        private readonly Vector3[] _sdFxBaseScale = new Vector3[5];
        private readonly Vector2[] _sdFxBasePosition = new Vector2[5];
        private readonly Vector2[] _sdFxBaseCenters = new Vector2[5];
        private readonly Vector2[] _sdFxCenters = new Vector2[5];
        private readonly int[] _sdFxValues = new int[5];
        private readonly int[] _sdFxPath = new int[5];
        private readonly int[] _sdFxGroups = new int[2];
        private readonly Image[] _sdFxSparks = new Image[28];
        private readonly Vector2[] _sdFxSparkOrigin = new Vector2[28];
        private readonly Vector2[] _sdFxSparkVelocity = new Vector2[28];
        private Image _sdFxTraceHead;
        private ScoreHandKind _sdFxLastKind;
        private int _sdFxTier, _sdFxMask, _sdFxPathCount, _sdFxSparkCount, _sdFxScaledMask, _sdFxPositionedMask;
        private float _sdFxAge = -1, _sdFxDuration;
        private bool _sdFxActive;
        private static readonly Color SDFormationColor = new Color32(133, 237, 216, 255);
        private static readonly Color SDFormationPairColor = new Color32(247, 219, 165, 255);

        private void SDBuildHandEffects()
        {
            if (_sdFxRoot != null || _sdBattle == null) return;
            _sdFxRoot = UiFactory.CreateStretch("HandEffects", _sdBattle);
            _sdFxGeometry = _sdFxRoot.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _sdFxGeometry.raycastTarget = false;
            int firstDie = _sdBattle.childCount;
            for (int i = 0; i < 5; i++)
            {
                _sdFxDice[i] = _sdBattle.Find("Die" + i) as RectTransform;
                if (_sdFxDice[i] == null) continue;
                _sdFxBaseScale[i] = _sdFxDice[i].localScale;
                firstDie = Mathf.Min(firstDie, _sdFxDice[i].GetSiblingIndex());
            }
            // Geometry sits behind the faces: a traced line never obscures their pips. Its
            // travelling head and sparks show the ordered visits between the physical dice.
            _sdFxRoot.SetSiblingIndex(firstDie);
            for (int i = 0; i < _sdFxSparks.Length; i++)
            {
                var spark = UiFactory.CreatePanel("HandSpark" + i, _sdFxRoot,
                    new Vector2(7, 7), Vector2.zero, SDFormationColor);
                spark.sprite = UiFactory.CircleSprite;
                spark.raycastTarget = false;
                spark.gameObject.SetActive(false);
                _sdFxSparks[i] = spark;
            }
            _sdFxTraceHead = UiFactory.CreatePanel("StraightHead", _sdFxRoot,
                new Vector2(16, 16), Vector2.zero, SDFormationColor);
            _sdFxTraceHead.sprite = UiFactory.CircleSprite;
            _sdFxTraceHead.raycastTarget = false;
            _sdFxTraceHead.gameObject.SetActive(false);
            _sdFxRoot.gameObject.SetActive(false);
        }

        private void SDPlayHandEffects(ScoreOffer offer)
        {
            SDResetHandEffects();
            if (_sdFxRoot == null || !offer.IsFormed || offer.Mask == 0 ||
                offer.Kind == ScoreHandKind.Chance || SDModalOpen) return;
            _sdFxLastKind = offer.Kind;
            _sdFxMask = offer.Mask & 31;
            _sdFxTier = SDHandEffectTier(offer.Kind);
            _sdFxDuration = _sdReducedMotion ? .30f : _sdFxTier == 0 ? .36f :
                _sdFxTier == 1 ? .55f : _sdFxTier == 2 ? .74f : _sdFxTier == 3 ? .84f : 1.02f;
            _sdFxAge = 0;
            _sdFxActive = true;
            if (_sdFxTier >= 2 && _sdGestureGraphic != null) _sdGestureGraphic.gameObject.SetActive(false);
            _sdFxRoot.gameObject.SetActive(true);
            _sdFxPathCount = 0;
            _sdFxGroups[0] = _sdFxGroups[1] = 0;
            for (int i = 0; i < 5; i++)
            {
                if (_sdFxDice[i] == null) continue;
                _sdFxBasePosition[i] = _sdFxDice[i].anchoredPosition;
                _sdFxBaseCenters[i] = _sdFxRoot.InverseTransformPoint(_sdFxDice[i].TransformPoint(Vector3.zero));
                _sdFxCenters[i] = _sdFxBaseCenters[i];
                _sdFxValues[i] = _sdDice[i].Value;
                if ((_sdFxMask & (1 << i)) != 0) _sdFxPath[_sdFxPathCount++] = i;
            }
            bool straight = offer.Kind == ScoreHandKind.SmallStraight || offer.Kind == ScoreHandKind.LargeStraight;
            if (straight)
            {
                // Sort the physical die indices by actual face, not by screen slot. This makes
                // a scrambled [4,1,5,2,3] trace 1 -> 2 -> 3 -> 4 -> 5 across its real positions.
                for (int i = 1; i < _sdFxPathCount; i++)
                {
                    int die = _sdFxPath[i], at = i;
                    while (at > 0 && _sdFxValues[_sdFxPath[at - 1]] > _sdFxValues[die])
                    { _sdFxPath[at] = _sdFxPath[at - 1]; at--; }
                    _sdFxPath[at] = die;
                }
            }
            if (offer.Kind == ScoreHandKind.FullHouse)
            {
                for (int i = 0; i < 5; i++)
                {
                    int count = 0;
                    for (int j = 0; j < 5; j++) if (_sdFxValues[j] == _sdFxValues[i]) count++;
                    if (count == 3) _sdFxGroups[0] |= 1 << i;
                    else if (count == 2) _sdFxGroups[1] |= 1 << i;
                }
            }
            _sdFxSparkCount = _sdReducedMotion ? 0 : _sdFxTier == 0 ? 3 :
                _sdFxTier == 1 ? 8 : _sdFxTier == 2 ? 14 : _sdFxTier == 3 ? 19 : 28;
            for (int i = 0; i < _sdFxSparkCount; i++)
            {
                int die = _sdFxPath[i % _sdFxPathCount];
                float angle = (i * 137.508f + (int)offer.Kind * 17.7f) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _sdFxSparkOrigin[i] = _sdFxCenters[die] + direction * 62f;
                _sdFxSparkVelocity[i] = direction * (42 + _sdFxTier * 22 + (i % 4) * 10);
                _sdFxSparks[i].gameObject.SetActive(true);
            }
            SDTickHandEffects(0);
        }

        private static int SDHandEffectTier(ScoreHandKind kind)
        {
            switch (kind)
            {
                case ScoreHandKind.Pair: return 0;
                case ScoreHandKind.TwoPair:
                case ScoreHandKind.Three: return 1;
                case ScoreHandKind.FullHouse:
                case ScoreHandKind.SmallStraight: return 2;
                case ScoreHandKind.Four:
                case ScoreHandKind.LargeStraight: return 3;
                case ScoreHandKind.Five: return 4;
                default: return 0;
            }
        }

        private void SDTickHandEffects(float deltaTime)
        {
            if (!_sdFxActive) return;
            if (SDModalOpen || _sdRolling || _sdCommitted || _sdBattle == null || !_sdBattle.gameObject.activeInHierarchy)
            { SDResetHandEffects(); return; }
            _sdFxAge += Mathf.Max(0, deltaTime);
            if (_sdFxAge >= _sdFxDuration) { SDResetHandEffects(); return; }
            float progress = Mathf.Clamp01(_sdFxAge / _sdFxDuration);
            float fade = Mathf.Clamp01((1 - progress) * 3.3f);
            SDSpreadHandEffects(progress);
            _sdFxGeometry.Clear();
            bool straight = _sdFxLastKind == ScoreHandKind.SmallStraight || _sdFxLastKind == ScoreHandKind.LargeStraight;
            if (straight) SDDrawStraight(progress, fade);
            else if (_sdFxLastKind == ScoreHandKind.FullHouse) SDDrawFullHouse(progress, fade);
            else SDDrawMatchingHand(progress, fade);
            _sdFxGeometry.Flush();

            for (int i = 0; i < _sdFxSparkCount; i++)
            {
                float age = Mathf.Max(0, _sdFxAge - (i % 5) * .014f);
                float life = _sdFxDuration * (.55f + (i % 3) * .06f);
                float t = Mathf.Clamp01(age / life);
                Image spark = _sdFxSparks[i];
                int die = _sdFxPath[i % _sdFxPathCount];
                spark.rectTransform.anchoredPosition = _sdFxSparkOrigin[i] + _sdFxCenters[die] - _sdFxBaseCenters[die] + _sdFxSparkVelocity[i] * age +
                    new Vector2(0, -65f * age * age);
                float size = (5.5f + _sdFxTier * 1.8f) * (1 - t * .65f);
                spark.rectTransform.sizeDelta = new Vector2(size, size);
                Color color = _sdFxLastKind == ScoreHandKind.Five ? SDFormationPairColor : SDFormationColor;
                color.a = (1 - t) * fade * .9f;
                spark.color = color;
            }
        }

        private void SDDrawStraight(float progress, float fade)
        {
            float travel = _sdReducedMotion ? _sdFxPathCount - 1 :
                Mathf.Clamp01(progress / .78f) * (_sdFxPathCount - 1);
            Color color = SDFormationColor;
            color.a = fade * (_sdReducedMotion ? .55f : .9f);
            for (int at = 0; at < _sdFxPathCount - 1; at++)
            {
                float length = Mathf.Clamp01(travel - at);
                if (length <= 0) break;
                Vector2 a = _sdFxCenters[_sdFxPath[at]], b = _sdFxCenters[_sdFxPath[at + 1]];
                _sdFxGeometry.Line(a, Vector2.Lerp(a, b, length), _sdReducedMotion ? 2 : 3.6f, color);
            }
            int current = Mathf.Min(_sdFxPathCount - 1, Mathf.FloorToInt(travel));
            int next = Mathf.Min(_sdFxPathCount - 1, current + 1);
            Vector2 head = Vector2.Lerp(_sdFxCenters[_sdFxPath[current]], _sdFxCenters[_sdFxPath[next]], travel - current);
            _sdFxTraceHead.gameObject.SetActive(!_sdReducedMotion);
            _sdFxTraceHead.rectTransform.anchoredPosition = head;
            _sdFxTraceHead.color = color;
            for (int i = 0; i < _sdFxPathCount; i++)
            {
                float arrived = travel - i;
                if (arrived < -.05f) continue;
                int die = _sdFxPath[i];
                float pulse = _sdReducedMotion ? 0 : Mathf.Max(0, 1 - Mathf.Abs(arrived) * 1.5f);
                SDScaleEffectDie(die, 1 + pulse * (_sdFxTier == 3 ? .19f : .14f));
                _sdFxGeometry.Ring(_sdFxCenters[die], 62 + pulse * 4, _sdReducedMotion ? 1.5f : 2.4f, color, 20);
            }
        }

        private void SDDrawFullHouse(float progress, float fade)
        {
            Color three = SDFormationColor, pair = SDFormationPairColor;
            three.a = pair.a = fade * (_sdReducedMotion ? .48f : .83f);
            float triplePulse = _sdReducedMotion ? 0 : SDPulse(progress, .18f, .26f);
            float pairPulse = _sdReducedMotion ? 0 : SDPulse(progress, .46f, .26f);
            SDDrawGroup(_sdFxGroups[0], three, triplePulse, .23f);
            SDDrawGroup(_sdFxGroups[1], pair, pairPulse, .23f);
            if (!_sdReducedMotion)
            {
                Vector2 a = SDGroupCenter(_sdFxGroups[0]), b = SDGroupCenter(_sdFxGroups[1]);
                Color joining = SDFormationColor;
                joining.a = fade * Mathf.Clamp01((progress - .24f) * 4) * .62f;
                _sdFxGeometry.Line(a, b, 3, joining);
            }
        }

        private void SDDrawGroup(int mask, Color color, float pulse, float enlargement)
        {
            int previous = -1;
            for (int i = 0; i < 5; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                SDScaleEffectDie(i, 1 + pulse * enlargement);
                _sdFxGeometry.Ring(_sdFxCenters[i], 63 + pulse * 7, _sdReducedMotion ? 1.4f : 2.6f, color, 20);
                if (previous >= 0) _sdFxGeometry.Line(_sdFxCenters[previous], _sdFxCenters[i], 2.8f, color);
                previous = i;
            }
        }

        private void SDDrawMatchingHand(float progress, float fade)
        {
            float pulse = _sdReducedMotion ? 0 : SDPulse(progress, .23f, .45f);
            float enlargement = _sdFxTier == 4 ? .32f : _sdFxTier == 3 ? .23f : _sdFxTier == 1 ? .12f : .065f;
            Color color = _sdFxTier == 4 ? SDFormationPairColor : SDFormationColor;
            color.a = fade * (_sdReducedMotion ? .42f : .8f);
            SDDrawGroup(_sdFxMask, color, pulse, enlargement);
            if (_sdFxTier < 3 || _sdReducedMotion) return;
            Vector2 center = SDGroupCenter(_sdFxMask);
            for (int i = 0; i < (_sdFxTier == 4 ? 14 : 8); i++)
            {
                float angle = (i * 360f / (_sdFxTier == 4 ? 14 : 8)) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 a = center + direction * (68 + progress * 20);
                Vector2 b = a + direction * (10 + pulse * (_sdFxTier == 4 ? 33 : 16));
                _sdFxGeometry.Line(a, b, _sdFxTier == 4 ? 3.6f : 2.6f, color);
            }
            if (_sdFxTier == 4)
            {
                // A second, broad pulse is confined to the hand, rather than washing out HP
                // and controls. Its size follows the current layout's physical dice bounds.
                float left = float.MaxValue, right = float.MinValue;
                for (int i = 0; i < 5; i++)
                { left = Mathf.Min(left, _sdFxCenters[i].x); right = Mathf.Max(right, _sdFxCenters[i].x); }
                _sdFxGeometry.Ellipse(new Vector2((left + right) * .5f, center.y),
                    (right - left) * .5f + 62 + pulse * 10, 57 + pulse * 8, 2.4f, color, 36);
            }
        }

        private static float SDPulse(float progress, float peak, float width)
        {
            float t = Mathf.Clamp01(1 - Mathf.Abs(progress - peak) / width);
            return t * t * (3 - 2 * t);
        }

        private void SDSpreadHandEffects(float progress)
        {
            float spread = 0;
            if (!_sdReducedMotion)
            {
                if (_sdFxLastKind == ScoreHandKind.Five)
                    spread = 30 * SDPulse(progress, .23f, .45f);
                else if (_sdFxLastKind == ScoreHandKind.FullHouse)
                    // Both groups share the spacing pulse. Moving only one group inward
                    // would let an enlarged die collide with its stationary neighbour.
                    spread = 14 * Mathf.Max(SDPulse(progress, .18f, .26f), SDPulse(progress, .46f, .26f));
            }
            for (int i = 0; i < 5; i++)
            {
                if (_sdFxDice[i] == null) continue;
                _sdFxCenters[i] = _sdFxBaseCenters[i];
                if (spread == 0 && _sdFxPositionedMask == 0) continue;
                Vector2 offset = Vector2.right * ((i - 2) * spread);
                _sdFxDice[i].anchoredPosition = _sdFxBasePosition[i] + offset;
                _sdFxCenters[i] += (Vector2)_sdFxRoot.InverseTransformVector(_sdFxDice[i].parent.TransformVector(offset));
                _sdFxPositionedMask |= 1 << i;
            }
        }

        private Vector2 SDGroupCenter(int mask)
        {
            Vector2 center = Vector2.zero;
            int count = 0;
            for (int i = 0; i < 5; i++) if ((mask & (1 << i)) != 0) { center += _sdFxCenters[i]; count++; }
            return count == 0 ? Vector2.zero : center / count;
        }

        private void SDScaleEffectDie(int index, float scale)
        {
            if (_sdReducedMotion || _sdFxDice[index] == null) return;
            _sdFxScaledMask |= 1 << index;
            // Absolute, cached-base scaling runs after DiceView.Tick. It cannot multiply its
            // own last frame or fight the normal landing punch with accumulating transforms.
            _sdFxDice[index].localScale = _sdFxBaseScale[index] * scale;
        }

        private void SDResetHandEffects()
        {
            _sdFxActive = false;
            _sdFxAge = -1;
            for (int i = 0; i < 5; i++)
            {
                if (_sdFxDice[i] == null) continue;
                if ((_sdFxScaledMask & (1 << i)) != 0) _sdFxDice[i].localScale = _sdFxBaseScale[i];
                if ((_sdFxPositionedMask & (1 << i)) != 0) _sdFxDice[i].anchoredPosition = _sdFxBasePosition[i];
                _sdFxCenters[i] = _sdFxBaseCenters[i];
            }
            _sdFxScaledMask = _sdFxPositionedMask = 0;
            _sdFxSparkCount = 0;
            if (_sdFxGeometry != null) { _sdFxGeometry.Clear(); _sdFxGeometry.Flush(); }
            foreach (Image spark in _sdFxSparks) if (spark != null) spark.gameObject.SetActive(false);
            if (_sdFxTraceHead != null) _sdFxTraceHead.gameObject.SetActive(false);
            if (_sdFxRoot != null) _sdFxRoot.gameObject.SetActive(false);
        }
    }

    /// <summary>A bounded stroke mesh for transient effects; never an input target.</summary>
    internal sealed class ScoreHandEffectGraphic : MaskableGraphic
    {
        private struct Stroke { public Vector2 A, B; public float Width; public Color32 Color; }
        private readonly Stroke[] _strokes = new Stroke[220];
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
}
