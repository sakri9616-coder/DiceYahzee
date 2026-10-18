using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ParvaMirabilia.Quality.Tests
{
    public sealed class ScoreDuelFeedbackTests
    {
        private GameFlow _flow;
        private int _motion, _audio;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ScoreDuel Engine => Read<ScoreDuel>("_sdEngine");
        private static Color GoldRing
        {
            get
            {
                var factory = typeof(GameFlow).Assembly.GetType("ParvaMirabilia.Presentation.UiFactory");
                return (Color)factory.GetField("Gold").GetValue(null);
            }
        }

        [UnitySetUp] public IEnumerator SetUp()
        {
            GameFlow.ConfigOverride = null;
            GameFlow.LegacyPresentationForTests = false;
            GameFlow.SeedOverride = () => 617;
            _motion = PlayerPrefs.GetInt("arcane.reducedMotion", 0);
            _audio = PlayerPrefs.GetInt("pv.audioStep", 3);
            PlayerPrefs.SetInt("arcane.reducedMotion", 0);
            PlayerPrefs.SetInt("pv.audioStep", 3);
            foreach (var old in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            Screen.SetResolution(1280, 720, false);
            yield return null;
            yield return null;
            _flow = new GameObject("FeedbackPresentationUnderTest").AddComponent<GameFlow>();
            yield return null;
            Press("_sdTitle", "Start");
            // Allow the real opening throw and its notification to end before injecting pointer
            // events or a deliberately isolated visual fixture.
            yield return new WaitForSeconds(1.8f);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            GameFlow.ConfigOverride = null;
            GameFlow.SeedOverride = null;
            GameFlow.LegacyPresentationForTests = false;
            PlayerPrefs.SetInt("arcane.reducedMotion", _motion);
            PlayerPrefs.SetInt("pv.audioStep", _audio);
            Screen.SetResolution(1280, 720, false);
            if (_flow != null) Object.Destroy(_flow.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest] public IEnumerator EveryDieHasAFullPointerSurface_IncludingFaceAndCorners()
        {
            Canvas.ForceUpdateCanvases();
            var eventSystem = EventSystem.current;
            var results = new System.Collections.Generic.List<RaycastResult>();
            for (int i = 0; i < 5; i++)
                foreach (float x in new[] { -50f, 0f, 50f })
                    foreach (float y in new[] { -50f, 0f, 50f })
                    {
                        var screen = RectTransformUtility.WorldToScreenPoint(null, Die(i).TransformPoint(new Vector2(x, y)));
                        var pointer = new PointerEventData(eventSystem) { position = screen };
                        results.Clear(); eventSystem.RaycastAll(pointer, results);
                        Assert.That(results.Count, Is.GreaterThan(0), "die " + i + " has no input at " + x + "," + y);
                        Assert.That(ExecuteEvents.GetEventHandler<IBeginDragHandler>(results[0].gameObject),
                            Is.EqualTo(Die(i).gameObject), "decorations must not steal the full die hit area");
                    }
            yield return null;
        }

        [UnityTest] public IEnumerator InviteCodeCanBeClickedAndTyped_AndVolumeSliderChangesPlayback()
        {
            Call("OpenOnline");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var input = Read<InputField>("_onlineCode");
            var eventSystem = EventSystem.current;
            var pointer = new PointerEventData(eventSystem) {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, input.transform.position)
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject),
                Is.EqualTo(input.gameObject), "the visible invite box must receive a real pointer click");
            ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(input.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(input.isFocused, Is.True);
            input.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, character = 'A' });
            input.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Alpha7, character = '7' });
            Assert.That(input.text, Is.EqualTo("A7"));
            input.ForceLabelUpdate();
            Assert.That(input.placeholder.enabled, Is.False);

            var slider = Read<Slider>("_soundSlider");
            Assert.That(slider.gameObject.activeInHierarchy, Is.True);
            Assert.That(slider.GetComponent<Image>().raycastTarget, Is.True);
            float original = GameAudio.Volume;
            bool hadContinuous = PlayerPrefs.HasKey("pv.audioVolume");
            float stored = PlayerPrefs.GetFloat("pv.audioVolume", 0);
            slider.value = .5f;
            Assert.That(GameAudio.Volume, Is.EqualTo(.5f).Within(.001f));
            AudioSource[] sources = GameAudio.Instance.GetComponents<AudioSource>();
            Assert.That(sources[0].volume, Is.EqualTo(.0945f).Within(.001f), "BGM maximum is 40% below the previous 0.315 cap");
            slider.value = 0f;
            Assert.That(sources[0].volume, Is.Zero);
            GameAudio.SetVolume(original);
            if (hadContinuous) PlayerPrefs.SetFloat("pv.audioVolume", stored);
            else PlayerPrefs.DeleteKey("pv.audioVolume");
            PlayerPrefs.Save();
            input.DeactivateInputField();
            eventSystem.SetSelectedGameObject(null);
            yield return null;
        }

        [UnityTest] public IEnumerator VolumeBarSitsAboveTheScoreDuelTable_AndThePointerReachesIt()
        {
            var slider = Read<Slider>("_soundSlider");
            Assert.That(slider, Is.Not.Null);
            Assert.That(slider.gameObject.activeInHierarchy, Is.True);

            var corners = new Vector3[4];
            ((RectTransform)slider.transform).GetWorldCorners(corners);
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) / 2f);
            Assert.That(center.y, Is.InRange(0f, Screen.height));
            Assert.That(center.x, Is.InRange(0f, Screen.width));

            var hits = new System.Collections.Generic.List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current) { position = center };
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            GameObject top = hits[0].gameObject;
            bool inBar = top == slider.gameObject;
            for (var t = top.transform; t != null; t = t.parent)
                if (t == slider.transform) { inBar = true; break; }
            Assert.That(inBar, Is.True, "the volume bar must sit above the SD table, but " + top.name + " covers it");

            // While we are here, confirm the bar still drives the real volume when it is visible.
            float original = GameAudio.Volume;
            bool hadContinuous = PlayerPrefs.HasKey("pv.audioVolume");
            float stored = PlayerPrefs.GetFloat("pv.audioVolume", 0);
            slider.value = .62f;
            Assert.That(GameAudio.Volume, Is.EqualTo(.62f).Within(.001f), "a drag on the SD screen must reach GameAudio");

            hits.Clear();
            EventSystem.current.RaycastAll(pointer, hits);
            top = hits[0].gameObject;
            bool stillInBar = top == slider.gameObject;
            for (var t = top.transform; t != null; t = t.parent)
                if (t == slider.transform) { stillInBar = true; break; }
            Assert.That(stillInBar, Is.True, "changing the value pulled the bar under " + top.name);

            GameAudio.SetVolume(original);
            if (hadContinuous) PlayerPrefs.SetFloat("pv.audioVolume", stored);
            else PlayerPrefs.DeleteKey("pv.audioVolume");
            PlayerPrefs.Save();
            yield return null;
        }

        [UnityTest] public IEnumerator BackgroundPullMovesAllDice_AndButtonOriginsCannotFire()
        {
            var battle = Root("_sdBattle");
            var homes = DiceHomes();
            var start = ScreenAtLocal(new Vector2(-335, -150));
            var end = start + Vector2.down * 100;
            var button = Read<Button>("_sdSaveButton");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = start, pressPosition = start, pointerDrag = battle.gameObject,
                pointerPressRaycast = new RaycastResult { gameObject = button.gameObject } };
            ExecuteEvents.Execute(battle.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = end;
            ExecuteEvents.Execute(battle.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(battle.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(Engine.History.Count, Is.Zero); AssertHomes(homes);
            pointer.position = start;
            pointer.pointerPressRaycast = new RaycastResult { gameObject = battle.Find("TablePointerSurface").gameObject };
            ExecuteEvents.Execute(battle.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = end;
            ExecuteEvents.Execute(battle.gameObject, pointer, ExecuteEvents.dragHandler);
            for (int i = 0; i < 5; i++) Assert.That(Die(i).anchoredPosition.y, Is.LessThan(homes[i].y - 20));
            Assert.That(Engine.History.Count, Is.Zero);
            ExecuteEvents.Execute(battle.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(Engine.History.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { false, false, false, false, false }, Engine.Held);
            yield return new WaitForSeconds(.95f);
            AssertHomes(homes);
        }

        [UnityTest] public IEnumerator ThreeHeldDiceShowRightClickGuide_WithoutActionButtons()
        {
            Assert.That(Root("_sdBattle").Find("Reroll"), Is.Null);
            Assert.That(Root("_sdBattle").Find("Commit"), Is.Null);
            Assert.That(Root("_sdPractice").Find("PracticeReroll"), Is.Null);
            Assert.That(Root("_sdPractice").Find("PracticeCommit"), Is.Null);
            Engine.SetHeld(new[] { true, true, true, false, false }); Call("SDRefresh");
            Assert.That(Read<Text>("_sdInstruction").text, Is.Empty);
            Assert.That(Root("_sdBattle").Find("RerollSymbol"), Is.Null,
                "remaining rerolls are shown by the two dots below health, without a separate icon");
            yield return null;
            Assert.That(Root("_sdBattle").Find("GestureGuide").gameObject.activeInHierarchy, Is.True);
            Engine.SetHeld(new[] { true, true, false, false, false }); Call("SDRefresh");
            Assert.That(Read<Text>("_sdInstruction").text, Is.Empty);
            Call("SDHandleRightClick");
            yield return null;
            Assert.That(Root("_sdBattle").Find("GestureGuide").gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest] public IEnumerator HorizontalSweepPaintsCrossedDiceOnce_EvenWhenThePointerJumpsAndReverses()
        {
            var data = BeginDrag(0);
            Drag(data, ScreenAtDie(2));
            Drag(data, ScreenAtDie(0));
            EndDrag(data, ScreenAtDie(0));
            CollectionAssert.AreEqual(new[] { true, true, true, false, false }, Engine.Held,
                "the physical sweep crosses Die1 even if no intermediate drag sample lands on it");
            Assert.That(Engine.History.Count, Is.Zero);
            yield return null;

            data = BeginDrag(0);
            Drag(data, ScreenAtDie(2));
            Drag(data, ScreenAtDie(0));
            EndDrag(data, ScreenAtDie(0));
            CollectionAssert.AreEqual(new[] { false, false, false, false, false }, Engine.Held,
                "a reverse visit must not toggle an already painted die a second time");
            Assert.That(Read<bool>("_sdDragging"), Is.False);
        }

        [UnityTest] public IEnumerator DownwardPullCommitsTheActualSelectedCategoryOnce_ThenRestoresTheRenderedDice()
        {
            PressLive(ScoreHandKind.Chance);
            var forecast = Engine.Forecast(ScoreHandKind.Chance);
            Vector2[] homes = DiceHomes();
            var data = BeginDrag(0);
            Vector2 target = ScreenAtLocal(homes[0] + Vector2.down * 120);
            Drag(data, target);
            Assert.That(Die(0).anchoredPosition.y, Is.LessThan(homes[0].y - 20));
            Assert.That(Engine.History.Count, Is.Zero);
            EndDrag(data, target);
            EndDrag(data, target); // A second delivery after release must not resolve twice.
            Assert.That(Engine.History.Count, Is.EqualTo(1));
            Assert.That(Engine.LastRound.PlayerKind, Is.EqualTo(ScoreHandKind.Chance));
            Assert.That(Engine.LastRound.DamageToPlayer, Is.EqualTo(forecast.DamageToPlayer));
            Assert.That(Engine.LastRound.DamageToNpc, Is.EqualTo(forecast.DamageToNpc));
            Assert.That(Root("_sdBattle").Find("Advance"), Is.Null);
            TableClick();
            Assert.That(Engine.Round, Is.EqualTo(1), "a running flight cannot leak into the next turn");
            yield return new WaitForSeconds(.95f);
            AssertHomes(homes);
            Assert.That(Read<float>("_sdAttackAge"), Is.LessThan(0));
        }

        [UnityTest] public IEnumerator TableClickRequiresAFreshPressAfterRecovery_AndAdvancesOnlyOnce()
        {
            Assert.That(Root("_sdBattle").Find("Advance"), Is.Null);
            PullToFire();
            Assert.That(Engine.History.Count, Is.EqualTo(1));
            int round = Engine.Round;
            var battle = Root("_sdBattle");
            var release = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, battle.position),
                pointerPressRaycast = new RaycastResult { gameObject = battle.gameObject },
                pointerCurrentRaycast = new RaycastResult { gameObject = battle.gameObject } };
            ExecuteEvents.Execute(battle.gameObject, release, ExecuteEvents.pointerClickHandler);
            Assert.That(Engine.Round, Is.EqualTo(round), "the commit release cannot advance");
            ExecuteEvents.Execute(battle.gameObject, release, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(battle.gameObject, release, ExecuteEvents.pointerClickHandler);
            Assert.That(Engine.Round, Is.EqualTo(round), "a click during attack is ignored");
            yield return new WaitForSeconds(.95f);
            ExecuteEvents.Execute(battle.gameObject, release, ExecuteEvents.pointerClickHandler);
            Assert.That(Engine.Round, Is.EqualTo(round), "a press begun during attack stays ineligible after recovery");
            Call("SDOpenRules");
            TableClick();
            Assert.That(Engine.Round, Is.EqualTo(round), "modal blocks background clicks");
            Press("_sdRules", "CloseRules");
            TableClick();
            Assert.That(Engine.Round, Is.EqualTo(round), "closing the modal consumes this frame");
            yield return null;
            TableClick();
            TableClick("Die0");
            Assert.That(Engine.Round, Is.EqualTo(round + 1), "background and die callbacks in one frame advance once");
            Assert.That(Engine.History.Count, Is.EqualTo(1));
            yield return new WaitForSeconds(.8f);
            PullToFire();
            yield return new WaitForSeconds(.95f);
            TableClick("Die0");
            Assert.That(Engine.Round, Is.EqualTo(round + 2), "a recovered turn also accepts a fresh die click");
            Assert.That(Engine.Held[0], Is.False, "advancing with a die does not hold the next hand");
        }
        [UnityTest] public IEnumerator ShortDownwardDragCancels_AndRestoresTheOriginalHoldsAndPositions()
        {
            Press("_sdBattle", "Die0");
            bool[] original = (bool[])Engine.Held.Clone();
            Vector2[] homes = DiceHomes();
            var data = BeginDrag(1);
            Vector2 target = ScreenAtLocal(homes[1] + Vector2.down * 24);
            Drag(data, target);
            Assert.That(Die(1).anchoredPosition.y, Is.LessThan(homes[1].y - 10));
            EndDrag(data, target);
            AssertHomes(homes);
            CollectionAssert.AreEqual(original, Engine.Held);
            Assert.That(Engine.History.Count, Is.Zero);
            Assert.That(Read<bool>("_sdDragging"), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator OpeningAModalMidDragRestoresTheHand_AndBlocksPointerKeyboardAndRightClick()
        {
            bool[] original = (bool[])Engine.Held.Clone();
            Vector2[] homes = DiceHomes();
            var data = BeginDrag(0);
            Vector2 target = ScreenAtLocal(homes[0] + Vector2.down * 110);
            Drag(data, target);
            Call("SDOpenRules");
            AssertHomes(homes);
            CollectionAssert.AreEqual(original, Engine.Held);
            Assert.That(Read<bool>("_sdDragging"), Is.False);
            EndDrag(data, target);
            BeginDrag(0);
            Call("SDHandleRightClick");
            Call("SDHandleShortcut", KeyCode.Space);
            Call("SDCommit");
            Assert.That(Engine.RerollsLeft, Is.EqualTo(2));
            Assert.That(Engine.History.Count, Is.Zero);
            foreach (Button category in Read<Button[]>("_sdLiveCategories")) Assert.That(category.IsInteractable(), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator RightClickObeysModalHoldRollingAndTwoRerollLimits()
        {
            Call("SDOpenRules");
            Call("SDHandleRightClick");
            Assert.That(Engine.RerollsLeft, Is.EqualTo(2));
            Call("SDCloseModals");
            yield return null;
            for (int i = 0; i < 5; i++) Press("_sdBattle", "Die" + i);
            Call("SDHandleRightClick");
            Assert.That(Engine.RerollsLeft, Is.EqualTo(2), "keeping all five must not consume a reroll");
            Assert.That(Read<bool>("_sdRolling"), Is.False);
            yield return null;
            Press("_sdBattle", "Die2");
            int[] before = (int[])Engine.PlayerDice.Clone();
            Call("SDHandleRightClick");
            Assert.That(Engine.RerollsLeft, Is.EqualTo(1));
            Call("SDHandleRightClick");
            Assert.That(Engine.RerollsLeft, Is.EqualTo(1), "one frame or rolling hand cannot reroll twice");
            for (int i = 0; i < 5; i++) if (i != 2) Assert.That(Engine.PlayerDice[i], Is.EqualTo(before[i]));
            yield return new WaitForSeconds(.8f);
            Call("SDHandleRightClick");
            Assert.That(Engine.RerollsLeft, Is.Zero);
            yield return new WaitForSeconds(.8f);
            Call("SDHandleRightClick");
            Assert.That(Engine.RerollsLeft, Is.Zero);
            Assert.That(Engine.History.Count, Is.Zero);
        }

        [UnityTest] public IEnumerator SaveToggleIsReversible_AndOnlyAFullPullCarriesTheDiceIntoTheNextTurn()
        {
            Press("_sdBattle", "Die0");
            Press("_sdBattle", "Die3");
            int[] dice = (int[])Engine.PlayerDice.Clone();
            bool[] held = (bool[])Engine.Held.Clone();
            ScoreHandKind category = Read<ScoreHandKind>("_sdSelectedKind");
            var normal = Engine.Forecast(category);
            var saved = Engine.Forecast(category, true);
            int playerHp = Engine.PlayerHp, npcHp = Engine.NpcHp;
            var save = Read<Button>("_sdSaveButton");
            yield return null;
            save.onClick.Invoke();
            Assert.That(Read<bool>("_sdSaveArmed"), Is.True);
            Assert.That(Engine.History.Count, Is.Zero, "arming carry must not resolve the turn");
            Assert.That(Engine.SavedHandUsed, Is.False);
            Assert.That(Engine.PlayerUsed[(int)category], Is.False);
            Assert.That(Engine.PlayerHp, Is.EqualTo(playerHp));
            Assert.That(Engine.NpcHp, Is.EqualTo(npcHp));
            CollectionAssert.AreEqual(dice, Engine.PlayerDice);
            CollectionAssert.AreEqual(held, Engine.Held);
            AssertForecastNumbers(saved);
            yield return null;
            save.onClick.Invoke();
            Assert.That(Read<bool>("_sdSaveArmed"), Is.False);
            AssertForecastNumbers(normal);
            Assert.That(Engine.History.Count, Is.Zero);
            yield return null;
            save.onClick.Invoke();
            yield return null;
            Vector2[] homes = DiceHomes();
            var pointer = BeginDrag(1);
            Vector2 shortPull = ScreenAtLocal(homes[1] + Vector2.down * 24);
            Drag(pointer, shortPull); EndDrag(pointer, shortPull);
            Assert.That(Engine.History.Count, Is.Zero, "canceling a short pull keeps the turn unresolved");
            Assert.That(Read<bool>("_sdSaveArmed"), Is.True, "canceling the gesture preserves the chosen carry mode");
            AssertHomes(homes);
            CollectionAssert.AreEqual(dice, Engine.PlayerDice);
            CollectionAssert.AreEqual(held, Engine.Held);
            AssertForecastNumbers(saved);
            yield return null;
            PullToFire();
            Assert.That(Engine.LastRound.IsSaved, Is.True);
            Assert.That(Engine.LastRound.DamageToPlayer, Is.EqualTo(saved.DamageToPlayer));
            Assert.That(Engine.LastRound.DamageToNpc, Is.EqualTo(saved.DamageToNpc));
            Assert.That(Engine.PlayerUsed[(int)category], Is.True);
            Assert.That(Read<bool>("_sdSaveArmed"), Is.False, "the completed release consumes the armed mode");
            Assert.That(Engine.Outcome, Is.Null, "the chosen seeded first turn permits a carry test");
            yield return new WaitForSeconds(.95f);
            TableClick();
            yield return new WaitForSeconds(.6f);
            CollectionAssert.AreEqual(dice, Engine.PlayerDice);
            CollectionAssert.AreEqual(held, Engine.Held);
            Assert.That(Engine.RerollsLeft, Is.EqualTo(2));
            Assert.That(Read<bool>("_sdRolling"), Is.False, "saved dice must not be silently rerolled");
            Assert.That(Engine.SavedHandUsed, Is.True);
            Assert.That(save.interactable, Is.False);
            Assert.That(Read<Button[]>("_sdLiveCategories")[(int)category].interactable, Is.False);
            Assert.That(Read<Text[]>("_sdLiveValues")[(int)category].text, Is.EqualTo("—"));
            int count = Engine.History.Count;
            Call("SDSaveHand");
            Assert.That(Engine.History.Count, Is.EqualTo(count));
            Assert.That(Read<bool>("_sdSaveArmed"), Is.False);
        }

        [UnityTest] public IEnumerator DisarmingCarryMakesTheNormalPullUseTheNormalForecast()
        {
            var save = Read<Button>("_sdSaveButton");
            yield return null; save.onClick.Invoke();
            Assert.That(Read<bool>("_sdSaveArmed"), Is.True);
            yield return null; save.onClick.Invoke();
            Assert.That(Read<bool>("_sdSaveArmed"), Is.False);
            ScoreHandKind category = Read<ScoreHandKind>("_sdSelectedKind");
            var normal = Engine.Forecast(category);
            AssertForecastNumbers(normal);
            yield return null; PullToFire();
            Assert.That(Engine.LastRound.IsSaved, Is.False);
            Assert.That(Engine.LastRound.DamageToNpc, Is.EqualTo(normal.DamageToNpc));
            Assert.That(Engine.LastRound.DamageToPlayer, Is.EqualTo(normal.DamageToPlayer));
            Assert.That(Engine.SavedHandUsed, Is.False);
        }
        [UnityTest] public IEnumerator VisibleCategoryButtonsShowLegalUnformedChoices_AndUpdateTheActualForecast()
        {
            var choices = Read<Button[]>("_sdLiveCategories");
            Assert.That(choices.Length, Is.EqualTo(9));
            ScoreHandKind unformedKind = ScoreHandKind.Five;
            foreach (ScoreOffer offer in Engine.Offers)
            {
                Assert.That(choices[(int)offer.Kind].gameObject.activeInHierarchy, Is.True);
                if (!offer.IsFormed) unformedKind = offer.Kind;
            }
            var unformed = ScoreRules.Evaluate(unformedKind, Engine.PlayerDice);
            Assert.That(unformed.IsFormed, Is.False);
            Assert.That(unformed.BonusScore, Is.Zero);
            Assert.That(unformed.BaseScore, Is.GreaterThanOrEqualTo(5));
            Assert.That(unformed.Score, Is.EqualTo(unformed.BaseScore));
            PressLive(unformedKind);
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(unformedKind));
            Assert.That(Read<Text>("_sdRoleLabel").text, Is.EqualTo(ScoreRules.Name(unformedKind)));
            Assert.That(Read<Text>("_sdScoreNumber").text, Is.EqualTo(unformed.Score.ToString()));
            Assert.That(Read<Image[]>("_sdLiveMarkers")[(int)unformedKind].gameObject.activeSelf, Is.True);
            var forecast = Engine.Forecast(unformedKind);
            AssertForecastNumbers(forecast);
            PressLive(ScoreHandKind.Chance);
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(ScoreHandKind.Chance));
            Assert.That(Read<Image[]>("_sdLiveMarkers")[(int)unformedKind].gameObject.activeSelf, Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator ChanceSelectionGathersTheHandInGoldAndLeavesNoCueLine()
        {
            PressLive(ScoreHandKind.Chance);
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(ScoreHandKind.Chance));
            yield return null;
            Assert.That(Read<RectTransform>("_sdChanceGlowRoot").gameObject.activeSelf, Is.True,
                "a chance selection must ring the hand in gold while the role is still changeable");
            ScoreHandKind plain = ScoreHandKind.Pair;
            foreach (ScoreOffer offer in Engine.Offers)
                if (offer.IsAvailable && offer.Kind != ScoreHandKind.Chance) { plain = offer.Kind; break; }
            PressLive(plain);
            yield return null;
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(plain));
            Assert.That(Read<RectTransform>("_sdChanceGlowRoot").gameObject.activeSelf, Is.False,
                "the gold gathering belongs to the chance selection alone");
            bool anyCue = false;
            foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.name == "ChanceCue") anyCue = true;
            Assert.That(anyCue, Is.False, "the cue text is retired; the glow and the rule card carry the reversal");
            yield return null;
        }

        [UnityTest] public IEnumerator ScoreNumeralsReadAsStruckMetal()
        {
            Text mine = Read<Text>("_sdScoreNumber");
            Text npc = Read<Text>("_sdNpcScoreNumber");
            foreach (Text numeral in new[] { mine, npc })
            {
                var effects = numeral.GetComponents<BaseMeshEffect>();
                Assert.That(effects.Length, Is.EqualTo(2), "a score numeral carries the gradient and then the dark edge");
                Assert.That(effects[0].GetType().Name, Is.EqualTo("SdMetallicTextEffect"),
                    "the gradient must tint the glyphs before the outline darkens its copies");
                Assert.That(effects[1].GetType(), Is.EqualTo(typeof(Outline)));
            }
            Assert.That(mine.fontSize, Is.EqualTo(64), "the player's own figure is the loudest number on screen");
            Assert.That(npc.fontSize, Is.EqualTo(50), "the enemy forecast stays the quieter figure");
            yield return null;
        }

        [UnityTest] public IEnumerator RoleRailsPrintTheirMultiplierTag()
        {
            var categories = Read<Button[]>("_sdLiveCategories");
            Assert.That(categories[(int)ScoreHandKind.Pair].transform.Find("Multiplier").GetComponent<Text>().text, Is.EqualTo("×1.5"));
            Assert.That(categories[(int)ScoreHandKind.FullHouse].transform.Find("Multiplier").GetComponent<Text>().text, Is.EqualTo("×2.5"));
            Assert.That(categories[(int)ScoreHandKind.Five].transform.Find("Multiplier").GetComponent<Text>().text, Is.EqualTo("150点"));
            Assert.That(categories[(int)ScoreHandKind.Chance].transform.Find("Multiplier").GetComponent<Text>().text, Is.EqualTo("逆転"));
            var npcRow = Root("_sdBattle").Find("NpcScorecard/NpcCategory0");
            Assert.That(npcRow.Find("Multiplier").GetComponent<Text>().text, Is.EqualTo("×1.5"),
                "the enemy rail prints the same multipliers beside its roles");
            yield return null;
        }

        [UnityTest] public IEnumerator RerollPipsBreatheWhileRerollsRemain()
        {
            var pips = Read<Image[]>("_sdRollPips");
            FieldInfo age = typeof(GameFlow).GetField("_sdRollPipPulseAge", Private);
            Assert.That(pips[0].gameObject.activeSelf, Is.True);
            Assert.That(pips[1].gameObject.activeSelf, Is.True);
            age.SetValue(_flow, Mathf.PI / 2f / 4.2f);
            Call("SDTickRollPipPulse", 0f);
            float brightest = pips[0].color.a;
            float widest = pips[0].transform.localScale.x;
            age.SetValue(_flow, 3f * Mathf.PI / 2f / 4.2f);
            Call("SDTickRollPipPulse", 0f);
            float dim = pips[0].color.a;
            Assert.That(brightest, Is.EqualTo(1f).Within(.01f), "the pulse peaks at full strength");
            Assert.That(dim, Is.EqualTo(.92f).Within(.01f), "the pulse dips gently rather than flashing");
            Assert.That(widest, Is.EqualTo(1.08f).Within(.01f),
                "the same breath carries a bigger claim on the dot itself");
            yield return null;
        }

        [UnityTest] public IEnumerator ArmedCarryRackRingsEveryDieAsKept()
        {
            Button save = Read<Button>("_sdSaveButton");
            // Reduced motion settles the toggle the same frame; while it flies the sparks own
            // the stroke, and this probe must read the settled drawing.
            Write("_sdReducedMotion", true);
            save.onClick.Invoke();
            yield return null;
            Assert.That(Read<bool>("_sdSaveArmed"), Is.True);
            var rack = Read<RectTransform>("_sdCarryRack");
            Assert.That(rack.gameObject.activeSelf, Is.True, "the armed hand previews on the rack");
            for (int i = 0; i < 5; i++)
            {
                var ring = rack.Find("CarryDie" + i)?.Find("Ring");
                Assert.That(ring, Is.Not.Null, "rack die " + i + " keeps its ring");
                Assert.That(ring.GetComponent<Image>().color, Is.EqualTo(GoldRing),
                    "the whole hand travels: every die shows the keep ring, not just the held ones");
                // A 10-unit pad on a 19-unit die reaches past the 27-unit pitch and fuses five
                // keeps into one band; the tiny cap must stay inside it to read per die.
                Assert.That(((RectTransform)ring).sizeDelta.x, Is.EqualTo(25f),
                    "rack die " + i + " wears its own cap, clear of the neighbours at 27-unit pitch");
            }
        }

        [UnityTest] public IEnumerator AfterTheProposalSettlesItPromotesUpward()
        {
            Array.Copy(new[] { 3, 3, 3, 4, 4 }, Engine.PlayerDice, 5);
            ScoreHandKind expected = ScoreHandKind.Chance;
            int best = -1;
            foreach (ScoreOffer offer in Engine.Offers)
                if (offer.IsFormed && offer.IsAvailable && offer.Kind != ScoreHandKind.Chance && offer.Score > best)
                { best = offer.Score; expected = offer.Kind; }
            Call("SDPromoteFormedSelection");
            Assert.That(expected, Is.Not.EqualTo(ScoreHandKind.Chance), "the fixture must contain a formed role");
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(expected),
                "the standing proposal moves to the strongest formed hand the dice now make");
            PressLive(ScoreHandKind.Pair);
            Call("SDPromoteFormedSelection");
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(expected),
                "a reroll that lands a stronger formed hand promotes the manual pick up to it");
            Array.Copy(new[] { 2, 3, 4, 6, 6 }, Engine.PlayerDice, 5);
            PressLive(ScoreHandKind.Pair);
            Call("SDPromoteFormedSelection");
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(ScoreHandKind.Pair),
                "no stronger hand, no promotion: the pick keeps the wheel");
            yield return null;
        }

        [UnityTest] public IEnumerator DamageBandsStayInsideThePillCaps()
        {
            var generate = typeof(GameFlow).GetMethod("SDBarSegmentSprite", Private | BindingFlags.Static);
            Assert.That(generate, Is.Not.Null);
            Sprite full = (Sprite)generate.Invoke(null, new object[] { 572f });
            Sprite slice = (Sprite)generate.Invoke(null, new object[] { 32f });
            Assert.That(slice, Is.Not.EqualTo(full),
                "a narrow segment is authored at its own width, never a stretch of the full pill cap");
            Assert.That((Sprite)generate.Invoke(null, new object[] { 34f }), Is.EqualTo(slice),
                "segment widths quantize to one cache, so the amount of authored pills stays bounded");
            var ghost = new GameObject("GhostProbe").AddComponent<Image>();
            ghost.rectTransform.SetParent(Root("_sdBattle"), false);
            var place = typeof(GameFlow).GetMethod("SDPlaceGhost", Private | BindingFlags.Static);
            Assert.That(place, Is.Not.Null);
            place.Invoke(null, new object[] { ghost, 100, 100, 1, true });
            float right = ghost.rectTransform.anchoredPosition.x + ghost.rectTransform.sizeDelta.x;
            Assert.That(right, Is.LessThanOrEqualTo(572f + .1f), "no damage band may pass the pill's rounded end");
            Object.Destroy(ghost.gameObject);
            yield return null;
        }

        [UnityTest] public IEnumerator WheelMovesOneRolePerNotchInBothDirections_Wraps_AndRefreshesTheForecast()
        {
            int[] hand = (int[])Engine.PlayerDice.Clone();
            bool[] held = (bool[])Engine.Held.Clone();
            PressLive(ScoreHandKind.Pair);
            yield return null;
            Call("SDHandleWheel", -1f);
            AssertWheelSelection(ScoreHandKind.TwoPair);
            Call("SDHandleWheel", -1f);
            AssertWheelSelection(ScoreHandKind.TwoPair); // No second action in the same input frame.
            yield return null;
            Call("SDHandleWheel", 1f);
            AssertWheelSelection(ScoreHandKind.Pair);
            yield return null;
            Call("SDHandleWheel", 1f);
            AssertWheelSelection(ScoreHandKind.Chance);
            yield return null;
            Call("SDHandleWheel", -1f);
            AssertWheelSelection(ScoreHandKind.Pair);
            CollectionAssert.AreEqual(hand, Engine.PlayerDice);
            CollectionAssert.AreEqual(held, Engine.Held);
            Assert.That(Engine.RerollsLeft, Is.EqualTo(2));
            Assert.That(Engine.History.Count, Is.Zero);
        }

        [UnityTest] public IEnumerator FractionalWheelMotionAccumulates_MultipleNotchesKeepTheirDistance_AndModalMotionIsDiscarded()
        {
            PressLive(ScoreHandKind.Pair);
            yield return null;
            Call("SDHandleWheel", -.4f);
            AssertWheelSelection(ScoreHandKind.Pair);
            yield return null;
            Call("SDHandleWheel", -.35f);
            AssertWheelSelection(ScoreHandKind.Pair);
            yield return null;
            Call("SDHandleWheel", -.25f);
            AssertWheelSelection(ScoreHandKind.TwoPair);
            Assert.That(Read<float>("_sdWheelRemainder"), Is.Zero.Within(.00001f));
            yield return null;
            Call("SDHandleWheel", -3f);
            AssertWheelSelection(ScoreHandKind.SmallStraight);
            yield return null;
            Call("SDHandleWheel", 2f);
            AssertWheelSelection(ScoreHandKind.Three);
            yield return null;
            Call("SDHandleWheel", -.75f);
            AssertWheelSelection(ScoreHandKind.Three);
            Call("SDOpenRules");
            Call("SDHandleWheel", -1f);
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(ScoreHandKind.Three));
            Assert.That(Read<float>("_sdWheelRemainder"), Is.Zero);
            Call("SDCloseModals");
            yield return null;
            Call("SDHandleWheel", -.25f);
            AssertWheelSelection(ScoreHandKind.Three);
            Call("SDHandleWheel", float.NaN);
            Call("SDHandleWheel", float.PositiveInfinity);
            AssertWheelSelection(ScoreHandKind.Three);
            Assert.That(Read<float>("_sdWheelRemainder"), Is.Zero);
        }

        [UnityTest] public IEnumerator WheelSkipsAUsedRole_AndCannotChangeTheChoiceDuringDragRollCommitOrTitle()
        {
            PressLive(ScoreHandKind.Pair);
            yield return null; Read<Button>("_sdSaveButton").onClick.Invoke();
            Assert.That(Engine.History.Count, Is.Zero);
            yield return null; PullToFire();
            Assert.That(Engine.PlayerUsed[(int)ScoreHandKind.Pair], Is.True);
            Call("SDHandleWheel", -1f);
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(ScoreHandKind.Pair));
            Assert.That(Engine.Outcome, Is.Null);
            yield return new WaitForSeconds(.95f);
            TableClick();
            PressLive(ScoreHandKind.Chance);
            yield return new WaitForSeconds(.8f);
            Call("SDHandleWheel", -1f);
            AssertWheelSelection(ScoreHandKind.TwoPair);
            Assert.That(Read<Button[]>("_sdLiveCategories")[(int)ScoreHandKind.Pair].interactable, Is.False);
            Assert.That(Read<Image[]>("_sdLiveMarkers")[(int)ScoreHandKind.Pair].gameObject.activeSelf, Is.False);
            yield return null;
            var pointer = BeginDrag(0);
            Call("SDHandleWheel", -1f);
            AssertWheelSelection(ScoreHandKind.TwoPair);
            EndDrag(pointer, ScreenAtDie(0));
            yield return null;
            Call("SDHandleRightClick");
            Assert.That(Read<bool>("_sdRolling"), Is.True);
            ScoreHandKind before = Read<ScoreHandKind>("_sdSelectedKind");
            Call("SDHandleWheel", -1f);
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(before));
            yield return new WaitForSeconds(.8f);
            before = Read<ScoreHandKind>("_sdSelectedKind");
            Call("SDCommit");
            Call("SDHandleWheel", -1f);
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(before));
            Assert.That(Engine.History.Count, Is.EqualTo(2));
            Assert.That(Engine.LastRound.PlayerKind, Is.EqualTo(before));
            ScoreDuel completedMatch = Engine;
            Call("SDBackToTitle");
            Call("SDHandleWheel", -1f);
            Assert.That(Engine, Is.Null, "returning to the title releases the active match");
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(before));
            Assert.That(completedMatch.History.Count, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator WheelSelectedUnformedTargetSurvivesARealReroll_AndNextTurnReturnsToAnUnusedBestResult()
        {
            Assert.That(Read<bool>("_sdSelectedManually"), Is.False);
            Assert.That(ScoreRules.Evaluate(ScoreHandKind.Five, Engine.PlayerDice).IsFormed, Is.False,
                "this seeded hand lets the player choose an unformed target rather than the opening suggestion");
            int current = (int)Read<ScoreHandKind>("_sdSelectedKind");
            int distance = ((int)ScoreHandKind.Five - current + ScoreRules.HandCount) % ScoreRules.HandCount;
            Assert.That(distance, Is.GreaterThan(0));
            Call("SDHandleWheel", -(float)distance);
            AssertWheelSelection(ScoreHandKind.Five);
            Assert.That(Read<bool>("_sdSelectedManually"), Is.True);
            yield return null;
            Call("SDHandleRightClick");
            Assert.That(Read<bool>("_sdRolling"), Is.True);
            Assert.That(Engine.RerollsLeft, Is.EqualTo(1));
            yield return new WaitForSeconds(.8f);
            Assert.That(Read<bool>("_sdRolling"), Is.False);
            // A reroll that lands a better formed hand promotes the wheel back to it; the
            // unformed Five itself stays picked only when nothing formed beats its forecast.
            ScoreHandKind expected = ScoreHandKind.Five;
            int expectedScore = Engine.Forecast(ScoreHandKind.Five).Score;
            foreach (ScoreOffer offer in Engine.Offers)
                if (offer.IsFormed && offer.IsAvailable && offer.Score > expectedScore)
                {
                    expectedScore = offer.Score;
                    expected = offer.Kind;
                }
            AssertWheelSelection(expected);

            PullToFire();
            Assert.That(Engine.LastRound.PlayerKind, Is.EqualTo(expected));
            Assert.That(Engine.PlayerUsed[(int)expected], Is.True);
            Assert.That(Engine.Outcome, Is.Null);
            yield return new WaitForSeconds(.95f);
            TableClick();
            Assert.That(Read<bool>("_sdSelectedManually"), Is.False);
            yield return new WaitForSeconds(.8f);
            Assert.That(Read<bool>("_sdRolling"), Is.False);
            ScoreHandKind next = Read<ScoreHandKind>("_sdSelectedKind");
            Assert.That(next, Is.Not.EqualTo(expected));
            Assert.That(Engine.PlayerUsed[(int)next], Is.False);
            AssertWheelSelection(next);
            int score = Engine.Forecast(next).Score;
            foreach (ScoreOffer offer in Engine.Offers)
                if (offer.IsAvailable) Assert.That(score, Is.GreaterThanOrEqualTo(offer.Score));
            Assert.That(Engine.Round, Is.EqualTo(2));
            Assert.That(Engine.History.Count, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator BothStraightsTraceThePhysicalDiceInActualFaceOrder()
        {
            int[][] hands = { new[] { 4, 1, 6, 2, 3 }, new[] { 4, 1, 5, 2, 3 } };
            ScoreHandKind[] kinds = { ScoreHandKind.SmallStraight, ScoreHandKind.LargeStraight };
            for (int fixture = 0; fixture < hands.Length; fixture++)
            {
                ApplyVisualFixture(hands[fixture]);
                Call("SDPlayHandEffects", ScoreRules.Evaluate(kinds[fixture], hands[fixture]));
                int count = Read<int>("_sdFxPathCount");
                Assert.That(count, Is.EqualTo(fixture + 4));
                int[] path = Read<int[]>("_sdFxPath");
                for (int i = 0; i < count; i++) Assert.That(hands[fixture][path[i]], Is.EqualTo(i + 1));
                Call("SDTickHandEffects", Read<float>("_sdFxDuration") * .31f);
                Assert.That(Read<Image>("_sdFxTraceHead").gameObject.activeInHierarchy, Is.True);
                Assert.That(Read<bool>("_sdFxActive"), Is.True);
                if (fixture == 0) Assert.That(Die(2).localScale.x, Is.EqualTo(1).Within(.001f), "the unused six is outside the four-die path");
                Call("SDResetHandEffects");
                AssertBaseScales();
            }
            yield return null;
        }

        [UnityTest] public IEnumerator FullHousePulsesItsThreeAndTwoSeparately_AndFiveIsStrongerThanPair()
        {
            int[] pair = { 2, 2, 4, 5, 6 };
            ApplyVisualFixture(pair);
            Vector2[] homes = DiceHomes();
            float originalHeight = RenderedHeight(Die(0));
            Call("SDPlayHandEffects", ScoreRules.Evaluate(ScoreHandKind.Pair, pair));
            Call("SDTickHandEffects", Read<float>("_sdFxDuration") * .23f);
            float pairHeight = RenderedHeight(Die(0));
            Assert.That(pairHeight, Is.GreaterThan(originalHeight));

            int[] fullHouse = { 2, 5, 2, 5, 2 };
            ApplyVisualFixture(fullHouse);
            Call("SDPlayHandEffects", ScoreRules.Evaluate(ScoreHandKind.FullHouse, fullHouse));
            CollectionAssert.AreEqual(new[] { 21, 10 }, Read<int[]>("_sdFxGroups"));
            float duration = Read<float>("_sdFxDuration");
            Call("SDTickHandEffects", duration * .18f);
            foreach (int index in new[] { 0, 2, 4 }) Assert.That(RenderedHeight(Die(index)), Is.GreaterThan(originalHeight * 1.2f));
            foreach (int index in new[] { 1, 3 }) Assert.That(Die(index).localScale.x, Is.EqualTo(1).Within(.001f));
            AssertRenderedGaps(3f);
            AssertEffectCentersMatchDice();
            Call("SDTickHandEffects", duration * (.46f - .18f));
            foreach (int index in new[] { 1, 3 }) Assert.That(RenderedHeight(Die(index)), Is.GreaterThan(originalHeight * 1.2f));
            AssertRenderedGaps(3f);
            Call("SDResetHandEffects");
            AssertHomes(homes);
            AssertBaseScales();

            int[] five = { 1, 1, 1, 1, 1 };
            ApplyVisualFixture(five);
            Call("SDPlayHandEffects", ScoreRules.Evaluate(ScoreHandKind.Five, five));
            Call("SDTickHandEffects", Read<float>("_sdFxDuration") * .23f);
            foreach (RectTransform die in DiceRects()) Assert.That(RenderedHeight(die), Is.GreaterThan(pairHeight * 1.15f));
            Assert.That(Read<int>("_sdFxSparkCount"), Is.GreaterThan(20));
            AssertRenderedGaps(9f);
            Assert.That(Die(0).anchoredPosition.x, Is.LessThan(homes[0].x - 59f));
            Assert.That(Die(4).anchoredPosition.x, Is.GreaterThan(homes[4].x + 59f));
            AssertEffectCentersMatchDice();
            Call("SDTickHandEffects", Read<float>("_sdFxDuration"));
            Assert.That(Read<bool>("_sdFxActive"), Is.False);
            AssertHomes(homes);
            AssertBaseScales();
            Call("SDPlayHandEffects", ScoreRules.Evaluate(ScoreHandKind.Five, five));
            Call("SDTickHandEffects", Read<float>("_sdFxDuration") * .23f);
            var pointer = BeginDrag(0);
            Assert.That(Read<bool>("_sdFxActive"), Is.False);
            AssertHomes(homes);
            EndDrag(pointer, ScreenAtDie(0));
            AssertHomes(homes);
            yield return null;
        }

        [UnityTest] public IEnumerator ReducedMotionAvoidsEnlargement_AndRerollAndModalResetEffectsImmediately()
        {
            int[] five = { 1, 1, 1, 1, 1 };
            ApplyVisualFixture(five);
            Vector2[] homes = DiceHomes();
            Write("_sdReducedMotion", true);
            Call("SDPlayHandEffects", ScoreRules.Evaluate(ScoreHandKind.Five, five));
            Call("SDTickHandEffects", .10f);
            AssertBaseScales();
            AssertHomes(homes);
            Assert.That(Read<int>("_sdFxSparkCount"), Is.Zero);
            Assert.That(Read<Image>("_sdFxTraceHead").gameObject.activeSelf, Is.False);
            Assert.That(Read<bool>("_sdFxActive"), Is.True, "the restrained static indicator remains available");
            Call("SDResetHandEffects");
            Write("_sdReducedMotion", false);
            ApplyVisualFixture(five);
            Call("SDPlayHandEffects", ScoreRules.Evaluate(ScoreHandKind.Five, five));
            Call("SDTickHandEffects", .20f);
            Assert.That(Die(0).localScale.x, Is.GreaterThan(1.2f));
            Call("SDHandleRightClick");
            Assert.That(Read<bool>("_sdFxActive"), Is.False);
            AssertBaseScales();
            AssertHomes(homes);
            yield return new WaitForSeconds(1.8f);
            ApplyVisualFixture(five);
            Call("SDPlayHandEffects", ScoreRules.Evaluate(ScoreHandKind.Five, five));
            Call("SDTickHandEffects", .20f);
            Call("SDOpenRules");
            Assert.That(Read<bool>("_sdFxActive"), Is.False);
            AssertBaseScales();
            AssertHomes(homes);
            foreach (Image spark in Read<Image[]>("_sdFxSparks")) Assert.That(spark.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator FormationNoticeTemporarilyReplacesTheRoleNameBelowTheDice()
        {
            var role = Read<Text>("_sdRoleLabel");
            var toast = Read<Text>("_sdToast");
            Write("_sdToastAge", 0f);
            toast.text = "スリーカインド ×1.75";
            Call("SDRefresh");
            Canvas.ForceUpdateCanvases();
            Assert.That(role.enabled, Is.False);
            Assert.That(Mathf.Abs(role.transform.position.y - toast.transform.position.y), Is.LessThan(1f));
            Assert.That(toast.transform.position.y, Is.LessThan(Die(2).position.y));
            Write("_sdToastAge", -1f);
            toast.text = "";
            Call("SDRefresh");
            Assert.That(role.enabled, Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator GestureGuidesChangeRenderedGeometryOverTime_AndEnemyDiceStayCentered()
        {
            var battle = Root("_sdBattle");
            var guide = battle.Find("GestureGuide").GetComponent<Graphic>();
            Write("_sdGuideAge", 0f);
            Call("SDTickGestureGuides", .2f);
            string first = GraphicGeometry(guide);
            var mouse = battle.Find("GestureGuide/MouseInput").GetComponent<RectTransform>();
            Assert.That(mouse.anchoredPosition.x, Is.GreaterThan(0), "the gesture guide stays beside the player role card");
            Vector2 firstMouse = mouse.anchoredPosition;
            GuideInk(guide, out Vector3 firstArrow);
            Call("SDTickGestureGuides", .6f);
            Assert.That(GraphicGeometry(guide), Is.Not.EqualTo(first), "down chevrons change actual generated vertices or colors");
            Assert.That(mouse.anchoredPosition, Is.Not.EqualTo(firstMouse), "the mouse illustration travels with the pull guide");
            GuideInk(guide, out Vector3 movedArrow);
            Assert.That(mouse.anchoredPosition.y - firstMouse.y, Is.EqualTo(movedArrow.y - firstArrow.y).Within(.01f),
                "mouse and arrows move downward together at the same speed");
            Write("_sdGuideAge", 1.2f);
            Call("SDTickGestureGuides", 0f);
            Vector2 bottomMouse = mouse.anchoredPosition;
            float visibleAlpha = mouse.GetComponent<Image>().color.a;
            Write("_sdGuideAge", 1.4f);
            Call("SDTickGestureGuides", 0f);
            Assert.That(mouse.anchoredPosition.y, Is.EqualTo(bottomMouse.y).Within(.01f), "the mouse never travels upward while visible");
            Assert.That(mouse.GetComponent<Image>().color.a, Is.LessThan(visibleAlpha), "mouse fades after its downward travel");
            Assert.That(GuideInk(guide, out _), Is.LessThan(visibleAlpha), "arrows fade with the mouse");
            Write("_sdGuideAge", 1.65f);
            Call("SDTickGestureGuides", 0f);
            Assert.That(mouse.GetComponent<Image>().color.a, Is.Zero, "mouse completely disappears before resetting");
            Assert.That(GuideInk(guide, out _), Is.Zero, "arrows completely disappear before resetting");
            Write("_sdGuideAge", 1.8f);
            Call("SDTickGestureGuides", 0f);
            Assert.That(mouse.anchoredPosition.y, Is.EqualTo(25f).Within(.01f), "reset occurs at the original position");
            Assert.That(mouse.GetComponent<Image>().color.a, Is.Zero, "reset remains invisible");
            Assert.That(GuideInk(guide, out _), Is.Zero, "arrows remain invisible at reset");
            Call("SDTickGestureGuides", .2f);
            Assert.That(mouse.anchoredPosition.y, Is.EqualTo(firstMouse.y).Within(.01f), "the next cycle repeats the same start");
            Assert.That(mouse.GetComponent<Image>().color.a, Is.GreaterThan(.9f), "mouse reappears for the next cycle");
            Assert.That(guide.raycastTarget, Is.False);
            Engine.SetHeld(new[] { true, true, true, false, false });
            Call("SDRefresh");
            Call("SDTickGestureGuides", .2f);
            Sprite right = mouse.GetComponent<Image>().sprite;
            string reroll = GraphicGeometry(guide);
            Call("SDTickGestureGuides", .4f);
            Assert.That(GraphicGeometry(guide), Is.Not.EqualTo(reroll), "right-click guidance pulses in rendered geometry");
            Assert.That(right, Is.Not.Null);
            var wheel = battle.Find("ScoreChoices/WheelMotion").GetComponent<Graphic>();
            Call("SDTickGestureGuides", .15f);
            string wheelFirst = GraphicGeometry(wheel);
            Call("SDTickGestureGuides", .3f);
            Assert.That(GraphicGeometry(wheel), Is.Not.EqualTo(wheelFirst), "wheel marks move inside their mouse anchor");
            var categories = Read<Button[]>("_sdLiveCategories");
            var selected = Read<ScoreHandKind>("_sdSelectedKind");
            Assert.That(categories[(int)selected].transform.Find("Selected").gameObject.activeInHierarchy, Is.True, "selected row shows its tick");
            var npc = Root("_sdNpcReveal");
            Vector2 center = Vector2.zero;
            for (int i = 0; i < 5; i++) center += (Vector2)battle.InverseTransformPoint(npc.Find("NpcDie" + i).position);
            center /= 5;
            Assert.That(center.x, Is.EqualTo(0).Within(.5f));
            Assert.That(center.y, Is.EqualTo(180).Within(1f));
            Assert.That(battle.Find("IntentPanel"), Is.Null, "enemy summary is printed directly on the central table");
            var roleBounds = BattleBounds(Read<Text>("_sdIntentLabel").rectTransform);
            var diceBounds = BattleBounds(npc);
            var formulaBounds = BattleBounds(Read<Text>("_sdNpcFormula").rectTransform);
            var numberBounds = BattleBounds(Read<Text>("_sdNpcScoreNumber").rectTransform);
            Assert.That(diceBounds.yMin, Is.GreaterThan(roleBounds.yMax), "enemy role clears the bottom of its actual dice");
            Assert.That(roleBounds.yMin, Is.GreaterThan(formulaBounds.yMax), "enemy formula clears the bottom of its role");
            Assert.That(formulaBounds.yMin, Is.GreaterThan(numberBounds.yMax), "enemy score remains separate below the small formula");
            Assert.That(Read<Text>("_sdNpcFormula").fontSize, Is.LessThan(Read<Text>("_sdNpcScoreNumber").fontSize));            yield return null;
        }

        [UnityTest] public IEnumerator FourOutgoingDamageTiersDrawDifferentContactGeometry_AndRecoverOriginalDice()
        {
            PullToFire();
            var round = Engine.LastRound;
            var damageProperty = typeof(ScoreRound).GetProperty("DamageToNpc");
            var incomingProperty = typeof(ScoreRound).GetProperty("DamageToPlayer");
            int originalDamage = round.DamageToNpc, originalIncoming = round.DamageToPlayer;
            int[] damages = { 20, 40, 65, 100 };
            int previousStrokeCount = 0;
            var shapes = new System.Collections.Generic.HashSet<string>();
            string weakestGeometry = null;
            Vector2[] homes = DiceHomes();
            Call("SDResetImpactPresentation");
            homes = DiceHomes();
            try
            {
                for (int tier = 0; tier < 4; tier++)
                {
                    Call("SDResetImpactPresentation");
                    damageProperty.SetValue(round, damages[tier]);
                    // The impact tier follows the larger of the two sides, so a neutral
                    // incoming value isolates each outgoing tier on its own.
                    incomingProperty.SetValue(round, 0);
                    Call("SDStartAttackMotion");
                    Assert.That(Read<int>("_sdImpactTier"), Is.EqualTo(tier));
                    // Pin the launch punch so the mid-flight position stays above the asserted
                    // line no matter what score the real committed hand happened to carry.
                    Write("_sdImpactPunch", 1);
                    Write("_sdAttackAge", .26f); Call("SDAnimateImpactPresentation");
                    Assert.That(Read<Text>("_sdImpactCounter").text, Is.Empty, "damage waits for contact");
                    Assert.That(Read<bool>("_sdImpactContact"), Is.False);
                    Assert.That(Die(2).anchoredPosition.y, Is.GreaterThan(homes[2].y + 100), "rendered dice fly toward the opponent");
                    Write("_sdAttackAge", .36f); Call("SDAnimateImpactPresentation");
                    Assert.That(Read<bool>("_sdImpactContact"), Is.True);
                    var geometry = Read<Graphic>("_sdImpactGeometry");
                    int count = (int)geometry.GetType().GetField("_count", Private).GetValue(geometry);
                    Assert.That(count, Is.GreaterThan(previousStrokeCount), "stronger impacts draw more geometry"); previousStrokeCount = count;
                    string tierGeometry = GraphicGeometry(geometry);
                    if (tier == 0) weakestGeometry = tierGeometry;
                    Assert.That(shapes.Add(tierGeometry), Is.True, "every tier produces different rendered geometry");
                    Assert.That(Read<Text>("_sdImpactCounter").text, Is.Empty, "damage is displayed at the HP bar instead of scattering");
                    Assert.That(Read<Text>("_sdImpactIncomingCounter").text, Is.Empty);
                    Call("SDTickAttackMotion", 1f);
                    AssertHomes(homes); AssertBaseScales();
                    Assert.That(Read<float>("_sdAttackAge"), Is.LessThan(0));
                    Assert.That(Read<Text>("_sdImpactCounter").text, Is.Empty);
                }

                // A light outgoing hit paired with a heavy incoming one must reach the top
                // tier and draw the strong contact shape rather than the weak 20-damage one.
                Assert.That(weakestGeometry, Is.Not.Null);
                Call("SDResetImpactPresentation");
                damageProperty.SetValue(round, damages[0]);
                incomingProperty.SetValue(round, 120);
                Call("SDStartAttackMotion");
                Assert.That(Read<int>("_sdImpactTier"), Is.EqualTo(3),
                    "20 outgoing with 120 incoming must reach the top impact tier");
                Write("_sdAttackAge", .36f); Call("SDAnimateImpactPresentation");
                Assert.That(Read<bool>("_sdImpactContact"), Is.True);
                Assert.That(GraphicGeometry(Read<Graphic>("_sdImpactGeometry")), Is.Not.EqualTo(weakestGeometry),
                    "the heavy incoming hit draws the strong contact geometry, not the weak outgoing shape");
            }
            finally
            {
                damageProperty.SetValue(round, originalDamage);
                incomingProperty.SetValue(round, originalIncoming);
                Call("SDResetImpactPresentation");
            }
            yield return null;
        }

        [UnityTest] public IEnumerator TheLaunchCocksBackThenWhipsAndTheBigScoresShakeTheTable()
        {
            PullToFire();
            var round = Engine.LastRound;
            var scoreProperty = typeof(ScoreRound).GetProperty("PlayerScore");
            var npcScoreProperty = typeof(ScoreRound).GetProperty("NpcScore");
            int originalScore = round.PlayerScore, originalNpcScore = round.NpcScore;
            try
            {
                scoreProperty.SetValue(round, 70); npcScoreProperty.SetValue(round, 60);
                Call("SDResetImpactPresentation");
                Call("SDStartAttackMotion");
                // Reading the homes here, right after the reset, so they are the standing
                // positions - before the reset they still carry the flick's pull.
                Vector2[] homes = DiceHomes();
                // The wind-up punches below the origin, which a single glide curve never does.
                Write("_sdAttackAge", .05f); Call("SDAnimateImpactPresentation");
                Assert.That(Die(2).anchoredPosition.y, Is.LessThan(homes[2].y - 6f),
                    "the launch rears back before it flies");
                Assert.That(Root("_sdBattle").anchoredPosition, Is.EqualTo(Vector2.zero),
                    "a modest clash never shakes the table");
                // The whip keeps its crest: the two hundredths before contact still cover a
                // third of the flight, so the strike does not decelerate into the enemy.
                Write("_sdAttackAge", .28f); Call("SDAnimateImpactPresentation");
                float crest = Die(2).anchoredPosition.y;
                Write("_sdAttackAge", .299f); Call("SDAnimateImpactPresentation");
                Assert.That(Die(2).anchoredPosition.y - crest, Is.GreaterThan(20f),
                    "the launch arrives at speed instead of gliding to a stop");
                // From 100 points up the table itself shakes, on the same explicit age, and
                // decays back to the untouched origin inside the presentation window.
                scoreProperty.SetValue(round, 150);
                Call("SDStartAttackMotion");
                Write("_sdAttackAge", .32f); Call("SDAnimateImpactPresentation");
                Vector2 shaken = Root("_sdBattle").anchoredPosition;
                Assert.That(shaken.sqrMagnitude, Is.GreaterThan(1f),
                    "a 150-point clash shakes the table on landing");
                Write("_sdAttackAge", .80f); Call("SDAnimateImpactPresentation");
                Assert.That(Root("_sdBattle").anchoredPosition, Is.EqualTo(Vector2.zero),
                    "the shake decays back to the untouched origin before the presentation ends");
            }
            finally
            {
                scoreProperty.SetValue(round, originalScore);
                npcScoreProperty.SetValue(round, originalNpcScore);
                Call("SDResetImpactPresentation");
            }
            yield return null;
        }

        [UnityTest] public IEnumerator TheTitleNamesItsBuild()
        {
            var version = Root("_sdTitle").Find("Version");
            Assert.That(version, Is.Not.Null, "the title stamps its build tag");
            var text = version.GetComponent<Text>();
            Assert.That(text, Is.Not.Null, "the tag is a text");
            Assert.That(text.text, Is.EqualTo("Ver.0.2"));
            // It shares the bottom chrome row with the menu and the volume bar, inside their
            // free span; nothing may park on top of it again.
            Assert.That(((RectTransform)version).anchoredPosition, Is.EqualTo(new Vector2(280f, -319f)));
            yield return null;
        }

        [UnityTest] public IEnumerator TheCentralNumeralSwellsWithTheScore()
        {
            var map = typeof(GameFlow).GetMethod("SDScoreFontFor", Private | BindingFlags.Static);
            int[] scores = { 24, 70, 110, 150 }, expected = { 60, 64, 72, 80 };
            for (int i = 0; i < scores.Length; i++)
                Assert.That((int)map.Invoke(null, new object[] { scores[i] }), Is.EqualTo(expected[i]),
                    scores[i] + " points size their figure at " + expected[i]);
            // Only the committed display scales; the live preview keeps the everyday base.
            Assert.That(Read<Text>("_sdScoreNumber").fontSize, Is.EqualTo(64),
                "the preview numeral stays at the base size");
            PullToFire();
            var round = Engine.LastRound;
            int original = round.PlayerScore;
            typeof(ScoreRound).GetProperty("PlayerScore").SetValue(round, 150);
            Call("SDRefresh");
            Assert.That(Read<Text>("_sdScoreNumber").fontSize, Is.EqualTo(80),
                "a 150-point hand fronts an 80-unit figure");
            typeof(ScoreRound).GetProperty("PlayerScore").SetValue(round, original);
            yield return null;
        }
        private static float GuideInk(Graphic graphic, out Vector3 firstVertex)
        {
            using (var helper = new VertexHelper())
            {
                graphic.GetType().GetMethod("OnPopulateMesh", Private, null,
                    new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                firstVertex = Vector3.zero;
                float alpha = 0;
                var vertex = UIVertex.simpleVert;
                for (int i = 0; i < helper.currentVertCount; i++)
                {
                    helper.PopulateUIVertex(ref vertex, i);
                    if (i == 0) firstVertex = vertex.position;
                    alpha = Mathf.Max(alpha, vertex.color.a / 255f);
                }
                return alpha;
            }
        }

        private static string GraphicGeometry(Graphic graphic)
        {
            using (var helper = new VertexHelper())
            {
                graphic.GetType().GetMethod("OnPopulateMesh", Private, null,
                    new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                var mesh = new Mesh(); helper.FillMesh(mesh);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), graphic.name + " must actually draw");
                var text = new System.Text.StringBuilder();
                foreach (Vector3 vertex in mesh.vertices) text.Append(vertex.ToString("F3"));
                foreach (Color32 color in mesh.colors32) text.Append(color.ToString());
                Object.Destroy(mesh);
                return text.ToString();
            }
        }
        private Rect BattleBounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = Root("_sdBattle").InverseTransformPoint(corners[0]);
            Vector2 max = Root("_sdBattle").InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private void TableClick(string diePath = null)
        {
            var target = diePath == null ? Root("_sdBattle") : (RectTransform)Root("_sdBattle").Find(diePath);
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, target.position),
                pointerPressRaycast = new RaycastResult { gameObject = target.gameObject },
                pointerCurrentRaycast = new RaycastResult { gameObject = target.gameObject } };
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        private void PullToFire(bool practice = false)
        {
            var root = Root(practice ? "_sdPractice" : "_sdBattle");
            var die = (RectTransform)root.Find(practice ? "PracticeDie0" : "Die0");
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, die.position);
            Vector2 end = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(die.anchoredPosition + Vector2.down * 120));
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pressPosition = start, position = start };
            ExecuteEvents.Execute(die.gameObject, data, ExecuteEvents.beginDragHandler);
            data.position = end;
            ExecuteEvents.Execute(die.gameObject, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(die.gameObject, data, ExecuteEvents.endDragHandler);
        }

        private T Read<T>(string name) => (T)typeof(GameFlow).GetField(name, Private).GetValue(_flow);
        private void Write(string name, object value) => typeof(GameFlow).GetField(name, Private).SetValue(_flow, value);
        private RectTransform Root(string name) => Read<RectTransform>(name);
        private void Call(string method, params object[] args) => typeof(GameFlow).GetMethod(method, Private).Invoke(_flow, args);
        private RectTransform Die(int index) => (RectTransform)Root("_sdBattle").Find("Die" + index);
        private void Press(string field, string path)
        {
            Button button = Root(field).Find(path).GetComponent<Button>();
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, path);
            button.onClick.Invoke();
        }
        private void PressLive(ScoreHandKind kind) => Press("_sdBattle", "ScoreChoices/LiveCategory" + (int)kind);
        private void AssertWheelSelection(ScoreHandKind kind)
        {
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.EqualTo(kind));
            var forecast = Engine.Forecast(kind);
            Assert.That(Read<Text>("_sdRoleLabel").text, Is.EqualTo(ScoreRules.Name(kind)));
            Assert.That(Read<Text>("_sdScoreNumber").text, Is.EqualTo(forecast.Score.ToString()));
            AssertForecastNumbers(forecast);
            var markers = Read<Image[]>("_sdLiveMarkers");
            for (int i = 0; i < markers.Length; i++) Assert.That(markers[i].gameObject.activeSelf, Is.EqualTo(i == (int)kind));
        }
        private void AssertForecastNumbers(ScoreForecast forecast)
        {
            Assert.That(ScoreDuelHealthLine.Damage(Read<Text>("_sdNpcHpLabel")), Is.EqualTo(forecast.DamageToNpc),
                "the enemy bar names the drop the chosen role lands on it");
            Assert.That(ScoreDuelHealthLine.Damage(Read<Text>("_sdPlayerHpLabel")), Is.EqualTo(forecast.DamageToPlayer),
                "the player bar names the drop the chosen role takes");
            Assert.That(Read<Text>("_sdEffectLabel").text, Is.Empty);
            var kind = Read<ScoreHandKind>("_sdSelectedKind");
            var offer = ScoreRules.Evaluate(kind, Engine.PlayerDice);
            float factor = offer.IsFormed ? ScoreRules.MultiplierQuarters(kind) / 4f : 1f;
            Assert.That(Read<Text>("_sdBreakdown").text, Is.EqualTo("(20 + " + (offer.BaseScore - 20) + ") ×" + factor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(Read<Text>("_sdNpcScoreNumber").text, Is.EqualTo(Engine.Intent.Score.ToString()));
        }
        private Vector2 ScreenAtDie(int index) => RectTransformUtility.WorldToScreenPoint(null, Die(index).TransformPoint(Vector3.zero));
        private Vector2 ScreenAtLocal(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, Root("_sdBattle").TransformPoint(local));
        private PointerEventData BeginDrag(int index)
        {
            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pressPosition = ScreenAtDie(index), position = ScreenAtDie(index), pointerDrag = Die(index).gameObject
            };
            ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.beginDragHandler);
            return data;
        }
        private static void Drag(PointerEventData data, Vector2 point)
        {
            data.position = point;
            ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.dragHandler);
        }
        private static void EndDrag(PointerEventData data, Vector2 point)
        {
            data.position = point;
            ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.endDragHandler);
        }
        private RectTransform[] DiceRects()
        {
            var dice = new RectTransform[5];
            for (int i = 0; i < 5; i++) dice[i] = Die(i);
            return dice;
        }
        private Vector2[] DiceHomes()
        {
            var homes = new Vector2[5];
            for (int i = 0; i < 5; i++) homes[i] = Die(i).anchoredPosition;
            return homes;
        }
        private void AssertHomes(Vector2[] homes)
        {
            for (int i = 0; i < 5; i++) Assert.That(Vector2.Distance(Die(i).anchoredPosition, homes[i]), Is.LessThan(.001f), "Die" + i);
        }
        private void AssertBaseScales()
        {
            for (int i = 0; i < 5; i++) Assert.That(Vector3.Distance(Die(i).localScale, Vector3.one), Is.LessThan(.001f), "Die" + i);
        }
        private void AssertRenderedGaps(float minimum)
        {
            var left = new Vector3[4];
            var right = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                Die(i).GetWorldCorners(left);
                Die(i + 1).GetWorldCorners(right);
                float gap = Root("_sdBattle").InverseTransformPoint(right[0]).x - Root("_sdBattle").InverseTransformPoint(left[2]).x;
                Assert.That(gap, Is.GreaterThanOrEqualTo(minimum), "rendered faces " + i + " and " + (i + 1) + " must stay separate");
            }
        }
        private void AssertEffectCentersMatchDice()
        {
            var centers = Read<Vector2[]>("_sdFxCenters");
            for (int i = 0; i < 5; i++)
            {
                Vector2 actual = Root("_sdFxRoot").InverseTransformPoint(Die(i).TransformPoint(Vector3.zero));
                Assert.That(Vector2.Distance(centers[i], actual), Is.LessThan(.001f), "effect follows the rendered Die" + i);
            }
        }
        private static float RenderedHeight(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Vector3.Distance(corners[0], corners[1]);
        }
        private void ApplyVisualFixture(int[] faces)
        {
            // VFX-only fixture: change the five views, never the production RNG, the engine
            // hand, its scorecard, or its round receipt. Explicit faces make masks verifiable.
            Call("SDResetHandEffects");
            IList dice = Read<IList>("_sdDice");
            for (int i = 0; i < 5; i++)
            {
                object die = dice[i];
                die.GetType().GetMethod("SetHidden").Invoke(die, null);
                die.GetType().GetMethod("SetValue").Invoke(die, new object[] { faces[i] });
            }
        }
    }
}
