#if UNITY_EDITOR
using DialogSystem.Runtime.DialogHistory;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.UI
{
    [CustomEditor(typeof(DialogueHistory))]
    public class DialogueHistoryEditor : UnityEditor.Editor
    {
        private SerializedProperty _manager;
        private SerializedProperty _view;
        private SerializedProperty _maxEntries;
        private SerializedProperty _resumeAutoplayOnClose;

        private bool _showRefs = true;
        private bool _showSettings = true;

        private void OnEnable()
        {
            _manager = serializedObject.FindProperty("manager");
            _view = serializedObject.FindProperty("view");
            _maxEntries = serializedObject.FindProperty("maxEntries");
            _resumeAutoplayOnClose = serializedObject.FindProperty("resumeAutoplayOnClose");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            DrawValidation();

            _showRefs = EditorGUILayout.Foldout(_showRefs, "Runtime References", true, EditorStyles.foldoutHeader);
            if (_showRefs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_manager);
                EditorGUILayout.PropertyField(_view);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showSettings = EditorGUILayout.Foldout(_showSettings, "Settings", true, EditorStyles.foldoutHeader);
            if (_showSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_maxEntries);
                EditorGUILayout.PropertyField(_resumeAutoplayOnClose);
                EditorGUI.indentLevel--;
            }

            if (Application.isPlaying)
            {
                var history = (DialogueHistory)target;
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox($"Entries: {history.Entries.Count}\nOpen: {history.IsOpen}", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(DialogueHistory), false);
            }

            EditorGUILayout.Space(4);
        }

        private void DrawValidation()
        {
            if (_manager.objectReferenceValue == null && _view.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("DialogueHistory can auto-resolve its manager and view at runtime from DialogUIManager. Leave these empty unless you want manual overrides.", MessageType.None);
            }
        }
    }
}
#endif
