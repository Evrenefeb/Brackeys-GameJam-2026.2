using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Immutable snapshot of a <see cref="DialogGraph"/> asset state captured before an import overwrite.
    /// Used for failure verification — after a failed import, the original state should match.
    /// </summary>
    public readonly struct DialogGraphImportSnapshot
    {
        /// <summary>Project-relative asset path.</summary>
        public string AssetPath { get; }

        /// <summary>Unity main-asset GUID from the .meta file.</summary>
        public string MainAssetGuid { get; }

        /// <summary>Stable graph identity GUID (graphGuid field).</summary>
        public string GraphId { get; }

        /// <summary>Total node count across all node lists plus start/end via EnumerateAllNodeGuids.</summary>
        public int NodeCount { get; }

        /// <summary>Number of GraphLink entries.</summary>
        public int LinkCount { get; }

        /// <summary>Number of sub-assets (objects returned by LoadAllAssetsAtPath minus the main asset).</summary>
        public int SubAssetCount { get; }

        private DialogGraphImportSnapshot(
            string assetPath,
            string mainAssetGuid,
            string graphId,
            int nodeCount,
            int linkCount,
            int subAssetCount)
        {
            AssetPath = assetPath;
            MainAssetGuid = mainAssetGuid;
            GraphId = graphId;
            NodeCount = nodeCount;
            LinkCount = linkCount;
            SubAssetCount = subAssetCount;
        }

        /// <summary>
        /// Captures a snapshot of the graph at the given path. Returns null when the asset does not exist or is not a DialogGraph.
        /// </summary>
        public static DialogGraphImportSnapshot? Capture(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            var graph = AssetDatabase.LoadAssetAtPath<DialogGraph>(assetPath);
            if (graph == null)
                return null;

            var mainAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(mainAssetGuid))
                return null;

            var graphId = graph.GraphGuid ?? string.Empty;
            var nodeCount = CountNodes(graph);
            var linkCount = graph.links?.Count ?? 0;

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var subAssetCount = allAssets?.Count(a => a != graph && a != null) ?? 0;

            return new DialogGraphImportSnapshot(
                assetPath,
                mainAssetGuid,
                graphId,
                nodeCount,
                linkCount,
                subAssetCount);
        }

        /// <summary>
        /// Compares this snapshot with another. Returns true when the key structural properties match
        /// (asset path, main asset GUID, graph ID, node count, link count, sub-asset count).
        /// Outputs a diff string describing the first mismatch on failure.
        /// </summary>
        public bool Matches(DialogGraphImportSnapshot? other, out string diff)
        {
            diff = string.Empty;

            if (other == null)
            {
                diff = "Other snapshot is null.";
                return false;
            }

            var o = other.Value;

            if (!string.Equals(AssetPath, o.AssetPath, StringComparison.Ordinal))
            {
                diff = $"AssetPath: expected '{AssetPath}', got '{o.AssetPath}'.";
                return false;
            }

            if (!string.Equals(MainAssetGuid, o.MainAssetGuid, StringComparison.Ordinal))
            {
                diff = $"MainAssetGuid: expected '{MainAssetGuid}', got '{o.MainAssetGuid}'.";
                return false;
            }

            if (!string.Equals(GraphId, o.GraphId, StringComparison.Ordinal))
            {
                diff = $"GraphId: expected '{GraphId}', got '{o.GraphId}'.";
                return false;
            }

            if (NodeCount != o.NodeCount)
            {
                diff = $"NodeCount: expected {NodeCount}, got {o.NodeCount}.";
                return false;
            }

            if (LinkCount != o.LinkCount)
            {
                diff = $"LinkCount: expected {LinkCount}, got {o.LinkCount}.";
                return false;
            }

            if (SubAssetCount != o.SubAssetCount)
            {
                diff = $"SubAssetCount: expected {SubAssetCount}, got {o.SubAssetCount}.";
                return false;
            }

            return true;
        }

        #region ---------------- Node Counting ----------------

        private static int CountNodes(DialogGraph graph)
        {
            var count = 0;
            count += graph.nodes?.Count ?? 0;
            count += graph.choiceNodes?.Count ?? 0;
            count += graph.actionNodes?.Count ?? 0;
            count += graph.conditionNodes?.Count ?? 0;
            count += graph.variableMutationNodes?.Count ?? 0;
            count += graph.graphJumpNodes?.Count ?? 0;
            count += graph.outcomeNodes?.Count ?? 0;

            if (!string.IsNullOrWhiteSpace(graph.startGuid))
                count++;
            if (!string.IsNullOrWhiteSpace(graph.endGuid))
                count++;

            return count;
        }

        #endregion
    }
}
