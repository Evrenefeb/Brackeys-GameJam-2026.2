using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using UnityEditor;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Editor-only service that automatically stamps locale keys onto newly created or
    /// edited nodes and keeps the source <see cref="DialogLocalizationTable"/> in sync.
    ///
    /// Rules:
    ///  - Keys are generated once at node creation and never regenerated automatically.
    ///  - Any text edit to a keyed field writes the new value into the source table.
    ///  - No source table → silently no-ops (the user can create one later).
    /// </summary>
    public static class DialogLocalizationAutoSync
    {
        // ── Public entry points ───────────────────────────────────────────────

        /// <summary>
        /// Call immediately after a new <see cref="DialogNode"/> data asset is created.
        /// Stamps keys and registers empty source-table entries.
        /// </summary>
        public static void OnDialogNodeCreated(DialogNode node, DialogGraph graph)
        {
            if (node == null || graph == null) return;

            var graphTitle = graph.graphTitle ?? graph.name;

            if (string.IsNullOrWhiteSpace(node.questionTextLocaleKey))
            {
                node.questionTextLocaleKey = DialogLocaleKeyGenerator.GenerateDialogNodeKey(
                    graphTitle, node.GetGuid());
            }

            if (!string.IsNullOrWhiteSpace(node.speakerName) &&
                string.IsNullOrWhiteSpace(node.speakerNameLocaleKey))
            {
                node.speakerNameLocaleKey = DialogLocaleKeyGenerator.GenerateSpeakerNameKey(
                    graphTitle, node.GetGuid());
            }

            EditorUtility.SetDirty(node);
            SyncNodeToSourceTable(node, graph);
        }

        /// <summary>
        /// Call immediately after a new <see cref="ChoiceNode"/> data asset is created.
        /// Stamps keys and registers empty source-table entries.
        /// </summary>
        public static void OnChoiceNodeCreated(ChoiceNode node, DialogGraph graph)
        {
            if (node == null || graph == null) return;

            var graphTitle = graph.graphTitle ?? graph.name;

            if (!string.IsNullOrWhiteSpace(node.text) &&
                string.IsNullOrWhiteSpace(node.textLocaleKey))
            {
                node.textLocaleKey = DialogLocaleKeyGenerator.GenerateChoiceNodePromptKey(
                    graphTitle, node.GetGuid());
            }

            if (node.choices != null)
            {
                node.AssignMissingChoiceIds();
                foreach (var choice in node.choices)
                {
                    if (choice == null) continue;
                    if (!string.IsNullOrWhiteSpace(choice.answerText) &&
                        string.IsNullOrWhiteSpace(choice.answerTextLocaleKey))
                    {
                        choice.answerTextLocaleKey = DialogLocaleKeyGenerator.GenerateChoiceKey(
                            graphTitle, node.GetGuid(), choice.choiceId);
                    }
                }
            }

            EditorUtility.SetDirty(node);
            SyncChoiceNodeToSourceTable(node, graph);
        }

        /// <summary>
        /// Call whenever <see cref="DialogNode.questionText"/> changes.
        /// Ensures a key exists and updates the source table.
        /// </summary>
        public static void OnDialogTextChanged(DialogNode node, DialogGraph graph, string newText)
        {
            if (node == null || graph == null) return;

            var graphTitle = graph.graphTitle ?? graph.name;

            if (string.IsNullOrWhiteSpace(node.questionTextLocaleKey))
            {
                node.questionTextLocaleKey = DialogLocaleKeyGenerator.GenerateDialogNodeKey(
                    graphTitle, node.GetGuid());
                EditorUtility.SetDirty(node);
            }

            WriteToSourceTable(node.questionTextLocaleKey, newText);
        }

        /// <summary>
        /// Call whenever <see cref="DialogNode.speakerName"/> changes.
        /// Ensures a key exists and updates the source table.
        /// </summary>
        public static void OnSpeakerNameChanged(DialogNode node, DialogGraph graph, string newName)
        {
            if (node == null || graph == null) return;
            if (string.IsNullOrWhiteSpace(newName)) return;

            var graphTitle = graph.graphTitle ?? graph.name;

            if (string.IsNullOrWhiteSpace(node.speakerNameLocaleKey))
            {
                node.speakerNameLocaleKey = DialogLocaleKeyGenerator.GenerateSpeakerNameKey(
                    graphTitle, node.GetGuid());
                EditorUtility.SetDirty(node);
            }

            WriteToSourceTable(node.speakerNameLocaleKey, newName);
        }

        /// <summary>
        /// Call whenever a choice's <see cref="Choice.answerText"/> changes.
        /// Ensures a key exists and updates the source table.
        /// </summary>
        public static void OnChoiceAnswerChanged(ChoiceNode node, DialogGraph graph, Choice choice, string newText)
        {
            if (node == null || graph == null || choice == null) return;

            var graphTitle = graph.graphTitle ?? graph.name;

            if (!choice.HasChoiceId)
                choice.AssignChoiceIdIfMissing(Choice.CreateChoiceId());

            if (string.IsNullOrWhiteSpace(choice.answerTextLocaleKey))
            {
                choice.answerTextLocaleKey = DialogLocaleKeyGenerator.GenerateChoiceKey(
                    graphTitle, node.GetGuid(), choice.choiceId);
                EditorUtility.SetDirty(node);
            }

            WriteToSourceTable(choice.answerTextLocaleKey, newText);
        }

        /// <summary>
        /// Call whenever a choice prompt text changes on a <see cref="ChoiceNode"/>.
        /// </summary>
        public static void OnChoicePromptChanged(ChoiceNode node, DialogGraph graph, string newText)
        {
            if (node == null || graph == null) return;

            var graphTitle = graph.graphTitle ?? graph.name;

            if (string.IsNullOrWhiteSpace(node.textLocaleKey))
            {
                node.textLocaleKey = DialogLocaleKeyGenerator.GenerateChoiceNodePromptKey(
                    graphTitle, node.GetGuid());
                EditorUtility.SetDirty(node);
            }

            WriteToSourceTable(node.textLocaleKey, newText);
        }

        /// <summary>
        /// Synchronizes every keyed source string from <paramref name="graph"/> into
        /// <paramref name="table"/>. Returns the number of entries written.
        /// </summary>
        public static int SyncGraphToTable(DialogGraph graph, DialogLocalizationTable table = null)
        {
            if (graph == null)
            {
                return 0;
            }

            table ??= GetSourceTable();
            if (table == null)
            {
                return 0;
            }

            var written = 0;

            if (graph.nodes != null)
            {
                foreach (var node in graph.nodes)
                {
                    if (node == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(node.questionTextLocaleKey))
                    {
                        table.SetEntry(node.questionTextLocaleKey, node.questionText ?? string.Empty);
                        written++;
                    }

                    if (!string.IsNullOrWhiteSpace(node.speakerNameLocaleKey))
                    {
                        table.SetEntry(node.speakerNameLocaleKey, node.speakerName ?? string.Empty);
                        written++;
                    }
                }
            }

            if (graph.choiceNodes != null)
            {
                foreach (var node in graph.choiceNodes)
                {
                    if (node == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(node.textLocaleKey))
                    {
                        table.SetEntry(node.textLocaleKey, node.text ?? string.Empty);
                        written++;
                    }

                    if (node.choices == null)
                    {
                        continue;
                    }

                    foreach (var choice in node.choices)
                    {
                        if (choice == null || string.IsNullOrWhiteSpace(choice.answerTextLocaleKey))
                        {
                            continue;
                        }

                        table.SetEntry(choice.answerTextLocaleKey, choice.answerText ?? string.Empty);
                        written++;
                    }
                }
            }

            if (written > 0)
            {
                EditorUtility.SetDirty(table);
            }

            return written;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private static void SyncNodeToSourceTable(DialogNode node, DialogGraph graph)
        {
            var table = GetSourceTable();
            if (table == null) return;

            if (!string.IsNullOrWhiteSpace(node.questionTextLocaleKey))
                table.SetEntry(node.questionTextLocaleKey, node.questionText ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(node.speakerNameLocaleKey))
                table.SetEntry(node.speakerNameLocaleKey, node.speakerName ?? string.Empty);

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
        }

        private static void SyncChoiceNodeToSourceTable(ChoiceNode node, DialogGraph graph)
        {
            var table = GetSourceTable();
            if (table == null) return;

            if (!string.IsNullOrWhiteSpace(node.textLocaleKey))
                table.SetEntry(node.textLocaleKey, node.text ?? string.Empty);

            if (node.choices != null)
            {
                foreach (var choice in node.choices)
                {
                    if (choice == null) continue;
                    if (!string.IsNullOrWhiteSpace(choice.answerTextLocaleKey))
                        table.SetEntry(choice.answerTextLocaleKey, choice.answerText ?? string.Empty);
                }
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
        }

        private static void WriteToSourceTable(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var table = GetSourceTable();
            if (table == null) return;

            table.SetEntry(key, value ?? string.Empty);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
        }

        private static DialogLocalizationTable GetSourceTable()
        {
            var service = new DialogLocalizationRegistryService();
            return service.GetSourceTable();
        }
    }
}
