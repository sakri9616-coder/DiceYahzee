using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private readonly Text[] _sdNpcRoleValues = new Text[9];
        private readonly Text[] _sdNpcRoleChecks = new Text[9];
        private readonly Image[] _sdNpcRoleMarkers = new Image[9];

        private void SDBuildNpcScorecard()
        {
            var panel = UiFactory.CreatePanel("NpcScorecard", _sdBattle,
                new Vector2(SDRolePanelWidth, SDRolePanelHeight), new Vector2(-SDRolePanelX, 20), Color.clear);
            panel.sprite = null;
            panel.raycastTarget = false;
            SDText("Heading", panel.transform, "相手の役", 20, new Vector2(-38, 164), new Vector2(112, 28), SDRailInk, TextAnchor.MiddleLeft);
            var headRule = UiFactory.CreatePanel("HeadRule", panel.transform, new Vector2(174, 1), new Vector2(0, 143), SDRailRule);
            headRule.raycastTarget = false;
            for (int i = 0; i < 9; i++)
            {
                var row = UiFactory.CreateRect("NpcCategory" + i, panel.transform, new Vector2(174, 34), new Vector2(0, 128 - i * 37));
                SDText("Name", row, ScoreRules.Name((ScoreHandKind)i), 16, new Vector2(-24, 0), new Vector2(114, 32), SDRailInk, TextAnchor.MiddleLeft);
                _sdNpcRoleChecks[i] = SDText("Used", row, "", 13, new Vector2(44, 0), new Vector2(18, 30), SDRailMuted, TextAnchor.MiddleCenter);
                _sdNpcRoleValues[i] = SDText("Value", row, "—", 18, new Vector2(70, 0), new Vector2(32, 32), SDRailInk, TextAnchor.MiddleRight);
                _sdNpcRoleValues[i].font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? _sdNpcRoleValues[i].font;
                _sdNpcRoleMarkers[i] = UiFactory.CreatePanel("Intent", row, new Vector2(3, 28), new Vector2(-84, 0), SDPositive);
                _sdNpcRoleMarkers[i].raycastTarget = false;
                var rule = UiFactory.CreatePanel("RowRule", panel.transform, new Vector2(168, 1), new Vector2(0, 109 - i * 37), SDRailRule);
                rule.raycastTarget = false;
                foreach (Graphic graphic in row.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            }
        }

        private void SDRefreshNpcScorecard(bool npcRolling)
        {
            for (int i = 0; i < 9; i++)
            {
                bool used = _sdEngine.NpcUsed[i];
                int score = 0;
                if (used)
                    foreach (ScoreRound round in _sdEngine.History)
                        if ((int)round.NpcKind == i) score = round.NpcScore;
                _sdNpcRoleValues[i].text = used ? score.ToString() : "—";
                _sdNpcRoleValues[i].color = used ? SDRailInk : SDRailMuted;
                _sdNpcRoleChecks[i].text = used ? "✓" : "";
                _sdNpcRoleMarkers[i].gameObject.SetActive(!npcRolling && (int)_sdEngine.Intent.Kind == i);
            }
        }
    }
}
