using System.Linq;
using DialogSystem.EditorTools.Localization;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Inspectors
{
    /// <summary>
    /// Adds batch locale key assignment tools to <see cref="DialogGraph"/> assets
    /// via the Assets context menu and the graph asset Inspector header.
    ///
    /// Use "Assign Missing Locale Keys" to stamp stable keys on every node in the
    /// selected graph(s) that is still missing one.
    /// Existing keys are never overwritten.
    /// </summary>
    public static class DialogGraphLocalizationInspector
    {
        private const string MenuPath = "Assets/Dialogue Graph/Assign Missing Locale Keys";
        private const string RebuildMenuPath = "Assets/Dialogue Graph/Rebuild Locale Keys";

        [MenuItem(MenuPath, validate = false)]
        public static void AssignMissingLocaleKeys()
        {
            var graphs = Selection.objects
                .OfType<DialogGraph>()
                .ToList();

            if (graphs.Count == 0)
            {
                Debug.LogWarning("[DialogGraphLocalizationInspector] No DialogGraph asset selected.");
                return;
            }

            var totalAssigned = 0;
            foreach (var graph in graphs)
                totalAssigned += AssignKeysForGraph(graph);

            if (totalAssigned > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"[DialogGraphLocalizationInspector] Assigned {totalAssigned} missing locale " +
                $"key(s) across {graphs.Count} graph(s).");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ValidateAssignMissingLocaleKeys()
        {
            return Selection.objects.OfType<DialogGraph>().Any();
        }

        [MenuItem(RebuildMenuPath, validate = false)]
        public static void RebuildLocaleKeys()
        {
            var graphs = Selection.objects
                .OfType<DialogGraph>()
                .ToList();

            if (graphs.Count == 0)
            {
                Debug.LogWarning("[DialogGraphLocalizationInspector] No DialogGraph asset selected.");
                return;
            }

            var totalRebuilt = 0;
            foreach (var graph in graphs)
            {
                totalRebuilt += RebuildKeysForGraph(graph);
            }

            if (totalRebuilt > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"[DialogGraphLocalizationInspector] Rebuilt {totalRebuilt} locale key(s) across {graphs.Count} graph(s).");
        }

        [MenuItem(RebuildMenuPath, validate = true)]
        private static bool ValidateRebuildLocaleKeys()
        {
            return ValidateAssignMissingLocaleKeys();
        }

        // ── Internals ─────────────────────────────────────────────────────────

        /// <summary>
        /// Assigns missing locale keys to all nodes inside <paramref name="graph"/>.
        /// Returns the total number of keys assigned.
        /// </summary>
        public static int AssignKeysForGraph(
            DialogGraph graph,
            DialogLocalizationTable targetTable = null)
        {
            if (graph == null) return 0;

            var title    = graph.graphTitle ?? graph.name;
            var assigned = 0;

            // Dialog nodes — questionText
            foreach (var node in graph.nodes ?? Enumerable.Empty<DialogNode>())
            {
                if (node == null) continue;

                if (!node.HasQuestionLocaleKey)
                {
                    node.questionTextLocaleKey = DialogLocaleKeyGenerator.GenerateDialogNodeKey(
                        title, node.GetGuid());
                    EditorUtility.SetDirty(node);
                    assigned++;
                }

                if (!string.IsNullOrWhiteSpace(node.speakerName) &&
                    !node.HasSpeakerLocaleKey)
                {
                    node.speakerNameLocaleKey = DialogLocaleKeyGenerator.GenerateSpeakerNameKey(
                        title, node.GetGuid());
                    EditorUtility.SetDirty(node);
                    assigned++;
                }
            }

            // Choice nodes — prompt text and each choice's answer text
            foreach (var node in graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (node == null) continue;

                node.AssignMissingChoiceIds();

                if (!string.IsNullOrWhiteSpace(node.text) &&
                    string.IsNullOrWhiteSpace(node.textLocaleKey))
                {
                    node.textLocaleKey = DialogLocaleKeyGenerator.GenerateChoiceNodePromptKey(
                        title, node.GetGuid());
                    EditorUtility.SetDirty(node);
                    assigned++;
                }

                if (node.choices == null) continue;
                foreach (var choice in node.choices)
                {
                    if (choice == null) continue;
                    if (!choice.HasAnswerLocaleKey && choice.HasChoiceId)
                    {
                        choice.answerTextLocaleKey = DialogLocaleKeyGenerator.GenerateChoiceKey(
                            title, node.GetGuid(), choice.choiceId);
                        EditorUtility.SetDirty(node);
                        assigned++;
                    }
                }
            }

            if (assigned > 0)
            {
                DialogLocalizationAutoSync.SyncGraphToTable(graph, targetTable);
            }

            if (assigned > 0)
                EditorUtility.SetDirty(graph);

            return assigned;
        }

        /// <summary>
        /// Rebuilds locale keys for all localized fields in <paramref name="graph"/>.
        /// Existing keys are overwritten with the current generator output.
        /// </summary>
        public static int RebuildKeysForGraph(DialogGraph graph)
        {
            if (graph == null) return 0;

            var title = graph.graphTitle ?? graph.name;
            var rebuilt = 0;

            foreach (var node in graph.nodes ?? Enumerable.Empty<DialogNode>())
            {
                if (node == null) continue;

                var questionKey = DialogLocaleKeyGenerator.GenerateDialogNodeKey(title, node.GetGuid());
                if (!string.Equals(node.questionTextLocaleKey, questionKey))
                {
                    node.questionTextLocaleKey = questionKey;
                    rebuilt++;
                }

                if (!string.IsNullOrWhiteSpace(node.speakerName))
                {
                    var speakerKey = DialogLocaleKeyGenerator.GenerateSpeakerNameKey(title, node.GetGuid());
                    if (!string.Equals(node.speakerNameLocaleKey, speakerKey))
                    {
                        node.speakerNameLocaleKey = speakerKey;
                        rebuilt++;
                    }
                }

                EditorUtility.SetDirty(node);
            }

            foreach (var node in graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (node == null) continue;

                node.AssignMissingChoiceIds();

                if (!string.IsNullOrWhiteSpace(node.text))
                {
                    var promptKey = DialogLocaleKeyGenerator.GenerateChoiceNodePromptKey(title, node.GetGuid());
                    if (!string.Equals(node.textLocaleKey, promptKey))
                    {
                        node.textLocaleKey = promptKey;
                        rebuilt++;
                    }
                }

                if (node.choices != null)
                {
                    foreach (var choice in node.choices)
                    {
                        if (choice == null || !choice.HasChoiceId) continue;

                        var answerKey = DialogLocaleKeyGenerator.GenerateChoiceKey(title, node.GetGuid(), choice.choiceId);
                        if (!string.Equals(choice.answerTextLocaleKey, answerKey))
                        {
                            choice.answerTextLocaleKey = answerKey;
                            rebuilt++;
                        }
                    }
                }

                EditorUtility.SetDirty(node);
            }

            if (rebuilt > 0)
            {
                DialogLocalizationAutoSync.SyncGraphToTable(graph);
                EditorUtility.SetDirty(graph);
            }

            return rebuilt;
        }
    }
}
