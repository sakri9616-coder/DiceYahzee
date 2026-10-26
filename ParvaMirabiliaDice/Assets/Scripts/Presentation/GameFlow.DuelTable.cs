using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        // Both controllers build the same table pieces. Only their state/input adapters differ.
        private Image SDBuildTableBackground(Transform root)
        {
            var table = UiFactory.CreateStretchPanel("Table", root, new Color32(12, 21, 25, 255));
            var grain = UiFactory.CreateStretch("Leather", table.transform).gameObject.AddComponent<RawImage>();
            grain.texture = Resources.Load<Texture2D>("Art/Textures/leather-white");
            grain.color = new Color(.09f, .16f, .18f, .28f);
            grain.uvRect = new Rect(0, 0, 2.5f, 1.5f); grain.raycastTarget = false;
            var light = UiFactory.CreateStretchPanel("TableLight", root, Color.white);
            light.sprite = UiFactory.VignetteSprite; light.color = new Color(1, 1, 1, .22f); light.raycastTarget = false;
            TableAmbience.Build(root);
            return table;
        }

        private Button SDBuildCarryButton(Transform root, string name, UnityAction action, out Image symbol)
        {
            var button = SDButton(name, root, new Vector2(0, -232), new Vector2(204, 48), "", 19, action, true);
            DiceYahzeeVisuals.StyleButton(button, true);
            button.GetComponent<ButtonPalette>().Face = DiceYahzeeVisuals.Brass;
            button.GetComponent<ButtonPalette>().Label = SDPaperInk;
            symbol = SDIcon("SaveSymbol", button.transform, "icon-floppy", Vector2.zero, 30, SDPaperInk);
            SDText("SaveCount", button.transform, "", 16, new Vector2(82, 9), new Vector2(22, 27), SDPaperInk);
            return button;
        }
        private DiceView SDBuildTableDie(Transform root, int index, float size, string name, Vector2 home)
        {
            var die = new DiceView(root, index, size, name, new Color32(255, 249, 232, 255));
            die.SetPosition(home);
            DiceYahzeeVisuals.StyleDie(root.Find(name));
            return die;
        }

        private Text SDBuildScoreNumber(string name, Transform root, Vector2 position, bool player)
        {
            var label = SDText(name, root, "", player ? 64 : 50, position,
                player ? new Vector2(240, 116) : new Vector2(240, 46), SDScoreGold);
            label.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? label.font;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            SDAddMetallicScore(label, player ? 3.2f : 2.5f);
            return label;
        }

        private Button SDBuildRoleRow(string name, Transform root, Vector2 position, ScoreHandKind kind,
            UnityAction select, out Text value, out Image marker)
        {
            var button = SDButton(name, root, position, new Vector2(174, 34), "", 14, select);
            button.GetComponent<Image>().sprite = null;
            button.GetComponent<Shadow>().enabled = false;
            SDText("Name", button.transform, ScoreRules.Name(kind), 16, new Vector2(-23, 0),
                new Vector2(116, 32), SDRailInk, TextAnchor.MiddleLeft).horizontalOverflow = HorizontalWrapMode.Overflow;
            SDText("Multiplier", button.transform, SDRailMultiplier(kind), 11, new Vector2(34, 0),
                new Vector2(34, 28), SDRailMuted, TextAnchor.MiddleRight).horizontalOverflow = HorizontalWrapMode.Overflow;
            value = SDText("Value", button.transform, "", 15, new Vector2(68, 0),
                new Vector2(30, 32), SDRailInk, TextAnchor.MiddleRight);
            value.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? value.font;
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            marker = UiFactory.CreatePanel("Selected", button.transform, new Vector2(3, 30), new Vector2(-84, 0), SDPositive);
            marker.raycastTarget = false;
            var rule = UiFactory.CreatePanel("RowRule", root, new Vector2(186, 1), position + Vector2.down * 19, SDRailRule);
            rule.raycastTarget = false;
            return button;
        }

        private static void SDSetTableHp(Image fill, float hp, float maximum)
        {
            float width = SDHpWidth * Mathf.Clamp01(hp / Mathf.Max(1, maximum));
            fill.rectTransform.sizeDelta = new Vector2(width, SDHpThickness);
            fill.sprite = SDBarSegmentSprite(width);
        }

        private static string SDImpactSound(int outgoing, int incoming)
            => outgoing > 0 ? "hit" : incoming > 0 ? "hurt" : "guard";

        private static bool SDTryPreferredOffer(int[] dice, System.Collections.Generic.IReadOnlyList<bool> used,
            bool formedOnly, out ScoreOffer best)
        {
            best = default; bool found = false;
            for (int i = 0; i < ScoreRules.HandCount; i++)
            {
                if (i == (int)ScoreHandKind.Chance || used[i]) continue;
                var offer = ScoreRules.Evaluate((ScoreHandKind)i, dice);
                if (formedOnly && !offer.IsFormed) continue;
                if (!found || offer.Score > best.Score) { found = true; best = offer; }
            }
            return found;
        }

        private Text SDBuildResultTitle(Transform root)
        {
            var title = SDText("Title", root, "", 48, new Vector2(0, 205), new Vector2(1000, 80), SDPositive);
            title.verticalOverflow = VerticalWrapMode.Overflow;
            title.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? title.font;
            SDAddScoreEdge(title);
            return title;
        }

        private void SDBuildResultActions(Transform root, UnityAction rematch, UnityAction back,
            string backName, string backLabel, out Button rematchButton, out Button backButton)
        {
            rematchButton = SDButton("Rematch", root, new Vector2(0, -130), new Vector2(342, 60), "再戦", 23, rematch, true);
            backButton = SDButton(backName, root, new Vector2(0, -213), new Vector2(300, 48), backLabel, 19, back);
        }
    }
}
