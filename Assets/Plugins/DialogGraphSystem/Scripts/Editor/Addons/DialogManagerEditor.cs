#if UNITY_EDITOR
using DialogSystem.Runtime.Core;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Addons
{
    [CustomEditor(typeof(DialogManager))]
    public class DialogManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _doDebug;
        private SerializedProperty _dialogGraphs;
        private SerializedProperty _dialogUiManagerPrefab;
        private SerializedProperty _dialogUiManager;
        private SerializedProperty _audioSource;
        private SerializedProperty _localTextSettings;
        private SerializedProperty _localAudioSettings;
        private SerializedProperty _actionRunner;
        private SerializedProperty _variableStore;

        private bool _showGraphs = true;
        private bool _showUi = true;
        private bool _showAudio = true;
        private bool _showOverrides = true;
        private bool _showActions = true;
        private bool _showVariables = true;

        private void OnEnable()
        {
            _doDebug = serializedObject.FindProperty("doDebug");
            _dialogGraphs = serializedObject.FindProperty("dialogGraphs");
            _dialogUiManagerPrefab = serializedObject.FindProperty("dialogUIManagerPrefab");
            _dialogUiManager = serializedObject.FindProperty("dialogUIManager");
            _audioSource = serializedObject.FindProperty("audioSource");
            _localTextSettings = serializedObject.FindProperty("localTextSettings");
            _localAudioSettings = serializedObject.FindProperty("localAudioSettings");
            _actionRunner = serializedObject.FindProperty("actionRunner");
            _variableStore = serializedObject.FindProperty("variableStore");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            DrawValidation();

            EditorGUILayout.PropertyField(_doDebug);

            _showGraphs = EditorGUILayout.Foldout(_showGraphs, "Dialogue Graphs", true, EditorStyles.foldoutHeader);
            if (_showGraphs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_dialogGraphs, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showUi = EditorGUILayout.Foldout(_showUi, "Runtime UI Bootstrap", true, EditorStyles.foldoutHeader);
            if (_showUi)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_dialogUiManagerPrefab, new GUIContent("Dialogue UI Manager Prefab"));
                EditorGUILayout.PropertyField(_dialogUiManager, new GUIContent("Existing UI Manager"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showAudio = EditorGUILayout.Foldout(_showAudio, "Audio", true, EditorStyles.foldoutHeader);
            if (_showAudio)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_audioSource);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showOverrides = EditorGUILayout.Foldout(_showOverrides, "Overrides", true, EditorStyles.foldoutHeader);
            if (_showOverrides)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_localTextSettings);
                EditorGUILayout.PropertyField(_localAudioSettings);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showActions = EditorGUILayout.Foldout(_showActions, "Actions", true, EditorStyles.foldoutHeader);
            if (_showActions)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_actionRunner, new GUIContent("Action Runner"));
                EditorGUILayout.HelpBox("Optional. If left empty, DialogManager first checks this GameObject for a DialogActionRunner, then searches the scene.", MessageType.None);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showVariables = EditorGUILayout.Foldout(_showVariables, "Variables", true, EditorStyles.foldoutHeader);
            if (_showVariables)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_variableStore, new GUIContent("Variable Store"));
                EditorGUILayout.HelpBox("Optional. If left empty, DialogManager first checks this GameObject for a DialogueVariableStore, then searches the scene.", MessageType.None);
                EditorGUI.indentLevel--;
            }

            DrawSettingsButton();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(DialogManager), false);
            }

            EditorGUILayout.Space(4);
        }

        private void DrawValidation()
        {
            if (_dialogUiManagerPrefab.objectReferenceValue == null && _dialogUiManager.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a Dialogue UI Manager prefab for zero-setup runtime bootstrap, or reference an existing DialogUIManager in the scene.", MessageType.Warning);
            }

            if (target is DialogManager manager)
            {
                var registryValidation = DialogManager.ValidateDialogRegistry(manager.dialogGraphs);
                foreach (var issue in registryValidation.Issues)
                {
                    var messageType = issue.Severity == DialogManager.DialogRegistryValidationSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning;
                    EditorGUILayout.HelpBox(issue.Message, messageType);
                }
            }
        }

        private void DrawSettingsButton()
        {
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Open Settings", GUILayout.Height(26)))
                {
                    var type =
                     System.Type.GetType("DialogSystem.EditorTools.Settings.DialogSettingsEditorWindow, Assembly-CSharp-Editor") ??
                     System.Type.GetType("DialogSystem.EditorTools.Settings.DialogSettingsEditorWindow");

                    if (type == null)
                    {
                        EditorUtility.DisplayDialog(
                            "Dialogue Graph System",
                            "Settings window could not be found.",
                            "OK"
                        );
                        return;
                    }

                    var method = type.GetMethod(
                        "Open",
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.Public,
                        null,
                        System.Type.EmptyTypes,
                        null
                    );

                    if (method == null)
                    {
                        EditorUtility.DisplayDialog(
                            "Dialogue Graph System",
                            "Settings window found, but the static Open() method is missing.",
                            "OK"
                        );
                        return;
                    }

                    method.Invoke(null, null);
                }
            }
        }
    }
}
#endif