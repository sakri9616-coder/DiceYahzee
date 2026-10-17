using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Core;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ParvaMirabilia.Quality.Tests
{
    // This namespace deliberately does not inherit HistoricalPresentationScope.
    public sealed class ScoreDuelPresentationTests
    {
        private GameFlow _flow;
        private int _motion;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ScoreDuel Engine => Read<ScoreDuel>("_sdEngine");

        [UnitySetUp] public IEnumerator SetUp()
        {
            GameFlow.ConfigOverride = null;
            GameFlow.LegacyPresentationForTests = false;
            GameFlow.SeedOverride = () => 617;
            _motion = PlayerPrefs.GetInt("arcane.reducedMotion", 0);
            PlayerPrefs.SetInt("arcane.reducedMotion", 0);
            PlayerPrefs.SetInt("pv.audioStep", 3);
            foreach (var old in Object.FindObjectsByType<GameFlow>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            yield return null;
            _flow = new GameObject("ProductionScoreDuelUnderTest").AddComponent<GameFlow>();
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            GameFlow.ConfigOverride = null;
            GameFlow.SeedOverride = null;
            GameFlow.LegacyPresentationForTests = false;
            PlayerPrefs.SetInt("arcane.reducedMotion", _motion);
            Screen.SetResolution(1280, 720, false);
            if (_flow != null) Object.Destroy(_flow.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest] public IEnumerator ProductionShowsTheNineChoices_AndTheOpponentsActualDice()
        {
            Assert.That(Root("_sdTitle").gameObject.activeInHierarchy, Is.True);
            Assert.That(Root("_sdTitle").Find("Title").GetComponent<Text>().text, Is.EqualTo("DiceYahzee"));
            Assert.That(Root("_titleRoot").childCount, Is.Zero, "production must not construct legacy controls");
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(.8f);
            var battle = Root("_sdBattle");
            Assert.That(battle.Find("CurrentRole"), Is.Not.Null);
            Assert.That(battle.Find("Brand").GetComponent<Text>().text, Is.EqualTo("DiceYahzee"));
            var choices = (RectTransform)battle.Find("ScoreChoices");
            Assert.That(choices.anchoredPosition.x, Is.GreaterThan(0), "the main role selector is on the right");
            Assert.That(choices.Find("WheelHint"), Is.Null);
            Assert.That(choices.Find("WheelSymbol").GetComponent<Image>().sprite, Is.Not.Null);
            var role = (RectTransform)battle.Find("CurrentRole");
            Assert.That(role.anchoredPosition.x + role.rect.width * .5f,
                Is.LessThan(choices.anchoredPosition.x - choices.rect.width * .5f), "role summary and right selector must not overlap");
            Assert.That(Root("_sdSheet").gameObject.activeSelf, Is.False);
            Assert.That(Engine.RerollsLeft, Is.EqualTo(2));
            Assert.That(Engine.NpcDice, Is.Null);
            Assert.That(Root("_sdNpcReveal").gameObject.activeSelf, Is.True);
            var live = Read<Button[]>("_sdLiveCategories");
            Assert.That(live.Length, Is.EqualTo(9));
            foreach (Button choice in live) Assert.That(choice.gameObject.activeInHierarchy, Is.True);
            for (int i = 0; i < 5; i++)
                Assert.That(Root("_sdNpcReveal").Find("NpcDie" + i + "/Face").GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(Engine.Intent.Score, Is.EqualTo(ScoreRules.Evaluate(Engine.Intent.Kind, NpcHand()).Score));
            Assert.That(battle.Find("Empower"), Is.Null);
            Assert.That(battle.Find("Mana"), Is.Null);
            for (int i = 0; i < 9; i++) Assert.That(battle.Find("Yaku" + i), Is.Null);
            Assert.That(Read<Text>("_sdRoleLabel").text, Does.Contain(ScoreRules.Name(Read<ScoreHandKind>("_sdSelectedKind"))));
        }

        [UnityTest] public IEnumerator HandNoticeArrivesAfterLanding_AndHoldingDoesNotRepeatIt()
        {
            // Find a real generated hand, rather than overriding a production engine's dice.
            for (int seed = 0; seed < 100; seed++)
            {
                var sample = new ScoreDuel(seed); sample.Roll();
                if (!ScoreRules.Evaluate(ScoreHandKind.Pair, sample.PlayerDice).IsFormed) continue;
                int selectedSeed = seed;
                GameFlow.SeedOverride = () => selectedSeed;
                break;
            }
            Press("_sdTitle", "Start");
            Assert.That(Read<bool>("_sdRolling"), Is.True);
            Assert.That(Read<int>("_sdFormationCount"), Is.Zero);
            Assert.That(Read<Text>("_sdRoleLabel").text, Is.Empty);
            Assert.That(Read<Text>("_sdScoreNumber").text, Is.EqualTo("—"));
            Assert.That(Root("_sdBattle").Find("Commit"), Is.Null);
            Call("SDCommit");
            Assert.That(Engine.History.Count, Is.Zero);
            yield return new WaitForSeconds(.72f);
            Assert.That(Read<bool>("_sdRolling"), Is.False);
            Assert.That(Read<int>("_sdFormationCount"), Is.EqualTo(1));
            Assert.That(Read<Text>("_sdToast").text, Does.Contain("×"));
            Assert.That(Read<int>("_sdFormationMask"), Is.Not.Zero);
            int notices = Read<int>("_sdFormationCount");
            Press("_sdBattle", "Die0");
            yield return new WaitForSeconds(.1f);
            Assert.That(Read<int>("_sdFormationCount"), Is.EqualTo(notices));
        }

        [UnityTest] public IEnumerator HeldDieKeepsItsRenderedPips_WhileOtherDiceRoll()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(.9f);
            Press("_sdBattle", "Die0");
            yield return new WaitForSeconds(.3f);
            var held = Root("_sdBattle").Find("Die0");
            int pips = PipMask(held);
            Vector3 scale = held.localScale;
            int value = Engine.PlayerDice[0];
            Call("SDHandleRightClick");
            Assert.That(IsRolling(0), Is.False);
            Assert.That(IsRolling(1), Is.True);
            yield return new WaitForSeconds(.12f);
            Assert.That(PipMask(held), Is.EqualTo(pips), "held face must remain visually unchanged");
            Assert.That(Vector3.Distance(held.localScale, scale), Is.LessThan(.001f));
            Assert.That(Engine.PlayerDice[0], Is.EqualTo(value));
            Assert.That(Read<Text>("_sdRoleLabel").text, Is.Empty);
            Assert.That(Read<Text>("_sdScoreNumber").text, Is.EqualTo("—"));
            yield return new WaitForSeconds(.7f);
            Assert.That(Read<bool>("_sdRolling"), Is.False);
        }

        [UnityTest] public IEnumerator TwoRerollsAndOneCommitResolveATurn_WithTheDisplayedDamage()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(.8f);
            for (int i = 0; i < 2; i++)
            {
                Call("SDHandleRightClick");
                yield return new WaitForSeconds(.8f);
                Assert.That(Engine.RerollsLeft, Is.EqualTo(1 - i));
            }
            Assert.That(Root("_sdBattle").Find("Reroll"), Is.Null);
            var forecast = Engine.Forecast(Read<ScoreHandKind>("_sdSelectedKind"));
            int playerBefore = Engine.PlayerHp, npcBefore = Engine.NpcHp;
            PullToFire();
            Assert.That(Engine.History.Count, Is.EqualTo(1));
            Assert.That(Engine.LastRound.PlayerRerollsUsed, Is.EqualTo(2));
            Assert.That(Engine.LastRound.DamageToPlayer, Is.EqualTo(forecast.DamageToPlayer));
            Assert.That(Engine.LastRound.DamageToNpc, Is.EqualTo(forecast.DamageToNpc));
            Assert.That(Engine.PlayerHp, Is.EqualTo(Mathf.Max(0, playerBefore - forecast.DamageToPlayer)));
            Assert.That(Engine.NpcHp, Is.EqualTo(Mathf.Max(0, npcBefore - forecast.DamageToNpc)));
            Assert.That(Engine.NpcDice.Length, Is.EqualTo(5));
            Assert.That(Root("_sdNpcReveal").gameObject.activeInHierarchy, Is.True);
            yield return new WaitForSeconds(.95f);
            TableClick();
            Assert.That(Engine.Round, Is.EqualTo(2));
            Assert.That(Read<bool>("_sdRolling"), Is.True, "next turn must animate too");
            Assert.That(Root("_sdNpcReveal").gameObject.activeSelf, Is.True);
            yield return new WaitForSeconds(.8f);
        }

        [UnityTest] public IEnumerator AlwaysVisibleChoicesSelectAndConsumeOneCategory()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(.8f);
            var categories = Read<Button[]>("_sdLiveCategories");
            Assert.That(categories.Length, Is.EqualTo(9));
            for (int i = 0; i < 9; i++) Assert.That(categories[i].interactable, Is.True, "unused zero-point choices are legal");
            categories[(int)ScoreHandKind.Chance].onClick.Invoke();
            yield return null;
            PullToFire();
            Assert.That(Engine.PlayerUsed[(int)ScoreHandKind.Chance], Is.True);
            yield return new WaitForSeconds(.95f);
            TableClick();
            yield return new WaitForSeconds(.8f);
            Assert.That(Read<ScoreHandKind>("_sdSelectedKind"), Is.Not.EqualTo(ScoreHandKind.Chance));
            Assert.That(categories[(int)ScoreHandKind.Chance].interactable, Is.False);
            Call("SDSelectCategory", (int)ScoreHandKind.Chance);
            Assert.That(Root("_sdSheet").gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator ModalBlocksGameplay_AndPracticeNeverChangesAnOngoingMatch()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(.8f);
            var engine = Engine;
            int[] originalDice = (int[])engine.PlayerDice.Clone();
            Call("SDOpenRules");
            Call("SDReroll"); Call("SDCommit"); Call("SDOnDieClicked", 0);
            Call("SDHandleShortcut", KeyCode.Space);
            Assert.That(engine.RerollsLeft, Is.EqualTo(2));
            Assert.That(engine.History.Count, Is.Zero);
            CollectionAssert.AreEqual(originalDice, engine.PlayerDice);
            Call("SDHandleShortcut", KeyCode.Escape);
            Call("SDHandleShortcut", KeyCode.Space);
            Call("SDHandleShortcut", KeyCode.Alpha1);
            Call("SDHandleShortcut", KeyCode.Return);
            Assert.That(Root("_sdRules").gameObject.activeSelf, Is.False);
            Assert.That(engine.RerollsLeft, Is.EqualTo(2), "closing a modal must consume the input frame");
            Assert.That(engine.Held[0], Is.False);
            Assert.That(engine.History.Count, Is.Zero);
            Call("SDOpenPractice");
            Call("SDOnPracticeDie", 0); Call("SDOnPracticeDie", 1);
            yield return null; Call("SDHandleRightClick");
            yield return new WaitForSeconds(.8f);
            Assert.That(Read<Text>("_sdPracticeFeedback").text, Does.Contain(ScoreRules.Evaluate(ScoreHandKind.Three, new[] { 1, 1, 1, 5, 6 }).Score.ToString()));
            PullToFire(true);
            Press("_sdPractice", "PracticeFinish");
            Assert.That(Engine, Is.SameAs(engine));
            Assert.That(engine.History.Count, Is.Zero);
            CollectionAssert.AreEqual(originalDice, engine.PlayerDice);
        }

        [UnityTest] public IEnumerator WholeMatchCanFinishAndRematch_WithoutLegacySystems()
        {
            Press("_sdTitle", "Start");
            int guard = 0;
            while (!Root("_sdResult").gameObject.activeSelf && guard++ < ScoreDuel.SafetyRoundBound + 1)
            {
                yield return new WaitForSeconds(.8f);
                PullToFire();
                yield return new WaitForSeconds(.95f);
                TableClick();
            }
            Assert.That(Root("_sdResult").gameObject.activeInHierarchy, Is.True);
            Assert.That(Engine.Outcome, Is.Not.Null);
            Assert.That(Engine.History.Count, Is.InRange(1, ScoreDuel.SafetyRoundBound));
            Assert.That(Engine.PlayerHp == 0 || Engine.NpcHp == 0, Is.True, "a duel must finish in a knockout");
            Press("_sdResult", "Rematch");
            Assert.That(Engine.Round, Is.EqualTo(1));
            Assert.That(Engine.History.Count, Is.Zero);
            Assert.That(Engine.PlayerHp, Is.EqualTo(ScoreDuel.StartingHp));
            foreach (bool used in Engine.PlayerUsed) Assert.That(used, Is.False);
            yield return new WaitForSeconds(.8f);
        }

        [UnityTest] public IEnumerator VisibleControlsFitNarrowAndWideWindows()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(.8f);
            foreach (Vector2Int size in new[] { new Vector2Int(960, 720), new Vector2Int(1600, 900) })
            {
                Screen.SetResolution(size.x, size.y, false);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (Button button in Root("_sdRoot").GetComponentsInChildren<Button>())
                {
                    if (!button.gameObject.activeInHierarchy) continue;
                    var corners = new Vector3[4];
                    button.GetComponent<RectTransform>().GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                    {
                        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner);
                        Assert.That(point.x, Is.InRange(-1f, Screen.width + 1f), button.name);
                        Assert.That(point.y, Is.InRange(-1f, Screen.height + 1f), button.name);
                    }
                }
                foreach (string field in new[] { "_sdRoleLabel", "_sdIntentLabel", "_sdScoreNumber", "_sdBreakdown" })
                {
                    var text = Read<Text>(field);
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), field);
                }
            }
        }

        [UnityTest] public IEnumerator IllustratedBattleHudKeepsScoreFormulaAndHealthDistinct()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(1.8f);
            Canvas.ForceUpdateCanvases();
            var selected = Read<ScoreHandKind>("_sdSelectedKind");
            var offer = ScoreRules.Evaluate(selected, Engine.PlayerDice);
            var forecast = Engine.Forecast(selected);
            Assert.That(Read<Text>("_sdRoleLabel").text, Is.EqualTo(ScoreRules.Name(selected)));
            Assert.That(Read<Text>("_sdScoreNumber").text, Is.EqualTo(offer.Score.ToString()));
            Assert.That(ScoreDuelHealthLine.Damage(Read<Text>("_sdNpcHpLabel")), Is.EqualTo(forecast.DamageToNpc));
            Assert.That(ScoreDuelHealthLine.Damage(Read<Text>("_sdPlayerHpLabel")), Is.EqualTo(forecast.DamageToPlayer));
            Assert.That(Read<Image>("_sdPlayerBar").color, Is.Not.EqualTo(Read<Image>("_sdNpcBar").color));
            foreach (string field in new[] { "_sdInstruction", "_sdEffectLabel" })
            {
                Assert.That(Read<Text>(field).text, Is.Empty);
                Assert.That(Read<Text>(field).gameObject.activeInHierarchy, Is.False);
            }
            foreach (Text text in Root("_sdRoot").GetComponentsInChildren<Text>())
                foreach (string prose in new[] { "右クリック", "下へ引", "離すと", "ホイール", "残したダイス" })
                    Assert.That(text.text, Does.Not.Contain(prose), text.name);
            Assert.That(Read<Image>("_sdSaveSymbol").sprite, Is.Not.Null);
            Assert.That(Root("_sdBattle").Find("RerollSymbol"), Is.Null);
            var role = Root("_sdBattle").Find("CurrentRole").GetComponent<RectTransform>();
            Assert.That(role.GetComponent<Image>(), Is.Null, "score summary stays on the table material");
            var corners = new Vector3[4];
            role.GetWorldCorners(corners);
            var diceCorners = new Vector3[4];
            for (int i = 0; i < 5; i++)
            {
                Root("_sdBattle").Find("Die" + i).GetComponent<RectTransform>().GetWorldCorners(diceCorners);
                Assert.That(diceCorners[0].y, Is.GreaterThan(corners[2].y), "role summary must clear the rendered die " + i);
            }
            Call("SDOpenPractice");
            foreach (Text text in Root("_sdRoot").GetComponentsInChildren<Text>())
                foreach (string prose in new[] { "右クリック", "下へ引", "離すと", "ホイール", "残したダイス", "固定出目の操作練習" })
                    Assert.That(text.text, Does.Not.Contain(prose), text.name);
        }

        [UnityTest] public IEnumerator SymmetricRoleRailsLeaveTheCentralPlayClear_AndRecordTheEnemiesActualUsedRole()
        {
            Press("_sdTitle", "Start");
            yield return new WaitForSeconds(1.8f);
            var battle = Root("_sdBattle");
            var left = (RectTransform)battle.Find("NpcScorecard");
            var right = (RectTransform)battle.Find("ScoreChoices");
            Assert.That(left.anchoredPosition.x, Is.EqualTo(-right.anchoredPosition.x).Within(.1f));
            Assert.That(left.anchoredPosition.y, Is.EqualTo(right.anchoredPosition.y).Within(.1f));
            Assert.That(left.rect.size, Is.EqualTo(right.rect.size));
            Assert.That(left.rect.width, Is.LessThanOrEqualTo(190f), "role rails leave more of the central play visible");
            Assert.That(left.rect.height, Is.LessThanOrEqualTo(400f), "role rails no longer span the HP bars");
            var carry = Read<Button>("_sdSaveButton");
            Assert.That(carry.transform.parent, Is.EqualTo(battle), "the carry action belongs to the central table");
            Rect carryBounds = TableBounds((RectTransform)carry.transform);
            Rect scoreBounds = TableBounds(Read<Text>("_sdScoreNumber").rectTransform);
            Assert.That(carryBounds.center.x, Is.EqualTo(scoreBounds.center.x).Within(.1f), "carry stays centered under the score");
            Assert.That(carryBounds.yMax, Is.LessThan(scoreBounds.center.y), "carry sits below the score");
            Assert.That(carryBounds.xMin, Is.GreaterThan(TableBounds(left).xMax), "carry clears the enemy rail");
            Assert.That(carryBounds.xMax, Is.LessThan(TableBounds(right).xMin), "carry clears the player rail");
            Assert.That(carryBounds.yMin, Is.GreaterThan(TableBounds((RectTransform)battle.Find("PlayerBar")).yMax), "carry clears the HP bar");
            Assert.That(left.GetComponentsInChildren<Button>().Length, Is.Zero, "enemy history is read-only");
            AssertCentralCorridor(left, right);
            AssertFullWidthTableAndCenteredHealth(left, right);
            Assert.That(battle.Find("IntentPanel"), Is.Null);
            for (int i = 0; i < ScoreRules.HandCount; i++)
            {
                var row = left.Find("NpcCategory" + i);
                Assert.That(row.Find("Name").GetComponent<Text>().text, Is.EqualTo(ScoreRules.Name((ScoreHandKind)i)));
                Assert.That(row.Find("Name").GetComponent<Text>().fontSize, Is.GreaterThanOrEqualTo(16));
                Assert.That(row.Find("Value").GetComponent<Text>().text, Is.EqualTo("—"));
                Assert.That(row.Find("Used").GetComponent<Text>().text, Is.Empty);
                foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
            }
            PullToFire();
            yield return new WaitForSeconds(.95f);
            var receipt = Engine.LastRound;
            for (int i = 0; i < ScoreRules.HandCount; i++)
            {
                var row = left.Find("NpcCategory" + i);
                bool used = Engine.NpcUsed[i];
                Assert.That(used, Is.EqualTo(i == (int)receipt.NpcKind));
                Assert.That(row.Find("Used").GetComponent<Text>().text, Is.EqualTo(used ? "✓" : ""));
                Assert.That(row.Find("Value").GetComponent<Text>().text, Is.EqualTo(used ? receipt.NpcScore.ToString() : "—"));
            }
            AssertCentralCorridor(left, right);
            AssertFullWidthTableAndCenteredHealth(left, right);
        }

        private void AssertCentralCorridor(RectTransform left, RectTransform right)
        {
            Rect leftBounds = TableBounds(left), rightBounds = TableBounds(right);
            foreach (string path in new[] { "Die0", "Die1", "Die2", "Die3", "Die4", "NpcReveal", "Intent", "NpcFormula", "NpcScore", "CurrentRole" })
            {
                Rect bounds = TableBounds((RectTransform)Root("_sdBattle").Find(path));
                Assert.That(bounds.xMin, Is.GreaterThan(leftBounds.xMax), path + " clears the enemy role rail");
                Assert.That(bounds.xMax, Is.LessThan(rightBounds.xMin), path + " clears the player role rail");
            }
        }

        private void AssertFullWidthTableAndCenteredHealth(RectTransform left, RectTransform right)
        {
            var battle = Root("_sdBattle");
            var rim = (RectTransform)battle.Find("TableRim");
            var mat = (RectTransform)battle.Find("TableMat");
            Assert.That(rim.rect.width, Is.GreaterThanOrEqualTo(Mathf.Min(battle.rect.width, 1280f) * .9f), "table spans nearly the reference battle width");
            Assert.That(rim.anchoredPosition.x, Is.Zero.Within(.1f));
            Rect table = TableBounds(mat);
            foreach (var card in new[] { left, right })
            {
                Rect bounds = TableBounds(card);
                float overlap = Mathf.Min(bounds.xMax, table.xMax) - Mathf.Max(bounds.xMin, table.xMin);
                Assert.That(overlap, Is.GreaterThan(card.rect.width * .9f), "central bounds continue behind " + card.name);
                Assert.That(mat.GetSiblingIndex(), Is.LessThan(card.GetSiblingIndex()), "role rail draws over the central bounds");
                Assert.That(card.GetComponent<Image>().color.a, Is.LessThan(.01f), "role rail has no backing window");
                Assert.That(card.Find("Heading").GetComponent<Text>().fontSize, Is.GreaterThanOrEqualTo(20));
            }
            foreach (string field in new[] { "_sdNpcHpLabel", "_sdPlayerHpLabel" })
            {
                Text label = Read<Text>(field);
                Rect bounds = TableBounds(label.rectTransform);
                bool npcSide = field == "_sdNpcHpLabel";
                Rect bar = TableBounds((RectTransform)battle.Find(npcSide ? "NpcBar" : "PlayerBar"));
                Assert.That(bounds.center.x, Is.Zero.Within(.1f), field + " is centered over the play area");
                Assert.That(bounds.center.y, Is.EqualTo(bar.center.y).Within(.1f), field + " sits inside the thicker health bar");
                Assert.That(bar.height, Is.GreaterThanOrEqualTo(16f));
                Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal), field + " keeps health and damage in one face");
                Assert.That(label.text, Does.StartWith(npcSide ? "相手 " : "あなた "),
                    "the centered line opens with the side it belongs to");
                Assert.That(label.text, Does.Not.Contain(" / "), "health prints as one figure, not a spaced pair");
                // This runs after real rounds, so the bar may still be draining: what has to hold is
                // that the line states one readable pool against the cap, not that it has settled.
                string[] parts = label.text.Split(' ');
                int shownHealth;
                Assert.That(parts.Length, Is.GreaterThanOrEqualTo(2), "the line carries health after its side name");
                Assert.That(int.TryParse(parts[1].Split('/')[0], out shownHealth), Is.True, "the line prints a readable health figure");
                Assert.That(parts[1].Split('/')[1], Is.EqualTo((npcSide ? Engine.NpcMaxHp : ScoreDuel.StartingHp).ToString()),
                    "the line states the cap this bar drains from");
                Assert.That(shownHealth, Is.InRange(0, npcSide ? Engine.NpcMaxHp : ScoreDuel.StartingHp));
            }
            Rect playerBar = TableBounds((RectTransform)battle.Find("PlayerBar"));
            for (int i = 0; i < 2; i++)
                Assert.That(TableBounds((RectTransform)battle.Find("RollPip" + i)).yMax,
                    Is.LessThan(playerBar.yMin), "reroll dots sit below the player health bar");
        }
        private Rect TableBounds(RectTransform rect)
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
        private int[] NpcHand() { var hand = new int[5]; for (int i = 0; i < 5; i++) hand[i] = Engine.PlannedNpcDice[i]; return hand; }
        private RectTransform Root(string name) => Read<RectTransform>(name);
        private void Call(string method, params object[] args) => typeof(GameFlow).GetMethod(method, Private).Invoke(_flow, args);
        private void Press(string field, string child)
        {
            var button = Root(field).Find(child).GetComponent<Button>();
            Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, child);
            button.onClick.Invoke();
        }
        private bool IsRolling(int index)
        {
            object die = Read<IList>("_sdDice")[index];
            return (bool)die.GetType().GetProperty("IsRolling").GetValue(die);
        }
        private static int PipMask(Transform die)
        {
            int mask = 0;
            for (int i = 0; i < 7; i++)
            {
                var pip = die.Find("Face/Pip" + i).GetComponent<Image>();
                if (pip.enabled && pip.gameObject.activeInHierarchy) mask |= 1 << i;
            }
            return mask;
        }
    }
}
