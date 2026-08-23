using System.Collections.Generic;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Lightweight display-time DTO representing one row in the Localization Manager table.
    /// One row = one locale key across all loaded tables.
    /// </summary>
    public class LocRow
    {
        /// <summary>Full locale key, e.g. <c>demo_knight.0c321f00.text</c>.</summary>
        public string Key;

        /// <summary>Prefix before the first dot — used to populate the dialogue filter dropdown.</summary>
        public string GraphSlug;

        /// <summary>Human-readable graph title for UI context.</summary>
        public string GraphTitle;

        /// <summary>Owning node GUID for this localized field.</summary>
        public string NodeGuid;

        /// <summary>Speaker name resolved from the key's graph node (may be empty).</summary>
        public string Speaker;

        /// <summary>Human-readable context string such as dialog:Kira or choice_prompt.</summary>
        public string Context;

        /// <summary>Semantic field type, e.g. dialog_text or choice_answer.</summary>
        public string EntryType;

        /// <summary>Short display label shown in the localization UI.</summary>
        public string DisplayName;

        /// <summary>The translated value from the source-language table.</summary>
        public string SourceText;

        /// <summary>Per-language translations keyed by locale code. A <c>null</c> value means the entry is missing.</summary>
        public Dictionary<string, string> Translations = new();

        /// <summary>Worst-case status across all target languages.</summary>
        public OverallStatus Status;
    }

    /// <summary>Represents the worst-case translation completeness for a <see cref="LocRow"/>.</summary>
    public enum OverallStatus
    {
        Missing,
        Translated
    }
}
