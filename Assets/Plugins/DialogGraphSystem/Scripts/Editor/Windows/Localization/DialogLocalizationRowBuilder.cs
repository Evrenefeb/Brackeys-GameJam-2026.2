using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Builds localization table rows directly from a graph's authored content rather
    /// than from whatever happens to exist in the source table asset.
    /// </summary>
    public static class DialogLocalizationRowBuilder
    {
        public static IReadOnlyList<string> FindDuplicateKeys(DialogGraph graph)
        {
            if (graph == null)
            {
                return System.Array.Empty<string>();
            }

            return DialogLocalizationExporter.BuildSourceExport(graph)
                .entries
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.key))
                .GroupBy(entry => entry.key.Trim(), System.StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(key => key, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<LocRow> BuildRows(
            DialogGraph graph,
            IReadOnlyList<DialogLocalizationTable> allTables,
            IReadOnlyCollection<string> activeLocaleCodes)
        {
            var rows = new List<LocRow>();
            if (graph == null)
            {
                return rows;
            }

            var record = DialogLocalizationExporter.BuildSourceExport(graph);
            foreach (var entry in record.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                var row = new LocRow
                {
                    Key = entry.key,
                    GraphSlug = entry.graphSlug,
                    GraphTitle = graph.graphTitle ?? graph.name,
                    NodeGuid = entry.nodeGuid,
                    Speaker = entry.speakerName ?? string.Empty,
                    Context = entry.context ?? string.Empty,
                    EntryType = entry.entryType ?? string.Empty,
                    DisplayName = entry.displayName ?? string.Empty,
                    SourceText = entry.sourceText ?? string.Empty,
                    Translations = new Dictionary<string, string>()
                };

                var hasAnyMissing = false;
                foreach (var table in allTables ?? System.Array.Empty<DialogLocalizationTable>())
                {
                    if (table == null || table.IsSourceLanguage)
                    {
                        continue;
                    }

                    if (activeLocaleCodes != null &&
                        activeLocaleCodes.Count > 0 &&
                        !activeLocaleCodes.Contains(table.LocaleCode))
                    {
                        continue;
                    }

                    var translation = table.TryResolve(entry.key);
                    row.Translations[table.LocaleCode] = translation;
                    if (string.IsNullOrWhiteSpace(translation))
                    {
                        hasAnyMissing = true;
                    }
                }

                row.Status = row.Translations.Count == 0
                    ? OverallStatus.Translated
                    : (hasAnyMissing ? OverallStatus.Missing : OverallStatus.Translated);

                rows.Add(row);
            }

            return rows;
        }
    }
}
