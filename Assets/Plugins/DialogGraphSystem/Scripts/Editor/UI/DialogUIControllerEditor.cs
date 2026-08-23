#if UNITY_EDITOR
using DialogSystem.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.UI
{
    [CustomEditor(typeof(DialogUIController))]
    public class DialogUIControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _panelRoot;
        private SerializedProperty _speakerName;
        private SerializedProperty _dialogText;
        private SerializedProperty _portraitImage;
        private SerializedProperty _allowPortraitSideSwapping;
        private SerializedProperty _portraitLayoutRoot;
        private SerializedProperty _dialogContentLayoutRoot;
        private SerializedProperty _dialogBoxImage;
        private SerializedProperty _nameplateImage;
        private SerializedProperty _avatarFrameImage;
        private SerializedProperty _skipButtonIconImage;
        private SerializedProperty _skipButtonBackgroundImage;
        private SerializedProperty _skipLineButtonIconImage;
        private SerializedProperty _skipLineButtonBackgroundImage;
        private SerializedProperty _doDebug;
        private SerializedProperty _choicesContainer;
        private SerializedProperty _choiceButtonPrefab;
        private SerializedProperty _skipButton;
        private SerializedProperty _skipLineButton;
        private SerializedProperty _skipAllTextLabel;
        private SerializedProperty _historyPanelButton;
        private SerializedProperty _historyButtonBackgroundImage;
        private SerializedProperty _historyButtonIconImage;
        private SerializedProperty _autoPlayButton;
        private SerializedProperty _settingsPanelButton;
        private SerializedProperty _settingsButtonBackgroundImage;
        private SerializedProperty _settingsButtonIconImage;
        private SerializedProperty _autoPlayButtonBackgroundImage;
        private SerializedProperty _autoPlayButtonIconImage;

        private bool _showCoreUi = true;
        private bool _showThemeTargets = true;
        private bool _showChoices = true;
        private bool _showButtons = true;

        private void OnEnable()
        {
            _panelRoot = serializedObject.FindProperty("panelRoot");
            _speakerName = serializedObject.FindProperty("speakerName");
            _dialogText = serializedObject.FindProperty("dialogText");
            _portraitImage = serializedObject.FindProperty("portraitImage");
            _allowPortraitSideSwapping = serializedObject.FindProperty("_allowPortraitSideSwapping");
            _portraitLayoutRoot = serializedObject.FindProperty("_portraitLayoutRoot");
            _dialogContentLayoutRoot = serializedObject.FindProperty("_dialogContentLayoutRoot");
            _dialogBoxImage = serializedObject.FindProperty("_dialogBoxImage");
            _nameplateImage = serializedObject.FindProperty("_nameplateImage");
            _avatarFrameImage = serializedObject.FindProperty("_avatarFrameImage");
            _skipButtonIconImage = serializedObject.FindProperty("_skipButtonIconImage");
            _skipButtonBackgroundImage = serializedObject.FindProperty("_skipButtonBackgroundImage");
            _skipLineButtonIconImage = serializedObject.FindProperty("_skipLineButtonIconImage");
            _skipLineButtonBackgroundImage = serializedObject.FindProperty("_skipLineButtonBackgroundImage");
            _doDebug = serializedObject.FindProperty("_doDebug");
            _choicesContainer = serializedObject.FindProperty("choicesContainer");
            _choiceButtonPrefab = serializedObject.FindProperty("choiceButtonPrefab");
            _skipButton = serializedObject.FindProperty("skipButton");
            _skipLineButton = serializedObject.FindProperty("skipLineButton");
            _skipAllTextLabel = serializedObject.FindProperty("_skipAllTextLabel");
            _historyPanelButton = serializedObject.FindProperty("historyPanelButton");
            _historyButtonBackgroundImage = serializedObject.FindProperty("_historyButtonBackgroundImage");
            _historyButtonIconImage = serializedObject.FindProperty("_historyButtonIconImage");
            _autoPlayButton = serializedObject.FindProperty("autoPlayButton");
            _settingsPanelButton = serializedObject.FindProperty("settingsPanelButton");
            _settingsButtonBackgroundImage = serializedObject.FindProperty("_settingsButtonBackgroundImage");
            _settingsButtonIconImage = serializedObject.FindProperty("_settingsButtonIconImage");
            _autoPlayButtonBackgroundImage = serializedObject.FindProperty("_autoPlayButtonBackgroundImage");
            _autoPlayButtonIconImage = serializedObject.FindProperty("_autoPlayButtonIconImage");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();

            _showCoreUi = EditorGUILayout.Foldout(_showCoreUi, "Core UI", true, EditorStyles.foldoutHeader);
            if (_showCoreUi)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_panelRoot);
                EditorGUILayout.PropertyField(_speakerName);
                EditorGUILayout.PropertyField(_dialogText);
                EditorGUILayout.PropertyField(_portraitImage);
                EditorGUILayout.PropertyField(_allowPortraitSideSwapping, new GUIContent("Allow Portrait Side Swapping"));
                EditorGUILayout.PropertyField(_portraitLayoutRoot, new GUIContent("Portrait Layout Root"));
                EditorGUILayout.PropertyField(_dialogContentLayoutRoot, new GUIContent("Dialog Content Layout Root"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showThemeTargets = EditorGUILayout.Foldout(_showThemeTargets, "Theme Targets", true, EditorStyles.foldoutHeader);
            if (_showThemeTargets)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_dialogBoxImage, new GUIContent("Dialog Box"));
                EditorGUILayout.PropertyField(_nameplateImage, new GUIContent("Nameplate"));
                EditorGUILayout.PropertyField(_avatarFrameImage, new GUIContent("Avatar Frame"));
                EditorGUILayout.PropertyField(_skipButtonBackgroundImage, new GUIContent("Skip Button Background"));
                EditorGUILayout.PropertyField(_skipButtonIconImage, new GUIContent("Skip Button Icon"));
                EditorGUILayout.PropertyField(_skipLineButtonBackgroundImage, new GUIContent("Skip Line Button Background"));
                EditorGUILayout.PropertyField(_skipLineButtonIconImage, new GUIContent("Skip Line Button Icon"));
                EditorGUILayout.PropertyField(_skipAllTextLabel, new GUIContent("Skip All Text"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showChoices = EditorGUILayout.Foldout(_showChoices, "Choices", true, EditorStyles.foldoutHeader);
            if (_showChoices)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_doDebug, new GUIContent("Debug Logging"));
                EditorGUILayout.PropertyField(_choicesContainer);
                EditorGUILayout.PropertyField(_choiceButtonPrefab);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showButtons = EditorGUILayout.Foldout(_showButtons, "Buttons", true, EditorStyles.foldoutHeader);
            if (_showButtons)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_skipButton);
                EditorGUILayout.PropertyField(_skipLineButton, new GUIContent("Skip Line Button"));
                EditorGUILayout.PropertyField(_historyPanelButton, new GUIContent("History Button"));
                EditorGUILayout.PropertyField(_historyButtonBackgroundImage, new GUIContent("History Button Background"));
                EditorGUILayout.PropertyField(_historyButtonIconImage, new GUIContent("History Button Icon"));
                EditorGUILayout.PropertyField(_autoPlayButton);
                EditorGUILayout.PropertyField(_settingsPanelButton, new GUIContent("Settings Button"));
                EditorGUILayout.PropertyField(_settingsButtonBackgroundImage, new GUIContent("Settings Button Background"));
                EditorGUILayout.PropertyField(_settingsButtonIconImage, new GUIContent("Settings Button Icon"));
                EditorGUILayout.PropertyField(_autoPlayButtonBackgroundImage, new GUIContent("AutoPlay Button Background"));
                EditorGUILayout.PropertyField(_autoPlayButtonIconImage, new GUIContent("AutoPlay Button Icon"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(DialogUIController), false);
            }

            EditorGUILayout.Space(4);
        }
    }
}
#endif
