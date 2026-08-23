#if UNITY_EDITOR
using DialogSystem.Runtime.UI;
using DialogSystem.Runtime.UI.Theming;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DialogSystem.EditorTools.UI
{
    [CustomEditor(typeof(DialogThemeSO))]
    public class DialogThemeSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _themeName;
        private SerializedProperty _themeDescription;
        private SerializedProperty _dialogBox;
        private SerializedProperty _nameplate;
        private SerializedProperty _avatarFrame;
        private SerializedProperty _skipButtonIcon;
        private SerializedProperty _skipButtonBackground;
        private SerializedProperty _skipLineButtonIcon;
        private SerializedProperty _skipLineButtonBackground;
        private SerializedProperty _choiceButtonNormal;
        private SerializedProperty _choiceButtonActive;
        private SerializedProperty _choiceMessageIcon;
        private SerializedProperty _choiceButtonPrefabOverride;
        private SerializedProperty _hotkeyBackgroundSprite;
        private SerializedProperty _historyPanelBg;
        private SerializedProperty _historyTitleBg;
        private SerializedProperty _historyRowChar;
        private SerializedProperty _historyRowChoice;
        private SerializedProperty _historyChoiceIcon;
        private SerializedProperty _historyButton;
        private SerializedProperty _historyButtonIcon;
        private SerializedProperty _settingsButton;
        private SerializedProperty _settingsButtonIcon;
        private SerializedProperty _closeButton;
        private SerializedProperty _autoPlayButtonBackground;
        private SerializedProperty _autoPlayNormalIcon;
        private SerializedProperty _autoPlayActiveIcon;
        private SerializedProperty _dialogTextColor;
        private SerializedProperty _speakerNameColor;
        private SerializedProperty _choiceNormalColor;
        private SerializedProperty _choiceActiveColor;
        private SerializedProperty _historySpeakerColor;
        private SerializedProperty _historyCharTextColor;
        private SerializedProperty _historyChoiceTextColor;
        private SerializedProperty _skipAllTextColor;
        private SerializedProperty _historyTitleTextColor;
        private SerializedProperty _defaultFont;
        private SerializedProperty _speakerNameFont;
        private SerializedProperty _dialogTextFont;
        private SerializedProperty _choiceTextFont;
        private SerializedProperty _choiceHotkeyFont;
        private SerializedProperty _historySpeakerFont;
        private SerializedProperty _historyLineFont;

        private bool _showIdentity = true;
        private bool _showDialog = true;
        private bool _showChoices = true;
        private bool _showHistory = true;
        private bool _showButtons = true;
        private bool _showColors = true;
        private bool _showFonts = true;
        private bool _showFontOverrides = false;

        private void OnEnable()
        {
            _themeName = serializedObject.FindProperty("themeName");
            _themeDescription = serializedObject.FindProperty("themeDescription");
            _dialogBox = serializedObject.FindProperty("dialogBox");
            _nameplate = serializedObject.FindProperty("nameplate");
            _avatarFrame = serializedObject.FindProperty("avatarFrame");
            _skipButtonIcon = serializedObject.FindProperty("skipButtonIcon");
            _skipButtonBackground = serializedObject.FindProperty("skipButtonBackground");
            _skipLineButtonIcon = serializedObject.FindProperty("skipLineButtonIcon");
            _skipLineButtonBackground = serializedObject.FindProperty("skipLineButtonBackground");
            _choiceButtonNormal = serializedObject.FindProperty("choiceButtonNormal");
            _choiceButtonActive = serializedObject.FindProperty("choiceButtonActive");
            _choiceMessageIcon = serializedObject.FindProperty("choiceMessageIcon");
            _choiceButtonPrefabOverride = serializedObject.FindProperty("choiceButtonPrefabOverride");
            _hotkeyBackgroundSprite = serializedObject.FindProperty("hotkeyBackgroundSprite");
            _historyPanelBg = serializedObject.FindProperty("historyPanelBg");
            _historyTitleBg = serializedObject.FindProperty("historyTitleBg");
            _historyRowChar = serializedObject.FindProperty("historyRowChar");
            _historyRowChoice = serializedObject.FindProperty("historyRowChoice");
            _historyChoiceIcon = serializedObject.FindProperty("historyChoiceIcon");
            _historyButton = serializedObject.FindProperty("historyButton");
            _historyButtonIcon = serializedObject.FindProperty("historyButtonIcon");
            _settingsButton = serializedObject.FindProperty("settingsButton");
            _settingsButtonIcon = serializedObject.FindProperty("settingsButtonIcon");
            _closeButton = serializedObject.FindProperty("closeButton");
            _autoPlayButtonBackground = serializedObject.FindProperty("autoPlayButtonBackground");
            _autoPlayNormalIcon = serializedObject.FindProperty("autoPlayNormalIcon");
            _autoPlayActiveIcon = serializedObject.FindProperty("autoPlayActiveIcon");
            _dialogTextColor = serializedObject.FindProperty("dialogTextColor");
            _speakerNameColor = serializedObject.FindProperty("speakerNameColor");
            _choiceNormalColor = serializedObject.FindProperty("choiceNormalColor");
            _choiceActiveColor = serializedObject.FindProperty("choiceActiveColor");
            _historySpeakerColor = serializedObject.FindProperty("historySpeakerColor");
            _historyCharTextColor = serializedObject.FindProperty("historyCharTextColor");
            _historyChoiceTextColor = serializedObject.FindProperty("historyChoiceTextColor");
            _skipAllTextColor = serializedObject.FindProperty("skipAllTextColor");
            _historyTitleTextColor = serializedObject.FindProperty("historyTitleTextColor");
            _defaultFont = serializedObject.FindProperty("defaultFont");
            _speakerNameFont = serializedObject.FindProperty("speakerNameFont");
            _dialogTextFont = serializedObject.FindProperty("dialogTextFont");
            _choiceTextFont = serializedObject.FindProperty("choiceTextFont");
            _choiceHotkeyFont = serializedObject.FindProperty("choiceHotkeyFont");
            _historySpeakerFont = serializedObject.FindProperty("historySpeakerFont");
            _historyLineFont = serializedObject.FindProperty("historyLineFont");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            DrawValidation();

            _showIdentity = EditorGUILayout.Foldout(_showIdentity, "Identity", true, EditorStyles.foldoutHeader);
            if (_showIdentity)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_themeName);
                EditorGUILayout.PropertyField(_themeDescription);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showDialog = EditorGUILayout.Foldout(_showDialog, "Dialog UI", true, EditorStyles.foldoutHeader);
            if (_showDialog)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_dialogBox);
                EditorGUILayout.PropertyField(_nameplate);
                EditorGUILayout.PropertyField(_avatarFrame);
                EditorGUILayout.PropertyField(_skipButtonBackground);
                EditorGUILayout.PropertyField(_skipButtonIcon);
                EditorGUILayout.PropertyField(_skipLineButtonBackground);
                EditorGUILayout.PropertyField(_skipLineButtonIcon);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showChoices = EditorGUILayout.Foldout(_showChoices, "Choices", true, EditorStyles.foldoutHeader);
            if (_showChoices)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_choiceButtonNormal);
                EditorGUILayout.PropertyField(_choiceButtonActive);
                EditorGUILayout.PropertyField(_choiceMessageIcon);
                EditorGUILayout.PropertyField(_hotkeyBackgroundSprite);
                EditorGUILayout.PropertyField(_choiceButtonPrefabOverride);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showHistory = EditorGUILayout.Foldout(_showHistory, "History", true, EditorStyles.foldoutHeader);
            if (_showHistory)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_historyPanelBg);
                EditorGUILayout.PropertyField(_historyTitleBg);
                EditorGUILayout.PropertyField(_historyRowChar);
                EditorGUILayout.PropertyField(_historyRowChoice);
                EditorGUILayout.PropertyField(_historyChoiceIcon);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showButtons = EditorGUILayout.Foldout(_showButtons, "Buttons", true, EditorStyles.foldoutHeader);
            if (_showButtons)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_historyButton);
                EditorGUILayout.PropertyField(_historyButtonIcon);
                EditorGUILayout.PropertyField(_settingsButton);
                EditorGUILayout.PropertyField(_settingsButtonIcon);
                EditorGUILayout.PropertyField(_closeButton);
                EditorGUILayout.PropertyField(_autoPlayButtonBackground);
                EditorGUILayout.PropertyField(_autoPlayNormalIcon);
                EditorGUILayout.PropertyField(_autoPlayActiveIcon);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showColors = EditorGUILayout.Foldout(_showColors, "Colors", true, EditorStyles.foldoutHeader);
            if (_showColors)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_dialogTextColor);
                EditorGUILayout.PropertyField(_speakerNameColor);
                EditorGUILayout.PropertyField(_choiceNormalColor);
                EditorGUILayout.PropertyField(_choiceActiveColor);
                EditorGUILayout.PropertyField(_historySpeakerColor);
                EditorGUILayout.PropertyField(_historyCharTextColor);
                EditorGUILayout.PropertyField(_historyChoiceTextColor);
                EditorGUILayout.PropertyField(_skipAllTextColor);
                EditorGUILayout.PropertyField(_historyTitleTextColor);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showFonts = EditorGUILayout.Foldout(_showFonts, "Fonts", true, EditorStyles.foldoutHeader);
            if (_showFonts)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Common setup: assign Default Font once and leave the role-specific overrides empty. " +
                    "Only expand Font Overrides when a specific UI role needs a different font.",
                    MessageType.Info
                );

                EditorGUILayout.PropertyField(_defaultFont);

                EditorGUILayout.Space(4);

                _showFontOverrides = EditorGUILayout.Foldout(
                    _showFontOverrides,
                    "Font Overrides",
                    true
                );

                if (_showFontOverrides)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(_speakerNameFont);
                    EditorGUILayout.PropertyField(_dialogTextFont);
                    EditorGUILayout.PropertyField(_choiceTextFont);
                    EditorGUILayout.PropertyField(_choiceHotkeyFont);
                    EditorGUILayout.PropertyField(_historySpeakerFont);
                    EditorGUILayout.PropertyField(_historyLineFont);

                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((ScriptableObject)target), typeof(DialogThemeSO), false);
            }

            EditorGUILayout.Space(4);
        }

        private void DrawValidation()
        {
            if (string.IsNullOrWhiteSpace(_themeName.stringValue))
            {
                EditorGUILayout.HelpBox("Theme name is empty. Give the theme a readable display name so users can identify it quickly.", MessageType.Warning);
            }

            if (_defaultFont.objectReferenceValue == null &&
                _speakerNameFont.objectReferenceValue == null &&
                _dialogTextFont.objectReferenceValue == null &&
                _choiceTextFont.objectReferenceValue == null &&
                _choiceHotkeyFont.objectReferenceValue == null &&
                _historySpeakerFont.objectReferenceValue == null &&
                _historyLineFont.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("No fonts are assigned. Text will keep the prefab defaults. Assign Default Font if you want the theme to fully control typography.", MessageType.Info);
            }

            var prefab = _choiceButtonPrefabOverride.objectReferenceValue as GameObject;
            if (prefab != null)
            {
                if (prefab.GetComponent<ChoiceButtonView>() == null)
                {
                    EditorGUILayout.HelpBox("Choice Button Prefab Override is missing a ChoiceButtonView component on the root.", MessageType.Warning);
                }

                if (prefab.GetComponent<Button>() == null && prefab.GetComponentInChildren<Button>(true) == null)
                {
                    EditorGUILayout.HelpBox("Choice Button Prefab Override does not contain a Button component.", MessageType.Warning);
                }
            }
        }
    }
}
#endif