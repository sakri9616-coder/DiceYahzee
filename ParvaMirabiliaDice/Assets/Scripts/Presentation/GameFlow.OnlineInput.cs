using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private bool _onlineDragging, _onlineAiming, _onlineCommitQueued, _onlineRoleManual;
        private int _onlineDragIndex, _onlineOriginalHeld, _onlinePainted;
        private bool _onlinePaintValue;
        private Vector2 _onlineDragStart, _onlineDragLast;
        private float _onlineDragPull, _onlineWheelRemainder;
        private bool OnlineRolling
        {
            get { foreach (var die in _onlineMine) if (die != null && die.IsRolling) return true; return false; }
        }
        private bool OnlineCanChoose => _onlineSession != null && _onlineSession.IsReady && _onlineState != null &&
            (_onlineState.phase == (int)VersusPhase.Playing || _onlineState.phase == (int)VersusPhase.ChoosingRoles) &&
            !_onlineState.players[OnlineMe].submitted && !_onlineCommitQueued && !_onlinePending && !_onlineStopped && !OnlineHelpOpen && !OnlineRolling;
        private bool OnlineCanAdvance => OnlineActive && _onlineState != null && !_onlineStopped && !OnlineHelpOpen &&
            _onlineState.phase == (int)VersusPhase.Resolved && _onlineImpactAge < 0 && !_onlineState.ready[OnlineMe] && !_onlinePending;

        private Vector2 OnlineLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_onlineRoot, screen, null, out Vector2 local);
            return local;
        }

        private void OnlinePointerBegin(int index, Vector2 screen)
        {
            if (!OnlineCanChoose) return;
            SDRefreshOnlineFormation();
            _onlineDragging = true; _onlineAiming = false; _onlineDragIndex = index;
            _onlineDragStart = _onlineDragLast = OnlineLocal(screen);
            _onlineOriginalHeld = _onlineHeld; _onlinePainted = 0;
            _onlinePaintValue = index >= 0 && (_onlineHeld & (1 << index)) == 0;
        }

        private void OnlinePointerMove(Vector2 screen)
        {
            if (!_onlineDragging) return;
            if (!OnlineCanChoose) { CancelOnlinePointer(); return; }
            Vector2 point = OnlineLocal(screen), delta = point - _onlineDragStart, previous = _onlineDragLast;
            _onlineDragLast = point;
            bool wasAiming = _onlineAiming;
            _onlineAiming = delta.y < -20 && -delta.y > Mathf.Abs(delta.x) * .65f;
            if (_onlineAiming)
            {
                _onlineHeld = _onlineOriginalHeld;
                _onlineDragPull = Mathf.Clamp(-delta.y * .65f, 0, 55);
                for (int i = 0; i < 5; i++) _onlineMine[i].SetPosition(OnlineDieHome(i, true) + Vector2.down * _onlineDragPull);
            }
            else
            {
                _onlineDragPull = 0;
                if (wasAiming) _onlinePainted = 0;
                if (OnlineRerolling && _onlineDragIndex >= 0)
                    for (int i = 0; i < 5; i++)
                        if ((i == _onlineDragIndex || SDSegmentHitsDie(previous, point, OnlineDieHome(i, true))) && (_onlinePainted & (1 << i)) == 0)
                        {
                            _onlinePainted |= 1 << i;
                            if (_onlinePaintValue) _onlineHeld |= 1 << i; else _onlineHeld &= ~(1 << i);
                        }
                RefreshOnline();
            }
        }

        private void OnlinePointerEnd(Vector2 screen)
        {
            if (!_onlineDragging) return;
            OnlinePointerMove(screen);
            if (!_onlineDragging) return;
            Vector2 delta = OnlineLocal(screen) - _onlineDragStart;
            bool commit = _onlineAiming && -delta.y >= SDFlickDistance && OnlineCanChoose;
            if (_onlineAiming) _onlineHeld = _onlineOriginalHeld;
            _onlineDragging = _onlineAiming = false; _onlineDragPull = 0;
            for (int i = 0; i < 5; i++) _onlineMine[i].SetPosition(OnlineDieHome(i, true));
            if (commit) CommitOnlineHand(); else RefreshOnline();
        }

        private void CancelOnlinePointer()
        {
            if (_onlineDragging) _onlineHeld = _onlineOriginalHeld;
            _onlineDragging = _onlineAiming = false; _onlineDragPull = 0;
            for (int i = 0; i < 5; i++) if (_onlineMine[i] != null) _onlineMine[i].SetPosition(OnlineDieHome(i, true));
        }

        private void CommitOnlineHand()
        {
            if (!OnlineCanChoose) return;
            SDRefreshOnlineFormation();
            if (_onlineState.phase == (int)VersusPhase.ChoosingRoles) SendOnline(DuelNetworkAction.Submit);
            else
            {
                _onlineCommitQueued = true;
                if (!_onlineState.players[OnlineMe].rerollsFinished) SendOnline(DuelNetworkAction.FinishRerolls);
                RefreshOnline();
            }
        }

        private void SelectOnlineSuggestedRole(bool newRound)
        {
            if (_onlineState == null || _onlineState.players[OnlineMe].submitted) return;
            var mine = _onlineState.players[OnlineMe];
            if (newRound) _onlineRoleManual = false;
            if (!_onlineRoleManual && SDTryPreferredOffer(mine.dice, mine.used, false, out var best)) _onlineKind = (int)best.Kind;
            if (SDTryPreferredOffer(mine.dice, mine.used, true, out var formed) && formed.Score > ScoreRules.Evaluate((ScoreHandKind)_onlineKind, mine.dice).Score)
            { _onlineKind = (int)formed.Kind; _onlineRoleManual = false; }
        }

        private void OnlineWheel(float delta)
        {
            if (!OnlineCanChoose || _onlineDragging || Input.GetMouseButton(0) || Input.GetMouseButton(1)) return;
            if (delta * _onlineWheelRemainder < 0) _onlineWheelRemainder = 0;
            _onlineWheelRemainder += delta;
            int count = Mathf.FloorToInt(Mathf.Abs(_onlineWheelRemainder) + .00001f);
            int direction = _onlineWheelRemainder > 0 ? -1 : 1;
            _onlineWheelRemainder += count * direction;
            for (int i = 0; i < count; i++) ChangeOnlineRole(direction);
            if (count > 0) RefreshOnline();
        }
    }
}
