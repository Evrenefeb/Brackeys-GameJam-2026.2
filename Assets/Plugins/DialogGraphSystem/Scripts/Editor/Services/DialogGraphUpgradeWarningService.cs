using System;
using System.Collections.Generic;
using DialogSystem.Runtime.Models;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Session-only warning flow for legacy dialog graph schema upgrades.
    /// </summary>
    public static class DialogGraphUpgradeWarningService
    {
        #region ---------------- Constants ----------------

        private const string DialogTitle = "Upgrade Dialogue Graph";
        private const string UpgradeMessage =
            "This dialogue graph was created with an older version of Dialogue Graph System or is missing stable internal identity/port metadata. " +
            "To use newer editor features safely, this graph can be upgraded. The upgrade fills missing graph, node, choice, link, and port identity data. " +
            "Existing valid IDs, dialogue flow, choices, actions, conditions, and runtime playback will not be changed.";

        #endregion

        #region ---------------- Session State ----------------

        private static readonly HashSet<string> LaterDismissedGraphPaths = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> ActivePromptGraphPaths = new HashSet<string>(StringComparer.Ordinal);

        #endregion

        #region ---------------- Public API ----------------

        /// <summary>
        /// Shows a non-destructive upgrade warning when the graph needs schema migration.
        /// </summary>
        public static bool ShowIfNeeded(DialogGraph graph, Action onUpgradeSucceeded = null)
        {
            if (graph == null)
            {
                return false;
            }

            DialogGraphSchemaReport report;
            try
            {
                report = DialogGraphSchemaDetector.Inspect(graph);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Dialogue Graph System could not inspect graph schema for '{graph.name}': {exception.Message}", graph);
                return false;
            }

            if (!report.IsUpgradeNeeded)
            {
                return false;
            }

            var graphPath = ResolveGraphPath(graph);
            if (LaterDismissedGraphPaths.Contains(graphPath) || ActivePromptGraphPaths.Contains(graphPath))
            {
                return false;
            }

            ActivePromptGraphPaths.Add(graphPath);
            try
            {
                return ShowUpgradeDialog(graph, graphPath, onUpgradeSucceeded);
            }
            finally
            {
                ActivePromptGraphPaths.Remove(graphPath);
            }
        }

        #endregion

        #region ---------------- Dialog Flow ----------------

        private static bool ShowUpgradeDialog(DialogGraph graph, string graphPath, Action onUpgradeSucceeded)
        {
            var choice = EditorUtility.DisplayDialogComplex(
                DialogTitle,
                UpgradeMessage,
                "Upgrade With Backup",
                "Later",
                "Upgrade Without Backup");

            switch (choice)
            {
                case 0:
                    UpgradeGraph(graph, createBackup: true, onUpgradeSucceeded);
                    return true;
                case 2:
                    UpgradeGraph(graph, createBackup: false, onUpgradeSucceeded);
                    return true;
                default:
                    LaterDismissedGraphPaths.Add(graphPath);
                    return false;
            }
        }

        private static void UpgradeGraph(DialogGraph graph, bool createBackup, Action onUpgradeSucceeded)
        {
            var result = DialogGraphUpgradeService.UpgradeGraph(graph, createBackup);
            if (result.Success)
            {
                EditorUtility.DisplayDialog(
                    "Dialogue Graph Upgraded",
                    BuildSuccessMessage(result),
                    "OK");

                onUpgradeSucceeded?.Invoke();
                return;
            }

            EditorUtility.DisplayDialog(
                "Dialogue Graph Upgrade Failed",
                BuildFailureMessage(result),
                "OK");
        }

        #endregion

        #region ---------------- Messages ----------------

        private static string BuildSuccessMessage(DialogGraphUpgradeResult result)
        {
            return
                $"Graph: {Fallback(result.GraphName, "(unnamed)")}\n" +
                $"Schema: {result.PreviousSchemaVersion} -> {result.NewSchemaVersion}\n" +
                $"Links: {result.LinkCount}\n" +
                $"Assigned missing link IDs: {result.LinkGuidAssignedCount}\n" +
                $"Fixed duplicate link IDs: {result.DuplicateLinkGuidFixedCount}\n" +
                $"Backup: {FormatOptionalValue(result.BackupPath)}";
        }

        private static string BuildFailureMessage(DialogGraphUpgradeResult result)
        {
            var errorText = result.Errors.Count == 0
                ? "Unknown error."
                : string.Join("\n", result.Errors);

            return
                $"Graph: {Fallback(result.GraphName, "(unnamed)")}\n" +
                $"Path: {FormatOptionalValue(result.GraphPath)}\n\n" +
                errorText;
        }

        private static string ResolveGraphPath(DialogGraph graph)
        {
            var path = AssetDatabase.GetAssetPath(graph);
            return string.IsNullOrWhiteSpace(path)
                ? $"instance:{graph.GetEntityId()}"
                : path;
        }

        private static string FormatOptionalValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        #endregion
    }
}
