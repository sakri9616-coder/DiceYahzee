using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// How a match ends on screen: the freeze on each strike, the stamp on the finishing blow and
    /// the ceremony that opens the result. The rules decide every outcome before any of this
    /// draws; this partial only gives the outcome its weight.
    /// </summary>
    public sealed partial class GameFlow
    {
        private readonly ImpactHitStop _sdAttackStop = new ImpactHitStop();
        private readonly ImpactHitStop _sdHpStop = new ImpactHitStop();
        private KoStamp _sdKoStamp;
        private ResultCeremony _sdCeremony;
        private Text _sdStatBestValue, _sdStatBestKind, _sdStatDealtValue, _sdStatTakenValue;

        private KoStamp _onlineKoStamp;
        private ResultCeremony _onlineCeremony;
        private readonly ImpactHitStop _onlineStop = new ImpactHitStop();
        private bool _onlineResultWasShown;

        private static readonly Color SDVerdictWin = new Color32(246, 214, 140, 255);
        private static readonly Color SDVerdictLoss = new Color32(232, 128, 118, 255);

        private void SDBuildKoStamp()
        {
            if (_sdKoStamp == null) _sdKoStamp = KoStamp.Build(_sdBattle);
        }

        /// <summary>The freeze this round's strike earns, measured once from the settled round.</summary>
        private float SDImpactHold()
        {
            if (_sdEngine == null || _sdEngine.LastRound == null) return 0f;
            return ImpactHitStop.HoldFor(_sdImpactPunch, _sdEngine.Outcome != null);
        }

        private void SDResetFinale()
        {
            _sdAttackStop.Reset();
            _sdHpStop.Reset();
            if (_sdKoStamp != null) _sdKoStamp.Hide();
        }

        /// <summary>The strike has reached its bar. If it ended the match, stamp the verdict.</summary>
        private void SDOnImpactContact()
        {
            if (_sdEngine == null || _sdEngine.Outcome == null || _sdKoStamp == null) return;
            bool bothDown = _sdEngine.PlayerHp == 0 && _sdEngine.NpcHp == 0;
            MatchResult result = _sdEngine.Outcome.Result;
            Color color = result == MatchResult.PlayerWin ? SDVerdictWin : result == MatchResult.NpcWin ? SDVerdictLoss : SDMiracle;
            _sdKoStamp.Show(bothDown ? "DOUBLE K.O." : "K.O.", color);
        }

        /// <summary>
        /// The result screen as a ceremony: verdict, the match in three numbers, then the choice
        /// of what comes next. The three numbers are the ones a player tells someone afterwards -
        /// the biggest hit, what they dealt and what they took.
        /// </summary>
        private void SDBuildResultSummary()
        {
            _sdResultTitle.fontSize = 88;
            _sdResultDetail.fontSize = 22;
            _sdResultDetail.rectTransform.anchoredPosition = new Vector2(0, 126);
            _sdResultDetail.rectTransform.sizeDelta = new Vector2(1000, 96);
            _sdResultDetail.color = SDRailInk;
            Transform stats = UiFactory.CreateRect("Summary", _sdResult, new Vector2(720, 84), new Vector2(0, 40));
            SDBuildStatCard(stats, "BestHit", -244, "最大打点", out _sdStatBestValue, out _sdStatBestKind);
            SDBuildStatCard(stats, "Dealt", 0, "与えたダメージ", out _sdStatDealtValue, out _);
            SDBuildStatCard(stats, "Taken", 244, "受けたダメージ", out _sdStatTakenValue, out _);
            _sdCeremony = ResultCeremony.Attach(_sdResult, _sdResultTitle);
        }

        private void SDBuildStatCard(Transform parent, string name, float x, string caption, out Text value, out Text note)
        {
            Image card = UiFactory.CreatePanel(name, parent, new Vector2(224, 84), new Vector2(x, 0), new Color(.07f, .13f, .15f, .78f));
            card.sprite = UiFactory.RoundedSprite(224, 84);
            card.raycastTarget = false;
            var rim = card.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(.80f, .66f, .42f, .28f);
            rim.effectDistance = new Vector2(1, -1);
            SDText("Caption", card.transform, caption, 14, new Vector2(0, 26), new Vector2(210, 22), SDRailMuted).raycastTarget = false;
            value = SDText("Value", card.transform, "", 32, new Vector2(0, -2), new Vector2(210, 44), SDScoreGold);
            value.font = Resources.Load<Font>("Fonts/AoboshiOne-Regular") ?? value.font;
            value.verticalOverflow = VerticalWrapMode.Overflow;
            value.raycastTarget = false;
            SDAddMetallicScore(value, 2f);
            note = SDText("Note", card.transform, "", 13, new Vector2(0, -30), new Vector2(210, 20), SDRailMuted);
            note.raycastTarget = false;
        }

        private void SDPlayResultCeremony(MatchResult result)
        {
            if (_sdCeremony == null) return;
            int best = 0, dealt = 0, taken = 0;
            ScoreHandKind bestKind = ScoreHandKind.Chance;
            foreach (ScoreRound round in _sdEngine.History)
            {
                if (round.PlayerScore > best) { best = round.PlayerScore; bestKind = round.PlayerKind; }
                dealt += round.DamageToNpc;
                taken += round.DamageToPlayer;
            }
            _sdStatBestKind.text = best > 0 ? ScoreRules.Name(bestKind) : "—";
            _sdCeremony.Hide();
            _sdCeremony.CountUp(_sdStatBestValue, "", best, "", .55f);
            _sdCeremony.CountUp(_sdStatDealtValue, "", dealt, "", .63f);
            _sdCeremony.CountUp(_sdStatTakenValue, "", taken, "", .71f);
            _sdCeremony.Play(result == MatchResult.PlayerWin ? ResultCeremony.Mood.Victory :
                result == MatchResult.NpcWin ? ResultCeremony.Mood.Defeat : ResultCeremony.Mood.Miracle);
        }

        // ---------------------------------------------------------------- surfaces
        private RoundBanner _sdRoundBanner;

        /// <summary>
        /// The material pass over screens that were already built: modals become framed cards,
        /// health bars catch light like glass tubes, and the round counter learns to announce a
        /// new round. Runs once, after every screen exists.
        /// </summary>
        private void SDDressSurfaces()
        {
            foreach (RectTransform modal in new[] { _sdRules, _sdSheet, _sdMenu, _sdCredits, _sdPractice })
            {
                if (modal == null) continue;
                Transform panel = modal.Find("Panel");
                Transform title = modal.Find("Title");
                ModalChrome.Dress(modal, panel != null ? panel.GetComponent<Image>() : null,
                    title != null ? title.GetComponent<Text>() : null);
            }
            Surfaces.Shade<TubeShade>(_sdPlayerBar);
            Surfaces.Shade<TubeShade>(_sdNpcBar);
            _sdRoundBanner = RoundBanner.Attach(_sdRoundLabel);
        }

        private void SDAnnounceRound()
        {
            if (_sdRoundBanner != null && _sdEngine != null) _sdRoundBanner.Show();
        }

        // ---------------------------------------------------------------- online
        private void BuildOnlineFinale()
        {
            _onlineResultTitle.fontSize = 76;
            _onlineCeremony = ResultCeremony.Attach(_onlineResult, _onlineResultTitle);
            _onlineKoStamp = KoStamp.Build(_onlineRoot);
            Surfaces.Shade<TubeShade>(_onlineOwnHpFill);
            Surfaces.Shade<TubeShade>(_onlineEnemyHpFill);
            if (_onlineHelp != null)
            {
                Transform panel = _onlineHelp.Find("HelpPanel");
                ModalChrome.Dress(_onlineHelp, panel != null ? panel.GetComponent<Image>() : null, null);
            }
        }

        /// <summary>Called on every online refresh; plays the ceremony only on the frame the result opens.</summary>
        private void SyncOnlineCeremony(bool shown, bool finished)
        {
            if (_onlineCeremony == null) return;
            if (shown && !_onlineResultWasShown)
            {
                if (_onlineKoStamp != null) _onlineKoStamp.Hide();
                ResultCeremony.Mood mood = !finished || _onlineStopped ? ResultCeremony.Mood.Neutral :
                    _onlineState.winner == 2 ? ResultCeremony.Mood.Miracle :
                    _onlineState.winner == OnlineMe ? ResultCeremony.Mood.Victory : ResultCeremony.Mood.Defeat;
                _onlineCeremony.Play(mood);
            }
            else if (!shown && _onlineResultWasShown) _onlineCeremony.Hide();
            _onlineResultWasShown = shown;
        }

        private float OnlineImpactHold()
        {
            if (_onlineState == null || _onlineState.players == null) return 0f;
            int score = Mathf.Max(_onlineState.players[0].score, _onlineState.players[1].score);
            return ImpactHitStop.HoldFor(SDScorePulseTier(score), _onlineState.phase == (int)VersusPhase.Finished);
        }

        private void OnlineImpactContactStamp()
        {
            if (_onlineKoStamp == null || _onlineState == null || _onlineState.phase != (int)VersusPhase.Finished) return;
            Color color = _onlineState.winner == 2 ? SDMiracle : _onlineState.winner == OnlineMe ? SDVerdictWin : SDVerdictLoss;
            _onlineKoStamp.Show(_onlineState.winner == 2 ? "DOUBLE K.O." : "K.O.", color);
        }
    }
}
