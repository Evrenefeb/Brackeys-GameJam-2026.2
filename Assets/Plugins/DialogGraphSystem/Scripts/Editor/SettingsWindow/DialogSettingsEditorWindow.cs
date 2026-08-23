using System;
using System.Linq;
using DialogSystem.EditorTools.Settings.Panels;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.EditorTools.Resources;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings
{
    /// <summary>
    /// Main settings window with side navigation and card-based panels.
    /// </summary>
    public class DialogSettingsEditorWindow : EditorWindow
    {
        #region ---------------- Constants ----------------
        private const string RES_DIR = "Assets/DialogGraphSystem/Resources/DialogSettingsSO";
        private const string MASTER_PATH = RES_DIR + "/DialogSystemSettings.asset";
        #endregion

        #region ---------------- State ----------------
        private DialogSystemSettings _master;
        private SerializedObject _masterSo;

        private VisualElement _header;
        private Label _breadcrumbLabel;
        private VisualElement _body;
        private ScrollView _navScrollView;
        private VisualElement _navRoot;
        private VisualElement _contentRoot;

        private enum Tab { Text, Choices, Audio, RuntimeUI, GraphView, AI, About }
        private Tab _activeTab = Tab.Text;
        #endregion

        #region ---------------- Menu ----------------
        //[MenuItem(TextResources.MENU_DIALOGUE_GRAPH_SYSTEM_SETTINGS, priority = 100)]
        private static void OpenFromLegacyMenu()
        {
            DialogSystemMainWindow.OpenSettingsTab();
        }

        [System.Obsolete("Use DialogSystemMainWindow Settings tab instead.")]
        public static void Open()
        {
            DialogSystemMainWindow.OpenSettingsTab();
        }

        #endregion

        #region ---------------- Unity ----------------
        private void OnEnable()
        {
            LoadOrCreateMaster();
            BuildUI();
            RefreshContent();
        }
        #endregion

        #region ---------------- Load/Create ----------------
        private void LoadOrCreateMaster()
        {
            _master = AssetDatabase.LoadAssetAtPath<DialogSystemSettings>(MASTER_PATH);

            if (_master == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/DialogGraphSystem/Resources"))
                    AssetDatabase.CreateFolder("Assets/DialogGraphSystem", "Resources");
                if (!AssetDatabase.IsValidFolder("Assets/DialogGraphSystem/Resources/DialogSettingsSO"))
                    AssetDatabase.CreateFolder("Assets/DialogGraphSystem/Resources", "DialogSettingsSO");

                _master = CreateInstance<DialogSystemSettings>();
                AssetDatabase.CreateAsset(_master, MASTER_PATH);

                _master.textSettings = CreateSubAsset<DialogTextSettings>("TextSettings");
                _master.choiceSettings = CreateSubAsset<DialogChoiceSettings>("ChoiceSettings");
                _master.inputSettings = CreateSubAsset<DialogInputSettings>("InputSettings");
                _master.audioSettings = CreateSubAsset<DialogAudioSettings>("AudioSettings");
                _master.uiSettings = CreateSubAsset<DialogueRuntimeUISettings>("RuntimeUISettings");

                EditorUtility.SetDirty(_master);
                AssetDatabase.SaveAssets();
            }
            else
            {
                if (_master.textSettings == null) _master.textSettings = CreateSubAsset<DialogTextSettings>("TextSettings");
                if (_master.choiceSettings == null) _master.choiceSettings = CreateSubAsset<DialogChoiceSettings>("ChoiceSettings");
                if (_master.inputSettings == null) _master.inputSettings = CreateSubAsset<DialogInputSettings>("InputSettings");
                if (_master.audioSettings == null) _master.audioSettings = CreateSubAsset<DialogAudioSettings>("AudioSettings");
                if (_master.uiSettings == null) _master.uiSettings = CreateSubAsset<DialogueRuntimeUISettings>("RuntimeUISettings");
                EditorUtility.SetDirty(_master);
                AssetDatabase.SaveAssets();
            }

            _masterSo = new SerializedObject(_master);
        }

        private T CreateSubAsset<T>(string name) where T : ScriptableObject
        {
            var obj = CreateInstance<T>();
            obj.name = name;
            AssetDatabase.AddObjectToAsset(obj, _master);
            return obj;
        }
        #endregion

        #region ---------------- UI ----------------
        private void BuildUI()
        {
            rootVisualElement.Clear();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.SETTINGS_STYLE_PATH);
            if (styleSheet != null) rootVisualElement.styleSheets.Add(styleSheet);

            rootVisualElement.AddToClassList("dgs-root");

            // ========== HEADER ==========
            _header = BuildHeader();
            rootVisualElement.Add(_header);

            // ========== BODY ==========
            _body = new VisualElement();
            _body.AddToClassList("dgs-body");
            rootVisualElement.Add(_body);

            // ========== NAVIGATION ==========
            var navContainer = BuildNavigation();
            _body.Add(navContainer);

            // ========== CONTENT ==========
            _contentRoot = new VisualElement();
            _contentRoot.AddToClassList("dgs-content");
            _body.Add(_contentRoot);
        }

        private VisualElement BuildHeader()
        {
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("dgs-header");

            // Left section
            var leftSection = new VisualElement();
            leftSection.AddToClassList("dgs-header-left");

            _breadcrumbLabel = new Label($"Dialogue Graph System Settings  >  {GetTabTitle(_activeTab)}");
            _breadcrumbLabel.AddToClassList("dgs-breadcrumb");
            leftSection.Add(_breadcrumbLabel);

            headerContainer.Add(leftSection);

            // Right section
            var rightSection = new VisualElement();
            rightSection.AddToClassList("dgs-header-right");

            var versionLabel = new Label($"v{_master.version}");
            versionLabel.AddToClassList("dgs-version");
            rightSection.Add(versionLabel);

            headerContainer.Add(rightSection);

            return headerContainer;
        }

        private VisualElement BuildNavigation()
        {
            // Navigation container
            _navRoot = new VisualElement();
            _navRoot.AddToClassList("dgs-nav");

            // ScrollView for navigation
            _navScrollView = new ScrollView(ScrollViewMode.Vertical);
            _navScrollView.AddToClassList("dgs-nav-scroll");
            _navScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _navScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;

            // Navigation title
            var navTitle = new Label("DIALOGUE GRAPH SYSTEM");
            navTitle.AddToClassList("dgs-nav-title");
            _navScrollView.Add(navTitle);

            // Core settings
            AddNavButton("Text", Tab.Text, DialogGraphIconId.NodeDialog);
            AddNavButton("Choices", Tab.Choices, DialogGraphIconId.NodeChoice);
            AddNavButton("Audio", Tab.Audio, DialogGraphIconId.ToolbarPreview);
            AddNavButton("Runtime UI", Tab.RuntimeUI, DialogGraphIconId.ToolbarSettings);
            AddNavButton("Graph View", Tab.GraphView, DialogGraphIconId.ToolbarPreview);
            if (HasAiSettingsPanel())
            {
                AddNavButton("AI", Tab.AI, DialogGraphIconId.AiRewrite);
            }

            // Spacer
            var spacer = new VisualElement();
            spacer.style.height = 6;
            _navScrollView.Add(spacer);

            // About button
            AddNavButton("About", Tab.About, DialogGraphIconId.StatusInfo);

            _navRoot.Add(_navScrollView);
            return _navRoot;
        }

        private void AddNavButton(string label, Tab tab, DialogGraphIconId iconId)
        {
            var btn = new Button(() => SwitchTab(tab));
            btn.userData = tab;
            btn.AddToClassList("dgs-nav-button");
            if (_activeTab == tab) btn.AddToClassList("active");

            // Button content container
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;

            // Icon
            var icon = Icon(iconId, "dgs-icon--sm");
            row.Add(icon);

            // Label
            var labelElement = new Label(label);
            labelElement.style.flexGrow = 1;
            row.Add(labelElement);

            btn.Add(row);
            _navScrollView.Add(btn);
        }

        private void SwitchTab(Tab newTab)
        {
            if (_activeTab == newTab) return;

            _activeTab = newTab;
            RefreshContent();
        }

        private void RefreshContent()
        {
            // Update breadcrumb
            _breadcrumbLabel.text = $"Dialogue Graph System Settings  >  {GetTabTitle(_activeTab)}";

            // Update active button state
            foreach (var child in _navScrollView.Children())
            {
                if (child is Button btn)
                {
                    btn.RemoveFromClassList("active");
                    if (btn.userData is Tab buttonTab && buttonTab == _activeTab)
                    {
                        btn.AddToClassList("active");
                    }
                }
            }

            // Clear and rebuild content
            _contentRoot.Clear();

            BasePanel panel = _activeTab switch
            {
                Tab.Text => new TextPanel(),
                Tab.Choices => new ChoicePanel(),
                Tab.Audio => new AudioPanel(),
                Tab.RuntimeUI => new RuntimeUIPanel(),
                Tab.GraphView => new GraphViewPanel(),
                Tab.AI => CreateAiSettingsPanel() ?? new AboutPanel(),
                Tab.About => new AboutPanel(),
                _ => new AboutPanel(),
            };

            panel.BuildUI(_masterSo);
            _contentRoot.Add(panel);
        }

        private static string GetTabTitle(Tab t) => t switch
        {
            Tab.Text => "Text",
            Tab.Choices => "Choices",
            Tab.Audio => "Audio",
            Tab.RuntimeUI => "Runtime UI",
            Tab.GraphView => "Graph View",
            Tab.AI => "AI",
            Tab.About => "About",
            _ => "Unknown"
        };

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
