using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace ParvaMirabilia.Tests.PlayMode
{
    /// <summary>
    /// Covers the hit-point reveal that plays when a round is committed.
    ///
    /// The requirement was "animate the HP change, keep it non-blocking and skippable". Those are
    /// three separate promises and each one is a way to be wrong:
    ///
    /// - <b>animates</b>: the number has to actually travel, not just reappear elsewhere. Driving
    ///   the tween from a deliberate stand-in value rather than from the dice keeps a damage-free
    ///   round from passing the test by doing nothing at all.
    /// - <b>non-blocking</b>: nothing here may gate an input, and the animation must resolve on
    ///   its own to whatever the engine holds.
    /// - <b>skippable</b>: leaving the screen mid-tween (the next-round button is never blocked)
    ///   has to land on the truth, not on whatever frame the animation happened to be on.
    ///
    /// The last one is the promise that keeps the presentation from becoming a rule: a tween that
    /// can show a number the rules do not have is worse than no tween.
    /// </summary>
    public sealed class RevealMotionTests
    {
        private GameFlow _flow;
        private MatchEngine _engine;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Muted: a headless run has no audio listener, and the warnings add nothing here.
            PlayerPrefs.SetInt("pv.audioStep", 3);
            PlayerPrefs.Save();

            var host = new GameObject("RevealMotionUnderTest");
            _flow = host.AddComponent<GameFlow>();
            yield return null;
            yield return null;

            GameFlow.ConfigOverride = null;
            Method("StartMatch").Invoke(_flow, null);
            _engine = Engine();
            Assert.That(_engine, Is.Not.Null, "StartMatch must build an engine");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameFlow.ConfigOverride = null;
            if (_flow != null)
                UnityEngine.Object.Destroy(_flow.gameObject);
            yield return null;
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator HpReveal_TravelsAndLandsOnTheEngineValue()
        {
            int starting = _engine.Config.StartingHp;
            Assert.That(Shown("_shownPlayerHp"), Is.EqualTo(starting).Within(0.01f),
                "a fresh match starts at rest: nothing has happened yet");

            // 0,0 is a stand-in, not damage - the dice are random, so starting from the real
            // pre-commit values would let a damage-free round pass by never moving.
            Invoke("StartHpTween", 0, 0);
            Invoke("Update");
            float stepped = Shown("_shownPlayerHp");
            Assert.That(stepped, Is.GreaterThan(0f), "the reveal must move toward the engine's value");
            Assert.That(stepped, Is.LessThan(_engine.PlayerHp), "and must not jump straight there");

            yield return new WaitForSeconds(0.8f);

            Assert.That(Flag("_tweening"), Is.False, "the reveal must finish on its own");
            Assert.That(Shown("_shownPlayerHp"), Is.EqualTo(_engine.PlayerHp).Within(0.01f));
            Assert.That(Shown("_shownNpcHp"), Is.EqualTo(_engine.NpcHp).Within(0.01f));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator HpReveal_SnapJumpsToTheEngineValue()
        {
            Invoke("StartHpTween", 0, 0);
            Invoke("Update");
            Assert.That(Shown("_shownPlayerHp"), Is.Not.EqualTo(_engine.PlayerHp).Within(0.01f),
                "precondition: the screen is somewhere other than the truth");

            Invoke("SnapHp");

            Assert.That(Shown("_shownPlayerHp"), Is.EqualTo(_engine.PlayerHp).Within(0.01f),
                "leaving the screen mid-reveal must land on the engine's value");
            Assert.That(Flag("_tweening"), Is.False);

            yield return null;
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator CommittedRound_LeavesTheDisplayEqualToTheEngine()
        {
            // The default split is legal on the first click, so this is exactly what pressing
            // "確定する" would do with no other input.
            Invoke("DoCommit");
            Assert.That(_engine.History.Count, Is.GreaterThan(0), "the default split must be legal");

            yield return new WaitForSeconds(0.8f);

            Assert.That(Shown("_shownPlayerHp"), Is.EqualTo(_engine.PlayerHp).Within(0.01f),
                "after a real commit the screen must agree with the rules");
            Assert.That(Shown("_shownNpcHp"), Is.EqualTo(_engine.NpcHp).Within(0.01f));
        }

        // ---------------------------------------------------------------- helpers
        private static MethodInfo Method(string name)
        {
            var method = typeof(GameFlow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "GameFlow." + name + " is missing");
            return method;
        }

        private void Invoke(string name, params object[] args)
        {
            Method(name).Invoke(_flow, args);
        }

        private MatchEngine Engine()
        {
            var field = typeof(GameFlow).GetField("_engine",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "GameFlow._engine is missing");
            return (MatchEngine)field.GetValue(_flow);
        }

        private float Shown(string name)
        {
            return Convert.ToSingle(Field(name).GetValue(_flow));
        }

        private bool Flag(string name)
        {
            return (bool)Field(name).GetValue(_flow);
        }

        private static FieldInfo Field(string name)
        {
            var field = typeof(GameFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "GameFlow." + name + " is missing");
            return field;
        }
    }
}
