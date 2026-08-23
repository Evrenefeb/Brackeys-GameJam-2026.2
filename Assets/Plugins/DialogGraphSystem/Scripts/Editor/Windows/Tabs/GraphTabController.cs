using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.AI;
using DialogSystem.EditorTools.Resources;
using DialogSystem.EditorTools.Settings;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.EditorTools.Utils;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Utils;
using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CoreActionRegistryService = DialogSystem.EditorTools.Services.DialogActionRegistryService;
using CoreCharacterRegistryService = DialogSystem.EditorTools.Services.DialogCharacterRegistryService;
using CoreVariableRegistryService = DialogSystem.EditorTools.Services.DialogVariableRegistryService;

namespace DialogSystem.EditorTools.Windows.Tabs
{
    /// <summary>
    /// Manages the Graph tab in <see cref="DialogSystemMainWindow"/>.
    /// Implements <see cref="IDialogGraphOwner"/> so <see cref="EditorSidebarWindow"/>
    /// can work without needing a <see cref="DialogGraphEditorWindow"/> reference.
    /// </summary>
    public partial class GraphTabController : IDialogGraphOwner
    {
        #region ---------------- Constants ----------------

        private const string PrefKeyLastGraph       = "DialogSystem_LastGraph";
        private const string PrefKeyRecentGraphs    = "DialogSystem_RecentGraphs";
        private const string PrefKeyOpenGraphs      = "DialogSystem_OpenGraphs";
        private const string ViewStatePrefKeyPrefix = "DialogSystem_ViewState_";
        private const string ButtonIconElementName  = "button-icon";
        private const string ButtonLabelElementName = "button-label";
        private const int    MaxRecentGraphs        = 8;

        #endregion

        #region ---------------- State ----------------

        private readonly DialogSystemMainWindow _owner;

        private VisualElement _root;
        private List<string>  _openGraphNames = new();

        private DialogGraphView     _graphView;
        private VisualElement       _graphHost;
        private EditorSidebarWindow _sidebar;
        private TwoPaneSplitView    _split;
        private VisualElement       _rightSidebarRoot;
        private VisualElement       _soloHost;
        private VisualElement       _workspaceTabBar;
        private bool                _sidebarVisible   = true;
        private float               _sidebarWidthMemo = 340f;
        private string              _loadedGraphName;
        private string              _graphViewLoadedGraphName;

        private Button _saveBtn;
        private Button _formatBtn;
        private Button _toggleSidebarBtn;
        private Button _minimapOverlayBtn;
        private Image _minimapOverlayIcon;

        private DialogGraphAiFloatingPanel _aiFloatingPanel;

        #endregion

        #region ---------------- Constructor ----------------

        public GraphTabController(DialogSystemMainWindow owner)
        {
            _owner = owner;
            LoadTabPersistence();
        }

        private void LoadTabPersistence()
        {
            var raw = EditorPrefs.GetString(PrefKeyOpenGraphs, string.Empty);
            if (!string.IsNullOrEmpty(raw))
            {
                _openGraphNames = raw.Split('|')
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            _loadedGraphName = EditorPrefs.GetString(PrefKeyLastGraph, string.Empty);
            if (!string.IsNullOrEmpty(_loadedGraphName) && !_openGraphNames.Contains(_loadedGraphName))
                _openGraphNames.Add(_loadedGraphName);
        }

        private void SaveTabPersistence()
        {
            EditorPrefs.SetString(PrefKeyOpenGraphs, string.Join("|", _openGraphNames));
            EditorPrefs.SetString(PrefKeyLastGraph, _loadedGraphName ?? string.Empty);
        }

        #endregion

        #region ---------------- Public API ----------------

        /// <summary>Called by the main window for the Launcher tab.</summary>
        public void BuildLauncherUI(VisualElement root)
        {
            _root = root;
            ShowHome();
        }

        /// <summary>Called by the main window for the Graphs tab.</summary>
        public void BuildWorkspaceUI(VisualElement root)
        {
            _root = root;
            PruneMissingOpenGraphs();

            if (_openGraphNames.Count == 0 && !string.IsNullOrEmpty(_loadedGraphName))
                _openGraphNames.Add(_loadedGraphName);

            if (_openGraphNames.Count > 0)
                ShowCanvas();
            else
                ShowEmptyWorkspace();
        }

        /// <summary>Transitions from Home to canvas view using a graph asset reference.</summary>
        public void LoadGraph(DialogGraph graph)
        {
            if (graph == null) return;
            LoadGraphByName(graph.name);
        }

        /// <summary>Loads a graph by name and adds it to the open documents.</summary>
        public void LoadGraphByName(string graphName)
        {
            if (string.IsNullOrWhiteSpace(graphName)) return;

            if (!TryEnsureGraphExists(graphName))
            {
                return;
            }

            WarnIfLegacyWindowAlreadyEditing(graphName);
            
            if (!_openGraphNames.Contains(graphName))
                _openGraphNames.Add(graphName);

            if (_loadedGraphName == graphName && _graphView != null)
            {
                _owner.SwitchTab(DialogSystemMainWindow.TabType.Graphs);
                ShowCanvas();
                return;
            }

            EnsureGraphViewCreated();

            if (!string.IsNullOrEmpty(_loadedGraphName) && _graphView != null)
            {
                SaveCurrentViewState();
                _graphView.SaveGraph(_loadedGraphName);
            }

            var hasSavedState = TryLoadViewState(graphName, out var savedState);
            _graphView.LoadGraph(graphName, false, focusStartNode: !hasSavedState);
            _graphViewLoadedGraphName = graphName;
            if (hasSavedState)
            {
                RestoreViewState(savedState, 16);
            }

            _loadedGraphName = graphName;
            FormatLayout();
            SaveTabPersistence();
            TrackRecentGraph(graphName);
            _sidebar?.RebuildFromGraph();
            
            _owner.SwitchTab(DialogSystemMainWindow.TabType.Graphs, forceRefresh: true);
        }

        /// <summary>Returns to Home view, auto-saving the current graph first.</summary>
        public void UnloadGraph()
        {
            SaveActiveGraphForWindowClose();
            _loadedGraphName = null;
            _openGraphNames.Clear();
            SaveTabPersistence();
            _owner.SwitchTab(DialogSystemMainWindow.TabType.Launcher, forceRefresh: true);
            _owner.Repaint();
        }

        /// <summary>Closes a specific graph tab.</summary>
        public void CloseGraph(string graphName)
        {
            if (string.IsNullOrEmpty(graphName)) return;
            
            if (_loadedGraphName == graphName && _graphView != null)
            {
                SaveCurrentViewState();
                _graphView.SaveGraph(_loadedGraphName);
            }

            _openGraphNames.Remove(graphName);

            if (_loadedGraphName == graphName)
            {
                _loadedGraphName = null;
                if (_openGraphNames.Count > 0)
                    LoadGraphByName(_openGraphNames.Last());
                else
                {
                    SaveTabPersistence();
                    ShowEmptyWorkspace();
                }
            }
            else
            {
                if (_openGraphNames.Count > 0)
                    ShowCanvas();
                else
                {
                    SaveTabPersistence();
                    ShowEmptyWorkspace();
                }
            }
            SaveTabPersistence();
            _owner.Repaint();
        }

        /// <summary>Saves the active graph and view state before window teardown or domain reload.</summary>
        public void SaveActiveGraphForWindowClose()
        {
            if (_graphView != null && !string.IsNullOrEmpty(_loadedGraphName))
            {
                if (DialogGraphAssetPaths.LoadGraphAsset(_loadedGraphName) != null)
                {
                    SaveCurrentViewState();
                    _graphView.SaveGraph(_loadedGraphName);
                }
                else
                {
                    Debug.LogWarning($"[GraphTabController] Skipped window-close save because dialog graph '{_loadedGraphName}' no longer exists.");
                }
            }

            SaveTabPersistence();
        }

        /// <summary>Whether a graph is currently loaded in the canvas.</summary>
        public bool HasLoadedGraph => !string.IsNullOrEmpty(_loadedGraphName) && _graphView != null;

        /// <summary>Reloads the active graph after an undo/redo operation and preserves the camera state.</summary>
        public void HandleUndoRedo()
        {
            if (_root == null)
            {
                return;
            }

            if (_graphView == null || string.IsNullOrEmpty(_loadedGraphName))
            {
                ShowEmptyWorkspace();
                return;
            }

            var asset = DialogGraphAssetPaths.LoadGraphAsset(_loadedGraphName);
            if (asset == null)
            {
                _openGraphNames.Remove(_loadedGraphName);
                _loadedGraphName = null;
                SaveTabPersistence();
                ShowEmptyWorkspace();
                return;
            }

            var state = new GraphViewCameraState
            {
                position = _graphView.GetViewPosition(),
                scale = _graphView.GetViewScale()
            };

            _graphView.LoadGraph(_loadedGraphName, true, focusStartNode: false);
            _graphViewLoadedGraphName = _loadedGraphName;
            RestoreViewState(state, 16);
            _sidebar?.RebuildFromGraph();
            BuildWorkspaceTabs();
        }

        private bool TryEnsureGraphExists(string graphName)
        {
            if (DialogGraphAssetPaths.LoadGraphAsset(graphName) != null)
            {
                return true;
            }

            _openGraphNames.RemoveAll(name => string.Equals(name, graphName, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(_loadedGraphName, graphName, StringComparison.OrdinalIgnoreCase))
            {
                _loadedGraphName = string.Empty;
                _graphViewLoadedGraphName = null;
            }

            SaveTabPersistence();
            Debug.LogWarning($"[GraphTabController] Dialog graph '{graphName}' was not found and was removed from the open graph workspace.");
            return false;
        }

        private void PruneMissingOpenGraphs()
        {
            if (_openGraphNames.Count == 0 && string.IsNullOrWhiteSpace(_loadedGraphName))
            {
                return;
            }

            var originalCount = _openGraphNames.Count;
            _openGraphNames = _openGraphNames
                .Where(name => !string.IsNullOrWhiteSpace(name) && DialogGraphAssetPaths.LoadGraphAsset(name) != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(_loadedGraphName) && DialogGraphAssetPaths.LoadGraphAsset(_loadedGraphName) == null)
            {
                Debug.LogWarning($"[GraphTabController] Last open dialog graph '{_loadedGraphName}' no longer exists.");
                _loadedGraphName = _openGraphNames.LastOrDefault() ?? string.Empty;
                _graphViewLoadedGraphName = null;
            }

            if (originalCount != _openGraphNames.Count)
            {
                SaveTabPersistence();
            }
        }

        private bool EnsureLoadedGraphInCanvas()
        {
            if (string.IsNullOrWhiteSpace(_loadedGraphName) && _openGraphNames.Count > 0)
            {
                _loadedGraphName = _openGraphNames.Last();
            }

            if (string.IsNullOrWhiteSpace(_loadedGraphName))
            {
                return false;
            }

            if (!TryEnsureGraphExists(_loadedGraphName))
            {
                return false;
            }

            if (string.Equals(_graphViewLoadedGraphName, _loadedGraphName, StringComparison.Ordinal))
            {
                return true;
            }

            var hasSavedState = TryLoadViewState(_loadedGraphName, out var savedState);
            _graphView.LoadGraph(_loadedGraphName, false, focusStartNode: !hasSavedState);
            _graphViewLoadedGraphName = _loadedGraphName;
            if (hasSavedState)
            {
                RestoreViewState(savedState, 16);
            }

            _sidebar?.RebuildFromGraph();
            TrackRecentGraph(_loadedGraphName);
            SaveTabPersistence();
            return true;
        }

        private static void WarnIfLegacyWindowAlreadyEditing(string graphName)
        {
            if (string.IsNullOrWhiteSpace(graphName))
            {
                return;
            }

            var legacyWindow = UnityEngine.Resources
                .FindObjectsOfTypeAll<DialogGraphEditorWindow>()
                .FirstOrDefault(window =>
                    window != null &&
                    string.Equals(window.GetCurrentGraphName(), graphName, StringComparison.Ordinal));

            if (legacyWindow == null)
            {
                return;
            }

            Debug.LogWarning(
                $"[GraphTabController] Graph '{graphName}' is already open in the legacy graph editor window. " +
                "Editing the same graph in multiple windows can overwrite view state or recent unsaved changes.",
                legacyWindow);
        }

        #endregion

        #region ---------------- IDialogGraphOwner ----------------

        /// <inheritdoc/>
        public DialogGraphView GetGraphView() => _graphView;

        /// <inheritdoc/>
        public string GetCurrentGraphName() => _loadedGraphName ?? string.Empty;

        /// <inheritdoc/>
        public DialogGraph LoadCurrentGraphAsset(bool createIfMissing = false)
        {
            if (_graphView == null || string.IsNullOrWhiteSpace(_graphView.graphId))
                return null;

            var path  = DialogGraphAssetPaths.ResolveGraphAssetPath(_graphView.graphId);
            var asset = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (asset != null || !createIfMissing) return asset;

            DialogGraphAssetPaths.EnsurePrimaryGraphFolderExists();
            path  = DialogGraphAssetPaths.GetPrimaryGraphAssetPath(_graphView.graphId);
            asset = ScriptableObject.CreateInstance<DialogGraph>();
            asset.AssignGraphGuidIfMissing(Guid.NewGuid().ToString("N"));
            asset.MarkSchemaCurrentForMigration();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
            return AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
        }

        /// <inheritdoc/>
        public void ToggleSidebar()
        {
            if (_sidebarVisible) CollapseSidebar();
            else ExpandSidebar();
        }

        /// <inheritdoc/>
        public void ApplySpritesToNodes(List<DialogGraphEditorWindow.CharacterBinding> bindings)
        {
            if (_graphView == null || bindings == null || bindings.Count == 0) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Sidebar Character Changes");
            var undoGroup = Undo.GetCurrentGroup();
            var applied   = false;

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

                    var newName          = string.IsNullOrEmpty(bind.currentName?.Trim()) ? speaker : bind.currentName.Trim();
                    var shouldUpdateName = !string.Equals(newName, speaker, StringComparison.Ordinal);
                    var shouldUpdateSpr  = bind.applySprite && bind.sprite != dnv.portraitSprite;
                    if (!shouldUpdateName && !shouldUpdateSpr)
                    {
                        if (bind.forceRefresh)
                        {
                            dnv.RefreshCharacterRegistrationUi();
                            applied = true;
                            dnv.MarkDirtyRepaint();
                        }

                        continue;
                    }

                    dnv.ApplySidebarCharacterEdits(newName, bind.sprite, bind.applySprite);
                    applied = true;
                    dnv.MarkDirtyRepaint();
                }

                if (applied) AssetDatabase.SaveAssets();
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

        /// <inheritdoc/>
        public void ApplyActionsToNodes(List<DialogGraphEditorWindow.ActionBinding> bindings)
        {
            if (_graphView == null || bindings == null || bindings.Count == 0) return;

            var actionViews = CollectActionNodes().ToList();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Apply Sidebar Action Changes");
            var undoGroup = Undo.GetCurrentGroup();
            var applied   = false;

            try
            {
                foreach (var bind in bindings)
                {
                    var key = bind.originalActionId ?? "";
                    foreach (var av in actionViews)
                    {
                        if (!string.Equals(av.actionId ?? "", key, StringComparison.Ordinal)) continue;

                        if (string.Equals(av.actionId ?? "", bind.actionId ?? "", StringComparison.Ordinal) &&
                            string.Equals(av.payloadJson ?? "", bind.payloadJson ?? "", StringComparison.Ordinal) &&
                            av.waitForCompletion == bind.waitForCompletion &&
                            Mathf.Approximately(av.waitSeconds, bind.waitSeconds))
                            continue;

                        av.ApplySidebarEdits(bind.actionId ?? "", bind.payloadJson ?? "", bind.waitForCompletion, bind.waitSeconds);
                        applied = true;
                        av.MarkDirtyRepaint();
                    }
                }

                if (applied) AssetDatabase.SaveAssets();
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        /// <inheritdoc/>
        public IEnumerable<string> CollectSpeakersFromNodes()
        {
            if (_graphView == null) yield break;
            foreach (var ge in _graphView.nodes.ToList())
            {
                if (ge is not DialogNodeView dnv) continue;
                var s = dnv.speakerName;
                if (!string.IsNullOrWhiteSpace(s))
                    yield return s.Trim();
            }
        }

        /// <inheritdoc/>
        public Sprite FindFirstSpriteForSpeaker(string speaker)
        {
            if (_graphView == null || string.IsNullOrWhiteSpace(speaker)) return null;
            var key = speaker.Trim();
            foreach (var ge in _graphView.nodes.ToList())
            {
                if (ge is not DialogNodeView dnv) continue;
                if (string.Equals(dnv.speakerName?.Trim(), key, StringComparison.Ordinal) && dnv.portraitSprite != null)
                    return dnv.portraitSprite;
            }
            return null;
        }

        /// <inheritdoc/>
        public IEnumerable<ActionNodeView> CollectActionNodes()
        {
            if (_graphView == null) return Enumerable.Empty<ActionNodeView>();
            return _graphView.nodes.ToList().OfType<ActionNodeView>();
        }

        /// <inheritdoc/>
        public IEnumerable<string> CollectActionIdsFromNodes()
            => CollectActionNodes()
               .Select(n => n.actionId?.Trim())
               .Where(id => !string.IsNullOrWhiteSpace(id));

        /// <inheritdoc/>
        public void UpdateGraphContext(string undoLabel, Action<DialogGraph> applyChanges)
        {
            if (applyChanges == null) return;
            var asset = LoadCurrentGraphAsset(createIfMissing: true);
            if (asset == null) return;
            Undo.RecordObject(asset, undoLabel);
            applyChanges(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        /// <inheritdoc/>
        public bool TryAssignRegisteredCharacterToSelectedDialogNode(DialogCharacterSO character, out string error)
        {
            error = string.Empty;
            if (character == null) { error = "Select a registered character first."; return false; }

            var sel = _graphView?.selection?.OfType<DialogNodeView>().ToList();
            if (sel == null || sel.Count != 1)
            {
                error = "Select exactly one dialog node first.";
                return false;
            }

            sel[0].ApplyCharacterDefinition(character);
            _sidebar?.RebuildFromGraph();
            return true;
        }

        /// <inheritdoc/>
        public bool TryInsertRegisteredActionAfterSelectedNode(DialogActionSO action, out string error)
        {
            error = string.Empty;
            if (action == null) { error = "Select a registered action first."; return false; }

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
                error = "Select a linear node — not a Choice node.";
                return false;
            }

            var outgoing = _graphView.edges.ToList().OfType<Edge>()
                .Count(e => e.output?.node == selectedNode);
            if (outgoing > 1)
            {
                error = "The selected node has multiple outgoing links.";
                return false;
            }

            var mutationService = new DialogGraphMutationService(null);
            var draft = new ActionNodeMutationData
            {
                actionId          = action.ActionID,
                payloadJson       = action.DefaultPayloadJson,
                waitForCompletion = action.WaitForCompletion,
                waitSeconds       = action.DefaultDelay
            };
            mutationService.InsertActionNodeAfter(selectedNode, draft, 0);
            _sidebar?.RebuildFromGraph();
            return true;
        }

        /// <inheritdoc/>
        public int RegisterMissingActionAssetsFromCurrentGraph(out List<string> createdActionIds)
        {
            createdActionIds = new List<string>();
            var registry = new DialogActionRegistryService();

            foreach (var g in CollectActionNodes()
                .Where(n => !string.IsNullOrWhiteSpace(n.actionId))
                .GroupBy(n => n.actionId.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (DialogGraphDefinitionResolver.FindActionById(g.Key) != null) continue;

                var source = g.First();
                var asset  = registry.CreateNewAsset(g.Key);
                if (asset == null) continue;

                asset.ActionID           = g.Key;
                asset.DisplayName        = g.Key;
                asset.DefaultPayloadJson = source.payloadJson ?? "{}";
                asset.WaitForCompletion  = source.waitForCompletion;
                asset.DefaultDelay       = source.waitSeconds;
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                createdActionIds.Add(g.Key);
            }

            return createdActionIds.Count;
        }

        /// <inheritdoc/>
        public int AutofillAvailableActionsFromCurrentGraph(out List<string> missingActionIds, out List<string> assignedActionIds)
        {
            missingActionIds  = new List<string>();
            assignedActionIds = new List<string>();

            var graph = LoadCurrentGraphAsset(createIfMissing: true);
            if (graph == null) return 0;

            var registry = new DialogActionRegistryService();
            var byId = registry.GetAllRegisteredDefinitions()
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.ActionID))
                .GroupBy(a => a.ActionID.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var matched = new List<DialogActionSO>();
            foreach (var actionId in CollectActionIdsFromNodes().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (byId.TryGetValue(actionId, out var action))
                {
                    matched.Add(action);
                    assignedActionIds.Add(actionId);
                }
                else
                {
                    missingActionIds.Add(actionId);
                }
            }

            UpdateGraphContext("Autofill Available Actions", asset =>
            {
                asset.availableActions = matched.Distinct().ToList();
            });

            return matched.Count;
        }

        /// <inheritdoc/>
        public void ValidateGraph()
        {
            if (_graphView == null || string.IsNullOrEmpty(_loadedGraphName)) return;
            _graphView.SaveGraph(_loadedGraphName);
            AssetDatabase.SaveAssets();

            var path   = DialogGraphAssetPaths.ResolveGraphAssetPath(_loadedGraphName);
            var asset  = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            var result = DialogGraphValidationRunner.ValidateGraphAsset(asset);
            if (!_sidebarVisible)
            {
                ExpandSidebar();
            }
            _sidebar?.ShowValidationResult(result);
        }

        #endregion

        #region ---------------- Home View ----------------

        private void ShowHome()
        {
            _root.Clear();
            BuildHomeBrowserUI();
        }

        private static VisualElement MakeHomeSection(string headerText)
        {
            var section = new VisualElement();
            section.AddToClassList("ds-home-section");

            var title = new Label(headerText);
            title.AddToClassList("ds-home-section-title");
            section.Add(title);

            return section;
        }

        private void OnClickCreateNew(string rawName)
        {
            var finalName = (rawName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(finalName))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Please enter a valid graph name.", "OK");
                return;
            }

            var allGraphs = DialogGraphAssetPaths.GetVisibleGraphNames();
            if (allGraphs.Contains(finalName))
            {
                var choice = EditorUtility.DisplayDialogComplex(
                    "Graph Already Exists",
                    $"A graph named '{finalName}' already exists.\n\nWhat would you like to do?",
                    "Open Existing", "Cancel", "Overwrite");
                if (choice == 0) { LoadGraphByName(finalName); return; }
                if (choice == 1) return;
            }

            CreateGraphAsset(finalName);
            LoadGraphByName(finalName);
        }

        private void OnClickOpenRecent(string graphName)
        {
            var allGraphs = DialogGraphAssetPaths.GetVisibleGraphNames();
            if (!allGraphs.Contains(graphName))
            {
                EditorUtility.DisplayDialog("Graph Not Found", $"The graph '{graphName}' no longer exists.", "OK");
                var recents = GetRecentGraphs();
                recents.Remove(graphName);
                SaveRecentGraphs(recents);
                ShowHome();
                return;
            }
            LoadGraphByName(graphName);
        }

        private static void CreateGraphAsset(string graphName)
        {
            DialogGraphAssetPaths.EnsurePrimaryGraphFolderExists();
            var path     = DialogGraphAssetPaths.GetPrimaryGraphAssetPath(graphName);
            var existing = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.AssignGraphGuidIfMissing(Guid.NewGuid().ToString("N"));
            graph.MarkSchemaCurrentForMigration();
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        #endregion

        #region ---------------- Canvas View ----------------

        private void EnsureGraphViewCreated()
        {
            if (_graphView != null) return;

            _graphView = new DialogGraphView { name = "Dialog Graph" };
            _graphView.GraphOwner = this;

            _graphHost = new VisualElement();
            _graphHost.AddToClassList("dlg-graph-host");
            _graphHost.style.flexGrow = 1;
            _graphHost.Add(_graphView);
            _graphView.StretchToParentSize();

            BuildAiFloatingPanel();
            BuildMinimapOverlayButton();

            _rightSidebarRoot = new VisualElement();
            _rightSidebarRoot.AddToClassList("dlg-sidebar");

            _sidebar = new EditorSidebarWindow(this);
            _rightSidebarRoot.Add(_sidebar);

            _graphView?.EnsureStartEndNodes();
        }

        private void ShowEmptyWorkspace()
        {
            _root.Clear();
            _loadedGraphName = null;
            _graphViewLoadedGraphName = null;
            SaveTabPersistence();

            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            _root.Add(container);

            var label = new Label("No graphs are currently open.\nGo to the Launcher tab to open or create a graph.");
            label.AddToClassList("ds-home-subtitle");
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(label);

            var openLauncherBtn = new Button(() => _owner.SwitchTab(DialogSystemMainWindow.TabType.Launcher)) { text = "Open Launcher" };
            openLauncherBtn.AddToClassList("dlg-btn");
            openLauncherBtn.AddToClassList("primary");
            container.Add(openLauncherBtn);
        }

        private void ShowCanvas()
        {
            _root.Clear();
            EnsureGraphViewCreated();
            if (!EnsureLoadedGraphInCanvas())
            {
                ShowEmptyWorkspace();
                return;
            }

            UpdateMinimapToggleText();

            BuildWorkspaceTabs();
            _root.Add(BuildSubToolbar());
            BuildSplit();
        }

        private void BuildWorkspaceTabs()
        {
            if (_workspaceTabBar == null)
            {
                _workspaceTabBar = new VisualElement();
                _workspaceTabBar.AddToClassList("ds-graph-tab-bar");
            }
            
            _workspaceTabBar.Clear();
            _workspaceTabBar.RemoveFromHierarchy();
            _root.Insert(0, _workspaceTabBar);

            foreach (var graphName in _openGraphNames)
            {
                var tab = new VisualElement();
                tab.AddToClassList("ds-graph-tab");
                if (graphName == _loadedGraphName)
                    tab.AddToClassList("ds-graph-tab--active");

                var label = new Button(() => LoadGraphByName(graphName)) { text = graphName };
                label.AddToClassList("ds-graph-tab-label");
                tab.Add(label);

                var closeBtn = new Button(() => CloseGraph(graphName)) { text = "×" };
                closeBtn.AddToClassList("ds-graph-tab-close");
                tab.Add(closeBtn);

                _workspaceTabBar.Add(tab);
            }
        }

        private VisualElement BuildSubToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ds-sub-toolbar");

            var nameLabel = new Label(string.IsNullOrEmpty(_loadedGraphName) ? "Unsaved Graph" : _loadedGraphName);
            nameLabel.AddToClassList("ds-sub-toolbar-label");
            toolbar.Add(nameLabel);

            toolbar.Add(MakeSep());

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            _saveBtn = CreateToolbarButton("Save", DialogGraphIconId.ToolbarSave, "Save the current dialogue graph", OnClickSave, "primary");
            _saveBtn.AddToClassList("primary");
            toolbar.Add(_saveBtn);

            _formatBtn = CreateToolbarButton("Format Layout", DialogGraphIconId.ToolbarAutoLayout, "Reformat linked node positions only", OnClickFormat, "format");
            toolbar.Add(_formatBtn);

            _toggleSidebarBtn = CreateToolbarButton(
                _sidebarVisible ? "Hide Sidebar" : "Show Sidebar",
                _sidebarVisible ? DialogGraphIconId.ActionCollapse : DialogGraphIconId.ActionExpand,
                "Show or hide the graph sidebar",
                ToggleSidebar,
                "secondary");
            toolbar.Add(_toggleSidebarBtn);

            return toolbar;
        }

        private void BuildSplit()
        {
            if (!_sidebarVisible)
            {
                _soloHost = new VisualElement();
                _soloHost.AddToClassList("dlg-graph-host");
                _soloHost.style.flexGrow = 1;
                _soloHost.Add(_graphHost);
                _root.Add(_soloHost);
                return;
            }

            _split = new TwoPaneSplitView(1, _sidebarWidthMemo, TwoPaneSplitViewOrientation.Horizontal);
            _split.AddToClassList("dlg-split");
            _split.style.flexGrow = 1;
            _split.Add(_graphHost);
            _split.Add(_rightSidebarRoot);
            _root.Add(_split);
        }

        private void CollapseSidebar()
        {
            if (!_sidebarVisible) return;
            _sidebarVisible = false;

            _sidebarWidthMemo = Mathf.Max(240f, _rightSidebarRoot.resolvedStyle.width > 0
                ? _rightSidebarRoot.resolvedStyle.width
                : _sidebarWidthMemo);

            _root.Clear();
            BuildWorkspaceTabs();
            _root.Add(BuildSubToolbar());

            _soloHost = new VisualElement();
            _soloHost.AddToClassList("dlg-graph-host");
            _soloHost.style.flexGrow = 1;
            _soloHost.Add(_graphHost);
            _root.Add(_soloHost);
        }

        private void ExpandSidebar()
        {
            if (_sidebarVisible) return;
            _sidebarVisible = true;

            _root.Clear();
            BuildWorkspaceTabs();
            _root.Add(BuildSubToolbar());
            BuildSplit();
        }

        private void BuildAiFloatingPanel()
        {
            VisualElement aiContent    = null;
            Action        openSettings = null;

            var bridge = DialogGraphAiBridgeLocator.Current;
            if (bridge != null && bridge.IsAvailable)
            {
                aiContent = bridge.CreateAiSidebarContent(this);
                openSettings = bridge.OpenSettings;
            }

            _aiFloatingPanel = new DialogGraphAiFloatingPanel(aiContent, openSettings);
            _graphHost.Add(_aiFloatingPanel);

            var aiBtn = new Button(() => _aiFloatingPanel?.Toggle()) { tooltip = "Toggle AI Assistant" };
            aiBtn.AddToClassList("dlg-ai-launcher");

            var icon = new Label("✦");
            icon.AddToClassList("dlg-ai-launcher-icon");
            aiBtn.Add(icon);

            var lbl = new Label("AI");
            lbl.AddToClassList("dlg-ai-launcher-label");
            aiBtn.Add(lbl);

            _graphHost.Add(aiBtn);
        }

        private void BuildMinimapOverlayButton()
        {
            _minimapOverlayBtn = new Button(OnClickToggleMinimap)
            {
                tooltip = "Show or hide the minimap overlay"
            };
            _minimapOverlayBtn.AddToClassList("dlg-minimap-overlay-btn");

            _minimapOverlayIcon = DialogGraphIconManager.CreateImage(
                DialogGraphIconId.ToolbarAutoLayout,
                "dgs-icon--md",
                "dgs-icon--muted");
            _minimapOverlayIcon.name = "minimap-overlay-icon";
            _minimapOverlayBtn.Add(_minimapOverlayIcon);

            _graphView.AttachMinimapToggle(_minimapOverlayBtn);
            _graphView.Add(_minimapOverlayBtn);
            UpdateMinimapToggleText();
        }

        #endregion

        #region ---------------- Sub-toolbar Handlers ----------------

        private void OnClickSave()
        {
            if (_graphView == null) return;

            if (!string.IsNullOrWhiteSpace(_loadedGraphName))
            {
                _graphView.SaveGraph(_loadedGraphName);
                SaveCurrentViewState();
                EditorPrefs.SetString(PrefKeyLastGraph, _loadedGraphName);
                TrackRecentGraph(_loadedGraphName);
                RefreshGraphEditorState();
                return;
            }

            var suggestion = string.IsNullOrEmpty(_loadedGraphName) ? "DialogGraph" : _loadedGraphName;
            var existing   = DialogGraphAssetPaths.GetVisibleGraphNames();
            var isEmpty    = _graphView.IsGraphEmptyForSave();
            var previousLoadedGraphName = _loadedGraphName;

            SaveGraphPromptWindow.Open(
                currentName:     suggestion,
                loadedGraphName: _loadedGraphName,
                existingNames:   existing,
                isGraphEmpty:    isEmpty,
                onConfirm: finalName =>
                {
                    _graphView.SaveGraph(finalName);
                    _loadedGraphName = finalName;
                    _graphViewLoadedGraphName = finalName;
                    if (!string.Equals(previousLoadedGraphName, finalName, StringComparison.OrdinalIgnoreCase))
                    {
                        _graphView.LoadGraph(finalName, false, focusStartNode: false);
                    }

                    if (!_openGraphNames.Contains(finalName))
                    {
                        _openGraphNames.Add(finalName);
                    }

                    SaveCurrentViewState();
                    EditorPrefs.SetString(PrefKeyLastGraph, finalName);
                    SaveTabPersistence();
                    TrackRecentGraph(finalName);
                    ShowCanvas();
                });
        }

        private void OnClickValidate() => ValidateGraph();

        private void OnClickFormat()
        {
            FormatLayout();
        }

        public void FormatLayout(string preserveNodeGuid = null)
        {
            if (_graphView == null) return;
            DialogGraphLayoutFormatterEditor.FormatLinkedSubgraphOnly(
                _graphView, DialogGraphLayoutFormatterEditor.DefaultSettings, preserveNodeGuid);
            _graphView.MarkDirtyRepaint();
            if (!string.IsNullOrEmpty(_loadedGraphName))
                _graphView.SaveGraph(_loadedGraphName);
            _sidebar?.RebuildFromGraph();
        }

        private void OnClickToggleMinimap()
        {
            if (_graphView != null && _graphView.ConsumeMinimapToggleClickSuppression())
            {
                return;
            }

            DialogGraphEditorSettings.MinimapVisible = !DialogGraphEditorSettings.MinimapVisible;
            UpdateMinimapToggleText();
        }

        #endregion

        #region ---------------- View State ----------------

        private void SaveCurrentViewState()
        {
            if (string.IsNullOrEmpty(_loadedGraphName) || _graphView == null) return;
            var state = new GraphViewCameraState
            {
                position = _graphView.GetViewPosition(),
                scale    = _graphView.GetViewScale()
            };
            EditorPrefs.SetString(ViewStatePrefKeyPrefix + _loadedGraphName, JsonUtility.ToJson(state));
        }

        private bool TryLoadViewState(string graphName, out GraphViewCameraState state)
        {
            state = default;
            if (string.IsNullOrWhiteSpace(graphName)) return false;
            var key = ViewStatePrefKeyPrefix + graphName;
            if (!EditorPrefs.HasKey(key)) return false;
            var json = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                state = JsonUtility.FromJson<GraphViewCameraState>(json);
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning($"[GraphTabController] Ignoring invalid saved view state for '{graphName}': {ex.Message}");
                EditorPrefs.DeleteKey(key);
                state = default;
                return false;
            }

            return state.scale.x > 0f;
        }

        private void RestoreViewState(GraphViewCameraState state, int delayMs)
            => _graphView?.RestoreViewTransform(state.position, state.scale, delayMs);

        [Serializable]
        private struct GraphViewCameraState
        {
            public Vector3 position;
            public Vector3 scale;
        }

        #endregion

        #region ---------------- Recent Graphs ----------------

        private static List<string> GetRecentGraphs()
        {
            var raw = EditorPrefs.GetString(PrefKeyRecentGraphs, string.Empty);
            if (string.IsNullOrEmpty(raw)) return new List<string>();
            return new List<string>(raw.Split('|').Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private static void SaveRecentGraphs(List<string> list)
            => EditorPrefs.SetString(PrefKeyRecentGraphs, string.Join("|", list));

        private static void TrackRecentGraph(string graphName)
        {
            if (string.IsNullOrEmpty(graphName)) return;
            var recents = GetRecentGraphs();
            recents.Remove(graphName);
            recents.Insert(0, graphName);
            if (recents.Count > MaxRecentGraphs)
                recents = recents.Take(MaxRecentGraphs).ToList();
            SaveRecentGraphs(recents);
        }

        #endregion

        #region ---------------- UI Helpers ----------------

        private PopupField<string> CreateLabelVisibilityPopup()
        {
            var options = Enum.GetNames(typeof(EdgeLabelVisibility)).ToList();
            var current = DialogGraphEditorSettings.EdgeLabelVisibility.ToString();
            var selectedIndex = Mathf.Max(0, options.IndexOf(current));

            var popup = new PopupField<string>("Labels", options, selectedIndex)
            {
                tooltip = "Set visibility mode for edge connection labels"
            };

            popup.AddToClassList("dlg-popup");
            popup.AddToClassList("tight-label");
            popup.style.minWidth = 100;
            popup.style.maxWidth = 130;
            popup.style.marginRight = 2;
            popup.style.marginLeft = 0;

            popup.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse(evt.newValue, out EdgeLabelVisibility mode))
                {
                    return;
                }

                DialogGraphEditorSettings.EdgeLabelVisibility = mode;
                DialogGraphEditorSettings.NotifySettingsChanged();
            });

            return popup;
        }

        private static Button CreateToolbarButton(
            string label,
            DialogGraphIconId? iconId,
            string tooltip,
            Action onClick,
            string styleClass = null,
            bool iconOnly = false)
        {
            var btn = new Button { tooltip = string.IsNullOrEmpty(tooltip) ? label : tooltip };
            btn.AddToClassList("dlg-btn");
            if (!string.IsNullOrEmpty(styleClass))
            {
                btn.AddToClassList(styleClass);
            }

            if (onClick != null)
            {
                btn.clicked += onClick;
            }

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;

            if (iconId.HasValue && DialogGraphIconManager.HasIcon(iconId.Value))
            {
                var image = DialogGraphIconManager.CreateImage(iconId.Value, "dgs-icon--md");
                image.name = ButtonIconElementName;
                image.style.marginRight = iconOnly ? 0 : 8;
                row.Add(image);
            }

            var labelElement = new Label(iconOnly ? string.Empty : label);
            labelElement.name = ButtonLabelElementName;
            labelElement.style.flexShrink = 1;
            labelElement.style.overflow = Overflow.Hidden;
#if UNITY_2021_3_OR_NEWER
            labelElement.style.textOverflow = TextOverflow.Ellipsis;
#endif
            row.Add(labelElement);

            btn.text = string.Empty;
            btn.Add(row);
            btn.style.marginRight = 2;
            btn.style.minHeight = 26;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 10;
            return btn;
        }

        private void UpdateMinimapToggleText()
        {
            if (_minimapOverlayBtn == null)
            {
                return;
            }

            _minimapOverlayBtn.text = string.Empty;
            _minimapOverlayBtn.tooltip = GetMinimapToggleLabel();
            _minimapOverlayBtn.EnableInClassList(
                "dlg-minimap-overlay-btn--active",
                DialogGraphEditorSettings.MinimapVisible);

            if (_minimapOverlayIcon != null)
            {
                _minimapOverlayIcon.EnableInClassList("dgs-icon--brand", DialogGraphEditorSettings.MinimapVisible);
                _minimapOverlayIcon.EnableInClassList("dgs-icon--muted", !DialogGraphEditorSettings.MinimapVisible);
            }
        }

        private static string GetMinimapToggleLabel()
            => DialogGraphEditorSettings.MinimapVisible ? "Hide minimap" : "Show minimap";

        private static VisualElement MakeSep()
        {
            var sep = new VisualElement();
            sep.AddToClassList("ds-sub-toolbar-sep");
            return sep;
        }

        private static string GetNodeGuid(Node node) => node switch
        {
            DialogNodeView d           => d.GUID,
            ChoiceNodeView c           => c.GUID,
            ActionNodeView a           => a.GUID,
            ConditionNodeView co       => co.GUID,
            VariableMutationNodeView v => v.GUID,
            GraphJumpNodeView g        => g.GUID,
            StartNodeView s            => s.GUID,
            EndNodeView e              => e.GUID,
            _                          => string.Empty
        };

        /// <summary>Selects and frames a node by GUID in the graph view.</summary>
        public bool FocusNodeByGuid(string guid)
        {
            if (_graphView == null || string.IsNullOrWhiteSpace(guid)) return false;
            var target = _graphView.nodes.ToList()
                .OfType<Node>()
                .FirstOrDefault(n => string.Equals(GetNodeGuid(n), guid, StringComparison.Ordinal));
            if (target == null) return false;
            _graphView.ClearSelection();
            _graphView.AddToSelection(target);
            _graphView.FrameSelection();
            _owner.SwitchTab(DialogSystemMainWindow.TabType.Graphs);
            _owner.Focus();
            return true;
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
            _sidebar?.RebuildFromGraph();
            _owner.Repaint();
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
            ApplySpritesToNodes(new List<DialogGraphEditorWindow.CharacterBinding>
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

        public DialogActionSO CreateActionAsset(
            string actionId,
            string payloadJson = "{}",
            bool waitForCompletion = true,
            float delay = 0f,
            bool pingAsset = true)
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

        public string GetAiSidebarPrompt() => string.Empty;

        public string GetAiSidebarTone() => string.Empty;

        public string GetAiSidebarInstructionPreset() => string.Empty;

        #endregion
    }
}
