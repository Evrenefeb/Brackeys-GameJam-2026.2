using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Scans graph assets and registered definitions for action IDs used by the project.
    /// </summary>
    public sealed class DialogActionDiscoveryService
    {
        public IReadOnlyList<DialogGraph> LoadAllGraphAssets()
        {
            return DialogGraphAssetPaths.LoadAllGraphAssets();
        }

        public IReadOnlyList<string> GetAllDiscoveredActionIds()
        {
            var actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var graph in LoadAllGraphAssets())
            {
                CollectActionIds(graph, actionIds);
            }

            return actionIds
                .OrderBy(actionId => actionId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<DialogActionSO> GetRegisteredActionsUsedByGraph(DialogGraph graph)
        {
            if (graph == null)
            {
                return Array.Empty<DialogActionSO>();
            }

            var graphActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectActionIds(graph, graphActionIds);

            var registry = new DialogActionRegistryService();
            return registry.GetAllRegisteredDefinitions()
                .Where(action =>
                    action != null &&
                    !string.IsNullOrWhiteSpace(action.ActionID) &&
                    graphActionIds.Contains(action.ActionID.Trim()))
                .OrderBy(action => action.ActionID, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void CollectActionIds(DialogGraph graph, ISet<string> destination)
        {
            if (graph == null || destination == null)
            {
                return;
            }

            if (graph.actionNodes != null)
            {
                foreach (var node in graph.actionNodes)
                {
                    AddIfValid(destination, node?.actionId);
                }
            }

            if (graph.availableActions != null)
            {
                foreach (var action in graph.availableActions)
                {
                    AddIfValid(destination, action?.ActionID);
                }
            }
        }

        private static void AddIfValid(ISet<string> destination, string actionId)
        {
            if (destination == null || string.IsNullOrWhiteSpace(actionId))
            {
                return;
            }

            destination.Add(actionId.Trim());
        }
    }
}
