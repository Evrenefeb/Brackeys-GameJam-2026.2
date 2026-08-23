using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Dialog
{
    /// <summary>
    /// Clarifies that serialized store lists are optional startup defaults, not runtime event wiring.
    /// </summary>
    [CustomEditor(typeof(DialogueVariableStore))]
    public sealed class DialogueVariableStoreEditor : UnityEditor.Editor
    {
        private SerializedProperty _variableDefinitions;
        private SerializedProperty _initialVariables;

        private void OnEnable()
        {
            _variableDefinitions = serializedObject.FindProperty("variableDefinitions");
            _initialVariables = serializedObject.FindProperty("initialVariables");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Dialogue Variable Store", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            EditorGUILayout.HelpBox(
                "This component stores runtime dialogue values. The lists below are optional startup defaults only. " +
                "They are applied on Awake and when ResetToInitialVariables is called.",
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "These defaults do not trigger gameplay behavior by themselves. To react in game, read the store from code, " +
                "subscribe to VariableChanged, or use dialogue actions for side effects.",
                MessageType.None);

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(
                _variableDefinitions,
                new GUIContent("Definition Assets (Optional)", "Reusable variable assets used to seed runtime defaults."),
                true);

            EditorGUILayout.Space(2f);
            EditorGUILayout.PropertyField(
                _initialVariables,
                new GUIContent("Inline Default Values (Optional)", "Scene-local startup defaults applied after definition assets."),
                true);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Runtime API", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Example: store.SetInt(\"gold\", 10) updates dialogue state. Your game can then read that value or listen to VariableChanged.",
                MessageType.None);

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}
