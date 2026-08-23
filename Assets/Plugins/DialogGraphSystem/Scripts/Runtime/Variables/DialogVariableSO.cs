using System;
using System.Globalization;
using UnityEngine;

namespace DialogSystem.Runtime.Variables
{
    /// <summary>
    /// Reusable dialogue variable definition with a typed default value.
    /// Add these assets to a <see cref="DialogueVariableStore"/> to seed runtime state.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogVariable", menuName = "Beka Forge/Dialogues/Variable", order = 20)]
    public sealed class DialogVariableSO : ScriptableObject
    {
        #region ---------------- Serialized State ----------------
        [SerializeField, Tooltip("Unique runtime key used by conditions, scripts, and text tokens.")]
        private string variableKey;

        [SerializeField, Tooltip("Value type stored by this variable.")]
        private DialogueVariableValueType valueType = DialogueVariableValueType.Boolean;

        [SerializeField] private bool boolDefaultValue;
        [SerializeField] private int intDefaultValue;
        [SerializeField] private float floatDefaultValue;
        [SerializeField] private string stringDefaultValue;
        #endregion

        #region ---------------- Public API ----------------
        /// <summary>
        /// Unique runtime key used by conditions, scripts, and text tokens.
        /// Falls back to the asset name when the serialized key is empty.
        /// </summary>
        public string Key
        {
            get
            {
                if (this == null) return string.Empty;
                return string.IsNullOrWhiteSpace(variableKey) ? name : variableKey.Trim();
            }
        }

        /// <summary>
        /// Value type stored by this variable.
        /// </summary>
        public DialogueVariableValueType ValueType => valueType;

        /// <summary>
        /// Default bool value used when <see cref="ValueType"/> is Boolean.
        /// </summary>
        public bool BoolDefaultValue => boolDefaultValue;

        /// <summary>
        /// Default int value used when <see cref="ValueType"/> is Integer.
        /// </summary>
        public int IntDefaultValue => intDefaultValue;

        /// <summary>
        /// Default float value used when <see cref="ValueType"/> is Float.
        /// </summary>
        public float FloatDefaultValue => floatDefaultValue;

        /// <summary>
        /// Default string value used when <see cref="ValueType"/> is String.
        /// </summary>
        public string StringDefaultValue => stringDefaultValue ?? string.Empty;

        /// <summary>
        /// Applies this definition's default value to the provided runtime store.
        /// </summary>
        public bool ApplyDefaultTo(DialogueVariableStore store)
        {
            if (store == null || string.IsNullOrWhiteSpace(Key))
            {
                return false;
            }

            switch (valueType)
            {
                case DialogueVariableValueType.Boolean:
                    store.SetBool(Key, boolDefaultValue);
                    return true;
                case DialogueVariableValueType.Integer:
                    store.SetInt(Key, intDefaultValue);
                    return true;
                case DialogueVariableValueType.Float:
                    store.SetFloat(Key, floatDefaultValue);
                    return true;
                case DialogueVariableValueType.String:
                    store.SetString(Key, stringDefaultValue ?? string.Empty);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the default value as a culture-invariant string for debugging and editor UI.
        /// </summary>
        public string GetDefaultValueAsString()
        {
            return valueType switch
            {
                DialogueVariableValueType.Boolean => boolDefaultValue ? "true" : "false",
                DialogueVariableValueType.Integer => intDefaultValue.ToString(CultureInfo.InvariantCulture),
                DialogueVariableValueType.Float => floatDefaultValue.ToString(CultureInfo.InvariantCulture),
                DialogueVariableValueType.String => stringDefaultValue ?? string.Empty,
                _ => string.Empty
            };
        }

        /// <summary>
        /// Sets the definition key. Editor and setup tools can use this when creating variables programmatically.
        /// </summary>
        public void SetKey(string key)
        {
            variableKey = key?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Sets the default bool value and changes this definition to Boolean.
        /// </summary>
        public void SetDefaultValue(bool value)
        {
            valueType = DialogueVariableValueType.Boolean;
            boolDefaultValue = value;
        }

        /// <summary>
        /// Sets the default int value and changes this definition to Integer.
        /// </summary>
        public void SetDefaultValue(int value)
        {
            valueType = DialogueVariableValueType.Integer;
            intDefaultValue = value;
        }

        /// <summary>
        /// Sets the default float value and changes this definition to Float.
        /// </summary>
        public void SetDefaultValue(float value)
        {
            valueType = DialogueVariableValueType.Float;
            floatDefaultValue = value;
        }

        /// <summary>
        /// Sets the default string value and changes this definition to String.
        /// </summary>
        public void SetDefaultValue(string value)
        {
            valueType = DialogueVariableValueType.String;
            stringDefaultValue = value ?? string.Empty;
        }
        #endregion

        #region ---------------- Validation ----------------
        private void OnValidate()
        {
            variableKey = variableKey?.Trim();
        }
        #endregion
    }
}
