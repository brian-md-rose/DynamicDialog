using UnityEngine;
#if DD_SAMPLE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// The courtyard's controls, read from the Input System package when it's installed and enabled, or from the
    /// old Input Manager otherwise, so the sample runs in any project. Nothing here is specific to Dynamic Dialog.
    /// </summary>
    /// <remarks>
    /// Keyboard: WASD or arrow keys to walk, E to talk, F to pick up or drop, Tab for the debug overlays.
    /// Gamepad: left stick or d-pad, A (south) to talk, X (west) to pick up or drop, View (select) for the overlays.
    /// The sample's assembly gets <c>DD_SAMPLE_INPUT_SYSTEM</c> from a version define when com.unity.inputsystem is installed.
    /// </remarks>
    public static class CourtyardInput
    {
        /// <summary>Walk direction this frame, length at most 1.</summary>
        public static Vector2 Move { get; private set; }
        /// <summary>Talk was pressed this frame.</summary>
        public static bool Talk { get; private set; }
        /// <summary>Pick up / drop was pressed this frame.</summary>
        public static bool Carry { get; private set; }
        /// <summary>The debug overlay toggle was pressed this frame.</summary>
        public static bool ToggleDebug { get; private set; }
        /// <summary>True when the gamepad was used more recently than the keyboard (for button prompts).</summary>
        public static bool UsingGamepad { get; private set; }

        static int polledFrame = -1;

        /// <summary>Reads this frame's input. Safe to call more than once a frame.</summary>
        public static void Poll()
        {
            if (polledFrame == Time.frameCount) return;
            polledFrame = Time.frameCount;
            Vector2 move = Vector2.zero;
            bool talk = false, carry = false, debug = false;
#if DD_SAMPLE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                talk |= kb.eKey.wasPressedThisFrame;
                carry |= kb.fKey.wasPressedThisFrame;
                debug |= kb.tabKey.wasPressedThisFrame;
                if (kb.anyKey.wasPressedThisFrame) UsingGamepad = false;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                bool south = pad.buttonSouth.wasPressedThisFrame, west = pad.buttonWest.wasPressedThisFrame, select = pad.selectButton.wasPressedThisFrame;
                if (stick.sqrMagnitude > 0.04f) move += stick; // small dead zone
                talk |= south;
                carry |= west;
                debug |= select;
                if (stick.sqrMagnitude > 0.1f || south || west || select || pad.buttonNorth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame)
                    UsingGamepad = true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            talk = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton0);
            carry = Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton2);
            debug = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton6);
            if (Input.anyKeyDown)
                UsingGamepad = Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton2) || Input.GetKeyDown(KeyCode.JoystickButton6);
            // The default Input Manager's Horizontal and Vertical axes include the left stick.
            var axes = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (move == Vector2.zero && axes.sqrMagnitude > 0.04f) move = axes;
#endif
            Move = Vector2.ClampMagnitude(move, 1f);
            Talk = talk;
            Carry = carry;
            ToggleDebug = debug;
        }
    }
}
