using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Resolves choice selection data independently of UI button creation.
    /// </summary>
    internal sealed class DialogChoiceResolver
    {
        private readonly DialogGraphTraversalController traversalController;

        public DialogChoiceResolver(DialogGraphTraversalController traversalController)
        {
            this.traversalController = traversalController;
        }

        public bool TryResolveChoice(ChoiceNode choiceNode, int choiceIndex, out Choice choice)
        {
            choice = null;

            if (choiceNode?.choices == null ||
                choiceIndex < 0 ||
                choiceIndex >= choiceNode.choices.Count)
            {
                return false;
            }

            choice = choiceNode.choices[choiceIndex];
            return choice != null;
        }

        public string ResolveTargetGuid(ChoiceNode choiceNode, int choiceIndex)
        {
            if (!TryResolveChoice(choiceNode, choiceIndex, out var choice))
            {
                return null;
            }

            var linkedTarget = traversalController.GetNextFromChoice(choiceNode.GetGuid(), choiceIndex);
            return !string.IsNullOrWhiteSpace(linkedTarget)
                ? linkedTarget
                : choice.nextNodeGUID;
        }

        public bool HasValidTarget(DialogGraph graph, string targetGuid)
        {
            return !string.IsNullOrWhiteSpace(targetGuid) &&
                   (DialogGraphFlowUtility.IsEndGuid(graph, targetGuid) ||
                    DialogGraphFlowUtility.ContainsRuntimeNode(graph, targetGuid));
        }
    }
}
