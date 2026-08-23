using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Editor-side catalog of localization target languages available to the UI.
    /// Uses the runtime's installed specific cultures, with common game-translation
    /// targets pinned near the top for faster access.
    /// </summary>
    public static class DialogLocalizationLanguageCatalog
    {
        private static readonly string[] PreferredLocaleOrder =
        {
            "en-US",
            "en-GB",
            "fr-FR",
            "it-IT",
            "de-DE",
            "es-ES",
            "es-MX",
            "pt-BR",
            "pt-PT",
            "pl-PL",
            "ru-RU",
            "uk-UA",
            "tr-TR",
            "ja-JP",
            "ko-KR",
            "zh-CN",
            "zh-TW",
            "ar-SA",
            "th-TH",
            "cs-CZ",
            "nl-NL",
            "sv-SE",
            "da-DK",
            "fi-FI",
            "hu-HU",
            "ro-RO",
            "vi-VN",
            "id-ID",
            "hi-IN"
        };

        private static readonly List<DialogLocalizationLanguageOption> _allOptions = BuildOptions();
        private static readonly Dictionary<string, DialogLocalizationLanguageOption> _optionsByCode =
            _allOptions.ToDictionary(option => option.LocaleCode, StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> _codeByChoiceLabel =
            _allOptions.ToDictionary(option => option.ChoiceLabel, option => option.LocaleCode, StringComparer.Ordinal);

        public static IReadOnlyList<DialogLocalizationLanguageOption> AllOptions => _allOptions;

        public static IReadOnlyList<string> GetChoiceLabels(
            IReadOnlyCollection<string> excludedLocaleCodes = null,
            string sourceLocaleCode = null,
            bool includeCustomOption = false,
            string customOptionLabel = null)
        {
            var choices = new List<string>();
            var excluded = excludedLocaleCodes != null
                ? new HashSet<string>(excludedLocaleCodes, StringComparer.OrdinalIgnoreCase)
                : null;
            var excludedLanguages = BuildExcludedLanguageCodes(excludedLocaleCodes, sourceLocaleCode);

            foreach (var option in _allOptions)
            {
                if (!string.IsNullOrWhiteSpace(sourceLocaleCode) &&
                    string.Equals(option.LocaleCode, sourceLocaleCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (excluded != null && excluded.Contains(option.LocaleCode))
                {
                    continue;
                }

                if (excludedLanguages.Contains(GetLanguageCode(option.LocaleCode)))
                {
                    continue;
                }

                choices.Add(option.ChoiceLabel);
            }

            if (includeCustomOption && !string.IsNullOrWhiteSpace(customOptionLabel))
            {
                choices.Add(customOptionLabel);
            }

            return choices;
        }

        public static bool TryGetLocaleCode(string choiceLabel, out string localeCode)
        {
            localeCode = string.Empty;
            if (string.IsNullOrWhiteSpace(choiceLabel))
            {
                return false;
            }

            return _codeByChoiceLabel.TryGetValue(choiceLabel, out localeCode);
        }

        public static string GetChoiceLabel(string localeCode)
        {
            if (TryGetOption(localeCode, out var option))
            {
                return option.ChoiceLabel;
            }

            return localeCode ?? string.Empty;
        }

        public static string GetDisplayName(string localeCode)
        {
            if (TryGetOption(localeCode, out var option))
            {
                return option.DisplayName;
            }

            return localeCode ?? string.Empty;
        }

        public static bool TryGetOption(string localeCode, out DialogLocalizationLanguageOption option)
        {
            option = null;
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return false;
            }

            return _optionsByCode.TryGetValue(localeCode.Trim(), out option);
        }

        private static HashSet<string> BuildExcludedLanguageCodes(
            IReadOnlyCollection<string> excludedLocaleCodes,
            string sourceLocaleCode)
        {
            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(sourceLocaleCode))
            {
                var sourceLanguage = GetLanguageCode(sourceLocaleCode);
                if (!string.IsNullOrWhiteSpace(sourceLanguage))
                {
                    languages.Add(sourceLanguage);
                }
            }

            if (excludedLocaleCodes == null)
            {
                return languages;
            }

            foreach (var localeCode in excludedLocaleCodes)
            {
                var languageCode = GetLanguageCode(localeCode);
                if (!string.IsNullOrWhiteSpace(languageCode))
                {
                    languages.Add(languageCode);
                }
            }

            return languages;
        }

        private static string GetLanguageCode(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return string.Empty;
            }

            try
            {
                return CultureInfo.GetCultureInfo(localeCode.Trim()).TwoLetterISOLanguageName;
            }
            catch (CultureNotFoundException)
            {
                var separatorIndex = localeCode.IndexOf('-');
                if (separatorIndex > 0)
                {
                    return localeCode.Substring(0, separatorIndex).Trim();
                }

                return localeCode.Trim();
            }
        }

        private static List<DialogLocalizationLanguageOption> BuildOptions()
        {
            var byCode = CultureInfo
                .GetCultures(CultureTypes.SpecificCultures)
                .Where(culture => !string.IsNullOrWhiteSpace(culture.Name))
                .GroupBy(culture => culture.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(culture => culture.EnglishName, StringComparer.OrdinalIgnoreCase).First(),
                    StringComparer.OrdinalIgnoreCase);

            var options = new List<DialogLocalizationLanguageOption>();

            foreach (var localeCode in PreferredLocaleOrder)
            {
                if (!byCode.TryGetValue(localeCode, out var culture))
                {
                    continue;
                }

                options.Add(new DialogLocalizationLanguageOption(culture.Name, culture.EnglishName));
                byCode.Remove(localeCode);
            }

            foreach (var culture in byCode.Values
                         .OrderBy(value => value.EnglishName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
            {
                options.Add(new DialogLocalizationLanguageOption(culture.Name, culture.EnglishName));
            }

            return options;
        }
    }

    public sealed class DialogLocalizationLanguageOption
    {
        public DialogLocalizationLanguageOption(string localeCode, string displayName)
        {
            LocaleCode = localeCode;
            DisplayName = displayName;
            ChoiceLabel = $"{displayName} ({localeCode})";
        }

        public string LocaleCode { get; }

        public string DisplayName { get; }

        public string ChoiceLabel { get; }
    }
}
