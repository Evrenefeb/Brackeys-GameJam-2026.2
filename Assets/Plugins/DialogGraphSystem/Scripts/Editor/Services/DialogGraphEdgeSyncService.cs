using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.Experimental.GraphView;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Synchronizes visual GraphView edges with serialized <see cref="GraphLink"/> records.
    /// </summary>
    public sealed class DialogGraphEdgeSyncService
    {
        #region ---------------- Public API ----------------

        /// <summary>
        /// Reconciles serialized links against the currently visible edge set without changing
        /// graph schema or node data.
        /// </summary>
        public bool ReconcileSerializedLinks(
            DialogGraph asset,
            IEnumerable<Edge> visualEdges,
            IEnumerable<Node> visibleNodes)
        {
            if (asset?.links == null)
            {
                return false;
            }

            var visualRecords = CollectVisualEdgeRecords(
                asset,
                visualEdges,
                out var uncertainSourcePorts,
                out var hasInvalidVisualEdge);
            var visualRecordSet = new HashSet<GraphLinkIdentity>(visualRecords);
            var visibleNodeGuids = CollectVisibleNodeGuids(visibleNodes);

            var changed = RemoveDuplicateSerializedLinks(asset);

            if (!hasInvalidVisualEdge)
            {
                for (var i = asset.links.Count - 1; i >= 0; i--)
                {
                    var link = asset.links[i];
                    if (!TryCreateLinkIdentity(link, out var identity))
                    {
                        continue;
                    }

                    if (visualRecordSet.Contains(identity))
                    {
                        continue;
                    }

                    if (!IsSerializedLinkClearlyAbsent(identity, visibleNodeGuids, uncertainSourcePorts))
                    {
                        continue;
                    }

                    asset.links.RemoveAt(i);
                    changed = true;
                }
            }

            foreach (var visualRecord in visualRecords)
            {
                if (uncertainSourcePorts.Contains(visualRecord.SourcePort))
                {
                    continue;
                }

                if (DialogGraphLinkMutationService.FindExactLink(
                        asset,
                        visualRecord.FromGuid,
                        visualRecord.ToGuid,
                        visualRecord.FromPortIndex,
                        visualRecord.FromPortKey) != null)
                {
                    continue;
                }

                if (hasInvalidVisualEdge && HasAnyLinkFromSourcePort(asset, visualRecord.SourcePort))
                {
                    continue;
                }

                var addedLink = DialogGraphLinkMutationService.AddOrReplaceLink(
                    asset,
                    visualRecord.FromGuid,
                    visualRecord.ToGuid,
                    visualRecord.FromPortIndex,
                    visualRecord.FromPortKey,
                    DialogGraphPortKeys.Default);
                changed |= addedLink != null;
            }

            return changed;
        }

        /// <summary>
        /// Rebuilds the legacy Choice.nextNodeGUID mirror from serialized links.
        /// </summary>
        public bool SyncChoiceNextNodeGuids(DialogGraph asset)
        {
            if (asset?.choiceNodes == null)
            {
                return false;
            }

            var changed = false;
            foreach (var choiceNode in asset.choiceNodes)
            {
                if (choiceNode?.choices == null)
                {
                    continue;
                }

                var choiceNodeChanged = false;
                var choiceNodeGuid = choiceNode.GetGuid();
                for (var i = 0; i < choiceNode.choices.Count; i++)
                {
                    var choice = choiceNode.choices[i];
                    if (choice == null)
                    {
                        continue;
                    }

                    string nextNodeGuid = null;
                    if (asset.links != null)
                    {
                        var choicePortKey = choice.PortKey;
                        foreach (var link in asset.links)
                        {
                            if (link != null &&
                                string.Equals(link.fromGuid, choiceNodeGuid, StringComparison.Ordinal) &&
                                LinkMatchesChoicePort(link, i, choicePortKey))
                            {
                                nextNodeGuid = link.toGuid;
                            }
                        }
                    }

                    if (string.Equals(choice.nextNodeGUID, nextNodeGuid, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    choice.nextNodeGUID = nextNodeGuid;
                    choiceNodeChanged = true;
                }

                if (choiceNodeChanged)
                {
                    EditorUtility.SetDirty(choiceNode);
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Creates a visual edge for a serialized link using an existing node lookup.
        /// Returns <c>null</c> when either endpoint or port cannot be resolved.
        /// </summary>
        public DialogGraphEdge CreateVisualEdgeFromLink(
            DialogGraph asset,
            IReadOnlyDictionary<string, Node> viewLookup,
            GraphLink link,
            DialogGraphView graphView = null)
        {
            if (asset == null || viewLookup == null || link == null)
            {
                return null;
            }

            if (!viewLookup.TryGetValue(link.fromGuid, out var fromView))
            {
                return null;
            }

            if (!viewLookup.TryGetValue(link.toGuid, out var toView))
            {
                return null;
            }

            var outPort = ResolveOutputPort(asset, fromView, link);
            var inPort = ResolveInputPort(toView);

            if (outPort == null || inPort == null)
            {
                return null;
            }

            var edge = DialogGraphEdgeFactory.ConnectFromLink(outPort, inPort, link);
            if (edge != null)
            {
                edge.Initialize(asset, graphView);
            }

            return edge;
        }

        #endregion

        #region ---------------- Visual Records ----------------

        private static List<GraphLinkIdentity> CollectVisualEdgeRecords(
            DialogGraph asset,
            IEnumerable<Edge> visualEdges,
            out HashSet<SourcePortIdentity> uncertainSourcePorts,
            out bool hasInvalidVisualEdge)
        {
            var records = new List<GraphLinkIdentity>();
            var exactRecords = new HashSet<GraphLinkIdentity>();
            var sourceTargets = new Dictionary<SourcePortIdentity, string>();
            uncertainSourcePorts = new HashSet<SourcePortIdentity>();
            hasInvalidVisualEdge = false;

            if (visualEdges == null)
            {
                return records;
            }

            foreach (var edge in visualEdges)
            {
                var fromGuid = ExtractGuidFromView(edge?.output?.node as Node);
                var toGuid = ExtractGuidFromView(edge?.input?.node as Node);

                if (string.IsNullOrWhiteSpace(fromGuid) || string.IsNullOrWhiteSpace(toGuid))
                {
                    hasInvalidVisualEdge = true;
                    continue;
                }

                var fromPortIndex = GetOutputPortIndex(edge.output);
                var fromPortKey = edge is DialogGraphEdge graphEdge && !string.IsNullOrWhiteSpace(graphEdge.fromPortKey)
                    ? graphEdge.fromPortKey
                    : GetOutputPortKey(asset, edge.output, assignMissingChoiceId: false);
                var record = new GraphLinkIdentity(fromGuid, toGuid, fromPortIndex, fromPortKey);
                var sourcePort = record.SourcePort;

                if (sourceTargets.TryGetValue(sourcePort, out var previousTarget) &&
                    !string.Equals(previousTarget, toGuid, StringComparison.Ordinal))
                {
                    uncertainSourcePorts.Add(sourcePort);
                }
                else
                {
                    sourceTargets[sourcePort] = toGuid;
                }

                if (exactRecords.Add(record))
                {
                    records.Add(record);
                }
            }

            return records;
        }

        private static HashSet<string> CollectVisibleNodeGuids(IEnumerable<Node> visibleNodes)
        {
            var visibleNodeGuids = new HashSet<string>(StringComparer.Ordinal);
            if (visibleNodes == null)
            {
                return visibleNodeGuids;
            }

            foreach (var node in visibleNodes)
            {
                var guid = ExtractGuidFromView(node);
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    visibleNodeGuids.Add(guid);
                }
            }

            return visibleNodeGuids;
        }

        #endregion

        #region ---------------- Link Reconciliation ----------------

        private static bool RemoveDuplicateSerializedLinks(DialogGraph asset)
        {
            var changed = false;
            var preservedByIdentity = new Dictionary<GraphLinkIdentity, GraphLink>();
            var linksToRemove = new HashSet<GraphLink>();

            foreach (var link in asset.links)
            {
                if (!TryCreateLinkIdentity(link, out var identity))
                {
                    continue;
                }

                if (!preservedByIdentity.TryGetValue(identity, out var preservedLink))
                {
                    preservedByIdentity[identity] = link;
                    continue;
                }

                var preferredLink = SelectPreferredDuplicateLink(preservedLink, link);
                var duplicateLink = ReferenceEquals(preferredLink, preservedLink)
                    ? link
                    : preservedLink;

                preservedByIdentity[identity] = preferredLink;
                linksToRemove.Add(duplicateLink);
            }

            if (linksToRemove.Count > 0)
            {
                asset.links.RemoveAll(link => link != null && linksToRemove.Contains(link));
                changed = true;
            }

            return changed;
        }

        private static GraphLink SelectPreferredDuplicateLink(GraphLink current, GraphLink candidate)
        {
            if (candidate != null && candidate.HasLinkGuid && (current == null || !current.HasLinkGuid))
            {
                return candidate;
            }

            return current;
        }

        private static bool IsSerializedLinkClearlyAbsent(
            GraphLinkIdentity identity,
            HashSet<string> visibleNodeGuids,
            HashSet<SourcePortIdentity> uncertainSourcePorts)
        {
            return !uncertainSourcePorts.Contains(identity.SourcePort) &&
                   visibleNodeGuids.Contains(identity.FromGuid) &&
                   visibleNodeGuids.Contains(identity.ToGuid);
        }

        private static bool HasAnyLinkFromSourcePort(DialogGraph asset, SourcePortIdentity sourcePort)
        {
            return asset?.links != null &&
                   asset.links.Any(link =>
                       link != null &&
                       string.Equals(link.fromGuid, sourcePort.FromGuid, StringComparison.Ordinal) &&
                       SourcePortMatches(link, sourcePort));
        }

        private static bool SourcePortMatches(GraphLink link, SourcePortIdentity sourcePort)
        {
            if (link == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(link.fromPortKey) &&
                !string.IsNullOrWhiteSpace(sourcePort.FromPortKey))
            {
                return string.Equals(link.fromPortKey, sourcePort.FromPortKey, StringComparison.Ordinal);
            }

            return link.fromPortIndex == sourcePort.FromPortIndex;
        }

        private static bool TryCreateLinkIdentity(GraphLink link, out GraphLinkIdentity identity)
        {
            if (link == null ||
                string.IsNullOrWhiteSpace(link.fromGuid) ||
                string.IsNullOrWhiteSpace(link.toGuid))
            {
                identity = default;
                return false;
            }

            identity = new GraphLinkIdentity(link.fromGuid, link.toGuid, link.fromPortIndex, link.fromPortKey);
            return true;
        }

        private static bool LinkMatchesChoicePort(GraphLink link, int choiceIndex, string choicePortKey)
        {
            if (link == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(choicePortKey) &&
                !string.IsNullOrWhiteSpace(link.fromPortKey))
            {
                return string.Equals(link.fromPortKey, choicePortKey, StringComparison.Ordinal);
            }

            return link.fromPortIndex == choiceIndex;
        }

        #endregion

        #region ---------------- Port Resolution ----------------

        private static Port ResolveOutputPort(DialogGraph asset, Node fromView, GraphLink link)
        {
            if (fromView == null || link == null)
            {
                return null;
            }

            if (fromView is ChoiceNodeView choiceView)
            {
                var choiceIndex = ResolveChoicePortIndex(asset, link, choiceView);
                return choiceIndex >= 0 && choiceIndex < choiceView.outputPorts.Count
                    ? choiceView.outputPorts[choiceIndex]
                    : null;
            }

            if (fromView is ConditionNodeView conditionView)
            {
                if (string.Equals(link.fromPortKey, DialogGraphPortKeys.False, StringComparison.Ordinal))
                {
                    return conditionView.falseOutputPort;
                }

                if (string.Equals(link.fromPortKey, DialogGraphPortKeys.True, StringComparison.Ordinal))
                {
                    return conditionView.trueOutputPort;
                }

                return conditionView.GetOutputPort(link.fromPortIndex);
            }

            if (fromView is DialogNodeView dialogView) return dialogView.outputPort;
            if (fromView is StartNodeView startView) return startView.outputPort;
            if (fromView is ActionNodeView actionView) return actionView.outputPort;
            if (fromView is VariableMutationNodeView variableView) return variableView.outputPort;
            if (fromView is GraphJumpNodeView graphJumpView) return graphJumpView.outputPort;
            if (fromView is OutcomeNodeView outcomeView) return outcomeView.outputPort;

            return null;
        }

        private static Port ResolveInputPort(Node toView)
        {
            if (toView is DialogNodeView dialogView) return dialogView.inputPort;
            if (toView is ChoiceNodeView choiceView) return choiceView.inputPort;
            if (toView is EndNodeView endView) return endView.inputPort;
            if (toView is ActionNodeView actionView) return actionView.inputPort;
            if (toView is VariableMutationNodeView variableView) return variableView.inputPort;
            if (toView is ConditionNodeView conditionView) return conditionView.inputPort;
            if (toView is GraphJumpNodeView graphJumpView) return graphJumpView.inputPort;
            if (toView is OutcomeNodeView outcomeView) return outcomeView.inputPort;

            return null;
        }

        private static int ResolveChoicePortIndex(DialogGraph asset, GraphLink link, ChoiceNodeView choiceView)
        {
            if (!string.IsNullOrWhiteSpace(link.fromPortKey) &&
                DialogGraphPortKeys.TryGetChoiceId(link.fromPortKey, out var choiceId))
            {
                var choiceNode = asset?.choiceNodes?.FirstOrDefault(node =>
                    node != null &&
                    string.Equals(node.GetGuid(), link.fromGuid, StringComparison.Ordinal));

                if (choiceNode?.choices != null)
                {
                    for (var i = 0; i < choiceNode.choices.Count; i++)
                    {
                        var choice = choiceNode.choices[i];
                        if (choice != null &&
                            string.Equals(choice.choiceId, choiceId, StringComparison.Ordinal))
                        {
                            return i;
                        }
                    }
                }
            }

            return link.fromPortIndex >= 0 && link.fromPortIndex < choiceView.outputPorts.Count
                ? link.fromPortIndex
                : -1;
        }

        private static string ExtractGuidFromView(Node nodeView)
        {
            if (nodeView is EndNodeView ev) return ev.GUID;
            if (nodeView is StartNodeView sv) return sv.GUID;
            if (nodeView is DialogNodeView dv) return dv.GUID;
            if (nodeView is ChoiceNodeView cv) return cv.GUID;
            if (nodeView is ActionNodeView av) return av.GUID;
            if (nodeView is ConditionNodeView cnv) return cnv.GUID;
            if (nodeView is VariableMutationNodeView vmv) return vmv.GUID;
            if (nodeView is GraphJumpNodeView gjv) return gjv.GUID;
            if (nodeView is OutcomeNodeView onv) return onv.GUID;
            return string.Empty;
        }

        private static int GetOutputPortIndex(Port output)
        {
            if (output?.node is DialogNodeView) return 0;
            if (output?.node is ChoiceNodeView chv) return chv.GetPortIndex(output);
            if (output?.node is StartNodeView) return 0;
            if (output?.node is ActionNodeView) return 0;
            if (output?.node is VariableMutationNodeView) return 0;
            if (output?.node is GraphJumpNodeView) return 0;
            if (output?.node is OutcomeNodeView) return 0;
            if (output?.node is ConditionNodeView cnv) return cnv.GetPortIndex(output);
            return 0;
        }

        private static string GetOutputPortKey(DialogGraph asset, Port output, bool assignMissingChoiceId)
        {
            if (output?.node is ChoiceNodeView choiceView)
            {
                var choiceIndex = choiceView.GetPortIndex(output);
                if (choiceIndex < 0)
                {
                    return string.Empty;
                }

                var choiceNode = asset?.choiceNodes?.FirstOrDefault(node =>
                    node != null &&
                    string.Equals(node.GetGuid(), choiceView.GUID, StringComparison.Ordinal));

                if (choiceNode?.choices != null && choiceIndex < choiceNode.choices.Count)
                {
                    var choice = choiceNode.choices[choiceIndex];
                    if (choice != null)
                    {
                        if (assignMissingChoiceId && !choice.HasChoiceId)
                        {
                            choice.AssignChoiceIdIfMissing(Choice.CreateChoiceId());
                            EditorUtility.SetDirty(choiceNode);
                        }

                        output.userData = choice.choiceId ?? string.Empty;
                        return choice.PortKey;
                    }
                }

                return output.userData is string choiceId && !string.IsNullOrWhiteSpace(choiceId)
                    ? DialogGraphPortKeys.ForChoiceId(choiceId)
                    : string.Empty;
            }

            if (output?.node is ConditionNodeView conditionView)
            {
                return conditionView.GetPortIndex(output) == ConditionNode.FalsePortIndex
                    ? DialogGraphPortKeys.False
                    : DialogGraphPortKeys.True;
            }

            if (output?.node is ActionNodeView)
            {
                return DialogGraphPortKeys.ActionSuccess;
            }

            return DialogGraphPortKeys.Default;
        }

        #endregion

        #region ---------------- Identity ----------------

        private readonly struct GraphLinkIdentity : IEquatable<GraphLinkIdentity>
        {
            public GraphLinkIdentity(string fromGuid, string toGuid, int fromPortIndex, string fromPortKey)
            {
                FromGuid = fromGuid;
                ToGuid = toGuid;
                FromPortIndex = fromPortIndex;
                FromPortKey = fromPortKey ?? string.Empty;
            }

            public string FromGuid { get; }
            public string ToGuid { get; }
            public int FromPortIndex { get; }
            public string FromPortKey { get; }
            public SourcePortIdentity SourcePort => new SourcePortIdentity(FromGuid, FromPortIndex, FromPortKey);

            public bool Equals(GraphLinkIdentity other)
            {
                var bothHavePortKeys =
                    !string.IsNullOrWhiteSpace(FromPortKey) &&
                    !string.IsNullOrWhiteSpace(other.FromPortKey);

                return string.Equals(FromGuid, other.FromGuid, StringComparison.Ordinal) &&
                       string.Equals(ToGuid, other.ToGuid, StringComparison.Ordinal) &&
                       (bothHavePortKeys
                           ? string.Equals(FromPortKey, other.FromPortKey, StringComparison.Ordinal)
                           : FromPortIndex == other.FromPortIndex);
            }

            public override bool Equals(object obj)
            {
                return obj is GraphLinkIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + (FromGuid != null ? StringComparer.Ordinal.GetHashCode(FromGuid) : 0);
                    hash = hash * 31 + (ToGuid != null ? StringComparer.Ordinal.GetHashCode(ToGuid) : 0);
                    return hash;
                }
            }
        }

        private readonly struct SourcePortIdentity : IEquatable<SourcePortIdentity>
        {
            public SourcePortIdentity(string fromGuid, int fromPortIndex, string fromPortKey)
            {
                FromGuid = fromGuid;
                FromPortIndex = fromPortIndex;
                FromPortKey = fromPortKey ?? string.Empty;
            }

            public string FromGuid { get; }
            public int FromPortIndex { get; }
            public string FromPortKey { get; }

            public bool Equals(SourcePortIdentity other)
            {
                var bothHavePortKeys =
                    !string.IsNullOrWhiteSpace(FromPortKey) &&
                    !string.IsNullOrWhiteSpace(other.FromPortKey);

                return string.Equals(FromGuid, other.FromGuid, StringComparison.Ordinal) &&
                       (bothHavePortKeys
                           ? string.Equals(FromPortKey, other.FromPortKey, StringComparison.Ordinal)
                           : FromPortIndex == other.FromPortIndex);
            }

            public override bool Equals(object obj)
            {
                return obj is SourcePortIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + (FromGuid != null ? StringComparer.Ordinal.GetHashCode(FromGuid) : 0);
                    return hash;
                }
            }
        }

        #endregion
    }
}
