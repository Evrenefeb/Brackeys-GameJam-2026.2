using System.Collections.Generic;
using DialogSystem.Runtime.Localization;
using UnityEditor;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Editor registry service for <see cref="DialogLocalizationTable"/> assets.
    /// Follows the same pattern as <see cref="DialogActionRegistryService"/>.
    /// </summary>
    public sealed class DialogLocalizationRegistryService
    {
        private const string DefaultFolder = "Assets/DialogGraphSystem/Definitions/Localization";

        /// <summary>Returns all <see cref="DialogLocalizationTable"/> assets found in the project.</summary>
        public IReadOnlyList<DialogLocalizationTable> GetAllTables()
        {
            return DialogDefinitionRegistryUtility.LoadAllAssets<DialogLocalizationTable>();
        }

        /// <summary>
        /// Returns the table flagged as the source / default language, or
        /// <c>null</c> when no source table exists.
        /// </summary>
        public DialogLocalizationTable GetSourceTable()
        {
            foreach (var table in GetAllTables())
            {
                if (table != null && table.IsSourceLanguage)
                    return table;
            }
            return null;
        }

        /// <summary>
        /// Returns the table whose <see cref="DialogLocalizationTable.LocaleCode"/>
        /// matches <paramref name="localeCode"/> (case-insensitive), or <c>null</c>
        /// when not found.
        /// </summary>
        public DialogLocalizationTable GetTableForLocale(string localeCode)
        {
            return DialogDefinitionRegistryUtility.FindById<DialogLocalizationTable>(
                asset => asset.LocaleCode,
                localeCode);
        }

        /// <summary>
        /// Returns all locale codes for which a table asset exists.
        /// </summary>
        public IReadOnlyList<string> GetAllLocaleCodes()
        {
            var codes = new List<string>();
            foreach (var table in GetAllTables())
            {
                if (table != null && !string.IsNullOrWhiteSpace(table.LocaleCode))
                    codes.Add(table.LocaleCode);
            }
            return codes;
        }

        /// <summary>
        /// Creates a new <see cref="DialogLocalizationTable"/> asset in the default
        /// definitions folder. Call <c>AssetDatabase.SaveAssets</c> after creation.
        /// </summary>
        /// <param name="localeCode">BCP 47 code for the new table, e.g. <c>"fr-FR"</c>.</param>
        /// <param name="isSource">Pass <c>true</c> to flag this as the source language.</param>
        public DialogLocalizationTable CreateNewTable(string localeCode, bool isSource = false)
        {
            var fileName = string.IsNullOrWhiteSpace(localeCode)
                ? "DialogLocalizationTable"
                : $"DialogLocalizationTable_{localeCode.Replace("-", "_")}";

            var table = DialogDefinitionRegistryUtility.CreateAsset<DialogLocalizationTable>(
                DefaultFolder, fileName);

            if (table == null)
            {
                return null;
            }

            ApplyTableMetadata(table, localeCode, isSource);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            return table;
        }

        public DialogLocalizationTable EnsureSourceTable(string localeCode = "en-US")
        {
            return GetSourceTable() ?? CreateNewTable(localeCode, isSource: true);
        }

        public void ApplyTableMetadata(
            DialogLocalizationTable table,
            string localeCode,
            bool isSource,
            IEnumerable<DialogLocalizationTable> existingTables = null)
        {
            if (table == null)
            {
                return;
            }

            if (isSource)
            {
                foreach (var existing in existingTables ?? GetAllTables())
                {
                    if (existing == null || existing == table || !existing.IsSourceLanguage)
                    {
                        continue;
                    }

                    existing.ConfigureMetadata(existing.LocaleCode, false);
                    EditorUtility.SetDirty(existing);
                }
            }

            table.ConfigureMetadata(localeCode, isSource);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
        }

        public bool DeleteTable(DialogLocalizationTable table, out string error)
        {
            error = string.Empty;

            if (table == null)
            {
                error = "Could not remove language: localization table is null.";
                return false;
            }

            if (table.IsSourceLanguage)
            {
                error = "Could not remove the source language table.";
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(table);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "Could not remove language: asset path was not found.";
                return false;
            }

            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                error = $"Could not remove language at '{assetPath}'.";
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }
    }
}
