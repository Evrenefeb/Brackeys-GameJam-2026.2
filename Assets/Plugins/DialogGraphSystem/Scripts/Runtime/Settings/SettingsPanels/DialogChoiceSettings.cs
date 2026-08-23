using UnityEngine;

namespace DialogSystem.Runtime.Settings.Panels
{
    [CreateAssetMenu(fileName = "DialogChoiceSettings", menuName = "Beka Forge/Dialogues/Settings/Choice Settings")]
    public class DialogChoiceSettings : ScriptableObject
    {
        [SerializeField] private bool doDebug = false;

        public bool DoDebug => doDebug;

        #region Navigation
        [Header("Navigation")]
        public bool wrapNavigation = true;
        [Min(0.01f)] public float holdRepeatDelay = 0.15f;
        public bool selectFirstOnEnable = true;
        public bool mouseHoverMovesSelection = true;
        #endregion

        #region Visuals
        [Header("Visuals")]
        public Color selectedOutlineColor = Color.white;
        [Range(0.5f, 10f)] public float outlineThickness = 1.0f;
        public bool animateSelected = true;
        [Range(1.0f, 1.2f)] public float animatePulseScale = 1.06f;
        [Range(0.25f, 3f)] public float animatePulseSpeed = 1.0f;
        #endregion

        #region Hints & Confirm
        [Header("Hints & Confirm")]
        public bool showKeyHints = true;

        [Tooltip("Allow confirming with a specific keyboard or mouse key.")]
        public bool enableKeyboardConfirmKey = true;

        [Tooltip("KeyCode name used to confirm the highlighted choice. Legacy single-letter values are still supported.")]
        public string keyboardConfirmLetter = "F";

        public bool alsoAcceptSubmit = true;
        public bool acceptGamepadConfirm = true;
        public bool acceptXRSelect = true;

        // Legacy/compat (kept for serialized backwards-compat)
        [HideInInspector] public string keyboardConfirm = "";
        [HideInInspector] public string altConfirm = "";
        [HideInInspector] public string gamepadConfirm = "South";
        [HideInInspector] public string mouseHint = "Click";
        #endregion

        private void OnValidate()
        {
            // Normalize legacy letters and newer KeyCode names to a stable KeyCode string.
            keyboardConfirmLetter = TryParseConfirmKey(keyboardConfirmLetter, out var key)
                ? key.ToString()
                : KeyCode.F.ToString();
        }

        private static bool TryParseConfirmKey(string rawValue, out KeyCode key)
        {
            key = KeyCode.None;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var value = rawValue.Trim();
            if (System.Enum.TryParse(value, true, out key) && key != KeyCode.None)
            {
                return true;
            }

            if (value.Length != 1)
            {
                key = value.ToUpperInvariant() switch
                {
                    "ENTER" => KeyCode.Return,
                    "ESC" => KeyCode.Escape,
                    "CTRL" => KeyCode.LeftControl,
                    "CONTROL" => KeyCode.LeftControl,
                    "SHIFT" => KeyCode.LeftShift,
                    "ALT" => KeyCode.LeftAlt,
                    _ => KeyCode.None
                };

                return key != KeyCode.None;
            }

            var c = char.ToUpperInvariant(value[0]);
            if (c >= 'A' && c <= 'Z')
            {
                key = (KeyCode)((int)KeyCode.A + (c - 'A'));
                return true;
            }

            if (c >= '0' && c <= '9')
            {
                key = (KeyCode)((int)KeyCode.Alpha0 + (c - '0'));
                return true;
            }

            return false;
        }
    }
}
