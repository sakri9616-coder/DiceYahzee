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
    /// <summary>
    /// The readability pass made several numbers easier to read and added two bits of feedback
    /// (a carry pulse and a health-drop amount). Each of those is a visual claim, so each is
    /// measured here rather than left to the eye: font sizes, contrast edges and the colours that
    /// separate a spent yaku from a live one. All five difficulties are checked end to end
    /// because the enemy health pool touches the rules, the HUD and the title together.
    /// </summary>
    public sealed class ScoreDuelReadabilityTests
    {
        private GameFlow _flow;
        private int _motion, _audio;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ScoreDuel Engine => Read<ScoreDuel>("_sdEngine");

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
            _flow = new GameObject("ReadabilityImprovementsUnderTest").AddComponent<GameFlow>();
            yield return null;
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

        [UnityTest] public IEnumerator LargeScoresAndDamageHaveAContrastEdgeAndFitTheirBox()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(1.0f);
            Canvas.ForceUpdateCanvases();

            Text score = Read<Text>("_sdScoreNumber");
            Assert.That(score.fontSize, Is.GreaterThanOrEqualTo(58), "the settled score should be larger than before");
            Assert.That(score.GetComponent<Outline>(), Is.Not.Null, "the score needs a dark edge over the leather");
            Assert.That(score.rectTransform.rect.height, Is.GreaterThanOrEqualTo(score.preferredHeight),
                "the enlarged score must still fit its own box");

            Text npc = Read<Text>("_sdNpcScoreNumber");
            Assert.That(npc.fontSize, Is.GreaterThanOrEqualTo(50));
            Assert.That(npc.GetComponent<Outline>(), Is.Not.Null);
            Assert.That(Read<Text>("_sdNpcFormula").fontSize, Is.LessThan(npc.fontSize));

            // The drop is no longer a second numeral warped onto the right end of the bar. Each bar
            // prints one line — side, health, then the drop it is taking in red — so the claim to
            // hold is that the line lives inside its own track, at one size, and carries the figure
            // the core forecasts.
            var forecast = Engine.Forecast(Read<ScoreHandKind>("_sdSelectedKind"));
            foreach (string field in new[] { "_sdNpcHpLabel", "_sdPlayerHpLabel" })
            {
                Text line = Read<Text>(field);
                bool npcSide = field == "_sdNpcHpLabel";
                var track = (RectTransform)Read<Image>(npcSide ? "_sdNpcBar" : "_sdPlayerBar").transform.parent;
                Assert.That(line.transform.parent, Is.SameAs(track),
                    "the health line belongs to the bar it drains, not beside it");
                Assert.That(track.rect.Contains((Vector2)line.rectTransform.localPosition), Is.True,
                    "the health line must stay within its own bar");
                Assert.That(line.fontSize, Is.EqualTo(18), "health and its drop share one size");
                Assert.That(line.fontStyle, Is.EqualTo(FontStyle.Bold),
                    "health text follows the shared heavier text face");
                var edge = line.GetComponent<Outline>();
                Assert.That(edge, Is.Not.Null, "the line still needs a dark edge where it crosses the drain band");
                Assert.That(Mathf.Abs(edge.effectDistance.x), Is.LessThanOrEqualTo(1.5f),
                    "the edge backs the glyphs instead of fattening them");
                Assert.That(line.text, Is.EqualTo(ScoreDuelHealthLine.Expected(
                    npcSide ? "相手" : "あなた",
                    npcSide ? Engine.NpcHp : Engine.PlayerHp,
                    npcSide ? Engine.NpcMaxHp : ScoreDuel.StartingHp,
                    npcSide ? forecast.DamageToNpc : forecast.DamageToPlayer)),
                    "the bar prints side, health and the drop on one centred line");
            }
            foreach (var text in Root("_sdBattle").GetComponentsInChildren<Text>(true))
                Assert.That(new[] { "NpcDamage", "PlayerDamage", "NpcHpDelta", "PlayerHpDelta" }, Does.Not.Contain(text.name),
                    "no second numeral sits loose on top of a bar");
            yield return null;
        }

        [UnityTest] public IEnumerator RerollCountKeepsBothSlotsVisibleSpentAndUnspent()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(1.0f);
            var pips = Read<Image[]>("_sdRollPips");
            Assert.That(pips.Length, Is.EqualTo(3), "the rail reserves a slot for a Yahtzee bonus");
            Assert.That(pips[0].rectTransform.rect.width, Is.GreaterThanOrEqualTo(14f));
            Assert.That(pips[0].GetComponent<Outline>(), Is.Not.Null);
            Assert.That(pips[1].GetComponent<Outline>(), Is.Not.Null);
            Assert.That(pips[2].GetComponent<Outline>(), Is.Not.Null);
            Assert.That(Engine.RerollsLeft, Is.EqualTo(2));
            Assert.That(pips[0].gameObject.activeInHierarchy, Is.True);
            Assert.That(pips[1].gameObject.activeInHierarchy, Is.True);
            Assert.That(pips[2].gameObject.activeInHierarchy, Is.False, "a normal round hides the bonus slot");
            Assert.That(pips[0].color.a, Is.GreaterThan(.9f));
            Assert.That(pips[1].color.a, Is.GreaterThan(.9f), "both reroll slots should read as available");

            Call("SDHandleRightClick");
            yield return new WaitForSeconds(.8f);
            Assert.That(Engine.RerollsLeft, Is.EqualTo(1));
            Assert.That(pips[0].gameObject.activeInHierarchy, Is.True);
            Assert.That(pips[1].gameObject.activeInHierarchy, Is.True, "a spent slot must stay visible");
            Assert.That(pips[2].gameObject.activeInHierarchy, Is.False, "the bonus slot stays hidden off a bonus round");
            Assert.That(pips[1].color.a, Is.GreaterThan(.3f), "the spent slot must not vanish into the table");
            Assert.That(pips[0].color.a, Is.GreaterThan(pips[1].color.a));
            yield return null;
        }

        [UnityTest] public IEnumerator YahtzeeBonusRoundUnlocksAThirdGoldPip()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(1.0f);
            var pips = Read<Image[]>("_sdRollPips");
            Assert.That(pips[2].gameObject.activeInHierarchy, Is.False, "round one is a normal two-reroll round");

            // Commit a real formed Yahtzee so the next round earns the core's extra reroll
            // instead of fabricating the state the HUD is meant to read.
            for (int i = 0; i < 5; i++) Engine.PlayerDice[i] = 6;
            Call("SDSelectCategory", (int)ScoreHandKind.Five);
            Call("SDCommit");
            Assert.That(Engine.LastRound.PlayerKind, Is.EqualTo(ScoreHandKind.Five));
            Assert.That(Engine.LastRound.PlayerScore, Is.EqualTo(150));
            Call("SDResetImpactPresentation");
            Call("SDAdvance");
            Assert.That(Engine.RerollsLeft, Is.EqualTo(3), "a formed Yahtzee grants a third reroll next round");

            Assert.That(pips[2].gameObject.activeInHierarchy, Is.True, "the bonus round reveals the third slot");
            Assert.That(pips[0].color.a, Is.GreaterThan(.9f));
            Assert.That(pips[2].color.a, Is.GreaterThan(.9f), "all three bonus rerolls read as available");

            yield return new WaitForSeconds(1.0f);
            Call("SDHandleRightClick");
            yield return null;
            Assert.That(Engine.RerollsLeft, Is.EqualTo(2));
            Assert.That(pips[2].gameObject.activeInHierarchy, Is.True, "a spent bonus slot must stay visible");
            Assert.That(pips[2].color.a, Is.GreaterThan(.3f));
            Assert.That(pips[0].color.a, Is.GreaterThan(pips[2].color.a), "the spent bonus slot recedes");
            yield return null;
        }

        [UnityTest] public IEnumerator UsedYakuTextIsDimmedAgainstTheUnusedRail()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(1.0f);
            PressLive(ScoreHandKind.Chance);
            PullToFire();
            yield return new WaitForSeconds(1.0f);
            TableClick();
            yield return new WaitForSeconds(.9f);
            Assert.That(Engine.PlayerUsed[(int)ScoreHandKind.Chance], Is.True);

            var categories = Read<Button[]>("_sdLiveCategories");
            Color used = categories[(int)ScoreHandKind.Chance].transform.Find("Name").GetComponent<Text>().color;
            int unusedIndex = -1;
            int selected = (int)Read<ScoreHandKind>("_sdSelectedKind");
            for (int i = 0; i < 9; i++)
                if (!Engine.PlayerUsed[i] && i != selected) { unusedIndex = i; break; }
            Assert.That(unusedIndex, Is.GreaterThanOrEqualTo(0), "there must be an unused row to compare against");
            Color unused = categories[unusedIndex].transform.Find("Name").GetComponent<Text>().color;
            Assert.That(Luminance(used), Is.LessThan(Luminance(unused)),
                "a spent yaku name must be darker than a live one");
            yield return null;
        }

        [UnityTest] public IEnumerator CarryTogglePlaysAReversiblePulse()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(1.0f);
            Assert.That(Read<float>("_sdCarryToggleAge"), Is.LessThan(0));

            var save = Read<Button>("_sdSaveButton");
            Assert.That(((RectTransform)save.transform).anchoredPosition.x, Is.Zero.Within(.1f),
                "carry remains on the central action axis");
            yield return null;
            save.onClick.Invoke();
            Assert.That(Read<bool>("_sdSaveArmed"), Is.True);
            Assert.That(Read<float>("_sdCarryToggleAge"), Is.GreaterThanOrEqualTo(0), "arming carry starts its pulse");
            Graphic effect = Read<Graphic>("_sdCarryEffect");
            int strokes = (int)effect.GetType().GetField("_count", Private).GetValue(effect);
            Assert.That(strokes, Is.GreaterThan(0), "the pulse must actually draw geometry");

            Call("SDTickCarryToggle", 1f);
            Assert.That(Read<float>("_sdCarryToggleAge"), Is.LessThan(0), "the pulse ends");
            Assert.That(Read<RectTransform>("_sdCarryRack").localScale, Is.EqualTo(Vector3.one));

            yield return null;
            save.onClick.Invoke();
            Assert.That(Read<bool>("_sdSaveArmed"), Is.False);
            Assert.That(Read<float>("_sdCarryToggleAge"), Is.GreaterThanOrEqualTo(0), "disarming replays the pulse");
            Call("SDTickCarryToggle", 1f);
            Assert.That(Read<float>("_sdCarryToggleAge"), Is.LessThan(0));
            yield return null;
        }

        [UnityTest] public IEnumerator HpDropIsNamedInRedInsideTheDrainingBar()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(.8f);
            PullToFire();
            var round = Engine.LastRound;
            Assert.That(round.DamageToNpc, Is.GreaterThan(0));
            Assert.That(round.DamageToPlayer, Is.GreaterThan(0));

            Call("SDShowHpDeltas", 1f);
            Text npcLine = Read<Text>("_sdNpcHpLabel");
            Text playerLine = Read<Text>("_sdPlayerHpLabel");
            Assert.That(npcLine.text, Does.Contain(ScoreDuelHealthLine.Red), "the drop is printed in red");
            Assert.That(playerLine.text, Does.Contain(ScoreDuelHealthLine.Red));
            Assert.That(ScoreDuelHealthLine.Damage(npcLine), Is.EqualTo(round.DamageToNpc));
            Assert.That(ScoreDuelHealthLine.Damage(playerLine), Is.EqualTo(round.DamageToPlayer));
            var npcBand = Read<Image>("_sdNpcLossBand");
            var playerBand = Read<Image>("_sdPlayerLossBand");
            Assert.That(npcBand.gameObject.activeSelf && playerBand.gameObject.activeSelf, Is.True, "both losses are highlighted on their bars");
            Assert.That(npcBand.rectTransform.rect.width, Is.GreaterThan(0));
            Assert.That(playerBand.rectTransform.rect.width, Is.GreaterThan(0));
            Assert.That(npcBand.transform.parent, Is.EqualTo(Read<Image>("_sdNpcBar").transform.parent));
            Assert.That(playerBand.transform.parent, Is.EqualTo(Read<Image>("_sdPlayerBar").transform.parent));
            Call("SDShowHpDeltas", 0f);
            Assert.That(ScoreDuelHealthLine.Damage(Read<Text>("_sdNpcHpLabel")), Is.Zero, "the figure leaves with the drain");
            Assert.That(ScoreDuelHealthLine.Damage(Read<Text>("_sdPlayerHpLabel")), Is.Zero);
            Assert.That(npcBand.gameObject.activeSelf || playerBand.gameObject.activeSelf, Is.False, "loss emphasis settles after the drain");
            yield return null;
        }

        [UnityTest] public IEnumerator FiveDifficultiesEachStartWithTheirAdvertisedEnemyHealthOnScreen()
        {
            string[] labels = { "入門", "初級", "中級", "上級", "超級" };
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(.8f);
            Call("SDBackToTitle");
            for (int tier = 0; tier < labels.Length; tier++)
            {
                Call("SDSelectOpponent", tier);
                Assert.That(Root("_sdTitle").Find("Opponent" + tier).GetComponentInChildren<Text>().text,
                    Is.EqualTo(labels[tier]));
                Call("SDStartMatch");
                yield return new WaitForSeconds(.8f);

                Assert.That(Engine.NpcMaxHp, Is.EqualTo(ScoreDuel.NpcHpForDifficulty(tier)));
                Assert.That(Engine.NpcHp, Is.EqualTo(ScoreDuel.NpcHpForDifficulty(tier)));
                Assert.That(Engine.PlayerHp, Is.EqualTo(ScoreDuel.StartingHp));
                Assert.That(Read<Text>("_sdNpcHpLabel").text, Does.StartWith("相手 " +
                    ScoreDuel.NpcHpForDifficulty(tier) + "/" + ScoreDuel.NpcHpForDifficulty(tier)));
                Assert.That(Read<Text>("_sdPlayerHpLabel").text, Does.StartWith("あなた " +
                    ScoreDuel.StartingHp + "/" + ScoreDuel.StartingHp));
                Call("SDBackToTitle");
            }
            Assert.That(Read<Button[]>("_sdOpponents").Length, Is.EqualTo(5), "the title offers five difficulties");
            yield return null;
        }

        // ------------------------------------------------------------------ helpers
        private static float Luminance(Color color)
        {
            return 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);
        }

        private static float Linear(float channel)
        {
            return channel <= 0.03928f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        private T Read<T>(string name) => (T)typeof(GameFlow).GetField(name, Private).GetValue(_flow);
        private void Call(string method, params object[] args) => typeof(GameFlow).GetMethod(method, Private).Invoke(_flow, args);
        private RectTransform Root(string name) => Read<RectTransform>(name);
        private void Press(string field, string path)
        {
            Button button = Root(field).Find(path).GetComponent<Button>();
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, path);
            button.onClick.Invoke();
        }
        private void PressLive(ScoreHandKind kind) => Press("_sdBattle", "ScoreChoices/LiveCategory" + (int)kind);

        private void PullToFire()
        {
            var root = Root("_sdBattle");
            var die = (RectTransform)root.Find("Die0");
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, die.position);
            Vector2 end = RectTransformUtility.WorldToScreenPoint(null, root.TransformPoint(die.anchoredPosition + Vector2.down * 120));
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pressPosition = start, position = start };
            ExecuteEvents.Execute(die.gameObject, data, ExecuteEvents.beginDragHandler);
            data.position = end;
            ExecuteEvents.Execute(die.gameObject, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(die.gameObject, data, ExecuteEvents.endDragHandler);
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
    }
}
