using DialogSystem.Runtime.Settings.Panels;

namespace DialogSystem.Runtime.UI
{
    internal enum DialogOptionalUiFeature
    {
        BackgroundPanel,
        SpeakerName,
        Portrait,
        SkipButton,
        AutoButton,
        HistoryButton,
        LanguageButton,
        ContinueIndicator,
        AutoSkipIcon,
        ChoicePanel,
        HistoryPanel,
        SettingsPanel,
        SettingsLanguageDropdown,
        SettingsThemeDropdown,
        SettingsTextSpeed,
        SettingsAutoAdvance,
        SettingsVoiceVolume,
        SettingsSfxVolume,
        SettingsButton,
    }

    internal static class DialogOptionalUiFeaturePolicy
    {
        public static bool IsEnabled(DialogueRuntimeUISettings settings, DialogOptionalUiFeature feature)
        {
            if (settings == null)
            {
                return true;
            }

            switch (feature)
            {
                case DialogOptionalUiFeature.BackgroundPanel:
                    return settings.showBackgroundPanel;
                case DialogOptionalUiFeature.SpeakerName:
                    return settings.showSpeakerName;
                case DialogOptionalUiFeature.Portrait:
                    return settings.showPortrait;
                case DialogOptionalUiFeature.SkipButton:
                    return settings.showSkipButton;
                case DialogOptionalUiFeature.AutoButton:
                    return settings.showAutoButton;
                case DialogOptionalUiFeature.HistoryButton:
                    return settings.showHistoryButton;
                case DialogOptionalUiFeature.SettingsButton:
                    return settings.showSettingsButton;
                case DialogOptionalUiFeature.LanguageButton:
                    return settings.showLanguageButton;
                case DialogOptionalUiFeature.ContinueIndicator:
                    return settings.showContinueIndicator;
                case DialogOptionalUiFeature.AutoSkipIcon:
                    return settings.showAutoSkipIcon;
                case DialogOptionalUiFeature.ChoicePanel:
                    return settings.showChoicePanel;
                case DialogOptionalUiFeature.HistoryPanel:
                    return settings.showHistoryPanel;
                case DialogOptionalUiFeature.SettingsPanel:
                    return settings.showSettingsPanel;
                case DialogOptionalUiFeature.SettingsLanguageDropdown:
                    return settings.showSettingsLanguageDropdown;
                case DialogOptionalUiFeature.SettingsThemeDropdown:
                    return settings.showSettingsThemeDropdown;
                case DialogOptionalUiFeature.SettingsTextSpeed:
                    return settings.showSettingsTextSpeed;
                case DialogOptionalUiFeature.SettingsAutoAdvance:
                    return settings.showSettingsAutoAdvance;
                case DialogOptionalUiFeature.SettingsVoiceVolume:
                    return settings.showSettingsVoiceVolume;
                case DialogOptionalUiFeature.SettingsSfxVolume:
                    return settings.showSettingsSfxVolume;
                default:
                    return true;
            }
        }

        public static string GetDisplayName(DialogOptionalUiFeature feature)
        {
            switch (feature)
            {
                case DialogOptionalUiFeature.BackgroundPanel:
                    return "background panel";
                case DialogOptionalUiFeature.SpeakerName:
                    return "speaker name";
                case DialogOptionalUiFeature.Portrait:
                    return "portrait";
                case DialogOptionalUiFeature.SkipButton:
                    return "skip button";
                case DialogOptionalUiFeature.AutoButton:
                    return "auto button";
                case DialogOptionalUiFeature.HistoryButton:
                    return "history button";
                case DialogOptionalUiFeature.LanguageButton:
                    return "language button";
                case DialogOptionalUiFeature.ContinueIndicator:
                    return "continue indicator";
                case DialogOptionalUiFeature.AutoSkipIcon:
                    return "auto-skip icon";
                case DialogOptionalUiFeature.ChoicePanel:
                    return "choice panel";
                case DialogOptionalUiFeature.HistoryPanel:
                    return "history panel";
                case DialogOptionalUiFeature.SettingsPanel:
                    return "runtime settings panel";
                case DialogOptionalUiFeature.SettingsLanguageDropdown:
                    return "settings language selector";
                case DialogOptionalUiFeature.SettingsThemeDropdown:
                    return "settings theme selector";
                case DialogOptionalUiFeature.SettingsTextSpeed:
                    return "settings text speed slider";
                case DialogOptionalUiFeature.SettingsAutoAdvance:
                    return "settings auto-advance toggle";
                case DialogOptionalUiFeature.SettingsVoiceVolume:
                    return "settings voice volume slider";
                case DialogOptionalUiFeature.SettingsSfxVolume:
                    return "settings SFX volume slider";
                default:
                    return "optional UI feature";
            }
        }
    }
}
