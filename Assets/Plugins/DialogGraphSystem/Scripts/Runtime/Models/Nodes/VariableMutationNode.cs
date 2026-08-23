using System.Globalization;
using DialogSystem.Runtime.Variables;
using UnityEngine;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Operation applied by a variable mutation node.
    /// </summary>
    public enum VariableMutationOperation
    {
        Set,
        Add,
        Subtract,
        Toggle,
        ClearString
    }

    /// <summary>
    /// Hidden runtime flow node that modifies a value in <see cref="DialogueVariableStore"/>.
    /// </summary>
    public class VariableMutationNode : BaseNode
    {
        #region ---------------- Variable Mutation ----------------
        [Header("Variable")]
        [Tooltip("Runtime variable key to modify.")]
        public string variableName;

        [Tooltip("Expected variable type.")]
        public DialogueVariableValueType valueType = DialogueVariableValueType.Boolean;

        [Tooltip("Mutation operation applied when this node is reached.")]
        public VariableMutationOperation operation = VariableMutationOperation.Set;

        [Tooltip("Operation value. Not used by Toggle or Clear String.")]
        public string value;
        #endregion

        /// <summary>
        /// Creates a variable mutation node.
        /// </summary>
        public VariableMutationNode()
        {
            nodeKind = NodeKind.VariableMutation;
        }

        /// <summary>
        /// Applies this node's mutation to a runtime variable store.
        /// </summary>
        public bool Apply(DialogueVariableStore variableStore, out string error)
        {
            error = string.Empty;
            if (variableStore == null)
            {
                error = "No DialogueVariableStore is available.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(variableName))
            {
                error = "Variable name is empty.";
                return false;
            }

            var key = variableName.Trim();
            switch (operation)
            {
                case VariableMutationOperation.Set:
                    return ApplySet(variableStore, key, out error);
                case VariableMutationOperation.Add:
                    return ApplyNumericDelta(variableStore, key, positive: true, out error);
                case VariableMutationOperation.Subtract:
                    return ApplyNumericDelta(variableStore, key, positive: false, out error);
                case VariableMutationOperation.Toggle:
                    return ApplyToggle(variableStore, key, out error);
                case VariableMutationOperation.ClearString:
                    return ApplyClearString(variableStore, key, out error);
                default:
                    error = $"Unsupported variable operation '{operation}'.";
                    return false;
            }
        }

        private bool ApplySet(DialogueVariableStore variableStore, string key, out string error)
        {
            error = string.Empty;
            if (variableStore.SetValue(key, valueType, value))
            {
                return true;
            }

            error = $"Could not parse '{value}' as {valueType}.";
            return false;
        }

        private bool ApplyNumericDelta(DialogueVariableStore variableStore, string key, bool positive, out string error)
        {
            error = string.Empty;
            switch (valueType)
            {
                case DialogueVariableValueType.Integer:
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intDelta))
                    {
                        error = $"Could not parse '{value}' as an integer.";
                        return false;
                    }

                    variableStore.SetInt(key, variableStore.GetInt(key) + (positive ? intDelta : -intDelta));
                    return true;
                case DialogueVariableValueType.Float:
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatDelta))
                    {
                        error = $"Could not parse '{value}' as a float.";
                        return false;
                    }

                    variableStore.SetFloat(key, variableStore.GetFloat(key) + (positive ? floatDelta : -floatDelta));
                    return true;
                default:
                    error = "Add and Subtract only support Integer and Float variables.";
                    return false;
            }
        }

        private bool ApplyToggle(DialogueVariableStore variableStore, string key, out string error)
        {
            error = string.Empty;
            if (valueType != DialogueVariableValueType.Boolean)
            {
                error = "Toggle only supports Boolean variables.";
                return false;
            }

            variableStore.SetBool(key, !variableStore.GetBool(key));
            return true;
        }

        private bool ApplyClearString(DialogueVariableStore variableStore, string key, out string error)
        {
            error = string.Empty;
            if (valueType != DialogueVariableValueType.String)
            {
                error = "Clear String only supports String variables.";
                return false;
            }

            variableStore.SetString(key, string.Empty);
            return true;
        }
    }
}
