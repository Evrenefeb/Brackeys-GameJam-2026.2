#if UNITY_EDITOR
using DialogSystem.Runtime.UI.Theming;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.UI
{
    [CustomEditor(typeof(DialogSettingsThemeSO))]
    public class DialogSettingsThemeSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _themeName;
        private SerializedProperty _themeDescription;
        private SerializedProperty _panelBackground;
        private SerializedProperty _headerBackground;
        private SerializedProperty _rowBackground;
        private SerializedProperty _controlBackground;
        private SerializedProperty _closeButton;
        private SerializedProperty _panelTint;
        private SerializedProperty _headerTint;
        private SerializedProperty _rowTint;
        private SerializedProperty _controlTint;
        private SerializedProperty _accentColor;
        private SerializedProperty _textColor;
        private SerializedProperty _valueTextColor;
        private SerializedProperty _font;

        private bool _showIdentity = true;
        private bool _showSprites = true;
        private bool _showColors = true;
        private bool _showTypography = true;

        private void OnEnable()
        {
            _themeName = serializedObject.FindProperty("themeName");
            _themeDescription = serializedObject.FindProperty("themeDescription");
            _panelBackground = serializedObject.FindProperty("panelBackground");
            _headerBackground = serializedObject.FindProperty("headerBackground");
            _rowBackground = serializedObject.FindProperty("rowBackground");
            _controlBackground = serializedObject.FindProperty("controlBackground");
            _closeButton = serializedObject.FindProperty("closeButton");
            _panelTint = serializedObject.FindProperty("panelTint");
            _headerTint = serializedObject.FindProperty("headerTint");
            _rowTint = serializedObject.FindProperty("rowTint");
            _controlTint = serializedObject.FindProperty("controlTint");
            _accentColor = serializedObject.FindProperty("accentColor");
            _textColor = serializedObject.FindProperty("textColor");
            _valueTextColor = serializedObject.FindProperty("valueTextColor");
            _font = serializedObject.FindProperty("font");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((ScriptableObject)target), typeof(DialogSettingsThemeSO), false);
            }

            EditorGUILayout.Space(4f);

            if (string.IsNullOrWhiteSpace(_themeName.stringValue))
            {
                EditorGUILayout.HelpBox("Theme name is empty. Give the settings theme a readable display name.", MessageType.Warning);
            }

            _showIdentity = EditorGUILayout.Foldout(_showIdentity, "Identity", true, EditorStyles.foldoutHeader);
            if (_showIdentity)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_themeName);
                EditorGUILayout.PropertyField(_themeDescription);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);

            _showSprites = EditorGUILayout.Foldout(_showSprites, "Sprites", true, EditorStyles.foldoutHeader);
            if (_showSprites)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Empty sprite fields keep the visuals from the settings prefab.", MessageType.Info);
                EditorGUILayout.PropertyField(_panelBackground);
                EditorGUILayout.PropertyField(_headerBackground);
                EditorGUILayout.PropertyField(_rowBackground);
                EditorGUILayout.PropertyField(_controlBackground);
                EditorGUILayout.PropertyField(_closeButton);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);

            _showColors = EditorGUILayout.Foldout(_showColors, "Colors", true, EditorStyles.foldoutHeader);
            if (_showColors)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Leave alpha at 0 to keep the color from the settings prefab for that role.", MessageType.Info);
                EditorGUILayout.PropertyField(_panelTint);
                EditorGUILayout.PropertyField(_headerTint);
                EditorGUILayout.PropertyField(_rowTint);
                EditorGUILayout.PropertyField(_controlTint);
                EditorGUILayout.PropertyField(_accentColor);
                EditorGUILayout.PropertyField(_textColor);
                EditorGUILayout.PropertyField(_valueTextColor);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);

            _showTypography = EditorGUILayout.Foldout(_showTypography, "Typography", true, EditorStyles.foldoutHeader);
            if (_showTypography)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_font);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
