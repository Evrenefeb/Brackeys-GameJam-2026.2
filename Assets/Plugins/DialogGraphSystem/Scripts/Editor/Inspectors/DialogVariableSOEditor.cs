using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Dialog
{
    /// <summary>
    /// Custom inspector for dialogue variable definition assets.
    /// </summary>
    [CustomEditor(typeof(DialogVariableSO))]
    public sealed class DialogVariableSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _variableKey;
        private SerializedProperty _valueType;
        private SerializedProperty _boolDefaultValue;
        private SerializedProperty _intDefaultValue;
        private SerializedProperty _floatDefaultValue;
        private SerializedProperty _stringDefaultValue;

        private void OnEnable()
        {
            _variableKey = serializedObject.FindProperty("variableKey");
            _valueType = serializedObject.FindProperty("valueType");
            _boolDefaultValue = serializedObject.FindProperty("boolDefaultValue");
            _intDefaultValue = serializedObject.FindProperty("intDefaultValue");
            _floatDefaultValue = serializedObject.FindProperty("floatDefaultValue");
            _stringDefaultValue = serializedObject.FindProperty("stringDefaultValue");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Dialogue Variable", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.PropertyField(_variableKey, new GUIContent("Key"));
            EditorGUILayout.PropertyField(_valueType, new GUIContent("Type"));

            DrawDefaultValueField();

            if (target is DialogVariableSO variable)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Dialogue token: {" + variable.Key + "}\nLegacy tokens also work: {{" + variable.Key + "}} and {var:" + variable.Key + "}",
                    MessageType.Info);
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
            }
        }

        private void DrawDefaultValueField()
        {
            var type = (DialogueVariableValueType)_valueType.enumValueIndex;
            switch (type)
            {
                case DialogueVariableValueType.Boolean:
                    EditorGUILayout.PropertyField(_boolDefaultValue, new GUIContent("Default Value"));
                    break;
                case DialogueVariableValueType.Integer:
                    EditorGUILayout.PropertyField(_intDefaultValue, new GUIContent("Default Value"));
                    break;
                case DialogueVariableValueType.Float:
                    EditorGUILayout.PropertyField(_floatDefaultValue, new GUIContent("Default Value"));
                    break;
                case DialogueVariableValueType.String:
                    EditorGUILayout.PropertyField(_stringDefaultValue, new GUIContent("Default Value"));
                    break;
            }
        }
    }
}
