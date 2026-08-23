using System;
using System.Linq;
using System.Text.RegularExpressions;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Helper methods for matching graph-authored speaker/action strings against registered definition assets.
    /// </summary>
    public static class DialogGraphDefinitionResolver
    {
        public static DialogCharacterSO FindCharacterBySpeaker(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker))
            {
                return null;
            }

            var registry = new DialogCharacterRegistryService();
            var trimmed = speaker.Trim();

            return registry.GetAllRegisteredDefinitions()
                .FirstOrDefault(character =>
                    Matches(character?.CharacterID, trimmed) ||
                    Matches(character?.DisplayName, trimmed));
        }

        public static DialogActionSO FindActionById(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return null;
            }

            var registry = new DialogActionRegistryService();
            var trimmed = actionId.Trim();

            return registry.GetAllRegisteredDefinitions()
                .FirstOrDefault(action => Matches(action?.ActionID, trimmed));
        }

        public static DialogVariableSO FindVariableByKey(string variableKey)
        {
            if (string.IsNullOrWhiteSpace(variableKey))
            {
                return null;
            }

            var registry = new DialogVariableRegistryService();
            var trimmed = variableKey.Trim();

            return registry.GetAllRegisteredDefinitions()
                .FirstOrDefault(variable => Matches(variable?.Key, trimmed));
        }

        public static string CreateSuggestedCharacterId(string displayName)
        {
            return SanitizeId(displayName, "character_id");
        }

        public static string CreateSuggestedActionId(string actionIdOrName)
        {
            return SanitizeId(actionIdOrName, "action_id");
        }

        public static string CreateSuggestedVariableKey(string variableKeyOrName)
        {
            return SanitizeId(variableKeyOrName, "variable_key");
        }

        public static string CreateSuggestedEnvironmentId(string environmentName)
        {
            return SanitizeId(environmentName, "environment_id");
        }

        public static string CreateSuggestedContextId(string contextName)
        {
            return SanitizeId(contextName, "scene_context_id");
        }

        public static void PingAndSelect(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static bool Matches(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                   !string.IsNullOrWhiteSpace(right) &&
                   string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeId(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_");
            normalized = normalized.Trim('_');
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }
    }
}
