using System;
using System.Collections.Generic;
using System.IO;
using DialogSystem.Runtime.Localization;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.ExportImport
{
    /// <summary>
    /// Editor-only utility that reads a <see cref="LocalizationExportRecord"/> JSON
    /// and writes entries into a <see cref="DialogLocalizationTable"/> asset.
    /// </summary>
    public static class DialogLocalizationImporter
    {
        private const string SupportedSchemaVersion = "loc-1.0";

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Parses <paramref name="json"/> and merges entries into <paramref name="targetTable"/>.</summary>
        /// <returns>An <see cref="ImportResult"/> with counts and any warnings.</returns>
        public static ImportResult ImportFromJson(string json, DialogLocalizationTable targetTable)
        {
            if (string.IsNullOrWhiteSpace(json))
                return ImportResult.Failure("Import failed: JSON string is empty.");
            if (targetTable == null)
                return ImportResult.Failure("Import failed: target table is null.");

            LocalizationExportRecord record;
            try
            {
                record = JsonUtility.FromJson<LocalizationExportRecord>(json);
            }
            catch (Exception e)
            {
                return ImportResult.Failure($"Import failed: could not parse JSON. {e.Message}");
            }

            if (record == null)
                return ImportResult.Failure("Import failed: parsed record is null.");

            return ApplyRecord(record, targetTable);
        }

        /// <summary>
        /// Reads a JSON file at <paramref name="absolutePath"/> and merges entries
        /// into <paramref name="targetTable"/>.
        /// </summary>
        public static ImportResult ImportFromFile(string absolutePath, DialogLocalizationTable targetTable)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
                return ImportResult.Failure("Import failed: file path is empty.");
            if (!File.Exists(absolutePath))
                return ImportResult.Failure($"Import failed: file not found at '{absolutePath}'.");

            string json;
            try
            {
                json = File.ReadAllText(absolutePath);
            }
            catch (Exception e)
            {
                return ImportResult.Failure($"Import failed: could not read file. {e.Message}");
            }

            return ImportFromJson(json, targetTable);
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static ImportResult ApplyRecord(
            LocalizationExportRecord record,
            DialogLocalizationTable targetTable)
        {
            var result = new ImportResult();

            if (!string.Equals(record.schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
            {
                result.Warnings.Add(
                    $"Schema version '{record.schemaVersion}' differs from expected '{SupportedSchemaVersion}'. " +
                    "Attempting import anyway.");
            }

            if (record.entries == null || record.entries.Count == 0)
            {
                result.Warnings.Add("No entries found in the import file.");
                return result;
            }

            foreach (var entry in record.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    result.Skipped++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.translatedText))
                {
                    result.Skipped++;
                    continue;
                }

                var existing = targetTable.TryResolve(entry.key);
                targetTable.SetEntry(entry.key, entry.translatedText);

                if (existing == null)
                    result.Added++;
                else
                    result.Updated++;
            }

            if (result.Added > 0 || result.Updated > 0)
            {
                EditorUtility.SetDirty(targetTable);
                AssetDatabase.SaveAssets();
            }

            return result;
        }
    }

    /// <summary>
    /// Result returned by <see cref="DialogLocalizationImporter"/> import operations.
    /// </summary>
    public sealed class ImportResult
    {
        /// <summary>Number of new keys added to the table.</summary>
        public int Added { get; set; }

        /// <summary>Number of existing keys whose values were updated.</summary>
        public int Updated { get; set; }

        /// <summary>Number of entries skipped (empty translation or blank key).</summary>
        public int Skipped { get; set; }

        /// <summary>Non-fatal warnings accumulated during import.</summary>
        public List<string> Warnings { get; } = new();

        /// <summary>When non-null, the import failed with this message.</summary>
        public string Error { get; private set; }

        /// <summary>Returns true when the import completed without a fatal error.</summary>
        public bool IsSuccess => Error == null;

        /// <summary>Summary line for display in editor UI.</summary>
        public string Summary =>
            IsSuccess
                ? $"Import complete: {Added} added, {Updated} updated, {Skipped} skipped."
                : $"Import failed: {Error}";

        internal static ImportResult Failure(string error) => new ImportResult { Error = error };
    }
}
