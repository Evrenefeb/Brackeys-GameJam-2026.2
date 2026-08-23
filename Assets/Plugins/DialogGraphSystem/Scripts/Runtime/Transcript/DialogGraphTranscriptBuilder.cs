using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DialogSystem.Runtime.Transcript
{
    public enum DialogGraphTranscriptBuildMode
    {
        SpecificBranchPath = 0
    }

    public struct BranchChoiceStep
    {
        public string choiceNodeGuid;
        public int selectedIndex;

        public BranchChoiceStep(string choiceNodeGuid, int selectedIndex)
        {
            this.choiceNodeGuid = choiceNodeGuid;
            this.selectedIndex = selectedIndex;
        }
    }

    /// <summary>
    /// Reconstructs a deterministic transcript directly from a dialog graph and branch path.
    /// </summary>
    public static class DialogGraphTranscriptBuilder
    {
        public static bool TryBuildTranscript(
            DialogGraph graph,
            DialogGraphTranscriptBuildMode mode,
            IReadOnlyList<int> branchPath,
            out string transcript,
            out string error)
        {
            transcript = string.Empty;
            error = null;

            if (graph == null)
            {
                error = "Graph is null.";
                return false;
            }

            if (mode != DialogGraphTranscriptBuildMode.SpecificBranchPath)
            {
                error = $"Unsupported transcript build mode: {mode}.";
                return false;
            }

            var entryGuid = ResolveEntryGuid(graph);
            if (string.IsNullOrEmpty(entryGuid))
            {
                error = $"Graph '{graph.name}' does not have a playable entry node.";
                return false;
            }

            var sb = new StringBuilder(256);
            var branchCursor = 0;
            var stepBudget = CalculateStepBudget(graph);
            var currentGuid = entryGuid;

            while (!string.IsNullOrEmpty(currentGuid) && stepBudget-- > 0)
            {
                var dialogNode = FindDialogByGuid(graph, currentGuid);
                if (dialogNode != null)
                {
                    AppendDialogLine(sb, dialogNode);
                    currentGuid = GetNextFromPort(graph, currentGuid, DialogGraphPortKeys.Default, 0);
                    continue;
                }

                var choiceNode = FindChoiceByGuid(graph, currentGuid);
                if (choiceNode != null)
                {
                    if (branchCursor >= (branchPath?.Count ?? 0))
                    {
                        error = $"Missing branch index for choice node '{choiceNode.GetGuid()}'.";
                        return false;
                    }

                    var selectedIndex = branchPath[branchCursor++];
                    if (selectedIndex < 0 || selectedIndex >= choiceNode.choices.Count)
                    {
                        error = $"Invalid branch index {selectedIndex} for choice node '{choiceNode.GetGuid()}'.";
                        return false;
                    }

                    var selectedChoice = choiceNode.choices[selectedIndex];
                    AppendChoiceLine(sb, selectedChoice);

                    currentGuid = GetNextChoiceTarget(graph, choiceNode, selectedIndex);
                    continue;
                }

                var actionNode = FindActionByGuid(graph, currentGuid);
                if (actionNode != null)
                {
                    currentGuid = GetNextFromPort(graph, currentGuid, DialogGraphPortKeys.ActionSuccess, 0);
                    continue;
                }

                // Treat known terminal markers or dead-ends as valid termination
                if (IsTerminalGuid(graph, currentGuid))
                {
                    break; // valid end of transcript
                }

                error = $"Unknown node guid '{currentGuid}' while rebuilding transcript.";
                return false;
            }

            if (stepBudget <= 0)
            {
                error = $"Loop protection triggered while rebuilding transcript for graph '{graph.name}'.";
                return false;
            }

            transcript = sb.ToString().TrimEnd();
            return true;
        }

        private static bool IsTerminalGuid(DialogGraph graph, string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return true;

            // Common explicit terminal markers
            if (DialogGraphFlowUtility.IsEndGuid(graph, guid))
                return true;

            // If it is not a node AND has no outgoing links → dead end → valid termination
            if (!DialogGraphFlowUtility.ContainsRuntimeNode(graph, guid))
            {
                bool hasOutgoing = graph?.links?.Any(l => l != null && l.fromGuid == guid) ?? false;
                if (!hasOutgoing)
                    return true;
            }

            return false;
        }

        private static void AppendDialogLine(StringBuilder sb, DialogNode node)
        {
            if (sb.Length > 0)
                sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(node.speakerName))
                sb.Append(node.speakerName).Append(": ");

            sb.Append(node.questionText ?? string.Empty);
        }

        private static void AppendChoiceLine(StringBuilder sb, Choice choice)
        {
            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append("Your Choice: ");
            sb.Append(choice?.answerText ?? string.Empty);
        }

        private static string ResolveEntryGuid(DialogGraph graph)
        {
            return DialogGraphFlowUtility.ResolveEntryGuid(graph);
        }

        private static string GetNextChoiceTarget(DialogGraph graph, ChoiceNode node, int choiceIndex)
        {
            var portKey = choiceIndex >= 0 && choiceIndex < node.choices.Count
                ? node.choices[choiceIndex]?.PortKey
                : null;
            var linkedTarget = GetNextFromPort(graph, node.GetGuid(), portKey, choiceIndex);
            if (!string.IsNullOrEmpty(linkedTarget))
                return linkedTarget;

            if (choiceIndex < 0 || choiceIndex >= node.choices.Count)
                return null;

            return node.choices[choiceIndex]?.nextNodeGUID;
        }

        private static string GetNextFromPort(DialogGraph graph, string fromGuid, string portKey, int portIndex)
        {
            return DialogGraphFlowUtility.GetNextFromPort(graph, fromGuid, portKey, portIndex);
        }

        private static DialogNode FindDialogByGuid(DialogGraph graph, string guid)
        {
            return graph?.nodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
        }

        private static ChoiceNode FindChoiceByGuid(DialogGraph graph, string guid)
        {
            return graph?.choiceNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
        }

        private static ActionNode FindActionByGuid(DialogGraph graph, string guid)
        {
            return graph?.actionNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
        }

        private static int CalculateStepBudget(DialogGraph graph)
        {
            var totalNodes =
                (graph?.nodes?.Count ?? 0) +
                (graph?.choiceNodes?.Count ?? 0) +
                (graph?.actionNodes?.Count ?? 0) +
                (graph?.conditionNodes?.Count ?? 0) +
                (graph?.variableMutationNodes?.Count ?? 0) +
                (graph?.graphJumpNodes?.Count ?? 0);

            return Mathf.Max(16, (totalNodes * 4) + 8);
        }
    }
}