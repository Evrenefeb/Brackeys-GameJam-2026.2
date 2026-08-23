using System;

namespace DialogSystem.Runtime.Localization
{
    /// <summary>
    /// Serializable key-value pair backing <see cref="DialogLocalizationTable"/>.
    /// Stored as a list so Unity can serialize it without a Dictionary.
    /// </summary>
    [Serializable]
    public class LocalizationEntry
    {
        /// <summary>Stable locale key, e.g. <c>npc_001.line_003.text</c>.</summary>
        public string key;

        /// <summary>Translated string for the locale this table represents.</summary>
        public string value;

        public LocalizationEntry() { }

        public LocalizationEntry(string key, string value)
        {
            this.key   = key;
            this.value = value;
        }
    }
}
