using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ParvaMirabilia.Quality.Tests
{
    public sealed class ScorePulseChanceTests
    {
        private GameFlow _flow;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        private T Read<T>(string field) => (T)typeof(GameFlow).GetField(field, Private).GetValue(_flow);
        private object Call(string method, params object[] args) => typeof(GameFlow).GetMethod(method, Private).Invoke(_flow, args);

        [UnitySetUp] public IEnumerator SetUp()
        {
            GameFlow.ConfigOverride = null;
            GameFlow.LegacyPresentationForTests = false;
            GameFlow.SeedOverride = () => 617;
            foreach (var old in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null; yield return null;
            _flow = new GameObject("ScorePulseChanceUnderTest").AddComponent<GameFlow>();
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            GameFlow.SeedOverride = null;
            if (_flow != null) Object.Destroy(_flow.gameObject);
            yield return null; yield return null;
        }

        [UnityTest] public IEnumerator AHighScorePulsesMoreThanALowScore_AndSettlesAtNormalSize()
        {
            Call("SDStartMatch");
            Text label = Read<Text>("_sdScoreNumber");
            var field = typeof(GameFlow).GetField("_sdOwnScorePulse", Private);
            var observe = typeof(GameFlow).GetMethod("SDObserveScorePulse", Private);
            var tick = typeof(GameFlow).GetMethod("SDTickScorePulse", Private);
            float Sample(int score)
            {
                object[] reset = {label, field.GetValue(_flow), -1};
                observe.Invoke(null, reset); field.SetValue(_flow, reset[1]);
                object[] begin = {label, field.GetValue(_flow), score};
                observe.Invoke(null, begin); field.SetValue(_flow, begin[1]);
                object[] advance = {label, field.GetValue(_flow), .10f};
                tick.Invoke(_flow, advance); field.SetValue(_flow, advance[1]);
                return label.rectTransform.localScale.x;
            }
            float low = Sample(40), high = Sample(150);
            Assert.That(low, Is.GreaterThan(1.02f));
            Assert.That(high, Is.GreaterThan(low + .12f), "a top score needs visibly more scale than a modest one");
            object[] settle = {label, field.GetValue(_flow), 1f};
            tick.Invoke(_flow, settle); field.SetValue(_flow, settle[1]);
            Assert.That(label.rectTransform.localScale, Is.EqualTo(Vector3.one));
            yield return null;
        }

        [UnityTest] public IEnumerator ChanceSelectionDrawsStrongerRedParticlesAroundTheFiveDice_ThenCleansUp()
        {
            Call("SDStartMatch");
            yield return new WaitForSeconds(.95f);
            // Switching while a previous formation cue is still active must show Chance now.
            typeof(GameFlow).GetField("_sdFxActive", Private).SetValue(_flow, true);
            Read<Button[]>("_sdLiveCategories")[(int)ScoreHandKind.Chance].onClick.Invoke();
            Call("SDTickChanceGlow", .1f);
            RectTransform root = Read<RectTransform>("_sdChanceGlowRoot");
            Assert.That(root.gameObject.activeSelf, Is.True);
            Graphic graphic = Read<Graphic>("_sdChanceGlowGeometry");
            var countField = graphic.GetType().GetField("_count", Private);
            Assert.That((int)countField.GetValue(graphic), Is.EqualTo(380), "five individual halos and their bounded particles");
            var strokes = (System.Array)graphic.GetType().GetField("_strokes", Private).GetValue(graphic);
            // Each die contributes 24-segment rings at starts 0/24/48 (inner/middle/outer),
            // then 4 one-stroke particles. Read each ring's representative stroke alpha and
            // confirm a soft-red halo with outward falloff, not a solid disk.
            Color32 At(int s)
            {
                var stroke = strokes.GetValue(s);
                return (Color32)stroke.GetType().GetField("Color", BindingFlags.Instance | BindingFlags.Public).GetValue(stroke);
            }
            Color32 inner = At(0), middle = At(24), outer = At(48);
            Assert.That(inner.r, Is.GreaterThan(inner.g * 3), "the halo is red, not gold/green");
            Assert.That(inner.a / 255f, Is.GreaterThan(.25f), "the inner red halo is clearly visible rather than faint");
            Assert.That(inner.a / 255f, Is.LessThan(.6f), "the glow stays a soft halo, not a solid red disk");
            Assert.That(inner.a, Is.GreaterThan(middle.a), "middle ring is behind the inner for falloff");
            Assert.That(middle.a, Is.GreaterThan(outer.a), "outer ring is the faintest of the three");
            Assert.That(inner.a, Is.GreaterThan(outer.a * 2), "the stronger glow spans all three rings with real falloff");
            Read<Button[]>("_sdLiveCategories")[(int)ScoreHandKind.Pair].onClick.Invoke();
            Call("SDTickChanceGlow", .1f);
            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That((int)countField.GetValue(graphic), Is.Zero, "cleanup clears the glow mesh");
        }
    }
}
