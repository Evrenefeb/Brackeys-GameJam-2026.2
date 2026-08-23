using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Variables;
using UnityEditor;

namespace DialogSystem.EditorTools.Services.Validation
{
    /// <summary>
    /// Editor entry point for validating the currently open graph with project registry context.
    /// </summary>
    public static class DialogGraphValidationRunner
    {
        /// <summary>
        /// Saves pending graph-view data, loads the current graph asset, and validates it.
        /// </summary>
        public static DialogGraphValidationResult ValidateOpenGraph(IDialogGraphOwner owner)
        {
            var graph = LoadCurrentGraphForValidation(owner);
            return ValidateGraphAsset(graph);
        }

        /// <summary>
        /// Validates a graph asset using registered characters, actions, variables,
        /// and locale tables.
        /// </summary>
        public static DialogGraphValidationResult ValidateGraphAsset(DialogGraph graph)
        {
            if (graph == null)
            {
                return DialogGraphValidator.Validate(null);
            }

            var hasLocaleSource = new DialogLocalizationRegistryService().GetAllTables().Count > 0;

            return DialogGraphValidator.Validate(
                graph,
                CollectKnownSpeakerIds(graph),
                CollectKnownActionIds(graph),
                CollectKnownVariables(graph),
                hasLocaleSource);
        }

        private static DialogGraph LoadCurrentGraphForValidation(IDialogGraphOwner owner)
        {
            var graphView = owner?.GetGraphView();
            if (graphView == null || string.IsNullOrWhiteSpace(graphView.graphId))
            {
                return null;
            }

            graphView.SaveGraph(graphView.graphId);
            AssetDatabase.SaveAssets();

            return owner.LoadCurrentGraphAsset(createIfMissing: false);
        }

        private static IEnumerable<string> CollectKnownSpeakerIds(DialogGraph graph)
        {
            var registry = new DialogCharacterRegistryService();
            foreach (var character in registry.GetAllRegisteredDefinitions())
            {
                foreach (var id in CharacterIds(character))
                {
                    yield return id;
                }
            }

            foreach (var character in graph?.participatingCharacters?.AsEnumerable() ?? Enumerable.Empty<DialogCharacterSO>())
            {
                foreach (var id in CharacterIds(character))
                {
                    yield return id;
                }
            }

            foreach (var character in graph?.sceneContext?.ParticipatingCharacters?.AsEnumerable() ?? Enumerable.Empty<DialogCharacterSO>())
            {
                foreach (var id in CharacterIds(character))
                {
                    yield return id;
                }
            }
        }

        private static IEnumerable<string> CollectKnownActionIds(DialogGraph graph)
        {
            var registry = new DialogActionRegistryService();
            foreach (var action in registry.GetAllRegisteredDefinitions())
            {
                if (!string.IsNullOrWhiteSpace(action?.ActionID))
                {
                    yield return action.ActionID.Trim();
                }
            }

            foreach (var action in graph?.availableActions?.AsEnumerable() ?? Enumerable.Empty<DialogActionSO>())
            {
                if (!string.IsNullOrWhiteSpace(action?.ActionID))
                {
                    yield return action.ActionID.Trim();
                }
            }

            foreach (var action in graph?.sceneContext?.AvailableActions?.AsEnumerable() ?? Enumerable.Empty<DialogActionSO>())
            {
                if (!string.IsNullOrWhiteSpace(action?.ActionID))
                {
                    yield return action.ActionID.Trim();
                }
            }
        }

        private static IEnumerable<DialogVariableSO> CollectKnownVariables(DialogGraph graph)
        {
            var registry = new DialogVariableRegistryService();
            foreach (var variable in registry.GetAllRegisteredDefinitions())
            {
                if (variable != null)
                {
                    yield return variable;
                }
            }

            foreach (var variable in graph?.availableVariables?.AsEnumerable() ?? Enumerable.Empty<DialogVariableSO>())
            {
                if (variable != null)
                {
                    yield return variable;
                }
            }
        }

        private static IEnumerable<string> CharacterIds(DialogCharacterSO character)
        {
            if (!string.IsNullOrWhiteSpace(character?.CharacterID))
            {
                yield return character.CharacterID.Trim();
            }

            if (!string.IsNullOrWhiteSpace(character?.DisplayName))
            {
                yield return character.DisplayName.Trim();
            }
        }
    }
}
