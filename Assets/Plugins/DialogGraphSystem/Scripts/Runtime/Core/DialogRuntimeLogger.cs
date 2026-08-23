using System.Collections.Generic;
using DialogSystem.Runtime.Models;
using UnityEngine;

namespace DialogSystem.Runtime.Core
{
    public enum DialogRuntimeLogMode
    {
        Off = 0,
        WarningsOnly = 1,
        Important = 2,
        Verbose = 3
    }

    /// <summary>
    /// Centralizes runtime diagnostics and prevents repeated warnings from spamming users.
    /// </summary>
    internal sealed class DialogRuntimeLogger
    {
        private readonly HashSet<string> emittedKeys = new HashSet<string>();
        private DialogRuntimeLogMode mode = DialogRuntimeLogMode.WarningsOnly;

        public DialogRuntimeLogMode Mode => mode;
        public bool IsVerbose => mode >= DialogRuntimeLogMode.Verbose;

        public void SetMode(DialogRuntimeLogMode logMode)
        {
            mode = logMode;
        }

        public void ResetSession()
        {
            emittedKeys.Clear();
        }

        public void WarnOnce(string key, string message)
        {
            if (mode == DialogRuntimeLogMode.Off)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                key = message ?? string.Empty;
            }

            if (!emittedKeys.Add(key))
            {
                return;
            }

            Debug.LogWarning(message);
        }

        public void ErrorOnce(string key, string message)
        {
            if (mode == DialogRuntimeLogMode.Off)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                key = message ?? string.Empty;
            }

            if (!emittedKeys.Add(key))
            {
                return;
            }

            Debug.LogError(message);
        }

        public void Important(string message)
        {
            if (mode >= DialogRuntimeLogMode.Important)
            {
                Debug.Log(message);
            }
        }

        public void Verbose(string message)
        {
            if (mode >= DialogRuntimeLogMode.Verbose)
            {
                Debug.Log(message);
            }
        }

        public void VerboseWarning(string key, string message)
        {
            if (mode >= DialogRuntimeLogMode.Verbose)
            {
                WarnOnce(key, message);
            }
        }

        public void HiddenFlowCycle(DialogGraph graph, string guid)
        {
            WarnOnce(
                $"hidden-flow-cycle:{GetGraphKey(graph)}:{guid}",
                $"[DialogManager] Hidden-flow cycle detected at node '{guid ?? "<null>"}' in graph '{GetGraphName(graph)}'. Dialogue will end gracefully to avoid an infinite loop.");
        }

        public void HiddenFlowLimitExceeded(DialogGraph graph, int maxTraversalSteps)
        {
            WarnOnce(
                $"hidden-flow-limit:{GetGraphKey(graph)}:{maxTraversalSteps}",
                $"[DialogManager] Hidden-flow traversal exceeded the safe limit ({maxTraversalSteps}) in graph '{GetGraphName(graph)}'. Dialogue will end gracefully to avoid an infinite loop.");
        }

        public void MissingBranchTarget(DialogGraph graph, string nodeGuid, string branchName)
        {
            WarnOnce(
                $"missing-branch:{GetGraphKey(graph)}:{nodeGuid}:{branchName}",
                $"[DialogManager] Condition node '{nodeGuid ?? "<null>"}' selected the {branchName} branch, but that branch has no target. Dialogue will end gracefully.");
        }

        public void InvalidRuntimeTarget(DialogGraph graph, string guid)
        {
            WarnOnce(
                $"invalid-target:{GetGraphKey(graph)}:{guid}",
                $"[DialogManager] Runtime target '{guid ?? "<null>"}' does not exist in graph '{GetGraphName(graph)}'. Dialogue will end gracefully.");
        }

        public void MissingChoiceTarget(DialogGraph graph, string choiceNodeGuid, int choiceIndex)
        {
            WarnOnce(
                $"missing-choice-target:{GetGraphKey(graph)}:{choiceNodeGuid}:{choiceIndex}",
                $"[DialogManager] Choice index {choiceIndex} on node '{choiceNodeGuid ?? "<null>"}' in graph '{GetGraphName(graph)}' has no valid target. Dialogue will end gracefully.");
        }

        public void InvalidStateTransition(string message)
        {
            WarnOnce($"state-transition:{message}", $"[DialogManager] {message}");
        }

        private static string GetGraphKey(DialogGraph graph)
        {
            if (graph == null)
            {
                return "<null>";
            }

            return !string.IsNullOrWhiteSpace(graph.GraphGuid)
                ? graph.GraphGuid
                : graph.GetEntityId().ToString();
        }

        private static string GetGraphName(DialogGraph graph)
        {
            return graph != null ? graph.name : "<null>";
        }
    }
}
