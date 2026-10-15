using System.Globalization;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private const float SDHpWidth = 572;
        private const float SDHpThickness = 18;
        private const float SDPlayerHpY = -278;
        private const float SDRollPipY = -309;
        private const float SDRolePanelWidth = 188;
        private const float SDRolePanelHeight = 390;
        private const float SDRolePanelX = 515;
        private const float SDPlayerDiceY = -28;
        private Text _sdScoreNumber, _sdNpcScoreNumber, _sdNpcFormula, _sdNpcDamage, _sdPlayerDamage;
        private Text _sdNpcHpDelta, _sdPlayerHpDelta;
        private readonly Image[] _sdHoldMarks = new Image[5];
        private readonly Image[] _sdRollPips = new Image[2];
        private Image _sdPlayerGhost, _sdNpcGhost, _sdSaveSymbol;
        private Image _sdSoundSymbol;
        private float _sdLastSoundVolume = -1;
        private static readonly Color SDRailInk = new Color32(218, 229, 224, 255);
        private static readonly Color SDRailMuted = new Color32(171, 192, 188, 255);
        /// <summary>A spent category recedes rather than disappearing; still legible, clearly unavailable.</summary>
        private static readonly Color SDRailUsed = new Color32(108, 128, 128, 255);
        private static readonly Color SDRailRule = new Color32(58, 82, 86, 255);
        private static readonly Color SDPaperInk = new Color32(31, 46, 52, 255);
        private static readonly Color SDPaperMuted = new Color32(90, 102, 103, 255);
        private static readonly Color SDPlayerColor = new Color32(104, 168, 156, 255);
        private static readonly Color SDEnemyColor = new Color32(181, 111, 108, 255);

        private Image SDIcon(string name, Transform parent, string icon, Vector2 position, float size, Color color)
        {
            return DiceYahzeeVisuals.Icon(name, parent, "Art/DiceYahzee/" + icon,
                position, Vector2.one * size, color);
        }

        private void SDBuildTableMaterial()
        {
            var frame = UiFactory.CreatePanel("TableRim", _sdBattle, new Vector2(1200, 560), new Vector2(0, -8), SDPositive);
            frame.sprite = null;
            frame.color = Color.clear;
            frame.raycastTarget = false;
            var mat = UiFactory.CreatePanel("TableMat", _sdBattle, new Vector2(1192, 552), new Vector2(0, -8), SDInk);
            mat.sprite = null;
            mat.color = Color.clear;
            mat.raycastTarget = false;
            var grain = UiFactory.CreateRect("Grain", mat.transform, new Vector2(1176, 536), Vector2.zero).gameObject.AddComponent<RawImage>();
            grain.texture = Resources.Load<Texture2D>("Art/Textures/leather-white");
            grain.uvRect = new Rect(0, 0, 2.4f, 1.5f);
            grain.color = new Color(.48f, .57f, .61f, .035f);
            grain.raycastTarget = false;
            var line = UiFactory.CreatePanel("OpponentDivider", _sdBattle, new Vector2(680, 1), new Vector2(0, 40), SDMuted);
            line.color = new Color(.55f, .72f, .72f, .34f);
            line.raycastTarget = false;
        }

        private void SDBuildHudDetails()
        {
            _sdNpcDamage = SDText("NpcDamage", _sdBattle, "", 26, new Vector2(268, 284), new Vector2(120, 34), SDPositive, TextAnchor.MiddleRight);
            _sdNpcDamage.fontStyle = FontStyle.Bold;
            SDAddScoreEdge(_sdNpcDamage);
            _sdPlayerDamage = SDText("PlayerDamage", _sdBattle, "", 26, new Vector2(268, -244), new Vector2(120, 34), SDNegative, TextAnchor.MiddleRight);
            _sdPlayerDamage.fontStyle = FontStyle.Bold;
            SDAddScoreEdge(_sdPlayerDamage);
            // The amount each bar is about to lose, shown beside the bar while it drains.
            _sdNpcHpDelta = SDText("NpcHpDelta", _sdBattle, "", 22, new Vector2(-350, 245), new Vector2(136, 30), SDPositive, TextAnchor.MiddleRight);
            _sdNpcHpDelta.fontStyle = FontStyle.Bold;
            SDAddScoreEdge(_sdNpcHpDelta);
            _sdPlayerHpDelta = SDText("PlayerHpDelta", _sdBattle, "", 22, new Vector2(-350, -278), new Vector2(136, 30), SDNegative, TextAnchor.MiddleRight);
            _sdPlayerHpDelta.fontStyle = FontStyle.Bold;
            SDAddScoreEdge(_sdPlayerHpDelta);
            _sdNpcGhost = SDGhost(_sdNpcBar);
            _sdPlayerGhost = SDGhost(_sdPlayerBar);
            _sdScoreNumber = SDText("ScoreNumber", _sdBattle.Find("CurrentRole"), "", 58, new Vector2(0, -29), new Vector2(240, 100), SDScoreGold);
            _sdScoreNumber.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? _sdScoreNumber.font;
            _sdScoreNumber.verticalOverflow = VerticalWrapMode.Overflow;
            SDAddScoreEdge(_sdScoreNumber);
            for (int i = 0; i < 2; i++)
            {
                _sdRollPips[i] = UiFactory.CreatePanel("RollPip" + i, _sdBattle, Vector2.one * 14,
                    new Vector2((i - .5f) * 24, SDRollPipY), SDPositive);
                _sdRollPips[i].sprite = UiFactory.CircleSprite;
                // A dark rim keeps an unspent reroll legible against the leather even when the
                // gold is dimmed on the spent one.
                var rim = _sdRollPips[i].gameObject.AddComponent<Outline>();
                rim.effectColor = new Color(0.03f, 0.07f, 0.08f, .9f);
                rim.effectDistance = new Vector2(2, -2);
            }
            for (int i = 0; i < 5; i++)
                _sdHoldMarks[i] = SDIcon("HoldMark" + i, _sdBattle, "icon-lock", new Vector2(SDHandCenterX + (i - 2) * 132, SDPlayerDiceY - 74), 18, SDPositive);
        }

        private static Image SDGhost(Image fill)
        {
            var ghost = UiFactory.CreatePanel("Forecast", fill.transform.parent, new Vector2(SDHpWidth, SDHpThickness), Vector2.zero, SDEnemyColor);
            ghost.rectTransform.anchorMin = ghost.rectTransform.anchorMax = new Vector2(0, .5f);
            ghost.rectTransform.pivot = new Vector2(0, .5f);
            ghost.color = new Color(.86f, .53f, .48f, .65f);
            return ghost;
        }

        private static string SDMultiplier(ScoreHandKind kind, bool formed = true)
        {
            float multiplier = formed ? ScoreRules.MultiplierQuarters(kind) / 4f : 1f;
            return "×" + multiplier.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string SDCalculation(ScoreHandKind kind, int[] dice)
        {
            ScoreOffer offer = ScoreRules.Evaluate(kind, dice);
            return offer.BaseScore + " " + SDMultiplier(kind, offer.IsFormed);
        }

        private void SDRefreshHud()
        {
            if (_sdEngine == null || _sdNpcDamage == null) return;
            bool plan = !_sdRolling && !_sdCommitted && !SDModalOpen;
            ScoreForecast forecast = _sdCommitted ? new ScoreForecast(_sdEngine.LastRound.PlayerScore, _sdEngine.LastRound.NpcScore, _sdEngine.LastRound.IsSaved) : _sdEngine.Forecast(_sdSelectedKind, _sdSaveArmed);
            _sdNpcDamage.text = _sdRolling ? "" : "−" + (_sdCommitted ? _sdEngine.LastRound.DamageToNpc : forecast.DamageToNpc);
            _sdPlayerDamage.text = _sdRolling ? "" : "−" + (_sdCommitted ? _sdEngine.LastRound.DamageToPlayer : forecast.DamageToPlayer);
            _sdNpcDamage.color = SDPositive;
            _sdPlayerDamage.color = SDNegative;
            SDPlaceGhost(_sdNpcGhost, _sdEngine.NpcHp, _sdEngine.NpcMaxHp, forecast.DamageToNpc, plan);
            SDPlaceGhost(_sdPlayerGhost, _sdEngine.PlayerHp, ScoreDuel.StartingHp, forecast.DamageToPlayer, plan);
            for (int i = 0; i < 2; i++)
            {
                _sdRollPips[i].color = i < _sdEngine.RerollsLeft ? SDPositive : new Color(.62f, .66f, .67f, .5f);
                _sdRollPips[i].gameObject.SetActive(!_sdCommitted);
            }
            for (int i = 0; i < 5; i++) _sdHoldMarks[i].gameObject.SetActive(plan && _sdEngine.Held[i] && !_sdDragAiming && _sdAttackAge < 0 && !_sdFxActive);
            _sdSaveSymbol.color = _sdSaveButton.interactable ? SDPaperInk : SDMuted;
            _sdSaveButton.transform.Find("SaveCount").GetComponent<Text>().color = _sdSaveSymbol.color;
            SDRefreshCarryPresentation();
        }

        private static void SDPlaceGhost(Image ghost, int hp, int max, int damage, bool visible)
        {
            ghost.gameObject.SetActive(visible);
            if (!visible || max <= 0) return;
            float end = Mathf.Clamp01((float)hp / max);
            float start = Mathf.Clamp01((float)(hp - damage) / max);
            ghost.rectTransform.anchoredPosition = new Vector2(SDHpWidth * start, 0);
            ghost.rectTransform.sizeDelta = new Vector2(SDHpWidth * (end - start), SDHpThickness);
        }

        /// <summary>
        /// Writes the health each bar is losing next to that bar while it drains, so the drop is a
        /// number the reader can name rather than only a shrinking bar. Alpha follows the drain.
        /// </summary>
        private void SDShowHpDeltas(float alpha)
        {
            if (_sdNpcHpDelta == null) return;
            int npcLoss = _sdEngine != null ? Mathf.Max(0, Mathf.RoundToInt(_sdNpcHpFrom) - _sdEngine.NpcHp) : 0;
            int playerLoss = _sdEngine != null ? Mathf.Max(0, Mathf.RoundToInt(_sdPlayerHpFrom) - _sdEngine.PlayerHp) : 0;
            SDSetHpDelta(_sdNpcHpDelta, npcLoss, SDPositive, alpha);
            SDSetHpDelta(_sdPlayerHpDelta, playerLoss, SDNegative, alpha);
        }

        private static void SDSetHpDelta(Text label, int loss, Color color, float alpha)
        {
            if (label == null) return;
            label.text = loss > 0 && alpha > .01f ? "−" + loss : "";
            color.a = Mathf.Clamp01(alpha);
            label.color = color;
        }

        private void SDStyleRemainingScreens()
        {
            foreach (var button in _sdRoot.GetComponentsInChildren<Button>(true))
            {
                if (button.name.StartsWith("Die") || button.name.StartsWith("NpcDie") || button.name.StartsWith("ExampleDie") || button.name.StartsWith("PracticeDie")) continue;
                if (button.name.StartsWith("LiveCategory") || button == _sdSaveButton) continue;
                DiceYahzeeVisuals.StyleButton(button, button.name == "Start" || button.name == "Rematch");
            }
            foreach (RectTransform modal in new[] { _sdRules, _sdSheet, _sdMenu, _sdCredits, _sdPractice })
                DiceYahzeeVisuals.StylePanel(modal.Find("Panel").GetComponent<Image>());
            _sdSheetNote.gameObject.SetActive(false);
            _sdInstruction.gameObject.SetActive(false);
            _sdSaveNote.gameObject.SetActive(false);
            _sdPracticeGestureText.gameObject.SetActive(false);
            var menu = _sdRoot.Find("Menu");
            menu.GetComponentInChildren<Text>().text = "";
            ((RectTransform)menu).anchoredPosition = new Vector2(-554, -319);
            ((RectTransform)menu).sizeDelta = new Vector2(46, 38);
            SDIcon("MenuSymbol", menu, "icon-menu", Vector2.zero, 22, SDMuted);
            Transform sound = _canvasRoot.transform.Find("SoundToggle");
            if (sound != null)
            {
                ((RectTransform)sound).anchoredPosition = new Vector2(554, -319);
                ((RectTransform)sound).sizeDelta = new Vector2(46, 38);
                sound.GetComponentInChildren<Text>().enabled = false;
                DiceYahzeeVisuals.StyleButton(sound.GetComponent<Button>(), false, true);
                _sdSoundSymbol = SDIcon("SoundSymbol", sound, "icon-sound", Vector2.zero, 24, SDMuted);
            }
        }

        private void SDRefreshSoundSymbol()
        {
            float volume = GameAudio.Volume;
            if (_sdSoundSymbol == null || Mathf.Approximately(volume, _sdLastSoundVolume)) return;
            _sdLastSoundVolume = volume;
            _sdSoundSymbol.sprite = DiceYahzeeVisuals.LoadSprite("Art/DiceYahzee/" + (volume <= .001f ? "icon-sound-off" : "icon-sound"));
            _sdSoundSymbol.color = volume <= .001f ? SDMuted : SDPositive;
        }
    }
}
