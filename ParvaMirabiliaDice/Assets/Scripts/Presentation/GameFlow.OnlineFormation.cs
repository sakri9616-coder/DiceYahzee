using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private ScoreFormationPresenter _onlineFormationPresenter;
        private System.Func<bool> _onlineFormationReducedMotion;
        private readonly RectTransform[] _onlineFormationDice = new RectTransform[5];
        private readonly int[] _onlineFormationValues = new int[5];
        private int _onlineFormationMatch = int.MinValue;
        private int _onlineFormationRound = int.MinValue;
        private int _onlineFormationSignature;
        private ScoreHandKind _onlineFormationAnnouncedKind;
        private int _onlineFormationAnnouncedScore;
        private bool _onlineFormationAnnounced;
        private bool _onlineFormationHasState;
        private bool _onlineFormationBuilt;

        private void SDBuildOnlineFormation()
        {
            if (_onlineFormationBuilt) return;
            if (_onlineRoot == null || _onlineOwnDice == null) return;
            int firstDie = _onlineRoot.childCount;
            for (int i = 0; i < 5; i++)
            {
                RectTransform die = _onlineOwnDice[i];
                if (die == null) return;
                _onlineFormationDice[i] = die;
                firstDie = Mathf.Min(firstDie, die.GetSiblingIndex());
            }
            if (_onlineFormationReducedMotion == null) _onlineFormationReducedMotion = SDIsReducedMotion;
            _onlineFormationPresenter = new ScoreFormationPresenter(
                _onlineRoot, _onlineFormationDice, _onlineFormationReducedMotion, firstDie);
            _onlineFormationBuilt = true;
        }

        private void SDTickOnlineFormation(float dt)
        {
            if (!_onlineFormationBuilt)
            {
                SDBuildOnlineFormation();
                if (!_onlineFormationBuilt) return;
            }
            if (_onlineState == null || _onlineStopped || _onlineLeaving || _onlineConnecting)
            {
                if (_onlineFormationHasState || _onlineFormationPresenter.Active)
                    SDResetOnlineFormation(true);
                return;
            }
            _onlineFormationHasState = true;
            if (_onlineState.match != _onlineFormationMatch || _onlineState.round != _onlineFormationRound)
            {
                _onlineFormationMatch = _onlineState.match;
                _onlineFormationRound = _onlineState.round;
                ClearOnlineFormationAnnouncement();
                if (_onlineFormationPresenter.Active) _onlineFormationPresenter.Reset();
                _onlineFormationSignature = 0;
            }
            if (!OnlineFormationEligible(out int signature))
            {
                if ((_onlineAttackPresenter == null || !_onlineAttackPresenter.Active) && _onlineFormationPresenter.Active) _onlineFormationPresenter.Reset();
                return;
            }
            if (signature != _onlineFormationSignature)
            {
                _onlineFormationSignature = signature;
                TryStartOnlineFormation();
            }
            _onlineFormationPresenter.Tick(dt);
        }

        private bool OnlineFormationEligible(out int signature)
        {
            signature = 0;
            if (_onlineState == null || _onlineState.players == null || _onlineState.players.Length < 2) return false;
            if (_onlineSession == null || !_onlineSession.IsReady) return false;
            if (_onlineRoot == null || !_onlineRoot.gameObject.activeInHierarchy) return false;
            if (_onlineConnecting || _onlineLeaving || _onlineStopped || _onlinePending || _onlineCommitQueued) return false;
            if (OnlineHelpOpen || _onlineDragging || _onlineAiming) return false;
            if (_onlineImpactAge >= 0) return false;
            if (_onlineAttackPresenter != null && _onlineAttackPresenter.Active) return false;
            int phase = _onlineState.phase;
            if (phase == (int)VersusPhase.Resolved || phase == (int)VersusPhase.Finished ||
                phase == (int)VersusPhase.Interrupted) return false;
            if (phase != (int)VersusPhase.Playing && phase != (int)VersusPhase.ChoosingRoles) return false;
            if (!OnlineCanChoose) return false;
            if (_onlineKind == (int)ScoreHandKind.Chance) return false;
            int me = OnlineMe;
            OnlinePlayer mine = _onlineState.players[me];
            if (mine == null || mine.submitted || mine.dice == null || mine.dice.Length < 5 ||
                mine.used == null || mine.used.Length < ScoreRules.HandCount) return false;
            for (int i = 0; i < 5; i++)
            {
                if (_onlineMine[i] == null || _onlineMine[i].IsRolling) return false;
            }
            if (OnlineRolling) return false;
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _onlineState.match;
                hash = hash * 31 + _onlineState.round;
                hash = hash * 31 + phase;
                hash = hash * 31 + me;
                hash = hash * 31 + _onlineKind;
                hash = hash * 31 + (_onlineRoleManual ? 1 : 0);
                for (int i = 0; i < 5; i++) hash = hash * 31 + mine.dice[i];
                for (int i = 0; i < ScoreRules.HandCount; i++) hash = hash * 31 + (mine.used[i] ? 1 : 0);
                signature = hash;
            }
            return true;
        }

        private void TryStartOnlineFormation()
        {
            if (_onlineFormationPresenter == null || _onlineState == null) return;
            OnlinePlayer mine = _onlineState.players[OnlineMe];
            if (mine == null || mine.dice == null || mine.used == null) return;
            for (int i = 0; i < 5; i++)
            {
                if (_onlineMine[i] == null || _onlineMine[i].IsRolling) return;
                _onlineFormationValues[i] = mine.dice[i];
            }
            if (!SDTryPreferredOffer(mine.dice, mine.used, true, out ScoreOffer best)) return;
            if (best.Kind == ScoreHandKind.Chance || !best.IsFormed || best.Mask == 0) return;
            if (_onlineFormationAnnounced && _onlineFormationAnnouncedKind == best.Kind &&
                best.Score <= _onlineFormationAnnouncedScore) return;
            _onlineFormationAnnounced = true;
            _onlineFormationAnnouncedKind = best.Kind;
            _onlineFormationAnnouncedScore = best.Score;
            GameAudio.Play("formation");
            _onlineFormationPresenter.Play(best, _onlineFormationValues);
        }

        private void SDResetOnlineFormation(bool forgetAnnouncement = true)
        {
            if (_onlineFormationPresenter != null) _onlineFormationPresenter.Reset();
            if (!forgetAnnouncement) return;
            ClearOnlineFormationAnnouncement();
            _onlineFormationSignature = 0;
            _onlineFormationMatch = int.MinValue;
            _onlineFormationRound = int.MinValue;
            _onlineFormationHasState = false;
        }

        private void SDRefreshOnlineFormation()
        {
            if (_onlineAttackPresenter != null && _onlineAttackPresenter.Active) return;
            if (_onlineFormationPresenter != null) _onlineFormationPresenter.Reset();
        }

        private void ClearOnlineFormationAnnouncement()
        {
            _onlineFormationAnnounced = false;
            _onlineFormationAnnouncedKind = default;
            _onlineFormationAnnouncedScore = 0;
        }
    }
}
