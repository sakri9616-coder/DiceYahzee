using UnityEngine;
using UnityEngine.EventSystems;

namespace ParvaMirabilia.Presentation
{
    // Unity's drag threshold keeps a tap as a normal die click. A drag suppresses
    // Button's click automatically, so a painted hold cannot toggle twice on release.
    internal sealed class ScoreDuelPointer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerClickHandler
    {
        private GameFlow _owner;
        private int _index;
        private bool _practice;
        private bool _advancePress;
        public void OnPointerDown(PointerEventData data)
        {
            _advancePress = !_practice && data.button == PointerEventData.InputButton.Left && _owner.SDCanAdvanceTable;
        }
        public void OnPointerClick(PointerEventData data)
        {
            bool ready = _advancePress; _advancePress = false;
            if (!ready || data.dragging || data.button != PointerEventData.InputButton.Left) return;
            var pressed = data.pointerPressRaycast.gameObject;
            var button = pressed == null ? null : pressed.GetComponentInParent<UnityEngine.UI.Button>();
            if (button != null && button.IsInteractable()) return;
            _owner.SDTableClick();
        }
        internal void Initialize(GameFlow owner, int index, bool practice = false) { _owner = owner; _index = index; _practice = practice; }
        public void OnBeginDrag(PointerEventData data)
        {
            _advancePress = false;
            var pressed = data.pointerPressRaycast.gameObject;
            if (_index < 0 && pressed != null)
            {
                var button = pressed.GetComponentInParent<UnityEngine.UI.Button>();
                if (button != null && button.IsInteractable()) return;
            }
            if (data.button == PointerEventData.InputButton.Left)
                if (_practice) _owner.SDPracticePointerBegin(data.pressPosition); else _owner.SDPointerBegin(_index, data.pressPosition);
        }
        public void OnDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) { if (_practice) _owner.SDPracticePointerMove(data.position); else _owner.SDPointerMove(data.position); }
        }
        public void OnEndDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) { if (_practice) _owner.SDPracticePointerEnd(data.position); else _owner.SDPointerEnd(data.position); }
        }
    }
}
