using UnityEngine;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private ScoreHandEffectGraphic _sdGestureGraphic, _sdPracticeGestureGraphic;
        private Text _sdPracticeGestureText;
        private Image _sdGuideMouse;
        private Image _sdWheelMouse;
        private ScoreHandEffectGraphic _sdWheelGraphic;
        private Sprite _sdMouseLeft, _sdMouseRight;
        private const float SDGuideX = 350;
        private float _sdGuideAge;
        private bool _sdPracticeDragging;
        private Vector2 _sdPracticeDragStart;
        private readonly Vector2[] _sdPracticeHomes = new Vector2[5];
        private bool SDPracticeCorrectSelection => _sdPracticeHeld[0] && _sdPracticeHeld[1] &&
            !_sdPracticeHeld[2] && !_sdPracticeHeld[3] && !_sdPracticeHeld[4];

        private static void SDBuildDiePointerSurface(Transform die)
        {
            // A solid rectangular input surface includes the face, pips and rounded corners.
            // It follows the rendered die during enlargement; no decoration owns its input.
            var root = (RectTransform)die;
            var target = UiFactory.CreateRect("PointerSurface", die, root.sizeDelta + Vector2.one * 14, Vector2.zero);
            var image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            image.canvasRenderer.cullTransparentMesh = false;
            die.Find("Ring").GetComponent<Image>().raycastTarget = false;
        }

        private static ScoreHandEffectGraphic SDCreateGuide(Transform parent)
        {
            var root = UiFactory.CreateStretch("GestureGuide", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var graphic = root.gameObject.AddComponent<ScoreHandEffectGraphic>();
            graphic.raycastTarget = false;
            root.gameObject.SetActive(false);
            return graphic;
        }

        private void SDBuildGestureGuides()
        {
            var surface = UiFactory.CreateStretchPanel("TablePointerSurface", _sdBattle, Color.clear);
            surface.raycastTarget = true;
            surface.canvasRenderer.cullTransparentMesh = false;
            surface.transform.SetAsFirstSibling();
            _sdBattle.gameObject.AddComponent<ScoreDuelPointer>().Initialize(this, -1);
            _sdGestureGraphic = SDCreateGuide(_sdBattle);
            _sdMouseLeft = DiceYahzeeVisuals.LoadSprite("Art/DiceYahzee/input-mouse-left");
            _sdMouseRight = DiceYahzeeVisuals.LoadSprite("Art/DiceYahzee/input-mouse-right");
            _sdGuideMouse = SDIcon("MouseInput", _sdGestureGraphic.transform, "input-mouse-left", new Vector2(SDGuideX, 25), 36, SDPositive);
            _sdWheelMouse = _sdBattle.Find("ScoreChoices/WheelSymbol").GetComponent<Image>();
            var wheelRoot = UiFactory.CreateStretch("WheelMotion", _sdBattle.Find("ScoreChoices"));
            _sdWheelGraphic = wheelRoot.gameObject.AddComponent<ScoreHandEffectGraphic>();
            _sdWheelGraphic.raycastTarget = false;
            _sdInstruction.gameObject.SetActive(false);
        }

        private void SDBuildPracticeGestureGuide()
        {
            _sdPractice.gameObject.AddComponent<ScoreDuelPointer>().Initialize(this, -1, true);
            _sdPracticeGestureText = SDText("GestureInstruction", _sdPractice, "", 18,
                new Vector2(0, -155), new Vector2(800, 40), SDPositive);
            _sdPracticeGestureGraphic = SDCreateGuide(_sdPractice);
        }

        private void SDRefreshGestureInstruction()
        {
            _sdInstruction.text = "";
            _sdInstruction.gameObject.SetActive(false);
        }

        private void SDTickGestureGuides(float dt)
        {
            _sdGuideAge += dt;
            bool next = SDCanAdvanceTable;
            bool battle = next || (SDCanPlan && !(_sdFxActive && _sdFxTier >= 2));
            _sdGestureGraphic.gameObject.SetActive(battle);
            if (battle)
            {
                int held = 0;
                foreach (bool value in _sdEngine.Held) if (value) held++;
                bool reroll = held == 3 && _sdEngine.RerollsLeft > 0 && !_sdDragAiming;
                _sdGuideMouse.sprite = reroll ? _sdMouseRight : _sdMouseLeft;
                float phase = Mathf.Repeat(_sdGuideAge, 1.8f) / 1.8f;
                float down = Mathf.SmoothStep(0, 1, Mathf.Clamp01((phase - .12f) / .56f));
                float visibility = _sdReducedMotion || reroll || next ? 1 :
                    Mathf.Min(Mathf.Clamp01(phase / .08f), 1 - Mathf.Clamp01((phase - .80f) / .12f));
                float pull = _sdReducedMotion || reroll || next ? 0 : 18 * down;
                float press = _sdReducedMotion ? 0 : Mathf.Sin(Mathf.Clamp01(phase / .24f) * Mathf.PI);
                _sdGuideMouse.rectTransform.anchoredPosition = new Vector2(next ? SDHandCenterX : SDGuideX, (next ? -211 : 25) - pull);
                _sdGuideMouse.rectTransform.localScale = Vector3.one * (1 - press * .10f);
                Color mouseColor = Color.Lerp(SDMuted, SDPositive, _sdReducedMotion ? 1 : .4f + .6f * press);
                mouseColor.a *= visibility;
                _sdGuideMouse.color = mouseColor;
                if (next)
                {
                    _sdGestureGraphic.Clear();
                    var c = SDPositive; c.a = _sdReducedMotion ? .65f : (1 - phase) * .65f;
                    _sdGestureGraphic.Ring(new Vector2(SDHandCenterX, -211), 23 + phase * 12, 1.6f, c, 30);
                    _sdGestureGraphic.Flush();
                }
                else SDDrawGestureGuide(_sdGestureGraphic, new Vector2(SDGuideX, -35), reroll);
            }
            SDTickWheelGuide();
            bool practice = _sdPractice.gameObject.activeInHierarchy && !_sdPracticeRolling && _sdPracticeStep < 2;
            _sdPracticeGestureGraphic.gameObject.SetActive(practice);
            _sdPracticeGestureText.gameObject.SetActive(false);
            if (practice && (_sdPracticeStep == 1 || SDPracticeCorrectSelection)) SDDrawGestureGuide(_sdPracticeGestureGraphic,
                new Vector2(-345, _sdPracticeStep == 0 ? -155 : 74), _sdPracticeStep == 0);
            else if (practice)
            {
                _sdPracticeGestureGraphic.Clear();
                float pulse = _sdReducedMotion ? 0 : Mathf.Sin(_sdGuideAge * 4) * 3;
                for (int i = 0; i < 5; i++)
                {
                    if (_sdPracticeHeld[i] == (i < 2)) continue;
                    Vector2 target = new Vector2((i - 2) * 120, 142 + pulse);
                    _sdPracticeGestureGraphic.Line(target + new Vector2(-6, 4), target, 2, SDPositive);
                    _sdPracticeGestureGraphic.Line(target, target + new Vector2(6, 4), 2, SDPositive);
                    _sdPracticeGestureGraphic.Ring(target + new Vector2(0, 16), 5, 1.5f, SDPositive, 16);
                }
                _sdPracticeGestureGraphic.Flush();
            }
        }

        private void SDDrawGestureGuide(ScoreHandEffectGraphic graphic, Vector2 origin, bool reroll)
        {
            graphic.Clear();
            float t = Mathf.Repeat(_sdGuideAge, 1.6f) / 1.6f;
            Color color = reroll ? SDPositive : new Color32(98, 199, 153, 255);
            color.a = _sdReducedMotion ? .7f : .45f + Mathf.Sin(t * Mathf.PI) * .5f;
            if (reroll)
            {
                if (graphic == _sdGestureGraphic)
                {
                    graphic.Ring(_sdGuideMouse.rectTransform.anchoredPosition, 24 + (_sdReducedMotion ? 0 : t*7), 1.8f, color, 24);
                    Vector2 button = _sdGuideMouse.rectTransform.anchoredPosition + new Vector2(5, 7);
                    graphic.Line(button + Vector2.down * 4, button + Vector2.up * 4, 4, color);
                    graphic.Flush();
                    return;
                }
                graphic.Ellipse(origin, 12, 19, 1.8f, color, 20);
                graphic.Line(origin + new Vector2(0, 19), origin, 1.4f, color);
                // The right half lights up; the mouse body itself remains a static landmark.
                for (int i = 0; i < 5; i++) graphic.Line(origin + new Vector2(2 + i * 2, 3), origin + new Vector2(2 + i * 2, 13), 1.8f, color);
                graphic.Ring(origin, 23 + (_sdReducedMotion ? 0 : t * 9), 1.2f, color, 22);
            }
            else
            {
                float downPhase = Mathf.Repeat(_sdGuideAge, 1.8f) / 1.8f;
                float travel = _sdReducedMotion ? 16 : 42 * Mathf.SmoothStep(0, 1,
                    Mathf.Clamp01((downPhase - .12f) / .56f));
                if (!_sdReducedMotion)
                    color.a *= Mathf.Min(Mathf.Clamp01(downPhase / .08f),
                        1 - Mathf.Clamp01((downPhase - .80f) / .12f));
                Vector2 ghost = origin + Vector2.down * travel;
                for (int arrow = 0; arrow < 3; arrow++)
                {
                    Color cue = color; cue.a *= 1 - arrow * .22f;
                    Vector2 at = ghost + Vector2.up * (arrow * 15);
                    graphic.Line(at + new Vector2(-9, 4), at + new Vector2(0, -5), 3, cue);
                    graphic.Line(at + new Vector2(0, -5), at + new Vector2(9, 4), 3, cue);
                }
                if (graphic == _sdGestureGraphic)
                {
                    Vector2 at = _sdGuideMouse.rectTransform.anchoredPosition;
                    graphic.Line(at + new Vector2(-5, 3), at + new Vector2(-5, 11), 3.5f, color);
                }
                if (_sdDragAiming || _sdPracticeDragging)
                {
                    float progress = _sdPracticeDragging ? 1 : Mathf.Clamp01((_sdDragStart.y - _sdDragLast.y) / SDFlickDistance);
                    Vector2 center = _sdPracticeDragging ? new Vector2(0,-95) : new Vector2(SDHandCenterX,-114);
                    for (int i=0; i<32*progress; i++)
                    {
                        float a=(i/32f*360-90)*Mathf.Deg2Rad, b=((i+1)/32f*360-90)*Mathf.Deg2Rad;
                        graphic.Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*18, center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*18, 2.2f, SDPositive);
                    }
                    if(progress>=1) { graphic.Line(center+new Vector2(-6,-2),center+new Vector2(0,4),2.4f,SDPositive); graphic.Line(center+new Vector2(0,4),center+new Vector2(6,-2),2.4f,SDPositive); }
                }
            }
            graphic.Flush();
        }

        private void SDTickWheelGuide()
        {
            if (_sdWheelGraphic == null) return;
            _sdWheelGraphic.gameObject.SetActive(SDCanPlan);
            if (!SDCanPlan) return;
            _sdWheelGraphic.Clear();
            float motion = _sdReducedMotion ? 0 : Mathf.Sin(_sdGuideAge * 3.8f) * 4;
            Vector2 at = _sdWheelMouse.rectTransform.anchoredPosition;
            Color ink = SDRailInk; ink.a = .8f;
            _sdWheelGraphic.Line(at + new Vector2(0, 2 + motion), at + new Vector2(0, 7 + motion), 2.5f, ink);
            for (int direction = -1; direction <= 1; direction += 2)
            {
                Vector2 tip = at + Vector2.up * (direction * (22 + Mathf.Abs(motion) * .5f));
                _sdWheelGraphic.Line(tip + new Vector2(-4, -direction * 4), tip, 1.6f, ink);
                _sdWheelGraphic.Line(tip, tip + new Vector2(4, -direction * 4), 1.6f, ink);
            }
            _sdWheelGraphic.Flush();
        }

        internal void SDPracticePointerBegin(Vector2 screen)
        {
            if (!_sdPractice.gameObject.activeInHierarchy || _sdPracticeStep != 1 || _sdPracticeRolling) return;
            _sdPracticeDragging = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_sdPractice, screen, null, out _sdPracticeDragStart);
            for (int i = 0; i < 5; i++) _sdPracticeHomes[i] = ((RectTransform)_sdPractice.Find("PracticeDie" + i)).anchoredPosition;
        }
        internal void SDPracticePointerMove(Vector2 screen)
        {
            if (!_sdPracticeDragging) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_sdPractice, screen, null, out Vector2 point);
            float pull = Mathf.Clamp((_sdPracticeDragStart.y - point.y) * .65f, 0, 55);
            for (int i = 0; i < 5; i++) ((RectTransform)_sdPractice.Find("PracticeDie" + i)).anchoredPosition = _sdPracticeHomes[i] + Vector2.down * pull;
            _sdPracticeGestureText.text = "";
        }
        internal void SDPracticePointerEnd(Vector2 screen)
        {
            if (!_sdPracticeDragging) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_sdPractice, screen, null, out Vector2 point);
            bool fire = _sdPractice.gameObject.activeInHierarchy && _sdPracticeStep == 1 && !_sdPracticeRolling &&
                _sdPracticeDragStart.y - point.y >= SDFlickDistance;
            SDCancelPracticePointer();
            if (fire) SDPracticeCommit();
        }
        private void SDCancelPracticePointer()
        {
            if (!_sdPracticeDragging) return;
            _sdPracticeDragging = false;
            for (int i = 0; i < 5; i++) ((RectTransform)_sdPractice.Find("PracticeDie" + i)).anchoredPosition = _sdPracticeHomes[i];
            _sdPracticeGestureText.text = "";
        }
    }
}
