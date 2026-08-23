using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using DialogSystem.EditorTools.AI;
using DialogSystem.EditorTools.Settings;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.EditorTools.Resources;
using CoreActionRegistryService = DialogSystem.EditorTools.Services.DialogActionRegistryService;
using CoreCharacterRegistryService = DialogSystem.EditorTools.Services.DialogCharacterRegistryService;
using CoreVariableRegistryService = DialogSystem.EditorTools.Services.DialogVariableRegistryService;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Variables;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.Runtime.Utils;
using DialogSystem.EditorTools.Utils;
using UnityEditor.Experimental.GraphView;

namespace DialogSystem.EditorTools.Windows
{
    public class DialogGraphEditorWindow : EditorWindow, IDialogGraphOwner
    {
        #region -------------------- SETTINGS --------------------
        [SerializeField] private bool doDebug = false;

        [SerializeField, Tooltip("Initial width of the Sidebar when shown.")]
        private float initialSidebarWidth = 340f;

        private const string LastGraphPrefKey = "DialogGraph_LastGraphName";
        private const string ViewStatePrefKeyPrefix = "DialogGraph_ViewState_";
        private const string ButtonIconElementName = "button-icon";
        private const string ButtonLabelElementName = "button-label";
        #endregion

        #region -------------------- STATE --------------------
        // Graph + layout
        private DialogGraphView _graphView;
        private VisualElement _toolbarWrapper;
        private TwoPaneSplitView _split;          // graph (left) | sidebar (right)
        private VisualElement _graphHost;         // container to keep GraphView sizing stable
        private VisualElement _rightSidebarRoot;  // sidebar container
        private VisualElement _soloHost;          // used when sidebar is collapsed

        // Toolbar widgets
        private PopupField<string> _graphPopup;
        private PopupField<string> _labelVisibilityPopup;
        private Button _addNodeBtn, _newBtn, _loadBtn, _saveBtn, _undoBtn, _redoBtn, _groupBtn, _clearBtn, _toggleSidebarBtn, _formatBtn, _validateBtn, _toggleMinimapBtn;

        // Sidebar (separate class)
        private EditorSidebarWindow _editorSidebar;

        // Floating AI panel
        private DialogGraphAiFloatingPanel _aiFloatingPanel;
        private Button _aiLauncherBtn;

        // Runtime
        private string _loadedGraphName;
        private bool _sidebarVisible = true;
        private float _sidebarWidthMemo = 440f;
        #endregion

        #region -------------------- VIEW STATE --------------------
        [Serializable]
        private struct GraphViewCameraState
        {
            public Vector3 position;
            public Vector3 scale;
        }
        #endregion

        #region -------------------- STATIC API (Launcher entry) --------------------
        /// <summary>
        /// Called from DialogGraphLauncherWindow. Opens the editor and loads the given graph.
        /// </summary>
        public static void OpenWithGraph(string graphName)
        {
            if (string.IsNullOrWhiteSpace(graphName))
            {
                Debug.LogWarning("[DialogGraphEditorWindow] Cannot open a graph with an empty name.");
                return;
            }

            var window = GetWindow<DialogGraphEditorWindow>();
            window.titleContent = new GUIContent($"Dialogue Graph - {graphName}");
            window.Show();
            window.Focus();
            window.LoadGraphExternal(graphName);
        }

        public static void OpenWithGraph(DialogGraph graph)
        {
            if (graph == null)
                return;

            var window = GetWindow<DialogGraphEditorWindow>();
            window.titleContent = new GUIContent($"Dialogue Graph - {graph.name}");
            window.Show();
            window.Focus();
            window.LoadGraphExternal(graph.name);
        }

        #endregion

        #region -------------------- UNITY --------------------
        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null) rootVisualElement.styleSheets.Add(ss);

            BuildUI();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);

            // Auto-save if there’s a loaded graph
            if (!string.IsNullOrEmpty(_loadedGraphName) && _graphView != null)
            {
                if (DialogGraphAssetPaths.LoadGraphAsset(_loadedGraphName) != null)
                {
                    SaveCurrentGraphViewState();
                    _graphView.SaveGraph(_loadedGraphName);
                }
                else
                {
                    Debug.LogWarning($"[DialogGraphEditorWindow] Skipped window-close save because dialog graph '{_loadedGraphName}' no longer exists.");
                }

                PlayerPrefs.SetString(LastGraphPrefKey, _loadedGraphName);
                PlayerPrefs.Save();
            }

            rootVisualElement.Clear();
        }

        private void OnUndoRedoPerformed()
        {
            if (_graphView == null || string.IsNullOrEmpty(_loadedGraphName))
                return;

            if (!TryCaptureGraphViewState(out var cachedViewState))
                return;

            // Reload the graph data (nodes, edges, etc.) so the view matches Undo state.
            LoadGraph(_loadedGraphName, true);

            // Restore camera (pan/zoom) so Undo does *not* jump back to the Start node.
            RestoreGraphViewState(cachedViewState, 16);

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

            if (evt.actionKey && !evt.shiftKey && evt.keyCode == KeyCode.G)
            {
                _graphView?.GroupSelectedNodes();
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

        #region -------------------- UI BUILD --------------------
        private void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("dlg-graph");

            // Top toolbar
            _toolbarWrapper = new VisualElement();
            _toolbarWrapper.AddToClassList("dlg-toolbar");
            rootVisualElement.Add(_toolbarWrapper);
            GenerateToolbar();

            CreateGraphAndSidebar();
            CreateSplit();

            _sidebarWidthMemo = Mathf.Max(240f, initialSidebarWidth);
            UpdateSidebarToggleText();
        }

        private void CreateGraphAndSidebar()
        {
            // Graph
            _graphView = new DialogGraphView { name = "Dialog Graph" };
            _graphView.GraphOwner = this;
            _graphHost = new VisualElement();
            _graphHost.AddToClassList("dlg-graph-host");
            _graphHost.style.flexGrow = 1;
            _graphHost.Add(_graphView);
            _graphView.StretchToParentSize();

            // ── Floating AI panel overlay ─────────────────────────────
            CreateAiFloatingPanel();

            // Sidebar
            _rightSidebarRoot = new VisualElement();
            _rightSidebarRoot.AddToClassList("dlg-sidebar");

            _editorSidebar = new EditorSidebarWindow(this);
            _rightSidebarRoot.Add(_editorSidebar);

            _graphView?.EnsureStartEndNodes();
        }

        private void CreateAiFloatingPanel()
        {
            // Ask the bridge for the AI content VisualElement — the same content
            // previously shown in the sidebar AI tab. Null if extension not installed.
            VisualElement aiContent   = null;
            Action        openSettings = null;
            var bridge = DialogGraphAiBridgeLocator.Current;
            if (bridge != null && bridge.IsAvailable)
            {
                aiContent    = bridge.CreateAiSidebarContent(this);
                openSettings = bridge.OpenSettings;
            }

            _aiFloatingPanel = new DialogGraphAiFloatingPanel(aiContent, openSettings);
            _graphHost.Add(_aiFloatingPanel);

            // ── AI launcher button – anchored bottom-left ─────────────
            _aiLauncherBtn = new Button(ToggleAiPanel) { tooltip = "Toggle AI Assistant panel" };
            _aiLauncherBtn.AddToClassList("dlg-ai-launcher");

            var btnIcon = new Label("✦");
            btnIcon.AddToClassList("dlg-ai-launcher-icon");
            _aiLauncherBtn.Add(btnIcon);

            var btnLabel = new Label("AI");
            btnLabel.AddToClassList("dlg-ai-launcher-label");
            _aiLauncherBtn.Add(btnLabel);

            _graphHost.Add(_aiLauncherBtn);
        }

        private void ToggleAiPanel()
        {
            _aiFloatingPanel?.Toggle();
        }

        private void CreateSplit()
        {
            _split = new TwoPaneSplitView(1, _sidebarWidthMemo, TwoPaneSplitViewOrientation.Horizontal);
            _split.AddToClassList("dlg-split");
            _split.style.flexGrow = 1;

            _split.Add(_graphHost);
            _split.Add(_rightSidebarRoot);

            rootVisualElement.Add(_split);
        }
        #endregion

        #region -------------------- TOOLBAR --------------------
        private void GenerateToolbar()
        {
            _loadedGraphName = null;

            var graphGroup = CreateToolbarGroup("");
            _toolbarWrapper.Add(graphGroup);

            _graphPopup = CreateGraphPopup();
            graphGroup.Add(_graphPopup);

            _newBtn = CreateButton("New", DialogGraphIconId.ActionAdd, "Create a new empty dialogue graph", OnClickNew, "secondary");
            graphGroup.Add(_newBtn);

            _loadBtn = CreateButton("Load", DialogGraphIconId.ToolbarOpen, "Load the selected dialogue graph", OnClickLoad, "secondary");
            graphGroup.Add(_loadBtn);

            var historyGroup = CreateToolbarGroup("Edit");
            _toolbarWrapper.Add(historyGroup);

            _undoBtn = CreateButton("Undo", null, "Undo the last graph edit (Ctrl+Z)", OnClickUndo, "secondary");
            historyGroup.Add(_undoBtn);

            _redoBtn = CreateButton("Redo", null, "Redo the last undone graph edit (Ctrl+Y or Ctrl+Shift+Z)", OnClickRedo, "secondary");
            historyGroup.Add(_redoBtn);

            _groupBtn = CreateButton("Group", null, "Create a GraphView group from the selected nodes (Ctrl+G)", () => _graphView?.GroupSelectedNodes(), "secondary");
            historyGroup.Add(_groupBtn);

            _toolbarWrapper.Add(MakeSpacer());

            var viewGroup = CreateToolbarGroup("View");
            _toolbarWrapper.Add(viewGroup);

            _labelVisibilityPopup = CreateLabelVisibilityPopup();
            viewGroup.Add(_labelVisibilityPopup);

            _toggleMinimapBtn = CreateButton(
                GetMinimapToggleLabel(),
                DialogGraphIconId.ToolbarAutoLayout,
                "Toggle the minimap overlay",
                OnClickToggleMinimap,
                "secondary"
            );
            viewGroup.Add(_toggleMinimapBtn);

            var layoutGroup = CreateToolbarGroup("");
            _toolbarWrapper.Add(layoutGroup);

            _saveBtn = CreateButton("Save", DialogGraphIconId.ToolbarSave, "Save the current dialogue graph", OnClickSave, "primary");
            layoutGroup.Add(_saveBtn);

            _validateBtn = CreateButton("Validate", DialogGraphIconId.ToolbarValidate, "Validate graph logic and connections", OnClickValidateGraph, "secondary");
            layoutGroup.Add(_validateBtn);

            _formatBtn = CreateButton("Format Layout", DialogGraphIconId.ToolbarAutoLayout, "Reformat linked node positions only", OnClickFormatLayout, "format");
            layoutGroup.Add(_formatBtn);

            var resetGroup = CreateToolbarGroup("");
            _toolbarWrapper.Add(resetGroup);

            _clearBtn = CreateButton("Clear", DialogGraphIconId.ActionDelete, "Clear the current graph after confirmation", OnClickClear, "danger");
            resetGroup.Add(_clearBtn);

            _toggleSidebarBtn = CreateButton("Hide Sidebar", DialogGraphIconId.ActionCollapse, "Show or hide the graph sidebar", ToggleSidebar, "secondary");
            _toggleSidebarBtn.style.marginLeft = 10;

            _toolbarWrapper.Add(_toggleSidebarBtn);
            UpdateActiveGraphStatus();
        }

        private VisualElement CreateToolbarGroup(string label)
        {
            var group = new VisualElement();
            group.AddToClassList("dlg-toolbar-group");

            var groupLabel = new Label(label);
            groupLabel.AddToClassList("dlg-toolbar-group-label");
            group.Add(groupLabel);

            return group;
        }

        private PopupField<string> CreateLabelVisibilityPopup()
        {
            var options = Enum.GetNames(typeof(EdgeLabelVisibility)).ToList();
            var current = DialogGraphEditorSettings.EdgeLabelVisibility.ToString();
            
            var pop = new PopupField<string>("Labels", options, options.IndexOf(current))
            {
                style = { minWidth = 100, maxWidth = 130, marginRight = 0, marginLeft = 0 },
                tooltip = "Set visibility mode for edge connection labels"
            };
            pop.AddToClassList("dlg-popup");
            pop.AddToClassList("tight-label");

            pop.RegisterValueChangedCallback(evt =>
            {
                if (Enum.TryParse<EdgeLabelVisibility>(evt.newValue, out var mode))
                {
                    DialogGraphEditorSettings.EdgeLabelVisibility = mode;
                    DialogGraphEditorSettings.NotifySettingsChanged();
                }
            });

            return pop;
        }

        private PopupField<string> CreateGraphPopup()
{
            var graphNames = GetAllGraphAssetNamesFallback();
            var pop = new PopupField<string>("Dialog", graphNames, graphNames.Count > 0 ? 0 : -1)
            {
                style = { maxWidth = 250, marginRight = 0, marginLeft = 0, flexGrow = 1 },
                tooltip = "Select a graph to load"
            };
            pop.AddToClassList("dlg-popup");
            pop.AddToClassList("tight-label");

            var popLabel = pop.labelElement;
            popLabel.style.width = StyleKeyword.Auto;
            popLabel.style.minWidth = StyleKeyword.Auto;
            popLabel.style.flexShrink = 0;
            popLabel.style.marginRight = 6;

            return pop;
        }

        private VisualElement MakeSpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("dlg-spacer");
            return spacer;
        }

        private Button CreateButton(string label, DialogGraphIconId? iconId, string tooltip, Action onClick, string styleClass = null, bool iconOnly = false)
        {
            var btn = new Button { tooltip = string.IsNullOrEmpty(tooltip) ? label : tooltip };
            btn.AddToClassList("dlg-btn");
            if (!string.IsNullOrEmpty(styleClass)) btn.AddToClassList(styleClass);
            if (onClick != null) btn.clicked += onClick;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;

            if (iconId.HasValue && DialogGraphIconManager.HasIcon(iconId.Value))
            {
                var img = DialogGraphIconManager.CreateImage(iconId.Value, "dgs-icon--md");
                img.name = ButtonIconElementName;
                img.style.marginRight = iconOnly ? 0 : 8;
                row.Add(img);
            }

            var lbl = new Label(iconOnly ? "" : label);
            lbl.name = ButtonLabelElementName;
            lbl.style.flexShrink = 1;
            lbl.style.overflow = Overflow.Hidden;
#if UNITY_2021_3_OR_NEWER
            lbl.style.textOverflow = TextOverflow.Ellipsis;
#endif
            row.Add(lbl);

            btn.text = ""; // avoid default overlay
            btn.Add(row);

            btn.style.minHeight = 26;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 10;

            return btn;
        }
        #endregion

        #region -------------------- TOOLBAR HANDLERS --------------------

        private void OnClickNew()
        {
            var existingNames = GetAllGraphAssetNamesFallback();

            // Pre-pick a collision-free suggestion
            var baseName      = "NewDialogue";
            var suggestedName = baseName;
            var taken         = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
            int suffix        = 2;
            while (taken.Contains(suggestedName))
                suggestedName = $"{baseName}{suffix++}";

            NewGraphPromptWindow.Open(
                suggestedName: suggestedName,
                existingNames: existingNames,
                onConfirm: (graphName, assetFolder) =>
                {
                    // Auto-save current graph before switching away
                    if (!string.IsNullOrEmpty(_loadedGraphName) && _graphView != null)
                    {
                        SaveCurrentGraphViewState();
                        _graphView.SaveGraph(_loadedGraphName);

                        if (doDebug)
                            Debug.Log($"[DialogGraphEditorWindow] Auto-saved '{_loadedGraphName}' before creating '{graphName}'.");
                    }

                    // Reset canvas to blank Start+End
                    _graphView?.ClearGraph();

                    // Create and save the new asset immediately so it exists on disk
                    if (_graphView != null)
                        _graphView.SaveGraph(graphName, assetFolder);

                    _loadedGraphName = graphName;

                    // Refresh the popup list and select the new graph
                    var names = GetAllGraphAssetNamesFallback();
                    if (!names.Contains(graphName))
                    {
                        names.Add(graphName);
                        names = names.Distinct().OrderBy(n => n).ToList();
                    }

                    if (_graphPopup != null)
                    {
                        _graphPopup.choices.Clear();
                        foreach (var n in names)
                            _graphPopup.choices.Add(n);

                        if (_graphPopup.choices.Contains(graphName))
                            _graphPopup.SetValueWithoutNotify(graphName);
                    }

                    _editorSidebar?.ClearAll();
                    titleContent = new GUIContent($"Dialogue Graph - {graphName}");
                    PlayerPrefs.SetString(LastGraphPrefKey, graphName);
                    PlayerPrefs.Save();
                    UpdateActiveGraphStatus();

                    if (doDebug)
                        Debug.Log($"[DialogGraphEditorWindow] Created new graph '{graphName}' at '{assetFolder}'.");
                }
            );
        }

        private void OnClickLoad()
        {
            var selected = _graphPopup != null ? _graphPopup.value : null;
            if (string.IsNullOrEmpty(selected))
            {
                EditorUtility.DisplayDialog("No graph selected", "Please choose a graph from the popup.", "OK");
                return;
            }

            if (_graphView != null && !string.IsNullOrEmpty(_loadedGraphName))
            {
                SaveCurrentGraphViewState();
            }

            LoadGraph(selected, false);
            _loadedGraphName = selected;
            FormatLayout();

            if (doDebug)
                Debug.Log($"[DialogGraphEditorWindow] Loaded graph '{selected}' via toolbar Load.");

            _editorSidebar?.RebuildFromGraph();
            PlayerPrefs.SetString(LastGraphPrefKey, selected);
            PlayerPrefs.Save();
            UpdateActiveGraphStatus();
        }

        private void OnClickSave() => OpenSavePopup();
        private void OnClickUndo() => Undo.PerformUndo();
        private void OnClickRedo() => Undo.PerformRedo();
        private void OnClickClear() => ClearLoadedGraph();
        private void OnClickFormatLayout() => FormatLayout();
        private void OnClickValidateGraph()
        {
            ValidateGraph();
        }

        /// <summary>Runs graph validation and opens the result panel.</summary>
        public void ValidateGraph()
        {
            var result = DialogGraphValidationRunner.ValidateOpenGraph(this);
            if (!_sidebarVisible)
            {
                ExpandSidebar();
            }

            _editorSidebar?.ShowValidationResult(result);
        }

        /// <summary>
        /// Runs the layout formatter on the current graph view, then saves positions
        /// immediately so the result is preserved without a manual Ctrl+S.
        /// Called both by the toolbar button and automatically after AI node insertion.
        /// </summary>
        public void FormatLayout(string preserveNodeGuid = null)
        {
            if (_graphView == null)
                return;

            bool nodePositionsChanged = DialogGraphLayoutFormatterEditor.FormatLinkedSubgraphOnly(
                _graphView, DialogGraphLayoutFormatterEditor.DefaultSettings, preserveNodeGuid);

            // Persist the new positions right away so they survive reload.
            if (nodePositionsChanged && !string.IsNullOrEmpty(_loadedGraphName))
                _graphView.SaveGraph(_loadedGraphName);

            // Refresh visuals
            _graphView.MarkDirtyRepaint();
            _editorSidebar?.RebuildFromGraph();
        }

        #endregion

        #region -------------------- SAVE / LOAD / CLEAR --------------------
        /// <summary>
        /// Entry used by the launcher to load/switch graphs.
        /// Ensures UI, loads graph, updates popup, updates last-graph pref.
        /// </summary>
        internal void LoadGraphExternal(string graphName)
        {
            if (string.IsNullOrWhiteSpace(graphName))
            {
                Debug.LogWarning("[DialogGraphEditorWindow] Cannot load a graph with an empty name.");
                return;
            }

            if (DialogGraphAssetPaths.LoadGraphAsset(graphName) == null)
            {
                Debug.LogWarning($"[DialogGraphEditorWindow] Dialog graph '{graphName}' was not found. Load request ignored.");
                return;
            }

            WarnIfMainWindowAlreadyEditing(graphName);

            // Ensure UI exists
            if (_graphView == null || _toolbarWrapper == null)
            {
                BuildUI();
            }

            // Auto-save currently loaded graph if switching
            if (!string.IsNullOrEmpty(_loadedGraphName) &&
                _loadedGraphName != graphName &&
                _graphView != null)
            {
                SaveCurrentGraphViewState();
                _graphView.SaveGraph(_loadedGraphName);
            }

            LoadGraph(graphName, false);
            _loadedGraphName = graphName;
            FormatLayout();

            // Refresh popup choices and select the requested graph
            var names = GetAllGraphAssetNamesFallback();
            if (!names.Contains(graphName))
            {
                names.Add(graphName);
                names = names.Distinct().OrderBy(n => n).ToList();
            }

            if (_graphPopup != null)
            {
                _graphPopup.choices.Clear();
                foreach (var n in names)
                    _graphPopup.choices.Add(n);

                if (_graphPopup.choices.Contains(graphName))
                    _graphPopup.SetValueWithoutNotify(graphName);
            }

            _editorSidebar?.RebuildFromGraph();

            PlayerPrefs.SetString(LastGraphPrefKey, graphName);
            PlayerPrefs.Save();
            UpdateActiveGraphStatus();

            if (doDebug)
                Debug.Log($"[DialogGraphEditorWindow] Loaded graph '{graphName}' via launcher.");
        }

        private void OpenSavePopup()
        {
            if (!string.IsNullOrWhiteSpace(_loadedGraphName))
            {
                SaveAsset(_loadedGraphName);
                SaveCurrentGraphViewState();

                PlayerPrefs.SetString(LastGraphPrefKey, _loadedGraphName);
                PlayerPrefs.Save();
                UpdateActiveGraphStatus();
                return;
            }

            var existing = GetAllGraphAssetNamesFallback();
            var suggestion = string.IsNullOrEmpty(_loadedGraphName) ? "DialogGraph" : _loadedGraphName;
            bool isEmpty = _graphView != null && _graphView.IsGraphEmptyForSave();
            var previousLoadedGraphName = _loadedGraphName;

            SaveGraphPromptWindow.Open(
                currentName: suggestion,
                loadedGraphName: _loadedGraphName,
                existingNames: existing,
                isGraphEmpty: isEmpty,
                onConfirm: finalName =>
                {
                    SaveAsset(finalName);

                    // Refresh popup choices / selection
                    var namesAfter = GetAllGraphAssetNamesFallback();
                    if (namesAfter.Contains(finalName))
                    {
                        _graphPopup.choices.Clear();
                        foreach (var n in namesAfter)
                            _graphPopup.choices.Add(n);

                        _graphPopup.SetValueWithoutNotify(finalName);
                    }

                    _loadedGraphName = finalName;
                    if (!string.Equals(previousLoadedGraphName, finalName, StringComparison.OrdinalIgnoreCase))
                    {
                        _graphView.LoadGraph(finalName, false, focusStartNode: false);
                    }

                    SaveCurrentGraphViewState();

                    PlayerPrefs.SetString(LastGraphPrefKey, finalName);
                    PlayerPrefs.Save();
                    UpdateActiveGraphStatus();

                    if (doDebug)
                        Debug.Log($"[DialogGraphEditorWindow] Saved graph as '{finalName}'.");
                }
            );
        }

        private void SaveAsset(string fileName) => _graphView.SaveGraph(fileName);

        private void LoadGraph(string fileName, bool onUndo)
        {
            var savedViewState = default(GraphViewCameraState);
            var hasSavedViewState = !onUndo && TryLoadGraphViewState(fileName, out savedViewState);

            _graphView.LoadGraph(fileName, onUndo, focusStartNode: !hasSavedViewState);

            if (hasSavedViewState)
            {
                RestoreGraphViewState(savedViewState, 16);
            }

            _loadedGraphName = fileName;
            _editorSidebar.RebuildFromGraph();
            UpdateActiveGraphStatus();

            if (!onUndo)
            {
                ShowLegacyGraphUpgradeWarning(fileName);
            }

            Debug.Log($"[DialogGraphEditorWindow] Loaded graph '{fileName}'{(onUndo ? " via Undo" : "")}.");
        }

        private void ShowLegacyGraphUpgradeWarning(string graphName)
        {
            var graph = LoadCurrentGraphAsset(createIfMissing: false);
            if (graph == null)
            {
                return;
            }

            DialogGraphUpgradeWarningService.ShowIfNeeded(
                graph,
                onUpgradeSucceeded: () => ReloadGraphAfterSchemaUpgrade(graphName));
        }

        private void ReloadGraphAfterSchemaUpgrade(string graphName)
        {
            if (string.IsNullOrWhiteSpace(graphName) || _graphView == null)
            {
                RefreshGraphEditorState();
                return;
            }

            LoadGraph(graphName, true);
            RefreshGraphEditorState();
        }

        private static string GetViewStatePrefKey(string graphName)
        {
            return ViewStatePrefKeyPrefix + graphName;
        }

        private bool TryCaptureGraphViewState(out GraphViewCameraState state)
        {
            state = default;

            if (_graphView == null)
                return false;

            state = new GraphViewCameraState
            {
                position = _graphView.GetViewPosition(),
                scale = _graphView.GetViewScale()
            };

            return true;
        }

        private void SaveCurrentGraphViewState()
        {
            if (string.IsNullOrEmpty(_loadedGraphName))
                return;

            if (!TryCaptureGraphViewState(out var state))
                return;

            EditorPrefs.SetString(GetViewStatePrefKey(_loadedGraphName), JsonUtility.ToJson(state));
        }

        private bool TryLoadGraphViewState(string graphName, out GraphViewCameraState state)
        {
            state = default;

            if (string.IsNullOrWhiteSpace(graphName))
                return false;

            var key = GetViewStatePrefKey(graphName);
            if (!EditorPrefs.HasKey(key))
                return false;

            var json = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                state = JsonUtility.FromJson<GraphViewCameraState>(json);
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning($"[DialogGraphEditorWindow] Ignoring invalid saved view state for '{graphName}': {ex.Message}");
                EditorPrefs.DeleteKey(key);
                state = default;
                return false;
            }

            return state.scale.x > 0f && state.scale.y > 0f;
        }

        private static void WarnIfMainWindowAlreadyEditing(string graphName)
        {
            var mainWindow = UnityEngine.Resources
                .FindObjectsOfTypeAll<DialogSystemMainWindow>()
                .FirstOrDefault(window =>
                    window?.GetGraphOwner() != null &&
                    string.Equals(window.GetGraphOwner().GetCurrentGraphName(), graphName, StringComparison.Ordinal));

            if (mainWindow == null)
            {
                return;
            }

            Debug.LogWarning(
                $"[DialogGraphEditorWindow] Graph '{graphName}' is already open in the unified graph workspace. " +
                "Editing the same graph in multiple windows can overwrite view state or recent unsaved changes.",
                mainWindow);
        }

        private void RestoreGraphViewState(GraphViewCameraState state, int delayMs)
        {
            if (_graphView == null)
                return;

            _graphView.RestoreViewTransform(state.position, state.scale, delayMs);
        }

        private void UpdateActiveGraphStatus()
        {
            titleContent = new GUIContent(string.IsNullOrEmpty(_loadedGraphName)
                ? "Dialogue Graph Editor"
                : $"Dialogue Graph Editor - {_loadedGraphName}");
        }

        private void ClearLoadedGraph()
        {
            _graphView.ClearGraphWithConfirmation();
            _editorSidebar.ClearAll();
        }

        private List<string> GetAllGraphAssetNamesFallback()
        {
            return DialogGraphAssetPaths.GetVisibleGraphNames();
        }
        #endregion

        #region -------------------- SIDEBAR SHOW/HIDE --------------------
        public void ToggleSidebar()
        {
            if (_sidebarVisible) CollapseSidebar();
            else ExpandSidebar();
        }

        private void CollapseSidebar()
        {
            if (!_sidebarVisible) return;
            _sidebarVisible = false;

            _sidebarWidthMemo = Mathf.Max(240f, _rightSidebarRoot.resolvedStyle.width);

            rootVisualElement.Remove(_split);

            if (_soloHost == null)
            {
                _soloHost = new VisualElement();
                _soloHost.AddToClassList("dlg-graph-host");
                _soloHost.style.flexGrow = 1;
            }

            _graphHost.RemoveFromHierarchy();
            _soloHost.Add(_graphHost);
            rootVisualElement.Add(_soloHost);

            UpdateSidebarToggleText();
        }

        private void ExpandSidebar()
        {
            if (_sidebarVisible) return;
            _sidebarVisible = true;

            if (_soloHost != null && _soloHost.parent != null)
                rootVisualElement.Remove(_soloHost);

            CreateSplit();
            UpdateSidebarToggleText();
        }

        private void UpdateSidebarToggleText()
        {
            if (_toggleSidebarBtn == null) return;

            _toggleSidebarBtn.text = string.Empty;
            var icon = _toggleSidebarBtn.Q<Image>(ButtonIconElementName);
            if (icon != null)
            {
                DialogGraphIconManager.SetImage(icon, _sidebarVisible ? DialogGraphIconId.ActionCollapse : DialogGraphIconId.ActionExpand);
            }

            var label = _toggleSidebarBtn.Q<Label>(ButtonLabelElementName);
            if (label != null)
                label.text = _sidebarVisible ? "Hide Sidebar" : "Show Sidebar";
        }

        private void OnClickToggleMinimap()
        {
            DialogGraphEditorSettings.MinimapVisible = !DialogGraphEditorSettings.MinimapVisible;
            UpdateMinimapToggleText();
            // OnSettingsChanged is already fired by the setter, so the canvas reacts automatically.
        }

        private void UpdateMinimapToggleText()
        {
            if (_toggleMinimapBtn == null) return;

            var label = _toggleMinimapBtn.Q<Label>(ButtonLabelElementName);
            if (label != null)
                label.text = GetMinimapToggleLabel();
        }

        private static string GetMinimapToggleLabel() =>
            DialogGraphEditorSettings.MinimapVisible ? "Hide Minimap" : "Show Minimap";

        #endregion

        #region -------------------- HELPERS (exposed to sidebar) --------------------
        public DialogGraphView GetGraphView() => _graphView;

        /// <summary>
        /// Selects and frames the node with the given GUID in the open graph view.
        /// </summary>
        public bool FocusNodeByGuid(string nodeGuid)
        {
            if (_graphView == null || string.IsNullOrWhiteSpace(nodeGuid))
            {
                return false;
            }

            var target = _graphView.nodes.ToList()
                .OfType<Node>()
                .FirstOrDefault(node => string.Equals(GetNodeGuid(node), nodeGuid, StringComparison.Ordinal));

            if (target == null)
            {
                return false;
            }

            _graphView.ClearSelection();
            _graphView.AddToSelection(target);
            _graphView.FrameSelection();
            Focus();
            return true;
        }

        private static string GetNodeGuid(Node node)
        {
            return node switch
            {
                DialogNodeView dialogNode => dialogNode.GUID,
                ChoiceNodeView choiceNode => choiceNode.GUID,
                ActionNodeView actionNode => actionNode.GUID,
                ConditionNodeView conditionNode => conditionNode.GUID,
                VariableMutationNodeView variableNode => variableNode.GUID,
                GraphJumpNodeView graphJumpNode => graphJumpNode.GUID,
                StartNodeView startNode => startNode.GUID,
                EndNodeView endNode => endNode.GUID,
                _ => string.Empty
            };
        }

        public string GetCurrentGraphName()
        {
            if (!string.IsNullOrWhiteSpace(_loadedGraphName))
            {
                return _loadedGraphName.Trim();
            }

            return _graphView?.graphId?.Trim() ?? string.Empty;
        }

        public string GetAiSidebarPrompt() => string.Empty;
        public string GetAiSidebarTone() => string.Empty;
        public string GetAiSidebarInstructionPreset() => string.Empty;

        public DialogGraph LoadCurrentGraphAsset(bool createIfMissing = false)
        {
            if (_graphView == null || string.IsNullOrWhiteSpace(_graphView.graphId))
            {
                return null;
            }

            var path = DialogGraphAssetPaths.ResolveGraphAssetPath(_graphView.graphId);
            var asset = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (asset != null || !createIfMissing)
            {
                return asset;
            }

            DialogGraphAssetPaths.EnsurePrimaryGraphFolderExists();
            path = DialogGraphAssetPaths.GetPrimaryGraphAssetPath(_graphView.graphId);
            asset = ScriptableObject.CreateInstance<DialogGraph>();
            asset.AssignGraphGuidIfMissing(Guid.NewGuid().ToString("N"));
            asset.MarkSchemaCurrentForMigration();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
            return AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
        }

        public void UpdateGraphContext(string undoLabel, Action<DialogGraph> applyChanges)
        {
            if (applyChanges == null)
            {
                return;
            }

            var asset = LoadCurrentGraphAsset(createIfMissing: true);
            if (asset == null)
            {
                return;
            }

            Undo.RecordObject(asset, undoLabel);
            asset.participatingCharacters ??= new List<DialogCharacterSO>();
            asset.availableActions ??= new List<DialogActionSO>();
            applyChanges(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        public IEnumerable<string> CollectSpeakersFromNodes()
        {
            if (_graphView == null) yield break;

            foreach (var ge in _graphView.nodes.ToList())
            {
                if (ge is not DialogNodeView dnv) continue;
                var speaker = dnv.speakerName;
                if (!string.IsNullOrWhiteSpace(speaker))
                    yield return speaker.Trim();
            }
        }

        public Sprite FindFirstSpriteForSpeaker(string speaker)
        {
            if (_graphView == null || string.IsNullOrWhiteSpace(speaker)) return null;
            var key = speaker.Trim();

            foreach (var ge in _graphView.nodes.ToList())
            {
                if (ge is not DialogNodeView dnv) continue;
                var s = dnv.speakerName;
                if (string.Equals(s?.Trim(), key, StringComparison.Ordinal))
                {
                    var spr = dnv.portraitSprite;
                    if (spr != null) return spr;
                }
            }
            return null;
        }

        internal (bool found, Color color) FindFirstNameColorForSpeaker(string speaker)
        {
            // Placeholder for future per-speaker color
            return (false, default);
        }

        public void ApplySpritesToNodes(List<CharacterBinding> bindings)
        {
            if (_graphView == null || bindings == null || bindings.Count == 0) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Sidebar Character Changes");
            var undoGroup = Undo.GetCurrentGroup();
            var appliedAnyChanges = false;

            try
            {
                foreach (var ge in _graphView.nodes.ToList())
                {
                    if (ge is not DialogNodeView dnv) continue;
                    var speaker = dnv.speakerName?.Trim();
                    if (string.IsNullOrEmpty(speaker)) continue;

                    var bind = bindings.FirstOrDefault(b =>
                        !string.IsNullOrWhiteSpace(b.originalName) &&
                        b.originalName.Trim().Equals(speaker, StringComparison.OrdinalIgnoreCase));

                    if (bind == null) continue;

                    var newName = bind.currentName?.Trim();
                    if (string.IsNullOrEmpty(newName))
                    {
                        newName = speaker;
                    }

                    var shouldUpdateSpeaker = !string.Equals(newName, speaker, StringComparison.Ordinal);
                    var shouldUpdatePortrait = bind.applySprite && bind.sprite != dnv.portraitSprite;
                    if (!shouldUpdateSpeaker && !shouldUpdatePortrait)
                    {
                        if (bind.forceRefresh)
                        {
                            dnv.RefreshCharacterRegistrationUi();
                            dnv.MarkDirtyRepaint();
                            appliedAnyChanges = true;
                        }

                        continue;
                    }

                    dnv.ApplySidebarCharacterEdits(newName, bind.sprite, bind.applySprite);
                    appliedAnyChanges = true;

                    dnv.MarkDirtyRepaint();
                }

                if (appliedAnyChanges)
                {
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            _graphView.schedule.Execute(() =>
            {
                foreach (var n in _graphView.nodes.ToList()) n.MarkDirtyRepaint();
            }).ExecuteLater(16);
        }

        public IEnumerable<ActionNodeView> CollectActionNodes()
        {
            if (_graphView == null) return Enumerable.Empty<ActionNodeView>();
            return _graphView.nodes.ToList().OfType<ActionNodeView>();
        }

        public IEnumerable<string> CollectActionIdsFromNodes()
        {
            return CollectActionNodes()
                .Select(node => node.actionId?.Trim())
                .Where(actionId => !string.IsNullOrWhiteSpace(actionId));
        }

        public int RegisterMissingActionAssetsFromCurrentGraph(out List<string> createdActionIds)
        {
            createdActionIds = new List<string>();

            var groupedNodes = CollectActionNodes()
                .Where(node => !string.IsNullOrWhiteSpace(node.actionId))
                .GroupBy(node => node.actionId.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DialogActionSO lastCreatedAsset = null;
            foreach (var group in groupedNodes)
            {
                if (DialogGraphDefinitionResolver.FindActionById(group.Key) != null)
                {
                    continue;
                }

                var sourceNode = group.First();
                lastCreatedAsset = CreateActionAsset(
                    group.Key,
                    sourceNode.payloadJson ?? "{}",
                    sourceNode.waitForCompletion,
                    sourceNode.waitSeconds,
                    pingAsset: false);

                if (lastCreatedAsset != null)
                {
                    createdActionIds.Add(group.Key);
                }
            }

            if (lastCreatedAsset != null)
            {
                DialogGraphDefinitionResolver.PingAndSelect(lastCreatedAsset);
            }

            return createdActionIds.Count;
        }

        public int AutofillAvailableActionsFromCurrentGraph(out List<string> missingActionIds, out List<string> assignedActionIds)
        {
            missingActionIds = new List<string>();
            assignedActionIds = new List<string>();

            var graph = LoadCurrentGraphAsset(createIfMissing: true);
            if (graph == null)
            {
                return 0;
            }

            var registry = new CoreActionRegistryService();
            var registeredById = registry.GetAllRegisteredDefinitions()
                .Where(action => action != null && !string.IsNullOrWhiteSpace(action.ActionID))
                .GroupBy(action => action.ActionID.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var matchedActions = new List<DialogActionSO>();
            foreach (var actionId in CollectActionIdsFromNodes()
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(actionId => actionId, StringComparer.OrdinalIgnoreCase))
            {
                if (registeredById.TryGetValue(actionId, out var action))
                {
                    matchedActions.Add(action);
                    assignedActionIds.Add(actionId);
                }
                else
                {
                    missingActionIds.Add(actionId);
                }
            }

            UpdateGraphContext("Autofill Available Actions", asset =>
            {
                asset.availableActions = matchedActions
                    .Distinct()
                    .ToList();
            });

            return matchedActions.Count;
        }

        public DialogNodeView GetSelectedDialogNode()
        {
            if (_graphView?.selection == null)
            {
                return null;
            }

            var selectedDialogNodes = _graphView.selection.OfType<DialogNodeView>().ToList();
            return selectedDialogNodes.Count == 1 ? selectedDialogNodes[0] : null;
        }

        public ActionNodeView GetSelectedActionNode()
        {
            if (_graphView?.selection == null)
            {
                return null;
            }

            var selectedActionNodes = _graphView.selection.OfType<ActionNodeView>().ToList();
            return selectedActionNodes.Count == 1 ? selectedActionNodes[0] : null;
        }

        public bool TryAssignRegisteredCharacterToSelectedDialogNode(DialogCharacterSO character, out string error)
        {
            error = string.Empty;

            if (character == null)
            {
                error = "Select a registered character first.";
                return false;
            }

            var selectedNode = GetSelectedDialogNode();
            if (selectedNode == null)
            {
                error = "Select exactly one dialog node first.";
                return false;
            }

            selectedNode.ApplyCharacterDefinition(character);
            _editorSidebar?.RebuildFromGraph();
            return true;
        }

        public bool TryInsertRegisteredActionAfterSelectedNode(DialogActionSO action, out string error)
        {
            error = string.Empty;

            if (action == null)
            {
                error = "Select a registered action first.";
                return false;
            }

            if (_graphView?.selection == null || _graphView.selection.Count != 1)
            {
                error = "Select exactly one node first.";
                return false;
            }

            var selectedNode = _graphView.selection.OfType<Node>().FirstOrDefault();
            if (selectedNode == null || selectedNode is EndNodeView)
            {
                error = "Select a Start, Dialog, or Action node first.";
                return false;
            }

            if (selectedNode is ChoiceNodeView)
            {
                error = "Select a Start, Dialog, or Action node. Choice nodes are branch-specific.";
                return false;
            }

            var outgoingCount = _graphView.edges.ToList()
                .OfType<Edge>()
                .Count(edge => edge.output?.node == selectedNode);

            if (outgoingCount > 1)
            {
                error = "The selected node has multiple outgoing links. Choose a linear node before inserting an action.";
                return false;
            }

            var mutationService = new DialogGraphMutationService(this);
            var draft = new ActionNodeMutationData
            {
                actionId = action.ActionID,
                payloadJson = action.DefaultPayloadJson,
                waitForCompletion = action.WaitForCompletion,
                waitSeconds = action.DefaultDelay
            };

            mutationService.InsertActionNodeAfter(selectedNode, draft, 0);
            _editorSidebar?.RebuildFromGraph();
            return true;
        }

        public DialogCharacterSO CreateCharacterAsset(string displayName, Sprite portrait = null)
        {
            var registry = new CoreCharacterRegistryService();
            var asset = registry.CreateNewAsset(string.IsNullOrWhiteSpace(displayName) ? "DialogCharacter" : displayName.Trim());
            if (asset == null)
            {
                return null;
            }

            Undo.RecordObject(asset, "Create Character Definition");
            asset.CharacterID = DialogGraphDefinitionResolver.CreateSuggestedCharacterId(displayName);
            asset.DisplayName = string.IsNullOrWhiteSpace(displayName) ? asset.DisplayName : displayName.Trim();
            if (portrait != null)
            {
                asset.Portrait = portrait;
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            ApplySpritesToNodes(new List<CharacterBinding>
            {
                new()
                {
                    originalName = asset.DisplayName,
                    currentName = asset.DisplayName,
                    sprite = asset.Portrait,
                    applySprite = asset.Portrait != null,
                    forceRefresh = true
                },
                new()
                {
                    originalName = asset.CharacterID,
                    currentName = asset.DisplayName,
                    sprite = asset.Portrait,
                    applySprite = asset.Portrait != null,
                    forceRefresh = true
                }
            });
            DialogGraphDefinitionResolver.PingAndSelect(asset);
            return asset;
        }

        public DialogActionSO CreateActionAsset(string actionId, string payloadJson = "{}", bool waitForCompletion = true, float delay = 0f, bool pingAsset = true)
        {
            var registry = new CoreActionRegistryService();
            var asset = registry.CreateNewAsset(string.IsNullOrWhiteSpace(actionId) ? "DialogAction" : actionId.Trim());
            if (asset == null)
            {
                return null;
            }

            Undo.RecordObject(asset, "Create Action Definition");
            asset.ActionID = string.IsNullOrWhiteSpace(actionId)
                ? DialogGraphDefinitionResolver.CreateSuggestedActionId(asset.name)
                : actionId.Trim();
            asset.DisplayName = asset.ActionID;
            asset.DefaultPayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
            asset.WaitForCompletion = waitForCompletion;
            asset.DefaultDelay = delay;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            if (pingAsset)
            {
                DialogGraphDefinitionResolver.PingAndSelect(asset);
            }

            return asset;
        }

        public DialogVariableSO CreateVariableAsset(
            string variableKey,
            DialogueVariableValueType valueType = DialogueVariableValueType.Boolean,
            string defaultValue = "",
            bool pingAsset = true)
        {
            var registry = new CoreVariableRegistryService();
            var asset = registry.CreateNewAsset(string.IsNullOrWhiteSpace(variableKey) ? "DialogVariable" : variableKey.Trim());
            if (asset == null)
            {
                return null;
            }

            Undo.RecordObject(asset, "Create Variable Definition");
            DialogAssetInitializer.InitializeVariable(asset, variableKey, valueType, defaultValue);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            if (pingAsset)
            {
                DialogGraphDefinitionResolver.PingAndSelect(asset);
            }

            return asset;
        }

        public DialogEnvironmentSO CreateEnvironmentAsset(string displayName, bool pingAsset = true)
        {
            var registry = new DialogEnvironmentRegistryService();
            var asset = registry.CreateNewAsset(string.IsNullOrWhiteSpace(displayName) ? "DialogEnvironment" : displayName.Trim());
            if (asset == null)
            {
                return null;
            }

            Undo.RecordObject(asset, "Create Environment Definition");
            asset.EnvironmentID = DialogGraphDefinitionResolver.CreateSuggestedEnvironmentId(displayName);
            asset.DisplayName = string.IsNullOrWhiteSpace(displayName) ? asset.DisplayName : displayName.Trim();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            if (pingAsset)
            {
                DialogGraphDefinitionResolver.PingAndSelect(asset);
            }

            return asset;
        }

        public DialogSceneContextSO CreateSceneContextAsset(
            string displayName,
            DialogEnvironmentSO environment = null,
            bool pingAsset = true)
        {
            var registry = new DialogSceneContextRegistryService();
            var asset = registry.CreateNewAsset(string.IsNullOrWhiteSpace(displayName) ? "DialogSceneContext" : displayName.Trim());
            if (asset == null)
            {
                return null;
            }

            Undo.RecordObject(asset, "Create Scene Context Definition");
            asset.Environment = environment;
            asset.name = string.IsNullOrWhiteSpace(displayName) ? asset.name : displayName.Trim();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            if (pingAsset)
            {
                DialogGraphDefinitionResolver.PingAndSelect(asset);
            }

            return asset;
        }

        public bool TryRewriteSelectedDialogNode(
            string customPrompt,
            string desiredTone,
            string instructionPreset,
            out string error)
        {
            error = string.Empty;

            if (_graphView == null)
            {
                error = "The graph editor is not ready yet.";
                return false;
            }

            if (_graphView.selection == null || _graphView.selection.Count == 0)
            {
                error = "Select one dialog node first.";
                return false;
            }

            var selectedDialogNodes = _graphView.selection.OfType<DialogNodeView>().ToList();
            if (selectedDialogNodes.Count != 1)
            {
                error = "Select exactly one dialog node.";
                return false;
            }

            return selectedDialogNodes[0].TryRewriteWithPreview(customPrompt, desiredTone, instructionPreset, out error);
        }

        public void RefreshGraphEditorState()
        {
            _graphView?.MarkDirtyRepaint();
            _editorSidebar?.RebuildFromGraph();
            Repaint();
        }

        public void ApplyActionsToNodes(List<ActionBinding> bindings)
        {
            if (_graphView == null || bindings == null || bindings.Count == 0) return;

            var actionViews = CollectActionNodes().ToList();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Sidebar Action Changes");
            var undoGroup = Undo.GetCurrentGroup();
            var appliedAnyChanges = false;

            try
            {
                foreach (var bind in bindings)
                {
                    var originalKey = bind.originalActionId ?? "";
                    foreach (var av in actionViews)
                    {
                        var curId = av.actionId ?? "";
                        if (!string.Equals(curId, originalKey, StringComparison.Ordinal)) continue;

                        var nextActionId = bind.actionId ?? string.Empty;
                        var nextPayload = bind.payloadJson ?? string.Empty;
                        if (string.Equals(curId, nextActionId, StringComparison.Ordinal) &&
                            string.Equals(av.payloadJson ?? string.Empty, nextPayload, StringComparison.Ordinal) &&
                            av.waitForCompletion == bind.waitForCompletion &&
                            Mathf.Approximately(av.waitSeconds, bind.waitSeconds))
                        {
                            continue;
                        }

                        av.ApplySidebarEdits(
                            bind.actionId ?? "",
                            bind.payloadJson ?? "",
                            bind.waitForCompletion,
                            bind.waitSeconds
                        );

                        appliedAnyChanges = true;
                        av.MarkDirtyRepaint();
                    }
                }

                if (appliedAnyChanges)
                {
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            _graphView.schedule.Execute(() =>
            {
                foreach (var n in actionViews) n.MarkDirtyRepaint();
            }).ExecuteLater(16);
        }

        #endregion

        #region -------------------- DATA TYPES --------------------

        [Serializable]
        public class CharacterBinding
        {
            public string originalName;
            public string currentName;
            public Sprite sprite;
            public bool applySprite = true;
            public bool forceRefresh;
        }

        [Serializable]
        public class ActionBinding
        {
            public string originalActionId;
            public string actionId;
            public string payloadJson;
            public bool   waitForCompletion;
            public float  waitSeconds;
        }

        #endregion

    }
}
