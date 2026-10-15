using System.Collections;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        // Opt-in actual-player validation. It drives production buttons and compares every
        // resolved turn with an independent core instance; ordinary startup never runs this.
        private IEnumerator ScoreDuelPlayerSmoke()
        {
            yield return new WaitForSecondsRealtime(2f);
            SmokeCheck(_sdTitle.gameObject.activeInHierarchy, "production starts on the simple dice-duel title");
            SmokeCheck(Application.productName == "DiceYahzee" &&
                _sdTitle.Find("Title").GetComponent<UnityEngine.UI.Text>().text.Contains("DiceYahzee"),
                "the player product name and visible title both use DiceYahzee");
            SmokeCheck(_titleRoot.childCount == 0, "legacy mana, guard and blessing screens are not constructed");
            SmokeCapture("title");
            var titleText = _sdTitle.Find("Title").GetComponent<UnityEngine.UI.Text>();
            SmokeCheck(titleText.cachedTextGenerator.vertexCount > 4, "title generates visible lettering geometry");
            Click(_sdTitle, "Rules");
            yield return new WaitForEndOfFrame();
            SmokeCapture("rules");
            Click(_sdRules, "CloseRules");
            Click(_sdTitle, "Practice");
            SmokeCheck(_sdEngine == null, "fixed practice is separate from a real match");
            SDTickGestureGuides(0);
            SmokeCapture("practice-start");
            Click(_sdPractice, "PracticeDie0");
            Click(_sdPractice, "PracticeDie1");
            SmokeCapture("practice-hold");
            // Modal closing consumes its frame, just as production input does.
            yield return null;
            SDHandleRightClick();
            yield return new WaitForSecondsRealtime(.78f);
            SmokeCheck(_sdPracticeFeedback.text.Contains(ScoreRules.Evaluate(ScoreHandKind.Three, new[] { 1, 1, 1, 5, 6 }).Score.ToString()), "fixed practice displays the independently evaluated triple score");
            SmokeCapture("practice-formed");
            SDSmokeFlick(0, 120, true);
            SmokeCapture("practice-used");
            Click(_sdPractice, "ClosePractice");

            const int seed = 1042;
            SeedOverride = () => seed;
            var proof = new ScoreDuel(seed, _sdOpponent);
            proof.Roll();
            Click(_sdTitle, "Start");
            SmokeCheck(_sdRolling && _sdRoleLabel.text == "" && _sdScoreNumber.text == "—", "first throw hides the settled hand");
            SDCommit();
            SmokeCheck(_sdEngine.History.Count == 0, "commit is blocked during the throw");
            yield return new WaitForSecondsRealtime(.72f);
            SmokeCheck(!_sdRolling && _sdEngine.RerollsLeft == 2, "first throw lands without an extra confirmation");
            SmokeCheck(_sdNpcReveal.gameObject.activeInHierarchy, "NPC actual dice are visible before choosing a result");
            bool visible = true;
            for (int i = 0; i < 5; i++) visible &= _sdNpcDice[i].Value == _sdEngine.PlannedNpcDice[i];
            SmokeCheck(visible, "the announced NPC hand matches all five displayed real dice");
            foreach (var button in _sdLiveCategories) SmokeCheck(button.gameObject.activeInHierarchy && button.interactable, "each unused role is always visible and selectable");
            SDAssertSameDice(proof);
            SDSmokeCheckRightScoreChoices();
            SmokeCapture("full-width-table-overlay-cards-centered-health");

            var originalKind = _sdSelectedKind;
            _sdLiveCategories[(int)ScoreHandKind.Pair].onClick.Invoke();
            yield return null;
            SDHandleWheel(-.4f);
            SmokeCheck(_sdSelectedKind == ScoreHandKind.Pair, "a partial mouse-wheel notch keeps the current role");
            SDHandleWheel(-.6f);
            SmokeCheck(_sdSelectedKind == ScoreHandKind.TwoPair && _sdLiveMarkers[1].gameObject.activeSelf &&
                _sdRoleLabel.text.Contains(ScoreRules.Name(ScoreHandKind.TwoPair)),
                "accumulated downward mouse-wheel input selects the next role and its visible marker");
            yield return null;
            SDHandleWheel(1f);
            SmokeCheck(_sdSelectedKind == ScoreHandKind.Pair, "upward mouse-wheel input selects the previous role");
            yield return null;
            SDHandleWheel(1f);
            SmokeCheck(_sdSelectedKind == ScoreHandKind.Chance, "mouse-wheel input wraps from the first role to the final role");
            yield return null;
            SDHandleWheel(-1f);
            SmokeCheck(_sdSelectedKind == ScoreHandKind.Pair, "mouse-wheel input wraps from the final role to the first role");
            SDSelectCategory((int)originalKind);
            SmokeCapture("battle-formed");

            SDResetHandEffects();
            bool fullInput = true;
            var hits = new System.Collections.Generic.List<RaycastResult>();
            for (int i = 0; i < 5; i++)
                foreach (float x in new[] { -50f, 0f, 50f })
                    foreach (float y in new[] { -50f, 0f, 50f })
                    {
                        var rect = (RectTransform)_sdBattle.Find("Die" + i);
                        var pointer = new PointerEventData(EventSystem.current) {
                            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector2(x, y))) };
                        hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
                        fullInput &= hits.Count > 0 && ExecuteEvents.GetEventHandler<IBeginDragHandler>(hits[0].gameObject) == rect.gameObject;
                    }
            SmokeCheck(fullInput, "all five actual die faces and corners receive drag input through the real GraphicRaycaster");
            SmokeCheck(_sdBattle.Find("Reroll") == null && _sdBattle.Find("Commit") == null,
                "reroll and use-result buttons are removed from the production battle");
            SDTickGestureGuides(.2f); SmokeCapture("gesture-down-guide-a");
            SDTickGestureGuides(.6f); SmokeCapture("gesture-down-guide-b");
            var pullStart = RectTransformUtility.WorldToScreenPoint(null, _sdBattle.TransformPoint(new Vector2(-335, -150)));
            var pullEnd = RectTransformUtility.WorldToScreenPoint(null, _sdBattle.TransformPoint(new Vector2(-335, -260)));
            var pull = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                pressPosition = pullStart, position = pullStart,
                pointerPressRaycast = new RaycastResult { gameObject = _sdBattle.Find("TablePointerSurface").gameObject } };
            ExecuteEvents.Execute(_sdBattle.gameObject, pull, ExecuteEvents.beginDragHandler);
            pull.position = pullEnd; ExecuteEvents.Execute(_sdBattle.gameObject, pull, ExecuteEvents.dragHandler);
            SmokeCheck(_sdDragging && _sdDragAiming && _sdEngine.History.Count == 0 &&
                ((RectTransform)_sdBattle.Find("Die0")).anchoredPosition.y < -20,
                "a background down-pull sinks the dice without resolving before release");
            SDRefreshDieLabelsVisibility();
            SDTickGestureGuides(0); SmokeCapture("gesture-background-charged");
            SDCancelPointer(); SDRefresh();
            _sdEngine.SetHeld(new[] { true, true, true, false, false }); SDRefresh(); SDTickGestureGuides(0);
            SmokeCheck(_sdInstruction.text == "" && _sdGestureGraphic.gameObject.activeInHierarchy &&
                _sdBattle.Find("RerollSymbol") == null, "three held dice use the mouse guide without a separate reroll icon");
            SmokeCapture("gesture-three-held-right-click");
            _sdEngine.SetHeld(new bool[5]); SDRefresh();

            int[] original = (int[])_sdEngine.PlayerDice.Clone();
            var menuKind = _sdSelectedKind;
            SDOpenMenu();
            SDHandleShortcut(KeyCode.Space); SDOnDieClicked(0); SDCommit();
            SDHandleWheel(-1f);
            SmokeCheck(_sdEngine.History.Count == 0 && _sdEngine.RerollsLeft == 2 && SDArrayEquals(original, _sdEngine.PlayerDice),
                "menu blocks keyboard, hold, reroll and commit");
            SmokeCheck(_sdSelectedKind == menuKind, "the menu also blocks mouse-wheel role selection");
            SmokeCapture("menu");
            Click(_sdMenu, "Credits");
            SDCommit();
            SmokeCheck(_sdCredits.gameObject.activeSelf && _sdEngine.History.Count == 0, "credits remain a blocking modal");
            SmokeCapture("credits");
            Click(_sdCredits, "CloseCredits");

            // Check the actual visible pip objects, rather than only the RNG values.
            Click(_sdBattle, "Die0");
            proof.ToggleHeld(0);
            yield return new WaitForSecondsRealtime(.25f);
            int pipMask = SDVisiblePips(0);
            Vector3 scale = _sdBattle.Find("Die0").localScale;
            yield return null;
            proof.Reroll();
            SDHandleRightClick();
            SmokeCheck(!_sdDice[0].IsRolling && _sdDice[1].IsRolling, "only unheld dice animate on a reroll");
            yield return new WaitForSecondsRealtime(.12f);
            SmokeCheck(SDVisiblePips(0) == pipMask && Vector3.Distance(_sdBattle.Find("Die0").localScale, scale) < .001f,
                "the held die keeps its rendered face and scale while other dice roll");
            SmokeCapture("reroll-moving");
            yield return new WaitForSecondsRealtime(.65f);
            SDAssertSameDice(proof);
            yield return null;
            proof.Reroll();
            SDHandleRightClick();
            yield return new WaitForSecondsRealtime(.78f);
            SDAssertSameDice(proof);
            SmokeCheck(_sdEngine.RerollsLeft == 0 && _sdBattle.Find("Reroll") == null && _sdBattle.Find("Commit") == null,
                "after two rerolls the turn offers result use only");
            SmokeCapture("last-roll");

            int rounds = 0;
            while (_sdEngine.Outcome == null && rounds++ < ScoreDuel.SafetyRoundBound)
            {
                if (_sdEngine.Round > 1)
                {
                    // Pattern policy edits the same five physical hold buttons as the player.
                    for (int roll = 0; roll < 2 && _sdEngine.RerollsLeft > 0; roll++)
                    {
                        var available = new bool[9];
                        for (int kind = 0; kind < 9; kind++) available[kind] = !_sdEngine.PlayerUsed[kind];
                        bool[] held = ScoreRules.SuggestHeld(_sdEngine.PlayerDice, available, _sdEngine.RerollsLeft,
                            0, _sdEngine.Round, _sdOpponent);
                        bool any = false;
                        for (int i = 0; i < 5; i++)
                        {
                            if (held[i] != _sdEngine.Held[i]) Click(_sdBattle, "Die" + i);
                            any |= !held[i];
                        }
                        proof.SetHeld(held);
                        if (!any) break;
                        // A prior wheel decision may have consumed this frame. Physical
                        // player actions occur on a later frame; don't bypass that guard.
                        yield return null;
                        proof.Reroll();
                        SDHandleRightClick();
                        yield return new WaitForSecondsRealtime(.78f);
                        SDAssertSameDice(proof);
                    }
                }
                var forecast = _sdEngine.Forecast(_sdSelectedKind);
                SmokeCheck(_sdEngine.NpcDice == null && _sdNpcReveal.gameObject.activeInHierarchy,
                    "round " + _sdEngine.Round + " shows the fixed real NPC dice before result use");
                var receipt = proof.Commit(_sdSelectedKind);
                if (_sdEngine.Round == 1)
                {
                    // Exercise Unity's production pointer callbacks, including a release. These
                    // are EventSystem events, rather than a claim of physical mouse hardware.
                    SDSmokeFlick(0, 120);
                }
                else SDSmokeFlick(0, 120);
                bool npcFacesMatch = _sdNpcReveal.gameObject.activeInHierarchy;
                for (int i = 0; i < 5; i++) npcFacesMatch &= _sdNpcDice[i].Value == receipt.NpcDice[i];
                SmokeCheck(npcFacesMatch, "round " + _sdEngine.Round + " visibly reveals the NPC's actual dice after result use");
                SmokeCheck(_sdEngine.LastRound.PlayerScore == receipt.PlayerScore &&
                    _sdEngine.LastRound.NpcScore == receipt.NpcScore &&
                    _sdEngine.PlayerHp == proof.PlayerHp && _sdEngine.NpcHp == proof.NpcHp &&
                    SDArrayEquals(_sdEngine.NpcDice, receipt.NpcDice), "round " + _sdEngine.Round + " matches the independent core receipt");
                SmokeCheck(_sdNpcDamage.text == "−" + forecast.DamageToNpc && _sdPlayerDamage.text == "−" + forecast.DamageToPlayer,
                    "round " + _sdEngine.Round + " renders both independently forecast damage values");
                SmokeCheck(_sdEngine.LastRound.DamageToPlayer == forecast.DamageToPlayer &&
                    _sdEngine.LastRound.DamageToNpc == forecast.DamageToNpc,
                    "round " + _sdEngine.Round + " uses exactly the displayed clash damage");
                yield return new WaitForSecondsRealtime(.95f);
                SDSmokeCheckRightScoreChoices();
                SmokeCapture("round-" + _sdEngine.Round + "-result");
                if (_sdEngine.Round == 1) SmokeCapture("full-width-table-overlay-cards-used-enemy-role");
                if (_sdEngine.Outcome != null) break;
                proof.NextRound(); proof.Roll();
                SDSmokeTableClick();
                SmokeCheck(_sdRolling, "round " + _sdEngine.Round + " begins with the same throw animation");
                yield return new WaitForSecondsRealtime(.72f);
                SDAssertSameDice(proof);
                if (_sdEngine.Round == 2)
                {
                    var nextKind = _sdSelectedKind;
                    int used = (int)_sdEngine.LastRound.PlayerKind;
                    int beforeUsed = (used + ScoreRules.HandCount - 1) % ScoreRules.HandCount;
                    int afterUsed = (used + 1) % ScoreRules.HandCount;
                    _sdLiveCategories[beforeUsed].onClick.Invoke();
                    yield return null;
                    SDHandleWheel(-1f);
                    SmokeCheck((int)_sdSelectedKind == afterUsed && !_sdLiveCategories[used].interactable,
                        "mouse-wheel role selection skips a used category while its row remains visible");
                    SDSelectCategory((int)nextKind);
                }
            }
            SmokeCheck(_sdEngine.Outcome != null && (_sdEngine.PlayerHp == 0 || _sdEngine.NpcHp == 0), "the match finishes by a knockout");
            SDSmokeTableClick();
            SmokeCheck(_sdResult.gameObject.activeSelf, "the match leads to the result screen");
            SmokeCapture("match-result");
            Click(_sdResult, "Scorecard");
            SmokeCapture("used-scorecard");
            Click(_sdSheet, "CloseSheet");
            Click(_sdResult, "Rematch");
            yield return new WaitForSecondsRealtime(.72f);
            SmokeCheck(_sdEngine.Round == 1 && _sdEngine.History.Count == 0 && _sdEngine.PlayerHp == ScoreDuel.StartingHp,
                "rematch resets health, history and score categories");
            foreach (bool used in _sdEngine.PlayerUsed) SmokeCheck(!used, "rematch starts with an unused category");
            SmokeCapture("rematch");
            int[] savedDice = (int[])_sdEngine.PlayerDice.Clone();
            Click(_sdBattle, "Die0");
            bool[] savedHeld = (bool[])_sdEngine.Held.Clone();
            var savedForecast = _sdEngine.Forecast(_sdSelectedKind, true);
            int oldPlayerHp = _sdEngine.PlayerHp, oldNpcHp = _sdEngine.NpcHp;
            yield return null;
            _sdSaveButton.onClick.Invoke();
            SmokeCheck(_sdSaveArmed && !_sdEngine.SavedHandUsed && _sdEngine.History.Count == 0 &&
                _sdEngine.PlayerHp == oldPlayerHp && _sdEngine.NpcHp == oldNpcHp && SDArrayEquals(savedDice, _sdEngine.PlayerDice),
                "carry mode arms without resolving, spending its use, or changing the current hand");
            SmokeCheck(_sdNpcDamage.text == "−" + savedForecast.DamageToNpc && _sdPlayerDamage.text == "−" + savedForecast.DamageToPlayer,
                "armed carry previews the exact unguarded damage");
            SmokeCapture("carry-armed");
            yield return null;
            _sdSaveButton.onClick.Invoke();
            var normalForecast = _sdEngine.Forecast(_sdSelectedKind);
            SmokeCheck(!_sdSaveArmed && _sdEngine.History.Count == 0 &&
                _sdNpcDamage.text == "−" + normalForecast.DamageToNpc && _sdPlayerDamage.text == "−" + normalForecast.DamageToPlayer,
                "disarming carry restores the ordinary clash preview without resolving");
            SmokeCapture("carry-disarmed");
            yield return null; _sdSaveButton.onClick.Invoke();
            yield return null; SDSmokeFlick(1, 24);
            bool canceledHoldsMatch = true;
            for (int i = 0; i < 5; i++) canceledHoldsMatch &= _sdEngine.Held[i] == savedHeld[i];
            SmokeCheck(_sdSaveArmed && _sdEngine.History.Count == 0 && SDArrayEquals(savedDice, _sdEngine.PlayerDice) && canceledHoldsMatch,
                "a canceled short carry pull preserves the current faces, holds and armed choice");
            SmokeCapture("carry-canceled-pull");
            yield return null; SDSmokeFlick(0, 120);
            SmokeCheck(_sdEngine.LastRound.IsSaved && _sdEngine.SavedHandUsed &&
                _sdEngine.PlayerHp == Mathf.Max(0, oldPlayerHp - savedForecast.DamageToPlayer) &&
                _sdEngine.NpcHp == Mathf.Max(0, oldNpcHp - savedForecast.DamageToNpc), "the full carry pull exchanges the exact displayed unguarded attacks once");
            yield return new WaitForSecondsRealtime(.95f);
            SmokeCapture("saved-exchange");
            SDSmokeTableClick();
            yield return new WaitForSecondsRealtime(.95f);
            bool holdsMatch = true;
            for (int i = 0; i < 5; i++) holdsMatch &= _sdEngine.Held[i] == savedHeld[i];
            SmokeCheck(SDArrayEquals(savedDice, _sdEngine.PlayerDice) && holdsMatch && _sdEngine.RerollsLeft == 2 && !_sdRolling,
                "the following turn restores the saved real faces and holds with two fresh rerolls");
            SmokeCheck(!_sdSaveButton.interactable, "saving is unavailable after its one use");
            SmokeCapture("saved-return");

            // Opt-in visual fixtures retain a real receipt, then replace only its displayed
            // damage for four deterministic presentation samples. The receipt is restored.
            var impactReceipt = _sdEngine.LastRound;
            int outgoingDamage = impactReceipt.DamageToNpc, incomingDamage = impactReceipt.DamageToPlayer;
            bool impactWasCommitted = _sdCommitted; _sdCommitted = true;
            int[] impactDamages = { 20, 40, 65, 100 };
            for (int tier = 0; tier < 4; tier++)
            {
                SDResetImpactPresentation();
                typeof(ScoreRound).GetProperty("DamageToNpc").SetValue(impactReceipt, impactDamages[tier]); typeof(ScoreRound).GetProperty("DamageToPlayer").SetValue(impactReceipt, 18);
                SDRefresh(); SDStartAttackMotion(); SDTickGestureGuides(0);
                SmokeCheck(_sdImpactTier == tier, "outgoing damage selects visual impact tier " + tier);
                _sdToast.text = "固定ダメージの演出検証 · " + impactDamages[tier]; _sdToastAge = -1;
                foreach (float age in new[] { .24f, .36f, .62f })
                {
                    _sdAttackAge = age; SDAnimateImpactPresentation(); SDRefreshDieLabelsVisibility();
                    SmokeCheck(age < .30f ? !_sdImpactContact && _sdImpactCounter.text == "" : _sdImpactContact && _sdImpactCounter.text != "",
                        "tier " + tier + " shows contact-gated damage at fixed age " + age);
                    SmokeCapture("damage-tier-" + tier + (age < .30f ? "-precontact" : age < .4f ? "-contact" : "-recovery"));
                }
                SDResetImpactPresentation();
            }
            typeof(ScoreRound).GetProperty("DamageToNpc").SetValue(impactReceipt, outgoingDamage); typeof(ScoreRound).GetProperty("DamageToPlayer").SetValue(impactReceipt, incomingDamage);
            _sdCommitted = impactWasCommitted; SDRefresh();
            // Isolated, clearly labelled visual fixtures make rare hands reproducible. They
            // run only under the opt-in smoke flag and never alter ordinary game startup.
            int[][] fixtures = { new[] { 1, 1, 3, 5, 6 }, new[] { 5, 2, 4, 1, 3 },
                new[] { 2, 5, 2, 5, 2 }, new[] { 1, 1, 1, 1, 1 } };
            ScoreHandKind[] kinds = { ScoreHandKind.Pair, ScoreHandKind.LargeStraight, ScoreHandKind.FullHouse, ScoreHandKind.Five };
            string[] names = { "effect-pair", "effect-straight", "effect-fullhouse", "effect-yahtzee" };
            for (int fixture = 0; fixture < fixtures.Length; fixture++)
            {
                SDResetHandEffects();
                for (int i = 0; i < 5; i++) { _sdEngine.PlayerDice[i] = fixtures[fixture][i]; _sdDice[i].SetValue(fixtures[fixture][i]); }
                _sdSelectedKind = kinds[fixture];
                SDRefresh();
                _sdToast.text = "固定出目の演出検証 · " + ScoreRules.Name(kinds[fixture]);
                _sdToastAge = -1;
                SDPlayHandEffects(ScoreRules.Evaluate(kinds[fixture], fixtures[fixture]));
                // Sample the production animation synchronously at a known age. Waiting for
                // a player frame can overshoot Pair's .36 s lifetime on a slow render device.
                // Assertions and camera capture share this state; normal SDTick still uses dt.
                float sampleAge = fixture == 1 ? .44f : .22f;
                SDTickHandEffects(sampleAge);
                SDRefreshDieLabelsVisibility();
                SmokeCheck(_sdFxActive && _sdFxRoot.gameObject.activeInHierarchy &&
                    _sdFxLastKind == kinds[fixture] && Mathf.Approximately(_sdFxAge, sampleAge),
                    "the " + kinds[fixture] + " fixture visibly plays its specific formation effect at a fixed sampled age");
                if (fixture >= 2)
                {
                    SmokeCheck(!_sdGestureGraphic.gameObject.activeInHierarchy,
                        "the enlarged " + kinds[fixture] + " effect hides input guidance immediately");
                    bool labelsHidden = true;
                    foreach (var label in _sdDieLabels) labelsHidden &= !label.gameObject.activeSelf;
                    SmokeCheck(labelsHidden, "the enlarged " + kinds[fixture] + " fixture hides hold labels behind its faces");
                    float smallest = float.MaxValue;
                    var previous = new Vector3[4]; var current = new Vector3[4];
                    ((RectTransform)_sdBattle.Find("Die0")).GetWorldCorners(previous);
                    for (int i = 1; i < 5; i++)
                    {
                        ((RectTransform)_sdBattle.Find("Die" + i)).GetWorldCorners(current);
                        smallest = Mathf.Min(smallest, current[0].x - previous[2].x);
                        System.Array.Copy(current, previous, 4);
                    }
                    SmokeCheck(smallest >= (fixture == 3 ? 9 : 3), "the enlarged " + kinds[fixture] + " faces keep a readable gap");
                }
                SmokeCapture(names[fixture]);
                yield return new WaitForSecondsRealtime(1.3f);
            }
            SDStartMatch();
            yield return new WaitForSecondsRealtime(.8f);
            foreach (var size in new[] { new Vector2Int(960, 720), new Vector2Int(1600, 900) })
            {
                UnityEngine.Screen.SetResolution(size.x, size.y, false);
                yield return new WaitForSecondsRealtime(.3f);
                SDSmokeCheckRightScoreChoices();
                SmokeCapture(size.x == 960 ? "battle-4x3" : "battle-wide");
            }
            UnityEngine.Screen.SetResolution(1280, 720, false);
            SeedOverride = null;
        }

        private void SDAssertSameDice(ScoreDuel proof)
        {
            SmokeCheck(SDArrayEquals(_sdEngine.PlayerDice, proof.PlayerDice) &&
                _sdEngine.Intent.Kind == proof.Intent.Kind && _sdEngine.Intent.Score == proof.Intent.Score,
                "round " + _sdEngine.Round + " preserves the actual dice and fixed NPC intent");
            var offer = ScoreRules.Evaluate(_sdSelectedKind, proof.PlayerDice);
            float factor = offer.IsFormed ? ScoreRules.MultiplierQuarters(_sdSelectedKind) / 4f : 1f;
            string formula = offer.BaseScore + " ×" + factor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var forecast = proof.Forecast(_sdSelectedKind);
            SmokeCheck(_sdScoreNumber.text == offer.Score.ToString() && _sdBreakdown.text == formula &&
                _sdNpcScoreNumber.text == proof.Intent.Score.ToString() &&
                _sdNpcDamage.text == "−" + forecast.DamageToNpc && _sdPlayerDamage.text == "−" + forecast.DamageToPlayer,
                "round " + _sdEngine.Round + " visibly matches the independent score formula and both attack forecasts");
            SmokeCheck(_sdPlayerBar.color != _sdNpcBar.color && _sdSaveSymbol.sprite != null,
                "both health sides have distinct colors and the central carry action keeps imported artwork");
            bool proseFree = !_sdInstruction.gameObject.activeInHierarchy && !_sdEffectLabel.gameObject.activeInHierarchy;
            foreach (var text in _sdBattle.GetComponentsInChildren<UnityEngine.UI.Text>())
                foreach (string prose in new[] { "右クリック", "下へ引", "離すと", "ホイール", "残したダイス" })
                    proseFree &= !text.text.Contains(prose);
            SmokeCheck(proseFree, "battle gesture guidance is visual without active explanatory prose");
        }
        private void SDSmokeCheckRightScoreChoices()
        {
            Canvas.ForceUpdateCanvases();
            var right = (RectTransform)_sdBattle.Find("ScoreChoices");
            var left = (RectTransform)_sdBattle.Find("NpcScorecard");
            SmokeCheck(left != null && Mathf.Abs(left.anchoredPosition.x + right.anchoredPosition.x) < .1f && left.rect.size == right.rect.size,
                "symmetric enemy and player role rails flank the central play area");
            SmokeCheck(left.rect.width <= 190 && left.rect.height <= 400 &&
                _sdSaveButton.transform.parent == _sdBattle &&
                Mathf.Abs(((RectTransform)_sdSaveButton.transform).anchoredPosition.x) < .1f,
                "compact role rails frame a central carry action");
            var rim = (RectTransform)_sdBattle.Find("TableRim");
            var mat = (RectTransform)_sdBattle.Find("TableMat");
            Rect table = SDSmokeTableBounds(mat);
            SmokeCheck(rim.rect.width >= Mathf.Min(_sdBattle.rect.width, 1280f) * .9f && Mathf.Abs(rim.anchoredPosition.x) < .1f,
                "the tabletop bounds preserve a wide central play corridor");
            foreach (var card in new[] { left, right })
            {
                Rect bounds = SDSmokeTableBounds(card);
                float overlap = Mathf.Min(bounds.xMax, table.xMax) - Mathf.Max(bounds.xMin, table.xMin);
                float alpha = card.GetComponent<UnityEngine.UI.Image>().color.a;
                Text heading = card.Find("Heading").GetComponent<Text>();
                Text firstRole = card.Find(card == left ? "NpcCategory0/Name" : "LiveCategory0/Name").GetComponent<Text>();
                SmokeCheck(overlap > card.rect.width * .9f && mat.GetSiblingIndex() < card.GetSiblingIndex() && alpha < .01f &&
                    heading.fontSize >= 20 && firstRole.fontSize >= 16,
                    card.name + " is a legible type rail without a backing window");
            }
            foreach (var label in new[] { _sdNpcHpLabel, _sdPlayerHpLabel })
            {
                Rect bar = SDSmokeTableBounds((RectTransform)_sdBattle.Find(label == _sdNpcHpLabel ? "NpcBar" : "PlayerBar"));
                SmokeCheck(Mathf.Abs(SDSmokeTableBounds(label.rectTransform).center.x) < .1f &&
                    Mathf.Abs(SDSmokeTableBounds(label.rectTransform).center.y - bar.center.y) < .1f &&
                    bar.height >= 16 &&
                    label.alignment == TextAnchor.MiddleCenter && label.text.Contains("/") &&
                    label.text.Length > (ScoreDuel.StartingHp + " / " + ScoreDuel.StartingHp).Length,
                    label.name + " is centered inside the thicker health bar");
            }
            Rect playerBar = SDSmokeTableBounds((RectTransform)_sdBattle.Find("PlayerBar"));
            SmokeCheck(SDSmokeTableBounds((RectTransform)_sdBattle.Find("RollPip0")).yMax < playerBar.yMin &&
                SDSmokeTableBounds((RectTransform)_sdBattle.Find("RollPip1")).yMax < playerBar.yMin,
                "two remaining-reroll dots sit below the player health bar");
            Rect leftBounds = SDSmokeTableBounds(left), rightBounds = SDSmokeTableBounds(right);
            foreach (string path in new[] { "Die0", "Die1", "Die2", "Die3", "Die4", "NpcReveal", "Intent", "NpcFormula", "NpcScore", "CurrentRole" })
            {
                Rect bounds = SDSmokeTableBounds((RectTransform)_sdBattle.Find(path));
                SmokeCheck(bounds.xMin > leftBounds.xMax && bounds.xMax < rightBounds.xMin,
                    path + " clears both role cards at " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height);
            }
            SmokeCheck(_sdBattle.Find("IntentPanel") == null, "enemy score information is printed directly on the table");
            Rect role = SDSmokeTableBounds(_sdIntentLabel.rectTransform), dice = SDSmokeTableBounds(_sdNpcReveal),
                formula = SDSmokeTableBounds(_sdNpcFormula.rectTransform), number = SDSmokeTableBounds(_sdNpcScoreNumber.rectTransform);
            SmokeCheck(dice.yMin > role.yMax && role.yMin > formula.yMax && formula.yMin > number.yMax,
                "enemy dice, role, small formula and number stay vertically separate in that order");
            SDSmokeCheckNpcUsedRoles();
        }
        private Rect SDSmokeTableBounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = _sdBattle.InverseTransformPoint(corners[0]), max = _sdBattle.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private void SDSmokeCheckNpcUsedRoles()
        {
            var left = _sdBattle.Find("NpcScorecard");
            for (int i = 0; i < ScoreRules.HandCount; i++)
            {
                var row = left.Find("NpcCategory" + i);
                bool used = _sdEngine.NpcUsed[i]; int actualScore = 0;
                foreach (ScoreRound receipt in _sdEngine.History) if ((int)receipt.NpcKind == i) actualScore = receipt.NpcScore;
                SmokeCheck(row.Find("Name").GetComponent<UnityEngine.UI.Text>().text == ScoreRules.Name((ScoreHandKind)i) &&
                    row.Find("Used").GetComponent<UnityEngine.UI.Text>().text == (used ? "✓" : "") &&
                    row.Find("Value").GetComponent<UnityEngine.UI.Text>().text == (used ? actualScore.ToString() : "—"),
                    "enemy role row " + i + " matches its actual used status and receipt score");
            }
        }        private void SDSmokeTableClick(string diePath = null)
        {
            var target = diePath == null ? _sdBattle : (RectTransform)_sdBattle.Find(diePath);
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, target.position),
                pointerPressRaycast = new RaycastResult { gameObject = target.gameObject },
                pointerCurrentRaycast = new RaycastResult { gameObject = target.gameObject } };
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        private void SDSmokeFlick(int index, float distance, bool practice = false)
        {
            var root = practice ? _sdPractice : _sdBattle;
            var die = (RectTransform)root.Find((practice ? "PracticeDie" : "Die") + index);
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, die.position);
            Vector2 end = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(die.anchoredPosition + Vector2.down * distance));
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pressPosition = start, position = start };
            ExecuteEvents.Execute(die.gameObject, data, ExecuteEvents.beginDragHandler);
            data.position = end;
            ExecuteEvents.Execute(die.gameObject, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(die.gameObject, data, ExecuteEvents.endDragHandler);
        }
        private static bool SDArrayEquals(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        private int SDVisiblePips(int index)
        {
            int mask = 0;
            for (int i = 0; i < 7; i++)
            {
                var pip = _sdBattle.Find("Die" + index + "/Face/Pip" + i).GetComponent<UnityEngine.UI.Image>();
                if (pip.enabled && pip.gameObject.activeInHierarchy) mask |= 1 << i;
            }
            return mask;
        }
    }
}
