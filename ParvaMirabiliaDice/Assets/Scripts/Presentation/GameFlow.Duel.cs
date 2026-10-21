using System;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private bool _empower;
        private RectTransform _intentRoot;
        private Text _intentName, _intentValues, _manaLabel, _manaAdvice, _opponentMotto;
        private Button _empowerButton;
        private Image _opponentSeal;
        private Image _resultSeal;
        private readonly Text[] _spellValues = new Text[7];
        private readonly Image[] _spellIcons = new Image[7];
        private static readonly string[] SpellIcons = { "edged-shield", "crossed-swords", "burning-embers",
            "lightning-helix", "comet-spark", "comet-spark", "crowned-explosion" };
        private static readonly string[] SpellConditions = { "すべてを守りに", "同じ目 × 2", "同じ目 × 3",
            "上位3個を攻撃に", "4つの連続", "5つの連続", "同じ目 × 5" };
        private static readonly string[] OpponentSeals = { "fire-silhouette", "edged-shield", "crystal-ball" };
        private static readonly string[] OpponentTitles = { "紅蓮の挑戦者", "蒼の守り手", "星読みの術師" };
        private static readonly string[] OpponentMottos = { "強化した一撃を、守りで受けるか。", "厚い守りの隙に、魔力を蓄える。", "変わる攻守に、次の一手を残す。" };
        private static readonly Color[] SealColors = { new Color32(230, 134, 111, 255),
            new Color32(125, 196, 208, 255), new Color32(202, 175, 239, 255) };
        private static readonly Color DuelPanel = new Color32(19, 38, 44, 255);

        private void BuildDuelPresentation()
        {
            if (!ResolveConfig().TacticalEnabled) return;
            var table = UiFactory.CreateStretchPanel("DuelTable", _canvasRoot.transform,
                new Color32(9, 22, 29, 255));
            table.raycastTarget = false;
            table.transform.SetSiblingIndex(1);
            var leather = UiFactory.CreateStretch("LeatherGrain", table.transform).gameObject.AddComponent<RawImage>();
            leather.texture = Resources.Load<Texture2D>("Art/Textures/leather-white");
            leather.color = new Color32(26, 53, 62, 255);
            leather.uvRect = new Rect(0, 0, 2.5f, 1.5f);
            leather.raycastTarget = false;
            var stars = UiFactory.CreateRect("CelestialInlay", table.transform, new Vector2(1800, 1100), Vector2.zero);
            UiFactory.AddVertexGraphic<ArcaneBackdrop>(stars.gameObject);

            // Opponents read as three illustrated cards with a visual identity and a practical
            // promise, rather than three undifferentiated buttons in a settings menu.
            Text title = _titleRoot.Find("Title").GetComponent<Text>();
            title.text = "ARCANE DUEL";
            title.font = DuelArt.DisplayFont;
            title.fontSize = 60;
            title.rectTransform.anchoredPosition = new Vector2(0, 239);
            Text subtitle = _titleRoot.Find("Subtitle").GetComponent<Text>();
            subtitle.text = "五つのダイス。予告を読み、魔力を賭ける。";
            subtitle.rectTransform.anchoredPosition = new Vector2(0, 181);
            Position(_titleRoot, "Rule", new Vector2(0, 147));
            Position(_titleRoot, "PickLabel", new Vector2(0, 119));
            _titleRoot.Find("PickLabel").GetComponent<Text>().fontSize = 17;
            for (int i = 0; i < 3; i++)
            {
                Button card = _opponentButtons[i];
                var rect = card.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(234, 192);
                ResizeDuelButton(card, rect.sizeDelta);
                rect.anchoredPosition = new Vector2((i - 1) * 260, 8);
                var label = card.GetComponentInChildren<Text>();
                label.rectTransform.sizeDelta = new Vector2(220, 26);
                label.rectTransform.anchoredPosition = new Vector2(0, -34);
                label.text = OpponentTitles[i];
                label.fontSize = 22;
                DuelArt.Icon(rect, "OpponentSeal", OpponentSeals[i], new Vector2(0, 30), 86, SealColors[i]);
                DuelArt.Frame(rect, rect.sizeDelta, UiFactory.GoldDim);
                UiFactory.CreateText("Approach", rect, NpcArchetypes.DisplayName(Opponents[i]) + "の魔術", 15,
                    TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0, -66), new Vector2(220, 24));
            }
            _opponentDescription.rectTransform.anchoredPosition = new Vector2(0, -111);
            _opponentDescription.rectTransform.sizeDelta = new Vector2(950, 32);
            _opponentDescription.fontSize = 17;
            Position(_titleRoot, "Start", new Vector2(0, -161));
            Position(_titleRoot, "Learn", new Vector2(-145, -235));
            Position(_titleRoot, "ShowRules", new Vector2(145, -235));
            var footnote = _titleRoot.Find("Footnote").GetComponent<Text>();
            footnote.text = "PARVA MIRABILIA  /  相手の予告・役の回復・魔力の温存で決まる短い決闘";
            footnote.rectTransform.anchoredPosition = new Vector2(0, -288);

            // Clear separation between promise, dice, spell, and outcome. The seven spell slots
            // never move: an unformed or recovering spell is information worth keeping visible.
            Position(_battleRoot, "Info", new Vector2(0, -165));
            Position(_battleRoot, "PlayerName", new Vector2(-290, -195));
            Position(_battleRoot, "PlayerHp", new Vector2(290, -195));
            Position(_battleRoot, "PlayerBar", new Vector2(0, -219));
            Position(_battleRoot, "PlayerFlash", new Vector2(0, -219));
            Position(_battleRoot, "Hint", new Vector2(0, -244));
            Position(_battleRoot, "Reroll", new Vector2(-145, -286));
            Position(_battleRoot, "Commit", new Vector2(145, -286));
            Position(_battleRoot, "Advance", new Vector2(0, -286));
            _flowNote.rectTransform.sizeDelta = new Vector2(790, 24);
            _flowNote.rectTransform.anchoredPosition = new Vector2(0, -332);
            _flowNote.fontSize = 14;
            _bannerText.rectTransform.sizeDelta = new Vector2(1080, 60);
            _battleRoot.Find("Brand").GetComponent<Text>().text = "ARCANE DUEL";
            _battleRoot.Find("Brand").GetComponent<Text>().font = DuelArt.DisplayFont;
            _quickLow.GetComponent<RectTransform>().anchoredPosition = new Vector2(-485, 0);
            _quickLow.GetComponent<RectTransform>().sizeDelta = new Vector2(192, 46);
            _quickAim.GetComponent<RectTransform>().sizeDelta = new Vector2(192, 60);

            Image intent = UiFactory.CreatePanel("IntentPanel", _battleRoot, new Vector2(580, 80),
                new Vector2(0, 155), DuelPanel);
            intent.raycastTarget = false;
            _intentRoot = intent.rectTransform;
            DuelArt.Frame(_intentRoot, new Vector2(580, 80), UiFactory.GoldDim);
            _intentName = UiFactory.CreateText("IntentName", _intentRoot, "相手の予告", 17,
                TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0, 19), new Vector2(540, 26));
            _intentValues = UiFactory.CreateText("IntentValues", _intentRoot, "", 26,
                TextAnchor.MiddleCenter, UiFactory.Text, new Vector2(0, -13), new Vector2(540, 36));
            _opponentSeal = DuelArt.Icon(_battleRoot, "RivalSeal", OpponentSeals[0], new Vector2(-485, 158), 106, SealColors[0]);
            _opponentMotto = UiFactory.CreateText("RivalMotto", _battleRoot, "", 15,
                TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(-485, 80), new Vector2(210, 46));
            _manaLabel = UiFactory.CreateText("Mana", _battleRoot, "", 22,
                TextAnchor.MiddleCenter, UiFactory.Gold, new Vector2(485, 181), new Vector2(220, 34));
            _manaAdvice = UiFactory.CreateText("ManaAdvice", _battleRoot, "", 15,
                TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(485, 130), new Vector2(210, 60));
            _empowerButton = UiFactory.CreateButton("Empower", _battleRoot, new Vector2(206, 56),
                new Vector2(485, -286), "魔力で強化", 18, ToggleEmpower);
            DuelArt.Frame(_empowerButton.transform, new Vector2(206, 56), UiFactory.GoldDim);
            _resultSeal = DuelArt.Icon(_resultRoot, "ResultSeal", "crowned-explosion", new Vector2(0, 249), 74, UiFactory.Gold);
            for (int i = 0; i < 7; i++)
            {
                var rect = _yakuButtons[i].GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(142, 96);
                ResizeDuelButton(_yakuButtons[i], rect.sizeDelta);
                rect.anchoredPosition = new Vector2((i - 3) * 150, -99);
                var label = _yakuButtons[i].GetComponentInChildren<Text>();
                label.rectTransform.sizeDelta = new Vector2(140, 24);
                label.rectTransform.anchoredPosition = new Vector2(0, -5);
                label.fontSize = 16;
                _spellIcons[i] = DuelArt.Icon(rect, "SpellIcon", SpellIcons[i], new Vector2(0, 24), 28, UiFactory.Gold);
                _spellValues[i] = UiFactory.CreateText("SpellValues", rect, "", 14,
                    TextAnchor.MiddleCenter, UiFactory.TextDim, new Vector2(0, -30), new Vector2(138, 24));
                DuelArt.Frame(rect, rect.sizeDelta, UiFactory.GoldDim);
            }
            var panel = _resultRoot.Find("ResultPanel");
            if (panel != null) DuelArt.Frame(panel, new Vector2(780, 450), UiFactory.GoldDim);
            SelectOpponent(1);
            StyleDuelSurfaces();
            BuildTrials();
            BuildDuelEffects();
        }

        private static void ResizeDuelButton(Button button, Vector2 size)
        {
            button.GetComponent<Image>().sprite = UiFactory.RoundedSprite((int)size.x, (int)size.y);
            var gloss = button.transform.Find("Gloss");
            if (gloss != null) gloss.gameObject.SetActive(false);
        }

        private void StyleDuelSurfaces()
        {
            foreach (Button button in _canvasRoot.GetComponentsInChildren<Button>(true))
            {
                var palette = button.GetComponent<ButtonPalette>();
                if (palette == null || palette.Face == UiFactory.Gold) continue;
                palette.Face = DuelPanel;
                button.GetComponent<Image>().color = DuelPanel;
                var gloss = button.transform.Find("Gloss");
                if (gloss != null) gloss.gameObject.SetActive(false);
            }
            _battleRoot.Find("TopBar").GetComponent<Image>().color = DuelPanel;
            foreach (Image panel in _canvasRoot.GetComponentsInChildren<Image>(true))
            {
                if (panel.name != "Panel" && panel.name != "RulesPanel" && panel.name != "ResultPanel") continue;
                panel.color = DuelPanel;
                var gloss = panel.transform.Find("Gloss");
                if (gloss != null) gloss.gameObject.SetActive(false);
                DuelArt.Frame(panel.transform, panel.rectTransform.sizeDelta, UiFactory.GoldDim);
            }
            _resultRoot.Find("ResultDim").GetComponent<Image>().color = new Color(0, 0, 0, .35f);
        }

        private static void StyleDuelChoice(Button button, bool selected)
        {
            UiFactory.StyleSelected(button, selected);
            if (selected) return;
            button.GetComponent<Image>().color = DuelPanel;
            button.GetComponent<ButtonPalette>().Face = DuelPanel;
        }

        private void RefreshOpponentCards(int selected)
        {
            for (int i = 0; i < 3; i++)
            {
                var card = _opponentButtons[i];
                StyleDuelChoice(card, selected == i);
                card.transform.Find("OpponentSeal").GetComponent<Image>().color = selected == i ? DuelPanel : SealColors[i];
                card.transform.Find("Approach").GetComponent<Text>().color = selected == i ? DuelPanel : UiFactory.TextDim;
            }
        }

        private static void Position(Transform root, string name, Vector2 pos)
        {
            var found = root.Find(name) as RectTransform;
            if (found != null) found.anchoredPosition = pos;
        }

        private static YakuOffer DefaultReadyOffer(YakuOffer[] offers)
        {
            // Default to the reusable Chance, preserving the player's scarce spells until they
            // deliberately select one. The view explains every alternative's immediate result.
            foreach (var offer in offers) if (offer.Kind == YakuKind.Chance) return offer;
            return offers[0];
        }

        private void SelectDuelSpell(int index)
        {
            if (index < 0 || index >= 7) return;
            YakuKind kind = (YakuKind)index;
            if (!_engine.IsYakuReady(kind) || !Yaku.Qualifies(_engine.PlayerDice, _engine.Config, kind)) return;
            _yakuOffer = Yaku.Resolve(_engine.PlayerDice, _engine.Config, kind);
            if (kind == YakuKind.Guard) _empower = false;
            GameAudio.Play("die_keep");
            RefreshBattle();
            _yakuPunchIndex = index;
            _yakuPunchElapsed = 0;
        }

        private void ToggleEmpower()
        {
            if (ModalOpen || _engine == null || _committed || _engine.PlayerMana < 1 ||
                _yakuOffer.Kind == YakuKind.Guard) return;
            _empower = !_empower;
            GameAudio.Play("die_keep");
            RefreshBattle();
        }

        private void LayoutSpellCards(bool revealing)
        {
            for (int i = 0; i < 7; i++)
            {
                Button card = _yakuButtons[i];
                card.gameObject.SetActive(!revealing);
                if (revealing) continue;
                var kind = (YakuKind)i;
                bool formed = Yaku.Qualifies(_engine.PlayerDice, _engine.Config, kind);
                bool ready = _engine.IsYakuReady(kind);
                var label = card.GetComponentInChildren<Text>();
                label.text = Yaku.DisplayName(kind);
                StyleDuelChoice(card, formed && ready && kind == _yakuOffer.Kind);
                UiFactory.SetButtonEnabled(card, formed && ready);
                if (!formed || !ready) card.GetComponent<Image>().color = new Color32(20, 31, 38, 255);
                bool selected = formed && ready && kind == _yakuOffer.Kind;
                _spellIcons[i].color = selected ? DuelPanel : (formed && ready ? UiFactory.Gold : UiFactory.TextDim);
                _spellValues[i].color = selected ? DuelPanel : UiFactory.TextDim;
                if (!ready) _spellValues[i].text = "回復中 · 次Rに復帰";
                else if (!formed) _spellValues[i].text = SpellConditions[i];
                else
                {
                    var offer = Yaku.Resolve(_engine.PlayerDice, _engine.Config, kind);
                    _spellValues[i].text = "攻 " + (offer.StrikeValue(_engine.PlayerDice) + _engine.Config.PlayerSpellAttackBonus[i]) +
                        "   守 " + (offer.WardValue(_engine.PlayerDice) + _engine.Config.PlayerSpellWardBonus[i]);
                }
            }
        }

        private void RefreshDuelPresentation(bool revealing)
        {
            if (!_engine.Config.TacticalEnabled || _intentRoot == null) return;
            _blessingsButton.gameObject.SetActive(_run != null);
            int rival = (int)_opponent;
            _opponentSeal.sprite = DuelArt.Load("Icons/" + OpponentSeals[rival]);
            _opponentSeal.color = SealColors[rival];
            _opponentMotto.text = OpponentMottos[rival];
            _npcName.text = OpponentTitles[rival];
            if (_run != null) _roundLabel.text = "三連決闘 " + _run.Fight + "/3  ·  R" + _engine.Round;
            _intentRoot.gameObject.SetActive(!revealing);
            for (int i = 0; i < 5; i++)
                _battleRoot.Find("NpcDie" + i).gameObject.SetActive(revealing);
            _manaLabel.text = "魔力  " + new string('◆', _engine.PlayerMana) +
                new string('◇', _engine.Config.MaxMana - _engine.PlayerMana);
            _empowerButton.gameObject.SetActive(!revealing);
            _manaAdvice.text = revealing ? "このラウンドに蓄えた魔力\n＋" + _engine.LastRound.PlayerManaGained :
                "残した振り直し → 魔力\n最大 " + _engine.Config.MaxMana + " · 強化は魔力1";
            if (revealing)
            {
                _infoText.text = "魔力 ＋" + _engine.LastRound.PlayerManaGained + "　／　" +
                    Yaku.DisplayName(_engine.LastRound.PlayerYaku) +
                    (_engine.LastRound.PlayerYaku == YakuKind.Chance ? "は続けて使用できます" : "は次Rに休み、その次に復帰");
                _flowNote.text = _run != null ? RunBlessingSummary() : "決闘の記録から、選んだ役とダメージを振り返れます。";
                return;
            }
            if (_yakuOffer.Kind == YakuKind.Guard || _engine.PlayerMana == 0) _empower = false;
            var intent = _engine.Intent;
            _intentName.text = "相手の予告  /  " + Yaku.DisplayName(intent.Kind) + (intent.Empowered ? "  ［魔力強化］" : "");
            _intentValues.text = "<color=#E99580>攻撃 " + intent.Attack + "</color>     <color=#93CCD8>守り " + intent.Ward + "</color>";
            var forecast = _engine.Forecast(_yakuOffer.Kind, _empower);
            string warning = forecast.DamageToPlayer >= _engine.PlayerHp && forecast.DamageToNpc >= _engine.NpcHp ?
                "  <color=#F3A38F>双方撃破 · 累計ダメージで判定</color>" :
                forecast.DamageToPlayer >= _engine.PlayerHp ? "  <color=#F3A38F>このままでは撃破される</color>" :
                forecast.DamageToNpc >= _engine.NpcHp ? "  <color=#E8C87A>相手を撃破できる</color>" : "";
            _bannerText.text = "<size=23>与える <color=#E8C87A>" + forecast.DamageToNpc +
                "</color>    /    受ける <color=#93CCD8>" + forecast.DamageToPlayer + "</color></size>" + warning +
                "\n" + (forecast.PushbackUncertain ? "双方の守りが成立。自分の合計 " + Scoring.Sum(_engine.PlayerDice) +
                " · 相手は未公開。押し勝ちは公開後に判定。" :
                "攻 " + forecast.Attack + " − 相手の守 " + intent.Ward + "    /    相手の攻 " + intent.Attack + " − 守 " + forecast.Ward);
            _infoText.text = Yaku.DisplayName(_yakuOffer.Kind) + "を宣告" + (_empower ? " ［強化 ＋" + _engine.Config.EmpowerAttack + "］" : "") +
                "   ·   振り直し " + _engine.PlayerRerollsLeft + "回   ·   確定後の魔力 " + forecast.ManaAfter;
            _infoText.color = UiFactory.Text;
            StyleDuelChoice(_empowerButton, _empower);
            UiFactory.SetButtonEnabled(_empowerButton, _engine.PlayerMana > 0 && _yakuOffer.Kind != YakuKind.Guard);
            if (!_empowerButton.interactable) _empowerButton.GetComponent<Image>().color = new Color32(20, 31, 38, 255);
            Caption(_empowerButton, _empower ? "強化中 · 攻 ＋" + _engine.Config.EmpowerAttack + "\n魔力1を使用" :
                "攻 ＋" + _engine.Config.EmpowerAttack + " に強化\n魔力1を使用");
            Caption(_commitButton, forecast.PushbackUncertain ? "宣告して押し勝ちを判定" :
                "宣告する · 与" + forecast.DamageToNpc + " / 受" + forecast.DamageToPlayer);
            _commitButton.GetComponentInChildren<Text>().fontSize = 18;
            _flowNote.text = _run != null ? RunBlessingSummary() : "役は次の1ラウンドだけ休みます。チャンスは連続使用可。相手の予告は変わりません。";
        }
    }
}
