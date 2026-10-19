using UnityEngine;
using UnityEngine.UI;
namespace ParvaMirabilia.Presentation
{
    internal sealed class SharedAttackPresenter
    {
        public const float Duration = 0.90f;
        public const float LoadingEnd = 0.12f;
        public const float CollisionTime = 0.24f;
        public const float HitStopEnd = 0.28f;
        public const float PushEnd = 0.44f;
        public const float BreakthroughEnd = 0.54f;
        public const float ContactTime = 0.64f;
        public const float RecoilStart = 0.70f;
        private const float DisplaySize = 60f;
        private const float SlotSpacing = 64f;
        private const float BodyGap = 2f;
        private const float PushDistance = 22f;
        private const float DeflectDistance = 80f;
        private const float LoadingDim = 0.55f;
        private const float DimFadeTime = 0.08f;
        private const int OverlaySortOrder = 500;
        private const int DieSortOrder = 510;
        private RectTransform _root;
        private Canvas _overlay;
        private Image _dim;
        private Image _contactCue;
        private Image _impactFlash;
        private bool _built;
        private bool _active;
        private bool _reduced;
        private float _age;
        private int _winner;
        private int _strength;
        private Vector2 _ownHpTarget;
        private Vector2 _enemyHpTarget;
        private readonly DieState[] _own = new DieState[5];
        private readonly DieState[] _enemy = new DieState[5];
        private Vector2 _ownCenter;
        private Vector2 _enemyCenter;
        private Vector2 _ownContactCenter;
        private Vector2 _enemyContactCenter;
        private Vector2 _midpoint;
        private Vector2 _axis;
        private Vector2 _perp;
        private struct DieState
        {
            public RectTransform Rect;
            public bool Has;
            public Transform Parent;
            public int Sibling;
            public Vector3 LocalPosition;
            public Vector3 LocalScale;
            public Quaternion LocalRotation;
            public Vector2 RootCenter;
            public float RootWidth;
            public float RootHeight;
            public Canvas Canvas;
            public bool HadCanvas;
            public bool CanvasEnabled;
            public bool CanvasOverride;
            public int CanvasOrder;
            public CanvasGroup Group;
            public bool HadGroup;
            public float GroupAlpha;
            public bool GroupInteractable;
            public bool GroupBlocks;
        }
        public SharedAttackPresenter(RectTransform presentationRoot)
        {
            _root = presentationRoot;
            _winner = -1;
        }
        public RectTransform Root { get { return _root; } }
        public bool Active { get { return _active; } }
        public int Winner { get { return _winner; } }
        public float Age { get { return _age; } }
        public bool HasStruck { get { return _active && _age >= ContactTime; } }
        public Image Dim { get { return _dim; } }
        public void Begin(RectTransform[] ownHand, RectTransform[] enemyHand, int ownAttacks, int enemyAttacks, Vector2 ownHpTarget, Vector2 enemyHpTarget, bool reducedMotion, int strengthTier)
        {
            Reset();
            if (_root == null || ownHand == null || enemyHand == null || ownHand.Length < 5 || enemyHand.Length < 5)
                return;
            if (!CaptureHand(ownHand, _own) || !CaptureHand(enemyHand, _enemy))
            {
                RestoreAll();
                return;
            }
            _ownHpTarget = ownHpTarget;
            _enemyHpTarget = enemyHpTarget;
            _reduced = reducedMotion;
            _strength = Mathf.Clamp(strengthTier, -2, 2);
            _winner = ownAttacks > enemyAttacks ? 0 : enemyAttacks > ownAttacks ? 1 : -1;
            _active = true;
            _age = 0f;
            EnsureBuilt();
            MeasureGroups();
            Sample(0f);
        }
        public void Sample(float absoluteAge)
        {
            if (!_active)
                return;
            _age = Mathf.Max(0f, absoluteAge);
            Apply();
        }
        public void Reset()
        {
            RestoreAll();
            _active = false;
            _age = 0f;
            _winner = -1;
            _strength = 0;
            HideOverlay();
        }
        private bool CaptureHand(RectTransform[] hand, DieState[] states)
        {
            for (int i = 0; i < 5; i++)
            {
                RectTransform rt = hand[i];
                states[i] = default(DieState);
                if (rt == null)
                    return false;
                DieState s = new DieState();
                s.Rect = rt;
                s.Has = true;
                s.Parent = rt.parent;
                s.Sibling = rt.GetSiblingIndex();
                s.LocalPosition = rt.localPosition;
                s.LocalScale = rt.localScale;
                s.LocalRotation = rt.localRotation;
                Vector3 worldCenter = rt.TransformPoint(new Vector3(rt.rect.center.x, rt.rect.center.y, 0f));
                s.RootCenter = _root.InverseTransformPoint(worldCenter);
                Vector3 bl = rt.TransformPoint(new Vector3(rt.rect.xMin, rt.rect.yMin, 0f));
                Vector3 br = rt.TransformPoint(new Vector3(rt.rect.xMax, rt.rect.yMin, 0f));
                Vector3 tl = rt.TransformPoint(new Vector3(rt.rect.xMin, rt.rect.yMax, 0f));
                Vector2 rbl = _root.InverseTransformPoint(bl);
                Vector2 rbr = _root.InverseTransformPoint(br);
                Vector2 rtl = _root.InverseTransformPoint(tl);
                s.RootWidth = (rbr - rbl).magnitude;
                s.RootHeight = (rtl - rbl).magnitude;
                s.Canvas = rt.GetComponent<Canvas>();
                s.HadCanvas = s.Canvas != null;
                if (s.HadCanvas)
                {
                    s.CanvasEnabled = s.Canvas.enabled;
                    s.CanvasOverride = s.Canvas.overrideSorting;
                    s.CanvasOrder = s.Canvas.sortingOrder;
                }
                else
                {
                    s.Canvas = rt.gameObject.AddComponent<Canvas>();
                    s.Canvas.enabled = false;
                }
                s.Group = rt.GetComponent<CanvasGroup>();
                s.HadGroup = s.Group != null;
                if (s.HadGroup)
                {
                    s.GroupAlpha = s.Group.alpha;
                    s.GroupInteractable = s.Group.interactable;
                    s.GroupBlocks = s.Group.blocksRaycasts;
                }
                else
                {
                    s.Group = rt.gameObject.AddComponent<CanvasGroup>();
                    s.GroupAlpha = 1f;
                    s.GroupInteractable = true;
                    s.GroupBlocks = true;
                }
                states[i] = s;
            }
            return true;
        }
        private void RestoreAll()
        {
            RestoreHand(_own);
            RestoreHand(_enemy);
        }
        private void RestoreHand(DieState[] states)
        {
            for (int i = 0; i < states.Length; i++)
            {
                DieState s = states[i];
                if (s.Has && s.Rect != null)
                {
                    RectTransform rt = s.Rect;
                    if (rt.parent != s.Parent)
                        rt.SetParent(s.Parent, false);
                    if (s.Parent != null)
                        rt.SetSiblingIndex(Mathf.Clamp(s.Sibling, 0, s.Parent.childCount - 1));
                    rt.localPosition = s.LocalPosition;
                    rt.localRotation = s.LocalRotation;
                    rt.localScale = s.LocalScale;
                    RestoreCanvas(s);
                    RestoreAlpha(s);
                }
                states[i] = default(DieState);
            }
        }
        private void RestoreVisuals()
        {
            RestoreVisuals(_own);
            RestoreVisuals(_enemy);
        }
        private void RestoreVisuals(DieState[] states)
        {
            for (int i = 0; i < states.Length; i++)
            {
                DieState s = states[i];
                if (!s.Has || s.Rect == null)
                    continue;
                s.Rect.localPosition = s.LocalPosition;
                s.Rect.localRotation = s.LocalRotation;
                s.Rect.localScale = s.LocalScale;
                RestoreCanvas(s);
                RestoreAlpha(s);
            }
        }
        private void RestoreCanvas(DieState s)
        {
            if (s.Canvas == null)
                return;
            if (s.HadCanvas)
            {
                s.Canvas.enabled = s.CanvasEnabled;
                s.Canvas.overrideSorting = s.CanvasOverride;
                s.Canvas.sortingOrder = s.CanvasOrder;
            }
            else
            {
                s.Canvas.enabled = false;
                s.Canvas.overrideSorting = false;
                s.Canvas.sortingOrder = 0;
            }
        }
        private void RestoreAlpha(DieState s)
        {
            if (s.Group == null)
                return;
            if (s.HadGroup)
            {
                s.Group.alpha = s.GroupAlpha;
                s.Group.interactable = s.GroupInteractable;
                s.Group.blocksRaycasts = s.GroupBlocks;
            }
            else
            {
                s.Group.alpha = 1f;
                s.Group.interactable = true;
                s.Group.blocksRaycasts = true;
            }
        }
        private void EnsureBuilt()
        {
            if (_built)
                return;
            _built = true;
            RectTransform overlayRt = UiFactory.CreateStretch("AttackPresenter", _root);
            overlayRt.SetAsLastSibling();
            _overlay = overlayRt.gameObject.AddComponent<Canvas>();
            _overlay.overrideSorting = true;
            _overlay.sortingOrder = OverlaySortOrder;
            _dim = UiFactory.CreateStretchPanel("Dim", overlayRt, new Color(0f, 0f, 0f, 0f));
            _contactCue = UiFactory.CreatePanel("ContactCue", overlayRt, new Vector2(90f, 4f), Vector2.zero, new Color(1f, 0.94f, 0.78f, 0f));
            _contactCue.sprite = UiFactory.CircleSprite;
            _contactCue.gameObject.SetActive(false);
            _impactFlash = UiFactory.CreatePanel("ImpactFlash", overlayRt, new Vector2(60f, 60f), Vector2.zero, new Color(1f, 0.94f, 0.78f, 0f));
            _impactFlash.sprite = UiFactory.CircleSprite;
            _impactFlash.gameObject.SetActive(false);
            _overlay.enabled = false;
        }
        private void MeasureGroups()
        {
            _ownCenter = Vector2.zero;
            _enemyCenter = Vector2.zero;
            for (int i = 0; i < 5; i++)
            {
                if (_own[i].Has)
                    _ownCenter += _own[i].RootCenter;
                if (_enemy[i].Has)
                    _enemyCenter += _enemy[i].RootCenter;
            }
            _ownCenter /= 5f;
            _enemyCenter /= 5f;
            _midpoint = (_ownCenter + _enemyCenter) * 0.5f;
            _axis = _enemyCenter - _ownCenter;
            if (_axis.sqrMagnitude < 0.0001f)
                _axis = Vector2.right;
            _axis.Normalize();
            _perp = new Vector2(-_axis.y, _axis.x);
            if (_perp.x < 0f)
                _perp = -_perp;
            float halfGap = (DisplaySize + BodyGap) * 0.5f;
            _ownContactCenter = _midpoint - _axis * halfGap;
            _enemyContactCenter = _midpoint + _axis * halfGap;
        }
        private void Apply()
        {
            if (_age >= Duration)
            {
                RestoreVisuals();
                HideOverlay();
                return;
            }
            if (_overlay != null)
                _overlay.enabled = true;
            float dimAlpha = LoadingDim;
            if (!_reduced)
            {
                dimAlpha = Mathf.Clamp01(_age / DimFadeTime) * LoadingDim;
                float endFadeStart = Duration - 0.10f;
                if (_age > endFadeStart)
                    dimAlpha *= Mathf.Clamp01((Duration - _age) / 0.10f);
            }
            if (_dim != null)
                _dim.color = new Color(0f, 0f, 0f, dimAlpha);
            if (_reduced)
                ApplyReduced();
            else
                ApplyStandard();
        }
        private void ApplyReduced()
        {
            for (int i = 0; i < 5; i++)
            {
                if (_own[i].Has)
                {
                    EnableCanvas(_own[i]);
                    SetDieAlpha(_own[i], 1f);
                }
                if (_enemy[i].Has)
                {
                    EnableCanvas(_enemy[i]);
                    SetDieAlpha(_enemy[i], 1f);
                }
            }
            if (_contactCue != null)
            {
                bool show = _age >= CollisionTime;
                _contactCue.gameObject.SetActive(show);
                if (show)
                {
                    float intensity = 1f + 0.06f * _strength;
                    _contactCue.rectTransform.anchoredPosition = _midpoint;
                    _contactCue.rectTransform.sizeDelta = new Vector2(80f * intensity, 3f);
                    _contactCue.color = new Color(1f, 0.94f, 0.78f, 0.18f * intensity);
                }
            }
            if (_impactFlash != null)
                _impactFlash.gameObject.SetActive(false);
        }
        private void ApplyStandard()
        {
            float poseBlend;
            if (_age < LoadingEnd)
                poseBlend = 0f;
            else if (_age < CollisionTime)
                poseBlend = Ease((_age - LoadingEnd) / (CollisionTime - LoadingEnd));
            else if (_age < RecoilStart)
                poseBlend = 1f;
            else
                poseBlend = 1f - Ease((_age - RecoilStart) / (Duration - RecoilStart));
            Vector2 ownOffset;
            Vector2 enemyOffset;
            float ownAlpha;
            float enemyAlpha;
            float flashAlpha;
            ComputePose(_age, out ownOffset, out enemyOffset, out ownAlpha, out enemyAlpha, out flashAlpha);
            for (int i = 0; i < 5; i++)
            {
                float slot = (i - 2) * SlotSpacing;
                if (_own[i].Has)
                {
                    Vector2 final = _ownContactCenter + ownOffset + _perp * slot;
                    Vector2 target = Vector2.Lerp(_own[i].RootCenter, final, poseBlend);
                    PlaceDie(_own[i], target, Mathf.Lerp(1f, ownAlpha, poseBlend), poseBlend);
                }
                if (_enemy[i].Has)
                {
                    Vector2 final = _enemyContactCenter + enemyOffset + _perp * slot;
                    Vector2 target = Vector2.Lerp(_enemy[i].RootCenter, final, poseBlend);
                    PlaceDie(_enemy[i], target, Mathf.Lerp(1f, enemyAlpha, poseBlend), poseBlend);
                }
            }
            if (_contactCue != null)
            {
                bool show = _age >= CollisionTime && _age < PushEnd;
                _contactCue.gameObject.SetActive(show);
                if (show)
                {
                    float t = Mathf.Clamp01((_age - CollisionTime) / (PushEnd - CollisionTime));
                    float intensity = 1f + 0.05f * _strength;
                    float alpha = (1f - Mathf.Abs(t * 2f - 1f)) * 0.7f * intensity;
                    _contactCue.rectTransform.anchoredPosition = _midpoint;
                    _contactCue.rectTransform.sizeDelta = new Vector2((90f + 30f * t) * intensity, 4f);
                    _contactCue.color = new Color(1f, 0.94f, 0.78f, alpha);
                }
            }
            if (_impactFlash != null)
            {
                bool show = flashAlpha > 0.001f;
                _impactFlash.gameObject.SetActive(show);
                if (show)
                {
                    Vector2 pos = _winner == 0 ? _enemyHpTarget : _winner == 1 ? _ownHpTarget : _midpoint;
                    _impactFlash.rectTransform.anchoredPosition = pos;
                    _impactFlash.color = new Color(1f, 0.94f, 0.78f, flashAlpha);
                }
            }
        }
        private void ComputePose(float a, out Vector2 ownOffset, out Vector2 enemyOffset, out float ownAlpha, out float enemyAlpha, out float flashAlpha)
        {
            ownOffset = Vector2.zero;
            enemyOffset = Vector2.zero;
            ownAlpha = 1f;
            enemyAlpha = 1f;
            flashAlpha = 0f;
            float push = 0f;
            float deflect = 0f;
            float travel = 0f;
            if (a >= HitStopEnd)
            {
                if (a < PushEnd)
                    push = Ease((a - HitStopEnd) / (PushEnd - HitStopEnd));
                else
                    push = 1f;
            }
            if (a >= PushEnd)
            {
                if (a < BreakthroughEnd)
                    deflect = Ease((a - PushEnd) / (BreakthroughEnd - PushEnd));
                else
                    deflect = 1f;
            }
            if (a >= BreakthroughEnd)
            {
                if (a < ContactTime)
                    travel = Ease((a - BreakthroughEnd) / (ContactTime - BreakthroughEnd));
                else
                    travel = 1f;
            }
            float winnerSign = _winner == 0 ? 1f : _winner == 1 ? -1f : 0f;
            Vector2 pushVec = _axis * (PushDistance * (1f + 0.06f * _strength) * push * winnerSign);
            ownOffset += pushVec;
            enemyOffset += pushVec;
            if (_winner == 0)
            {
                enemyOffset += _perp * (DeflectDistance * deflect);
                enemyAlpha = 1f - 0.8f * deflect;
                ownOffset = Vector2.Lerp(ownOffset, _enemyHpTarget - _ownContactCenter, travel);
            }
            else if (_winner == 1)
            {
                ownOffset += _perp * (DeflectDistance * deflect);
                ownAlpha = 1f - 0.8f * deflect;
                enemyOffset = Vector2.Lerp(enemyOffset, _ownHpTarget - _enemyContactCenter, travel);
            }
            else
            {
                float d = DeflectDistance * 0.45f * deflect;
                ownOffset += _perp * d;
                enemyOffset -= _perp * d;
            }
            if (_winner != -1 && a >= ContactTime && a < RecoilStart)
            {
                float t = Mathf.Clamp01((a - ContactTime) / (RecoilStart - ContactTime));
                flashAlpha = 0.85f * (1f - t) * (1f + 0.05f * _strength);
            }
        }
        private void PlaceDie(DieState die, Vector2 targetRoot, float alpha, float poseBlend)
        {
            if (!die.Has || die.Rect == null)
                return;
            EnableCanvas(die);
            Vector3 world = _root.TransformPoint(new Vector3(targetRoot.x, targetRoot.y, 0f));
            die.Rect.localPosition = die.Parent != null ? die.Parent.InverseTransformPoint(world) : world;
            float sx = die.RootWidth > 0.0001f ? DisplaySize / die.RootWidth : 1f;
            float sy = die.RootHeight > 0.0001f ? DisplaySize / die.RootHeight : 1f;
            Vector3 contactScale = new Vector3(die.LocalScale.x * sx, die.LocalScale.y * sy, die.LocalScale.z);
            die.Rect.localScale = Vector3.Lerp(die.LocalScale, contactScale, poseBlend);
            die.Rect.localRotation = die.LocalRotation;
            SetDieAlpha(die, alpha);
        }
        private void EnableCanvas(DieState die)
        {
            if (die.Canvas == null)
                return;
            die.Canvas.enabled = true;
            die.Canvas.overrideSorting = true;
            die.Canvas.sortingOrder = DieSortOrder;
        }
        private void SetDieAlpha(DieState die, float alpha)
        {
            if (die.Group == null)
                return;
            float baseAlpha = die.HadGroup ? die.GroupAlpha : 1f;
            die.Group.alpha = baseAlpha * Mathf.Clamp01(alpha);
        }
        private void HideOverlay()
        {
            if (_overlay != null)
                _overlay.enabled = false;
            if (_dim != null)
                _dim.color = new Color(0f, 0f, 0f, 0f);
            if (_contactCue != null)
                _contactCue.gameObject.SetActive(false);
            if (_impactFlash != null)
                _impactFlash.gameObject.SetActive(false);
        }
        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }
}
