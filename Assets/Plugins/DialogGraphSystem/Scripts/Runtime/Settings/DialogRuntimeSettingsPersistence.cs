using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.UI.Theming;
using UnityEngine;

namespace DialogSystem.Runtime.Settings
{
    /// <summary>
    /// Reads and writes per-player overrides to <see cref="PlayerPrefs"/>.
    /// The packaged <see cref="DialogSystemSettings"/> asset is never mutated;
    /// it acts purely as the source of default values.
    /// </summary>
    public static class DialogRuntimeSettingsPersistence
    {
        #region ---------------- Keys ----------------

        private const string KeyVoiceVolume          = "DGS_VoiceVolume";
        private const string KeySfxVolume            = "DGS_SfxVolume";
        private const string KeyTypewriterAudioOn    = "DGS_TypewriterAudioOn";
        private const string KeyTypewriterVolume     = "DGS_TypewriterVolume";
        private const string KeyTextSpeed            = "DGS_TextSpeed";
        private const string KeyAutoAdvance          = "DGS_AutoAdvance";
        private const string KeyAutoAdvanceDelay     = "DGS_AutoAdvanceDelay";
        private const string KeyThemeName            = "DGS_SelectedTheme";
        private const string KeyLocaleCode           = "DGS_SelectedLocale";

        #endregion

        #region ---------------- Save ----------------

        /// <summary>Saves audio overrides from the live <see cref="DialogAudioSettings"/>.</summary>
        public static void SaveAudio(DialogAudioSettings audio)
        {
            if (audio == null) return;
            PlayerPrefs.SetFloat(KeyVoiceVolume,       audio.voiceVolume);
            PlayerPrefs.SetFloat(KeySfxVolume,         audio.sfxVolume);
            PlayerPrefs.SetInt  (KeyTypewriterAudioOn, audio.enableTypewriterAudio ? 1 : 0);
            PlayerPrefs.SetFloat(KeyTypewriterVolume,  audio.typewriterVolume);
            PlayerPrefs.Save();
        }

        /// <summary>Saves text-flow overrides from the live <see cref="DialogTextSettings"/>.</summary>
        public static void SaveText(DialogTextSettings text)
        {
            if (text == null) return;
            PlayerPrefs.SetFloat(KeyTextSpeed,         text.charsPerSecond);
            PlayerPrefs.SetInt  (KeyAutoAdvance,       text.autoAdvance ? 1 : 0);
            PlayerPrefs.SetFloat(KeyAutoAdvanceDelay,  text.autoAdvanceDelay);
            PlayerPrefs.Save();
        }

        /// <summary>Saves the currently active theme name.</summary>
        public static void SaveTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName)) return;
            PlayerPrefs.SetString(KeyThemeName, themeName);
            PlayerPrefs.Save();
        }

        /// <summary>Saves the currently active locale code.</summary>
        public static void SaveLocale(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode)) return;
            PlayerPrefs.SetString(KeyLocaleCode, localeCode);
            PlayerPrefs.Save();
        }

        #endregion

        #region ---------------- Load ----------------

        /// <summary>
        /// Applies all persisted overrides on top of the live settings assets.
        /// Call this once on startup before the settings panel is opened.
        /// Audio and text settings objects are mutated in-memory only.
        /// </summary>
        public static void LoadIntoRuntime()
        {
            ApplyAudioOverrides(DialogSettingsRuntime.Audio);
            ApplyTextOverrides(DialogSettingsRuntime.Text);
        }

        /// <summary>Applies persisted audio values into the provided settings object.</summary>
        public static void ApplyAudioOverrides(DialogAudioSettings audio)
        {
            if (audio == null) return;

            if (PlayerPrefs.HasKey(KeyVoiceVolume))
                audio.voiceVolume = PlayerPrefs.GetFloat(KeyVoiceVolume, audio.voiceVolume);

            if (PlayerPrefs.HasKey(KeySfxVolume))
                audio.sfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, audio.sfxVolume);

            if (PlayerPrefs.HasKey(KeyTypewriterAudioOn))
                audio.enableTypewriterAudio = PlayerPrefs.GetInt(KeyTypewriterAudioOn, audio.enableTypewriterAudio ? 1 : 0) == 1;

            if (PlayerPrefs.HasKey(KeyTypewriterVolume))
                audio.typewriterVolume = PlayerPrefs.GetFloat(KeyTypewriterVolume, audio.typewriterVolume);
        }

        /// <summary>Applies persisted text-flow values into the provided settings object.</summary>
        public static void ApplyTextOverrides(DialogTextSettings text)
        {
            if (text == null) return;

            if (PlayerPrefs.HasKey(KeyTextSpeed))
                text.charsPerSecond = PlayerPrefs.GetFloat(KeyTextSpeed, text.charsPerSecond);

            if (PlayerPrefs.HasKey(KeyAutoAdvance))
                text.autoAdvance = PlayerPrefs.GetInt(KeyAutoAdvance, text.autoAdvance ? 1 : 0) == 1;

            if (PlayerPrefs.HasKey(KeyAutoAdvanceDelay))
                text.autoAdvanceDelay = PlayerPrefs.GetFloat(KeyAutoAdvanceDelay, text.autoAdvanceDelay);
        }

        /// <summary>Returns the persisted theme name, or <c>null</c> if none is saved.</summary>
        public static string LoadThemeName() =>
            PlayerPrefs.HasKey(KeyThemeName) ? PlayerPrefs.GetString(KeyThemeName) : null;

        /// <summary>Returns the persisted locale code, or <c>null</c> if none is saved.</summary>
        public static string LoadLocaleCode() =>
            PlayerPrefs.HasKey(KeyLocaleCode) ? PlayerPrefs.GetString(KeyLocaleCode) : null;

        #endregion

        #region ---------------- Reset ----------------

        /// <summary>Removes all persisted overrides, restoring asset defaults on next load.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyVoiceVolume);
            PlayerPrefs.DeleteKey(KeySfxVolume);
            PlayerPrefs.DeleteKey(KeyTypewriterAudioOn);
            PlayerPrefs.DeleteKey(KeyTypewriterVolume);
            PlayerPrefs.DeleteKey(KeyTextSpeed);
            PlayerPrefs.DeleteKey(KeyAutoAdvance);
            PlayerPrefs.DeleteKey(KeyAutoAdvanceDelay);
            PlayerPrefs.DeleteKey(KeyThemeName);
            PlayerPrefs.DeleteKey(KeyLocaleCode);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Resets runtime settings to the values from the packaged asset
        /// and removes all persisted overrides.
        /// </summary>
        public static void ResetToDefaults(DialogSystemSettings defaults)
        {
            ResetAll();

            if (defaults == null) return;

            // Reload clean values from the asset into the live in-memory objects
            // (they are the same references, so we just re-copy the serialized defaults)
            var audio = defaults.audioSettings;
            var text  = defaults.textSettings;

            // Force-read the asset's serialized field defaults by reloading the object
            // from disk so we get the pristine authored values.
            var freshMaster = UnityEngine.Resources.Load<DialogSystemSettings>("DialogSettingsSO/DialogSystemSettings");
            if (freshMaster != null)
            {
                if (freshMaster.audioSettings != null && audio != null)
                {
                    audio.voiceVolume          = freshMaster.audioSettings.voiceVolume;
                    audio.sfxVolume            = freshMaster.audioSettings.sfxVolume;
                    audio.enableTypewriterAudio = freshMaster.audioSettings.enableTypewriterAudio;
                    audio.typewriterVolume     = freshMaster.audioSettings.typewriterVolume;
                }

                if (freshMaster.textSettings != null && text != null)
                {
                    text.charsPerSecond    = freshMaster.textSettings.charsPerSecond;
                    text.autoAdvance       = freshMaster.textSettings.autoAdvance;
                    text.autoAdvanceDelay  = freshMaster.textSettings.autoAdvanceDelay;
                }
            }
        }

        #endregion
    }
}
