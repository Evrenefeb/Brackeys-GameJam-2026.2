using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Handles clipboard-style graph actions such as duplicating selected nodes.
    /// </summary>
    public sealed class DialogGraphClipboardService
    {
        private static readonly Vector2 DuplicateOffset = new Vector2(40f, 40f);

        /// <summary>
        /// Duplicates all selected Dialog / Choice / Action / Condition / Variable / Graph Jump / Outcome nodes,
        /// including edges between them.
        /// </summary>
        public void DuplicateSelectedNodes(DialogGraphView graphView)
        {
            if (graphView == null)
            {
                return;
            }

            var dialogNodeOriginals = graphView.selection.OfType<DialogNodeView>().ToList();
            var choiceNodeOriginals = graphView.selection.OfType<ChoiceNodeView>().ToList();
            var actionNodeOriginals = graphView.selection.OfType<ActionNodeView>().ToList();
            var conditionNodeOriginals = graphView.selection.OfType<ConditionNodeView>().ToList();
            var variableNodeOriginals = graphView.selection.OfType<VariableMutationNodeView>().ToList();
            var graphJumpNodeOriginals = graphView.selection.OfType<GraphJumpNodeView>().ToList();
            var outcomeNodeOriginals = graphView.selection.OfType<OutcomeNodeView>().ToList();

            if (dialogNodeOriginals.Count == 0 &&
                choiceNodeOriginals.Count == 0 &&
                actionNodeOriginals.Count == 0 &&
                conditionNodeOriginals.Count == 0 &&
                variableNodeOriginals.Count == 0 &&
                graphJumpNodeOriginals.Count == 0 &&
                outcomeNodeOriginals.Count == 0)
                return;

            var existingEdges = graphView.edges.ToList().OfType<Edge>().ToList();

            var mapDialogNodes = new Dictionary<DialogNodeView, DialogNodeView>();
            var mapChoiceNodes = new Dictionary<ChoiceNodeView, ChoiceNodeView>();
            var mapActionNodes = new Dictionary<ActionNodeView, ActionNodeView>();
            var mapConditionNodes = new Dictionary<ConditionNodeView, ConditionNodeView>();
            var mapVariableNodes = new Dictionary<VariableMutationNodeView, VariableMutationNodeView>();
            var mapGraphJumpNodes = new Dictionary<GraphJumpNodeView, GraphJumpNodeView>();
            var mapOutcomeNodes = new Dictionary<OutcomeNodeView, OutcomeNodeView>();

            DuplicateDialogNodes(graphView, dialogNodeOriginals, mapDialogNodes);
            DuplicateChoiceNodes(graphView, choiceNodeOriginals, mapChoiceNodes);
            DuplicateActionNodes(graphView, actionNodeOriginals, mapActionNodes);
            DuplicateConditionNodes(graphView, conditionNodeOriginals, mapConditionNodes);
            DuplicateVariableNodes(graphView, variableNodeOriginals, mapVariableNodes);
            DuplicateGraphJumpNodes(graphView, graphJumpNodeOriginals, mapGraphJumpNodes);
            DuplicateOutcomeNodes(graphView, outcomeNodeOriginals, mapOutcomeNodes);

            DuplicateInternalEdges(
                graphView,
                existingEdges,
                mapDialogNodes,
                mapChoiceNodes,
                mapActionNodes,
                mapConditionNodes,
                mapVariableNodes,
                mapGraphJumpNodes,
                mapOutcomeNodes);

            graphView.ClearSelection();
            foreach (var kv in mapDialogNodes) graphView.AddToSelection(kv.Value);
            foreach (var kv in mapChoiceNodes) graphView.AddToSelection(kv.Value);
            foreach (var kv in mapActionNodes) graphView.AddToSelection(kv.Value);
            foreach (var kv in mapConditionNodes) graphView.AddToSelection(kv.Value);
            foreach (var kv in mapVariableNodes) graphView.AddToSelection(kv.Value);
            foreach (var kv in mapGraphJumpNodes) graphView.AddToSelection(kv.Value);
            foreach (var kv in mapOutcomeNodes) graphView.AddToSelection(kv.Value);
        }

        private static void DuplicateDialogNodes(
            DialogGraphView graphView,
            IEnumerable<DialogNodeView> originals,
            IDictionary<DialogNodeView, DialogNodeView> map)
        {
            foreach (var src in originals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + DuplicateOffset;

                var clone = graphView.CreateDialogNode(src.nodeTitle, false, pos.x, pos.y);
                clone.LoadNodeData(
                    src.speakerName,
                    src.questionText,
                    src.nodeTitle,
                    src.portraitSprite,
                    src.dialogueAudio,
                    src.displayTimeSeconds,
                    src.waitForAudioFinish
                );
                clone.SetPosition(new Rect(pos, srcRect.size));
                map[src] = clone;
            }
        }

        private static void DuplicateChoiceNodes(
            DialogGraphView graphView,
            IEnumerable<ChoiceNodeView> originals,
            IDictionary<ChoiceNodeView, ChoiceNodeView> map)
        {
            foreach (var src in originals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + DuplicateOffset;

                var clone = graphView.CreateChoiceNode("Choice", false, pos.x, pos.y);
                clone.LoadNodeData(null);
                clone.LoadAnswers(src.answers.Select(a => Choice.Create(a)).ToList());
                clone.SetPosition(new Rect(pos, srcRect.size));
                map[src] = clone;
            }
        }

        private static void DuplicateActionNodes(
            DialogGraphView graphView,
            IEnumerable<ActionNodeView> originals,
            IDictionary<ActionNodeView, ActionNodeView> map)
        {
            foreach (var src in originals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + DuplicateOffset;

                var clone = graphView.CreateActionNode("Action", false, pos.x, pos.y);
                clone.LoadNodeData(src.actionId, src.payloadJson, src.waitForCompletion, src.waitSeconds);
                clone.SetPosition(new Rect(pos, srcRect.size));
                map[src] = clone;
            }
        }

        private static void DuplicateConditionNodes(
            DialogGraphView graphView,
            IEnumerable<ConditionNodeView> originals,
            IDictionary<ConditionNodeView, ConditionNodeView> map)
        {
            foreach (var src in originals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + DuplicateOffset;

                var clone = graphView.CreateConditionNode("Condition", false, pos.x, pos.y);
                var data = clone.data;
                if (data != null)
                {
                    Undo.RecordObject(data, "Duplicate Condition Node");
                    data.variableName = src.variableName;
                    data.valueType = src.valueType;
                    data.conditionOperator = src.conditionOperator;
                    data.comparisonValue = src.comparisonValue;
                    data.missingVariableResult = src.missingVariableResult;
                    EditorUtility.SetDirty(data);
                }
                clone.LoadNodeData(src.variableName, src.valueType, src.conditionOperator, src.comparisonValue, src.missingVariableResult);
                clone.SetPosition(new Rect(pos, srcRect.size));
                map[src] = clone;
            }
        }

        private static void DuplicateVariableNodes(
            DialogGraphView graphView,
            IEnumerable<VariableMutationNodeView> originals,
            IDictionary<VariableMutationNodeView, VariableMutationNodeView> map)
        {
            foreach (var src in originals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + DuplicateOffset;

                var clone = graphView.CreateVariableMutationNode("Set Variable", false, pos.x, pos.y);
                var data = clone.data;
                if (data != null)
                {
                    Undo.RecordObject(data, "Duplicate Variable Node");
                    data.variableName = src.variableName;
                    data.valueType = src.valueType;
                    data.operation = src.operation;
                    data.value = src.value;
                    EditorUtility.SetDirty(data);
                }
                clone.LoadNodeData(src.variableName, src.valueType, src.operation, src.value);
                clone.SetPosition(new Rect(pos, srcRect.size));
                map[src] = clone;
            }
        }

        private static void DuplicateGraphJumpNodes(
            DialogGraphView graphView,
            IEnumerable<GraphJumpNodeView> originals,
            IDictionary<GraphJumpNodeView, GraphJumpNodeView> map)
        {
            foreach (var src in originals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + DuplicateOffset;

                var clone = graphView.CreateGraphJumpNode("Graph Jump", false, pos.x, pos.y);
                var data = clone.data;
                if (data != null)
                {
                    Undo.RecordObject(data, "Duplicate Graph Jump Node");
                    CopyGraphReference(src.data?.targetGraph, data.targetGraph);
                    EditorUtility.SetDirty(data);
                }

                clone.LoadNodeData(data?.targetGraph);
                clone.SetPosition(new Rect(pos, srcRect.size));
                map[src] = clone;
            }
        }

        private static void DuplicateOutcomeNodes(
            DialogGraphView graphView,
            IEnumerable<OutcomeNodeView> originals,
            IDictionary<OutcomeNodeView, OutcomeNodeView> map)
        {
            foreach (var src in originals)
            {
                var srcRect = src.GetPosition();
                var pos = srcRect.position + DuplicateOffset;

                var clone = graphView.CreateOutcomeNode("Outcome", false, pos.x, pos.y);
                var data = clone.data;
                if (data != null)
                {
                    Undo.RecordObject(data, "Duplicate Outcome Node");
                    data.outcomeId = src.outcomeId;
                    data.displayName = src.displayName;
                    data.description = src.description;
                    EditorUtility.SetDirty(data);
                }

                clone.LoadNodeData(src.outcomeId, src.displayName, src.description);
                clone.SetPosition(new Rect(pos, srcRect.size));
                map[src] = clone;
            }
        }

        private static void CopyGraphReference(GraphReference source, GraphReference target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.graphAsset = source.graphAsset;
            target.graphGuid = source.graphGuid;
            target.runtimeDialogId = source.runtimeDialogId;
            target.graphName = source.graphName;
            target.assetPath = source.assetPath;
            target.entryGuid = source.entryGuid;
        }

        private static void DuplicateInternalEdges(
            DialogGraphView graphView,
            IEnumerable<Edge> existingEdges,
            IReadOnlyDictionary<DialogNodeView, DialogNodeView> mapDialogNodes,
            IReadOnlyDictionary<ChoiceNodeView, ChoiceNodeView> mapChoiceNodes,
            IReadOnlyDictionary<ActionNodeView, ActionNodeView> mapActionNodes,
            IReadOnlyDictionary<ConditionNodeView, ConditionNodeView> mapConditionNodes,
            IReadOnlyDictionary<VariableMutationNodeView, VariableMutationNodeView> mapVariableNodes,
            IReadOnlyDictionary<GraphJumpNodeView, GraphJumpNodeView> mapGraphJumpNodes,
            IReadOnlyDictionary<OutcomeNodeView, OutcomeNodeView> mapOutcomeNodes)
        {
            foreach (var e in existingEdges)
            {
                var from = e.output?.node as Node;
                var to = e.input?.node as Node;

                var fromClone = ResolveClone(from, mapDialogNodes, mapChoiceNodes, mapActionNodes, mapConditionNodes, mapVariableNodes, mapGraphJumpNodes, mapOutcomeNodes);
                var toClone = ResolveClone(to, mapDialogNodes, mapChoiceNodes, mapActionNodes, mapConditionNodes, mapVariableNodes, mapGraphJumpNodes, mapOutcomeNodes);

                if (fromClone == null || toClone == null) continue;

                var outPort = ResolveOutputPort(e, fromClone);
                var inPort = ResolveInputPort(toClone);

                if (outPort != null && inPort != null)
                {
                    var newEdge = DialogGraphEdgeFactory.Connect(outPort, inPort);
                    if (newEdge != null)
                    {
                        graphView.AddElement(newEdge);

                        // Also create the backing GraphLink in the data model
                        var fromGuid = GetNodeGuid(fromClone);
                        var toGuid = GetNodeGuid(toClone);
                        if (!string.IsNullOrEmpty(fromGuid) && !string.IsNullOrEmpty(toGuid))
                        {
                            var graphAsset = DialogGraphAssetPaths.LoadGraphAsset(graphView.graphId);
                            if (graphAsset != null)
                            {
                                var link = DialogGraphLinkMutationService.AddOrReplaceLink(
                                    graphAsset, fromGuid, toGuid, 0,
                                    fromPortKey:  "Default",
                                    toPortKey: "Default");
                                if (link != null && newEdge is DialogGraphEdge dge)
                                {
                                    DialogGraphEdgeFactory.ApplyLinkIdentity(dge, link);
                                    dge.Initialize(graphAsset, graphView);
                                    dge.RefreshGeometry();
                                }
                            }
                        }
                    }
                }
            }
        }

        private static Node ResolveClone(
            Node source,
            IReadOnlyDictionary<DialogNodeView, DialogNodeView> mapDialogNodes,
            IReadOnlyDictionary<ChoiceNodeView, ChoiceNodeView> mapChoiceNodes,
            IReadOnlyDictionary<ActionNodeView, ActionNodeView> mapActionNodes,
            IReadOnlyDictionary<ConditionNodeView, ConditionNodeView> mapConditionNodes,
            IReadOnlyDictionary<VariableMutationNodeView, VariableMutationNodeView> mapVariableNodes,
            IReadOnlyDictionary<GraphJumpNodeView, GraphJumpNodeView> mapGraphJumpNodes,
            IReadOnlyDictionary<OutcomeNodeView, OutcomeNodeView> mapOutcomeNodes)
        {
            if (source is DialogNodeView dialogNode && mapDialogNodes.TryGetValue(dialogNode, out var dialogClone)) return dialogClone;
            if (source is ChoiceNodeView choiceNode && mapChoiceNodes.TryGetValue(choiceNode, out var choiceClone)) return choiceClone;
            if (source is ActionNodeView actionNode && mapActionNodes.TryGetValue(actionNode, out var actionClone)) return actionClone;
            if (source is ConditionNodeView conditionNode && mapConditionNodes.TryGetValue(conditionNode, out var conditionClone)) return conditionClone;
            if (source is VariableMutationNodeView variableNode && mapVariableNodes.TryGetValue(variableNode, out var variableClone)) return variableClone;
            if (source is GraphJumpNodeView graphJumpNode && mapGraphJumpNodes.TryGetValue(graphJumpNode, out var graphJumpClone)) return graphJumpClone;
            if (source is OutcomeNodeView outcomeNode && mapOutcomeNodes.TryGetValue(outcomeNode, out var outcomeClone)) return outcomeClone;

            return null;
        }

        private static string GetNodeGuid(Node nodeView)
        {
            if (nodeView is DialogNodeView dv) return dv.GUID;
            if (nodeView is ChoiceNodeView cv) return cv.GUID;
            if (nodeView is ActionNodeView av) return av.GUID;
            if (nodeView is ConditionNodeView cnv) return cnv.GUID;
            if (nodeView is VariableMutationNodeView vv) return vv.GUID;
            if (nodeView is GraphJumpNodeView gj) return gj.GUID;
            if (nodeView is OutcomeNodeView ov) return ov.GUID;
            if (nodeView is StartNodeView sv) return sv.GUID;
            if (nodeView is EndNodeView ev) return ev.GUID;
            return null;
        }

        private static Port ResolveOutputPort(Edge sourceEdge, Node fromClone)
        {
            if (fromClone is DialogNodeView dialogNode) return dialogNode.outputPort;
            if (fromClone is ActionNodeView actionNode) return actionNode.outputPort;
            if (fromClone is VariableMutationNodeView variableNode) return variableNode.outputPort;
            if (fromClone is GraphJumpNodeView graphJumpNode) return graphJumpNode.outputPort;
            if (fromClone is OutcomeNodeView outcomeNode) return outcomeNode.outputPort;
            if (fromClone is ConditionNodeView conditionNode)
            {
                int idx = ((ConditionNodeView)sourceEdge.output.node).GetPortIndex((Port)sourceEdge.output);
                return conditionNode.GetOutputPort(idx);
            }
            if (fromClone is ChoiceNodeView choiceNode)
            {
                int idx = ((ChoiceNodeView)sourceEdge.output.node).GetPortIndex((Port)sourceEdge.output);
                if (idx >= 0 && idx < choiceNode.outputPorts.Count)
                    return choiceNode.outputPorts[idx];
            }

            return null;
        }

        private static Port ResolveInputPort(Node toClone)
        {
            if (toClone is DialogNodeView dialogNode) return dialogNode.inputPort;
            if (toClone is ChoiceNodeView choiceNode) return choiceNode.inputPort;
            if (toClone is ActionNodeView actionNode) return actionNode.inputPort;
            if (toClone is VariableMutationNodeView variableNode) return variableNode.inputPort;
            if (toClone is ConditionNodeView conditionNode) return conditionNode.inputPort;
            if (toClone is GraphJumpNodeView graphJumpNode) return graphJumpNode.inputPort;
            if (toClone is OutcomeNodeView outcomeNode) return outcomeNode.inputPort;

            return null;
        }
    }
}
