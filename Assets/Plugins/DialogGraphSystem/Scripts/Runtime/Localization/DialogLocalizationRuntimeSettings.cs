using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DialogSystem.Runtime.Localization
{
    [CreateAssetMenu(
        fileName = "DialogLocalizationRuntimeSettings",
        menuName = "Beka Forge/Dialogues/Localization Runtime Settings")]
    public class DialogLocalizationRuntimeSettings : ScriptableObject
    {
        public const string ResourcesPath = "DialogSettingsSO/DialogLocalizationRuntimeSettings";

        [Tooltip("Default/source locale used when there is no saved player choice.")]
        public string defaultLocaleCode = "en-US";

        [Tooltip("Remember the selected language in PlayerPrefs.")]
        public bool rememberPlayerChoice = true;

        [Tooltip("PlayerPrefs key used for the selected locale code.")]
        public string playerPrefsKey = "DialogSystem.Locale";

        [Tooltip("Localization tables available to runtime language selectors.")]
        public List<DialogLocalizationTable> localizationTables = new();

        [Tooltip("Optional display names that override CultureInfo names in runtime dropdowns.")]
        public List<LocaleDisplayName> localeDisplayNames = new();

        public IReadOnlyList<DialogLocalizationTable> Tables => localizationTables;

        public string GetDisplayName(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return string.Empty;
            }

            foreach (var entry in localeDisplayNames ?? new List<LocaleDisplayName>())
            {
                if (entry != null &&
                    string.Equals(entry.localeCode, localeCode, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(entry.displayName))
                {
                    return entry.displayName.Trim();
                }
            }

            try
            {
                return CultureInfo.GetCultureInfo(localeCode).DisplayName;
            }
            catch
            {
                return localeCode;
            }
        }
    }

    [Serializable]
    public sealed class LocaleDisplayName
    {
        public string localeCode;
        public string displayName;
    }
}
