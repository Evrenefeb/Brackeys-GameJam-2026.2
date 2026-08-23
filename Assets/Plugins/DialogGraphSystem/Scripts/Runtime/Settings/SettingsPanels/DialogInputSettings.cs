using UnityEngine;

namespace DialogSystem.Runtime.Settings.Panels
{
    /// <summary>
    /// Input bindings for keyboard, mouse, and basic gamepad (old Input Manager).
    /// </summary>
    public class DialogInputSettings : ScriptableObject
    {
        public const KeyCode DefaultLineAdvanceKey = KeyCode.Space;

        #region ---------------- Inspector ----------------
        [Header("Line Advance")]
        [Tooltip("Keyboard key used to skip the current line or advance to the next line. Mouse/touch continue remains controlled separately.")]
        public KeyCode lineAdvanceKey = DefaultLineAdvanceKey;

        [Header("Keys (Keyboard)")]
        public KeyCode[] confirmKeys = { KeyCode.Return, KeyCode.Space, KeyCode.E, KeyCode.F };
        public KeyCode[] skipKeys = { KeyCode.LeftControl, KeyCode.RightControl };
        public KeyCode[] fastForwardKeys = { KeyCode.LeftShift, KeyCode.RightShift };
        public KeyCode[] cancelKeys = { KeyCode.Escape };

        [Header("Navigation (Keyboard)")]
        public KeyCode[] navUpKeys = { KeyCode.W, KeyCode.UpArrow };
        public KeyCode[] navDownKeys = { KeyCode.S, KeyCode.DownArrow };

        [Header("Axes / Gamepad")]
        public string verticalAxis = "Vertical";
        [Tooltip("JoystickButton index for confirm (0 = A on many pads).")]
        public int joystickConfirmButton = 0;

        [Header("Mouse")]
        public bool allowMouseClickConfirm = true;
        #endregion

        public static KeyCode ResolveLineAdvanceKey(DialogInputSettings settings)
        {
            var key = settings != null ? settings.lineAdvanceKey : DefaultLineAdvanceKey;
            return key == KeyCode.None ? DefaultLineAdvanceKey : key;
        }

        public static string GetLineAdvanceKeyDisplayName(DialogInputSettings settings)
        {
            return FormatKeyCode(ResolveLineAdvanceKey(settings));
        }

        public static string FormatKeyCode(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return ((int)key - (int)KeyCode.Alpha0).ToString();
            }

            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                return "Keypad " + ((int)key - (int)KeyCode.Keypad0);
            }

            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
            {
                return "Mouse " + ((int)key - (int)KeyCode.Mouse0 + 1);
            }

            return key switch
            {
                KeyCode.None => "Unbound",
                KeyCode.Space => "Space",
                KeyCode.Return => "Enter",
                KeyCode.KeypadEnter => "Keypad Enter",
                KeyCode.Escape => "Esc",
                KeyCode.UpArrow => "Up Arrow",
                KeyCode.DownArrow => "Down Arrow",
                KeyCode.LeftArrow => "Left Arrow",
                KeyCode.RightArrow => "Right Arrow",
                KeyCode.LeftControl => "Left Ctrl",
                KeyCode.RightControl => "Right Ctrl",
                KeyCode.LeftShift => "Left Shift",
                KeyCode.RightShift => "Right Shift",
                KeyCode.LeftAlt => "Left Alt",
                KeyCode.RightAlt => "Right Alt",
                _ => key.ToString()
            };
        }
    }
}