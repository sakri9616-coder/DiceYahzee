using System;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ParvaMirabilia.Quality.Tests
{
    public sealed class SharedAttackPresenterTests
    {
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Type PresenterType =
            typeof(GameFlow).Assembly.GetType("ParvaMirabilia.Presentation.SharedAttackPresenter", true);

        private GameObject _canvasObject;
        private RectTransform _root;
        private RectTransform _ownParent;
        private RectTransform _enemyParent;
        private RectTransform[] _own;
        private RectTransform[] _enemy;

        [SetUp]
        public void SetUp()
        {
            _canvasObject = new GameObject("SharedAttackPresenterTestsCanvas", typeof(Canvas));
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform canvasRect = (RectTransform)_canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(1280f, 720f);

            _root = NewRect("PresentationRoot", canvasRect, new Vector2(1200f, 600f));
            _ownParent = NewRect("OwnHand", _root, new Vector2(100f, 400f));
            _ownParent.anchoredPosition = new Vector2(-260f, 0f);
            _enemyParent = NewRect("EnemyHand", _root, new Vector2(100f, 400f));
            _enemyParent.anchoredPosition = new Vector2(260f, 0f);

            _own = new RectTransform[5];
            _enemy = new RectTransform[5];
            for (int i = 0; i < 5; i++)
            {
                _own[i] = NewDie(_ownParent, "Own" + i, 112f, i, 0.82f + i * 0.02f);
                _enemy[i] = NewDie(_enemyParent, "Enemy" + i, 44f, i, 0.90f);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasObject != null)
                Object.DestroyImmediate(_canvasObject);
        }

        [Test]
        public void SameAbsoluteAgeTwice_ProducesIdenticalPose()
        {
            object presenter = NewPresenter();
            Begin(presenter, 3, 2, new Vector2(-200f, 40f), new Vector2(240f, 90f));
            Invoke(presenter, "Sample", Const("CollisionTime"));
            Pose own = CapturePose(_own[1]);
            Pose enemy = CapturePose(_enemy[3]);
            Color dim = ReadProp<Image>(presenter, "Dim").color;

            Invoke(presenter, "Sample", Const("PushEnd"));
            Invoke(presenter, "Sample", Const("CollisionTime"));

            AssertPose(own, _own[1], 0.001f, "own re-sampled");
            AssertPose(enemy, _enemy[3], 0.001f, "enemy re-sampled");
            Assert.That(ReadProp<Image>(presenter, "Dim").color.a, Is.EqualTo(dim.a).Within(0.0001f),
                "sampling by absolute age must not accumulate dim");
        }

        [Test]
        public void ContactAtPoint24_NormalizesRowsAndKeepsSlotOrder()
        {
            Pose[] ownBefore = CaptureAll(_own);
            Pose[] enemyBefore = CaptureAll(_enemy);
            Vector2 axis = ApproachAxis();
            Vector2 perp = new Vector2(-axis.y, axis.x);
            object presenter = NewPresenter();
            Begin(presenter, 3, 2, new Vector2(-200f, 90f), new Vector2(220f, 90f));

            Invoke(presenter, "Sample", 0f);
            for (int i = 0; i < 5; i++)
            {
                AssertPose(ownBefore[i], _own[i], 0.001f, "own approach origin " + i);
                AssertPose(enemyBefore[i], _enemy[i], 0.001f, "enemy approach origin " + i);
            }

            Invoke(presenter, "Sample", Const("CollisionTime"));
            for (int i = 0; i < 5; i++)
            {
                Assert.That(ProjectedSize(_own[i], axis), Is.EqualTo(60f).Within(1.5f),
                    "own die " + i + " is normalized at contact");
                Assert.That(ProjectedSize(_enemy[i], axis), Is.EqualTo(60f).Within(1.5f),
                    "enemy die " + i + " is normalized at contact");
                float gap = ProjectedMin(_enemy[i], axis) - ProjectedMax(_own[i], axis);
                Assert.That(gap, Is.EqualTo(2f).Within(1.5f), "opposing dice touch at contact");
            }

            float previous = float.NegativeInfinity;
            for (int i = 0; i < 5; i++)
            {
                float ownSlot = Vector2.Dot(RootCenter(_own[i]), perp);
                float enemySlot = Vector2.Dot(RootCenter(_enemy[i]), perp);
                Assert.That(ownSlot, Is.GreaterThan(previous), "slots keep their order");
                Assert.That(enemySlot, Is.EqualTo(ownSlot).Within(0.5f), "both rows share each slot line");
                previous = ownSlot;
            }
        }

        [Test]
        public void OwnHigher_PushesBothRowsTowardEnemy_AndStrikesEnemyHpAtContact()
        {
            Vector2 enemyHp = new Vector2(300f, 120f);
            Vector2 axis = ApproachAxis();
            object presenter = NewPresenter();
            Begin(presenter, 4, 1, new Vector2(-260f, 100f), enemyHp);

            Invoke(presenter, "Sample", Const("CollisionTime"));
            Vector2 ownContact = RowCenter(_own);
            Vector2 enemyContact = RowCenter(_enemy);

            Invoke(presenter, "Sample", Const("PushEnd"));
            Assert.That(Vector2.Dot(RowCenter(_own) - ownContact, axis), Is.GreaterThan(12f),
                "the winning own row advances toward the enemy");
            Assert.That(Vector2.Dot(RowCenter(_enemy) - enemyContact, axis), Is.GreaterThan(12f),
                "the enemy row is also pushed toward its own side");
            Assert.That(ReadProp<int>(presenter, "Winner"), Is.EqualTo(0));
            Assert.That(ReadProp<bool>(presenter, "HasStruck"), Is.False);

            Invoke(presenter, "Sample", Const("ContactTime"));
            Assert.That(ReadProp<bool>(presenter, "HasStruck"), Is.True);
            Assert.That(Vector2.Distance(RowCenter(_own), enemyHp), Is.LessThan(2f),
                "the own row reaches the enemy HP target");
            Image flash = (Image)ReadField(presenter, "_impactFlash");
            Assert.That(flash.gameObject.activeSelf, Is.True);
            Assert.That(Vector2.Distance(flash.rectTransform.anchoredPosition, enemyHp), Is.LessThan(2f),
                "the impact flash lands on the enemy HP target");
        }

        [Test]
        public void EnemyHigher_ReversesPushAndStrikesOwnHpAtContact()
        {
            Vector2 ownHp = new Vector2(-300f, 110f);
            Vector2 axis = ApproachAxis();
            object presenter = NewPresenter();
            Begin(presenter, 1, 4, ownHp, new Vector2(280f, 80f));

            Invoke(presenter, "Sample", Const("CollisionTime"));
            Vector2 ownContact = RowCenter(_own);
            Vector2 enemyContact = RowCenter(_enemy);

            Invoke(presenter, "Sample", Const("PushEnd"));
            Assert.That(Vector2.Dot(RowCenter(_own) - ownContact, axis), Is.LessThan(-12f));
            Assert.That(Vector2.Dot(RowCenter(_enemy) - enemyContact, axis), Is.LessThan(-12f));
            Assert.That(ReadProp<int>(presenter, "Winner"), Is.EqualTo(1));

            Invoke(presenter, "Sample", Const("ContactTime"));
            Assert.That(Vector2.Distance(RowCenter(_enemy), ownHp), Is.LessThan(2f),
                "the enemy row reaches the own HP target");
            Image flash = (Image)ReadField(presenter, "_impactFlash");
            Assert.That(flash.gameObject.activeSelf, Is.True);
            Assert.That(Vector2.Distance(flash.rectTransform.anchoredPosition, ownHp), Is.LessThan(2f));
        }

        [Test]
        public void Tie_NeverReportsAWinnerOrImpact()
        {
            Vector2 axis = ApproachAxis();
            Vector2 perp = new Vector2(-axis.y, axis.x);
            object presenter = NewPresenter();
            Begin(presenter, 2, 2, new Vector2(-240f, 70f), new Vector2(240f, 70f));

            Invoke(presenter, "Sample", Const("PushEnd"));
            Vector2 ownPush = RowCenter(_own);
            Vector2 enemyPush = RowCenter(_enemy);
            Assert.That(Vector2.Dot(enemyPush - ownPush, axis), Is.EqualTo(62f).Within(2f),
                "balanced clash keeps the original collision separation");
            Assert.That(ReadProp<int>(presenter, "Winner"), Is.EqualTo(-1), "no false winner at push end");

            Invoke(presenter, "Sample", Const("BreakthroughEnd"));
            Vector2 ownCenter = RowCenter(_own);
            Vector2 enemyCenter = RowCenter(_enemy);
            Assert.That(Mathf.Abs(Vector2.Dot(ownCenter - enemyCenter, perp)), Is.GreaterThan(60f),
                "tied rows split apart instead of pushing");
            Assert.That(Vector2.Dot(enemyCenter - ownCenter, axis), Is.EqualTo(62f).Within(2f),
                "breakthrough separates rows without changing collision axis separation");
            Assert.That(ReadProp<int>(presenter, "Winner"), Is.EqualTo(-1), "no false winner after breakthrough");

            Invoke(presenter, "Sample", Const("ContactTime"));
            Assert.That(ReadProp<int>(presenter, "Winner"), Is.EqualTo(-1), "no false winner");
            Assert.That(((Image)ReadField(presenter, "_impactFlash")).gameObject.activeSelf, Is.False,
                "a tie has no impact flash");
        }

        [Test]
        public void ChanceReversalFromRealForecast_FlipsWinnerWithoutRuleChanges()
        {
            ScoreForecast normal = new ScoreForecast(120, 80);
            ScoreForecast reversed = new ScoreForecast(120, 80, false, true);
            Assert.That(normal.IsReversed, Is.False);
            Assert.That(reversed.IsReversed, Is.True);
            Assert.That(normal.DamageToNpc, Is.GreaterThan(normal.DamageToPlayer));
            Assert.That(reversed.DamageToNpc, Is.LessThan(reversed.DamageToPlayer));

            Vector2 ownHp = new Vector2(-260f, 80f);
            Vector2 enemyHp = new Vector2(260f, 80f);
            object presenter = NewPresenter();

            Begin(presenter, normal.DamageToNpc, normal.DamageToPlayer, ownHp, enemyHp);
            Assert.That(ReadProp<int>(presenter, "Winner"), Is.EqualTo(0));
            Invoke(presenter, "Sample", Const("ContactTime"));
            Assert.That(Vector2.Distance(RowCenter(_own), enemyHp), Is.LessThan(2f));
            Invoke(presenter, "Reset");

            Begin(presenter, reversed.DamageToNpc, reversed.DamageToPlayer, ownHp, enemyHp);
            Assert.That(ReadProp<int>(presenter, "Winner"), Is.EqualTo(1), "real Chance reversal flips the winner");
            Invoke(presenter, "Sample", Const("ContactTime"));
            Assert.That(Vector2.Distance(RowCenter(_enemy), ownHp), Is.LessThan(2f));
        }

        [Test]
        public void ResetAfterMidClash_RestoresHierarchyPoseAndVisualState()
        {
            _own[0].GetComponent<Canvas>().enabled = false;
            _own[0].GetComponent<Canvas>().overrideSorting = false;
            _enemy[4].GetComponent<CanvasGroup>().alpha = 0.37f;
            _enemy[4].GetComponent<CanvasGroup>().interactable = false;
            _enemy[4].GetComponent<CanvasGroup>().blocksRaycasts = false;

            DieState[] own = CaptureStates(_own);
            DieState[] enemy = CaptureStates(_enemy);
            object presenter = NewPresenter();
            Begin(presenter, 4, 1, new Vector2(-220f, 60f), new Vector2(260f, 140f));
            Invoke(presenter, "Sample", Const("PushEnd"));
            Assert.That(Vector3.Distance(own[0].Pose.Position, _own[0].localPosition), Is.GreaterThan(1f),
                "the fixture is genuinely mid-clash");

            Invoke(presenter, "Reset");

            for (int i = 0; i < 5; i++)
            {
                AssertState(own[i], _own[i], "own " + i);
                AssertState(enemy[i], _enemy[i], "enemy " + i);
            }
            Assert.That(ReadProp<bool>(presenter, "Active"), Is.False);
            Assert.That(ReadProp<float>(presenter, "Age"), Is.EqualTo(0f));
            Assert.That(ReadProp<int>(presenter, "Winner"), Is.EqualTo(-1));
            Assert.That(ReadProp<Image>(presenter, "Dim").color.a, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(((Canvas)ReadField(presenter, "_overlay")).enabled, Is.False);
        }

        [Test]
        public void RepeatedBeginReset_DoesNotAccumulateState()
        {
            DieState[] own = CaptureStates(_own);
            DieState[] enemy = CaptureStates(_enemy);
            object presenter = NewPresenter();
            int before = _root.childCount;

            Begin(presenter, 3, 2, new Vector2(-210f, 50f), new Vector2(230f, 100f));
            Invoke(presenter, "Sample", 0.30f);
            Invoke(presenter, "Reset");
            int builtChildren = _root.childCount;
            Assert.That(builtChildren, Is.EqualTo(before + 1), "exactly one overlay is built");

            for (int cycle = 0; cycle < 4; cycle++)
            {
                Begin(presenter, cycle % 2 == 0 ? 4 : 1, 2, new Vector2(-210f, 50f), new Vector2(230f, 100f));
                Invoke(presenter, "Sample", 0.20f + cycle * 0.15f);
                Invoke(presenter, "Reset");
                Assert.That(ReadProp<bool>(presenter, "Active"), Is.False);
                Assert.That(ReadProp<float>(presenter, "Age"), Is.EqualTo(0f));
                Assert.That(ReadProp<int>(presenter, "Winner"), Is.EqualTo(-1));
                for (int i = 0; i < 5; i++)
                {
                    AssertState(own[i], _own[i], "cycle " + cycle + " own " + i);
                    AssertState(enemy[i], _enemy[i], "cycle " + cycle + " enemy " + i);
                }
            }
            Assert.That(_root.childCount, Is.EqualTo(builtChildren), "repeated cycles never duplicate the overlay");
        }

        [Test]
        public void DimIsInputTransparent_AndDiceRenderAboveOverlay()
        {
            object presenter = NewPresenter();
            Begin(presenter, 3, 2, new Vector2(-240f, 80f), new Vector2(240f, 80f));
            Invoke(presenter, "Sample", 0.20f);

            Image dim = ReadProp<Image>(presenter, "Dim");
            Assert.That(dim.raycastTarget, Is.False, "dim must not intercept input");
            Assert.That(dim.color.a, Is.GreaterThan(0.5f), "dim is visibly dark while active");
            Canvas overlay = (Canvas)ReadField(presenter, "_overlay");
            Assert.That(overlay.enabled, Is.True);
            Assert.That(overlay.overrideSorting, Is.True);
            for (int i = 0; i < 5; i++)
            {
                AssertAbove(_own[i], overlay);
                AssertAbove(_enemy[i], overlay);
            }
        }

        [Test]
        public void ReducedMotion_KeepsSourcePosesLitAcrossSamples_ThenResetClearsDim()
        {
            DieState[] own = CaptureStates(_own);
            DieState[] enemy = CaptureStates(_enemy);
            object presenter = NewPresenter();
            Begin(presenter, 3, 1, new Vector2(-220f, 90f), new Vector2(260f, 90f), true, 1);

            float contact = Const("ContactTime");
            float duration = Const("Duration");
            float fadeAge = duration - 0.05f;
            float expectedFade = (duration - fadeAge) / (duration - contact);

            float[] stationaryAges =
            {
                0f, Const("LoadingEnd"), Const("CollisionTime"), Const("PushEnd"), contact
            };
            foreach (float age in stationaryAges)
            {
                Invoke(presenter, "Sample", age);
                for (int i = 0; i < 5; i++)
                {
                    AssertPose(own[i].Pose, _own[i], 0.0001f, "reduced own " + i + " at " + age);
                    AssertPose(enemy[i].Pose, _enemy[i], 0.0001f, "reduced enemy " + i + " at " + age);
                    Assert.That(_own[i].GetComponent<CanvasGroup>().alpha,
                        Is.EqualTo(own[i].GroupAlpha).Within(0.0001f), "reduced-motion own baseline alpha at " + age);
                    Assert.That(_enemy[i].GetComponent<CanvasGroup>().alpha,
                        Is.EqualTo(enemy[i].GroupAlpha).Within(0.0001f), "reduced-motion enemy baseline alpha at " + age);
                }
                Assert.That(ReadProp<Image>(presenter, "Dim").color.a, Is.EqualTo(0.55f).Within(0.0001f),
                    "reduced motion uses a steady, non-fading dim");
            }

            Invoke(presenter, "Sample", fadeAge);
            for (int i = 0; i < 5; i++)
            {
                AssertPose(own[i].Pose, _own[i], 0.0001f, "reduced own " + i + " at fade age");
                AssertPose(enemy[i].Pose, _enemy[i], 0.0001f, "reduced enemy " + i + " at fade age");
                Assert.That(_own[i].GetComponent<CanvasGroup>().alpha,
                    Is.EqualTo(own[i].GroupAlpha * expectedFade).Within(0.001f), "reduced-motion own fade alpha");
                Assert.That(_enemy[i].GetComponent<CanvasGroup>().alpha,
                    Is.EqualTo(enemy[i].GroupAlpha * expectedFade).Within(0.001f), "reduced-motion enemy fade alpha");
            }
            Assert.That(ReadProp<Image>(presenter, "Dim").color.a, Is.EqualTo(0.55f).Within(0.0001f),
                "reduced motion uses a steady, non-fading dim");

            Invoke(presenter, "Sample", duration);
            for (int i = 0; i < 5; i++)
            {
                Assert.That(_own[i].GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f).Within(0.001f),
                    "reduced-motion own cleared at duration");
                Assert.That(_enemy[i].GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f).Within(0.001f),
                    "reduced-motion enemy cleared at duration");
            }
            Assert.That(ReadProp<Image>(presenter, "Dim").color.a, Is.EqualTo(0f).Within(0.0001f));

            Invoke(presenter, "Reset");
            for (int i = 0; i < 5; i++)
            {
                AssertState(own[i], _own[i], "reduced reset own " + i);
                AssertState(enemy[i], _enemy[i], "reduced reset enemy " + i);
            }
            Assert.That(ReadProp<Image>(presenter, "Dim").color.a, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(((Canvas)ReadField(presenter, "_overlay")).enabled, Is.False);
        }

        private struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        private struct DieState
        {
            public Transform Parent;
            public int Sibling;
            public Pose Pose;
            public Canvas Canvas;
            public bool CanvasEnabled;
            public bool CanvasOverride;
            public int CanvasOrder;
            public CanvasGroup Group;
            public float GroupAlpha;
            public bool GroupInteractable;
            public bool GroupBlocks;
        }

        private object NewPresenter()
        {
            return Activator.CreateInstance(PresenterType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { _root }, null);
        }

        private void Begin(object presenter, int ownAttacks, int enemyAttacks, Vector2 ownHp, Vector2 enemyHp,
            bool reduced = false, int strength = 0)
        {
            Invoke(presenter, "Begin", _own, _enemy, ownAttacks, enemyAttacks, ownHp, enemyHp, reduced, strength);
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            return PresenterType.GetMethod(method, PublicInstance).Invoke(target, args);
        }

        private static T ReadProp<T>(object presenter, string name)
        {
            return (T)PresenterType.GetProperty(name, PublicInstance).GetValue(presenter);
        }

        private static object ReadField(object presenter, string name)
        {
            return PresenterType.GetField(name, PrivateInstance).GetValue(presenter);
        }

        private static float Const(string name)
        {
            return (float)PresenterType.GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private static RectTransform NewRect(string name, RectTransform parent, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static RectTransform NewDie(RectTransform parent, string name, float size, int index, float alpha)
        {
            RectTransform rt = NewRect(name, parent, new Vector2(size, size));
            rt.anchoredPosition = new Vector2(0f, (index - 2) * 70f);
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = Color.white;
            Canvas canvas = rt.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 300 + index;
            CanvasGroup group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.interactable = true;
            group.blocksRaycasts = true;
            return rt;
        }

        private Vector2 ApproachAxis()
        {
            Vector2 delta = RowCenter(_enemy) - RowCenter(_own);
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        }

        private Vector2 RootCenter(RectTransform rt)
        {
            Vector3 world = rt.TransformPoint(new Vector3(rt.rect.center.x, rt.rect.center.y, 0f));
            return _root.InverseTransformPoint(world);
        }

        private Vector2 RowCenter(RectTransform[] hand)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < hand.Length; i++)
                sum += RootCenter(hand[i]);
            return sum / hand.Length;
        }

        private float ProjectedMin(RectTransform rt, Vector2 axis)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float result = float.PositiveInfinity;
            for (int i = 0; i < corners.Length; i++)
                result = Mathf.Min(result, Vector2.Dot(_root.InverseTransformPoint(corners[i]), axis));
            return result;
        }

        private float ProjectedMax(RectTransform rt, Vector2 axis)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float result = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
                result = Mathf.Max(result, Vector2.Dot(_root.InverseTransformPoint(corners[i]), axis));
            return result;
        }

        private float ProjectedSize(RectTransform rt, Vector2 axis)
        {
            return ProjectedMax(rt, axis) - ProjectedMin(rt, axis);
        }

        private static void AssertAbove(RectTransform die, Canvas overlay)
        {
            Canvas canvas = die.GetComponent<Canvas>();
            Assert.That(canvas.enabled, Is.True, die.name + " canvas is active");
            Assert.That(canvas.overrideSorting, Is.True, die.name + " overrides sorting");
            Assert.That(canvas.sortingOrder, Is.GreaterThan(overlay.sortingOrder),
                die.name + " renders above the dim overlay");
        }

        private static Pose CapturePose(RectTransform rt)
        {
            return new Pose { Position = rt.localPosition, Rotation = rt.localRotation, Scale = rt.localScale };
        }

        private static Pose[] CaptureAll(RectTransform[] hand)
        {
            Pose[] poses = new Pose[hand.Length];
            for (int i = 0; i < hand.Length; i++)
                poses[i] = CapturePose(hand[i]);
            return poses;
        }

        private static void AssertPose(Pose expected, RectTransform rt, float tolerance, string label)
        {
            Assert.That(Vector3.Distance(expected.Position, rt.localPosition), Is.LessThan(tolerance), label + " position");
            Assert.That(Quaternion.Angle(expected.Rotation, rt.localRotation),
                Is.LessThan(Mathf.Max(0.01f, tolerance * 10f)), label + " rotation");
            Assert.That(Vector3.Distance(expected.Scale, rt.localScale), Is.LessThan(tolerance), label + " scale");
        }

        private static DieState[] CaptureStates(RectTransform[] hand)
        {
            DieState[] states = new DieState[hand.Length];
            for (int i = 0; i < hand.Length; i++)
            {
                RectTransform rt = hand[i];
                Canvas canvas = rt.GetComponent<Canvas>();
                CanvasGroup group = rt.GetComponent<CanvasGroup>();
                states[i] = new DieState
                {
                    Parent = rt.parent,
                    Sibling = rt.GetSiblingIndex(),
                    Pose = CapturePose(rt),
                    Canvas = canvas,
                    CanvasEnabled = canvas.enabled,
                    CanvasOverride = canvas.overrideSorting,
                    CanvasOrder = canvas.sortingOrder,
                    Group = group,
                    GroupAlpha = group.alpha,
                    GroupInteractable = group.interactable,
                    GroupBlocks = group.blocksRaycasts
                };
            }
            return states;
        }

        private static void AssertState(DieState expected, RectTransform rt, string label)
        {
            Assert.That(rt.parent, Is.SameAs(expected.Parent), label + " parent");
            Assert.That(rt.GetSiblingIndex(), Is.EqualTo(expected.Sibling), label + " sibling");
            AssertPose(expected.Pose, rt, 0.0001f, label);
            Assert.That(expected.Canvas.enabled, Is.EqualTo(expected.CanvasEnabled), label + " canvas enabled");
            Assert.That(expected.Canvas.overrideSorting, Is.EqualTo(expected.CanvasOverride), label + " canvas override");
            Assert.That(expected.Canvas.sortingOrder, Is.EqualTo(expected.CanvasOrder), label + " canvas order");
            Assert.That(expected.Group.alpha, Is.EqualTo(expected.GroupAlpha).Within(0.0001f), label + " alpha");
            Assert.That(expected.Group.interactable, Is.EqualTo(expected.GroupInteractable), label + " interactable");
            Assert.That(expected.Group.blocksRaycasts, Is.EqualTo(expected.GroupBlocks), label + " blocksRaycasts");
        }
    }
}
