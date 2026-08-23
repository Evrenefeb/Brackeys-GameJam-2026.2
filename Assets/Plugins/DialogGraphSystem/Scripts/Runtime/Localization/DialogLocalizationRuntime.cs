using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DialogSystem.Runtime.Localization
{
    /// <summary>
    /// Lightweight runtime singleton that holds the active
    /// <see cref="DialogLocalizationTable"/> and resolves locale keys to
    /// translated strings at play time.
    /// <para>
    /// Usage:
    /// <code>
    ///   // Bootstrap (e.g. from a game settings loader on scene load):
    ///   DialogLocalizationRuntime.Instance.SetActiveTable(frenchTable);
    ///
    ///   // Resolve at display time:
    ///   string display = DialogLocalizationRuntime.Instance.Resolve(node.questionTextLocaleKey, node.questionText);
    /// </code>
    /// </para>
    /// <para>
    /// When no table is active, <see cref="Resolve"/> returns <paramref name="fallback"/>
    /// directly — zero allocation, no exceptions.
    /// </para>
    /// <para>
    /// <see cref="OnLocaleChanged"/> fires whenever <see cref="SetActiveTable"/> is
    /// called, allowing UI to hot-swap text mid-session without a scene reload.
    /// </para>
    /// </summary>
    public sealed class DialogLocalizationRuntime
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        private static DialogLocalizationRuntime _instance;

        /// <summary>Shared singleton instance. Created on first access.</summary>
        public static DialogLocalizationRuntime Instance
            => _instance ??= new DialogLocalizationRuntime();

        private DialogLocalizationRuntime() { }

        // ── State ─────────────────────────────────────────────────────────────

        private DialogLocalizationTable _activeTable;
        private DialogLocalizationRuntimeSettings _settings;
        private readonly List<DialogLocalizationTable> _availableTables = new List<DialogLocalizationTable>();

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired after <see cref="SetActiveTable"/> completes.
        /// Subscribe to update live UI when the locale changes mid-session.
        /// </summary>
        public event Action OnLocaleChanged;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Returns true when an active localization table is set.</summary>
        public bool HasActiveTable => _activeTable != null;

        /// <summary>The active localization table, or <c>null</c> when source fallback is active.</summary>
        public DialogLocalizationTable ActiveTable => _activeTable;

        /// <summary>Locale codes currently available for runtime language switching.</summary>
        public IReadOnlyList<string> AvailableLocales => _availableTables
            .Where(table => table != null && !string.IsNullOrWhiteSpace(table.LocaleCode))
            .Select(table => table.LocaleCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        /// <summary>
        /// The locale code of the currently active table, or <c>null</c>
        /// when no table is set.
        /// </summary>
        public string ActiveLocaleCode => _activeTable?.LocaleCode;

        /// <summary>Runtime settings used to populate available language tables.</summary>
        public DialogLocalizationRuntimeSettings Settings => _settings;

        /// <summary>
        /// Loads default runtime localization settings from Resources and initializes
        /// the active locale. Missing settings are valid and leave source fallback active.
        /// </summary>
        public void Initialize()
        {
            Initialize(Resources.Load<DialogLocalizationRuntimeSettings>(DialogLocalizationRuntimeSettings.ResourcesPath));
        }

        /// <summary>
        /// Initializes the runtime language catalog from a settings asset.
        /// </summary>
        public void Initialize(DialogLocalizationRuntimeSettings settings)
        {
            _settings = settings;
            _availableTables.Clear();

            if (settings?.localizationTables != null)
            {
                foreach (var table in settings.localizationTables)
                {
                    if (table != null && !_availableTables.Contains(table))
                    {
                        _availableTables.Add(table);
                    }
                }
            }

            var preferredLocale = LoadPreferredLocale();
            if (string.IsNullOrWhiteSpace(preferredLocale))
            {
                preferredLocale = settings?.defaultLocaleCode;
            }

            if (!SetActiveLocale(preferredLocale, saveChoice: false))
            {
                var fallbackTable = FindSourceTable()
                    ?? FindTable(settings?.defaultLocaleCode)
                    ?? _availableTables.FirstOrDefault(table => table != null);
                SetActiveTableInternal(fallbackTable, fireEvent: true);
            }
        }

        /// <summary>
        /// Sets the active localization table and fires <see cref="OnLocaleChanged"/>.
        /// Pass <c>null</c> to clear the active locale (all resolutions will fall back
        /// to source strings).
        /// </summary>
        public void SetActiveTable(DialogLocalizationTable table)
        {
            SetActiveTableInternal(table, fireEvent: true);
            SavePreferredLocale();
        }

        /// <summary>
        /// Activates a locale by code. Returns false when no matching table exists.
        /// </summary>
        public bool SetActiveLocale(string localeCode)
        {
            return SetActiveLocale(localeCode, saveChoice: true);
        }

        /// <summary>
        /// Switches to a different available locale chosen at random.
        /// Returns false when fewer than two locales are available.
        /// </summary>
        public bool TrySetRandomOtherLocale()
        {
            var localeCodes = AvailableLocales;
            if (localeCodes == null || localeCodes.Count <= 1)
            {
                return false;
            }

            var candidates = new List<string>();
            for (var i = 0; i < localeCodes.Count; i++)
            {
                var localeCode = localeCodes[i];
                if (string.IsNullOrWhiteSpace(localeCode))
                {
                    continue;
                }

                if (string.Equals(localeCode, ActiveLocaleCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                candidates.Add(localeCode);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            var nextLocale = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return SetActiveLocale(nextLocale);
        }

        /// <summary>Returns the runtime display name for a locale code.</summary>
        public string GetDisplayName(string localeCode)
        {
            return _settings != null
                ? _settings.GetDisplayName(localeCode)
                : localeCode ?? string.Empty;
        }

        /// <summary>Loads the persisted player language choice when enabled.</summary>
        public string LoadPreferredLocale()
        {
            if (_settings == null ||
                !_settings.rememberPlayerChoice ||
                string.IsNullOrWhiteSpace(_settings.playerPrefsKey) ||
                !PlayerPrefs.HasKey(_settings.playerPrefsKey))
            {
                return string.Empty;
            }

            return PlayerPrefs.GetString(_settings.playerPrefsKey, string.Empty);
        }

        /// <summary>Saves the current active locale when enabled.</summary>
        public void SavePreferredLocale()
        {
            if (_settings == null ||
                !_settings.rememberPlayerChoice ||
                string.IsNullOrWhiteSpace(_settings.playerPrefsKey) ||
                string.IsNullOrWhiteSpace(ActiveLocaleCode))
            {
                return;
            }

            PlayerPrefs.SetString(_settings.playerPrefsKey, ActiveLocaleCode);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Resolves <paramref name="localeKey"/> against the active table.
        /// Returns <paramref name="fallback"/> when:
        /// <list type="bullet">
        ///   <item>No table is active.</item>
        ///   <item><paramref name="localeKey"/> is null or empty.</item>
        ///   <item>The key is not found in the active table.</item>
        /// </list>
        /// This method is allocation-free when returning the fallback.
        /// </summary>
        /// <param name="localeKey">Stable locale key stamped on the node.</param>
        /// <param name="fallback">Source-language string to show when resolution fails.</param>
        public string Resolve(string localeKey, string fallback)
        {
            if (_activeTable == null || string.IsNullOrWhiteSpace(localeKey))
                return fallback;

            var resolved = _activeTable.TryResolve(localeKey);
            return resolved ?? fallback;
        }

        /// <summary>
        /// Resets the singleton — useful for test teardown or session restarts.
        /// </summary>
        internal static void Reset() => _instance = null;

        private bool SetActiveLocale(string localeCode, bool saveChoice)
        {
            var table = FindTable(localeCode);
            if (table == null)
            {
                return false;
            }

            SetActiveTableInternal(table, fireEvent: true);
            if (saveChoice)
            {
                SavePreferredLocale();
            }

            return true;
        }

        private void SetActiveTableInternal(DialogLocalizationTable table, bool fireEvent)
        {
            if (_activeTable == table)
            {
                return;
            }

            _activeTable = table;
            if (fireEvent)
            {
                OnLocaleChanged?.Invoke();
            }
        }

        private DialogLocalizationTable FindTable(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return null;
            }

            return _availableTables.FirstOrDefault(table =>
                table != null &&
                string.Equals(table.LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase));
        }

        private DialogLocalizationTable FindSourceTable()
        {
            return _availableTables.FirstOrDefault(table => table != null && table.IsSourceLanguage);
        }
    }
}
