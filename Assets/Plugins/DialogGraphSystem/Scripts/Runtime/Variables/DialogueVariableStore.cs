using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DialogSystem.Runtime.Variables
{
    /// <summary>
    /// Value types supported by dialogue variables and condition nodes.
    /// </summary>
    public enum DialogueVariableValueType
    {
        Boolean,
        Integer,
        Float,
        String
    }

    /// <summary>
    /// Scene-level runtime key-value store for dialogue variables.
    /// Supports bool, int, float, and string values for condition node evaluation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueVariableStore : MonoBehaviour
    {
        #region ---------------- Serialized Defaults ----------------
        [SerializeField, Tooltip("Optional variable definition assets used to seed this store on Awake and ResetToInitialVariables.")]
        private List<DialogVariableSO> variableDefinitions = new();

        [Tooltip("Optional inline startup defaults. These are applied after definition assets and can override them.")]
        [SerializeField] private List<DialogueVariableEntry> initialVariables = new();
        #endregion

        #region ---------------- Runtime State ----------------
        private readonly Dictionary<string, VariableValue> _values = new(StringComparer.Ordinal);
        #endregion

        #region ---------------- Events ----------------
        /// <summary>
        /// Raised after a runtime variable is added or changed.
        /// </summary>
        public event Action<string, DialogueVariableValueType> VariableChanged;
        #endregion

        #region ---------------- Unity ----------------
        private void Awake()
        {
            ResetToInitialVariables();
        }
        #endregion

        #region ---------------- Public API ----------------
        /// <summary>
        /// Clears runtime values and reloads the serialized initial variables.
        /// </summary>
        public void ResetToInitialVariables()
        {
            _values.Clear();

            foreach (var definition in variableDefinitions)
            {
                if (definition == null)
                {
                    continue;
                }

                definition.ApplyDefaultTo(this);
            }

            foreach (var entry in initialVariables)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                {
                    continue;
                }

                var key = entry.key.Trim();
                switch (entry.type)
                {
                    case DialogueVariableValueType.Boolean:
                        SetBool(key, entry.boolValue);
                        break;
                    case DialogueVariableValueType.Integer:
                        SetInt(key, entry.intValue);
                        break;
                    case DialogueVariableValueType.Float:
                        SetFloat(key, entry.floatValue);
                        break;
                    case DialogueVariableValueType.String:
                        SetString(key, entry.stringValue);
                        break;
                }
            }
        }

        /// <summary>
        /// Returns true if the store contains a variable with the given key.
        /// </summary>
        public bool Contains(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _values.ContainsKey(key.Trim());
        }

        /// <summary>
        /// Removes all runtime variable values from this store.
        /// </summary>
        public void Clear()
        {
            _values.Clear();
        }

        /// <summary>
        /// Removes a runtime variable by key.
        /// </summary>
        public bool Remove(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _values.Remove(key.Trim());
        }

        /// <summary>
        /// Applies the default value from a variable definition asset.
        /// </summary>
        public bool SetFromDefinition(DialogVariableSO definition)
        {
            return definition != null && definition.ApplyDefaultTo(this);
        }

        /// <summary>
        /// Tries to read the stored type for a variable.
        /// </summary>
        public bool TryGetType(string key, out DialogueVariableValueType type)
        {
            type = default;
            if (!TryGetNormalizedValue(key, out var stored))
            {
                return false;
            }

            type = stored.Type;
            return true;
        }

        /// <summary>
        /// Stores a typed value parsed from a culture-invariant string.
        /// </summary>
        public bool SetValue(string key, DialogueVariableValueType type, string rawValue)
        {
            switch (type)
            {
                case DialogueVariableValueType.Boolean:
                    if (!TryParseBool(rawValue, out var boolValue))
                    {
                        return false;
                    }

                    SetBool(key, boolValue);
                    return true;
                case DialogueVariableValueType.Integer:
                    if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    {
                        return false;
                    }

                    SetInt(key, intValue);
                    return true;
                case DialogueVariableValueType.Float:
                    if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                    {
                        return false;
                    }

                    SetFloat(key, floatValue);
                    return true;
                case DialogueVariableValueType.String:
                    SetString(key, rawValue ?? string.Empty);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Stores a bool variable.
        /// </summary>
        public void SetBool(string key, bool value)
        {
            SetValue(key, VariableValue.FromBool(value));
        }

        /// <summary>
        /// Stores an int variable.
        /// </summary>
        public void SetInt(string key, int value)
        {
            SetValue(key, VariableValue.FromInt(value));
        }

        /// <summary>
        /// Stores a float variable.
        /// </summary>
        public void SetFloat(string key, float value)
        {
            SetValue(key, VariableValue.FromFloat(value));
        }

        /// <summary>
        /// Stores a string variable.
        /// </summary>
        public void SetString(string key, string value)
        {
            SetValue(key, VariableValue.FromString(value ?? string.Empty));
        }

        /// <summary>
        /// Tries to read a bool variable.
        /// </summary>
        public bool TryGetBool(string key, out bool value)
        {
            value = default;
            return TryGetValue(key, DialogueVariableValueType.Boolean, out var stored) && stored.TryGetBool(out value);
        }

        /// <summary>
        /// Tries to read an int variable.
        /// </summary>
        public bool TryGetInt(string key, out int value)
        {
            value = default;
            return TryGetValue(key, DialogueVariableValueType.Integer, out var stored) && stored.TryGetInt(out value);
        }

        /// <summary>
        /// Tries to read a float variable. Integer variables are accepted and converted to float.
        /// </summary>
        public bool TryGetFloat(string key, out float value)
        {
            value = default;
            if (!TryGetNormalizedValue(key, out var stored))
            {
                return false;
            }

            return stored.TryGetFloat(out value);
        }

        /// <summary>
        /// Tries to read a string variable.
        /// </summary>
        public bool TryGetString(string key, out string value)
        {
            value = default;
            return TryGetValue(key, DialogueVariableValueType.String, out var stored) && stored.TryGetString(out value);
        }

        /// <summary>
        /// Reads a bool variable, returning <paramref name="fallback"/> if the key is missing or has another type.
        /// </summary>
        public bool GetBool(string key, bool fallback = false)
        {
            return TryGetBool(key, out var value) ? value : fallback;
        }

        /// <summary>
        /// Reads an int variable, returning <paramref name="fallback"/> if the key is missing or has another type.
        /// </summary>
        public int GetInt(string key, int fallback = 0)
        {
            return TryGetInt(key, out var value) ? value : fallback;
        }

        /// <summary>
        /// Reads a float variable, returning <paramref name="fallback"/> if the key is missing or has another type.
        /// Integer variables are accepted and converted to float.
        /// </summary>
        public float GetFloat(string key, float fallback = 0f)
        {
            return TryGetFloat(key, out var value) ? value : fallback;
        }

        /// <summary>
        /// Reads a string variable, returning <paramref name="fallback"/> if the key is missing or has another type.
        /// </summary>
        public string GetString(string key, string fallback = "")
        {
            return TryGetString(key, out var value) ? value : fallback;
        }

        /// <summary>
        /// Tries to read any supported variable as a culture-invariant display string.
        /// </summary>
        public bool TryGetValueAsString(string key, out string value)
        {
            value = default;
            if (!TryGetNormalizedValue(key, out var stored))
            {
                return false;
            }

            value = stored.ToDisplayString();
            return true;
        }

        /// <summary>
        /// Tries to read any supported variable as a display string using an exact key first,
        /// then a case-insensitive fallback for author-facing text tokens.
        /// </summary>
        public bool TryGetValueAsStringIgnoreCase(string key, out string value)
        {
            if (TryGetValueAsString(key, out value))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                value = default;
                return false;
            }

            var normalizedKey = key.Trim();
            foreach (var pair in _values)
            {
                if (!string.Equals(pair.Key, normalizedKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = pair.Value.ToDisplayString();
                return true;
            }

            value = default;
            return false;
        }
        #endregion

        #region ---------------- Internals ----------------
        private void SetValue(string key, VariableValue value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[DialogueVariableStore] Ignored variable with an empty key.");
                return;
            }

            var normalizedKey = key.Trim();
            if (_values.TryGetValue(normalizedKey, out var existing) && existing.Equals(value))
            {
                return;
            }

            _values[normalizedKey] = value;
            VariableChanged?.Invoke(normalizedKey, value.Type);
        }

        private bool TryGetValue(string key, DialogueVariableValueType expectedType, out VariableValue value)
        {
            value = default;
            return TryGetNormalizedValue(key, out value) && value.Type == expectedType;
        }

        private bool TryGetNormalizedValue(string key, out VariableValue value)
        {
            value = default;
            return !string.IsNullOrWhiteSpace(key) && _values.TryGetValue(key.Trim(), out value);
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

            value = default;
            return false;
        }

        private readonly struct VariableValue : IEquatable<VariableValue>
        {
            private readonly bool _boolValue;
            private readonly int _intValue;
            private readonly float _floatValue;
            private readonly string _stringValue;

            private VariableValue(DialogueVariableValueType type, bool boolValue, int intValue, float floatValue, string stringValue)
            {
                Type = type;
                _boolValue = boolValue;
                _intValue = intValue;
                _floatValue = floatValue;
                _stringValue = stringValue;
            }

            public DialogueVariableValueType Type { get; }

            public static VariableValue FromBool(bool value) => new(DialogueVariableValueType.Boolean, value, 0, 0f, null);
            public static VariableValue FromInt(int value) => new(DialogueVariableValueType.Integer, false, value, 0f, null);
            public static VariableValue FromFloat(float value) => new(DialogueVariableValueType.Float, false, 0, value, null);
            public static VariableValue FromString(string value) => new(DialogueVariableValueType.String, false, 0, 0f, value ?? string.Empty);

            public bool Equals(VariableValue other)
            {
                if (Type != other.Type)
                {
                    return false;
                }

                return Type switch
                {
                    DialogueVariableValueType.Boolean => _boolValue == other._boolValue,
                    DialogueVariableValueType.Integer => _intValue == other._intValue,
                    DialogueVariableValueType.Float => Mathf.Approximately(_floatValue, other._floatValue),
                    DialogueVariableValueType.String => string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal),
                    _ => false
                };
            }

            public bool TryGetBool(out bool value)
            {
                value = _boolValue;
                return Type == DialogueVariableValueType.Boolean;
            }

            public bool TryGetInt(out int value)
            {
                value = _intValue;
                return Type == DialogueVariableValueType.Integer;
            }

            public bool TryGetFloat(out float value)
            {
                if (Type == DialogueVariableValueType.Float)
                {
                    value = _floatValue;
                    return true;
                }

                if (Type == DialogueVariableValueType.Integer)
                {
                    value = _intValue;
                    return true;
                }

                value = default;
                return false;
            }

            public bool TryGetString(out string value)
            {
                value = _stringValue;
                return Type == DialogueVariableValueType.String;
            }

            public string ToDisplayString()
            {
                return Type switch
                {
                    DialogueVariableValueType.Boolean => _boolValue ? "true" : "false",
                    DialogueVariableValueType.Integer => _intValue.ToString(CultureInfo.InvariantCulture),
                    DialogueVariableValueType.Float => _floatValue.ToString(CultureInfo.InvariantCulture),
                    DialogueVariableValueType.String => _stringValue ?? string.Empty,
                    _ => string.Empty
                };
            }
        }
        #endregion
    }

    /// <summary>
    /// Serialized initial value for a dialogue variable.
    /// </summary>
    [Serializable]
    public sealed class DialogueVariableEntry
    {
        [Tooltip("Runtime key used by condition nodes.")]
        public string key;

        [Tooltip("Stored value type.")]
        public DialogueVariableValueType type;

        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
    }
}
