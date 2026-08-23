using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime.Localization
{
    /// <summary>
    /// Project-wide ScriptableObject that stores locale key → translated string
    /// mappings for one language. Create one asset per target locale under
    /// <c>Assets/DialogGraphSystem/Definitions/Localization/</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DialogLocalizationTable",
        menuName = "Beka Forge/Dialogues/Localization Table")]
    public class DialogLocalizationTable : ScriptableObject
    {
        #region -------- Settings --------

        [Header("Locale")]
        [Tooltip("BCP 47 locale code, e.g. 'en-US', 'fr-FR', 'ja-JP'.")]
        [SerializeField] private string _localeCode = "en-US";

        [Tooltip("Mark this table as the source / default language. Only one table should be the source.")]
        [SerializeField] private bool _isSourceLanguage = false;

        #endregion

        #region -------- Data --------

        [Header("Entries")]
        [SerializeField] private List<LocalizationEntry> _entries = new();

        #endregion

        #region -------- Lazy Lookup --------

        private Dictionary<string, string> _lookup;

        private Dictionary<string, string> Lookup
        {
            get
            {
                if (_lookup == null)
                    RebuildLookup();
                return _lookup;
            }
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            if (_entries == null) return;

            foreach (var entry in _entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
                _lookup[entry.key.Trim()] = entry.value ?? string.Empty;
            }
        }

        private void OnValidate() => _lookup = null;

        #endregion

        #region -------- Public API --------

        /// <summary>BCP 47 locale code for this table.</summary>
        public string LocaleCode => _localeCode;

        /// <summary>Returns true when this table represents the source / default language.</summary>
        public bool IsSourceLanguage => _isSourceLanguage;

        /// <summary>
        /// Configures locale metadata for editor authoring workflows.
        /// Runtime callers should treat table metadata as read-only.
        /// </summary>
        public void ConfigureMetadata(string localeCode, bool isSourceLanguage)
        {
            _localeCode = string.IsNullOrWhiteSpace(localeCode)
                ? "en-US"
                : localeCode.Trim();
            _isSourceLanguage = isSourceLanguage;
            _lookup = null;
        }

        /// <summary>
        /// Returns the translated string for <paramref name="localeKey"/>,
        /// or <c>null</c> when the key is not present in this table.
        /// </summary>
        public string TryResolve(string localeKey)
        {
            if (string.IsNullOrWhiteSpace(localeKey)) return null;
            return Lookup.TryGetValue(localeKey.Trim(), out var v) ? v : null;
        }

        /// <summary>Read-only view of all entries in this table.</summary>
        public IReadOnlyDictionary<string, string> AllEntries => Lookup;

        /// <summary>
        /// Writes or overwrites a single entry. Editor-only write path — call
        /// <c>EditorUtility.SetDirty</c> and <c>AssetDatabase.SaveAssets</c> after bulk writes.
        /// </summary>
        public void SetEntry(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            key = key.Trim();

            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] == null) continue;
                if (string.Equals(_entries[i].key, key, StringComparison.OrdinalIgnoreCase))
                {
                    _entries[i].value = value ?? string.Empty;
                    _lookup = null;
                    return;
                }
            }

            _entries.Add(new LocalizationEntry(key, value ?? string.Empty));
            _lookup = null;
        }

        /// <summary>Removes the entry with the given key. Editor write path — call SetDirty after.</summary>
        public bool RemoveEntry(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            key = key.Trim();
            var idx = _entries.FindIndex(e => string.Equals(e?.key, key, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return false;
            _entries.RemoveAt(idx);
            _lookup = null;
            return true;
        }

        /// <summary>Returns the total number of entries in this table.</summary>
        public int Count => _entries?.Count ?? 0;

        #endregion
    }
}
