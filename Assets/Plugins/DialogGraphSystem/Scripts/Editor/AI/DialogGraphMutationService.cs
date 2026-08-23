using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Util;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;
using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DialogSystem.EditorTools.AI
{
    [Serializable]
    public sealed class DialogNodeMutationData
    {
        public string guid;
        public string title;
        public string speakerName;
        public string questionText;
        public float displayTime;
    }

    [Serializable]
    public sealed class ChoiceMutationData
    {
        public string answerText;
        public string nextNodeGuid;
        public string tooltipOrSubLabel;
    }

    [Serializable]
    public sealed class ChoiceOptionMutationData
    {
        public string answerText;
        public string followUpSpeakerName;
        public string followUpText;
    }

    [Serializable]
    public sealed class ChoiceNodeMutationData
    {
        public string guid;
        public string text;
        public List<ChoiceMutationData> choices;
    }

    [Serializable]
    public sealed class ActionNodeMutationData
    {
        public string guid;
        public string actionId;
        public string payloadJson;
        public bool waitForCompletion;
        public float waitSeconds;
    }

    [Serializable]
    public sealed class ConditionNodeMutationData
    {
        public string guid;
        public string variableName;
        public DialogueVariableValueType valueType;
        public ConditionOperator conditionOperator;
        public string comparisonValue;
        public bool missingVariableResult;
    }

    [Serializable]
    public sealed class OutcomeNodeMutationData
    {
        public string guid;
        public string outcomeId;
        public string displayName;
        public string description;
    }

    [Serializable]
    public sealed class DialogFlowChoiceOptionMutationData
    {
        public string answerText;
        public List<DialogNodeMutationData> branchNodes;
    }

    [Serializable]
    public sealed class DialogFlowMutationData
    {
        public List<DialogNodeMutationData> linearNodes;
        public string choiceText;
        public List<DialogFlowChoiceOptionMutationData> choices;
        public bool connectBranchesToEnd;
    }

    /// <summary>
    /// Central mutation boundary for AI-driven graph edits.
    /// AI-facing execution code should only use this service and never mutate GraphView internals directly.
    /// </summary>
    public sealed class DialogGraphMutationService
    {
        private readonly IDialogGraphOwner _owner;
        private readonly DialogGraphLayoutService _layoutService;
        private int _mutationBatchDepth;
        private bool _pendingGraphDirty;
        private bool _pendingSaveAssets;
        private bool _pendingGraphRefresh;
        private bool _pendingOwnerRefresh;

        public DialogGraphMutationService(IDialogGraphOwner owner)
        {
            _owner = owner;
            _layoutService = new DialogGraphLayoutService();
        }

        private DialogGraphView GraphView => _owner?.GetGraphView();

        public DialogNodeView CreateDialogNode(DialogNodeMutationData draft, Vector2 position)
        {
            BeginMutationBatch();
            try
            {
                var graphView = RequireGraphView();
                var undoGroup = BeginUndoGroup("Create Dialog Node");

                try
                {
                    var title = !string.IsNullOrWhiteSpace(draft?.title) ? draft.title.Trim() : "Dialog";
                    var view = graphView.CreateDialogNode(title, false, position.x, position.y);
                    var data = FindDialogNodeData(view.GUID);

                    if (data != null)
                    {
                        Undo.RecordObject(data, "Configure Dialog Node");
                        ApplyDialogDraft(data, draft, position, title);
                        EditorUtility.SetDirty(data);
                    }

                    if (view != null)
                    {
                        view.LoadNodeData(
                            data?.speakerName ?? draft?.speakerName ?? string.Empty,
                            data?.questionText ?? draft?.questionText ?? string.Empty,
                            title,
                            data?.speakerPortrait,
                            data?.dialogAudio,
                            data != null ? data.displayTime : draft?.displayTime ?? 0f,
                            data?.waitForAudioFinish ?? false);
                        view.SetPosition(new Rect(position, view.GetPosition().size));
                    }

                    MarkGraphDirty();
                    RefreshGraphView();
                    return view;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        public ChoiceNodeView CreateChoiceNode(ChoiceNodeMutationData draft, Vector2 position)
        {
            BeginMutationBatch();
            try
            {
                var graphView = RequireGraphView();
                var undoGroup = BeginUndoGroup("Create Choice Node");

                try
                {
                    var view = graphView.CreateChoiceNode("Choice", false, position.x, position.y);
                    var data = FindChoiceNodeData(view.GUID);

                    if (data != null)
                    {
                        Undo.RecordObject(data, "Configure Choice Node");
                        ApplyChoiceDraft(data, draft, position);
                        EditorUtility.SetDirty(data);
                    }

                    if (view != null)
                    {
                        view.LoadNodeData(data?.choices);
                        view.SetPosition(new Rect(position, view.GetPosition().size));
                    }

                    MarkGraphDirty();
                    RefreshGraphView();
                    return view;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        public ActionNodeView CreateActionNode(ActionNodeMutationData draft, Vector2 position)
        {
            BeginMutationBatch();
            try
            {
                var graphView = RequireGraphView();
                var undoGroup = BeginUndoGroup("Create Action Node");

                try
                {
                    var view = graphView.CreateActionNode("Action", false, position.x, position.y);
                    var data = FindActionNodeData(view.GUID);

                    if (data != null)
                    {
                        Undo.RecordObject(data, "Configure Action Node");
                        ApplyActionDraft(data, draft, position);
                        EditorUtility.SetDirty(data);
                    }

                    if (view != null)
                    {
                        view.LoadNodeData(
                            data?.actionId ?? draft?.actionId ?? string.Empty,
                            data?.payloadJson ?? draft?.payloadJson ?? string.Empty,
                            data != null && data.waitForCompletion,
                            data != null ? data.waitSeconds : draft?.waitSeconds ?? 0f);
                        view.SetPosition(new Rect(position, view.GetPosition().size));
                    }

                    MarkGraphDirty();
                    RefreshGraphView();
                    return view;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        public ConditionNodeView CreateConditionNode(ConditionNodeMutationData draft, Vector2 position)
        {
            BeginMutationBatch();
            try
            {
                var graphView = RequireGraphView();
                var undoGroup = BeginUndoGroup("Create Condition Node");

                try
                {
                    var view = graphView.CreateConditionNode("Condition", false, position.x, position.y);
                    var data = FindConditionNodeData(view.GUID);

                    if (data != null)
                    {
                        Undo.RecordObject(data, "Configure Condition Node");
                        ApplyConditionDraft(data, draft, position);
                        EditorUtility.SetDirty(data);
                    }

                    if (view != null)
                    {
                        view.LoadNodeData(
                            data?.variableName ?? draft.variableName ?? string.Empty,
                            data?.valueType ?? draft.valueType,
                            data?.conditionOperator ?? draft.conditionOperator,
                            data?.comparisonValue ?? draft.comparisonValue ?? string.Empty,
                            data != null ? data.missingVariableResult : draft.missingVariableResult);
                        view.SetPosition(new Rect(position, view.GetPosition().size));
                    }

                    MarkGraphDirty();
                    RefreshGraphView();
                    return view;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }
        /// <summary>
        /// Creates an OutcomeNodeView and returns it.
        /// Outcome nodes record a named ending result before routing to End.
        /// </summary>
        public OutcomeNodeView CreateOutcomeNode(OutcomeNodeMutationData draft, Vector2 position)
        {
            BeginMutationBatch();
            try
            {
                var graphView = RequireGraphView();
                var undoGroup = BeginUndoGroup("Create Outcome Node");

                try
                {
                    var view = graphView.CreateOutcomeNode("Outcome", false, position.x, position.y);
                    var data = FindOutcomeNodeData(view.GUID);

                    if (data != null)
                    {
                        Undo.RecordObject(data, "Configure Outcome Node");
                        data.outcomeId = draft?.outcomeId ?? string.Empty;
                        data.displayName = draft?.displayName ?? string.Empty;
                        data.description = draft?.description ?? string.Empty;
                        EditorUtility.SetDirty(data);
                    }

                    if (view != null)
                    {
                        view.LoadNodeData(
                            data?.outcomeId ?? draft?.outcomeId ?? string.Empty,
                            data?.displayName ?? draft?.displayName ?? string.Empty,
                            data?.description ?? draft?.description ?? string.Empty);
                        view.SetPosition(new Rect(position, view.GetPosition().size));
                    }

                    MarkGraphDirty();
                    RefreshGraphView();
                    return view;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        private OutcomeNode FindOutcomeNodeData(string guid)
        {
            var graph = LoadGraphAssetSafe();
            if (graph == null || string.IsNullOrWhiteSpace(guid)) return null;
            return graph.outcomeNodes?.FirstOrDefault(n =>
                string.Equals(n?.GetGuid(), guid, StringComparison.Ordinal));
        }

        public Edge CreateLink(Node fromNode, Node toNode, int fromPortIndex = 0)
        {
            BeginMutationBatch();
            try
            {
                var graphView = RequireGraphView();
                if (fromNode == null || toNode == null)
                {
                    throw new ArgumentNullException(fromNode == null ? nameof(fromNode) : nameof(toNode));
                }

                var outputPort = GetOutputPort(fromNode, fromPortIndex);
                var inputPort = GetInputPort(toNode);
                if (outputPort == null || inputPort == null)
                {
                    RebuildNodePortsIfSupported(fromNode);
                    RebuildNodePortsIfSupported(toNode);
                    fromNode.RefreshPorts();
                    toNode.RefreshPorts();

                    outputPort = GetOutputPort(fromNode, fromPortIndex);
                    inputPort = GetInputPort(toNode);
                    if (outputPort == null || inputPort == null)
                    {
                        throw new InvalidOperationException(
                            $"Could not resolve compatible ports for the requested link. " +
                            $"From: {fromNode.GetType().Name} port {fromPortIndex}, To: {toNode.GetType().Name}.");
                    }
                }

                var existing = FindEdge(fromNode, toNode, fromPortIndex);
                if (existing != null)
                {
                    return existing;
                }

                var undoGroup = BeginUndoGroup("Create Link");
                try
                {
                    var edge = DialogGraphEdgeFactory.Connect(outputPort, inputPort);
                    if (edge == null)
                    {
                        throw new InvalidOperationException("Failed to connect the selected ports.");
                    }

                    // For AI mutation, we need to ensure the link identity is stamped immediately
                    // so reroute points and other visual features work without waiting for a save/reload cycle.
                    var asset = LoadGraphAsset();
                    var fromGuid = ExtractGuidFromView(fromNode);
                    var toGuid = ExtractGuidFromView(toNode);
                    var fromPortKey = GetOutputPortKey(asset, outputPort, true);
                    var toPortKey = GetInputPortKey(inputPort);

                    var link = DialogGraphLinkMutationService.AddOrReplaceLink(asset, fromGuid, toGuid, fromPortIndex, fromPortKey, toPortKey);
                    
                    if (edge is DialogGraphEdge dge && link != null)
                    {
                        DialogGraphEdgeFactory.ApplyLinkIdentity(dge, link);
                        dge.Initialize(asset, graphView);
                    }

                    graphView.AddElement(edge);
                    if (edge is DialogGraphEdge initializedEdge)
                    {
                        initializedEdge.RefreshGeometry();
                    }
                    MarkGraphDirty();
                    RefreshGraphView();
                    return edge;
                }
                finally
                {
                    if (undoGroup != -1) EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
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

        private static string GetOutputPortKey(DialogGraph asset, Port output, bool assignMissingChoiceId)
        {
            if (output?.node is ChoiceNodeView choiceView)
            {
                var choiceIndex = choiceView.GetPortIndex(output);
                if (choiceIndex < 0) return string.Empty;

                var choiceNode = asset?.choiceNodes?.FirstOrDefault(node =>
                    node != null && node.GetGuid() == choiceView.GUID);

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
                        return choice.PortKey;
                    }
                }
                return string.Empty;
            }

            if (output?.node is ConditionNodeView conditionView)
            {
                return conditionView.GetPortIndex(output) == ConditionNode.FalsePortIndex
                    ? DialogGraphPortKeys.False
                    : DialogGraphPortKeys.True;
            }

            if (output?.node is ActionNodeView) return DialogGraphPortKeys.ActionSuccess;

            return DialogGraphPortKeys.Default;
        }

        private static string GetInputPortKey(Port input)
        {
            // Currently all input ports use "default"
            return DialogGraphPortKeys.Default;
        }

        public bool InsertNodeBetween(Node fromNode, Node toNode, Node insertedNode, int fromPortIndex = 0)
        {
            BeginMutationBatch();
            try
            {
                if (fromNode == null || toNode == null || insertedNode == null)
                {
                    return false;
                }

                var undoGroup = BeginUndoGroup("Insert Node Between");
                try
                {
                    var insertedPosition = _layoutService.GetInsertedNodeBetweenPosition(GraphView, fromNode, toNode, insertedNode);
                    insertedNode.SetPosition(new Rect(insertedPosition, insertedNode.GetPosition().size));

                    RemoveLink(fromNode, toNode, fromPortIndex);
                    CreateLink(fromNode, insertedNode, fromPortIndex);
                    CreateLink(insertedNode, toNode, 0);
                    MarkGraphDirty();
                    RefreshGraphView();
                    return true;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        public List<DialogNodeView> InsertChainBeforeEnd(IReadOnlyList<DialogNodeMutationData> dialogDrafts)
        {
            BeginMutationBatch();
            try
            {
                var graphView = RequireGraphView();
                if (dialogDrafts == null || dialogDrafts.Count == 0)
                {
                    return new List<DialogNodeView>();
                }

                var endNode = graphView.nodes.ToList().OfType<EndNodeView>().FirstOrDefault();
                if (endNode == null)
                {
                    throw new InvalidOperationException("End node was not found in the graph.");
                }

                var undoGroup = BeginUndoGroup("Insert Dialog Chain Before End");
                try
                {
                    var created = new List<DialogNodeView>(dialogDrafts.Count);
                    var positions = _layoutService.GetChainBeforeEndPositions(graphView, endNode, dialogDrafts.Count);

                    for (var i = 0; i < dialogDrafts.Count; i++)
                    {
                        var pos = i < positions.Count ? positions[i] : Vector2.zero;
                        created.Add(CreateDialogNode(dialogDrafts[i], pos));
                    }

                    var first = created[0];
                    var last = created[created.Count - 1];

                    var incomingToEnd = graphView.edges.ToList()
                        .OfType<Edge>()
                        .Where(edge => edge.input?.node == endNode)
                        .Select(edge => new EdgeSnapshot(edge.output?.node as Node, GetPortIndex(edge.output), edge.input?.node as Node))
                        .Where(snapshot => snapshot.FromNode != null && snapshot.ToNode != null)
                        .ToList();

                    foreach (var incoming in graphView.edges.ToList().OfType<Edge>().Where(edge => edge.input?.node == endNode).ToList())
                    {
                        graphView.RemoveElement(incoming);
                    }

                    foreach (var snapshot in incomingToEnd)
                    {
                        CreateLink(snapshot.FromNode, first, snapshot.FromPortIndex);
                    }

                    for (var i = 0; i < created.Count - 1; i++)
                    {
                        CreateLink(created[i], created[i + 1], 0);
                    }

                    CreateLink(last, endNode, 0);
                    MarkGraphDirty();
                    RefreshGraphView();
                    return created;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        public ActionNodeView InsertActionNodeAfter(Node selectedNode, ActionNodeMutationData draft, int fromPortIndex = 0)
        {
            BeginMutationBatch();
            try
            {
                if (selectedNode == null)
                {
                    throw new ArgumentNullException(nameof(selectedNode));
                }

                var undoGroup = BeginUndoGroup("Insert Action Node After");
                try
                {
                    var firstOutgoingTarget = FindOutgoingEdges(selectedNode, fromPortIndex)
                        .Select(edge => edge.input?.node as Node)
                        .FirstOrDefault(node => node != null);

                    var actionPosition = _layoutService.GetInsertedActionNodePosition(GraphView, selectedNode, firstOutgoingTarget);
                    var actionView = CreateActionNode(draft, actionPosition);

                    var outgoingEdges = FindOutgoingEdges(selectedNode, fromPortIndex)
                        .Select(edge => new EdgeSnapshot(selectedNode, fromPortIndex, edge.input?.node as Node))
                        .Where(snapshot => snapshot.ToNode != null)
                        .ToList();

                    foreach (var edge in FindOutgoingEdges(selectedNode, fromPortIndex).ToList())
                    {
                        GraphView.RemoveElement(edge);
                    }

                    CreateLink(selectedNode, actionView, fromPortIndex);

                    foreach (var snapshot in outgoingEdges)
                    {
                        CreateLink(actionView, snapshot.ToNode, 0);
                    }

                    MarkGraphDirty();
                    RefreshGraphView();
                    return actionView;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        /// <summary>
        /// Inserts a condition node after <paramref name="selectedNode"/> on the given output port.
        /// Removes existing outgoing edges, wires the selected node into the condition input,
        /// routes the <c>True</c> branch to the original next node, and routes the
        /// <c>False</c> branch to the graph End node.
        /// </summary>
        /// <param name="selectedNode">The anchor node to insert after.</param>
        /// <param name="draft">Content for the new condition node.</param>
        /// <param name="fromPortIndex">Output port index on <paramref name="selectedNode"/>.</param>
        /// <returns>The created <see cref="ConditionNodeView"/>.</returns>
        public ConditionNodeView InsertConditionNodeAfter(Node selectedNode, ConditionNodeMutationData draft, int fromPortIndex = 0)
        {
            BeginMutationBatch();
            try
            {
                if (selectedNode == null)
                {
                    throw new ArgumentNullException(nameof(selectedNode));
                }

                var graphView = RequireGraphView();
                var undoGroup = BeginUndoGroup("Insert Condition Node After");
                try
                {
                    // Snapshot the original outgoing targets before we disconnect them.
                    var outgoingEdges = FindOutgoingEdges(selectedNode, fromPortIndex)
                        .Select(edge => new EdgeSnapshot(selectedNode, fromPortIndex, edge.input?.node as Node))
                        .Where(snapshot => snapshot.ToNode != null)
                        .ToList();

                    var firstOutgoingTarget = outgoingEdges
                        .Select(snapshot => snapshot.ToNode)
                        .FirstOrDefault();

                    // Position the new condition node between the anchor and the first outgoing target.
                    var conditionPosition = _layoutService.GetInsertedActionNodePosition(GraphView, selectedNode, firstOutgoingTarget);

                    // Remove all outgoing edges from the anchor port.
                    foreach (var edge in FindOutgoingEdges(selectedNode, fromPortIndex).ToList())
                    {
                        GraphView.RemoveElement(edge);
                    }

                    // Create and configure the condition node.
                    var view = graphView.CreateConditionNode("Condition", false, conditionPosition.x, conditionPosition.y);
                    var data = FindConditionNodeData(view.GUID);

                    if (data != null)
                    {
                        Undo.RecordObject(data, "Configure Condition Node");
                        ApplyConditionDraft(data, draft, conditionPosition);
                        EditorUtility.SetDirty(data);
                    }

                    if (view != null)
                    {
                        view.LoadNodeData(
                            data?.variableName ?? draft.variableName ?? string.Empty,
                            data?.valueType ?? draft.valueType,
                            data?.conditionOperator ?? draft.conditionOperator,
                            data?.comparisonValue ?? draft.comparisonValue ?? string.Empty,
                            data != null ? data.missingVariableResult : draft.missingVariableResult);
                    }

                    // Wire anchor → condition input.
                    CreateLink(selectedNode, view, fromPortIndex);

                    // Wire condition true output → original next node.
                    var trueTarget = firstOutgoingTarget;
                    if (trueTarget != null)
                    {
                        CreateLink(view, trueTarget, ConditionNode.TruePortIndex);
                    }

                    // Wire condition false output → End node.
                    var endNode = graphView.nodes.ToList().OfType<EndNodeView>().FirstOrDefault();
                    if (endNode == null)
                    {
                        throw new InvalidOperationException(
                            "End node was not found in the graph. " +
                            "A condition node requires an End node for its false branch.");
                    }

                    CreateLink(view, endNode, ConditionNode.FalsePortIndex);

                    MarkGraphDirty();
                    RefreshGraphView();
                    return view;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }


        public List<DialogNodeView> InsertDialogChainAfter(Node selectedNode, IReadOnlyList<DialogNodeMutationData> dialogDrafts, int fromPortIndex = 0)
        {
            BeginMutationBatch();
            try
            {
                if (selectedNode == null)
                {
                    throw new ArgumentNullException(nameof(selectedNode));
                }

                if (dialogDrafts == null || dialogDrafts.Count == 0)
                {
                    return new List<DialogNodeView>();
                }

                var outgoingEdges = FindOutgoingEdges(selectedNode, fromPortIndex).ToList();
                if (outgoingEdges.Count > 1)
                {
                    throw new InvalidOperationException("The selected node has multiple outgoing links. Select a linear node before inserting a generated chain.");
                }

                var undoGroup = BeginUndoGroup("Insert Dialog Chain After");
                try
                {
                    var originalNextNode = outgoingEdges
                        .Select(edge => edge.input?.node as Node)
                        .FirstOrDefault(node => node != null);

                    var created = new List<DialogNodeView>(dialogDrafts.Count);
                    var positions = _layoutService.GetChainBetweenPositions(GraphView, selectedNode, originalNextNode, dialogDrafts.Count);

                    for (var i = 0; i < dialogDrafts.Count; i++)
                    {
                        var pos = i < positions.Count ? positions[i] : Vector2.zero;
                        created.Add(CreateDialogNode(dialogDrafts[i], pos));
                    }

                    foreach (var edge in outgoingEdges)
                    {
                        GraphView.RemoveElement(edge);
                    }

                    CreateLink(selectedNode, created[0], fromPortIndex);

                    for (var i = 0; i < created.Count - 1; i++)
                    {
                        CreateLink(created[i], created[i + 1], 0);
                    }

                    if (originalNextNode != null)
                    {
                        CreateLink(created[created.Count - 1], originalNextNode, 0);
                    }

                    MarkGraphDirty();
                    RefreshGraphView();
                    return created;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        public ChoiceNodeView InsertChoiceNodeAfter(
            Node selectedNode,
            ChoiceNodeMutationData choiceDraft,
            IReadOnlyList<ChoiceOptionMutationData> optionDrafts,
            int fromPortIndex = 0)
        {
            BeginMutationBatch();
            try
            {
                if (selectedNode == null)
                {
                    throw new ArgumentNullException(nameof(selectedNode));
                }

                var sanitizedOptionDrafts = optionDrafts?.Where(option => option != null).ToList()
                                          ?? new List<ChoiceOptionMutationData>();
                if (sanitizedOptionDrafts.Count == 0)
                {
                    throw new InvalidOperationException("Choice insertion requires at least one option.");
                }

                var outgoingEdges = FindOutgoingEdges(selectedNode, fromPortIndex).ToList();
                if (outgoingEdges.Count > 1)
                {
                    throw new InvalidOperationException("The selected node has multiple outgoing links. Select a linear node before inserting a choice node.");
                }

                var undoGroup = BeginUndoGroup("Insert Choice Node After");
                try
                {
                    var originalNextNode = outgoingEdges
                        .Select(edge => edge.input?.node as Node)
                        .FirstOrDefault(node => node != null);

                    var choicePosition = _layoutService.GetInsertedChoiceNodePosition(GraphView, selectedNode, originalNextNode);
                    var choiceView = CreateChoiceNode(choiceDraft, choicePosition);

                    foreach (var edge in outgoingEdges)
                    {
                        GraphView.RemoveElement(edge);
                    }

                    CreateLink(selectedNode, choiceView, fromPortIndex);

                    var branchPositions = _layoutService.GetBranchChildrenPositions(GraphView, choiceView, sanitizedOptionDrafts.Count);
                    for (var i = 0; i < sanitizedOptionDrafts.Count; i++)
                    {
                        var optionDraft = sanitizedOptionDrafts[i];
                        var hasFollowUp = !string.IsNullOrWhiteSpace(optionDraft.followUpText) ||
                                          !string.IsNullOrWhiteSpace(optionDraft.followUpSpeakerName);

                        if (hasFollowUp)
                        {
                            var followUpDraft = new DialogNodeMutationData
                            {
                                title = $"Choice {i + 1} Reply",
                                speakerName = optionDraft.followUpSpeakerName ?? string.Empty,
                                questionText = optionDraft.followUpText ?? string.Empty,
                                displayTime = 0f
                            };

                            var followUpPosition = i < branchPositions.Count ? branchPositions[i] : Vector2.zero;
                            var followUpView = CreateDialogNode(followUpDraft, followUpPosition);
                            CreateLink(choiceView, followUpView, i);

                            if (originalNextNode != null)
                            {
                                CreateLink(followUpView, originalNextNode, 0);
                            }
                        }
                        else if (originalNextNode != null)
                        {
                            CreateLink(choiceView, originalNextNode, i);
                        }
                    }

                    MarkGraphDirty();
                    RefreshGraphView();
                    return choiceView;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        public ChoiceNodeView InsertDialogFlowAfter(
            Node selectedNode,
            DialogFlowMutationData flowDraft,
            int fromPortIndex = 0)
        {
            BeginMutationBatch();
            try
            {
                if (selectedNode == null)
                {
                    throw new ArgumentNullException(nameof(selectedNode));
                }

                if (flowDraft?.choices == null || flowDraft.choices.Count < 2)
                {
                    throw new InvalidOperationException("Dialog flow insertion requires at least two choice branches.");
                }

                var sanitizedLinearNodes = flowDraft.linearNodes?.Where(node => node != null).ToList()
                                         ?? new List<DialogNodeMutationData>();
                var sanitizedChoices = flowDraft.choices
                    .Where(choice => choice != null && !string.IsNullOrWhiteSpace(choice.answerText))
                    .ToList();

                if (sanitizedChoices.Count < 2)
                {
                    throw new InvalidOperationException("Dialog flow insertion requires at least two valid choice options.");
                }

                var outgoingEdges = FindOutgoingEdges(selectedNode, fromPortIndex).ToList();
                if (outgoingEdges.Count > 1)
                {
                    throw new InvalidOperationException("The selected node has multiple outgoing links. Select a single linear branch before inserting a dialog flow.");
                }

                var graphView = RequireGraphView();
                var endNode = graphView.nodes.ToList().OfType<EndNodeView>().FirstOrDefault();
                if (flowDraft.connectBranchesToEnd && endNode == null)
                {
                    throw new InvalidOperationException("End node was not found in the graph.");
                }

                var undoGroup = BeginUndoGroup("Insert Dialog Flow After");
                try
                {
                    var originalNextNode = outgoingEdges
                        .Select(edge => edge.input?.node as Node)
                        .FirstOrDefault(node => node != null);

                    foreach (var edge in outgoingEdges)
                    {
                        GraphView.RemoveElement(edge);
                    }

                    Node chainStartNode = selectedNode;
                    Node chainTailNode = selectedNode;

                    if (sanitizedLinearNodes.Count > 0)
                    {
                        var linearPositions = _layoutService.GetChainBetweenPositions(graphView, selectedNode, originalNextNode, sanitizedLinearNodes.Count);
                        for (var i = 0; i < sanitizedLinearNodes.Count; i++)
                        {
                            var linearNode = CreateDialogNode(
                                sanitizedLinearNodes[i],
                                i < linearPositions.Count ? linearPositions[i] : Vector2.zero);

                            if (i == 0)
                            {
                                chainStartNode = linearNode;
                            }
                            else
                            {
                                CreateLink(chainTailNode, linearNode, 0);
                            }

                            chainTailNode = linearNode;
                        }

                        CreateLink(selectedNode, chainStartNode, fromPortIndex);
                    }

                    var choiceData = new ChoiceNodeMutationData
                    {
                        text = flowDraft.choiceText ?? string.Empty,
                        choices = sanitizedChoices
                            .Select(choice => new ChoiceMutationData
                            {
                                answerText = choice.answerText ?? string.Empty,
                                nextNodeGuid = null,
                                tooltipOrSubLabel = null
                            })
                            .ToList()
                    };

                    var choicePosition = _layoutService.GetInsertedChoiceNodePosition(graphView, chainTailNode, originalNextNode);
                    var choiceNode = CreateChoiceNode(choiceData, choicePosition);

                    if (sanitizedLinearNodes.Count == 0)
                    {
                        CreateLink(selectedNode, choiceNode, fromPortIndex);
                    }
                    else
                    {
                        CreateLink(chainTailNode, choiceNode, 0);
                    }

                    var branchStarts = _layoutService.GetBranchChildrenPositions(graphView, choiceNode, sanitizedChoices.Count);
                    for (var i = 0; i < sanitizedChoices.Count; i++)
                    {
                        var branch = sanitizedChoices[i];
                        var branchNodes = branch.branchNodes?.Where(node => node != null).ToList()
                                         ?? new List<DialogNodeMutationData>();

                        if (branchNodes.Count == 0)
                        {
                            if (flowDraft.connectBranchesToEnd)
                            {
                                throw new InvalidOperationException("Each branch must contain at least one node when the flow connects branches to End.");
                            }

                            if (originalNextNode != null)
                            {
                                CreateLink(choiceNode, originalNextNode, i);
                            }

                            continue;
                        }

                        DialogNodeView previousNode = null;
                        for (var branchNodeIndex = 0; branchNodeIndex < branchNodes.Count; branchNodeIndex++)
                        {
                            var desiredPosition = branchNodeIndex == 0
                                ? (i < branchStarts.Count ? branchStarts[i] : Vector2.zero)
                                : _layoutService.GetNextNodeAfterSelectedPosition(graphView, previousNode);

                            var branchNode = CreateDialogNode(branchNodes[branchNodeIndex], desiredPosition);
                            if (branchNodeIndex == 0)
                            {
                                CreateLink(choiceNode, branchNode, i);
                            }
                            else
                            {
                                CreateLink(previousNode, branchNode, 0);
                            }

                            previousNode = branchNode;
                        }

                        if (flowDraft.connectBranchesToEnd)
                        {
                            CreateLink(previousNode, endNode, 0);
                        }
                        else if (originalNextNode != null)
                        {
                            CreateLink(previousNode, originalNextNode, 0);
                        }
                    }

                    MarkGraphDirty();
                    RefreshGraphView();
                    return choiceNode;
                }
                finally
                {
                    EndUndoGroup(undoGroup);
                }
            }
            finally
            {
                EndMutationBatch();
            }
        }

        public void MarkGraphDirty()
        {
            _pendingGraphDirty = true;
            _pendingSaveAssets = true;
            FlushPendingEditorUpdatesIfNeeded();
        }

        public void RefreshGraphView()
        {
            _pendingGraphRefresh = true;
            _pendingOwnerRefresh = true;
            FlushPendingEditorUpdatesIfNeeded();
        }

        private DialogGraphView RequireGraphView()
        {
            return GraphView ?? throw new InvalidOperationException("Dialog graph view is not available.");
        }

        private DialogGraph LoadGraphAsset()
        {
            var graphView = RequireGraphView();
            return DialogGraphAssetPaths.LoadGraphAsset(graphView.graphId);
        }

        private DialogNode FindDialogNodeData(string guid)
        {
            return LoadGraphAsset()?.nodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
        }

        private ChoiceNode FindChoiceNodeData(string guid)
        {
            return LoadGraphAsset()?.choiceNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
        }

        private ActionNode FindActionNodeData(string guid)
        {
            return LoadGraphAsset()?.actionNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
        }

        private ConditionNode FindConditionNodeData(string guid)
        {
            return LoadGraphAsset()?.conditionNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
        }

        private static void ApplyDialogDraft(DialogNode data, DialogNodeMutationData draft, Vector2 position, string fallbackTitle)
        {
            if (data == null)
            {
                return;
            }

            data.name = "Node_" + (!string.IsNullOrWhiteSpace(draft?.title) ? draft.title.Trim() : fallbackTitle);
            data.SetPosition(position);
            data.speakerName = draft?.speakerName ?? string.Empty;
            data.questionText = draft?.questionText ?? string.Empty;
            data.displayTime = draft?.displayTime ?? 0f;
        }

        private static void ApplyChoiceDraft(ChoiceNode data, ChoiceNodeMutationData draft, Vector2 position)
        {
            if (data == null)
            {
                return;
            }

            data.name = "ChoiceNode";
            data.SetPosition(position);
            data.text = draft?.text ?? string.Empty;
            data.choices = new List<Choice>();

            var choices = draft?.choices;
            if (choices != null && choices.Count > 0)
            {
                foreach (var draftChoice in choices)
                {
                    if (draftChoice == null)
                    {
                        continue;
                    }

                    data.choices.Add(Choice.Create(
                        draftChoice.answerText ?? string.Empty,
                        draftChoice.nextNodeGuid,
                        draftChoice.tooltipOrSubLabel));
                }
            }

            if (data.choices.Count == 0)
            {
                data.choices.Add(Choice.Create("New Choice", null));
            }
        }

        private static void ApplyActionDraft(ActionNode data, ActionNodeMutationData draft, Vector2 position)
        {
            if (data == null)
            {
                return;
            }

            data.name = "ActionNode";
            data.SetPosition(position);
            data.actionId = draft?.actionId ?? string.Empty;
            data.payloadJson = draft?.payloadJson ?? string.Empty;
            data.waitForCompletion = draft != null && draft.waitForCompletion;
            data.waitSeconds = draft?.waitSeconds ?? 0f;
        }

        private static void ApplyConditionDraft(ConditionNode data, ConditionNodeMutationData draft, Vector2 position)
        {
            if (data == null)
            {
                return;
            }

            data.name = "ConditionNode";
            data.SetPosition(position);
            data.variableName = draft?.variableName ?? string.Empty;
            data.valueType = draft?.valueType ?? DialogueVariableValueType.Boolean;
            data.conditionOperator = draft?.conditionOperator ?? ConditionOperator.IsTrue;
            data.comparisonValue = draft?.comparisonValue ?? string.Empty;
            data.missingVariableResult = draft != null && draft.missingVariableResult;
        }

        private Edge FindEdge(Node fromNode, Node toNode, int fromPortIndex)
        {
            return GraphView?.edges
                .ToList()
                .OfType<Edge>()
                .FirstOrDefault(edge =>
                    edge.output?.node == fromNode &&
                    edge.input?.node == toNode &&
                    GetPortIndex(edge.output) == fromPortIndex);
        }

        private IEnumerable<Edge> FindOutgoingEdges(Node fromNode, int fromPortIndex)
        {
            return GraphView?.edges
                       .ToList()
                       .OfType<Edge>()
                       .Where(edge => edge.output?.node == fromNode && GetPortIndex(edge.output) == fromPortIndex)
                   ?? Enumerable.Empty<Edge>();
        }

        private bool RemoveLink(Node fromNode, Node toNode, int fromPortIndex)
        {
            var edge = FindEdge(fromNode, toNode, fromPortIndex);
            if (edge == null)
            {
                return false;
            }

            GraphView.RemoveElement(edge);
            return true;
        }

        private static Port GetOutputPort(Node node, int fromPortIndex)
        {
            var resolved = node switch
            {
                DialogNodeView dialogNode => dialogNode.outputPort,
                ActionNodeView actionNode => actionNode.outputPort,
                ConditionNodeView conditionNode => conditionNode.GetOutputPort(fromPortIndex),
                GraphJumpNodeView graphJumpNode => graphJumpNode.outputPort,
                OutcomeNodeView outcomeNode => outcomeNode.outputPort,
                StartNodeView startNode => startNode.outputPort,
                ChoiceNodeView choiceNode when fromPortIndex >= 0 && fromPortIndex < choiceNode.outputPorts.Count => choiceNode.outputPorts[fromPortIndex],
                _ => null
            };

            if (resolved != null)
            {
                return resolved;
            }

            return node?.outputContainer?.Children()
                .OfType<Port>()
                .FirstOrDefault();
        }

        private static Port GetInputPort(Node node)
        {
            var resolved = node switch
            {
                DialogNodeView dialogNode => dialogNode.inputPort,
                ActionNodeView actionNode => actionNode.inputPort,
                ConditionNodeView conditionNode => conditionNode.inputPort,
                GraphJumpNodeView graphJumpNode => graphJumpNode.inputPort,
                OutcomeNodeView outcomeNode => outcomeNode.inputPort,
                EndNodeView endNode => endNode.inputPort,
                ChoiceNodeView choiceNode => choiceNode.inputPort,
                _ => null
            };

            if (resolved != null)
            {
                return resolved;
            }

            return node?.inputContainer?.Children()
                .OfType<Port>()
                .FirstOrDefault();
        }

        private static int GetPortIndex(Port port)
        {
            if (port?.node is ChoiceNodeView choiceNode)
            {
                return choiceNode.GetPortIndex(port);
            }

            if (port?.node is ConditionNodeView conditionNode)
            {
                return conditionNode.GetPortIndex(port);
            }

            return 0;
        }

        private static int BeginUndoGroup(string label)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(label);
            return Undo.GetCurrentGroup();
        }

        private static void RebuildNodePortsIfSupported(Node node)
        {
            switch (node)
            {
                case BaseNodeView<DialogNode> dialogNode:
                    dialogNode.RebuildPorts();
                    break;
                case BaseNodeView<ChoiceNode> choiceNode:
                    choiceNode.RebuildPorts();
                    break;
                case BaseNodeView<ActionNode> actionNode:
                    actionNode.RebuildPorts();
                    break;
                case BaseNodeView<ConditionNode> conditionNode:
                    conditionNode.RebuildPorts();
                    break;
                case BaseNodeView<VariableMutationNode> variableMutationNode:
                    variableMutationNode.RebuildPorts();
                    break;
                case BaseNodeView<GraphJumpNode> graphJumpNode:
                    graphJumpNode.RebuildPorts();
                    break;
                case BaseNodeView<OutcomeNode> outcomeNode:
                    outcomeNode.RebuildPorts();
                    break;
            }
        }

        private static void EndUndoGroup(int group)
        {
            Undo.CollapseUndoOperations(group);
        }

        public void BeginMutationBatch()
        {
            _mutationBatchDepth++;
        }

        public void EndMutationBatch()
        {
            _mutationBatchDepth = Math.Max(0, _mutationBatchDepth - 1);
            if (_mutationBatchDepth == 0)
            {
                FlushPendingEditorUpdates();
            }
        }

        private void FlushPendingEditorUpdatesIfNeeded()
        {
            if (_mutationBatchDepth == 0)
            {
                FlushPendingEditorUpdates();
            }
        }

        private void FlushPendingEditorUpdates()
        {
            if (!_pendingGraphDirty &&
                !_pendingSaveAssets &&
                !_pendingGraphRefresh &&
                !_pendingOwnerRefresh)
            {
                return;
            }

            var graph = _pendingGraphDirty ? LoadGraphAssetSafe() : null;
            if (graph != null)
            {
                EditorUtility.SetDirty(graph);
            }

            if (_pendingSaveAssets)
            {
                AssetDatabase.SaveAssets();
            }

            if (_pendingGraphRefresh && GraphView != null)
            {
                GraphView.MarkDirtyRepaint();
                GraphView.schedule.Execute(() =>
                {
                    foreach (var node in GraphView.nodes.ToList())
                    {
                        node.MarkDirtyRepaint();
                    }
                }).ExecuteLater(16);
            }

            if (_pendingOwnerRefresh)
            {
                _owner?.RefreshGraphEditorState();
            }

            _pendingGraphDirty = false;
            _pendingSaveAssets = false;
            _pendingGraphRefresh = false;
            _pendingOwnerRefresh = false;
        }

        private DialogGraph LoadGraphAssetSafe()
        {
            return GraphView == null ? null : DialogGraphAssetPaths.LoadGraphAsset(GraphView.graphId);
        }

        private readonly struct EdgeSnapshot
        {
            public EdgeSnapshot(Node fromNode, int fromPortIndex, Node toNode)
            {
                FromNode = fromNode;
                FromPortIndex = fromPortIndex;
                ToNode = toNode;
            }

            public Node FromNode { get; }
            public int FromPortIndex { get; }
            public Node ToNode { get; }
        }
    }
}
