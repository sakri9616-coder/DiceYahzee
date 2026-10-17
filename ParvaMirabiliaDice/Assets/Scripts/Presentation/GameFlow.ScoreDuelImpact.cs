using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private ScoreHandEffectGraphic _sdImpactGeometry;
        private Text _sdImpactCounter, _sdImpactIncomingCounter;
        private Image _sdImpactFlash;
        private Image _sdImpactScreenFlash;
        private readonly Vector2[] _sdImpactNpcTextHomes = new Vector2[3];
        private readonly Vector3[] _sdImpactRotations = new Vector3[5];
        private readonly Vector3[] _sdImpactScales = new Vector3[5];
        private Vector2 _sdImpactNpcHome;
        private int _sdImpactTier, _sdImpactDamage;
        private bool _sdImpactContact, _sdImpactCaptured;
        private const float SDImpactContactDelay = .30f;
        private float SDImpactDuration => .76f + _sdImpactTier * .03f;

        private void SDBuildImpactPresentation()
        {
            if (_sdImpactGeometry != null) return;
            _sdImpactScreenFlash = UiFactory.CreateStretchPanel("ImpactScreenFlash", _sdBattle, Color.clear);
            _sdImpactScreenFlash.raycastTarget = false;
            var geometry = UiFactory.CreateRect("AttackImpact", _sdBattle, new Vector2(1100, 500), Vector2.zero);
            _sdImpactGeometry = geometry.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _sdImpactGeometry.raycastTarget = false;
            _sdImpactFlash = UiFactory.CreatePanel("ContactFlash", _sdBattle,
                new Vector2(410, 88), new Vector2(SDHandCenterX, 190), Color.clear);
            _sdImpactFlash.raycastTarget = false;
            _sdImpactCounter = SDText("ImpactDamage", _sdBattle, "", 40,
                new Vector2(SDHandCenterX, 35), new Vector2(440, 76), SDPositive);
            _sdImpactCounter.raycastTarget = false;
            _sdImpactCounter.fontStyle = FontStyle.Bold;
            _sdImpactIncomingCounter = SDText("IncomingDamage", _sdBattle, "", 30,
                new Vector2(SDHandCenterX, -221), new Vector2(340, 52), SDNegative);
            _sdImpactIncomingCounter.raycastTarget = false;
            _sdImpactIncomingCounter.fontStyle = FontStyle.Bold;
        }

        private void SDResetImpactPresentation()
        {
            if (_sdImpactCaptured && _sdBattle != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    var die = _sdBattle.Find("Die" + i) as RectTransform;
                    if (die == null) continue;
                    die.anchoredPosition = _sdDieHomes[i];
                    die.localEulerAngles = _sdImpactRotations[i];
                    die.localScale = _sdImpactScales[i];
                }
                if (_sdNpcReveal != null) _sdNpcReveal.anchoredPosition = _sdImpactNpcHome;
                _sdIntentLabel.rectTransform.anchoredPosition = _sdImpactNpcTextHomes[0];
                _sdNpcFormula.rectTransform.anchoredPosition = _sdImpactNpcTextHomes[1];
                _sdNpcScoreNumber.rectTransform.anchoredPosition = _sdImpactNpcTextHomes[2];
            }
            _sdImpactCaptured = false;
            _sdImpactContact = false;
            _sdAttackAge = -1;
            _sdLaunchPull = 0;
            if (_sdImpactGeometry != null) { _sdImpactGeometry.Clear(); _sdImpactGeometry.Flush(); }
            if (_sdImpactCounter != null) _sdImpactCounter.text = "";
            if (_sdImpactIncomingCounter != null) _sdImpactIncomingCounter.text = "";
            if (_sdImpactFlash != null) _sdImpactFlash.color = Color.clear;
            if (_sdImpactScreenFlash != null) _sdImpactScreenFlash.color = Color.clear;
        }

        private void SDBeginImpactPresentation()
        {
            SDBuildImpactPresentation();
            _sdImpactDamage = _sdEngine.LastRound.DamageToNpc;
            int strength = Mathf.Max(_sdImpactDamage, _sdEngine.LastRound.DamageToPlayer);
            _sdImpactTier = strength < 30 ? 0 : strength < 50 ? 1 : strength < 80 ? 2 : 3;
            _sdImpactCounter.fontSize = 40 + _sdImpactTier * 5;
            _sdImpactNpcHome = _sdNpcReveal.anchoredPosition;
            _sdImpactNpcTextHomes[0] = _sdIntentLabel.rectTransform.anchoredPosition;
            _sdImpactNpcTextHomes[1] = _sdNpcFormula.rectTransform.anchoredPosition;
            _sdImpactNpcTextHomes[2] = _sdNpcScoreNumber.rectTransform.anchoredPosition;
            _sdImpactContact = false;
            _sdImpactCaptured = true;
            for (int i = 0; i < 5; i++)
            {
                Transform die = _sdBattle.Find("Die" + i);
                _sdImpactRotations[i] = die.localEulerAngles;
                // Hover/press scale is transient; restoring it after the impact would leave the die enlarged.
                _sdImpactScales[i] = _sdFxDice[i] != null ? _sdFxBaseScale[i] : die.localScale;
            }
            _sdImpactCounter.text = "";
        }

        private void SDAnimateImpactPresentation()
        {
            float age = _sdAttackAge;
            float after = age - SDImpactContactDelay;
            _sdImpactGeometry.Clear();
            Color accent = _sdImpactDamage > 0 ? SDPositive : SDNegative;
            Vector2 contact = _sdImpactNpcHome;
            _sdImpactFlash.rectTransform.anchoredPosition = contact;
            if (age >= SDImpactContactDelay && !_sdImpactContact)
            {
                _sdImpactContact = true;
                var round = _sdEngine.LastRound;
                GameAudio.Play(round.DamageToNpc > 0 ? "hit" : round.DamageToPlayer > 0 ? "hurt" : "guard");
            }
            float hold = _sdReducedMotion ? .035f : .045f + .022f * _sdImpactTier;
            for (int i = 0; i < 5; i++)
            {
                var die = (RectTransform)_sdBattle.Find("Die" + i);
                Vector2 target = contact + new Vector2((i - 2) * 64, -10);
                Vector2 origin = _sdDieHomes[i] + Vector2.down * _sdLaunchPull;
                if (!_sdReducedMotion)
                {
                    if (age < SDImpactContactDelay)
                    {
                        float t = Mathf.Clamp01(age / SDImpactContactDelay);
                        // Preserve velocity at contact so the five dice read as a strike.
                        float eased = Mathf.Pow(t, 1.7f);
                        Vector2 position = Vector2.Lerp(origin, target, eased);
                        position.x += Mathf.Sin(t * Mathf.PI) * (i - 2) * 12;
                        die.anchoredPosition = position;
                        die.localScale = _sdImpactScales[i] * Mathf.Lerp(1.06f, .82f, eased);
                        die.localEulerAngles = _sdImpactRotations[i] + new Vector3(0, 0, (i - 2) * 12 * eased);
                        if (t > .18f)
                        {
                            Vector2 tail = Vector2.Lerp(origin, target, Mathf.Max(0, eased - .28f));
                            Color trail = accent; trail.a = .28f + .30f * t;
                            _sdImpactGeometry.Line(tail, position, 21 + _sdImpactTier * 7, trail);
                            trail.a = .9f;
                            _sdImpactGeometry.Line(Vector2.Lerp(tail, position, .25f), position, 3 + _sdImpactTier, trail);
                        }
                    }
                    else
                    {
                        float t = Mathf.Clamp01((after - hold) / (SDImpactDuration - SDImpactContactDelay - hold));
                        float eased = 1 - Mathf.Pow(1 - t, 3);
                        die.anchoredPosition = Vector2.Lerp(target, _sdDieHomes[i], eased) + Vector2.down * (Mathf.Sin(t * Mathf.PI) * 20);
                        float squash = after < hold ? .68f : Mathf.Lerp(.68f, 1, eased);
                        die.localScale = _sdImpactScales[i] * squash;
                        die.localEulerAngles = Vector3.Lerp(_sdImpactRotations[i] + new Vector3(0, 0, (i - 2) * 12), _sdImpactRotations[i], eased);
                    }
                }
            }
            if (after >= 0)
            {
                float fade = Mathf.Clamp01(1 - after / .30f);
                // Damage remains anchored to the affected HP bar; do not scatter a second number.
                _sdImpactCounter.text = "";
                _sdImpactIncomingCounter.text = "";
                if (!_sdReducedMotion)
                {
                    float radius = Mathf.Min(158, 36 + after * (245 + _sdImpactTier * 110));
                    Color glow = accent; glow.a = fade;
                    _sdImpactGeometry.Ellipse(contact, radius, radius * .42f, 5 + _sdImpactTier * 3, glow, 28);
                    if (_sdImpactTier >= 1) _sdImpactGeometry.Ellipse(contact, radius * .72f, radius * .28f, 3, glow, 22);
                    int spokes = 8 + _sdImpactTier * 6;
                    for (int k = 0; k < spokes; k++)
                    {
                        float a = k * Mathf.PI * 2 / spokes;
                        Vector2 ray = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .6f);
                        _sdImpactGeometry.Line(contact + ray * (20 + after * 80), contact + ray * Mathf.Min(170, 40 + after * (180 + _sdImpactTier * 95)), 4 + _sdImpactTier * 2, glow);
                    }
                    if (_sdImpactTier >= 2) _sdImpactGeometry.Line(contact + Vector2.left * radius, contact + Vector2.right * radius, 4 * fade, glow);
                    float kick = Mathf.Sin(Mathf.Clamp01(after / .30f) * Mathf.PI) * (12 + _sdImpactTier * 12);
                    float shake = Mathf.Sin(after * 110) * (3 + _sdImpactTier * 3) * fade;
                    _sdNpcReveal.anchoredPosition = _sdImpactNpcHome + new Vector2(shake, kick);
                    Vector2 textKick = new Vector2(shake * .7f, kick * .65f);
                    _sdIntentLabel.rectTransform.anchoredPosition = _sdImpactNpcTextHomes[0] + textKick;
                    _sdNpcFormula.rectTransform.anchoredPosition = _sdImpactNpcTextHomes[1] + textKick;
                    _sdNpcScoreNumber.rectTransform.anchoredPosition = _sdImpactNpcTextHomes[2] + textKick;
                    float flash = Mathf.Clamp01(1 - after / (.11f + _sdImpactTier * .012f));
                    _sdImpactFlash.color = new Color(1, .92f, .67f, flash * (.28f + _sdImpactTier * .13f));
                    _sdImpactScreenFlash.color = new Color(1, .88f, .58f, flash * (.035f + _sdImpactTier * .045f));
                }
            }
            _sdImpactGeometry.Flush();
        }
    }
}



