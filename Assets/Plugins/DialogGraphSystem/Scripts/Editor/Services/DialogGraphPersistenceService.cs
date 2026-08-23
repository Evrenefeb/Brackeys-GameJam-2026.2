using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Editor persistence boundary for dialog graph assets.
    /// </summary>
    public interface IDialogGraphPersistenceService
    {
        /// <summary>
        /// Loads a graph asset using the shared graph path resolution rules.
        /// </summary>
        DialogGraph LoadGraphAsset(string graphName);

        /// <summary>
        /// Loads the graph asset for the given name, creating it in the primary graph folder when missing.
        /// </summary>
        DialogGraph RequireGraphAsset(string graphName);

        /// <summary>
        /// Loads the graph asset for the given name, creating it in the primary graph folder when missing.
        /// </summary>
        DialogGraph RequireGraphAsset(string graphName, out bool created);

        /// <summary>
        /// Ensures a graph asset exists in a specific project asset folder.
        /// </summary>
        DialogGraph EnsureGraphAssetInFolder(string graphName, string assetFolderPath);

        /// <summary>
        /// Resolves the save target and creates a graph asset when no existing asset matches the graph name.
        /// </summary>
        DialogGraph GetOrCreateGraphAssetForSave(string graphName, out string assetPath);

        /// <summary>
        /// Applies migration/default initialization required before saving and reports whether serialized data changed.
        /// </summary>
        DialogGraphSavePreparationResult PrepareForSave(DialogGraph graph);

        /// <summary>
        /// Marks the graph dirty and saves assets only when the caller reports serialized changes.
        /// </summary>
        void SaveAssetIfChanged(DialogGraph graph, bool changed);

        /// <summary>
        /// Marks an editor asset dirty and saves project assets immediately.
        /// </summary>
        void MarkDirtyAndSave(UnityEngine.Object asset);
    }

    /// <summary>
    /// Centralizes editor graph asset persistence decisions while DialogGraphView
    /// remains responsible for visual graph rebuild and view-to-data collection.
    /// </summary>
    public sealed class DialogGraphPersistenceService : IDialogGraphPersistenceService
    {
        #region ---------------- Asset Resolution ----------------

        /// <summary>
        /// Loads a graph asset using the shared graph path resolution rules.
        /// </summary>
        public DialogGraph LoadGraphAsset(string graphName)
        {
            var graph = DialogGraphAssetPaths.LoadGraphAsset(graphName);
            WarnIfLoadedGraphHasIntegrityIssues(graph);
            return graph;
        }

        /// <summary>
        /// Loads the graph asset for the given name, creating it in the primary graph folder when missing.
        /// </summary>
        public DialogGraph RequireGraphAsset(string graphName)
        {
            return RequireGraphAsset(graphName, out _);
        }

        /// <summary>
        /// Loads the graph asset for the given name, creating it in the primary graph folder when missing.
        /// </summary>
        public DialogGraph RequireGraphAsset(string graphName, out bool created)
        {
            var path = DialogGraphAssetPaths.ResolveGraphAssetPath(graphName);
            var asset = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (asset != null)
            {
                created = false;
                WarnIfLoadedGraphHasIntegrityIssues(asset);
                return asset;
            }

            DialogGraphAssetPaths.EnsurePrimaryGraphFolderExists();
            path = DialogGraphAssetPaths.GetPrimaryGraphAssetPath(graphName);
            asset = CreateGraphAsset(path);
            AssetDatabase.SaveAssets();
            created = true;

            return asset;
        }

        /// <summary>
        /// Ensures a graph asset exists in a specific project asset folder.
        /// </summary>
        public DialogGraph EnsureGraphAssetInFolder(string graphName, string assetFolderPath)
        {
            DialogGraphAssetPaths.EnsureFolderExists(assetFolderPath);

            var folder = assetFolderPath.Replace('\\', '/').TrimEnd('/');
            var path = $"{folder}/{graphName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            return asset != null ? asset : CreateGraphAsset(path);
        }

        /// <summary>
        /// Resolves the save target and creates a graph asset when no existing asset matches the graph name.
        /// </summary>
        public DialogGraph GetOrCreateGraphAssetForSave(string graphName, out string assetPath)
        {
            assetPath = DialogGraphAssetPaths.ResolveGraphAssetPath(graphName);
            var asset = AssetDatabase.LoadAssetAtPath<DialogGraph>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            DialogGraphAssetPaths.EnsurePrimaryGraphFolderExists();
            assetPath = DialogGraphAssetPaths.GetPrimaryGraphAssetPath(graphName);
            return CreateGraphAsset(assetPath);
        }

        #endregion

        #region ---------------- Save Preparation ----------------

        /// <summary>
        /// Applies migration/default initialization required before saving and reports whether serialized data changed.
        /// </summary>
        public DialogGraphSavePreparationResult PrepareForSave(DialogGraph graph)
        {
            if (graph == null)
            {
                return new DialogGraphSavePreparationResult(false, new List<string> { "DialogGraph asset is null." });
            }

            var migrationResult = DialogGraphUpgradeService.MigrateToCurrent(graph);
            var changed = migrationResult.Changed;

            changed |= EnsureNodeLists(graph);

            return new DialogGraphSavePreparationResult(changed, migrationResult.Errors);
        }

        /// <summary>
        /// Marks the graph dirty and saves assets only when the caller reports serialized changes.
        /// </summary>
        public void SaveAssetIfChanged(DialogGraph graph, bool changed)
        {
            if (graph == null || !changed)
            {
                return;
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Marks an editor asset dirty and saves project assets immediately.
        /// </summary>
        public void MarkDirtyAndSave(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Replaces the content of an existing graph asset with the supplied DTO,
        /// rebuilding all node sub-assets from scratch.
        /// </summary>
        public void ReplaceGraphAssetContents(
            DialogGraph graph,
            string assetPath,
            DialogGraphExport dto,
            bool assignNewGraphGuid,
            string graphNameOverride = null)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Asset path must be non-empty.", nameof(assetPath));
            }

            RemoveSubAssets(assetPath, graph);
            ClearGraphData(graph);
            DialogJsonImportBridge.BuildFromDto(graph, dto);
            DialogGraphUpgradeService.MigrateToCurrent(graph);

            if (assignNewGraphGuid)
            {
                graph.SetGraphGuidForMigration(Guid.NewGuid().ToString("N"));
            }

            if (!string.IsNullOrWhiteSpace(graphNameOverride))
            {
                graph.name = graphNameOverride.Trim();
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
        }

        #endregion

        #region ---------------- Helpers ----------------

        private static DialogGraph CreateGraphAsset(string assetPath)
        {
            var asset = ScriptableObject.CreateInstance<DialogGraph>();
            InitializeNewGraphIdentity(asset);
            AssetDatabase.CreateAsset(asset, assetPath);
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
            return asset;
        }

        private static void RemoveSubAssets(string assetPath, DialogGraph graph)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var subAsset in allAssets)
            {
                if (subAsset != null && subAsset != graph)
                {
                    AssetDatabase.RemoveObjectFromAsset(subAsset);
                }
            }
        }

        private static void ClearGraphData(DialogGraph graph)
        {
            graph.nodes?.Clear();
            graph.choiceNodes?.Clear();
            graph.actionNodes?.Clear();
            graph.conditionNodes?.Clear();
            graph.variableMutationNodes?.Clear();
            graph.graphJumpNodes?.Clear();
            graph.outcomeNodes?.Clear();
            graph.links?.Clear();
            graph.ClearAllGroupLayouts();
        }

        private static void WarnIfLoadedGraphHasIntegrityIssues(DialogGraph graph)
        {
            if (graph == null)
            {
                return;
            }

            var issueCount = 0;
            issueCount += CountMissingNodeGuids(graph);
            issueCount += CountDuplicateNodeGuids(graph);
            issueCount += CountUnknownLinkReferences(graph);

            if (issueCount <= 0)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(graph);
            Debug.LogWarning(
                $"Dialog graph '{graph.name}' loaded with {issueCount} integrity issue(s). " +
                $"Open {TextResources.MENU_DIALOGUE_GRAPH_SYSTEM_VALIDATION_PANEL_DISPLAY} for details. Asset: {path}");
        }

        private static int CountMissingNodeGuids(DialogGraph graph)
        {
            var count = 0;
            count += CountMissingNodeGuids(graph.nodes);
            count += CountMissingNodeGuids(graph.choiceNodes);
            count += CountMissingNodeGuids(graph.actionNodes);
            count += CountMissingNodeGuids(graph.conditionNodes);
            count += CountMissingNodeGuids(graph.variableMutationNodes);
            count += CountMissingNodeGuids(graph.graphJumpNodes);
            return count;
        }

        private static int CountMissingNodeGuids<TNode>(IEnumerable<TNode> nodes)
            where TNode : BaseNode
        {
            return nodes?.Count(node => node == null || string.IsNullOrWhiteSpace(node.GetGuid())) ?? 0;
        }

        private static int CountDuplicateNodeGuids(DialogGraph graph)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = 0;
            foreach (var guid in graph.EnumerateAllNodeGuids())
            {
                if (!seen.Add(guid))
                {
                    duplicates++;
                }
            }

            return duplicates;
        }

        private static int CountUnknownLinkReferences(DialogGraph graph)
        {
            if (graph.links == null || graph.links.Count == 0)
            {
                return 0;
            }

            var knownGuids = new HashSet<string>(graph.EnumerateAllNodeGuids(), StringComparer.Ordinal);
            knownGuids.Add("Start");
            knownGuids.Add("End");

            var count = 0;
            foreach (var link in graph.links)
            {
                if (link == null)
                {
                    count++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(link.fromGuid) || !knownGuids.Contains(link.fromGuid))
                {
                    count++;
                }

                if (string.IsNullOrWhiteSpace(link.toGuid) || !knownGuids.Contains(link.toGuid))
                {
                    count++;
                }
            }

            return count;
        }

        private static void InitializeNewGraphIdentity(DialogGraph graph)
        {
            if (graph == null)
            {
                return;
            }

            graph.AssignGraphGuidIfMissing(Guid.NewGuid().ToString("N"));
            graph.MarkSchemaCurrentForMigration();
        }

        private static bool EnsureNodeLists(DialogGraph graph)
        {
            var changed = false;

            if (graph.nodes == null)
            {
                graph.nodes = new List<DialogNode>();
                changed = true;
            }

            if (graph.choiceNodes == null)
            {
                graph.choiceNodes = new List<ChoiceNode>();
                changed = true;
            }

            if (graph.actionNodes == null)
            {
                graph.actionNodes = new List<ActionNode>();
                changed = true;
            }

            if (graph.conditionNodes == null)
            {
                graph.conditionNodes = new List<ConditionNode>();
                changed = true;
            }

            if (graph.variableMutationNodes == null)
            {
                graph.variableMutationNodes = new List<VariableMutationNode>();
                changed = true;
            }

            if (graph.graphJumpNodes == null)
            {
                graph.graphJumpNodes = new List<GraphJumpNode>();
                changed = true;
            }

            if (graph.links == null)
            {
                graph.links = new List<GraphLink>();
                changed = true;
            }

            return changed;
        }

        #endregion
    }

    public readonly struct DialogGraphSavePreparationResult
    {
        /// <summary>
        /// Describes graph save preparation changes and migration/defaulting errors.
        /// </summary>
        public DialogGraphSavePreparationResult(bool changed, IReadOnlyList<string> errors)
        {
            Changed = changed;
            Errors = errors ?? Array.Empty<string>();
        }

        public bool Changed { get; }
        public IReadOnlyList<string> Errors { get; }
    }
}
