using DialogSystem.Runtime.UI.Theming;
using UnityEngine;

namespace DialogSystem.Runtime.Settings.Panels
{
    /// <summary>
    /// Configuration for runtime UI visibility and common behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueRuntimeUISettings", menuName = "Beka Forge/Dialogues/Settings/Runtime UI Settings", order = 5)]
    public class DialogueRuntimeUISettings : ScriptableObject
    {
        [Header("Global Visibility")]
        [Tooltip("Overall visibility of the dialogue background panel.")]
        public bool showBackgroundPanel = true;

        [Header("Header / Meta")]
        [Tooltip("Visibility of the speaker name text.")]
        public bool showSpeakerName = true;

        [Tooltip("Visibility of the speaker portrait image.")]
        public bool showPortrait = true;

        [Header("Controls")]
        [Tooltip("Visibility of the skip conversation button.")]
        public bool showSkipButton = true;

        [Tooltip("Visibility of the autoplay toggle button.")]
        public bool showAutoButton = true;

        [Tooltip("Visibility of the history / transcript button.")]
        public bool showHistoryButton = true;

        [Tooltip("Visibility of the optional language/settings button.")]
        public bool showLanguageButton = true;

        [Tooltip("Visibility of the optional settings button.")]
        public bool showSettingsButton = true;

        [Header("Indicators")]
        [Tooltip("Visibility of the continue / skip icon (the chevron/arrow).")]
        public bool showContinueIndicator = true;

        [Tooltip("Visibility of the small icon that indicates auto/skip state.")]
        public bool showAutoSkipIcon = true;

        [Header("Panels")]
        [Tooltip("Overall visibility of the branching choice panel.")]
        public bool showChoicePanel = true;

        [Tooltip("Overall visibility of the history / log panel.")]
        public bool showHistoryPanel = true;

        [Header("Runtime Settings Panel")]
        [Tooltip("Game-wide visual theme for the runtime settings panel. This is separate from dialogue themes.")]
        public DialogSettingsThemeSO settingsTheme;

        [Tooltip("Overall availability of the in-game settings panel opened from the dialogue UI.")]
        public bool showSettingsPanel = true;

        [Tooltip("Visibility of the language selector inside the runtime settings panel.")]
        public bool showSettingsLanguageDropdown = true;

        [Tooltip("Visibility of the theme selector inside the runtime settings panel.")]
        public bool showSettingsThemeDropdown = true;

        [Tooltip("Visibility of the text speed slider inside the runtime settings panel.")]
        public bool showSettingsTextSpeed = true;

        [Tooltip("Visibility of the auto-advance toggle inside the runtime settings panel.")]
        public bool showSettingsAutoAdvance = true;

        [Tooltip("Visibility of the voice volume slider inside the runtime settings panel.")]
        public bool showSettingsVoiceVolume = true;

        [Tooltip("Visibility of the SFX volume slider inside the runtime settings panel.")]
        public bool showSettingsSfxVolume = true;
    }
}
