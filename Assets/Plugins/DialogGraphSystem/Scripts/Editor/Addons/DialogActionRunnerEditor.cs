#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Models;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Addons
{
    [CustomEditor(typeof(DialogActionRunner))]
    public class DialogActionRunnerEditor : UnityEditor.Editor
    {
        private SerializedProperty _global;
        private SerializedProperty _dialogueSets;
        private SerializedProperty _useGlobalFallback;

        private void OnEnable()
        {
            _global = serializedObject.FindProperty("global");
            _dialogueSets = serializedObject.FindProperty("dialogueSets");
            _useGlobalFallback = serializedObject.FindProperty("useGlobalFallback");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();

            EditorGUILayout.HelpBox(
                "Use Global Actions for handlers shared by every dialogue. " +
                "Use Dialogue Sets when a handler should only run for a specific dialogID from DialogManager.",
                MessageType.Info);

            EditorGUILayout.PropertyField(_useGlobalFallback);
            EditorGUILayout.Space(4f);

            EditorGUILayout.PropertyField(_global, new GUIContent("Global Actions"), true);
            EditorGUILayout.PropertyField(_dialogueSets, new GUIContent("Dialogue Sets"), true);

            EditorGUILayout.Space(8f);
            DrawSyncButtons();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Script",
                    MonoScript.FromMonoBehaviour((MonoBehaviour)target),
                    typeof(DialogActionRunner),
                    false);
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawSyncButtons()
        {
            if (GUILayout.Button("Add Missing Global Bindings From All Graph Assets", GUILayout.Height(24f)))
            {
                var runner = (DialogActionRunner)target;
                var discovery = new DialogActionDiscoveryService();
                var addedCount = EnsureBindings(runner.global, discovery.GetAllDiscoveredActionIds());

                EditorUtility.SetDirty(runner);
                serializedObject.Update();
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog(
                    "Dialog Action Runner",
                    addedCount > 0
                        ? $"Added {addedCount} missing global action binding(s)."
                        : "No new global action bindings were needed.",
                    "OK");
            }

            if (GUILayout.Button("Sync Dialogue Sets From Dialog Managers In Scene", GUILayout.Height(24f)))
            {
                var runner = (DialogActionRunner)target;
                var summary = SyncDialogueSetsFromScene(runner);

                EditorUtility.SetDirty(runner);
                serializedObject.Update();
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog(
                    "Dialog Action Runner",
                    summary,
                    "OK");
            }
        }

        private static int EnsureBindings(ConversationActionSet actionSet, IEnumerable<string> actionIds)
        {
            if (actionSet == null || actionIds == null)
            {
                return 0;
            }

            actionSet.bindings ??= new List<ActionBinding>();
            var existingIds = new HashSet<string>(
                actionSet.bindings
                    .Where(binding => binding != null && !string.IsNullOrWhiteSpace(binding.actionId))
                    .Select(binding => binding.actionId.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var addedCount = 0;
            foreach (var actionId in actionIds
                         .Where(actionId => !string.IsNullOrWhiteSpace(actionId))
                         .Select(actionId => actionId.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(actionId => actionId, StringComparer.OrdinalIgnoreCase))
            {
                if (!existingIds.Add(actionId))
                {
                    continue;
                }

                actionSet.bindings.Add(new ActionBinding { actionId = actionId });
                addedCount++;
            }

            return addedCount;
        }

        private static string SyncDialogueSetsFromScene(DialogActionRunner runner)
        {
            if (runner == null)
            {
                return "No DialogActionRunner selected.";
            }

            runner.dialogueSets ??= new List<ConversationActionSet>();
            var existingByKey = runner.dialogueSets
                .Where(set => set != null && !string.IsNullOrWhiteSpace(set.dialogueKey))
                .GroupBy(set => set.dialogueKey.Trim().ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var managers = UnityEngine.Resources.FindObjectsOfTypeAll<DialogManager>()
                .Where(manager =>
                    manager != null &&
                    manager.gameObject != null &&
                    manager.gameObject.scene.IsValid())
                .Distinct()
                .ToList();

            var discovered = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var manager in managers)
            {
                if (manager.dialogGraphs == null)
                {
                    continue;
                }

                foreach (var graphModel in manager.dialogGraphs)
                {
                    if (graphModel == null ||
                        graphModel.dialogGraph == null ||
                        string.IsNullOrWhiteSpace(graphModel.dialogID))
                    {
                        continue;
                    }

                    string key = graphModel.dialogID.Trim().ToString();
                    if (!discovered.TryGetValue(key, out var actionIds))
                    {
                        actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        discovered[key] = actionIds;
                    }

                    CollectActionIds(graphModel.dialogGraph, actionIds);
                }
            }

            if (discovered.Count == 0)
            {
                return "No dialogIDs with assigned graph assets were found on DialogManager components in open scenes.";
            }

            Undo.RecordObject(runner, "Sync Dialogue Sets");

            var addedSets = 0;
            var addedBindings = 0;
            foreach (var pair in discovered.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!existingByKey.TryGetValue(pair.Key, out var actionSet))
                {
                    actionSet = new ConversationActionSet
                    {
                        dialogueKey = pair.Key,
                        bindings = new List<ActionBinding>(),
                        handlers = new List<MonoBehaviour>()
                    };

                    runner.dialogueSets.Add(actionSet);
                    existingByKey[pair.Key] = actionSet;
                    addedSets++;
                }

                addedBindings += EnsureBindings(actionSet, pair.Value);
            }

            return $"Synced {discovered.Count} dialogue set(s). Added {addedSets} new set(s) and {addedBindings} missing binding(s).";
        }

        private static void CollectActionIds(DialogGraph graph, ISet<string> destination)
        {
            if (graph == null || destination == null)
            {
                return;
            }

            if (graph.actionNodes != null)
            {
                foreach (var actionNode in graph.actionNodes)
                {
                    if (!string.IsNullOrWhiteSpace(actionNode?.actionId))
                    {
                        destination.Add(actionNode.actionId.Trim());
                    }
                }
            }

            if (graph.availableActions != null)
            {
                foreach (var action in graph.availableActions)
                {
                    if (!string.IsNullOrWhiteSpace(action?.ActionID))
                    {
                        destination.Add(action.ActionID.Trim());
                    }
                }
            }
        }
    }
}
#endif
