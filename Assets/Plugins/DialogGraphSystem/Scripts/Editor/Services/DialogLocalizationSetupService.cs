using DialogSystem.EditorTools.Inspectors;
using DialogSystem.EditorTools.Localization;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using UnityEditor;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// One-click setup flow for graph localization authoring.
    /// Ensures a source table exists, stamps missing locale keys, and syncs the
    /// selected graph's source strings into that table.
    /// </summary>
    public static class DialogLocalizationSetupService
    {
        public const string DefaultSourceLocaleCode = "en-US";

        public sealed class Result
        {
            public DialogLocalizationTable SourceTable { get; internal set; }
            public bool CreatedSourceTable { get; internal set; }
            public int AssignedLocaleKeys { get; internal set; }
            public int SyncedEntries { get; internal set; }

            public string Summary =>
                $"Localization setup complete: {AssignedLocaleKeys} key(s) assigned, " +
                $"{SyncedEntries} source entr{(SyncedEntries == 1 ? "y" : "ies")} synced.";
        }

        public static Result SetupGraph(DialogGraph graph, string sourceLocaleCode = DefaultSourceLocaleCode)
        {
            if (graph == null)
            {
                return new Result();
            }

            var registry = new DialogLocalizationRegistryService();
            var sourceTable = registry.GetSourceTable();
            var createdSourceTable = false;

            if (sourceTable == null)
            {
                sourceTable = registry.CreateNewTable(sourceLocaleCode, isSource: true);
                createdSourceTable = sourceTable != null;
            }

            return SetupGraph(graph, sourceTable, createdSourceTable);
        }

        public static Result SetupGraph(DialogGraph graph, DialogLocalizationTable sourceTable, bool createdSourceTable = false)
        {
            var result = new Result
            {
                SourceTable = sourceTable,
                CreatedSourceTable = createdSourceTable
            };

            if (graph == null || sourceTable == null)
            {
                return result;
            }

            result.AssignedLocaleKeys = DialogGraphLocalizationInspector.AssignKeysForGraph(graph, sourceTable);
            result.SyncedEntries = DialogLocalizationAutoSync.SyncGraphToTable(graph, sourceTable);
            if (AssetDatabase.Contains(sourceTable))
            {
                DialogLocalizationRuntimeSettingsService.SyncFromProjectTables(sourceTable.LocaleCode);
            }

            EditorUtility.SetDirty(graph);
            EditorUtility.SetDirty(sourceTable);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result;
        }
    }
}
