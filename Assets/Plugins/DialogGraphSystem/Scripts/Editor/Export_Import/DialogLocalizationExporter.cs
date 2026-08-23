using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using UnityEngine;

namespace DialogSystem.EditorTools.ExportImport
{
    /// <summary>
    /// Editor-only utility that walks a <see cref="DialogGraph"/> and builds
    /// <see cref="LocalizationExportRecord"/> DTOs ready for JSON serialization.
    /// Follows the same pattern as <see cref="DialogExportUtility"/>.
    /// </summary>
    public static class DialogLocalizationExporter
    {
        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a source-language export for <paramref name="graph"/>.
        /// <c>translatedText</c> is left empty — this is the template a
        /// translator fills in.
        /// </summary>
        /// <param name="graph">The graph to export strings from.</param>
        /// <param name="localeCode">
        /// Target locale code for the export file, e.g. <c>"fr-FR"</c>.
        /// Pass <c>null</c> to produce a generic template.
        /// </param>
        public static LocalizationExportRecord BuildSourceExport(
            DialogGraph graph,
            string localeCode = null)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var record = CreateRecord(graph, localeCode ?? "TEMPLATE");
            CollectDialogNodes(graph, record, sourceTable: null);
            CollectChoiceNodes(graph, record, sourceTable: null);
            return record;
        }

        /// <summary>
        /// Builds an export pre-filled with existing translations from
        /// <paramref name="targetTable"/>. Use when updating a partially
        /// translated file.
        /// </summary>
        public static LocalizationExportRecord BuildTranslationExport(
            DialogGraph graph,
            DialogLocalizationTable targetTable)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var locale = targetTable != null ? targetTable.LocaleCode : "TEMPLATE";
            var record = CreateRecord(graph, locale);
            CollectDialogNodes(graph, record, targetTable);
            CollectChoiceNodes(graph, record, targetTable);
            return record;
        }

        /// <summary>
        /// Builds source exports for every graph in the project.
        /// </summary>
        /// <param name="localeCode">Target locale for the export files.</param>
        public static List<LocalizationExportRecord> BuildSourceExportAll(string localeCode = null)
        {
            var results = new List<LocalizationExportRecord>();

            foreach (var graph in LoadAllGraphAssets())
            {
                try
                {
                    results.Add(BuildSourceExport(graph, localeCode));
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"[DialogLocalizationExporter] Skipped graph '{graph?.name}': {e.Message}");
                }
            }

            return results;
        }

        /// <summary>Serializes <paramref name="record"/> to a pretty-printed JSON string.</summary>
        public static string ToJson(LocalizationExportRecord record)
        {
            return JsonUtility.ToJson(record, prettyPrint: true);
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static LocalizationExportRecord CreateRecord(DialogGraph graph, string localeCode)
        {
            return new LocalizationExportRecord
            {
                localeCode     = localeCode ?? string.Empty,
                graphGuid      = graph.GraphGuid ?? string.Empty,
                graphTitle     = graph.graphTitle ?? graph.name,
                exportedAtUtc  = DateTime.UtcNow.ToString("o")
            };
        }

        private static void CollectDialogNodes(
            DialogGraph graph,
            LocalizationExportRecord record,
            DialogLocalizationTable sourceTable)
        {
            var graphSlug = DialogSystem.EditorTools.Localization.DialogLocaleKeyGenerator.Slugify(
                graph.graphTitle ?? graph.name);

            foreach (var node in graph.nodes ?? Enumerable.Empty<DialogNode>())
            {
                if (node == null) continue;

                if (node.HasSpeakerLocaleKey && !string.IsNullOrWhiteSpace(node.speakerName))
                {
                    var existingSpeaker = sourceTable?.TryResolve(node.speakerNameLocaleKey);
                    record.entries.Add(new LocalizationExportEntry
                    {
                        key            = node.speakerNameLocaleKey,
                        sourceText     = node.speakerName ?? string.Empty,
                        translatedText = existingSpeaker ?? string.Empty,
                        context        = "speaker_name",
                        graphSlug      = graphSlug,
                        nodeGuid       = node.GetGuid(),
                        speakerName    = node.speakerName ?? string.Empty,
                        entryType      = "speaker_name",
                        displayName    = "Speaker Name"
                    });
                }

                if (!node.HasQuestionLocaleKey)
                {
                    Debug.LogWarning(
                        $"[DialogLocalizationExporter] Dialog node '{node.GetGuid()}' in '{graph.name}' " +
                        "has no questionTextLocaleKey — skipping. Assign keys with the Localization inspector tool.");
                    continue;
                }

                var existing = sourceTable?.TryResolve(node.questionTextLocaleKey);
                record.entries.Add(new LocalizationExportEntry
                {
                    key            = node.questionTextLocaleKey,
                    sourceText     = node.questionText ?? string.Empty,
                    translatedText = existing ?? string.Empty,
                    context        = $"dialog:{node.speakerName ?? "unknown"}",
                    graphSlug      = graphSlug,
                    nodeGuid       = node.GetGuid(),
                    speakerName    = node.speakerName ?? string.Empty,
                    entryType      = "dialog_text",
                    displayName    = "Dialog Text"
                });
            }
        }

        private static void CollectChoiceNodes(
            DialogGraph graph,
            LocalizationExportRecord record,
            DialogLocalizationTable sourceTable)
        {
            var graphSlug = DialogSystem.EditorTools.Localization.DialogLocaleKeyGenerator.Slugify(
                graph.graphTitle ?? graph.name);

            foreach (var node in graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (node == null) continue;

                // Prompt text
                if (!string.IsNullOrWhiteSpace(node.textLocaleKey) &&
                    !string.IsNullOrWhiteSpace(node.text))
                {
                    var existing = sourceTable?.TryResolve(node.textLocaleKey);
                    record.entries.Add(new LocalizationExportEntry
                    {
                        key            = node.textLocaleKey,
                        sourceText     = node.text,
                        translatedText = existing ?? string.Empty,
                        context        = "choice_prompt",
                        graphSlug      = graphSlug,
                        nodeGuid       = node.GetGuid(),
                        speakerName    = string.Empty,
                        entryType      = "choice_prompt",
                        displayName    = "Choice Prompt"
                    });
                }

                // Individual answers
                if (node.choices == null) continue;
                for (var i = 0; i < node.choices.Count; i++)
                {
                    var choice = node.choices[i];
                    if (choice == null) continue;

                    if (!choice.HasAnswerLocaleKey)
                    {
                        Debug.LogWarning(
                            $"[DialogLocalizationExporter] Choice node '{node.GetGuid()}', " +
                            $"choice index {i} in '{graph.name}' has no answerTextLocaleKey — skipping.");
                        continue;
                    }

                    var existing = sourceTable?.TryResolve(choice.answerTextLocaleKey);
                    record.entries.Add(new LocalizationExportEntry
                    {
                        key            = choice.answerTextLocaleKey,
                        sourceText     = choice.answerText ?? string.Empty,
                        translatedText = existing ?? string.Empty,
                        context        = $"choice:{i}",
                        graphSlug      = graphSlug,
                        nodeGuid       = node.GetGuid(),
                        speakerName    = string.Empty,
                        entryType      = "choice_answer",
                        displayName    = $"Choice {i + 1}"
                    });
                }
            }
        }

        private static IEnumerable<DialogGraph> LoadAllGraphAssets()
        {
            return DialogGraphAssetPaths.LoadAllGraphAssets();
        }
    }
}
