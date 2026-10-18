using System;
using System.Collections.Generic;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private RectTransform _sdRoot, _sdTitle, _sdBattle, _sdResult;
        private RectTransform _sdRules, _sdSheet, _sdMenu, _sdCredits, _sdPractice;
        private ScoreDuel _sdEngine;
        private NpcPersonality _sdOpponent = NpcPersonality.Balanced;
        private int _sdOpponentIndex;
        private ScoreHandKind _sdSelectedKind;
        private readonly List<DiceView> _sdDice = new List<DiceView>();
        private readonly List<DiceView> _sdNpcDice = new List<DiceView>();
        private RectTransform _sdNpcReveal;
        private readonly List<DiceView> _sdPracticeDice = new List<DiceView>();
        private readonly bool[] _sdPracticeHeld = new bool[5];
        private int[] _sdPracticeHand;
        private int _sdPracticeStep;
        private bool _sdPracticeRolling, _sdPracticeFromBattle;
        private Text _sdPracticeFeedback;
        private Button _sdPracticeFinishButton;
        private readonly Text[] _sdDieLabels = new Text[5];
        private readonly Button[] _sdOpponents = new Button[SDOpponentProfiles.Length];
        private readonly Button[] _sdCategoryButtons = new Button[9];
        private readonly Text[] _sdCategoryNames = new Text[9];
        private readonly Text[] _sdCategoryValues = new Text[9];
        private readonly Button[] _sdLiveCategories = new Button[9];
        private readonly Text[] _sdLiveValues = new Text[9];
        private readonly Image[] _sdLiveMarkers = new Image[9];
        private Button _sdSaveButton;
        private Text _sdSaveNote, _sdBreakdown;
        private Text _sdRoundLabel, _sdNpcHpLabel, _sdPlayerHpLabel, _sdIntentLabel;
        private Text _sdRoleLabel, _sdEffectLabel, _sdInstruction, _sdToast, _sdHit;
        private Text _sdResultTitle, _sdResultDetail, _sdSheetTitle, _sdSheetNote;
        private Text _sdOpponentNote, _sdMotionLabel;
        private Image _sdNpcBar, _sdPlayerBar;
        private bool _sdRolling, _sdCommitted, _sdReducedMotion, _sdSelectedManually;
        private bool _sdSaveArmed;
        private int _sdInputFrame = -1, _sdFormationMask, _sdFormationCount;
        private ScoreHandKind? _sdLastAnnouncedKind;
        private int _sdLastAnnouncedScore;
        private float _sdToastAge = -1, _sdHitAge = -1, _sdHpAge = -1;
        private float _sdPlayerHpFrom, _sdNpcHpFrom;
        private const float SDToastLife = 1.05f;
        private const float SDHandCenterX = 0;
        private static readonly Color SDInk = new Color32(32, 48, 57, 255);
        private static readonly Color SDMuted = new Color32(171, 191, 194, 255);
        private static readonly Color SDPositive = new Color32(203, 168, 106, 255);
        private static readonly Color SDNegative = new Color32(239, 156, 144, 255);
        private static readonly Color SDScoreGold = new Color32(240, 205, 132, 255);
        /// <summary>Warm off-white body text for the rules panel, legible on the dark modal.</summary>
        private static readonly Color SDCream = new Color32(246, 242, 230, 255);
        // A draw is the one result that gets its own name and its own colour.
        private static readonly Color SDMiracle = new Color32(255, 216, 82, 255);

        /// <summary>One selectable opponent: a planning personality plus the enemy health it starts with.</summary>
        private struct SDOpponentProfile
        {
            public string Label;
            public NpcPersonality Kind;
            public int NpcHp;
        }

        // Five named difficulties played by the same expert planner: only the enemy's health
        // pool changes. Enemy health comes from the rule engine's own table, so the buttons and
        // the match can never drift apart.
        private static readonly SDOpponentProfile[] SDOpponentProfiles =
        {
            new SDOpponentProfile { Label = "入門", Kind = NpcPersonality.Expert, NpcHp = ScoreDuel.NpcHpForDifficulty(0) },
            new SDOpponentProfile { Label = "初級", Kind = NpcPersonality.Expert, NpcHp = ScoreDuel.NpcHpForDifficulty(1) },
            new SDOpponentProfile { Label = "中級", Kind = NpcPersonality.Expert, NpcHp = ScoreDuel.NpcHpForDifficulty(2) },
            new SDOpponentProfile { Label = "上級", Kind = NpcPersonality.Expert, NpcHp = ScoreDuel.NpcHpForDifficulty(3) },
            new SDOpponentProfile { Label = "超級", Kind = NpcPersonality.Expert, NpcHp = ScoreDuel.NpcHpForDifficulty(4) }
        };

        private bool ScoreDuelPresentationActive => _sdRoot != null && _sdRoot.gameObject.activeSelf;
        private bool SDModalOpen => (_sdRules != null && _sdRules.gameObject.activeSelf) ||
            (_sdSheet != null && _sdSheet.gameObject.activeSelf) ||
            (_sdMenu != null && _sdMenu.gameObject.activeSelf) ||
            (_sdCredits != null && _sdCredits.gameObject.activeSelf) ||
            (_sdPractice != null && _sdPractice.gameObject.activeSelf);

        private void BuildScoreDuelPresentation()
        {
            if (_titleRoot != null) _titleRoot.gameObject.SetActive(false);
            if (_battleRoot != null) _battleRoot.gameObject.SetActive(false);
            if (_resultRoot != null) _resultRoot.gameObject.SetActive(false);
            if (_rulesRoot != null) _rulesRoot.gameObject.SetActive(false);
            Transform oldMenu = _canvasRoot.transform.Find("Menu");
            if (oldMenu != null) oldMenu.gameObject.SetActive(false);
            _sdReducedMotion = PlayerPrefs.GetInt("arcane.reducedMotion", 0) != 0;

            _sdRoot = UiFactory.CreateStretch("ScoreDuel", _canvasRoot.transform);
            SDBuildTableBackground(_sdRoot);
            _sdTitle = UiFactory.CreateStretch("SDTitle", _sdRoot);
            _sdBattle = UiFactory.CreateStretch("SDBattle", _sdRoot);
            _sdResult = UiFactory.CreateStretch("SDResult", _sdRoot);
            _sdTitle.gameObject.AddComponent<CanvasGroup>();
            _sdBattle.gameObject.AddComponent<CanvasGroup>();
            _sdResult.gameObject.AddComponent<CanvasGroup>();
            SDBuildTitle();
            SDBuildBattle();
            SDBuildResult();
            SDButton("Menu", _sdRoot, new Vector2(-555, -333), new Vector2(150, 40), "", 17, SDOpenMenu);
            SDBuildRules();
            SDBuildSheet();
            SDBuildMenu();
            SDBuildCredits();
            SDBuildPractice();
            Transform sound = _canvasRoot.transform.Find("SoundToggle");
            if (sound != null)
            {
                sound.GetComponent<Image>().color = SDInk;
                sound.GetComponent<ButtonPalette>().Face = SDInk;
                Transform gloss = sound.Find("Gloss");
                if (gloss != null) gloss.gameObject.SetActive(false);
            }
            // The SD root above was created after the audio chrome and its table is opaque,
            // so without the raise the volume bar stays buried under it, invisible and
            // unclickable, and the mute toggle degenerates into the only working control.
            LiftAudioChrome();
            SDStyleRemainingScreens();
            SDSelectOpponent(0);
            SDBackToTitle();
            BuildOnlineEntry();
            BuildPrivateUpdateUi();
        }

        private Text SDText(string name, Transform root, string text, int size, Vector2 pos,
            Vector2 box, Color? color = null, TextAnchor align = TextAnchor.MiddleCenter)
        {
            Text label = UiFactory.CreateText(name, root, text, size, align, color ?? UiFactory.Text, pos, box);
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        /// <summary>
        /// A dark edge behind a large numeral. The gold reads well on the dark table at a glance,
        /// but the thin strokes of a three-digit score fade over the leather grain without it.
        /// </summary>
        private static void SDAddScoreEdge(Text label, float thickness = 2f)
        {
            if (label == null) return;
            var edge = label.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.03f, 0.07f, 0.08f, .9f);
            edge.effectDistance = new Vector2(thickness, -thickness);
            edge.useGraphicAlpha = true;
        }

        /// <summary>
        /// Paints a score numeral as struck metal, then backs it with a thicker dark edge. The
        /// gradient mesh effect has to be attached before the outline so the outline copies the
        /// already-tinted glyphs and darkens only its own duplicates.
        /// </summary>
        private static void SDAddMetallicScore(Text label, float edge = 2.5f)
        {
            if (label == null) return;
            label.gameObject.AddComponent<SdMetallicTextEffect>();
            SDAddScoreEdge(label, edge);
        }

        /// <summary>
        /// The central numeral keeps 64 as its everyday base and breathes with the score: small
        /// hands sit at 60, a 100-point clash swells to 72 and the 130+ monsters reach 80, so the
        /// figure itself carries how much the hit is worth. Only the committed display scales;
        /// the live preview is pinned to the base, so the promise never dances in size.
        /// </summary>
        internal static int SDScoreFontFor(int score)
            => score < 50 ? 60 : score < 100 ? 64 : score < 130 ? 72 : 80;

        private Button SDButton(string name, Transform root, Vector2 pos, Vector2 size,
            string text, int fontSize, UnityAction action, bool primary = false)
        {
            Button button = UiFactory.CreateButton(name, root, size, pos, text, fontSize,
                () => { _sdInputFrame = Time.frameCount; action(); });
            if (primary) UiFactory.StylePrimary(button);
            else
            {
                button.GetComponent<Image>().color = SDInk;
                button.GetComponent<ButtonPalette>().Face = SDInk;
            }
            var gloss = button.transform.Find("Gloss");
            if (gloss != null) gloss.gameObject.SetActive(false);
            DiceYahzeeVisuals.StyleButton(button, primary);
            return button;
        }

        private static void SDSetButtonEnabled(Button button, bool enabled)
        {
            UiFactory.SetButtonEnabled(button, enabled);
            if (enabled) return;
            button.GetComponent<Image>().color = new Color32(29, 48, 53, 255);
            button.GetComponentInChildren<Text>().color = SDMuted;
        }

        private void SDBuildTitle()
        {
            var title = SDText("Title", _sdTitle, "DiceYahzee", 58, new Vector2(0, 220), new Vector2(900, 80), SDPositive);
            title.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? title.font;
            title.verticalOverflow = VerticalWrapMode.Overflow;
            title.rectTransform.sizeDelta = new Vector2(900, 136);
            SDText("Edition", _sdTitle, "D I C E   D U E L", 13, new Vector2(0, 166), new Vector2(500, 30), SDMuted);
            int[] sample = { 1, 1, 1, 4, 5 };
            for (int i = 0; i < sample.Length; i++)
            {
                var die = new DiceView(_sdTitle, i, 86, "ExampleDie" + i, new Color32(255, 249, 232, 255));
                die.SetPosition(new Vector2((i - 2) * 106, 67));
                die.SetValue(sample[i]); die.SetTint(DiceTint.Plain, i < 3); die.SetInteractable(false);
                DiceYahzeeVisuals.StyleDie(_sdTitle.Find("ExampleDie" + i));
            }
            var line = UiFactory.CreatePanel("Rule", _sdTitle, new Vector2(532, 1), new Vector2(0, -11), SDPositive);
            line.color = new Color(.8f,.66f,.42f,.3f);
            for (int i = 0; i < SDOpponentProfiles.Length; i++)
            {
                int captured = i;
                _sdOpponents[i] = SDButton("Opponent" + i, _sdTitle,
                    new Vector2((i - (SDOpponentProfiles.Length - 1) * .5f) * 176, -65), new Vector2(164, 52),
                    SDOpponentProfiles[i].Label, 18,
                    () => SDSelectOpponent(captured));
            }
            // Difficulty is only enemy health here, so the row states the pool the highlighted
            // button will actually start with.
            _sdOpponentNote = SDText("OpponentNote", _sdTitle, "", 15, new Vector2(0, -112), new Vector2(820, 30), SDMuted);
            SDButton("Start", _sdTitle, new Vector2(0, -162), new Vector2(320, 58), "対局開始", 23, SDStartMatch, true);
            SDButton("Practice", _sdTitle, new Vector2(-86, -241), new Vector2(148, 38), "練習", 17, SDOpenPractice);
            SDButton("Rules", _sdTitle, new Vector2(86, -241), new Vector2(148, 38), "遊び方", 17, SDOpenRules);
            // The build tag shares the bottom chrome row with the menu and the volume bar,
            // parked in their free span before the bar starts.
            SDText("Version", _sdTitle, "Ver.0.2", 14, new Vector2(280, -319),
                new Vector2(170, 26), SDMuted, TextAnchor.MiddleRight);
            SDSelectOpponent(0);
        }

        private void SDBuildBattle()
        {
            SDBuildTableMaterial(_sdBattle);
            var brand = SDText("Brand", _sdBattle, "DiceYahzee", 24, new Vector2(-492, 313), new Vector2(224, 34), SDRailInk, TextAnchor.MiddleLeft);
            brand.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? brand.font;
            brand.rectTransform.sizeDelta = new Vector2(224, 56);
            brand.verticalOverflow = VerticalWrapMode.Overflow;
            _sdRoundLabel = SDText("Round", _sdBattle, "", 15, new Vector2(0, 313), new Vector2(180, 34), SDRailMuted);
            SDButton("Rules", _sdBattle, new Vector2(535, 313), new Vector2(120, 34), "遊び方", 16, SDOpenRules);
            _sdNpcBar = SDBuildHpBar("NpcBar", _sdBattle, 245);
            _sdNpcHpLabel = SDBuildHpLabel("NpcHp", _sdNpcBar);
            _sdNpcReveal = UiFactory.CreateRect("NpcReveal", _sdBattle, new Vector2(260, 48), new Vector2(SDHandCenterX, 180));
            for (int i = 0; i < 5; i++)
            {
                var die = SDBuildTableDie(_sdNpcReveal, i, 44, "NpcDie" + i, new Vector2((i - 2) * 51, 0));
                die.SetInteractable(false); _sdNpcDice.Add(die);
            }
            _sdIntentLabel = SDText("Intent", _sdBattle, "", 17, new Vector2(0, 139), new Vector2(300, 29), SDRailInk);
            _sdNpcFormula = SDText("NpcFormula", _sdBattle, "", 13, new Vector2(0, 105), new Vector2(300, 22), SDRailMuted);
            _sdNpcScoreNumber = SDBuildScoreNumber("NpcScore", _sdBattle, new Vector2(0, 67), false);
            _sdToast = SDText("FormationToast", _sdBattle, "", 22, new Vector2(SDHandCenterX, -99), new Vector2(640, 38), SDPositive);
            var toastEdge = _sdToast.gameObject.AddComponent<Outline>();
            toastEdge.effectColor = new Color(0.04f, .09f, .10f, .95f);
            toastEdge.effectDistance = new Vector2(2, -2);
            SDBuildLiveScorecard(); SDBuildNpcScorecard(); SDDecorateNpcRail();
            for (int i = 0; i < 5; i++)
            {
                var die = SDBuildTableDie(_sdBattle, i, 112, "Die" + i, new Vector2(SDHandCenterX + (i - 2) * 132, SDPlayerDiceY));
                die.Clicked += SDOnDieClicked; _sdDice.Add(die);
                var pointer = _sdBattle.Find("Die" + i).gameObject.AddComponent<ScoreDuelPointer>();
                pointer.Initialize(this, i); SDBuildDiePointerSurface(_sdBattle.Find("Die" + i));
                DiceYahzeeVisuals.StyleDie(_sdBattle.Find("Die" + i));
                _sdDieLabels[i] = SDText("DieState" + i, _sdBattle, "", 1, Vector2.zero, Vector2.zero);
                _sdDieLabels[i].gameObject.SetActive(false);
            }
            var role = UiFactory.CreateRect("CurrentRole", _sdBattle, new Vector2(650, 108), new Vector2(SDHandCenterX, -144));
            _sdRoleLabel = SDText("Role", role, "", 21, new Vector2(0, 45), new Vector2(600, 32), SDRailInk);
            _sdBreakdown = SDText("Breakdown", role, "", 15, new Vector2(0, 18), new Vector2(600, 26), SDRailMuted);
            _sdRoleLabel.raycastTarget = false;
            _sdBreakdown.raycastTarget = false;
            _sdEffectLabel = SDText("Effect", role, "", 1, Vector2.zero, Vector2.zero);
            _sdEffectLabel.gameObject.SetActive(false);
            _sdInstruction = SDText("Instruction", _sdBattle, "", 1, Vector2.zero, Vector2.zero);
            _sdInstruction.gameObject.SetActive(false);
            _sdHit = SDText("Damage", _sdBattle, "", 1, Vector2.zero, Vector2.zero);
            _sdHit.gameObject.SetActive(false);
            _sdPlayerBar = SDBuildHpBar("PlayerBar", _sdBattle, SDPlayerHpY);
            _sdPlayerHpLabel = SDBuildHpLabel("PlayerHp", _sdPlayerBar);
            SDBuildHudDetails(); SDBuildHandEffects(); SDBuildGestureGuides(); SDBuildImpactPresentation();
            _sdNpcHpLabel.transform.SetAsLastSibling();
            _sdPlayerHpLabel.transform.SetAsLastSibling();
            _sdToast.transform.SetAsLastSibling();
        }

        private void SDBuildLiveScorecard()
        {
            var panel = UiFactory.CreatePanel("ScoreChoices", _sdBattle,
                new Vector2(SDRolePanelWidth, SDRolePanelHeight), new Vector2(SDRolePanelX, 20), Color.clear);
            panel.sprite = null;
            panel.raycastTarget = false;
            SDText("Heading", panel.transform, "役", 20, new Vector2(-71, 164), new Vector2(70, 28), SDRailInk, TextAnchor.MiddleLeft);
            SDIcon("WheelSymbol", panel.transform, "input-mouse-scroll", new Vector2(69, 164), 26, SDRailMuted);
            var headRule = UiFactory.CreatePanel("HeadRule", panel.transform, new Vector2(186, 1), new Vector2(0, 143), SDRailRule);
            headRule.raycastTarget = false;
            for (int i = 0; i < 9; i++)
            {
                int captured = i;
                _sdLiveCategories[i] = SDBuildRoleRow("LiveCategory" + i, panel.transform,
                    new Vector2(0, 128 - i * 37), (ScoreHandKind)i, () => SDSelectCategory(captured),
                    out _sdLiveValues[i], out _sdLiveMarkers[i]);
            }
            _sdSaveButton = SDBuildCarryButton(_sdBattle, "SaveHand", SDSaveHand, out _sdSaveSymbol);
            _sdSaveNote = SDText("SaveCost", _sdBattle, "", 1, Vector2.zero, Vector2.zero);
            _sdSaveNote.gameObject.SetActive(false);
            SDBuildCarryPresentation();
        }

        /// <summary>
        /// The enemy rail is built by its own partial, so its row is rebalanced here around the
        /// same columns the player rail uses: the role, its multiplier tag, the points, then the
        /// used check that closes the row. Only layout changes; the child names stay put.
        /// </summary>
        private void SDDecorateNpcRail()
        {
            Transform rail = _sdBattle.Find("NpcScorecard");
            if (rail == null) return;
            // The enemy rail is built by its own partial, so its dividers are widened here to the
            // span the player rail uses before its row is laid out again.
            foreach (Transform child in rail)
                if (child.name == "HeadRule" || child.name == "RowRule")
                    ((RectTransform)child).sizeDelta = new Vector2(186, 1);
            for (int i = 0; i < 9; i++)
            {
                Transform row = rail.Find("NpcCategory" + i);
                if (row == null) continue;
                var name = row.Find("Name").GetComponent<Text>();
                name.rectTransform.anchoredPosition = new Vector2(-23, 0);
                name.rectTransform.sizeDelta = new Vector2(116, 32);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                var value = row.Find("Value").GetComponent<Text>();
                value.rectTransform.anchoredPosition = new Vector2(68, 0);
                value.rectTransform.sizeDelta = new Vector2(30, 32);
                value.fontSize = 15;
                value.horizontalOverflow = HorizontalWrapMode.Overflow;
                SDText("Multiplier", row, SDRailMultiplier((ScoreHandKind)i), 11,
                    new Vector2(34, 0), new Vector2(34, 28), SDRailMuted, TextAnchor.MiddleRight)
                    .horizontalOverflow = HorizontalWrapMode.Overflow;
                // The check closes the row after the score: a column inside the name or the tag
                // would be crowded by the longest role name or by the widest multiplier.
                var used = row.Find("Used").GetComponent<Text>();
                used.rectTransform.anchoredPosition = new Vector2(89, 0);
                used.rectTransform.sizeDelta = new Vector2(12, 28);
                used.fontSize = 12;
                used.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        private Image SDBuildHpBar(string name, Transform root, float y)
        {
            Image track = UiFactory.CreatePanel(name, root, new Vector2(SDHpWidth, SDHpThickness), new Vector2(SDHandCenterX, y), new Color32(34, 48, 55, 255));
            Image fill = UiFactory.CreatePanel("Fill", track.transform, new Vector2(SDHpWidth, SDHpThickness), Vector2.zero, name == "NpcBar" ? SDEnemyColor : SDPlayerColor);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0, .5f);
            fill.rectTransform.pivot = new Vector2(0, .5f); fill.rectTransform.anchoredPosition = Vector2.zero;
            return fill;
        }

        private Text SDBuildHpLabel(string name, Image fill)
        {
            Text label = SDText(name, fill.transform.parent, "", 15, Vector2.zero,
                new Vector2(360, 30), new Color32(246, 242, 230, 255));
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var edge = label.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.06f, 0.10f, 0.13f, .85f);
            edge.effectDistance = new Vector2(1, -1);
            return label;
        }

        private void SDBuildResult()
        {
            _sdResultTitle = SDBuildResultTitle(_sdResult);
            _sdResultDetail = SDText("Detail", _sdResult, "", 24, new Vector2(0, 78), new Vector2(1000, 150));
            SDButton("Scorecard", _sdResult, new Vector2(0, -40), new Vector2(300, 46), "記録", 19, SDOpenSheet);
            SDBuildResultActions(_sdResult, SDStartMatch, SDBackToTitle, "Title", "タイトル", out _, out _);
        }

        private RectTransform SDModal(string name, string title, Vector2 size)
        {
            RectTransform root = UiFactory.CreateStretch(name, _sdRoot);
            Image dim = UiFactory.CreateStretchPanel("Dim", root, new Color(0, 0, 0, .75f));
            dim.raycastTarget = true;
            var panel = UiFactory.CreatePanel("Panel", root, size, Vector2.zero, SDInk);
            panel.raycastTarget = true;
            SDText("Title", root, title, 29, new Vector2(0, size.y * .5f - 48), new Vector2(size.x - 60, 50), SDPositive);
            root.gameObject.SetActive(false);
            return root;
        }

        private void SDBuildRules()
        {
            _sdRules = SDModal("SDRules", "遊び方", new Vector2(980, 660));
            Text steps = SDText("Steps", _sdRules,
                "① クリック・横ドラッグで残す目を選択します。\n" +
                "② 右クリックで最大2回振り直せます。\n" +
                "③ クリック・ホイールで役を選択します。\n" +
                "④ 下にドラッグしてリリースして攻撃。\n" +
                "⑤ それぞれの役は、一度使うと使えなくなります。",
                15, new Vector2(0, 192), new Vector2(900, 116), SDCream, TextAnchor.UpperLeft);
            steps.verticalOverflow = VerticalWrapMode.Overflow;
            // The scoring line is the one fact a player checks mid-match, so it is the loudest
            // single line on the panel rather than one sentence inside the instructions.
            SDText("Formula", _sdRules, "得点＝（20＋出目合計）×倍率、端数切捨て。", 22,
                new Vector2(0, 100), new Vector2(900, 36), SDScoreGold);
            SDText("DamageHeading", _sdRules, "ダメージ計算", 16, new Vector2(-330, 56), new Vector2(200, 26), SDPositive, TextAnchor.MiddleLeft);
            SDText("Damage", _sdRules,
                "ダメージ＝自分の得点−低い得点の半分。\n" +
                "例：自分56・相手70なら相手へ28・自分へ42。\n" +
                "チャンス：ダメージ計算式を反転させます。\n" +
                "ヤッツィー：次の振りなおし回数+1",
                14, new Vector2(0, -4), new Vector2(860, 84), SDCream, TextAnchor.UpperLeft);
            string[] names = ScoreRules.Names;
            string[] values = { "同じ目2つ · ×1.5", "ペア2組 · ×2", "同じ目3つ · ×2", "3つ＋2つ · ×2.5",
                "4連続 · ×2", "同じ目4つ · ×3", "5連続 · ×2.5", "同じ目5つ · 150点", "条件なし · ×1" };
            for (int i = 0; i < 9; i++)
            {
                int col = i / 5, row = i % 5;
                SDText("RuleKind" + i, _sdRules, names[i], 15,
                    new Vector2(-330 + col * 470, -72 - row * 28), new Vector2(175, 24), SDPositive, TextAnchor.MiddleLeft);
                SDText("RuleScore" + i, _sdRules, values[i], 15,
                    new Vector2(-122 + col * 470, -72 - row * 28), new Vector2(242, 24), SDMuted, TextAnchor.MiddleLeft);
            }
            SDButton("Practice", _sdRules, new Vector2(-165, -259), new Vector2(290, 46), "練習", 19, SDOpenPractice);
            SDButton("CloseRules", _sdRules, new Vector2(165, -259), new Vector2(290, 46), "戻る", 20, SDCloseModals, true);
        }

        private void SDBuildSheet()
        {
            _sdSheet = SDModal("SDScorecard", "使う役を変える", new Vector2(920, 638));
            _sdSheetTitle = _sdSheet.Find("Title").GetComponent<Text>();
            _sdSheetNote = SDText("Note", _sdSheet, "使った役と点数。9役を使い切ると再使用できます。", 17,
                new Vector2(0, 220), new Vector2(850, 32), SDMuted);
            for (int i = 0; i < 9; i++)
            {
                int captured = i;
                Button button = SDButton("Category" + i, _sdSheet, new Vector2(0, 169 - i * 42),
                    new Vector2(810, 38), "", 17, () => SDSelectCategory(captured));
                _sdCategoryButtons[i] = button;
                _sdCategoryNames[i] = SDText("Kind", button.transform, ScoreRules.Names[i], 18,
                    new Vector2(-195, 0), new Vector2(380, 36), null, TextAnchor.MiddleLeft);
                _sdCategoryValues[i] = SDText("Score", button.transform, "", 17,
                    new Vector2(184, 0), new Vector2(385, 36), SDMuted, TextAnchor.MiddleRight);
            }
            SDButton("CloseSheet", _sdSheet, new Vector2(0, -263), new Vector2(290, 46), "戻る", 20, SDCloseModals);
        }

        private void SDBuildMenu()
        {
            _sdMenu = SDModal("SDMenu", "メニュー", new Vector2(700, 550));
            SDButton("Continue", _sdMenu, new Vector2(0, 149), new Vector2(390, 48), "戻る", 20, SDCloseModals, true);
            SDButton("Rules", _sdMenu, new Vector2(0, 86), new Vector2(390, 46), "遊び方", 19, SDOpenRules);
            SDButton("Title", _sdMenu, new Vector2(0, 25), new Vector2(390, 46), "タイトル", 19, SDBackToTitle);
            SDButton("Credits", _sdMenu, new Vector2(0, -36), new Vector2(390, 46), "使用素材・クレジット", 19, SDOpenCredits);
            Button motion = SDButton("Motion", _sdMenu, new Vector2(0, -97), new Vector2(390, 46), "", 19, SDToggleMotion);
            _sdMotionLabel = motion.GetComponentInChildren<Text>();
            SDRefreshMotionLabel();
        }

        private void SDBuildCredits()
        {
            _sdCredits = SDModal("SDCredits", "使用素材・クレジット", new Vector2(980, 620));
            SDText("CreditsBody", _sdCredits,
                "テーブルの革：Leather White / Rob Tuytel / Poly Haven / CC0\npolyhaven.com/a/leather_white\n\n"
                + "アイコン：Lorc / game-icons.net / CC BY 3.0\ncrystal-ball, edged-shield, fire-silhouette, crossed-swords,\nburning-embers, lightning-helix, comet-spark, crowned-explosion\n表示時の色変更・縮小あり。\n\n"
                + "Kenney / UI Pack・Input Prompts・Game Icons・Fantasy UI Borders / CC0\nAoboshi One・Noto Sans JP / SIL OFL 1.1\n\n"
                + "音声：generated with StepFun\n(stepaudio-3-gen-preview / stepaudio-3-music-preview)\nライセンス全文と取得元：同梱のLicenses、ASSET_LICENSES.md",
                18, new Vector2(0, -3), new Vector2(870, 425), null, TextAnchor.UpperLeft);
            SDButton("CloseCredits", _sdCredits, new Vector2(0, -254), new Vector2(290, 46), "戻る", 20, SDCloseModals);
        }

        private void SDBuildPractice()
        {
            _sdPractice = SDModal("SDPractice", "練習", new Vector2(980, 600));
            SDText("FixedExample", _sdPractice, "固定出目", 15,
                new Vector2(0, 188), new Vector2(880, 32), SDMuted);
            for (int i = 0; i < 5; i++)
            {
                var die = new DiceView(_sdPractice, i, 94, "PracticeDie" + i, new Color32(255, 249, 232, 255));
                die.SetPosition(new Vector2((i - 2) * 120, 74));
                die.Clicked += SDOnPracticeDie;
                _sdPracticeDice.Add(die);
                var pointer = _sdPractice.Find("PracticeDie" + i).gameObject.AddComponent<ScoreDuelPointer>();
                pointer.Initialize(this, i, true);
                SDBuildDiePointerSurface(_sdPractice.Find("PracticeDie" + i));
                DiceYahzeeVisuals.StyleDie(_sdPractice.Find("PracticeDie" + i));
            }
            _sdPracticeFeedback = SDText("PracticeFeedback", _sdPractice, "", 22,
                new Vector2(0, -47), new Vector2(880, 128));
            SDBuildPracticeGestureGuide();
            _sdPracticeFinishButton = SDButton("PracticeFinish", _sdPractice, new Vector2(0, -155),
                new Vector2(380, 56), "対局を始める", 22, SDPracticeFinish, true);
            SDButton("ClosePractice", _sdPractice, new Vector2(0, -239), new Vector2(260, 44), "戻る", 18, SDCloseModals);
        }

        private void SDOpenPractice()
        {
            _sdPracticeFromBattle = _sdEngine != null;
            SDCloseModals();
            _sdPractice.gameObject.SetActive(true);
            _sdPracticeHand = new[] { 1, 1, 2, 5, 6 };
            Array.Clear(_sdPracticeHeld, 0, _sdPracticeHeld.Length);
            _sdPracticeStep = 0;
            _sdPracticeRolling = false;
            for (int i = 0; i < 5; i++)
            {
                _sdPracticeDice[i].SetHidden();
                _sdPracticeDice[i].SetValue(_sdPracticeHand[i]);
                _sdPracticeDice[i].SetTint(DiceTint.Plain, false);
                _sdPracticeDice[i].SetInteractable(true);
            }
            _sdPracticeFinishButton.gameObject.SetActive(false);
            _sdPracticeFinishButton.GetComponentInChildren<Text>().text = _sdPracticeFromBattle ? "対局へ戻る" : "対局を始める";
            SDRefreshPracticeSelection();
            SDRefresh();
        }

        private void SDOnPracticeDie(int index)
        {
            if (!_sdPractice.gameObject.activeSelf || _sdPracticeStep != 0 || index < 0 || index >= 5) return;
            _sdPracticeHeld[index] = !_sdPracticeHeld[index];
            _sdPracticeDice[index].SetTint(DiceTint.Plain, _sdPracticeHeld[index]);
            if (!_sdReducedMotion) _sdPracticeDice[index].Punch();
            GameAudio.Play("die_keep");
            SDRefreshPracticeSelection();
        }

        private void SDRefreshPracticeSelection()
        {
            bool correct = _sdPracticeHeld[0] && _sdPracticeHeld[1] && !_sdPracticeHeld[2] &&
                !_sdPracticeHeld[3] && !_sdPracticeHeld[4];
            var offer = ScoreRules.Evaluate(ScoreHandKind.Pair, _sdPracticeHand);
            _sdPracticeFeedback.text = ScoreRules.Name(offer.Kind) + "\n" + SDCalculation(offer.Kind, _sdPracticeHand) + " = " + offer.Score;
            _sdPracticeGestureText.text = "";
        }

        private void SDPracticeReroll()
        {
            if (!_sdPractice.gameObject.activeSelf || _sdPracticeStep != 0 || !SDPracticeCorrectSelection) return;
            _sdPracticeStep = 1;
            _sdPracticeRolling = true;
            _sdPracticeHand = new[] { 1, 1, 1, 5, 6 };
            _sdPracticeFeedback.text = "—";
            for (int i = 0; i < 5; i++)
            {
                if (!_sdPracticeHeld[i]) _sdPracticeDice[i].BeginRoll(i * RollStagger, RollScramble);
                _sdPracticeDice[i].SetValue(_sdPracticeHand[i]);
                _sdPracticeDice[i].SetInteractable(false);
            }
            GameAudio.Play("dice_roll");
        }

        private void SDPracticeCommit()
        {
            if (!_sdPractice.gameObject.activeSelf || _sdPracticeStep != 1 || _sdPracticeRolling) return;
            _sdPracticeStep = 2;
            foreach (DiceView die in _sdPracticeDice) die.SetInteractable(false);
            _sdPracticeFinishButton.gameObject.SetActive(true);
            _sdPracticeFeedback.text = "✓  " + ScoreRules.Name(ScoreHandKind.Three) + "\n" + SDCalculation(ScoreHandKind.Three, _sdPracticeHand) + " = " + ScoreRules.Evaluate(ScoreHandKind.Three, _sdPracticeHand).Score;
            GameAudio.Play("commit");
            SDRefreshScope();
        }

        private void SDPracticeFinish()
        {
            if (_sdPracticeStep != 2) return;
            if (_sdPracticeFromBattle) SDCloseModals(); else SDStartMatch();
        }

        private void SDSelectOpponent(int index)
        {
            if (index < 0 || index >= SDOpponentProfiles.Length) return;
            _sdOpponentIndex = index;
            _sdOpponent = SDOpponentProfiles[index].Kind;
            for (int i = 0; i < _sdOpponents.Length; i++)
            {
                UiFactory.StyleSelected(_sdOpponents[i], i == index);
                if (i != index)
                {
                    _sdOpponents[i].GetComponent<Image>().color = SDInk;
                    _sdOpponents[i].GetComponent<ButtonPalette>().Face = SDInk;
                }
            }
            _sdOpponentNote.text = "相手の体力 " + SDOpponentProfiles[index].NpcHp +
                "　／　全難易度が上級の読みで最善手を比べる。";
        }

        private void SDStartMatch()
        {
            SDCloseModals();
            SDResetSingleScorePulses();
            _sdEngine = new ScoreDuel(FreshSeed(), _sdOpponent, SDOpponentProfiles[_sdOpponentIndex].NpcHp);
            _sdEngine.Roll();
            _sdTitle.gameObject.SetActive(false);
            _sdResult.gameObject.SetActive(false);
            _sdBattle.gameObject.SetActive(true);
            SDResetTurn();
            SDBeginThrow(null);
            SDRefresh();
        }

        private void SDResetTurn()
        {
            SDResetCarryPresentation();
            SDResetImpactPresentation();
            SDCancelPointer();
            SDResetHandEffects();
            _sdAttackAge = -1;
            _sdCommitted = false;
            _sdRolling = false;
            _sdSaveArmed = false;
            _sdSelectedManually = false;
            _sdNpcReveal.gameObject.SetActive(true);
            SDStartNpcReplay();
            _sdLastAnnouncedKind = null;
            _sdLastAnnouncedScore = 0;
            _sdToastAge = -1;
            _sdFormationMask = 0;
            _sdToast.text = "";
            _sdHitAge = -1;
            _sdHit.gameObject.SetActive(false);
            _sdInstruction.gameObject.SetActive(false);
            _sdHpAge = -1;
            SDSetHp(_sdEngine.PlayerHp, _sdEngine.NpcHp);
            SDShowHpDeltas(0);
            SDEvaluateHand(false);
        }

        private void SDBeginThrow(bool[] throwing)
        {
            SDResetHandEffects();
            _sdRolling = true;
            _sdRoleLabel.text = "";
            _sdEffectLabel.text = "";
            _sdToast.text = "";
            _sdToastAge = -1;
            _sdFormationMask = 0;
            // Start the scramble before assigning the true faces. Neither the result panel nor
            // a die can expose the new hand before the throw has landed.
            StartThrow(_sdDice, throwing);
            for (int i = 0; i < _sdDice.Count; i++) _sdDice[i].SetValue(_sdEngine.PlayerDice[i]);
            GameAudio.Play("dice_roll");
            SDClearSelection();
        }

        private bool SDAnyRolling()
        {
            for (int i = 0; i < _sdDice.Count; i++) if (_sdDice[i].IsRolling) return true;
            return false;
        }

        private void SDOnDieClicked(int index)
        {
            if (_sdDragging || SDModalOpen || _sdEngine == null || _sdRolling || _sdCommitted || _sdNpcReplayAge >= 0 || _sdEngine.RerollsLeft == 0) return;
            SDResetHandEffects();
            _sdEngine.ToggleHeld(index);
            GameAudio.Play("die_keep");
            if (!_sdReducedMotion) _sdDice[index].Punch();
            SDRefresh();
        }

        private void SDReroll()
        {
            if (_sdDragging || SDModalOpen || _sdEngine == null || _sdRolling || _sdCommitted || _sdNpcReplayAge >= 0 || _sdEngine.RerollsLeft == 0) return;
            bool[] throwing = new bool[5];
            bool any = false;
            for (int i = 0; i < 5; i++) { throwing[i] = !_sdEngine.Held[i]; any |= throwing[i]; }
            if (!any)
            {
                _sdInstruction.text = "";
                return;
            }
            _sdEngine.Reroll();
            SDBeginThrow(throwing);
            SDRefresh();
        }

        private void SDEvaluateHand(bool announce)
        {
            // Suggest the strongest unused result until the player chooses a target. An
            // explicit choice survives rerolls; the formation notice still follows real dice.
            bool found = SDTryPreferredOffer(_sdEngine.PlayerDice, _sdEngine.PlayerUsed, false, out ScoreOffer best);
            bool foundFormation = SDTryPreferredOffer(_sdEngine.PlayerDice, _sdEngine.PlayerUsed, true, out ScoreOffer formation);
            if (!found) return;
            if (!_sdSelectedManually) _sdSelectedKind = best.Kind;
            if (!announce || !foundFormation) return;
            if (_sdLastAnnouncedKind == formation.Kind && formation.Score <= _sdLastAnnouncedScore) return;
            _sdLastAnnouncedKind = formation.Kind;
            _sdLastAnnouncedScore = formation.Score;
            _sdFormationCount++;
            _sdFormationMask = formation.Mask;
            _sdToastAge = 0;
            _sdToast.text = ScoreRules.Name(formation.Kind) + "  " + SDMultiplier(formation.Kind);
            _sdToast.color = SDPositive;
            GameAudio.Play("formation");
            SDPlayHandEffects(formation);
        }

        /// <summary>
        /// After the player's rerolls settle, move the selection to the strongest formed hand the
        /// new dice make when it beats what is selected - a reroll that lands a better hand may
        /// promote even a manual pick, and Chance is never the beneficiary: it reverses damage
        /// rather than scoring.
        /// </summary>
        private void SDPromoteFormedSelection()
        {
            if (_sdEngine == null) return;
            bool found = SDTryPreferredOffer(_sdEngine.PlayerDice, _sdEngine.PlayerUsed, true, out ScoreOffer best);
            int current = ScoreRules.Evaluate(_sdSelectedKind, _sdEngine.PlayerDice).Score;
            if (!found || best.Score <= current) return;
            _sdSelectedKind = best.Kind;
            // The suggestion owns the preview again after taking the wheel back.
            _sdSelectedManually = false;
        }

        private void SDCommit()
        {
            if (!SDCanPlan || _sdDragging) return;
            _sdPlayerHpFrom = _sdEngine.PlayerHp;
            _sdNpcHpFrom = _sdEngine.NpcHp;
            ScoreRound round = _sdSaveArmed ? _sdEngine.SaveHand(_sdSelectedKind) : _sdEngine.Commit(_sdSelectedKind);
            SDPresentRound(round);
        }

        private void SDSaveHand()
        {
            if (!SDCanPlan || _sdDragging || _sdEngine.SavedHandUsed) return;
            _sdSaveArmed = !_sdSaveArmed;
            SDBeginCarryToggle();
            GameAudio.Play("die_keep");
            SDRefresh();
        }

        private void SDPresentRound(ScoreRound round)
        {
            _sdSaveArmed = false;
            _sdCommitted = true;
            for (int i = 0; i < 5; i++) _sdNpcDice[i].SetValue(round.NpcDice[i]);
            _sdNpcReveal.gameObject.SetActive(true);
            _sdHpAge = 0;
            _sdToastAge = -1;
            _sdToast.text = "";
            _sdFormationMask = 0;
            _sdHitAge = 0;
            _sdHit.gameObject.SetActive(false);
            _sdInstruction.gameObject.SetActive(false);
            _sdHit.text = round.DamageToNpc > 0 && round.DamageToPlayer > 0 ?
                "相手へ " + round.DamageToNpc + " ／ あなたへ " + round.DamageToPlayer : round.DamageToNpc > 0 ? "相手に " + round.DamageToNpc + " ダメージ" :
                round.DamageToPlayer > 0 ? "あなたに " + round.DamageToPlayer + " ダメージ" : "同点。ダメージなし";
            _sdHit.color = round.DamageToNpc > 0 ? SDPositive : round.DamageToPlayer > 0 ? SDNegative : SDMuted;
            GameAudio.Play("commit");
            SDStartAttackMotion();
            SDClearSelection();
            SDRefresh();
        }

        private void SDAdvance()
        {
            if (SDModalOpen || _sdEngine == null || !_sdCommitted || _sdAttackAge >= 0) return;
            if (_sdEngine.Outcome != null)
            {
                SDShowResult();
                return;
            }
            _sdEngine.NextRound();
            bool restored = _sdEngine.Phase != MatchPhase.AwaitingRoll;
            if (!restored) _sdEngine.Roll();
            SDResetTurn();
            if (restored)
            {
                for (int i = 0; i < 5; i++) _sdDice[i].SetValue(_sdEngine.PlayerDice[i]);
                SDEvaluateHand(true);
                _sdToast.text = "";
            }
            else SDBeginThrow(null);
            SDRefresh();
            if (restored) SDBeginCarryReturn();
        }

        private void SDShowResult()
        {
            SDResetImpactPresentation();
            if (_sdEngine.Outcome.Result != MatchResult.Draw)
                GameAudio.Play(_sdEngine.Outcome.Result == MatchResult.PlayerWin ? "win" : "lose");
            SDSetHp(_sdEngine.PlayerHp, _sdEngine.NpcHp);
            SDShowHpDeltas(0);
            _sdBattle.gameObject.SetActive(false);
            _sdResult.gameObject.SetActive(true);
            bool draw = _sdEngine.Outcome.Result == MatchResult.Draw;
            _sdResultTitle.text = draw ? "奇跡" : MatchResults.DisplayName(_sdEngine.Outcome.Result);
            _sdResultTitle.fontSize = draw ? 112 : 48;
            _sdResultTitle.color = draw ? SDMiracle : _sdEngine.Outcome.Result == MatchResult.NpcWin ? SDNegative : SDPositive;
            bool bothKo = _sdEngine.PlayerHp == 0 && _sdEngine.NpcHp == 0;
            var last = _sdEngine.LastRound;
            _sdResultDetail.text = bothKo ? "同時KO\nあなた  " + last.PlayerRawHpAfter + "     相手  " + last.NpcRawHpAfter + "\nROUND " + _sdEngine.History.Count :
                "あなた  " + _sdEngine.PlayerHp + "     相手  " + _sdEngine.NpcHp + "\n\nROUND " + _sdEngine.History.Count;
            SDClearSelection();
            SDRefreshScope();
        }

        private void SDBackToTitle()
        {
            SDResetCarryPresentation();
            SDResetImpactPresentation();
            _sdNpcReplayAge = -1;
            SDCancelPointer();
            SDResetHandEffects();
            _sdAttackAge = -1;
            SDCloseModals();
            _sdEngine = null;
            _sdRolling = false;
            _sdCommitted = false;
            _sdSaveArmed = false;
            _sdTitle.gameObject.SetActive(true);
            _sdBattle.gameObject.SetActive(false);
            _sdResult.gameObject.SetActive(false);
            SDClearSelection();
            SDRefreshScope();
        }

        private void SDRefresh()
        {
            if (_sdEngine == null) { SDRefreshScope(); return; }
            _sdRoundLabel.text = "ROUND " + _sdEngine.Round.ToString("00");
            var intent = _sdEngine.Intent;
            bool npcRolling = _sdNpcReplayAge >= 0;
            foreach (DiceView die in _sdNpcDice) npcRolling |= die.IsRolling;
            _sdIntentLabel.text = npcRolling ? "" : ScoreRules.Name(intent.Kind);
            _sdNpcScoreNumber.text = npcRolling ? "—" : intent.Score.ToString();
            SDObserveScorePulse(_sdNpcScoreNumber, ref _sdEnemyScorePulse, npcRolling ? -1 : intent.Score);
            _sdNpcFormula.text = npcRolling ? "" : SDCalculation(intent.Kind, SDNpcHand());
            if (!npcRolling)
            {
                int mask = ScoreRules.Evaluate(intent.Kind, SDNpcHand()).Mask;
                for (int i = 0; i < 5; i++) _sdNpcDice[i].SetTint(DiceTint.Plain, (mask & (1 << i)) != 0);
            }
            if (_sdHpAge < 0) SDSetHp(_sdEngine.PlayerHp, _sdEngine.NpcHp);
            for (int i = 0; i < 5; i++)
            {
                _sdDice[i].SetValue(_sdEngine.PlayerDice[i]);
                // The rule carries the whole hand - every die and every hold goes to the next
                // turn - so with the save armed every die rings kept: the row must not promise a
                // smaller selection (held dice alone) than the save actually makes.
                _sdDice[i].SetTint(DiceTint.Plain, !_sdCommitted && (_sdEngine.Held[i] || _sdSaveArmed));
                _sdDice[i].SetInteractable(!_sdRolling && !_sdCommitted && !SDModalOpen);
                _sdDieLabels[i].text = "";
            }
            SDSetButtonEnabled(_sdSaveButton, !_sdRolling && !_sdCommitted && !SDModalOpen && !_sdEngine.SavedHandUsed);
            _sdSaveButton.GetComponentInChildren<Text>().text = "";
            _sdSaveButton.transform.Find("SaveCount").GetComponent<Text>().text = "";
            _sdSaveNote.text = ""; _sdInstruction.text = ""; _sdEffectLabel.text = "";
            SDRefreshLiveScorecard(); SDRefreshNpcScorecard(npcRolling); SDRefreshScope(); SDRefreshHud();
            _sdRoleLabel.enabled = _sdToastAge < 0;
            if (_sdRolling)
            {
                _sdRoleLabel.text = ""; _sdScoreNumber.text = "—"; _sdBreakdown.text = "";
                _sdScoreNumber.fontSize = 64;
                SDObserveScorePulse(_sdScoreNumber, ref _sdOwnScorePulse, -1);
                return;
            }
            ScoreHandKind kind = _sdCommitted ? _sdEngine.LastRound.PlayerKind : _sdSelectedKind;
            int[] dice = _sdCommitted ? _sdEngine.LastRound.PlayerDice : _sdEngine.PlayerDice;
            int score = _sdCommitted ? _sdEngine.LastRound.PlayerScore : _sdEngine.Forecast(kind).Score;
            _sdRoleLabel.text = ScoreRules.Name(kind);
            _sdScoreNumber.text = score.ToString();
            _sdScoreNumber.fontSize = _sdCommitted ? SDScoreFontFor(score) : 64;
            SDObserveScorePulse(_sdScoreNumber, ref _sdOwnScorePulse, score);
            _sdBreakdown.text = SDCalculation(kind, dice);
        }

        private static int SDTotal(System.Collections.Generic.IReadOnlyList<int> dice)
        { int total = 0; for (int i = 0; i < dice.Count; i++) total += dice[i]; return total; }
        private int[] SDNpcHand()
        { var dice = new int[5]; for (int i = 0; i < 5; i++) dice[i] = _sdEngine.PlannedNpcDice[i]; return dice; }

        private void SDRefreshLiveScorecard()
        {
            foreach (ScoreOffer offer in _sdEngine.Offers)
            {
                int i = (int)offer.Kind;
                bool selected = offer.Kind == _sdSelectedKind;
                bool enabled = offer.IsAvailable && !_sdRolling && !_sdCommitted && !SDModalOpen;
                var button = _sdLiveCategories[i]; button.interactable = enabled;
                button.GetComponent<Image>().color = selected ? new Color32(193, 157, 95, 255) : new Color(1,1,1,.005f);
                Text name = button.transform.Find("Name").GetComponent<Text>();
                name.color = selected ? SDPaperInk : offer.IsAvailable ? SDRailInk : SDRailUsed;
                _sdLiveValues[i].text = !offer.IsAvailable ? "—" : _sdRolling ? "·" : offer.Score.ToString();
                _sdLiveValues[i].color = selected ? SDPaperInk : offer.IsAvailable ? SDRailInk : SDRailUsed;
                _sdLiveMarkers[i].gameObject.SetActive(selected && !_sdRolling);
            }
        }

        private void SDSetHp(float player, float npc)
        {
            if (_sdEngine == null) return;
            _sdPlayerHpShown = player;
            _sdNpcHpShown = npc;
            SDSetTableHp(_sdPlayerBar, player, ScoreDuel.StartingHp);
            SDSetTableHp(_sdNpcBar, npc, _sdEngine.NpcMaxHp);
            SDApplyHpLines();
        }

        private void SDOpenSheet()
        {
            if (_sdEngine == null || _sdRolling) return;
            SDCloseModals();
            _sdSheetTitle.text = _sdCommitted ? "使った役" : "使う役を変える";
            _sdSheetNote.text = "各役は1回。9役を使い切ると次のターンに再使用できます。";
            foreach (ScoreOffer offer in _sdEngine.Offers)
            {
                int index = (int)offer.Kind;
                bool used = !offer.IsAvailable;
                int oldScore = 0;
                foreach (ScoreRound round in _sdEngine.History)
                    if (round.PlayerKind == offer.Kind) oldScore = round.PlayerScore;
                _sdCategoryNames[index].text = ScoreRules.Name(offer.Kind);
                _sdCategoryValues[index].text = used ? oldScore + "点 · 使用済み" : _sdCommitted ? "未使用" :
                    offer.Score + "点" + (offer.IsFormed ? "" : " · 未成立");
                _sdCategoryNames[index].color = used ? SDRailUsed : UiFactory.Text;
                _sdCategoryValues[index].color = used ? SDRailUsed : offer.Kind == _sdSelectedKind ? SDPositive : UiFactory.Text;
                SDSetButtonEnabled(_sdCategoryButtons[index], !used && !_sdCommitted);
            }
            _sdSheet.gameObject.SetActive(true);
            SDRefresh();
            SDClearSelection();
        }

        private void SDSelectCategory(int index)
        {
            if (SDModalOpen || _sdRolling || _sdEngine == null || _sdCommitted || index < 0 || index >= 9 || _sdEngine.PlayerUsed[index]) return;
            _sdSelectedKind = (ScoreHandKind)index;
            _sdSelectedManually = true;
            if (_sdSelectedKind == ScoreHandKind.Chance && _sdFxActive) SDResetHandEffects();
            GameAudio.Play("die_keep");
            SDRefresh();
        }

        private void SDOpenRules() { SDCancelPointer(); SDResetHandEffects(); SDCloseModals(); _sdRules.gameObject.SetActive(true); SDRefresh(); }
        private void SDOpenMenu() { SDCancelPointer(); SDResetHandEffects(); SDCloseModals(); _sdMenu.gameObject.SetActive(true); SDRefresh(); }
        private void SDOpenCredits() { SDCloseModals(); _sdCredits.gameObject.SetActive(true); SDRefresh(); }
        private void SDCloseModals()
        {
            SDCancelPracticePointer();
            if (_sdRules != null) _sdRules.gameObject.SetActive(false);
            if (_sdSheet != null) _sdSheet.gameObject.SetActive(false);
            if (_sdMenu != null) _sdMenu.gameObject.SetActive(false);
            if (_sdCredits != null) _sdCredits.gameObject.SetActive(false);
            if (_sdPractice != null) _sdPractice.gameObject.SetActive(false);
            _sdPracticeRolling = false;
            SDClearSelection();
            SDRefreshScope();
            if (_sdEngine != null) SDRefresh();
        }
        private void SDToggleMotion()
        {
            _sdReducedMotion = !_sdReducedMotion;
            PlayerPrefs.SetInt("arcane.reducedMotion", _sdReducedMotion ? 1 : 0);
            SDRefreshMotionLabel();
        }
        private void SDRefreshMotionLabel() => _sdMotionLabel.text = "成立時の動き：" + (_sdReducedMotion ? "控えめ" : "通常");
        private static void SDClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void UpdateScoreDuelPresentation()
        {
            float dt = Time.deltaTime;
            SDRefreshSoundSymbol();
            foreach (DiceView die in _sdDice) die.Tick(dt);
            SDTickNpcReplay(dt);
            bool npcMoving = false;
            foreach (DiceView die in _sdNpcDice) { npcMoving |= die.IsRolling; die.Tick(dt); }
            if (npcMoving) SDRefresh();
            SDTickHandEffects(dt);
            SDTickChanceGlow(dt);
            SDTickSingleScorePulses(dt);
            SDRefreshDieLabelsVisibility();
            SDTickAttackMotion(dt);
            SDTickCarryReturn(dt);
            SDTickCarryToggle(dt);
            SDTickGestureGuides(dt);
            SDTickRollPipPulse(dt);
            foreach (DiceView die in _sdPracticeDice) die.Tick(dt);
            if (_sdPracticeRolling)
            {
                bool rolling = false;
                foreach (DiceView die in _sdPracticeDice) rolling |= die.IsRolling;
                if (!rolling)
                {
                    _sdPracticeRolling = false;
                    _sdPracticeFeedback.text = ScoreRules.Name(ScoreHandKind.Three) + "\n" + SDCalculation(ScoreHandKind.Three, _sdPracticeHand) + " = " + ScoreRules.Evaluate(ScoreHandKind.Three, _sdPracticeHand).Score;
                    foreach (DiceView die in _sdPracticeDice) die.SetInteractable(true);
                    _sdPracticeGestureText.text = "";
                    GameAudio.Play("formation");
                    if (!_sdReducedMotion) for (int i = 0; i < 3; i++) _sdPracticeDice[i].Punch();
                    SDRefreshScope();
                }
            }
            if (_sdRolling && !SDAnyRolling() && !SDModalOpen)
            {
                _sdRolling = false;
                SDEvaluateHand(true);
                SDPromoteFormedSelection();
                SDRefresh();
            }
            if (_sdToastAge >= 0)
            {
                _sdToastAge += dt;
                float fraction = _sdToastAge / SDToastLife;
                _sdToast.rectTransform.localScale = Vector3.one * (_sdReducedMotion ? 1 :
                    Mathf.Lerp(1.12f, 1, Mathf.Clamp01(fraction * 5)));
                Color color = SDPositive;
                color.a = fraction < .68f ? 1 : Mathf.Clamp01((1 - fraction) / .32f);
                _sdToast.color = color;
                if (fraction >= 1)
                {
                    _sdToastAge = -1;
                    _sdToast.text = "";
                    _sdFormationMask = 0;
                    SDRefresh();
                }
            }
            if (_sdHitAge >= 0)
            {
                _sdHitAge += dt;
                _sdHit.rectTransform.localScale = Vector3.one * (_sdReducedMotion ? 1 : Mathf.Lerp(1.17f, 1, Mathf.Clamp01(_sdHitAge * 4)));
            }
            if (_sdHpAge >= 0 && _sdEngine != null && !SDModalOpen && (_sdAttackAge < 0 || _sdAttackAge >= SDImpactContactDelay))
            {
                _sdHpAge += dt;
                float t = Mathf.Clamp01(_sdHpAge / .85f);
                float eased = 1 - (1 - t) * (1 - t);
                SDSetHp(Mathf.Lerp(_sdPlayerHpFrom, _sdEngine.PlayerHp, eased), Mathf.Lerp(_sdNpcHpFrom, _sdEngine.NpcHp, eased));
                SDShowHpDeltas(1 - t);
                if (t >= 1) { _sdHpAge = -1; SDShowHpDeltas(0); }
            }
            if (Input.GetKeyDown(KeyCode.Escape)) SDHandleShortcut(KeyCode.Escape);
            if (Input.GetMouseButtonDown(0)) SDHandleTableClick(Input.mousePosition);
            if (Input.GetMouseButtonDown(1)) SDHandleRightClick();
            SDHandleWheel(Input.mouseScrollDelta.y);
            if (Input.GetKeyDown(KeyCode.Tab)) SDHandleShortcut(KeyCode.Tab);
            if (Input.GetKeyDown(KeyCode.Space)) SDHandleShortcut(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.Return)) SDHandleShortcut(KeyCode.Return);
            if (Input.GetKeyDown(KeyCode.KeypadEnter)) SDHandleShortcut(KeyCode.KeypadEnter);
            for (int i = 0; i < 5; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SDHandleShortcut((KeyCode)((int)KeyCode.Alpha1 + i));
        }

        private void SDRefreshDieLabelsVisibility()
        {
            bool enlarged = _sdDragAiming || _sdAttackAge >= 0 || (_sdFxActive && (_sdFxLastKind == ScoreHandKind.Five || _sdFxLastKind == ScoreHandKind.FullHouse));
            foreach (Text label in _sdDieLabels) label.gameObject.SetActive(false);
            for (int i = 0; i < 5; i++) _sdHoldMarks[i].gameObject.SetActive(!enlarged && SDCanPlan && _sdEngine.Held[i]);
        }

        private void SDHandleShortcut(KeyCode key)
        {
            if (key == KeyCode.Escape)
            {
                if (SDModalOpen) SDCloseModals(); else SDOpenMenu();
                _sdInputFrame = Time.frameCount;
                return;
            }
            if (key == KeyCode.Tab) { SDSelectNextButton(); return; }
            if (_sdDragging || SDModalOpen || !_sdBattle.gameObject.activeSelf || _sdInputFrame == Time.frameCount) return;
            if ((int)key >= (int)KeyCode.Alpha1 && (int)key <= (int)KeyCode.Alpha5)
            {
                SDClearSelection();
                SDOnDieClicked((int)key - (int)KeyCode.Alpha1);
            }
            else if (key == KeyCode.Space) SDReroll();
            else if ((key == KeyCode.Return || key == KeyCode.KeypadEnter) && !EventSystemHasSelection())
            {
                if (_sdCommitted) SDAdvance(); else SDCommit();
            }
        }

        private void SDSelectNextButton()
        {
            if (EventSystem.current == null) return;
            Transform scope = _sdPractice.gameObject.activeSelf ? _sdPractice : _sdRules.gameObject.activeSelf ? _sdRules : _sdSheet.gameObject.activeSelf ? _sdSheet :
                _sdMenu.gameObject.activeSelf ? _sdMenu : _sdCredits.gameObject.activeSelf ? _sdCredits :
                _sdTitle.gameObject.activeSelf ? _sdTitle : _sdBattle.gameObject.activeSelf ? _sdBattle : _sdResult;
            var available = new List<Button>();
            foreach (Button button in scope.GetComponentsInChildren<Button>())
                if (button.IsInteractable() && button.gameObject.activeInHierarchy) available.Add(button);
            if (available.Count == 0) return;
            int current = available.FindIndex(b => b.gameObject == EventSystem.current.currentSelectedGameObject);
            EventSystem.current.SetSelectedGameObject(available[(current + 1) % available.Count].gameObject);
        }

        private void SDRefreshScope()
        {
            if (_sdTitle == null) return;
            bool modal = SDModalOpen;
            foreach (RectTransform pane in new[] { _sdTitle, _sdBattle, _sdResult })
            {
                CanvasGroup group = pane.GetComponent<CanvasGroup>();
                group.interactable = !modal;
                group.blocksRaycasts = !modal;
            }
            Transform scope = _sdPractice != null && _sdPractice.gameObject.activeSelf ? _sdPractice :
                _sdRules != null && _sdRules.gameObject.activeSelf ? _sdRules :
                _sdSheet != null && _sdSheet.gameObject.activeSelf ? _sdSheet :
                _sdMenu != null && _sdMenu.gameObject.activeSelf ? _sdMenu :
                _sdCredits != null && _sdCredits.gameObject.activeSelf ? _sdCredits :
                _sdTitle.gameObject.activeSelf ? _sdTitle : _sdBattle.gameObject.activeSelf ? _sdBattle : _sdResult;
            var available = new List<Button>();
            foreach (Button button in scope.GetComponentsInChildren<Button>())
                if (button.IsInteractable() && button.gameObject.activeInHierarchy) available.Add(button);
            for (int i = 0; i < available.Count; i++)
            {
                Navigation navigation = available[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = navigation.selectOnLeft = available[(i + available.Count - 1) % available.Count];
                navigation.selectOnDown = navigation.selectOnRight = available[(i + 1) % available.Count];
                available[i].navigation = navigation;
            }
        }
    }
}

