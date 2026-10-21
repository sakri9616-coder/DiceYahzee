using ParvaMirabilia.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private SharedAttackPresenter _sdAttackPresenter;
        private readonly RectTransform[] _sdNpcDieTransforms = new RectTransform[5];
        private bool _sdNpcDiceCached;
        private int _sdImpactTier, _sdImpactDamage, _sdImpactPunch;
        private bool _sdImpactContact;
        private Text _sdImpactCounter, _sdImpactIncomingCounter;
        private const float SDImpactContactDelay = SharedAttackPresenter.ContactTime;
        private float SDImpactDuration => SharedAttackPresenter.Duration;

        private static Vector2 SDChargeOffset(float time, int index, float energy) => DuelPresentation.SDChargeOffset(time, index, energy);
        private static float SDChargeSign(float time, int index) => DuelPresentation.SDSign(time, index);

        private void SDBuildImpactPresentation()
        {
            if (_sdAttackPresenter != null) return;
            _sdAttackPresenter = new SharedAttackPresenter(_sdBattle);
            CacheNpcDieTransforms();
            // Two empty, input-transparent diagnostic text objects, built exactly once.
            // No scattered floating damage numbers: these stay blank and are retained only
            // so the opt-in smoke's references compile until the next stage rewrites it.
            if (_sdImpactCounter == null)
            {
                _sdImpactCounter = UiFactory.CreateText("SdImpactCounter", _sdBattle, "", 24, TextAnchor.MiddleCenter, UiFactory.Text, Vector2.zero, new Vector2(160, 40));
                _sdImpactCounter.raycastTarget = false;
                _sdImpactCounter.gameObject.SetActive(false);
            }
            if (_sdImpactIncomingCounter == null)
            {
                _sdImpactIncomingCounter = UiFactory.CreateText("SdImpactIncomingCounter", _sdBattle, "", 24, TextAnchor.MiddleCenter, UiFactory.Text, Vector2.zero, new Vector2(160, 40));
                _sdImpactIncomingCounter.raycastTarget = false;
                _sdImpactIncomingCounter.gameObject.SetActive(false);
            }
        }

        private void CacheNpcDieTransforms()
        {
            if (_sdNpcDiceCached || _sdNpcReveal == null) return;
            _sdNpcDiceCached = true;
            for (int i = 0; i < 5; i++)
                _sdNpcDieTransforms[i] = _sdNpcReveal.Find("NpcDie" + i) as RectTransform;
        }

        private void SDResetImpactPresentation()
        {
            if (_sdAttackPresenter != null) _sdAttackPresenter.Reset();
            _sdImpactContact = false;
            _sdImpactTier = 0; _sdImpactDamage = 0; _sdImpactPunch = 0;
            _sdAttackAge = -1;
            _sdLaunchPull = 0;
            if (_sdImpactCounter != null) _sdImpactCounter.text = "";
            if (_sdImpactIncomingCounter != null) _sdImpactIncomingCounter.text = "";
        }

        private void SDBeginImpactPresentation()
        {
            SDBuildImpactPresentation();
            CacheNpcDieTransforms();
            ScoreRound round = _sdEngine.LastRound;
            _sdImpactDamage = round.DamageToNpc;
            int outgoing = round.DamageToNpc;
            int incoming = round.DamageToPlayer;
            // Chance inverts the damage formula. The shared presenter only needs a signed
            // comparison to steer its attack, so when exactly one side declared Chance swap
            // the raw scores; both orderings preserve the real simultaneous HP loss.
            bool ownChance = round.PlayerKind == ScoreHandKind.Chance;
            bool enemyChance = round.NpcKind == ScoreHandKind.Chance;
            int ownAttack = round.PlayerScore;
            int enemyAttack = round.NpcScore;
            if (ownChance != enemyChance)
            {
                int swap = ownAttack; ownAttack = enemyAttack; enemyAttack = swap;
            }
            int strength = Mathf.Max(outgoing, incoming);
            // Damage bins follow the ×10 score economy: 250 / 450 / 700 of real HP loss.
            _sdImpactTier = strength < 250 ? 0 : strength < 450 ? 1 : strength < 700 ? 2 : 3;
            // Strength tier comes from the same raw own/enemy score bins the online adapter
            // uses, so identical rounds produce identical punch between single and online.
            int score = Mathf.Max(round.PlayerScore, round.NpcScore);
            _sdImpactPunch = SDScorePulseTier(score);
            Vector2 ownHp = SDHpTarget(_sdPlayerBar);
            Vector2 enemyHp = SDHpTarget(_sdNpcBar);
            // Reuse the cached die transforms directly, no per-attack RectTransform[5] garbage.
            _sdAttackPresenter.Begin(_sdFxDice, _sdNpcDieTransforms, ownAttack, enemyAttack, ownHp, enemyHp, _sdReducedMotion, _sdImpactPunch);
            _sdImpactContact = false;
        }

        private Vector2 SDHpTarget(Image fill)
        {
            if (fill == null) return Vector2.zero;
            // The full track center is stable: the colored fill's x-anchors move with HP so its
            // midpoint drifts. Use the parent track RectTransform for the full bar center instead.
            RectTransform track = fill.transform.parent as RectTransform;
            RectTransform source = track != null ? track : fill.rectTransform;
            Vector3 world = source.TransformPoint(source.rect.center);
            return _sdBattle.InverseTransformPoint(world);
        }

        private void SDAnimateImpactPresentation()
        {
            if (_sdAttackPresenter == null) return;
            _sdAttackPresenter.Sample(_sdAttackAge);
            if (!_sdImpactContact && _sdAttackPresenter.HasStruck)
            {
                _sdImpactContact = true;
                ScoreRound round = _sdEngine.LastRound;
                GameAudio.Play(SDImpactSound(round.DamageToNpc, round.DamageToPlayer));
            }
        }
    }
}
