using ParvaMirabilia.Core;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private bool _sdDragging, _sdDragHoldValue, _sdDragAiming;
        private int _sdDragIndex;
        private Vector2 _sdDragStart, _sdDragLast;
        private readonly bool[] _sdDragOriginal = new bool[5];
        private readonly bool[] _sdDragPainted = new bool[5];
        private readonly Vector2[] _sdDieHomes = new Vector2[5];
        private float _sdAttackAge = -1;
        // Opt-in smoke/test-only age freeze. Null in ordinary play; the ticker then advances
        // normally, but a capture can pin one authored phase across render boundaries.
        private float? _sdSmokeAttackAgeOverride;
        private float _sdWheelRemainder;
        private const float SDFlickDistance = 28;
        private float _sdLaunchPull;

        private bool SDCanPlan => _sdEngine != null && _sdBattle.gameObject.activeInHierarchy &&
            !SDModalOpen && !_sdRolling && !_sdCommitted && _sdNpcReplayAge < 0;

        private Vector2 SDPointerLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_sdBattle, screen, null, out Vector2 point);
            return point;
        }

        internal void SDPointerBegin(int index, Vector2 screen)
        {
            if (OnlineActive) { OnlinePointerBegin(index, screen); return; }
            if (!SDCanPlan || index < -1 || index >= 5) return;
            SDResetHandEffects();
            _sdDragging = true; _sdDragAiming = false; _sdDragIndex = index;
            _sdDragStart = SDPointerLocal(screen);
            _sdDragLast = _sdDragStart;
            _sdDragHoldValue = index >= 0 && !_sdEngine.Held[index];
            for (int i = 0; i < 5; i++)
            {
                _sdDragOriginal[i] = _sdEngine.Held[i]; _sdDragPainted[i] = false;
                _sdDieHomes[i] = ((RectTransform)_sdBattle.Find("Die" + i)).anchoredPosition;
            }
            SDClearSelection();
        }

        internal void SDPointerMove(Vector2 screen)
        {
            if (OnlineActive) { OnlinePointerMove(screen); return; }
            if (!_sdDragging) return;
            if (!SDCanPlan) { SDCancelPointer(); return; }
            Vector2 point = SDPointerLocal(screen), delta = point - _sdDragStart;
            Vector2 previous = _sdDragLast;
            _sdDragLast = point;
            bool wasAiming = _sdDragAiming;
            _sdDragAiming = delta.y < -20 && -delta.y > Mathf.Abs(delta.x) * .65f;
            if (_sdDragAiming)
            {
                _sdEngine.SetHeld(_sdDragOriginal);
                float pull = Mathf.Clamp(-delta.y * .65f, 0, 55);
                for (int i = 0; i < 5; i++) ((RectTransform)_sdBattle.Find("Die" + i)).anchoredPosition = _sdDieHomes[i] + Vector2.down * pull;
                _sdBattle.Find("CurrentRole").gameObject.SetActive(false);
                _sdInstruction.text = "";
            }
            else
            {
                SDRestoreDiePositions();
                _sdBattle.Find("CurrentRole").gameObject.SetActive(true);
                if (_sdEngine.RerollsLeft == 0) return;
                if (wasAiming) System.Array.Clear(_sdDragPainted, 0, 5);
                if (_sdDragIndex < 0) return;
                SDPaintHeld(_sdDragIndex);
                for (int i = 0; i < 5; i++)
                    if (SDSegmentHitsDie(previous, point, _sdDieHomes[i]))
                        SDPaintHeld(i);
            }
        }

        private static bool SDSegmentHitsDie(Vector2 start, Vector2 end, Vector2 center)
        {
            float from = 0, to = 1;
            Vector2 delta = end - start;
            return SDClipAxis(start.x, delta.x, center.x - 61, center.x + 61, ref from, ref to) &&
                SDClipAxis(start.y, delta.y, center.y - 58, center.y + 58, ref from, ref to);
        }
        private static bool SDClipAxis(float start, float delta, float min, float max, ref float from, ref float to)
        {
            if (Mathf.Abs(delta) < .001f) return start >= min && start <= max;
            float a = (min - start) / delta, b = (max - start) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            from = Mathf.Max(from, a); to = Mathf.Min(to, b);
            return from <= to;
        }

        private void SDPaintHeld(int index)
        {
            if (_sdDragPainted[index]) return;
            _sdDragPainted[index] = true;
            if (_sdEngine.Held[index] != _sdDragHoldValue) _sdEngine.ToggleHeld(index);
            GameAudio.Play("die_keep");
            SDRefresh();
        }

        internal void SDPointerEnd(Vector2 screen)
        {
            if (OnlineActive) { OnlinePointerEnd(screen); return; }
            if (!_sdDragging) return;
            SDPointerMove(screen);
            if (!_sdDragging) return;
            Vector2 delta = SDPointerLocal(screen) - _sdDragStart;
            bool throwHand = SDCanPlan && _sdDragAiming && -delta.y >= SDFlickDistance;
            bool restore = _sdDragAiming;
            _sdDragging = false; _sdDragAiming = false;
            if (restore) _sdEngine.SetHeld(_sdDragOriginal);
            _sdLaunchPull = throwHand ? Mathf.Clamp(-delta.y * .65f, 0, 55) : 0;
            SDRestoreDiePositions();
            _sdBattle.Find("CurrentRole").gameObject.SetActive(true);
            _sdInputFrame = Time.frameCount;
            if (throwHand) SDCommit(); else SDRefresh();
        }

        private void SDRestoreDiePositions()
        {
            for (int i = 0; i < 5; i++)
            {
                Transform die = _sdBattle.Find("Die" + i);
                if (die != null) ((RectTransform)die).anchoredPosition = _sdDieHomes[i];
            }
        }

        private void SDCancelPointer()
        {
            if (!_sdDragging) return;
            if (_sdEngine != null && !_sdCommitted && (_sdEngine.Phase == MatchPhase.AwaitingReroll || _sdEngine.Phase == MatchPhase.AwaitingSplit))
                _sdEngine.SetHeld(_sdDragOriginal);
            _sdDragging = false; _sdDragAiming = false;
            SDRestoreDiePositions();
            _sdBattle.Find("CurrentRole").gameObject.SetActive(true);
        }

        private void SDHandleRightClick()
        {
            if (_sdPractice.gameObject.activeInHierarchy)
            {
                if (!_sdPracticeDragging && _sdInputFrame != Time.frameCount) { SDPracticeReroll(); _sdInputFrame = Time.frameCount; }
                return;
            }
            if (!SDCanPlan || _sdDragging || _sdInputFrame == Time.frameCount) return;
            SDReroll(); _sdInputFrame = Time.frameCount;
        }

        private void SDHandleWheel(float delta)
        {
            if (!SDCanPlan || _sdDragging || _sdAttackAge >= 0 || _sdInputFrame == Time.frameCount ||
                Input.GetMouseButton(0) || Input.GetMouseButton(1) ||
                (_sdEngine.Phase != MatchPhase.AwaitingReroll && _sdEngine.Phase != MatchPhase.AwaitingSplit))
            {
                // Input made during another action belongs to that action. Never carry a
                // partial scroll out of a modal, a throw, or a drag into the next decision.
                _sdWheelRemainder = 0;
                return;
            }
            if (float.IsNaN(delta) || float.IsInfinity(delta)) { _sdWheelRemainder = 0; return; }
            if (delta == 0) return;
            _sdWheelRemainder += delta;
            int notches = Mathf.FloorToInt(Mathf.Abs(_sdWheelRemainder) + .00001f);
            if (notches == 0) return;
            int direction = _sdWheelRemainder > 0 ? -1 : 1;
            _sdWheelRemainder += direction * notches;
            if (Mathf.Abs(_sdWheelRemainder) < .00001f) _sdWheelRemainder = 0;

            int available = 0;
            for (int i = 0; i < ScoreRules.HandCount; i++) if (!_sdEngine.PlayerUsed[i]) available++;
            if (available == 0) return;
            // Keep a fast multi-notch turn's distance, while avoiding redundant full laps.
            int steps = notches % available;
            int selected = (int)_sdSelectedKind;
            for (int step = 0; step < steps; step++)
            {
                for (int search = 0; search < ScoreRules.HandCount; search++)
                {
                    selected = (selected + direction + ScoreRules.HandCount) % ScoreRules.HandCount;
                    if (!_sdEngine.PlayerUsed[selected]) break;
                }
            }
            if (selected == (int)_sdSelectedKind) return;
            SDClearSelection();
            SDSelectCategory(selected);
            _sdInputFrame = Time.frameCount;
        }

        private void SDStartAttackMotion()
        {
            SDResetHandEffects();
            // Capture actual idle homes first, clear any transient launch pull, and let only
            // SharedAttackPresenter own movement from here. Never pre-displace before Begin,
            // or Reset restores pulled-down positions instead of the idle homes.
            _sdLaunchPull = 0f;
            for (int i = 0; i < 5; i++) _sdDieHomes[i] = ((RectTransform)_sdBattle.Find("Die" + i)).anchoredPosition;
            _sdAttackAge = 0;
            SDBeginImpactPresentation();
        }

        private void SDTickAttackMotion(float dt)
        {
            if (_sdAttackAge < 0) return;
            // An opt-in smoke/test pin reproduces one authored age exactly across frames.
            if (_sdSmokeAttackAgeOverride.HasValue) _sdAttackAge = _sdSmokeAttackAgeOverride.Value;
            else _sdAttackAge += dt;
            if (SDModalOpen)
            {
                // Modal interrupted the attack: reset/hide the visuals but keep the age clock
                // running to Duration so the advance gate does not clear early. Do NOT redraw
                // the presenter above the modal on resume.
                if (_sdAttackPresenter != null) _sdAttackPresenter.Reset();
                _sdImpactContact = false;
                if (_sdImpactCounter != null) _sdImpactCounter.text = "";
                if (_sdImpactIncomingCounter != null) _sdImpactIncomingCounter.text = "";
                if (_sdAttackAge >= SDImpactDuration) SDResetImpactPresentation();
                return;
            }
            SDAnimateImpactPresentation();
            if (_sdAttackAge >= SDImpactDuration)
            {
                _sdAttackAge = -1;
                SDRefresh();
            }
        }
    }
}
