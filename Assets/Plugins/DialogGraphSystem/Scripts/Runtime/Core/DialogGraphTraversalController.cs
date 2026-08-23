using System;
using System.Linq;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Provides runtime graph lookup and next-node decisions for DialogManager.
    /// This class does not mutate graph data or trigger UI/runtime side effects.
    /// </summary>
    public sealed class DialogGraphTraversalController
    {
        private DialogGraph graph;

        public void Bind(DialogGraph dialogGraph)
        {
            graph = dialogGraph;
        }

        public void Clear()
        {
            graph = null;
        }

        public bool IsEndGuid(string guid)
        {
            return DialogGraphFlowUtility.IsEndGuid(graph, guid);
        }

        public string ResolveEntryGuid()
        {
            return DialogGraphFlowUtility.ResolveEntryGuid(graph);
        }

        public bool TryResolveVisibleNode(string guid, out DialogNode dialogNode, out ChoiceNode choiceNode)
        {
            dialogNode = FindDialogByGuid(guid);
            choiceNode = dialogNode == null ? FindChoiceByGuid(guid) : null;
            return dialogNode != null || choiceNode != null;
        }

        public bool IsHiddenFlowNode(string guid)
        {
            return FindActionByGuid(guid) != null ||
                   FindVariableMutationByGuid(guid) != null ||
                   FindConditionByGuid(guid) != null ||
                   FindGraphJumpByGuid(guid) != null ||
                   FindOutcomeByGuid(guid) != null;
        }

        public bool TryResolveHiddenFlowNode(
            string guid,
            out ActionNode actionNode,
            out VariableMutationNode variableMutationNode,
            out ConditionNode conditionNode,
            out GraphJumpNode graphJumpNode,
            out OutcomeNode outcomeNode)
        {
            actionNode = FindActionByGuid(guid);
            variableMutationNode = actionNode == null ? FindVariableMutationByGuid(guid) : null;
            conditionNode = actionNode == null && variableMutationNode == null ? FindConditionByGuid(guid) : null;
            graphJumpNode = actionNode == null && variableMutationNode == null && conditionNode == null
                ? FindGraphJumpByGuid(guid)
                : null;
            outcomeNode = actionNode == null && variableMutationNode == null && conditionNode == null && graphJumpNode == null
                ? FindOutcomeByGuid(guid)
                : null;
            return actionNode != null || variableMutationNode != null || conditionNode != null || graphJumpNode != null || outcomeNode != null;
        }

        public int GetHiddenFlowTraversalLimit()
        {
            return Math.Max(1,
                (graph?.actionNodes?.Count ?? 0) +
                (graph?.conditionNodes?.Count ?? 0) +
                (graph?.variableMutationNodes?.Count ?? 0) +
                (graph?.graphJumpNodes?.Count ?? 0) +
                (graph?.outcomeNodes?.Count ?? 0) + 1);
        }

        public DialogNode FindDialogByGuid(string guid)
        {
            if (graph?.nodes == null)
            {
                return null;
            }

            return graph.nodes.FirstOrDefault(node => NodeGuidEquals(node, guid));
        }

        public ChoiceNode FindChoiceByGuid(string guid)
        {
            if (graph?.choiceNodes == null)
            {
                return null;
            }

            return graph.choiceNodes.FirstOrDefault(node => NodeGuidEquals(node, guid));
        }

        public ActionNode FindActionByGuid(string guid)
        {
            if (graph?.actionNodes == null)
            {
                return null;
            }

            return graph.actionNodes.FirstOrDefault(node => NodeGuidEquals(node, guid));
        }

        public ConditionNode FindConditionByGuid(string guid)
        {
            if (graph?.conditionNodes == null)
            {
                return null;
            }

            return graph.conditionNodes.FirstOrDefault(node => NodeGuidEquals(node, guid));
        }

        public VariableMutationNode FindVariableMutationByGuid(string guid)
        {
            if (graph?.variableMutationNodes == null)
            {
                return null;
            }

            return graph.variableMutationNodes.FirstOrDefault(node => NodeGuidEquals(node, guid));
        }

        public GraphJumpNode FindGraphJumpByGuid(string guid)
        {
            if (graph?.graphJumpNodes == null)
            {
                return null;
            }

            return graph.graphJumpNodes.FirstOrDefault(node => NodeGuidEquals(node, guid));
        }

        public OutcomeNode FindOutcomeByGuid(string guid)
        {
            if (graph?.outcomeNodes == null)
            {
                return null;
            }

            return graph.outcomeNodes.FirstOrDefault(node => NodeGuidEquals(node, guid));
        }

        public OutcomeNode FindOutcomeById(string outcomeId)
        {
            return graph?.FindOutcomeNodeById(outcomeId);
        }

        public string GetNextFromDialog(string guid)
        {
            return DialogGraphFlowUtility.GetNextFromPort(graph, guid, DialogGraphPortKeys.Default, 0);
        }

        public string GetNextFromChoice(string guid, int choiceIndex)
        {
            var choiceNode = FindChoiceByGuid(guid);
            var portKey = choiceNode?.choices != null &&
                          choiceIndex >= 0 &&
                          choiceIndex < choiceNode.choices.Count
                ? choiceNode.choices[choiceIndex]?.PortKey
                : null;

            return DialogGraphFlowUtility.GetNextFromPort(graph, guid, portKey, choiceIndex);
        }

        public string GetNextFromAction(string guid)
        {
            return DialogGraphFlowUtility.GetNextFromPort(graph, guid, DialogGraphPortKeys.ActionSuccess, 0);
        }

        public string GetNextFromVariableMutation(string guid)
        {
            return DialogGraphFlowUtility.GetNextFromPort(graph, guid, DialogGraphPortKeys.Default, 0);
        }

        public string GetNextFromCondition(string guid, bool result)
        {
            var portIndex = result ? ConditionNode.TruePortIndex : ConditionNode.FalsePortIndex;
            var portKey = result ? DialogGraphPortKeys.True : DialogGraphPortKeys.False;
            return DialogGraphFlowUtility.GetNextFromPort(graph, guid, portKey, portIndex);
        }

        public string GetNextFromGraphJump(string guid)
        {
            return DialogGraphFlowUtility.GetNextFromPort(graph, guid, DialogGraphPortKeys.Default, 0);
        }

        public string GetNextFromOutcome(string guid)
        {
            return DialogGraphFlowUtility.GetNextFromPort(graph, guid, DialogGraphPortKeys.Default, 0);
        }

        private static bool NodeGuidEquals(BaseNode node, string guid)
        {
            return node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal);
        }
    }
}
