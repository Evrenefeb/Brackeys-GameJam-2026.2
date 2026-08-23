#if UNITY_EDITOR
using DialogSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.UI
{
    [CustomEditor(typeof(DialogueHistoryView))]
    public class DialogueHistoryViewEditor : UnityEditor.Editor
    {
        private SerializedProperty _root;
        private SerializedProperty _scrollRect;
        private SerializedProperty _titleText;
        private SerializedProperty _contentRoot;
        private SerializedProperty _itemPrefab;
        private SerializedProperty _panelBackground;
        private SerializedProperty _titleBackground;
        private SerializedProperty _closeButtonImage;
        private SerializedProperty _prewarm;
        private SerializedProperty _hideChoiceIcon;

        private bool _showUiRefs = true;
        private bool _showThemeTargets = true;
        private bool _showPool = true;

        private void OnEnable()
        {
            _root = serializedObject.FindProperty("root");
            _scrollRect = serializedObject.FindProperty("scrollRect");
            _titleText = serializedObject.FindProperty("_titleText");
            _contentRoot = serializedObject.FindProperty("contentRoot");
            _itemPrefab = serializedObject.FindProperty("itemPrefab");
            _panelBackground = serializedObject.FindProperty("_panelBackground");
            _titleBackground = serializedObject.FindProperty("_titleBackground");
            _closeButtonImage = serializedObject.FindProperty("_closeButtonImage");
            _prewarm = serializedObject.FindProperty("prewarm");
            _hideChoiceIcon = serializedObject.FindProperty("hideChoiceIcon");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();

            _showUiRefs = EditorGUILayout.Foldout(_showUiRefs, "UI References", true, EditorStyles.foldoutHeader);
            if (_showUiRefs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_root);
                EditorGUILayout.PropertyField(_scrollRect);
                EditorGUILayout.PropertyField(_contentRoot);
                EditorGUILayout.PropertyField(_itemPrefab);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showThemeTargets = EditorGUILayout.Foldout(_showThemeTargets, "Theme Targets", true, EditorStyles.foldoutHeader);
            if (_showThemeTargets)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_panelBackground);
                EditorGUILayout.PropertyField(_titleBackground);
                EditorGUILayout.PropertyField(_closeButtonImage);
                EditorGUILayout.PropertyField(_titleText);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            _showPool = EditorGUILayout.Foldout(_showPool, "Pool Settings", true, EditorStyles.foldoutHeader);
            if (_showPool)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_prewarm);
                EditorGUILayout.PropertyField(_hideChoiceIcon);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(DialogueHistoryView), false);
            }

            EditorGUILayout.Space(4);
        }
    }
}
#endif
