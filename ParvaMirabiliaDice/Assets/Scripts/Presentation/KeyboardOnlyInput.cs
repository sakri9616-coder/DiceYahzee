using UnityEngine;
using UnityEngine.EventSystems;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// UI navigation and submit from the keyboard only. Unity's default "Horizontal", "Vertical"
    /// and "Submit" axes also listen to every joystick, so a connected controller that drifts or
    /// sends noise (a Bluetooth pad, a tablet exposed as a game controller) moves the selection
    /// and presses whatever it lands on. The game is played with mouse and keyboard, so the
    /// event system reads the arrow keys, Enter and Escape and nothing else; pointer input is
    /// untouched.
    /// </summary>
    internal sealed class KeyboardOnlyInput : BaseInput
    {
        public override float GetAxisRaw(string axisName)
        {
            if (axisName == "Horizontal") return Axis(KeyCode.LeftArrow, KeyCode.RightArrow);
            if (axisName == "Vertical") return Axis(KeyCode.DownArrow, KeyCode.UpArrow);
            return 0f;
        }

        public override bool GetButtonDown(string buttonName)
        {
            if (buttonName == "Submit") return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (buttonName == "Cancel") return Input.GetKeyDown(KeyCode.Escape);
            return false;
        }

        private static float Axis(KeyCode negative, KeyCode positive) =>
            (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);

        /// <summary>Routes the event system's navigation and submit through the keyboard only.</summary>
        public static void Apply(GameObject eventSystem)
        {
            var module = eventSystem != null ? eventSystem.GetComponent<StandaloneInputModule>() : null;
            if (module == null) return;
            var input = eventSystem.GetComponent<KeyboardOnlyInput>();
            if (input == null) input = eventSystem.AddComponent<KeyboardOnlyInput>();
            module.inputOverride = input;
        }
    }
}
