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

        [UnityTest] public IEnumerator ChanceSelectionDrawsFaintRedParticlesAroundTheFiveDice_ThenCleansUp()
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
            var first = strokes.GetValue(0);
            Color32 tint = (Color32)first.GetType().GetField("Color", BindingFlags.Instance | BindingFlags.Public).GetValue(first);
            Assert.That((int)tint.r, Is.GreaterThan((int)tint.g * 3));
            Assert.That((int)tint.a, Is.LessThan(80), "the red glow stays faint");
            Read<Button[]>("_sdLiveCategories")[(int)ScoreHandKind.Pair].onClick.Invoke();
            Call("SDTickChanceGlow", .1f);
            Assert.That(root.gameObject.activeSelf, Is.False);
        }
    }
}
