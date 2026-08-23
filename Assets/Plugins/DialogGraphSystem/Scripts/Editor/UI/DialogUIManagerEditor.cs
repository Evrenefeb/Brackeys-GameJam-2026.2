#if UNITY_EDITOR
using System.Collections.Generic;
using DialogSystem.Runtime.UI;
using DialogSystem.Runtime.UI.Theming;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.UI
{
    [CustomEditor(typeof(DialogUIManager))]
    public class DialogUIManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty _defaultTheme;
        private SerializedProperty _themeMappings;
        private SerializedProperty _dialogCanvasPrefab;
        private SerializedProperty _dialogPanelPrefab;
        private SerializedProperty _fallbackChoiceButtonPrefab;
        private SerializedProperty _dontDestroyOnLoad;
        private SerializedProperty _dialogPanel;
        private SerializedProperty _historyPanel;
        private SerializedProperty _settingsPanel;
        private SerializedProperty _runtimeDialogCanvasInstance;
        private SerializedProperty _overrideRuntimeUiVisibility;
        private SerializedProperty _showBackgroundPanel;
        private SerializedProperty _showSpeakerName;
        private SerializedProperty _showPortrait;
        private SerializedProperty _showSkipButton;
        private SerializedProperty _showAutoButton;
        private SerializedProperty _showHistoryButton;
        private SerializedProperty _showLanguageButton;
        private SerializedProperty _showContinueIndicator;
        private SerializedProperty _showAutoSkipIcon;
        private SerializedProperty _showChoicePanel;
        private SerializedProperty _showHistoryPanel;
        private SerializedProperty _showSettingsPanel;
        private SerializedProperty _showSettingsLanguageDropdown;
        private SerializedProperty _showSettingsThemeDropdown;
        private SerializedProperty _showSettingsTextSpeed;
        private SerializedProperty _showSettingsAutoAdvance;
        private SerializedProperty _showSettingsVoiceVolume;
        private SerializedProperty _showSettingsSfxVolume;

        private bool _showThemes = true;
        private bool _showUiPrefabs = true;
        private bool _showSceneRefs = true;
        private bool _showVisibility = true;

        private void OnEnable()
        {
            _defaultTheme = serializedObject.FindProperty("_defaultTheme");
            _themeMappings = serializedObject.FindProperty("_themeMappings");
            _dialogCanvasPrefab = serializedObject.FindProperty("_dialogCanvasPrefab");
            _dialogPanelPrefab = serializedObject.FindProperty("_dialogPanelPrefab");
            _fallbackChoiceButtonPrefab = serializedObject.FindProperty("_fallbackChoiceButtonPrefab");
            _dontDestroyOnLoad = serializedObject.FindProperty("_dontDestroyOnLoad");
            _dialogPanel = serializedObject.FindProperty("_dialogPanel");
            _historyPanel = serializedObject.FindProperty("_historyPanel");
            _settingsPanel = serializedObject.FindProperty("_settingsPanel");
            _runtimeDialogCanvasInstance = serializedObject.FindProperty("_runtimeDialogCanvasInstance");
            _overrideRuntimeUiVisibility = serializedObject.FindProperty("_overrideRuntimeUiVisibility");
            _showBackgroundPanel = serializedObject.FindProperty("_showBackgroundPanel");
            _showSpeakerName = serializedObject.FindProperty("_showSpeakerName");
            _showPortrait = serializedObject.FindProperty("_showPortrait");
            _showSkipButton = serializedObject.FindProperty("_showSkipButton");
            _showAutoButton = serializedObject.FindProperty("_showAutoButton");
            _showHistoryButton = serializedObject.FindProperty("_showHistoryButton");
            _showLanguageButton = serializedObject.FindProperty("_showLanguageButton");
            _showContinueIndicator = serializedObject.FindProperty("_showContinueIndicator");
            _showAutoSkipIcon = serializedObject.FindProperty("_showAutoSkipIcon");
            _showChoicePanel = serializedObject.FindProperty("_showChoicePanel");
            _showHistoryPanel = serializedObject.FindProperty("_showHistoryPanel");
            _showSettingsPanel = serializedObject.FindProperty("_showSettingsPanel");
            _showSettingsLanguageDropdown = serializedObject.FindProperty("_showSettingsLanguageDropdown");
            _showSettingsThemeDropdown = serializedObject.FindProperty("_showSettingsThemeDropdown");
            _showSettingsTextSpeed = serializedObject.FindProperty("_showSettingsTextSpeed");
            _showSettingsAutoAdvance = serializedObject.FindProperty("_showSettingsAutoAdvance");
            _showSettingsVoiceVolume = serializedObject.FindProperty("_showSettingsVoiceVolume");
            _showSettingsSfxVolume = serializedObject.FindProperty("_showSettingsSfxVolume");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (DialogUIManager)target;
            DrawScriptField();
            DrawValidation(manager);
            DrawRuntimeStatus(manager);

            _showThemes = EditorGUILayout.Foldout(_showThemes, "Theme Setup", true, EditorStyles.foldoutHeader);
            if (_showThemes)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_defaultTheme);
                EditorGUILayout.PropertyField(_themeMappings, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);

            _showUiPrefabs = EditorGUILayout.Foldout(_showUiPrefabs, "UI Ownership", true, EditorStyles.foldoutHeader);
            if (_showUiPrefabs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_dialogCanvasPrefab);
                EditorGUILayout.PropertyField(_dialogPanelPrefab);
                EditorGUILayout.PropertyField(_fallbackChoiceButtonPrefab);
                EditorGUILayout.PropertyField(_dontDestroyOnLoad);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);

            _showSceneRefs = EditorGUILayout.Foldout(_showSceneRefs, "Existing Scene References", true, EditorStyles.foldoutHeader);
            if (_showSceneRefs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_dialogPanel);
                EditorGUILayout.PropertyField(_historyPanel);
                EditorGUILayout.PropertyField(_settingsPanel, new GUIContent("Settings Panel"));
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(_runtimeDialogCanvasInstance);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4f);

            _showVisibility = EditorGUILayout.Foldout(_showVisibility, "Runtime UI Visibility", true, EditorStyles.foldoutHeader);
            if (_showVisibility)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_overrideRuntimeUiVisibility, new GUIContent("Override Visibility"));
                using (new EditorGUI.DisabledScope(!_overrideRuntimeUiVisibility.boolValue))
                {
                    EditorGUILayout.PropertyField(_showBackgroundPanel, new GUIContent("Show Background Panel"));
                    EditorGUILayout.PropertyField(_showSpeakerName, new GUIContent("Show Speaker Name"));
                    EditorGUILayout.PropertyField(_showPortrait, new GUIContent("Show Portrait"));
                    EditorGUILayout.PropertyField(_showSkipButton, new GUIContent("Show Skip All"));
                    EditorGUILayout.PropertyField(_showAutoButton, new GUIContent("Show Auto Play"));
                    EditorGUILayout.PropertyField(_showHistoryButton, new GUIContent("Show History Button"));
                    EditorGUILayout.PropertyField(_showLanguageButton, new GUIContent("Show Settings Button"));
                    EditorGUILayout.PropertyField(_showContinueIndicator, new GUIContent("Show Continue Indicator"));
                    EditorGUILayout.PropertyField(_showAutoSkipIcon, new GUIContent("Show Auto/Skip Icon"));
                    EditorGUILayout.PropertyField(_showChoicePanel, new GUIContent("Show Choice Panel"));
                    EditorGUILayout.PropertyField(_showHistoryPanel, new GUIContent("Show History Panel"));
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.PropertyField(_showSettingsPanel, new GUIContent("Show Runtime Settings Panel"));
                    using (new EditorGUI.DisabledScope(!_showSettingsPanel.boolValue))
                    {
                        EditorGUILayout.PropertyField(_showSettingsLanguageDropdown, new GUIContent("Show Language Selector"));
                        EditorGUILayout.PropertyField(_showSettingsThemeDropdown, new GUIContent("Show Theme Selector"));
                        EditorGUILayout.PropertyField(_showSettingsTextSpeed, new GUIContent("Show Text Speed"));
                        EditorGUILayout.PropertyField(_showSettingsAutoAdvance, new GUIContent("Show Auto-Advance"));
                        EditorGUILayout.PropertyField(_showSettingsVoiceVolume, new GUIContent("Show Voice Volume"));
                        EditorGUILayout.PropertyField(_showSettingsSfxVolume, new GUIContent("Show SFX Volume"));
                    }
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawRuntimeStatus(DialogUIManager manager)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var themeName = manager.CurrentTheme != null ? manager.CurrentTheme.themeName : "none";
            var canvasName = manager.RuntimeDialogCanvasInstance != null ? manager.RuntimeDialogCanvasInstance.name : "none";
            EditorGUILayout.HelpBox($"Active theme: {themeName}\nRuntime canvas: {canvasName}", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Previous Theme", GUILayout.Height(24f)))
                {
                    manager.PreviousTheme();
                }

                if (GUILayout.Button("Next Theme", GUILayout.Height(24f)))
                {
                    manager.NextTheme();
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(DialogUIManager), false);
            }

            EditorGUILayout.Space(4f);
        }

        private static void DrawValidation(DialogUIManager manager)
        {
            if (!manager.HasDialogCanvasPrefab() && manager.CurrentDialogUI == null)
            {
                EditorGUILayout.HelpBox("Missing canvas source. Use an embedded canvas under DialogUIManager, assign a canvas UI prefab, or provide an existing dialog UI reference.", MessageType.Error);
            }

            if (!manager.HasConfiguredDefaultTheme())
            {
                EditorGUILayout.HelpBox("Missing default theme. DialogThemeType.Default cannot resolve until a default theme is assigned.", MessageType.Error);
            }

            if (!manager.HasResolvedMainDialogUiPrefab())
            {
                EditorGUILayout.HelpBox("Missing main dialogue UI prefab. Assign a canvas prefab containing DialogUIController, a dedicated main dialogue UI prefab, or an existing scene DialogUIController.", MessageType.Error);
            }

            if (!manager.HasChoicePrefabFallback())
            {
                EditorGUILayout.HelpBox("Missing choice prefab fallback. At least one fallback choice prefab should be available when a theme does not provide its own override.", MessageType.Warning);
            }

            var duplicates = manager.GetDuplicateThemeMappingCounts();
            var duplicateTypes = new List<string>();
            foreach (var pair in duplicates)
            {
                if (pair.Value > 1)
                {
                    duplicateTypes.Add(pair.Key.ToString());
                }
            }

            if (duplicateTypes.Count > 0)
            {
                EditorGUILayout.HelpBox($"Duplicate theme enum mappings detected: {string.Join(", ", duplicateTypes)}. Keep one mapping per DialogThemeType.", MessageType.Warning);
            }
        }
    }
}
#endif
