using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ParvaMirabilia.Tests.PlayMode
{
    public sealed class DuelPresentationTests
    {
        private GameFlow _flow;
        private MatchEngine Engine => Read<MatchEngine>("_engine");
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator SetUp()
        {
            GameFlow.ConfigOverride = null;
            GameFlow.SeedOverride = null;
            foreach (var old in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            yield return null;
            _flow = new GameObject("DuelPresentationUnderTest").AddComponent<GameFlow>();
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            GameFlow.ConfigOverride = null;
            GameFlow.SeedOverride = null;
            if (_flow != null) Object.Destroy(_flow.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest] public IEnumerator ForecastAndEmpowerUseTheSameDamageAsTheRealCommit()
        {
            Call("StartMatch");
            yield return new WaitForSeconds(.8f);
            var promise = Engine.Intent;
            var forecast = Engine.Forecast(YakuKind.Chance, true);
            Press("_battleRoot", "Empower");
            Assert.That(Read<bool>("_empower"), Is.True);
            Assert.That(Read<Text>("_bannerText").text, Does.Contain(forecast.DamageToNpc.ToString()));
            Press("_battleRoot", "Commit");
            Assert.That(Engine.LastRound.PlayerAttackSum, Is.EqualTo(forecast.Attack));
            Assert.That(Engine.LastRound.NpcAttackSum, Is.EqualTo(promise.Attack));
            Assert.That(Engine.LastRound.NpcGuardSum, Is.EqualTo(promise.Ward));
            Assert.That(Engine.LastRound.PlayerManaAfter, Is.EqualTo(forecast.ManaAfter));
            Assert.That(Root("_battleRoot").Find("NpcDie0").gameObject.activeSelf, Is.True);
            yield return new WaitForSeconds(.9f);
            Capture("duel-reveal");
        }

        [UnityTest] public IEnumerator SevenSpellSlotsStayVisible_AndRecoveringGuardCannotBeChosen()
        {
            Call("StartMatch");
            var battle = Root("_battleRoot");
            for (int i = 0; i < 7; i++) Assert.That(battle.Find("Yaku" + i).gameObject.activeSelf, Is.True);
            Assert.That(battle.Find("NpcDie0").gameObject.activeSelf, Is.False);
            Call("OnYakuClicked", 0);
            Call("DoCommit");
            if (Engine.Outcome == null)
            {
                Call("DoAdvance");
                var guard = battle.Find("Yaku0").GetComponent<Button>();
                Assert.That(guard.interactable, Is.False);
                Assert.That(guard.transform.Find("SpellValues").GetComponent<Text>().text, Does.Contain("回復中"));
                Call("OnYakuClicked", 0);
                Assert.That(Read<YakuOffer>("_yakuOffer").Kind, Is.Not.EqualTo(YakuKind.Guard));
                Capture("duel-recovery");
            }
            yield return null;
        }

        [UnityTest] public IEnumerator TacticalTutorialExplainsItsFourthStepBeforeStartingTheDuel()
        {
            Call("OpenGuide");
            Call("OnPracticeDie", 4);
            Call("AdvanceGuide");
            Call("AdvanceGuide");
            Call("AdvanceGuide");
            Assert.That(Read<int>("_guideStep"), Is.EqualTo(3));
            Assert.That(Read<Text>("_guideBody").text, Does.Contain("魔力1"));
            Press("_guideRoot", "PracticePower1");
            yield return null;
            Assert.That(Read<Text>("_guideFeedback").text, Does.Contain("与えるダメージ 18"));
            Assert.That(Read<Text>("_guideFeedback").text, Does.Contain("確定後の魔力 1"));
            Assert.That(Engine, Is.Null);
            Capture("duel-tutorial-mana");
            Call("AdvanceGuide");
            Assert.That(Engine.Intent, Is.Not.Null);
            Assert.That(Root("_guideRoot").gameObject.activeSelf, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator TrialChoiceBlocksBattle_AndStartsWithItsAdvertisedOpponentAndLife()
        {
            Press("_titleRoot", "Trials");
            var run = Read<DuelRun>("_run");
            Assert.That(Root("_runRoot").gameObject.activeSelf, Is.True);
            Call("HandleShortcut", KeyCode.Tab);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(Root("_runRoot")), Is.True);
            Call("DoCommit");
            Assert.That(Engine, Is.Null);
            Capture("trial-blessings");
            Press("_runRoot", "Blessing0");
            Assert.That(Engine.NpcType, Is.EqualTo(run.Opponent));
            Assert.That(Engine.NpcHp, Is.EqualTo(28));
            Assert.That(Engine.PlayerHp, Is.EqualTo(run.Life));
            Assert.That(Read<Text>("_npcHpText").text, Does.Contain("28 / 28"));
            Press("_battleRoot", "Blessings");
            Assert.That(Read<Text>("_journalBody").text, Does.Contain(DuelRun.Description(run.Blessings[0])));
            Call("DoCommit");
            Assert.That(Engine.History.Count, Is.Zero, "reading blessings blocks battle input");
            Press("_journalRoot", "CloseJournal");
            yield return new WaitForSeconds(.8f);
            Capture("trial-battle");
        }

        [UnityTest] public IEnumerator NewLayoutAndImportedAssetsRenderAtNarrowAndWideAspectRatios()
        {
            Capture("duel-title");
            foreach (var card in Root("_titleRoot").GetComponentsInChildren<Image>())
                if (card.name == "OpponentSeal") Assert.That(card.sprite, Is.Not.Null);
            Assert.That(Resources.Load<Font>("Fonts/AoboshiOne-Regular"), Is.Not.Null);
            Call("StartMatch");
            yield return new WaitForSeconds(.8f);
            Capture("duel-planning");
            Capture("duel-4x3", 1024, 768);
            Capture("duel-wide", 1920, 820);
            var battle = Root("_battleRoot");
            var hp = (RectTransform)battle.Find("PlayerHp");
            var empower = (RectTransform)battle.Find("Empower");
            Assert.That(empower.anchoredPosition.y + empower.sizeDelta.y / 2,
                Is.LessThan(hp.anchoredPosition.y - hp.sizeDelta.y / 2), "empower must not cover player HP");
            string[] ordered = { "NpcName", "NpcBar", "IntentPanel", "Banner", "PlayerDie0", "Yaku0", "Info", "PlayerName", "PlayerBar", "Hint", "Commit" };
            float previousBottom = 360;
            foreach (string name in ordered)
            {
                var rect = (RectTransform)battle.Find(name);
                float top = rect.anchoredPosition.y + rect.sizeDelta.y / 2;
                float bottom = rect.anchoredPosition.y - rect.sizeDelta.y / 2;
                Assert.That(top, Is.LessThanOrEqualTo(previousBottom), name + " overlaps the row above");
                Assert.That(bottom, Is.GreaterThan(-325), name + " falls off the design canvas");
                previousBottom = bottom;
            }
        }

        [UnityTest] public IEnumerator EffectsRespectMotionSetting_AndCreditsBlockBattleInput()
        {
            bool originalMotion = Read<bool>("_reducedMotion");
            try
            {
                if (originalMotion) Call("ToggleReducedMotion");
                Call("StartMatch");
                yield return new WaitForSeconds(.8f);
                Call("DoCommit");
                Assert.That(Read<float>("_duelFxAge"), Is.GreaterThanOrEqualTo(0));
                yield return new WaitForSeconds(.35f);
                Assert.That(Root("_battleRoot").Find("DuelBurst0").gameObject.activeSelf, Is.True);
                Capture("duel-impact");
                yield return new WaitForSeconds(.6f);
                Assert.That(Root("_battleRoot").Find("DuelSpark0").gameObject.activeSelf, Is.False);
                Call("StartMatch");
                Call("OpenMenu");
                Press("_menuRoot", "Motion");
                Assert.That(Read<bool>("_reducedMotion"), Is.True);
                Assert.That(PlayerPrefs.GetInt("arcane.reducedMotion"), Is.EqualTo(1));
                Press("_menuRoot", "Credits");
                Call("DoCommit");
                Assert.That(Engine.History.Count, Is.Zero);
                Call("HandleShortcut", KeyCode.Tab);
                Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(Root("_creditsRoot")), Is.True);
                Capture("duel-credits");
                Press("_creditsRoot", "CloseCredits");
                Call("DoCommit");
                Assert.That(Read<float>("_duelFxAge"), Is.EqualTo(-1));
                Assert.That(Root("_battleRoot").Find("DuelSpark0").gameObject.activeSelf, Is.False);
            }
            finally
            {
                if (Read<bool>("_reducedMotion") != originalMotion) Call("ToggleReducedMotion");
            }
        }

        [UnityTest] public IEnumerator TrialButtonsCarryTheWholeRunThroughVictoryAndRematch()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "logs", "quality-v4"));
            Directory.CreateDirectory(folder);
            typeof(GameFlow).GetField("_smokeOutput", Private).SetValue(_flow, folder);
            var routine = (IEnumerator)typeof(GameFlow).GetMethod("SmokeTrial", Private).Invoke(_flow, null);
            yield return routine;
            Assert.That(Read<DuelRun>("_run"), Is.Null);
            Assert.That(Engine, Is.Null);
            Assert.That(Root("_titleRoot").gameObject.activeInHierarchy, Is.True);
            Assert.That(GameFlow.SeedOverride, Is.Null);
        }

        private T Read<T>(string field) => (T)typeof(GameFlow).GetField(field, Private).GetValue(_flow);
        private RectTransform Root(string field) => Read<RectTransform>(field);
        private void Call(string method, params object[] args) => typeof(GameFlow).GetMethod(method, Private).Invoke(_flow, args);
        private void Press(string root, string name)
        {
            var button = Root(root).Find(name).GetComponent<Button>();
            Assert.That(button.interactable && button.gameObject.activeInHierarchy, Is.True, name);
            button.onClick.Invoke();
        }

        private void Capture(string name, int width = 1280, int height = 720)
        {
            var canvas = _flow.GetComponentInParent<Canvas>();
            var host = new GameObject("DuelCaptureCamera", typeof(Camera));
            var camera = host.GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(9, 22, 29, 255);
            var mode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            float plane = canvas.planeDistance;
            var previousTarget = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 5;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "logs", "quality-v4"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = plane;
                RenderTexture.active = previousTarget;
                camera.targetTexture = null;
                if (texture != null) Object.Destroy(texture);
                target.Release(); Object.Destroy(target); Object.Destroy(host);
            }
        }
    }
}
