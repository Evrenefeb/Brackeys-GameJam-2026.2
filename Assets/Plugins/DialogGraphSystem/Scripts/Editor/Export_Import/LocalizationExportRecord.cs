using System;
using System.Collections.Generic;

namespace DialogSystem.EditorTools.ExportImport
{
    /// <summary>
    /// Root DTO for a localization export file. Contains all translatable strings
    /// for one graph in one target locale.
    /// Serialized with <c>JsonUtility</c> — no external libraries required.
    /// </summary>
    [Serializable]
    public class LocalizationExportRecord
    {
        /// <summary>Schema version for forward-compatibility checks.</summary>
        public string schemaVersion = "loc-1.0";

        /// <summary>BCP 47 locale code this file targets, e.g. <c>"fr-FR"</c>.</summary>
        public string localeCode;

        /// <summary>GUID of the graph these strings come from.</summary>
        public string graphGuid;

        /// <summary>Human-readable title of the source graph.</summary>
        public string graphTitle;

        /// <summary>UTC timestamp of export, ISO 8601.</summary>
        public string exportedAtUtc;

        /// <summary>All translatable string pairs for this graph + locale.</summary>
        public List<LocalizationExportEntry> entries = new();
    }

    /// <summary>
    /// One translatable string pair: source text paired with its translation slot.
    /// </summary>
    [Serializable]
    public class LocalizationExportEntry
    {
        /// <summary>Stable locale key stamped on the node.</summary>
        public string key;

        /// <summary>
        /// Source (default language) string. Filled in during export;
        /// translators use this as the reference text.
        /// </summary>
        public string sourceText;

        /// <summary>
        /// Translated string for <c>localeCode</c>. Empty in a freshly
        /// exported template; filled in by the translator.
        /// </summary>
        public string translatedText;

        /// <summary>
        /// Human-readable context hint for the translator, e.g.
        /// <c>"dialog:Elara"</c> or <c>"choice:Elara"</c>.
        /// </summary>
        public string context;

        /// <summary>Graph slug derived from the owning graph title.</summary>
        public string graphSlug;

        /// <summary>Owning node GUID for editor-side context and tooling.</summary>
        public string nodeGuid;

        /// <summary>Optional speaker name associated with this string.</summary>
        public string speakerName;

        /// <summary>Semantic type such as dialog_text, speaker_name, choice_prompt, or choice_answer.</summary>
        public string entryType;

        /// <summary>Short UI label for the translated field.</summary>
        public string displayName;
    }
}
