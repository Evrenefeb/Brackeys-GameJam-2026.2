using System;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Windows.Tabs;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Utils;
using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Unified main editor window for the Dialogue Graph System.
    /// Replaces all separate windows with a single horizontal tab bar.
    /// </summary>
    public class DialogSystemMainWindow : EditorWindow
    {
        #region ---------------- Constants ----------------

        private const string PrefKeyLastTab = "DialogSystem_LastTab";
        private const string TabBarIconAssetPath = "Assets/DialogGraphSystem/Resources/Brand/Icons/beka-forge-logo-64.png";

        #endregion

        #region ---------------- Tab Enum ----------------

        public enum TabType { Launcher, Graphs, Characters, Actions, Variables, Context, Localization, ImportExport, Settings }

        #endregion

        #region ---------------- State ----------------

        private TabType _activeTab = TabType.Launcher;

        private VisualElement _tabBar;
        private VisualElement _contentContainer;

        // Tab controllers (created once and reused)
        private GraphTabController       _graphTab;
        private CharactersTabController  _charactersTab;
        private ActionsTabController     _actionsTab;
        private VariablesTabController   _variablesTab;
        private ContextTabController     _contextTab;
        private LocalizationTabController _localizationTab;
        private ImportExportTabController _importExportTab;
        private SettingsTabController    _settingsTab;
        private DialogGraphAiFloatingPanel _sharedAiFloatingPanel;
        private Button _sharedAiLauncherBtn;

        #endregion

        #region ---------------- Menu ----------------

        /// <summary>Single entry point for the entire Dialogue System editor.</summary>
        [MenuItem(TextResources.MENU_DIALOGUE_GRAPH_SYSTEM_ROOT, priority = 0)]
        public static void Open()
        {
            OpenLauncher();
        }

        public static void OpenLauncher()
        {
            var window = OpenWindow();
            window.SwitchTab(TabType.Launcher);
        }

        public static void OpenGraphs()
        {
            var window = OpenWindow();
            window.SwitchTab(TabType.Graphs);
        }

        public static void OpenTab(TabType tab, bool forceRefresh = false)
        {
            var window = OpenWindow();
            window.SwitchTab(tab, forceRefresh);
            window.Focus();
        }

        //[MenuItem(TextResources.MENU_DIALOGUE_GRAPH_SYSTEM_LOCALIZATION, priority = 2)]
        public static void OpenLocalizationTab()
        {
            var window = OpenWindow();
            window.SwitchTab(TabType.Localization);
        }

        public static void OpenLocalizationForGraph(DialogGraph graph)
        {
            var window = OpenWindow();
            if (window._localizationTab == null)
            {
                window._localizationTab = new LocalizationTabController();
            }

            window._localizationTab.SelectGraph(graph);
            window.SwitchTab(TabType.Localization, forceRefresh: true);
            window._localizationTab.SelectGraph(graph);
        }

        public static void OpenSettingsTab()
        {
            OpenSettingsTab(null);
        }

        public static void OpenSettingsTab(string settingsPanelKey)
        {
            var window = OpenWindow();
            window.SwitchTab(TabType.Settings);
            window._settingsTab?.OpenPanel(settingsPanelKey);
        }

        /// <summary>Opens the main window and immediately loads the named graph in the Graph tab.</summary>
        public static void OpenWithGraph(string graphName)
        {
            var window = OpenWindow();
            window.Focus();
            // Ensure Graphs tab is active then load the graph
            window._activeTab = TabType.Graphs;
            if (window._graphTab == null)
                window._graphTab = new GraphTabController(window);
            window._graphTab.LoadGraphByName(graphName);
            window.SwitchTab(TabType.Graphs);
        }

        private static DialogSystemMainWindow OpenWindow()
        {
            var window = GetWindow<DialogSystemMainWindow>();
            window.titleContent = new GUIContent("Dialogue Graph System");
            window.minSize = new Vector2(900f, 600f);
            window.Show();
            return window;
        }

        #endregion

        #region ---------------- Unity ----------------

        private void OnEnable()
        {
            _activeTab = (TabType)EditorPrefs.GetInt(PrefKeyLastTab, (int)TabType.Launcher);
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            BuildUI();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            _graphTab?.SaveActiveGraphForWindowClose();
            EditorPrefs.SetInt(PrefKeyLastTab, (int)_activeTab);
        }

        private void OnUndoRedoPerformed()
        {
            if (_activeTab == TabType.Graphs)
            {
                _graphTab?.HandleUndoRedo();
            }
            else
            {
                SwitchTab(_activeTab, forceRefresh: true);
            }

            Repaint();
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt == null || IsTextInputFocused())
            {
                return;
            }

            if (evt.actionKey && !evt.shiftKey && evt.keyCode == KeyCode.Z)
            {
                Undo.PerformUndo();
                evt.StopImmediatePropagation();
                return;
            }

            var isRedoKey =
                (evt.actionKey && evt.keyCode == KeyCode.Y) ||
                (evt.actionKey && evt.shiftKey && evt.keyCode == KeyCode.Z) ||
                (evt.actionKey && evt.shiftKey && evt.keyCode == KeyCode.R);

            if (!isRedoKey)
            {
                return;
            }

            Undo.PerformRedo();
            evt.StopImmediatePropagation();
        }

        private bool IsTextInputFocused()
        {
            var focusedElement = rootVisualElement?.panel?.focusController?.focusedElement as VisualElement;
            if (focusedElement == null)
            {
                return false;
            }

            if (focusedElement is TextField)
            {
                return true;
            }

            var typeName = focusedElement.GetType().Name;
            return typeName.IndexOf("TextField", StringComparison.OrdinalIgnoreCase) >= 0
                   || typeName.IndexOf("TextInput", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region ---------------- Build UI ----------------

        private void BuildUI()
        {
            rootVisualElement.Clear();

            var mainSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.MAIN_WINDOW_STYLE_PATH);
            if (mainSs != null) rootVisualElement.styleSheets.Add(mainSs);

            var settingsSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.SETTINGS_STYLE_PATH);
            if (settingsSs != null) rootVisualElement.styleSheets.Add(settingsSs);

            var graphSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (graphSs != null) rootVisualElement.styleSheets.Add(graphSs);

            rootVisualElement.AddToClassList("ds-root");

            // ── Tab Bar ──────────────────────────────────────────────────
            _tabBar = new VisualElement();
            _tabBar.AddToClassList("ds-tab-bar");
            rootVisualElement.Add(_tabBar);

            BuildTabBar();

            // ── Content Container ────────────────────────────────────────
            _contentContainer = new VisualElement();
            _contentContainer.AddToClassList("ds-content");
            rootVisualElement.Add(_contentContainer);

            // ── Initialize tab controllers ───────────────────────────────
            _graphTab        = new GraphTabController(this);
            _charactersTab   = new CharactersTabController(GetGraphOwner);
            _actionsTab      = new ActionsTabController();
            _variablesTab    = new VariablesTabController();
            _contextTab      = new ContextTabController();
            _localizationTab = new LocalizationTabController();
            _importExportTab = new ImportExportTabController();
            _settingsTab     = new SettingsTabController();

            ShowTab(_activeTab);
        }

        private void BuildTabBar()
        {
            _tabBar.Clear();

            AddTabBarIcon();
            AddTabButton("Launcher",       TabType.Launcher);
            AddTabButton("Graphs",         TabType.Graphs);
            AddTabButton("Characters",     TabType.Characters);
            AddTabButton("Actions",        TabType.Actions);
            AddTabButton("Variables",      TabType.Variables);
            AddTabButton("Context",        TabType.Context);
            AddTabButton("Localization",   TabType.Localization);
            AddTabButton("Import / Export", TabType.ImportExport);
            AddTabButton("Settings",       TabType.Settings);
        }

        private void AddTabBarIcon()
        {
            var favicon = AssetDatabase.LoadAssetAtPath<Texture2D>(TabBarIconAssetPath);
            if (favicon == null)
            {
                return;
            }

            var icon = new Image
            {
                image = favicon,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };

            icon.AddToClassList("ds-tab-bar-icon");
            _tabBar.Add(icon);
        }

        private void AddTabButton(string label, TabType tab)
        {
            var btn = new Button(() => SwitchTab(tab)) { text = label };
            btn.AddToClassList("ds-tab-button");
            if (_activeTab == tab) btn.AddToClassList("ds-tab-button--active");
            btn.userData = tab;
            _tabBar.Add(btn);
        }

        #endregion

        #region ---------------- Tab Switching ----------------

        /// <summary>Switches the active tab, refreshing the tab bar and content area.</summary>
        public void SwitchTab(TabType tab, bool forceRefresh = false)
        {
            if (!forceRefresh && _activeTab == tab && _contentContainer.childCount > 0) return;

            _activeTab = tab;
            EditorPrefs.SetInt(PrefKeyLastTab, (int)_activeTab);

            BuildTabBar();
            ShowTab(_activeTab);
            Repaint();
        }

        private void ShowTab(TabType tab)
        {
            _contentContainer.Clear();

            switch (tab)
            {
                case TabType.Launcher:
                    _graphTab.BuildLauncherUI(_contentContainer);
                    break;
                case TabType.Graphs:
                    _graphTab.BuildWorkspaceUI(_contentContainer);
                    break;
                case TabType.Characters:
                    _charactersTab.BuildUI(_contentContainer);
                    break;
                case TabType.Actions:
                    _actionsTab.BuildUI(_contentContainer);
                    break;
                case TabType.Variables:
                    _variablesTab.BuildUI(_contentContainer);
                    break;
                case TabType.Context:
                    _contextTab.BuildUI(_contentContainer);
                    break;
                case TabType.Localization:
                    _localizationTab.BuildUI(_contentContainer);
                    break;
                case TabType.ImportExport:
                    _importExportTab.BuildUI(_contentContainer);
                    break;
                case TabType.Settings:
                    _settingsTab.BuildUI(_contentContainer);
                    break;
            }

            AttachSharedAiOverlayIfNeeded(tab);
        }

        private void AttachSharedAiOverlayIfNeeded(TabType tab)
        {
            if (!ShouldShowSharedAiOverlay(tab))
            {
                return;
            }

            EnsureSharedAiOverlay();

            if (_sharedAiFloatingPanel.parent != _contentContainer)
            {
                _contentContainer.Add(_sharedAiFloatingPanel);
            }

            if (_sharedAiLauncherBtn.parent != _contentContainer)
            {
                _contentContainer.Add(_sharedAiLauncherBtn);
            }
        }

        private static bool ShouldShowSharedAiOverlay(TabType tab)
        {
            switch (tab)
            {
                case TabType.Characters:
                case TabType.Actions:
                case TabType.Variables:
                case TabType.Context:
                    return true;
                default:
                    return false;
            }
        }

        private void EnsureSharedAiOverlay()
        {
            if (_sharedAiFloatingPanel != null && _sharedAiLauncherBtn != null)
            {
                return;
            }

            VisualElement aiContent = null;
            Action openSettings = null;

            var bridge = DialogGraphAiBridgeLocator.Current;
            if (bridge != null && bridge.IsAvailable)
            {
                aiContent = bridge.CreateAiSidebarContent(_graphTab);
                openSettings = bridge.OpenSettings;
            }

            _sharedAiFloatingPanel = new DialogGraphAiFloatingPanel(aiContent, openSettings);

            _sharedAiLauncherBtn = new Button(() => _sharedAiFloatingPanel?.Toggle())
            {
                tooltip = "Toggle AI Assistant panel"
            };
            _sharedAiLauncherBtn.AddToClassList("dlg-ai-launcher");

            var icon = new Label("✦");
            icon.AddToClassList("dlg-ai-launcher-icon");
            _sharedAiLauncherBtn.Add(icon);

            var label = new Label("AI");
            label.AddToClassList("dlg-ai-launcher-label");
            _sharedAiLauncherBtn.Add(label);
        }

        #endregion

        #region ---------------- Public API ----------------

        /// <summary>Loads a graph into the Graphs tab and switches to it.</summary>
        public void LoadGraph(DialogGraph graph)
        {
            SwitchTab(TabType.Graphs, forceRefresh: true);
            _graphTab.LoadGraph(graph);
        }

        /// <summary>Returns the active graph owner used by shared graph tooling.</summary>
        public IDialogGraphOwner GetGraphOwner() => _graphTab;

        /// <summary>
        /// Creates a character asset through the Characters tab controller,
        /// ensuring the tab UI refreshes and selects the new asset.
        /// </summary>
        public static DialogCharacterSO CreateCharacterAndSelectInTab(
            string displayName,
            string description = null,
            string speechStyle = null,
            string[] personalityTraits = null)
        {
            var window = OpenWindow();
            window.SwitchTab(TabType.Characters, forceRefresh: true);
            if (window._charactersTab == null)
            {
                window._charactersTab = new CharactersTabController(window.GetGraphOwner);
                window._charactersTab.BuildUI(window._contentContainer);
            }

            return window._charactersTab.CreateAndSelectCharacter(
                displayName, description, speechStyle, personalityTraits);
        }

        /// <summary>
        /// Creates an action asset through the Actions tab controller,
        /// ensuring the tab UI refreshes and selects the new asset.
        /// </summary>
        public static DialogActionSO CreateActionAndSelectInTab(
            string actionLabel,
            string description = null)
        {
            var window = OpenWindow();
            window.SwitchTab(TabType.Actions, forceRefresh: true);
            if (window._actionsTab == null)
            {
                window._actionsTab = new ActionsTabController();
                window._actionsTab.BuildUI(window._contentContainer);
            }

            return window._actionsTab.CreateAndSelectAction(actionLabel, description);
        }

        /// <summary>
        /// Creates an environment asset through the Context tab controller,
        /// ensuring the tab UI refreshes and selects the new asset.
        /// </summary>
        public static DialogEnvironmentSO CreateEnvironmentAndSelectInTab(
            string displayName,
            string description = null,
            string atmosphere = null,
            string tone = null)
        {
            var window = OpenWindow();
            window.SwitchTab(TabType.Context, forceRefresh: true);
            if (window._contextTab == null)
            {
                window._contextTab = new ContextTabController();
                window._contextTab.BuildUI(window._contentContainer);
            }

            return window._contextTab.CreateAndSelectEnvironment(
                displayName, description, atmosphere, tone);
        }

        /// <summary>
        /// Creates a variable asset through the Variables tab controller,
        /// ensuring the tab UI refreshes and selects the new asset.
        /// </summary>
        public static DialogVariableSO CreateVariableAndSelectInTab(
            string variableLabel,
            DialogueVariableValueType valueType = DialogueVariableValueType.Boolean,
            string defaultValue = null)
        {
            var window = OpenWindow();
            window.SwitchTab(TabType.Variables, forceRefresh: true);
            if (window._variablesTab == null)
            {
                window._variablesTab = new VariablesTabController();
                window._variablesTab.BuildUI(window._contentContainer);
            }

            return window._variablesTab.CreateAndSelectVariable(
                variableLabel, valueType, defaultValue);
        }

        /// <summary>
        /// Creates a scene context asset through the Context tab controller,
        /// ensuring the tab UI refreshes and selects the new asset.
        /// </summary>
        public static DialogSceneContextSO CreateSceneContextAndSelectInTab(
            string assetName,
            DialogEnvironmentSO environment = null,
            string sceneGoal = null,
            string tone = null,
            string extraRules = null)
        {
            var window = OpenWindow();
            window.SwitchTab(TabType.Context, forceRefresh: true);
            if (window._contextTab == null)
            {
                window._contextTab = new ContextTabController();
                window._contextTab.BuildUI(window._contentContainer);
            }

            return window._contextTab.CreateAndSelectSceneContext(
                assetName, environment, sceneGoal, tone, extraRules);
        }

        public static void NotifyHomeBrowserDataChanged()
        {
            var windows = UnityEngine.Resources.FindObjectsOfTypeAll<DialogSystemMainWindow>();
            if (windows == null || windows.Length == 0)
            {
                return;
            }

            foreach (var window in windows)
            {
                window?._graphTab?.RefreshHomeBrowserIfVisible();
                window?.Repaint();
            }
        }

        #endregion
    }
}
