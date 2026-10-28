using System.Globalization;
using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private const float SDHpWidth = 572;
        private const float SDHpThickness = 36;
        private const float SDPlayerHpY = -278;
        private const float SDRollPipY = -309;
        private const float SDRolePanelWidth = 188;
        private const float SDRolePanelHeight = 390;
        private const float SDRolePanelX = 515;
        private const float SDPlayerDiceY = -28;
        private Text _sdScoreNumber, _sdNpcScoreNumber, _sdNpcFormula;
        // Each health bar prints one line — side, health, then the drop it is taking — so the line
        // is recomposed from these four values rather than spelling the damage out twice.
        private float _sdPlayerHpShown, _sdNpcHpShown;
        private int _sdPlayerDamageShown, _sdNpcDamageShown;
        private int _sdPlayerHealShown, _sdNpcHealShown;
        private readonly Image[] _sdHoldMarks = new Image[5];
        // Three slots so a formed Yahtzee's bonus reroll has a home; a normal round hides the third.
        private readonly Image[] _sdRollPips = new Image[3];
        // The pip pulse advances on its own clock so a settle or a refresh cannot restart it.
        private float _sdRollPipPulseAge;
        // The core exposes only the rerolls still left, so the round's allowance is remembered at
        // its start and reported until the next round resets it.
        private int _sdRerollAllowance = 2;
        private int _sdRerollAllowanceRound = -1;
        private Image _sdPlayerGhost, _sdNpcGhost, _sdSaveSymbol;
        private Image _sdPlayerLossBand, _sdNpcLossBand;
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
        /// <summary>
        /// The drop a bar is taking, printed in red on the bar that receives it.
        ///
        /// Both fills sit in the middle of the luminance range, so the figure has to be clearly
        /// darker than they are to survive: a bright coral measured only about 1.2:1 over the teal
        /// and 1.1:1 over the enemy rose, which is a difference in hue rather than a readable one.
        /// This deeper red measures about 2.0:1 and 1.5:1 there, and about 2.7:1 over the ivory
        /// drain band the figure is printed on while a bar empties.
        /// </summary>
        private static readonly Color SDDamageInk = new Color32(193, 39, 45, 255);
        /// <summary>The same red as a rich-text tag, built once so a per-frame line costs no parsing.</summary>
        private static readonly string SDDamageTag = "<color=#" + ColorUtility.ToHtmlStringRGB(SDDamageInk) + ">";
        /// <summary>Health restored by kept rerolls reads in a deep green that survives both fills.</summary>
        private static readonly Color SDHealInk = new Color32(27, 91, 64, 255);
        private static readonly string SDHealTag = "<color=#" + ColorUtility.ToHtmlStringRGB(SDHealInk) + ">";

        private Image SDIcon(string name, Transform parent, string icon, Vector2 position, float size, Color color)
        {
            return DiceYahzeeVisuals.Icon(name, parent, "Art/DiceYahzee/" + icon,
                position, Vector2.one * size, color);
        }

        private void SDBuildTableMaterial(Transform root)
        {
            var frame = UiFactory.CreatePanel("TableRim", root, new Vector2(1200, 560), new Vector2(0, -8), SDPositive);
            frame.sprite = null;
            frame.color = Color.clear;
            frame.raycastTarget = false;
            var mat = UiFactory.CreatePanel("TableMat", root, new Vector2(1192, 552), new Vector2(0, -8), SDInk);
            mat.sprite = null;
            mat.color = Color.clear;
            mat.raycastTarget = false;
            var grain = UiFactory.CreateRect("Grain", mat.transform, new Vector2(1176, 536), Vector2.zero).gameObject.AddComponent<RawImage>();
            grain.texture = Resources.Load<Texture2D>("Art/Textures/leather-white");
            grain.uvRect = new Rect(0, 0, 2.4f, 1.5f);
            grain.color = new Color(.48f, .57f, .61f, .035f);
            grain.raycastTarget = false;
            var line = UiFactory.CreatePanel("OpponentDivider", root, new Vector2(680, 1), new Vector2(0, 40), SDMuted);
            line.color = new Color(.55f, .72f, .72f, .34f);
            line.raycastTarget = false;
        }

        private void SDBuildHudDetails()
        {
            _sdNpcGhost = SDGhost(_sdNpcBar);
            _sdPlayerGhost = SDGhost(_sdPlayerBar);
            _sdNpcLossBand = SDBuildHpLossBand(_sdNpcBar);
            _sdPlayerLossBand = SDBuildHpLossBand(_sdPlayerBar);
            foreach (var label in new[] { _sdNpcHpLabel, _sdPlayerHpLabel })
            {
                // Health and the drop it is taking are one centred line inside the track. Warping a
                // second, larger numeral onto the right end of the bar read as a sticker laid over
                // the bar rather than as this bar's own figure, and forcing its weight up to survive
                // the ivory drain band only smeared it further.
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.sizeDelta = new Vector2(SDHpWidth - 24, 30);
                label.alignment = TextAnchor.MiddleCenter;
                label.fontSize = 18;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            _sdScoreNumber = SDBuildScoreNumber("ScoreNumber", _sdBattle.Find("CurrentRole"), new Vector2(0, -29), true);
            for (int i = 0; i < 3; i++)
            {
                _sdRollPips[i] = UiFactory.CreatePanel("RollPip" + i, _sdBattle, Vector2.one * 14,
                    new Vector2((i - 1) * 24, SDRollPipY), SDPositive);
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

        /// <summary>
        /// One health readout: side, current health, then the health kept rerolls restore in
        /// green and the drop that side is taking in red. A single centred label carries all of
        /// it, so both figures read as part of this bar's own result instead of as extra numerals
        /// parked at the far end of the track.
        /// </summary>
        private static string SDHealthLine(string side, int hp, int max, int damage, int heal = 0)
        {
            string line = side + " " + hp + "/" + Mathf.Max(0, max);
            if (heal > 0) line += " " + SDHealTag + "+" + heal + "</color>";
            return damage > 0 ? line + " " + SDDamageTag + "−" + damage + "</color>" : line;
        }

        /// <summary>
        /// Reprints both health lines from the health currently on screen plus the heal and drop
        /// each bar owes. The forecast and the drain both feed this one line, so neither can leave
        /// a stale figure behind for the other to contradict.
        /// </summary>
        private void SDApplyHpLines()
        {
            if (_sdEngine == null || _sdPlayerHpLabel == null) return;
            _sdPlayerHpLabel.text = SDHealthLine("あなた", Mathf.RoundToInt(_sdPlayerHpShown), ScoreDuel.StartingHp, _sdPlayerDamageShown, _sdPlayerHealShown);
            _sdNpcHpLabel.text = SDHealthLine("相手", Mathf.RoundToInt(_sdNpcHpShown), _sdEngine.NpcMaxHp, _sdNpcDamageShown, _sdNpcHealShown);
        }

        private static Image SDGhost(Image fill)
        {
            var ghost = UiFactory.CreatePanel("Forecast", fill.transform.parent, new Vector2(SDHpWidth, SDHpThickness), Vector2.zero, SDEnemyColor);
            ghost.rectTransform.anchorMin = ghost.rectTransform.anchorMax = new Vector2(0, .5f);
            ghost.rectTransform.pivot = new Vector2(0, .5f);
            ghost.color = new Color(.86f, .53f, .48f, .65f);
            return ghost;
        }

        private static Image SDBuildHpLossBand(Image fill)
        {
            var band = SDGhost(fill);
            band.name = "DamageLoss";
            band.raycastTarget = false;
            band.gameObject.SetActive(false);
            return band;
        }

        private static string SDMultiplier(ScoreHandKind kind, bool formed = true)
        {
            if (kind == ScoreHandKind.Five && formed) return "900点";
            int multiplier = formed ? ScoreRules.Multiplier(kind) : ScoreRules.UnformedMultiplier;
            return "×" + multiplier.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The one-glance tag a role rail prints beside its name: its multiplier, the fixed 900,
        /// or Chance's reversal, which is named rather than numbered because it turns the damage
        /// around rather than adding to it.
        /// </summary>
        private static string SDRailMultiplier(ScoreHandKind kind)
        {
            if (kind == ScoreHandKind.Five) return "900点";
            if (kind == ScoreHandKind.Chance) return "逆転";
            return "×" + ScoreRules.Multiplier(kind).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A health fill/forecast/damage segment sprite authored at its own width.
        ///
        /// Stretching the full-bar sprite onto a narrower segment shrinks its corner radius with
        /// the width, so the segment's near-square corner pokes past the pill's rounded cap and
        /// the red band bleeds onto the table. Re-authoring at (rounded-down) the segment's own
        /// width keeps the corner radius at or larger than the pill's, which can only pull the
        /// segment inward. Rounding down to 8px steps keeps the sprite cache bounded.
        /// </summary>
        private static Sprite SDBarSegmentSprite(float width)
        {
            int quantized = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(8f, width) / 8f) * 8, 8, Mathf.RoundToInt(SDHpWidth));
            return UiFactory.RoundedSprite(quantized, Mathf.RoundToInt(SDHpThickness));
        }

        private static string SDCalculation(ScoreHandKind kind, int[] dice)
        {
            ScoreOffer offer = ScoreRules.Evaluate(kind, dice);
            if (kind == ScoreHandKind.Five && offer.IsFormed) return "900";
            int multiplier = offer.IsFormed ? ScoreRules.Multiplier(kind) : ScoreRules.UnformedMultiplier;
            return offer.BaseScore + " × " + multiplier.ToString(CultureInfo.InvariantCulture);
        }

        private void SDRefreshHud()
        {
            if (_sdEngine == null || _sdPlayerHpLabel == null) return;
            bool plan = !_sdRolling && !_sdCommitted && !SDModalOpen;
            ScoreForecast forecast = _sdCommitted ? new ScoreForecast(_sdEngine.LastRound.PlayerScore, _sdEngine.LastRound.NpcScore, _sdEngine.LastRound.IsReversed) : _sdEngine.Forecast(_sdSelectedKind, _sdSaveArmed);
            // The drop in a bar's line has one owner at a time: the shared health timeline while
            // health is moving, and the forecast while a role is still being chosen. The timeline
            // prints only the beat it is in, so a green heal never shares the line with a red drop.
            if (_sdHpAge < 0)
            {
                bool showForecast = !_sdRolling && _sdAttackAge < 0 && !_sdCommitted;
                _sdNpcDamageShown = showForecast ? forecast.DamageToNpc : 0;
                _sdPlayerDamageShown = showForecast ? forecast.DamageToPlayer : 0;
                // The heal is named next to the damage while the role is chosen, so keeping a
                // reroll visibly buys health before it is spent.
                int playerHeal = ScoreRules.HealFor(_sdEngine.PlayerHp, ScoreDuel.StartingHp, _sdEngine.RerollsLeft);
                int npcHeal = ScoreRules.HealFor(_sdEngine.NpcHp, _sdEngine.NpcMaxHp, _sdEngine.NpcRerollsLeft);
                _sdNpcHealShown = showForecast ? npcHeal : 0;
                _sdPlayerHealShown = showForecast ? playerHeal : 0;
                SDApplyHpLines();
            }
            int playerHealPreview = ScoreRules.HealFor(_sdEngine.PlayerHp, ScoreDuel.StartingHp, _sdEngine.RerollsLeft);
            int npcHealPreview = ScoreRules.HealFor(_sdEngine.NpcHp, _sdEngine.NpcMaxHp, _sdEngine.NpcRerollsLeft);
            SDPlaceGhost(_sdNpcGhost, _sdEngine.NpcHp, _sdEngine.NpcMaxHp, forecast.DamageToNpc, npcHealPreview, plan);
            SDPlaceGhost(_sdPlayerGhost, _sdEngine.PlayerHp, ScoreDuel.StartingHp, forecast.DamageToPlayer, playerHealPreview, plan);
            if (_sdRerollAllowanceRound != _sdEngine.Round)
            {
                _sdRerollAllowanceRound = _sdEngine.Round;
                _sdRerollAllowance = _sdEngine.RerollsLeft;
            }
            _sdRerollAllowance = Mathf.Max(_sdRerollAllowance, _sdEngine.RerollsLeft);
            for (int i = 0; i < 3; i++)
            {
                _sdRollPips[i].gameObject.SetActive(!_sdCommitted && i < _sdRerollAllowance);
                _sdRollPips[i].color = i < _sdEngine.RerollsLeft ? SDPositive : new Color(.62f, .66f, .67f, .5f);
            }
            for (int i = 0; i < 5; i++) _sdHoldMarks[i].gameObject.SetActive(plan && _sdEngine.Held[i] && !_sdDragAiming && _sdAttackAge < 0 && !_sdFxActive);
            _sdSaveSymbol.color = _sdSaveButton.interactable ? SDPaperInk : SDMuted;
            _sdSaveButton.transform.Find("SaveCount").GetComponent<Text>().color = _sdSaveSymbol.color;
            SDRefreshCarryPresentation();
        }

        /// <summary>
        /// Breathes the unspent reroll pips so the allowance that is still there reads at a
        /// glance. Only unspent pips move and the alpha stays near full: a spent pip's dim
        /// state is never mistaken for the pulse, and the pips stay legible on the leather.
        /// </summary>
        private void SDTickRollPipPulse(float dt)
        {
            if (_sdEngine == null) return;
            if (!_sdReducedMotion) _sdRollPipPulseAge += Mathf.Max(0, dt);
            // A slow, visible breath: the unused dot swells through scale and alpha together so
            // the remaining rerolls claim attention without ever flashing.
            float glow = _sdReducedMotion ? 1 : .5f + .5f * Mathf.Sin(_sdRollPipPulseAge * 4.2f);
            for (int i = 0; i < 3; i++)
            {
                Image pip = _sdRollPips[i];
                if (pip == null || !pip.gameObject.activeSelf) continue;
                Color color = pip.color;
                if (i < _sdEngine.RerollsLeft)
                {
                    // The dip stays past the readability floor (.9): the spent slot and the
                    // tabletop must keep reading even at the bottom of the breath.
                    color.a = .92f + .08f * glow;
                    pip.transform.localScale = Vector3.one * (.94f + .14f * glow);
                }
                pip.color = color;
            }
        }

        /// <summary>
        /// Forecasts the final position after both beats. The ghost spans pre-round health to the
        /// final value, so it includes the kept-reroll heal instead of showing the gross damage as
        /// if the heal had not happened. A net gain is green; a net loss keeps the warm red.
        /// </summary>
        private static void SDPlaceGhost(Image ghost, int hp, int max, int damage, int heal, bool visible)
        {
            int after = max > 0 ? HealthPresentation.After(hp, heal, damage, max) : hp;
            bool changed = visible && max > 0 && after != hp;
            ghost.gameObject.SetActive(changed);
            if (!changed) return;
            int low = Mathf.Min(hp, after);
            int high = Mathf.Max(hp, after);
            float start = Mathf.Clamp01((float)low / max);
            float end = Mathf.Clamp01((float)high / max);
            float width = SDHpWidth * (end - start);
            ghost.rectTransform.anchoredPosition = new Vector2(SDHpWidth * start, 0);
            ghost.rectTransform.sizeDelta = new Vector2(width, SDHpThickness);
            ghost.color = after > hp ? new Color(.48f, .82f, .64f, .65f) : new Color(.86f, .53f, .48f, .65f);
            // Authored at its own width so the forecast cap follows the pill's curve.
            ghost.sprite = SDBarSegmentSprite(width);
        }

        /// <summary>
        /// Applies one sampled <see cref="HealthPresentation.Frame"/> to a bar. Both duel modes
        /// call this same method so the heal-first/damage-second beat cannot diverge between them.
        /// </summary>
        private void SDApplyHealthFrame(Image fill, Image band, HealthPresentation.Frame frame,
            int maximum, Color baseColor)
        {
            SDSetTableHp(fill, frame.Hp, maximum);
            SDAnimateHpLoss(fill, band, frame.LossFrom, frame.LossTo, maximum, baseColor, frame.LossAlpha);
            HealMotes.Feed(fill, frame, maximum);
        }

        /// <summary>
        /// Test/legacy hook: samples the damage beat from full emphasis (alpha 1) to settled
        /// (alpha 0). The live presentation uses the absolute ages in <see cref="HealthPresentation"/>.
        /// </summary>
        private void SDShowHpDeltas(float alpha)
        {
            if (_sdEngine == null || _sdEngine.LastRound == null) return;
            float age = alpha <= 0
                ? HealthPresentation.End
                : Mathf.Lerp(HealthPresentation.DamageStart, HealthPresentation.End, 1f - Mathf.Clamp01(alpha));
            SDTickHpPresentation(age);
        }

        private void SDAnimateHpLoss(Image fill, Image band, float before, int after,
            int maximum, Color baseColor, float alpha)
        {
            if (band == null) return;
            bool active = before > after && alpha > .001f;
            band.gameObject.SetActive(active);
            float emphasis = active ? Mathf.Clamp01(alpha * 3) : 0;
            float loss = Mathf.Max(0, before - after);
            int tier = loss < 30 ? 0 : loss < 50 ? 1 : loss < 80 ? 2 : 3;
            float phase = 1 - Mathf.Clamp01(alpha);
            float pulse = _sdReducedMotion ? 0 : Mathf.Exp(-phase * 8) * Mathf.Abs(Mathf.Sin(phase * 24));
            // No outline rides the track: a squared rim around a rounded pill pushes red past
            // the corner caps, which is exactly the bleed the tracks existed to keep inside.
            fill.color = Color.Lerp(baseColor, new Color(1, .84f, .68f),
                active ? alpha * (_sdReducedMotion ? .15f : .62f) : 0);
            if (!active) return;
            float end = Mathf.Clamp01(before / Mathf.Max(1, maximum));
            float start = Mathf.Clamp01((float)after / Mathf.Max(1, maximum));
            float width = SDHpWidth * (end - start);
            band.rectTransform.anchoredPosition = new Vector2(SDHpWidth * start, 0);
            band.rectTransform.sizeDelta = new Vector2(width, SDHpThickness);
            // The drained slice is a pill too: its own-width cap cannot poke past the track.
            band.sprite = SDBarSegmentSprite(width);
            // Removed health cools from ivory contact to ember as the track drains.
            band.color = Color.Lerp(new Color(1, .47f, .28f, emphasis * .75f),
                new Color(1, .94f, .72f, emphasis), Mathf.Clamp01(alpha * alpha + pulse * .18f * tier));
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
            Transform volume = _canvasRoot.transform.Find("SoundVolume");
            if (volume != null) ((RectTransform)volume).anchoredPosition = new Vector2(451, -319);
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
