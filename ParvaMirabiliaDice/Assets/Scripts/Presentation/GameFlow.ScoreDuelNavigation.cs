using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private readonly List<RaycastResult> _sdClickHits = new List<RaycastResult>();
        private PointerEventData _sdClickPointer;
        private float _sdNpcReplayAge = -1;
        private int _sdNpcReplayStep;

        internal bool SDCanAdvanceTable => OnlineActive ? OnlineCanAdvance : _sdEngine != null && _sdBattle.gameObject.activeInHierarchy &&
            _sdCommitted && _sdAttackAge < 0 && !SDModalOpen && _sdInputFrame != Time.frameCount;

        internal void SDTableClick()
        {
            if (OnlineActive) { if (OnlineCanAdvance) SendOnline(DuelNetworkAction.Continue); return; }
            if (!SDCanAdvanceTable) return;
            _sdInputFrame = Time.frameCount;
            SDAdvance();
        }

        private void SDHandleTableClick(Vector2 position)
        {
            if (!SDCanAdvanceTable || EventSystem.current == null) return;
            if (_sdClickPointer == null) _sdClickPointer = new PointerEventData(EventSystem.current);
            _sdClickPointer.Reset(); _sdClickPointer.position = position;
            _sdClickHits.Clear(); EventSystem.current.RaycastAll(_sdClickPointer, _sdClickHits);
            if (_sdClickHits.Count > 0)
            {
                var button = _sdClickHits[0].gameObject.GetComponentInParent<Button>();
                if (button != null && button.IsInteractable()) return;
            }
            SDTableClick();
        }

        private void SDStartNpcReplay()
        {
            _sdNpcReplayAge = 0; _sdNpcReplayStep = 0;
            SDShowNpcReplayStep(0);
        }

        private void SDShowNpcReplayStep(int step)
        {
            var roll = _sdEngine.PlannedNpcRolls[step];
            for (int i = 0; i < 5; i++)
            {
                bool kept = step > 0 && (roll.HeldMask & (1 << i)) != 0;
                if (!kept) _sdNpcDice[i].BeginRoll(i * .008f, .095f);
                _sdNpcDice[i].SetValue(roll.Dice[i]);
                _sdNpcDice[i].SetTint(DiceTint.Plain, kept);
            }
        }

        private void SDTickNpcReplay(float dt)
        {
            if (_sdNpcReplayAge < 0 || _sdEngine == null || SDModalOpen) return;
            _sdNpcReplayAge += dt;
            int step = Mathf.Min(_sdEngine.PlannedNpcRolls.Count - 1, Mathf.FloorToInt(_sdNpcReplayAge / .18f));
            if (step != _sdNpcReplayStep) { _sdNpcReplayStep = step; SDShowNpcReplayStep(step); }
            if (_sdNpcReplayAge >= .18f * _sdEngine.PlannedNpcRolls.Count)
            {
                _sdNpcReplayAge = -1;
                SDRefresh();
            }
        }
    }
}
