using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Cosmetic formation effect shared by the single and online own-hand adapters. It
    /// consumes real settled dice views and a real role offer; it never reads or writes
    /// score state. Every animated transform is restored to its cached base on Reset so the
    /// effect cannot leak into a roll, attack, drag or the next session.
    /// </summary>
    internal sealed class ScoreFormationPresenter
    {
        private readonly RectTransform _root;
        private readonly IScoreEffectSurface _geometry;
        private readonly RectTransform[] _dice;
        private readonly System.Func<bool> _reducedMotion;
        private readonly Vector3[] _baseScale = new Vector3[5];
        private readonly Vector2[] _basePosition = new Vector2[5];
        private readonly Vector2[] _baseCenters = new Vector2[5];
        private readonly Vector2[] _centers = new Vector2[5];
        private readonly int[] _values = new int[5];
        private readonly int[] _path = new int[5];
        private readonly int[] _groups = new int[2];
        private readonly Image[] _sparks = new Image[28];
        private readonly Vector2[] _sparkOrigin = new Vector2[28];
        private readonly Vector2[] _sparkVelocity = new Vector2[28];
        private readonly Image _traceHead;
        private ScoreHandKind _kind;
        private int _tier, _mask, _pathCount, _sparkCount, _scaledMask, _positionedMask;
        private float _age = -1, _duration;
        private bool _active;

        private static readonly Color FormationColor = new Color32(133, 237, 216, 255);
        private static readonly Color FormationPairColor = new Color32(247, 219, 165, 255);

        internal ScoreFormationPresenter(Transform parent, RectTransform[] dice, System.Func<bool> reducedMotion, int firstDie)
        {
            _dice = dice;
            _reducedMotion = reducedMotion;
            _root = UiFactory.CreateStretch("HandEffects", parent);
            _geometry = UiFactory.AddVertexGraphic<ScoreHandEffectGraphic>(_root.gameObject);
            for (int i = 0; i < 5; i++)
            {
                if (_dice[i] == null) continue;
                _baseScale[i] = _dice[i].localScale;
                firstDie = Mathf.Min(firstDie, _dice[i].GetSiblingIndex());
                _basePosition[i] = _dice[i].anchoredPosition;
                _baseCenters[i] = _root.InverseTransformPoint(_dice[i].TransformPoint(Vector3.zero));
                _centers[i] = _baseCenters[i];
            }
            _root.SetSiblingIndex(Mathf.Max(0, firstDie));
            for (int i = 0; i < _sparks.Length; i++)
            {
                var spark = UiFactory.CreatePanel("HandSpark" + i, _root,
                    new Vector2(7, 7), Vector2.zero, FormationColor);
                spark.sprite = UiFactory.CircleSprite;
                spark.raycastTarget = false;
                spark.gameObject.SetActive(false);
                _sparks[i] = spark;
            }
            _traceHead = UiFactory.CreatePanel("StraightHead", _root,
                new Vector2(16, 16), Vector2.zero, FormationColor);
            _traceHead.sprite = UiFactory.CircleSprite;
            _traceHead.raycastTarget = false;
            _traceHead.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        private bool Reduced => _reducedMotion != null && _reducedMotion();

        /// <summary>Pure tier classification used by the shared recipe and by callers.</summary>
        internal static int TierFor(ScoreHandKind kind)
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

        /// <summary>Pure pulse shape shared by the recipe and by legacy callers.</summary>
        internal static float Pulse(float progress, float peak, float width)
        {
            float t = Mathf.Clamp01(1 - Mathf.Abs(progress - peak) / width);
            return t * t * (3 - 2 * t);
        }

        internal RectTransform Root => _root;
        internal IScoreEffectSurface Geometry => _geometry;
        internal Image TraceHead => _traceHead;
        internal Image[] Sparks => _sparks;
        internal bool Active => _active;
        internal float Duration => _duration;
        internal float Age => _age;
        internal ScoreHandKind Kind => _kind;
        internal int Tier => _tier;
        internal int Mask => _mask;
        internal int[] Path => _path;
        internal int PathCount => _pathCount;
        internal int[] Groups => _groups;
        internal int SparkCount => _sparkCount;
        internal Vector2[] Centers => _centers;

        /// <summary>
        /// Starts the deterministic formation cue for a settled hand. <paramref name="values"/> are the
        /// real faces shown on the five dice. Chance never runs a formation cue.
        /// </summary>
        internal void Play(ScoreOffer offer, int[] values)
        {
            Reset();
            if (_root == null) return;
            if (!offer.IsFormed || offer.Mask == 0 || offer.Kind == ScoreHandKind.Chance) return;
            _kind = offer.Kind;
            _mask = offer.Mask & 31;
            _tier = TierFor(offer.Kind);
            bool reduced = Reduced;
            _duration = reduced ? .30f : _tier == 0 ? .36f :
                _tier == 1 ? .55f : _tier == 2 ? .74f : _tier == 3 ? .84f : 1.02f;
            _age = 0;
            _active = true;
            _root.gameObject.SetActive(true);
            _pathCount = 0;
            _groups[0] = _groups[1] = 0;
            for (int i = 0; i < 5; i++)
            {
                if (_dice[i] == null) continue;
                // Use the initial idle scale captured at build; never the transient hover/formation scale.
                _basePosition[i] = _dice[i].anchoredPosition;
                _baseCenters[i] = _root.InverseTransformPoint(_dice[i].TransformPoint(Vector3.zero));
                _centers[i] = _baseCenters[i];
                _values[i] = values[i];
                if ((_mask & (1 << i)) != 0) _path[_pathCount++] = i;
            }
            if (_pathCount == 0) { Reset(); return; }
            bool straight = offer.Kind == ScoreHandKind.SmallStraight || offer.Kind == ScoreHandKind.LargeStraight;
            if (straight)
            {
                // Sort physical die indices by actual face so a scrambled hand traces 1..N
                // across its real positions rather than screen slots.
                for (int i = 1; i < _pathCount; i++)
                {
                    int die = _path[i], at = i;
                    while (at > 0 && _values[_path[at - 1]] > _values[die])
                    { _path[at] = _path[at - 1]; at--; }
                    _path[at] = die;
                }
            }
            if (offer.Kind == ScoreHandKind.FullHouse)
            {
                for (int i = 0; i < 5; i++)
                {
                    int count = 0;
                    for (int j = 0; j < 5; j++) if (_values[j] == _values[i]) count++;
                    if (count == 3) _groups[0] |= 1 << i;
                    else if (count == 2) _groups[1] |= 1 << i;
                }
            }
            // Original effects emit up to 28 sparks and wrap the die index by pathCount with modulo.
            // Do not clamp by path length; the wrap is the intended recipe.
            _sparkCount = reduced ? 0 : _tier == 0 ? 3 :
                _tier == 1 ? 8 : _tier == 2 ? 14 : _tier == 3 ? 19 : 28;
            for (int i = 0; i < _sparkCount; i++)
            {
                int die = _path[i % _pathCount];
                float angle = (i * 137.508f + (int)offer.Kind * 17.7f) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _sparkOrigin[i] = _centers[die] + direction * 62f;
                _sparkVelocity[i] = direction * (42 + _tier * 22 + (i % 4) * 10);
                _sparks[i].gameObject.SetActive(true);
            }
            Tick(0);
        }

        internal void Tick(float deltaTime)
        {
            if (!_active) return;
            if (_root == null || !_root.gameObject.activeInHierarchy) { Reset(); return; }
            _age += Mathf.Max(0, deltaTime);
            if (_age >= _duration) { Reset(); return; }
            float progress = Mathf.Clamp01(_age / _duration);
            float fade = Mathf.Clamp01((1 - progress) * 3.3f);
            bool reduced = Reduced;
            SpreadHandEffects(progress, reduced);
            _geometry.Clear();
            bool straight = _kind == ScoreHandKind.SmallStraight || _kind == ScoreHandKind.LargeStraight;
            if (straight) DrawStraight(progress, fade, reduced);
            else if (_kind == ScoreHandKind.FullHouse) DrawFullHouse(progress, fade, reduced);
            else DrawMatchingHand(progress, fade, reduced);
            _geometry.Flush();
            for (int i = 0; i < _sparkCount; i++)
            {
                float age = Mathf.Max(0, _age - (i % 5) * .014f);
                float life = _duration * (.55f + (i % 3) * .06f);
                float t = Mathf.Clamp01(age / life);
                Image spark = _sparks[i];
                int die = _path[i % _pathCount];
                spark.rectTransform.anchoredPosition = _sparkOrigin[i] + _centers[die] - _baseCenters[die] + _sparkVelocity[i] * age +
                    new Vector2(0, -65f * age * age);
                float size = (5.5f + _tier * 1.8f) * (1 - t * .65f);
                spark.rectTransform.sizeDelta = new Vector2(size, size);
                Color color = _kind == ScoreHandKind.Five ? FormationPairColor : FormationColor;
                color.a = (1 - t) * fade * .9f;
                spark.color = color;
            }
        }

        internal void Reset()
        {
            _active = false;
            _age = -1;
            for (int i = 0; i < 5; i++)
            {
                if (_dice[i] == null) continue;
                if ((_scaledMask & (1 << i)) != 0) _dice[i].localScale = _baseScale[i];
                if ((_positionedMask & (1 << i)) != 0) _dice[i].anchoredPosition = _basePosition[i];
                _centers[i] = _baseCenters[i];
            }
            _scaledMask = _positionedMask = 0;
            _sparkCount = 0;
            if (_geometry != null) { _geometry.Clear(); _geometry.Flush(); }
            for (int i = 0; i < _sparks.Length; i++) if (_sparks[i] != null) _sparks[i].gameObject.SetActive(false);
            if (_traceHead != null) _traceHead.gameObject.SetActive(false);
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void DrawStraight(float progress, float fade, bool reduced)
        {
            float travel = reduced ? _pathCount - 1 : Mathf.Clamp01(progress / .78f) * (_pathCount - 1);
            Color color = FormationColor;
            color.a = fade * (reduced ? .55f : .9f);
            for (int at = 0; at < _pathCount - 1; at++)
            {
                float length = Mathf.Clamp01(travel - at);
                if (length <= 0) break;
                Vector2 a = _centers[_path[at]], b = _centers[_path[at + 1]];
                _geometry.Line(a, Vector2.Lerp(a, b, length), reduced ? 2 : 3.6f, color);
            }
            int current = Mathf.Min(_pathCount - 1, Mathf.FloorToInt(travel));
            int next = Mathf.Min(_pathCount - 1, current + 1);
            Vector2 head = Vector2.Lerp(_centers[_path[current]], _centers[_path[next]], travel - current);
            _traceHead.gameObject.SetActive(!reduced);
            _traceHead.rectTransform.anchoredPosition = head;
            _traceHead.color = color;
            for (int i = 0; i < _pathCount; i++)
            {
                float arrived = travel - i;
                if (arrived < -.05f) continue;
                int die = _path[i];
                float pulse = reduced ? 0 : Mathf.Max(0, 1 - Mathf.Abs(arrived) * 1.5f);
                ScaleEffectDie(die, 1 + pulse * (_tier == 3 ? .19f : .14f), reduced);
                _geometry.Ring(_centers[die], 62 + pulse * 4, reduced ? 1.5f : 2.4f, color, 20);
            }
        }

        private void DrawFullHouse(float progress, float fade, bool reduced)
        {
            Color three = FormationColor, pair = FormationPairColor;
            three.a = pair.a = fade * (reduced ? .48f : .83f);
            float triplePulse = reduced ? 0 : Pulse(progress, .18f, .26f);
            float pairPulse = reduced ? 0 : Pulse(progress, .46f, .26f);
            DrawGroup(_groups[0], three, triplePulse, .23f, reduced);
            DrawGroup(_groups[1], pair, pairPulse, .23f, reduced);
            if (!reduced)
            {
                Vector2 a = GroupCenter(_groups[0]), b = GroupCenter(_groups[1]);
                Color joining = FormationColor;
                joining.a = fade * Mathf.Clamp01((progress - .24f) * 4) * .62f;
                _geometry.Line(a, b, 3, joining);
            }
        }

        private void DrawGroup(int mask, Color color, float pulse, float enlargement, bool reduced)
        {
            int previous = -1;
            for (int i = 0; i < 5; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                ScaleEffectDie(i, 1 + pulse * enlargement, reduced);
                _geometry.Ring(_centers[i], 63 + pulse * 7, reduced ? 1.4f : 2.6f, color, 20);
                if (previous >= 0) _geometry.Line(_centers[previous], _centers[i], 2.8f, color);
                previous = i;
            }
        }

        private void DrawMatchingHand(float progress, float fade, bool reduced)
        {
            float pulse = reduced ? 0 : Pulse(progress, .23f, .45f);
            float enlargement = _tier == 4 ? .32f : _tier == 3 ? .23f : _tier == 1 ? .12f : .065f;
            Color color = _tier == 4 ? FormationPairColor : FormationColor;
            color.a = fade * (reduced ? .42f : .8f);
            DrawGroup(_mask, color, pulse, enlargement, reduced);
            if (_tier < 3 || reduced) return;
            Vector2 center = GroupCenter(_mask);
            for (int i = 0; i < (_tier == 4 ? 14 : 8); i++)
            {
                float angle = (i * 360f / (_tier == 4 ? 14 : 8)) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 a = center + direction * (68 + progress * 20);
                Vector2 b = a + direction * (10 + pulse * (_tier == 4 ? 33 : 16));
                _geometry.Line(a, b, _tier == 4 ? 3.6f : 2.6f, color);
            }
            if (_tier == 4)
            {
                float left = float.MaxValue, right = float.MinValue;
                for (int i = 0; i < 5; i++)
                { left = Mathf.Min(left, _centers[i].x); right = Mathf.Max(right, _centers[i].x); }
                _geometry.Ellipse(new Vector2((left + right) * .5f, center.y),
                    (right - left) * .5f + 62 + pulse * 10, 57 + pulse * 8, 2.4f, color, 36);
            }
        }

        private void SpreadHandEffects(float progress, bool reduced)
        {
            float spread = 0;
            if (!reduced)
            {
                if (_kind == ScoreHandKind.Five)
                    spread = 30 * Pulse(progress, .23f, .45f);
                else if (_kind == ScoreHandKind.FullHouse)
                    spread = 14 * Mathf.Max(Pulse(progress, .18f, .26f), Pulse(progress, .46f, .26f));
            }
            for (int i = 0; i < 5; i++)
            {
                if (_dice[i] == null) continue;
                _centers[i] = _baseCenters[i];
                if (spread == 0 && (_positionedMask & (1 << i)) == 0) continue;
                Vector2 offset = Vector2.right * ((i - 2) * spread);
                _dice[i].anchoredPosition = _basePosition[i] + offset;
                _centers[i] += (Vector2)_root.InverseTransformVector(_dice[i].parent.TransformVector(offset));
                _positionedMask |= 1 << i;
            }
        }

        private Vector2 GroupCenter(int mask)
        {
            Vector2 center = Vector2.zero;
            int count = 0;
            for (int i = 0; i < 5; i++) if ((mask & (1 << i)) != 0) { center += _centers[i]; count++; }
            return count == 0 ? Vector2.zero : center / count;
        }

        private void ScaleEffectDie(int index, float scale, bool reduced)
        {
            if (reduced || _dice[index] == null) return;
            _scaledMask |= 1 << index;
            _dice[index].localScale = _baseScale[index] * scale;
        }
    }
}
