using System;
using System.Globalization;
using DialogSystem.Runtime.Variables;
using UnityEngine;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Operators available to condition nodes.
    /// </summary>
    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        Greater,
        Less,
        Contains,
        IsTrue,
        IsFalse
    }

    /// <summary>
    /// Runtime flow node that chooses its true or false output based on a dialogue variable.
    /// </summary>
    public class ConditionNode : BaseNode
    {
        /// <summary>Output port index used when the condition evaluates true.</summary>
        public const int TruePortIndex = 0;

        /// <summary>Output port index used when the condition evaluates false.</summary>
        public const int FalsePortIndex = 1;

        #region ---------------- Condition ----------------
        [Header("Condition")]
        [Tooltip("Variable key to read from the DialogueVariableStore.")]
        public string variableName;

        [Tooltip("Expected variable type.")]
        public DialogueVariableValueType valueType = DialogueVariableValueType.Boolean;

        [Tooltip("Comparison operator used to evaluate the variable.")]
        public ConditionOperator conditionOperator = ConditionOperator.IsTrue;

        [Tooltip("Comparison literal. Not used by Is True or Is False.")]
        public string comparisonValue;

        [Tooltip("Result used when the variable is missing or cannot be evaluated.")]
        public bool missingVariableResult = false;
        #endregion

        /// <summary>
        /// Creates a condition node.
        /// </summary>
        public ConditionNode()
        {
            nodeKind = NodeKind.Condition;
        }

        /// <summary>
        /// Evaluates the condition against a runtime variable store.
        /// </summary>
        public bool Evaluate(DialogueVariableStore variableStore)
        {
            if (variableStore == null || string.IsNullOrWhiteSpace(variableName))
            {
                return missingVariableResult;
            }

            return valueType switch
            {
                DialogueVariableValueType.Boolean => EvaluateBool(variableStore),
                DialogueVariableValueType.Integer => EvaluateInt(variableStore),
                DialogueVariableValueType.Float => EvaluateFloat(variableStore),
                DialogueVariableValueType.String => EvaluateString(variableStore),
                _ => missingVariableResult
            };
        }

        private bool EvaluateBool(DialogueVariableStore variableStore)
        {
            if (!variableStore.TryGetBool(variableName, out var value))
            {
                return missingVariableResult;
            }

            return conditionOperator switch
            {
                ConditionOperator.IsTrue => value,
                ConditionOperator.IsFalse => !value,
                ConditionOperator.Equals => TryParseBool(comparisonValue, out var expected) && value == expected,
                ConditionOperator.NotEquals => TryParseBool(comparisonValue, out var expected) && value != expected,
                _ => false
            };
        }

        private bool EvaluateInt(DialogueVariableStore variableStore)
        {
            if (!variableStore.TryGetInt(variableName, out var value) ||
                !int.TryParse(comparisonValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expected))
            {
                return missingVariableResult;
            }

            return conditionOperator switch
            {
                ConditionOperator.Equals => value == expected,
                ConditionOperator.NotEquals => value != expected,
                ConditionOperator.Greater => value > expected,
                ConditionOperator.Less => value < expected,
                _ => false
            };
        }

        private bool EvaluateFloat(DialogueVariableStore variableStore)
        {
            if (!variableStore.TryGetFloat(variableName, out var value) ||
                !float.TryParse(comparisonValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var expected))
            {
                return missingVariableResult;
            }

            return conditionOperator switch
            {
                ConditionOperator.Equals => Mathf.Approximately(value, expected),
                ConditionOperator.NotEquals => !Mathf.Approximately(value, expected),
                ConditionOperator.Greater => value > expected,
                ConditionOperator.Less => value < expected,
                _ => false
            };
        }

        private bool EvaluateString(DialogueVariableStore variableStore)
        {
            if (!variableStore.TryGetString(variableName, out var value))
            {
                return missingVariableResult;
            }

            var expected = comparisonValue ?? string.Empty;
            return conditionOperator switch
            {
                ConditionOperator.Equals => string.Equals(value, expected, StringComparison.Ordinal),
                ConditionOperator.NotEquals => !string.Equals(value, expected, StringComparison.Ordinal),
                ConditionOperator.Contains => value?.IndexOf(expected, StringComparison.Ordinal) >= 0,
                _ => false
            };
        }

        private static bool TryParseBool(string rawValue, out bool value)
        {
            if (bool.TryParse(rawValue, out value))
            {
                return true;
            }

            if (string.Equals(rawValue, "1", StringComparison.Ordinal))
            {
                value = true;
                return true;
            }

            if (string.Equals(rawValue, "0", StringComparison.Ordinal))
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }
    }
}
