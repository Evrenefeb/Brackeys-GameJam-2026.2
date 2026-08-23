using System;
using System.Linq;
using DialogSystem.EditorTools.Settings;
using DialogSystem.EditorTools.Settings.Panels;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows.Tabs
{
    /// <summary>
    /// Settings tab in <see cref="DialogSystemMainWindow"/>.
    /// Embeds the existing <see cref="BasePanel"/> subclasses with left sub-navigation,
    /// reusing the same panel structure as <c>DialogSettingsEditorWindow</c>.
    /// </summary>
    public class SettingsTabController
    {
        #region ---------------- Constants ----------------

        private const string RES_DIR   = "Assets/DialogGraphSystem/Resources/DialogSettingsSO";
        private const string MASTER_PATH = RES_DIR + "/DialogSystemSettings.asset";

        #endregion

        #region ---------------- Settings Panels ----------------

        private enum SettingsPanel { Text, Choices, Audio, RuntimeUI, GraphView, AI, About }

        #endregion

        #region ---------------- State ----------------

        private VisualElement _root;
        private VisualElement _navRoot;
        private VisualElement _contentArea;

        private DialogSystemSettings _master;
        private SerializedObject     _masterSo;

        private SettingsPanel _activePanel = SettingsPanel.Text;

        #endregion

        #region ---------------- Public API ----------------

        /// <summary>Builds the Settings tab content into the given root.</summary>
        public void BuildUI(VisualElement root)
        {
            _root = root;
            _root.Clear();

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.SETTINGS_STYLE_PATH);
            if (ss != null && !_root.styleSheets.Contains(ss))
                _root.styleSheets.Add(ss);

            LoadOrCreateMaster();

            // ── Body: left nav + content ──────────────────────────────────
            var body = new VisualElement();
            body.AddToClassList("ds-settings-body");
            _root.Add(body);

            // Left sub-nav
            _navRoot = new VisualElement();
            _navRoot.AddToClassList("ds-settings-nav");
            body.Add(_navRoot);

            // Content area
            _contentArea = new VisualElement();
            _contentArea.AddToClassList("ds-settings-content");
            body.Add(_contentArea);

            BuildNavigation();
            ShowPanel(_activePanel);
        }

        #endregion

        #region ---------------- Load Settings ----------------

        private void LoadOrCreateMaster()
        {
            _master = AssetDatabase.LoadAssetAtPath<DialogSystemSettings>(MASTER_PATH);

            if (_master == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/DialogGraphSystem/Resources"))
                    AssetDatabase.CreateFolder("Assets/DialogGraphSystem", "Resources");
                if (!AssetDatabase.IsValidFolder(RES_DIR))
                    AssetDatabase.CreateFolder("Assets/DialogGraphSystem/Resources", "DialogSettingsSO");

                _master = ScriptableObject.CreateInstance<DialogSystemSettings>();
                AssetDatabase.CreateAsset(_master, MASTER_PATH);

                _master.textSettings   = CreateSubAsset<DialogTextSettings>("TextSettings");
                _master.choiceSettings = CreateSubAsset<DialogChoiceSettings>("ChoiceSettings");
                _master.inputSettings  = CreateSubAsset<DialogInputSettings>("InputSettings");
                _master.audioSettings  = CreateSubAsset<DialogAudioSettings>("AudioSettings");
                _master.uiSettings     = CreateSubAsset<DialogueRuntimeUISettings>("RuntimeUISettings");

                EditorUtility.SetDirty(_master);
                AssetDatabase.SaveAssets();
            }
            else
            {
                if (_master.textSettings   == null) _master.textSettings   = CreateSubAsset<DialogTextSettings>("TextSettings");
                if (_master.choiceSettings == null) _master.choiceSettings = CreateSubAsset<DialogChoiceSettings>("ChoiceSettings");
                if (_master.inputSettings  == null) _master.inputSettings  = CreateSubAsset<DialogInputSettings>("InputSettings");
                if (_master.audioSettings  == null) _master.audioSettings  = CreateSubAsset<DialogAudioSettings>("AudioSettings");
                if (_master.uiSettings     == null) _master.uiSettings     = CreateSubAsset<DialogueRuntimeUISettings>("RuntimeUISettings");
                EditorUtility.SetDirty(_master);
                AssetDatabase.SaveAssets();
            }

            _masterSo = new SerializedObject(_master);
        }

        private T CreateSubAsset<T>(string assetName) where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            obj.name = assetName;
            AssetDatabase.AddObjectToAsset(obj, _master);
            return obj;
        }

        #endregion

        #region ---------------- Navigation ----------------

        private void BuildNavigation()
        {
            _navRoot.Clear();

            AddNavBtn("Text",       SettingsPanel.Text);
            AddNavBtn("Choices",    SettingsPanel.Choices);
            AddNavBtn("Audio",      SettingsPanel.Audio);
            AddNavBtn("Runtime UI", SettingsPanel.RuntimeUI);
            AddNavBtn("Graph View", SettingsPanel.GraphView);
            if (HasAiSettingsPanel())
            {
                AddNavBtn("AI", SettingsPanel.AI);
            }

            // spacer = new VisualElement { style = { flexGrow = 1 } };
            //_navRoot.Add(spacer);

            AddNavBtn("About", SettingsPanel.About);
        }

        private void AddNavBtn(string label, SettingsPanel panel)
        {
            var btn = new Button(() => SwitchPanel(panel)) { text = label };
            btn.AddToClassList("ds-settings-nav-btn");
            if (_activePanel == panel) btn.AddToClassList("active");
            btn.userData = panel;
            _navRoot.Add(btn);
        }

        private void SwitchPanel(SettingsPanel panel)
        {
            _activePanel = panel;

            foreach (var child in _navRoot.Children())
            {
                if (child is Button btn)
                    btn.EnableInClassList("active", btn.userData is SettingsPanel p && p == _activePanel);
            }

            ShowPanel(_activePanel);
        }

        private void ShowPanel(SettingsPanel panel)
        {
            _contentArea.Clear();

            BasePanel p = panel switch
            {
                SettingsPanel.Text      => new TextPanel(),
                SettingsPanel.Choices   => new ChoicePanel(),
                SettingsPanel.Audio     => new AudioPanel(),
                SettingsPanel.RuntimeUI => new RuntimeUIPanel(),
                SettingsPanel.GraphView => new GraphViewPanel(),
                SettingsPanel.AI        => CreateAiSettingsPanel() ?? new AboutPanel(),
                SettingsPanel.About     => new AboutPanel(),
                _                       => new AboutPanel()
            };

            p.BuildUI(_masterSo);
            _contentArea.Add(p);
        }

        public void OpenPanel(string panelKey)
        {
            if (string.IsNullOrWhiteSpace(panelKey))
            {
                return;
            }

            if (string.Equals(panelKey, "AI", System.StringComparison.OrdinalIgnoreCase) &&
                HasAiSettingsPanel())
            {
                SwitchPanel(SettingsPanel.AI);
            }
        }

        private static bool HasAiSettingsPanel() => ResolveAiSettingsPanelType() != null;

        private static BasePanel CreateAiSettingsPanel()
        {
            var panelType = ResolveAiSettingsPanelType();
            return panelType != null
                ? Activator.CreateInstance(panelType) as BasePanel
                : null;
        }

        private static Type ResolveAiSettingsPanelType()
        {
            const string typeName = "DialogSystem.EditorTools.Settings.Panels.GraphAiSettingsPanel";
            const string assemblyName = "DialogSystemAIExtension.Editor";

            return Type.GetType($"{typeName}, {assemblyName}", throwOnError: false)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                    .FirstOrDefault(type => type != null);
        }

        #endregion
    }
}
