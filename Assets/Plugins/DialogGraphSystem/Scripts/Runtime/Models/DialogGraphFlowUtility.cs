using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Models.Nodes;

namespace DialogSystem.Runtime.Models
{
    /// <summary>
    /// Runtime-safe traversal helpers for explicit Start and End graph boundaries.
    /// </summary>
    public static class DialogGraphFlowUtility
    {
        /// <summary>
        /// Legacy-safe alias used by older serialized graphs for the explicit Start boundary.
        /// </summary>
        public const string StartAliasGuid = "Start";

        /// <summary>
        /// Legacy-safe alias used by older serialized graphs for the explicit End boundary.
        /// </summary>
        public const string EndAliasGuid = "End";

        /// <summary>
        /// Returns true when <paramref name="guid"/> identifies the graph's Start boundary.
        /// </summary>
        public static bool IsStartGuid(DialogGraph graph, string guid)
        {
            return MatchesBoundaryGuid(guid, graph?.startGuid, StartAliasGuid);
        }

        /// <summary>
        /// Returns true when <paramref name="guid"/> identifies the graph's End boundary.
        /// </summary>
        public static bool IsEndGuid(DialogGraph graph, string guid)
        {
            return MatchesBoundaryGuid(guid, graph?.endGuid, EndAliasGuid);
        }

        /// <summary>
        /// Resolves the first playable node by following the explicit Start node output.
        /// Returns null when the graph has no Start boundary or Start has no outgoing link.
        /// </summary>
        public static string ResolveEntryGuid(DialogGraph graph)
        {
            if (graph == null)
            {
                return null;
            }

            foreach (var startGuid in EnumerateBoundaryGuids(graph.startGuid, StartAliasGuid))
            {
                var next = GetFirstOutgoingTarget(graph, startGuid);
                if (!string.IsNullOrWhiteSpace(next))
                {
                    return next;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the first outgoing target from <paramref name="fromGuid"/> ordered by port index.
        /// </summary>
        public static string GetFirstOutgoingTarget(DialogGraph graph, string fromGuid)
        {
            if (graph?.links == null || string.IsNullOrWhiteSpace(fromGuid))
            {
                return null;
            }

            return graph.links
                .Where(link => link != null && string.Equals(link.fromGuid, fromGuid, StringComparison.Ordinal))
                .OrderBy(link => link.fromPortIndex)
                .FirstOrDefault()
                ?.toGuid;
        }

        /// <summary>
        /// Returns the target connected to a specific source port, or null when no link exists.
        /// </summary>
        public static string GetNextFromPort(DialogGraph graph, string fromGuid, int portIndex)
        {
            if (graph?.links == null || string.IsNullOrWhiteSpace(fromGuid))
            {
                return null;
            }

            return graph.links
                .FirstOrDefault(link =>
                    link != null &&
                    string.Equals(link.fromGuid, fromGuid, StringComparison.Ordinal) &&
                    link.fromPortIndex == portIndex)
                ?.toGuid;
        }

        /// <summary>
        /// Returns the target connected to a stable source port key, falling back to the legacy index
        /// when the graph or link has not been migrated to port keys yet.
        /// </summary>
        public static string GetNextFromPort(DialogGraph graph, string fromGuid, string portKey, int fallbackPortIndex)
        {
            if (graph?.links == null || string.IsNullOrWhiteSpace(fromGuid))
            {
                return null;
            }

            var hasPortKey = !string.IsNullOrWhiteSpace(portKey);
            return graph.links
                .FirstOrDefault(link =>
                    link != null &&
                    string.Equals(link.fromGuid, fromGuid, StringComparison.Ordinal) &&
                    (hasPortKey && !string.IsNullOrWhiteSpace(link.fromPortKey)
                        ? string.Equals(link.fromPortKey, portKey, StringComparison.Ordinal)
                        : link.fromPortIndex == fallbackPortIndex))
                ?.toGuid;
        }

        /// <summary>
        /// Returns true when <paramref name="guid"/> points to a runtime content node in the graph.
        /// </summary>
        public static bool ContainsRuntimeNode(DialogGraph graph, string guid)
        {
            if (graph == null || string.IsNullOrWhiteSpace(guid))
            {
                return false;
            }

            return ContainsNode(graph.nodes, guid) ||
                   ContainsNode(graph.choiceNodes, guid) ||
                   ContainsNode(graph.actionNodes, guid) ||
                   ContainsNode(graph.conditionNodes, guid) ||
                   ContainsNode(graph.variableMutationNodes, guid) ||
                   ContainsNode(graph.graphJumpNodes, guid) ||
                   ContainsNode(graph.outcomeNodes, guid);
        }

        private static bool ContainsNode<TNode>(IEnumerable<TNode> nodes, string guid)
            where TNode : BaseNode
        {
            return nodes != null &&
                   nodes.Any(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
        }

        private static IEnumerable<string> EnumerateBoundaryGuids(string explicitGuid, string aliasGuid)
        {
            if (!string.IsNullOrWhiteSpace(explicitGuid))
            {
                yield return explicitGuid;

                if (!string.Equals(explicitGuid, aliasGuid, StringComparison.Ordinal))
                {
                    yield return aliasGuid;
                }

                yield break;
            }

            yield return aliasGuid;
        }

        private static bool MatchesBoundaryGuid(string guid, string explicitGuid, string aliasGuid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return false;
            }

            return string.Equals(guid, aliasGuid, StringComparison.Ordinal) ||
                   (!string.IsNullOrWhiteSpace(explicitGuid) &&
                    string.Equals(guid, explicitGuid, StringComparison.Ordinal));
        }
    }
}
