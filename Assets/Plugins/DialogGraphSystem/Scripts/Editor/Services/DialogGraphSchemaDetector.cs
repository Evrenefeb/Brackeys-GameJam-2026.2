using System;
using System.Collections.Generic;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Read-only detector for serialized <see cref="DialogGraph"/> schema state.
    /// </summary>
    public static class DialogGraphSchemaDetector
    {
        #region ---------------- Public API ----------------

        /// <summary>
        /// Inspects graph schema and link identity health without mutating the graph.
        /// </summary>
        public static DialogGraphSchemaReport Inspect(DialogGraph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var graphSchemaVersion = graph.GraphSchemaVersion;
            var currentSchemaVersion = DialogGraph.CurrentSchemaVersion;
            var isLegacy = graphSchemaVersion < currentSchemaVersion;

            var linkCount = graph.links?.Count ?? 0;
            var linksMissingLinkGuidCount = 0;
            var duplicateLinkGuidCount = 0;
            var missingGraphGuidCount = graph.HasGraphGuid ? 0 : 1;
            var missingNodeGuidCount = CountMissingNodeGuids(graph);
            var duplicateNodeGuidCount = CountDuplicateNodeGuids(graph);
            var missingChoiceIdCount = CountMissingChoiceIds(graph);
            var duplicateChoiceIdCount = CountDuplicateChoiceIds(graph);
            var missingFromPortKeyCount = 0;
            var missingToPortKeyCount = 0;
            var nullLinkCount = 0;
            var emptyLinkCount = 0;
            var seenLinkGuids = new HashSet<string>(StringComparer.Ordinal);

            if (graph.links != null)
            {
                foreach (var link in graph.links)
                {
                    if (link == null)
                    {
                        nullLinkCount++;
                        continue;
                    }

                    var linkGuid = link.LinkGuid;
                    if (string.IsNullOrWhiteSpace(linkGuid))
                    {
                        linksMissingLinkGuidCount++;
                    }
                    else if (!seenLinkGuids.Add(linkGuid))
                    {
                        duplicateLinkGuidCount++;
                    }

                    if (IsEmptyLink(link))
                    {
                        emptyLinkCount++;
                    }

                    if (string.IsNullOrWhiteSpace(link.fromPortKey))
                    {
                        missingFromPortKeyCount++;
                    }

                    if (string.IsNullOrWhiteSpace(link.toPortKey))
                    {
                        missingToPortKeyCount++;
                    }
                }
            }

            var isUpgradeNeeded =
                isLegacy ||
                missingGraphGuidCount > 0 ||
                missingNodeGuidCount > 0 ||
                duplicateNodeGuidCount > 0 ||
                missingChoiceIdCount > 0 ||
                duplicateChoiceIdCount > 0 ||
                linksMissingLinkGuidCount > 0 ||
                duplicateLinkGuidCount > 0 ||
                missingFromPortKeyCount > 0 ||
                missingToPortKeyCount > 0 ||
                nullLinkCount > 0 ||
                emptyLinkCount > 0;

            return new DialogGraphSchemaReport(
                graphSchemaVersion,
                currentSchemaVersion,
                isLegacy,
                linkCount,
                linksMissingLinkGuidCount,
                duplicateLinkGuidCount,
                missingGraphGuidCount,
                missingNodeGuidCount,
                duplicateNodeGuidCount,
                missingChoiceIdCount,
                duplicateChoiceIdCount,
                missingFromPortKeyCount,
                missingToPortKeyCount,
                nullLinkCount,
                emptyLinkCount,
                isUpgradeNeeded);
        }

        #endregion

        #region ---------------- Link Inspection ----------------

        private static bool IsEmptyLink(GraphLink link)
        {
            return string.IsNullOrWhiteSpace(link.fromGuid) &&
                   string.IsNullOrWhiteSpace(link.toGuid) &&
                   string.IsNullOrWhiteSpace(link.LinkGuid) &&
                   string.IsNullOrWhiteSpace(link.fromPortKey) &&
                   string.IsNullOrWhiteSpace(link.toPortKey) &&
                   link.fromPortIndex == 0;
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

            if (string.IsNullOrWhiteSpace(graph.startGuid))
            {
                count++;
            }

            if (string.IsNullOrWhiteSpace(graph.endGuid))
            {
                count++;
            }

            return count;
        }

        private static int CountDuplicateNodeGuids(DialogGraph graph)
        {
            var duplicateCount = 0;
            var seenGuids = new HashSet<string>(StringComparer.Ordinal);
            CountDuplicateNodeGuids(graph.nodes, seenGuids, ref duplicateCount);
            CountDuplicateNodeGuids(graph.choiceNodes, seenGuids, ref duplicateCount);
            CountDuplicateNodeGuids(graph.actionNodes, seenGuids, ref duplicateCount);
            CountDuplicateNodeGuids(graph.conditionNodes, seenGuids, ref duplicateCount);
            CountDuplicateNodeGuids(graph.variableMutationNodes, seenGuids, ref duplicateCount);
            CountDuplicateNodeGuids(graph.graphJumpNodes, seenGuids, ref duplicateCount);

            if (!string.IsNullOrWhiteSpace(graph.startGuid) && !seenGuids.Add(graph.startGuid))
            {
                duplicateCount++;
            }

            if (!string.IsNullOrWhiteSpace(graph.endGuid) && !seenGuids.Add(graph.endGuid))
            {
                duplicateCount++;
            }

            return duplicateCount;
        }

        private static void CountDuplicateNodeGuids<TNode>(
            IEnumerable<TNode> nodes,
            HashSet<string> seenGuids,
            ref int duplicateCount)
            where TNode : BaseNode
        {
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                var guid = node?.GetGuid();
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                if (!seenGuids.Add(guid))
                {
                    duplicateCount++;
                }
            }
        }

        private static int CountMissingNodeGuids<TNode>(IEnumerable<TNode> nodes)
            where TNode : BaseNode
        {
            var count = 0;
            if (nodes == null)
            {
                return count;
            }

            foreach (var node in nodes)
            {
                if (node != null && string.IsNullOrWhiteSpace(node.GetGuid()))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountMissingChoiceIds(DialogGraph graph)
        {
            var count = 0;
            if (graph.choiceNodes == null)
            {
                return count;
            }

            foreach (var choiceNode in graph.choiceNodes)
            {
                if (choiceNode?.choices == null)
                {
                    continue;
                }

                foreach (var choice in choiceNode.choices)
                {
                    if (choice != null && !choice.HasChoiceId)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int CountDuplicateChoiceIds(DialogGraph graph)
        {
            var count = 0;
            if (graph.choiceNodes == null)
            {
                return count;
            }

            var seenChoiceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choiceNode in graph.choiceNodes)
            {
                if (choiceNode?.choices == null)
                {
                    continue;
                }

                foreach (var choice in choiceNode.choices)
                {
                    if (choice == null || string.IsNullOrWhiteSpace(choice.choiceId))
                    {
                        continue;
                    }

                    if (!seenChoiceIds.Add(choice.choiceId))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        #endregion
    }
}
