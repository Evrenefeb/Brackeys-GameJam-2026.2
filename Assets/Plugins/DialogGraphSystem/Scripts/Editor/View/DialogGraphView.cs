using DialogSystem.EditorTools.Localization;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.EditorTools.Util;
using DialogSystem.EditorTools.Utils;
using DialogSystem.EditorTools.View.Elements;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Settings;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.View
{
    /// <summary>
    /// Main GraphView responsible for authoring dialog graphs.
    /// - Owns all node views & edges.
    /// - Synchronizes with a DialogGraph ScriptableObject (via GraphId).
    /// - Handles Undo/Redo, link management, duplication, and save/load.
    /// </summary>
    public class DialogGraphView : GraphView
    {
        #region ---------------- Fields ----------------

        [SerializeField] private bool doDebug = false;

        private static readonly Vector2 kDefaultNodeSize = new Vector2(200, 120);
        private static readonly Vector2 kDefaultGroupPadding = new Vector2(48f, 56f);
        private const string StartAliasGuid = "Start";
        private const string EndAliasGuid = "End";
        private static readonly Vector2 kDefaultStartPosition = new Vector2(-320f, 80f);
        private static readonly Vector2 kDefaultEndPosition = new Vector2(720f, 80f);
        private readonly DialogGraphCanvasService _canvasService;
        private readonly DialogGraphPersistenceService _persistenceService = new DialogGraphPersistenceService();
        private readonly DialogGraphEdgeSyncService _edgeSyncService = new DialogGraphEdgeSyncService();
        private readonly DialogGraphClipboardService _clipboardService = new DialogGraphClipboardService();
        private readonly Action _onEditorSettingsChanged;
        private bool _isSubscribedToEditorSettings;
        private bool _suppressGroupCallbacks;
        private string _loadedGraphSnapshotGraphId;
        private DialogGraphExport _loadedGraphSnapshot;
        private bool _hasLoadedGraphSnapshot;

        /// <summary>
        /// Logical id / file name of the graph (without .asset extension).
        /// The editor window sets this before calling SaveGraph/LoadGraph.
        /// </summary>
        public string graphId { get; set; } = "DialogGraph";

        /// <summary>The editor host that owns this graph view.</summary>
        public IDialogGraphOwner GraphOwner { get; set; }

        #endregion

        #region ---------------- Ctor ----------------

        /// <summary>
        /// Constructs the graph view:
        /// - Configures zoom, dragging, selection.
        /// - Adds grid + minimap.
        /// - Registers context menu for node creation.
        /// - Hooks GraphViewChanged for Undo-aware data sync.
        /// </summary>
        public DialogGraphView()
        {
            name = "Dialog Graph";
            _canvasService = new DialogGraphCanvasService(this);
            _onEditorSettingsChanged = ApplyEditorSettings;

            // Core interactions — zoom limits driven by editor settings
            ApplyEditorSettings();
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            _canvasService.Setup();
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                SubscribeToEditorSettings();
                ApplyEditorSettings();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => UnsubscribeFromEditorSettings());

            // Context menu: create nodes at mouse position
            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                // 1. Create Stuff (Top)
                evt.menu.InsertAction(0, "Create/Dialog Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateDialogNode("New Node", false, pos.x, pos.y);
                });

                evt.menu.InsertAction(1, "Create/Choice Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateChoiceNode("Choice", false, pos.x, pos.y);
                });

                evt.menu.InsertAction(2, "Create/Action Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateActionNode("Action", false, pos.x, pos.y);
                });

                evt.menu.InsertAction(3, "Create/Variable Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateVariableMutationNode("Set Variable", false, pos.x, pos.y);
                });

                evt.menu.InsertAction(4, "Create/Condition Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateConditionNode("Condition", false, pos.x, pos.y);
                });

                evt.menu.InsertAction(5, "Create/Graph Jump Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateGraphJumpNode("Graph Jump", false, pos.x, pos.y);
                });

                evt.menu.InsertAction(6, "Create/Outcome Node", action =>
                {
                    Vector2 mouse = action.eventInfo?.mousePosition ?? Vector2.zero;
                    Vector2 pos = contentViewContainer.WorldToLocal(mouse);
                    _ = CreateOutcomeNode("Outcome", false, pos.x, pos.y);
                });

                // 2. Layout
                evt.menu.AppendSeparator("");
                evt.menu.AppendAction("Layout/Group Selected Nodes", _ => GroupSelectedNodes());
                evt.menu.AppendAction("Layout/Format Graph", _ => DialogGraphLayoutFormatterEditor.FormatLinkedSubgraphOnly(this, DialogGraphLayoutFormatterEditor.DefaultSettings, null));

                // 3. AI Commands
                evt.menu.AppendSeparator("");
                AppendAiContextAction(evt.menu, "AI/Continue Branch", DialogGraphAiQuickAction.ContinueBranch);
                AppendAiContextAction(evt.menu, "AI/Suggest Choices", DialogGraphAiQuickAction.SuggestChoices);
                AppendAiContextAction(evt.menu, "AI/Rewrite Node", DialogGraphAiQuickAction.RewriteDialog);
                AppendAiContextAction(evt.menu, "AI/Fix Grammar", DialogGraphAiQuickAction.GrammarFix);
            }));

            // Global graph change callback (undo-friendly)
            graphViewChanged = OnGraphViewChanged;
            elementsAddedToGroup = OnElementsAddedToGroup;
            elementsRemovedFromGroup = OnElementsRemovedFromGroup;
            groupTitleChanged = OnGroupTitleChanged;

            // Always ensure there is a start/end pair in the view
            EnsureStartEndNodes();

            // Initial hint state
            _canvasService.UpdateCanvasHint(nodes.ToList());
        }

        #endregion

        #region ---------------- Editor Settings ----------------

        private void ApplyEditorSettings()
        {
            SetupZoom(DialogGraphEditorSettings.MinZoom, DialogGraphEditorSettings.MaxZoom);
            _canvasService.SetMinimapVisible(DialogGraphEditorSettings.MinimapVisible);
        }

        public void AttachMinimapToggle(Button button)
        {
            _canvasService.AttachMiniMapToggle(button);
        }

        public bool ConsumeMinimapToggleClickSuppression()
        {
            return _canvasService.ConsumeToggleClickSuppression();
        }

        private void SubscribeToEditorSettings()
        {
            if (_isSubscribedToEditorSettings)
            {
                return;
            }

            DialogGraphEditorSettings.OnSettingsChanged += _onEditorSettingsChanged;
            _isSubscribedToEditorSettings = true;
        }

        private void UnsubscribeFromEditorSettings()
        {
            if (!_isSubscribedToEditorSettings)
            {
                return;
            }

            DialogGraphEditorSettings.OnSettingsChanged -= _onEditorSettingsChanged;
            _isSubscribedToEditorSettings = false;
        }

        #endregion

        #region ---------------- Node Create ----------------

        /// <summary>
        /// Ensures there is a DialogGraph asset for the current GraphId.
        /// If it does not exist, an empty asset is auto-created under the conversations folder.
        /// </summary>
        private DialogGraph RequireGraphAsset()
        {
            var asset = _persistenceService.RequireGraphAsset(graphId, out var created);

            if (doDebug && created)
                Debug.Log($"[DialogGraphView] Auto-created DialogGraph for: {graphId}");

            return asset;
        }

        /// <summary>
        /// Creates a new DialogNode:
        /// - Allocates a sub-asset in the DialogGraph with Undo.
        /// - Spawns a DialogNodeView bound to it at the desired position.
        /// </summary>
        public DialogNodeView CreateDialogNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.nodes ??= new List<DialogNode>();

            // Decide position
            Vector2 size = new Vector2(220, 170);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            // 1) Create data sub-asset
            var data = ScriptableObject.CreateInstance<DialogNode>();
            data.name = "Node_" + nodeName;
            data.SetGuid();
            data.SetPosition(position);

            // 2) Register Undo for creation + graph change
            DialogUndoUtility.RegisterCreatedNode("Create Dialog Node", asset, data);

            // 3) Attach to graph asset
            asset.nodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            // Auto-stamp locale key and register source-table entry
            DialogLocalizationAutoSync.OnDialogNodeCreated(data, asset);

            // 4) Create the view bound to this data
            var view = new DialogNodeView(nodeName, this)
            {
                GUID = data.GetGuid()
            };
            view.SetPosition(new Rect(position, size));
            AddElement(view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created DialogNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Creates a new ChoiceNode with one or more answers.
        /// </summary>
        public ChoiceNodeView CreateChoiceNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.choiceNodes ??= new List<ChoiceNode>();

            // Decide position
            Vector2 size = new Vector2(260, 220);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            // 1) Create data sub-asset
            var data = ScriptableObject.CreateInstance<ChoiceNode>();
            data.name = "ChoiceNode";
            data.SetGuid();
            data.SetPosition(position);

            // 2) Register Undo for creation + graph change
            DialogUndoUtility.RegisterCreatedNode("Create Choice Node", asset, data);

            // 3) Attach to graph asset
            asset.choiceNodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            // Auto-stamp locale keys and register source-table entries
            DialogLocalizationAutoSync.OnChoiceNodeCreated(data, asset);

            // 4) Create the view bound to this data
            var view = new ChoiceNodeView(nodeName, this)
            {
                GUID = data.GetGuid()
            };

            view.SetPosition(new Rect(position, size));
            view.LoadNodeData(null); // start with one empty row
            AddElement(view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created ChoiceNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Creates a new ActionNode view and backing sub-asset.
        /// </summary>
        public ActionNodeView CreateActionNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.actionNodes ??= new List<ActionNode>();

            // Decide position
            Vector2 size = new Vector2(240, 170);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            // 1) Create data sub-asset
            var data = ScriptableObject.CreateInstance<ActionNode>();
            data.name = "ActionNode";
            data.SetGuid();
            data.SetPosition(position);

            // 2) Register Undo for creation + graph change
            DialogUndoUtility.RegisterCreatedNode("Create Action Node", asset, data);

            // 3) Attach to graph asset
            asset.actionNodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            // 4) Create the view bound to this data
            var view = new ActionNodeView(data.GetGuid(), this);
            view.Initialize(data, position, nodeName);
            view.LoadNodeData("", "", false, 0f);
            view.SetPosition(new Rect(position, size));
            AddElement(view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created ActionNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Creates a new ConditionNode view and backing sub-asset.
        /// </summary>
        public ConditionNodeView CreateConditionNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.conditionNodes ??= new List<ConditionNode>();

            Vector2 size = new Vector2(340, 220);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            var data = ScriptableObject.CreateInstance<ConditionNode>();
            data.name = "ConditionNode";
            data.SetGuid();
            data.SetPosition(position);

            DialogUndoUtility.RegisterCreatedNode("Create Condition Node", asset, data);

            asset.conditionNodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            var view = new ConditionNodeView(data.GetGuid(), this);
            view.Initialize(data, position, nodeName);
            view.LoadNodeData(data.variableName, data.valueType, data.conditionOperator, data.comparisonValue, data.missingVariableResult);
            view.SetPosition(new Rect(position, size));
            AddElement(view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created ConditionNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Creates a new VariableMutationNode view and backing sub-asset.
        /// </summary>
        public VariableMutationNodeView CreateVariableMutationNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.variableMutationNodes ??= new List<VariableMutationNode>();

            Vector2 size = new Vector2(340, 220);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            var data = ScriptableObject.CreateInstance<VariableMutationNode>();
            data.name = "VariableMutationNode";
            data.SetGuid();
            data.SetPosition(position);

            DialogUndoUtility.RegisterCreatedNode("Create Variable Node", asset, data);

            asset.variableMutationNodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            var view = new VariableMutationNodeView(data.GetGuid(), this);
            view.Initialize(data, position, nodeName);
            view.LoadNodeData(data.variableName, data.valueType, data.operation, data.value);
            view.SetPosition(new Rect(position, size));
            AddElement(view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created VariableMutationNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Creates a new GraphJumpNode view and backing sub-asset.
        /// </summary>
        public GraphJumpNodeView CreateGraphJumpNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.graphJumpNodes ??= new List<GraphJumpNode>();

            Vector2 size = new Vector2(340, 220);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            var data = ScriptableObject.CreateInstance<GraphJumpNode>();
            data.name = "GraphJumpNode";
            data.SetGuid();
            data.SetPosition(position);
            data.EnsureReference();

            DialogUndoUtility.RegisterCreatedNode("Create Graph Jump Node", asset, data);

            asset.graphJumpNodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            var view = new GraphJumpNodeView(data.GetGuid(), this);
            view.Initialize(data, position, nodeName);
            view.LoadNodeData(data.targetGraph);
            view.SetPosition(new Rect(position, size));
            AddElement(view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created GraphJumpNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Creates a new OutcomeNode view and backing sub-asset.
        /// Automatically connects the node to End so it is valid on creation.
        /// </summary>
        public OutcomeNodeView CreateOutcomeNode(string nodeName, bool autoPosition = false, float xPos = 0, float yPos = 0)
        {
            var asset = RequireGraphAsset();
            asset.outcomeNodes ??= new List<OutcomeNode>();

            Vector2 size = new Vector2(320f, 220f);
            Vector2 position = new Vector2(xPos, yPos);

            if (autoPosition)
            {
                Vector2 viewCenter = contentViewContainer.WorldToLocal(layout.center);
                position = viewCenter - (size * 0.5f);
            }

            var data = ScriptableObject.CreateInstance<OutcomeNode>();
            data.name = "OutcomeNode";
            data.SetGuid();
            data.SetPosition(position);

            DialogUndoUtility.RegisterCreatedNode("Create Outcome Node", asset, data);

            asset.outcomeNodes.Add(data);
            AssetDatabase.AddObjectToAsset(data, asset);
            EditorUtility.SetDirty(asset);

            var view = new OutcomeNodeView(data.GetGuid());
            view.Initialize(data, position, nodeName);
            view.LoadNodeData(data.outcomeId, data.displayName, data.description);
            view.SetPosition(new Rect(position, size));
            AddElement(view);

            TryAutoConnectOutcomeToEnd(asset, view);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Created OutcomeNode '{nodeName}' at {position}");

            return view;
        }

        /// <summary>
        /// Returns an existing StartNode view or creates one at the given position.
        /// </summary>
        private StartNodeView GetOrCreateStartView(string guid, Vector2 pos)
        {
            var existing = nodes.ToList().OfType<StartNodeView>().FirstOrDefault();
            if (existing != null)
            {
                if (!string.IsNullOrEmpty(guid)) existing.GUID = guid;
                var r = existing.GetPosition();
                var size = (r.width <= 0f || r.height <= 0f)
                    ? kDefaultNodeSize
                    : new Vector2(r.width, r.height);
                existing.SetPosition(new Rect(pos, size));
                return existing;
            }

            var view = new StartNodeView(string.IsNullOrEmpty(guid) ? System.Guid.NewGuid().ToString("N") : guid);
            AddElement(view);
            view.SetPosition(new Rect(pos, kDefaultNodeSize));
            return view;
        }

        /// <summary>
        /// Returns an existing EndNode view or creates one at the given position.
        /// </summary>
        private EndNodeView GetOrCreateEndView(string guid, Vector2 pos)
        {
            var existing = nodes.ToList().OfType<EndNodeView>().FirstOrDefault();
            if (existing != null)
            {
                if (!string.IsNullOrEmpty(guid)) existing.GUID = guid;
                var r = existing.GetPosition();
                var size = (r.width <= 0f || r.height <= 0f)
                    ? kDefaultNodeSize
                    : new Vector2(r.width, r.height);
                existing.SetPosition(new Rect(pos, size));
                return existing;
            }

            var view = new EndNodeView(string.IsNullOrEmpty(guid) ? System.Guid.NewGuid().ToString("N") : guid);
            AddElement(view);
            view.SetPosition(new Rect(pos, kDefaultNodeSize));
            return view;
        }

        /// <summary>
        /// Ensures there is exactly one non-deletable Start and End node in the view.
        /// </summary>
        public void EnsureStartEndNodes()
        {
            bool hasStart = nodes.ToList().Any(n => n is StartNodeView);
            bool hasEnd = nodes.ToList().Any(n => n is EndNodeView);

            if (!hasStart)
            {
                var start = GetOrCreateStartView("Start", new Vector2(-320f, 80f));
                start.capabilities &= ~Capabilities.Deletable;
            }
            if (!hasEnd)
            {
                var end = GetOrCreateEndView("End", new Vector2(720f, 80f));
                end.capabilities &= ~Capabilities.Deletable;
            }
        }

        #endregion

        #region ---------------- Graph Change Handling ----------------

        /// <summary>
        /// Central callback for all GraphView structural changes (moves, deletions, link changes).
        /// Applies Undo-aware changes to the DialogGraph asset.
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            var asset = LoadGraphAsset(graphId);
            using var mutationBatch = new DialogGraphMutationBatch("Graph View Change");

            // Never allow deleting Start/End views
            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                var groupedNodesToPreserve = change.elementsToRemove
                    .OfType<Group>()
                    .SelectMany(group => group.containedElements.OfType<GraphElement>())
                    .OfType<Node>()
                    .ToHashSet();

                change.elementsToRemove = change.elementsToRemove
                    .Where(e => e is not StartNodeView && e is not EndNodeView)
                    .Where(e => e is not Node node || !groupedNodesToPreserve.Contains(node))
                    .ToList();
            }

            // Node moves → update data positions with a single Undo step
            if (change.movedElements != null && change.movedElements.Count > 0 && asset != null)
            {
                // One undo step for the whole move batch (Dialog / Choice / Action / Start / End)
                DialogUndoUtility.RecordGraphAndNodes("Move Nodes", asset, CollectUndoTargetsForMovedElements(asset, change.movedElements));

                foreach (var element in change.movedElements)
                {
                    // Dialog node
                    if (element is DialogNodeView dv)
                    {
                        var data = asset.nodes?.FirstOrDefault(n => n != null && n.GetGuid() == dv.GUID);
                        if (data != null)
                        {
                            data.SetPosition(dv.GetPosition().position);
                            EditorUtility.SetDirty(data);
                        }
                        continue;
                    }

                    // Choice node
                    if (element is ChoiceNodeView cv)
                    {
                        var data = asset.choiceNodes?.FirstOrDefault(n => n != null && n.GetGuid() == cv.GUID);
                        if (data != null)
                        {
                            data.SetPosition(cv.GetPosition().position);
                            EditorUtility.SetDirty(data);
                        }
                        continue;
                    }

                    // Action node
                    if (element is ActionNodeView av)
                    {
                        var data = asset.actionNodes?.FirstOrDefault(n => n != null && n.GetGuid() == av.GUID);
                        if (data != null)
                        {
                            data.SetPosition(av.GetPosition().position);
                            EditorUtility.SetDirty(data);
                        }
                        continue;
                    }

                    // Condition node
                    if (element is ConditionNodeView cnv)
                    {
                        var data = asset.conditionNodes?.FirstOrDefault(n => n != null && n.GetGuid() == cnv.GUID);
                        if (data != null)
                        {
                            data.SetPosition(cnv.GetPosition().position);
                            EditorUtility.SetDirty(data);
                        }
                        continue;
                    }

                    // Variable mutation node
                    if (element is VariableMutationNodeView vmv)
                    {
                        var data = asset.variableMutationNodes?.FirstOrDefault(n => n != null && n.GetGuid() == vmv.GUID);
                        if (data != null)
                        {
                            data.SetPosition(vmv.GetPosition().position);
                            EditorUtility.SetDirty(data);
                        }
                        continue;
                    }

                    if (element is GraphJumpNodeView gjv)
                    {
                        var data = asset.graphJumpNodes?.FirstOrDefault(n => n != null && n.GetGuid() == gjv.GUID);
                        if (data != null)
                        {
                            data.SetPosition(gjv.GetPosition().position);
                            EditorUtility.SetDirty(data);
                        }
                        continue;
                    }

                    // START node (no ScriptableObject, stored directly on DialogGraph)
                    if (element is StartNodeView sv)
                    {
                        asset.startPosition = sv.GetPosition().position;
                        asset.startInitialized = true;
                        continue;
                    }

                    // END node (no ScriptableObject, stored directly on DialogGraph)
                    if (element is EndNodeView ev)
                    {
                        asset.endPosition = ev.GetPosition().position;
                        asset.endInitialized = true;
                        continue;
                    }

                    if (element is DialogGraphGroupView groupView)
                    {
                        var record = asset.GetOrCreateGroupLayoutForEditor(groupView.GroupId, groupView.title);
                        Vector2 oldPos = record.bounds.position;
                        Vector2 newPos = groupView.GetPosition().position;
                        Vector2 delta = newPos - oldPos;

                        if (delta.sqrMagnitude > 0.0001f)
                        {
                            ApplyDeltaToReroutePoints(asset, groupView, delta);
                        }
                    }
                }

                SyncNodePositionsToAsset(asset);
                SyncGroupsToAsset(asset);
                EditorUtility.SetDirty(asset);
            }

            // Deletions (nodes or edges)
            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0 && asset != null)
            {
                // Group reroute handle deletions by edge to handle index shifts correctly
                var rerouteDeletions = new Dictionary<DialogGraphEdge, List<int>>();
                var otherElements = new List<GraphElement>();

                foreach (var element in change.elementsToRemove)
                {
                    if (element is DialogGraphEdge.RerouteHandle handle)
                    {
                        if (!rerouteDeletions.ContainsKey(handle.ParentEdge))
                            rerouteDeletions[handle.ParentEdge] = new List<int>();
                        rerouteDeletions[handle.ParentEdge].Add(handle.Index);
                    }
                    else
                    {
                        otherElements.Add(element);
                    }
                }

                // Apply reroute deletions first (if any)
                foreach (var kvp in rerouteDeletions)
                {
                    var edge = kvp.Key;
                    var indices = kvp.Value.OrderByDescending(i => i).ToList();
                    foreach (var idx in indices)
                    {
                        edge.DeleteReroutePoint(idx);
                    }
                }

                if (otherElements.Count > 0)
                {
                    DialogUndoUtility.RecordGraphAndNodes("Delete Elements", asset, CollectUndoTargetsForRemovedElements(asset, otherElements));

                    foreach (var element in otherElements)
                    {
                        if (element is Edge edge)
                        {
                            HandleDeleteEdge(asset, edge);
                            continue;
                        }

                        if (element is DialogNodeView || element is ChoiceNodeView || element is ActionNodeView || element is ConditionNodeView || element is VariableMutationNodeView || element is GraphJumpNodeView || element is OutcomeNodeView)
                        {
                            HandleDeleteNode(asset, element);
                            continue;
                        }

                        if (element is DialogGraphGroupView group)
                        {
                            asset.RemoveGroupLayout(group.GroupId);
                            continue;
                        }
                    }
                }

                SyncGroupsToAsset(asset);
                EditorUtility.SetDirty(asset);
                mutationBatch.RequestSave();
            }

            // Edge create → add/update link + set choice nextNodeGUID
            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0 && asset != null)
            {
                DialogUndoUtility.RecordGraphAndNodes("Connect Nodes", asset, CollectUndoTargetsForCreatedEdges(asset, change.edgesToCreate));

                for (var i = 0; i < change.edgesToCreate.Count; i++)
                {
                    var e = change.edgesToCreate[i];

                    // Upgrade standard Edge to DialogGraphEdge so reroute points work immediately
                    if (e is not DialogGraphEdge && e.output != null && e.input != null)
                    {
                        var outPort = e.output;
                        var inPort = e.input;

                        outPort.Disconnect(e);
                        inPort.Disconnect(e);

                        var newEdge = DialogGraphEdgeFactory.Connect(outPort, inPort);
                        if (newEdge != null)
                        {
                            change.edgesToCreate[i] = newEdge;
                            e = newEdge;
                        }
                    }

                    var fromGuid = ExtractGuidFromView(e.output?.node as Node);
                    var toGuid = ExtractGuidFromView(e.input?.node as Node);
                    if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid)) continue;

                    int portIdx = GetOutputPortIndex(e.output);
                    var fromPortKey = GetOutputPortKey(asset, e.output, assignMissingChoiceId: true);
                    var toPortKey = GetInputPortKey(e.input);

                    var link = DialogGraphLinkMutationService.AddOrReplaceLink(asset, fromGuid, toGuid, portIdx, fromPortKey, toPortKey);

                    // Stamp link identity onto the visual edge so future reroute code can
                    // look up the backing EdgeLayoutRecord without scanning all graph links.
                    if (e is DialogGraphEdge dge && link != null)
                    {
                        DialogGraphEdgeFactory.ApplyLinkIdentity(dge, link);
                        dge.Initialize(asset, this);
                    }

                    // If from is a ChoiceNode, update the saved next
                    var cSo = asset.choiceNodes?.FirstOrDefault(c => c != null && c.GetGuid() == fromGuid);
                    if (cSo != null && cSo.choices != null && portIdx >= 0 && portIdx < cSo.choices.Count)
                    {
                        cSo.choices[portIdx].nextNodeGUID = toGuid;
                        EditorUtility.SetDirty(cSo);
                    }
                }

                EditorUtility.SetDirty(asset);
                mutationBatch.RequestSave();

                // Update UI mapping in the view too
                foreach (var e in change.edgesToCreate)
                {
                    if (e.output?.node is ChoiceNodeView chv)
                    {
                        var toGuid = ExtractGuidFromView(e.input?.node as Node);
                        chv.SetNextForPort((Port)e.output, toGuid);
                    }
                }
            }

            // Refresh the empty-state hint after any structural change
            schedule.Execute(_ => _canvasService.UpdateCanvasHint(nodes.ToList())).ExecuteLater(0);

            return change;
        }

        #endregion

        #region ---------------- Port Rules ----------------

        /// <summary>
        /// Prevents connecting a port to itself or to the same node; allows only opposite directions.
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(port =>
                startPort != port &&
                startPort.node != port.node &&
                startPort.direction != port.direction).ToList();
        }

        #endregion

        #region ---------------- Utils ----------------

        /// <summary>
        /// Returns true if the graph has no user nodes or edges (only start/end).
        /// Used to warn about overwriting with an empty graph.
        /// </summary>
        public bool IsGraphEmptyForSave()
        {
            var anyNodes = this.nodes != null && this.nodes.ToList().Count > 0;
            var anyEdges = this.edges != null && this.edges.ToList().Count > 0;
            return !(anyNodes || anyEdges);
        }

        /// <summary>
        /// Clears all visual elements from the GraphView and re-adds the start/end nodes.
        /// </summary>
        public void ClearGraph()
        {
            graphElements.ToList().ForEach(RemoveElement);
            EnsureStartEndNodes();
        }

        private static string ExtractGuidFromView(Node nodeView)
        {
            if (nodeView is EndNodeView ev) return ev.GUID;
            if (nodeView is StartNodeView sv) return sv.GUID;
            if (nodeView is DialogNodeView dv) return dv.GUID;
            if (nodeView is ChoiceNodeView cv) return cv.GUID;
            if (nodeView is ActionNodeView av) return av.GUID;
            if (nodeView is ConditionNodeView cnv) return cnv.GUID;
            if (nodeView is VariableMutationNodeView vmv) return vmv.GUID;
            if (nodeView is GraphJumpNodeView gjv) return gjv.GUID;
            if (nodeView is OutcomeNodeView onv) return onv.GUID;
            return string.Empty;
        }

        private static int GetOutputPortIndex(Port output)
        {
            if (output?.node is DialogNodeView) return 0;
            if (output?.node is ChoiceNodeView chv)
                return chv.GetPortIndex(output);
            if (output?.node is StartNodeView) return 0;
            if (output?.node is ActionNodeView) return 0;
            if (output?.node is VariableMutationNodeView) return 0;
            if (output?.node is GraphJumpNodeView) return 0;
            if (output?.node is OutcomeNodeView) return 0;
            if (output?.node is ConditionNodeView cnv) return cnv.GetPortIndex(output);
            return 0;
        }

        private static string GetOutputPortKey(DialogGraph asset, Port output, bool assignMissingChoiceId)
        {
            if (output?.node is ChoiceNodeView choiceView)
            {
                var choiceIndex = choiceView.GetPortIndex(output);
                if (choiceIndex < 0)
                {
                    return string.Empty;
                }

                var choiceNode = asset?.choiceNodes?.FirstOrDefault(node =>
                    node != null &&
                    string.Equals(node.GetGuid(), choiceView.GUID, StringComparison.Ordinal));

                if (choiceNode?.choices != null && choiceIndex < choiceNode.choices.Count)
                {
                    var choice = choiceNode.choices[choiceIndex];
                    if (choice != null)
                    {
                        if (assignMissingChoiceId && !choice.HasChoiceId)
                        {
                            choice.AssignChoiceIdIfMissing(Choice.CreateChoiceId());
                            EditorUtility.SetDirty(choiceNode);
                        }

                        output.userData = choice.choiceId ?? string.Empty;
                        return choice.PortKey;
                    }
                }

                return output.userData is string choiceId && !string.IsNullOrWhiteSpace(choiceId)
                    ? DialogGraphPortKeys.ForChoiceId(choiceId)
                    : string.Empty;
            }

            if (output?.node is ConditionNodeView conditionView)
            {
                return conditionView.GetPortIndex(output) == ConditionNode.FalsePortIndex
                    ? DialogGraphPortKeys.False
                    : DialogGraphPortKeys.True;
            }

            if (output?.node is ActionNodeView)
            {
                return DialogGraphPortKeys.ActionSuccess;
            }

            return DialogGraphPortKeys.Default;
        }

        private static string GetInputPortKey(Port input)
        {
            return input == null ? string.Empty : DialogGraphPortKeys.Default;
        }

        private static string CombineAssetPath(string folder, string fileWithExt)
            => $"{folder.TrimEnd('/')}/{fileWithExt.TrimStart('/')}";

        private static string ResolveBoundaryViewGuid(string storedGuid, string aliasGuid)
        {
            return string.IsNullOrWhiteSpace(storedGuid) ? aliasGuid : storedGuid;
        }

        private static Vector2 ResolveBoundaryViewPosition(bool initialized, Vector2 storedPosition, Vector2 fallbackPosition)
        {
            return initialized ? storedPosition : fallbackPosition;
        }

        private static void RegisterViewLookupAlias(Dictionary<string, Node> viewLookup, string guid, Node view)
        {
            if (viewLookup == null || view == null || string.IsNullOrWhiteSpace(guid))
            {
                return;
            }

            viewLookup[guid] = view;
        }

        private DialogGraph LoadGraphAsset(string graphId)
        {
            return _persistenceService.LoadGraphAsset(graphId);
        }

        /// <summary>
        /// Creates a DialogNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.nodes.
        /// </summary>
        private DialogNodeView CreateDialogNodeViewFromData(DialogNode dNode)
        {
            if (dNode == null) return null;

            var title = string.IsNullOrEmpty(dNode.name)
                ? "Node"
                : dNode.name.Replace("Node_", "");

            var view = new DialogNodeView(title, this)
            {
                GUID = dNode.GetGuid()
            };

            var pos = dNode.GetPosition();
            var size = new Vector2(200f, 150f);

            view.SetPosition(new Rect(pos, size));
            view.LoadNodeData(
                dNode.speakerName,
                dNode.questionText,
                title,
                dNode.speakerPortrait,
                dNode.dialogAudio,
                dNode.displayTime,
                dNode.waitForAudioFinish
            );

            AddElement(view);
            return view;
        }

        /// <summary>
        /// Creates a ChoiceNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.choiceNodes.
        /// </summary>
        private ChoiceNodeView CreateChoiceNodeViewFromData(ChoiceNode chNode)
        {
            if (chNode == null) return null;

            var view = new ChoiceNodeView("Choice", this)
            {
                GUID = chNode.GetGuid()
            };

            var pos = chNode.GetPosition();
            var size = new Vector2(260f, 220f);

            view.SetPosition(new Rect(pos, size));
            view.LoadNodeData(chNode.choices);

            AddElement(view);
            return view;
        }

        /// <summary>
        /// Creates an ActionNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.actionNodes.
        /// </summary>
        private ActionNodeView CreateActionNodeViewFromData(ActionNode aNode)
        {
            if (aNode == null) return null;

            var view = new ActionNodeView(aNode.GetGuid(), this);
            var pos = aNode.GetPosition();
            var size = new Vector2(320f, 240f);

            view.Initialize(aNode, pos, "Action");
            view.LoadNodeData(
                aNode.actionId,
                aNode.payloadJson,
                aNode.waitForCompletion,
                aNode.waitSeconds
            );
            view.SetPosition(new Rect(pos, size));

            AddElement(view);
            return view;
        }

        /// <summary>
        /// Creates a ConditionNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.conditionNodes.
        /// </summary>
        private ConditionNodeView CreateConditionNodeViewFromData(ConditionNode cNode)
        {
            if (cNode == null) return null;

            var view = new ConditionNodeView(cNode.GetGuid(), this);
            var pos = cNode.GetPosition();
            var size = new Vector2(340f, 220f);

            view.Initialize(cNode, pos, "Condition");
            view.LoadNodeData(
                cNode.variableName,
                cNode.valueType,
                cNode.conditionOperator,
                cNode.comparisonValue,
                cNode.missingVariableResult);
            view.SetPosition(new Rect(pos, size));

            AddElement(view);
            return view;
        }

        /// <summary>
        /// Creates a VariableMutationNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.variableMutationNodes.
        /// </summary>
        private VariableMutationNodeView CreateVariableMutationNodeViewFromData(VariableMutationNode vNode)
        {
            if (vNode == null) return null;

            var view = new VariableMutationNodeView(vNode.GetGuid(), this);
            var pos = vNode.GetPosition();
            var size = new Vector2(340f, 220f);

            view.Initialize(vNode, pos, "Set Variable");
            view.LoadNodeData(vNode.variableName, vNode.valueType, vNode.operation, vNode.value);
            view.SetPosition(new Rect(pos, size));

            AddElement(view);
            return view;
        }

        private DialogGraphGroupView CreateGroupViewFromRecord(GroupLayoutRecord record, IReadOnlyDictionary<string, Node> viewLookup)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.groupId))
            {
                return null;
            }

            var group = new DialogGraphGroupView(record.groupId, record.title, DeleteGroupKeepNodes, OnGroupChanged);
            group.ApplyLayout(record);
            AddElement(group);

            if (viewLookup != null && record.nodeGuids != null)
            {
                _suppressGroupCallbacks = true;
                try
                {
                    foreach (var guid in record.nodeGuids)
                    {
                        if (string.IsNullOrWhiteSpace(guid) || !viewLookup.TryGetValue(guid, out var nodeView))
                        {
                            continue;
                        }

                        if (nodeView is StartNodeView || nodeView is EndNodeView)
                        {
                            continue;
                        }

                        if (!group.ContainsElement(nodeView))
                        {
                            group.AddElement(nodeView);
                        }
                    }
                }
                finally
                {
                    _suppressGroupCallbacks = false;
                }
            }

            return group;
        }

        /// <summary>
        /// Creates a GraphJumpNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.graphJumpNodes.
        /// </summary>
        private GraphJumpNodeView CreateGraphJumpNodeViewFromData(GraphJumpNode jNode)
        {
            if (jNode == null) return null;

            var view = new GraphJumpNodeView(jNode.GetGuid(), this);
            var pos = jNode.GetPosition();
            var size = new Vector2(340f, 220f);

            view.Initialize(jNode, pos, "Graph Jump");
            view.LoadNodeData(jNode.targetGraph);
            view.SetPosition(new Rect(pos, size));

            AddElement(view);
            return view;
        }

        /// <summary>
        /// Creates an OutcomeNodeView from existing data (used in LoadGraph).
        /// Does NOT modify asset.outcomeNodes.
        /// </summary>
        private OutcomeNodeView CreateOutcomeNodeViewFromData(OutcomeNode oNode)
        {
            if (oNode == null) return null;

            var view = new OutcomeNodeView(oNode.GetGuid());
            var pos = oNode.GetPosition();
            var size = new Vector2(320f, 220f);

            view.Initialize(oNode, pos, "Outcome");
            view.LoadNodeData(oNode.outcomeId, oNode.displayName, oNode.description);
            view.SetPosition(new Rect(pos, size));

            AddElement(view);
            return view;
        }

        public void GroupSelectedNodes()
        {
            var selectedNodes = selection
                .OfType<Node>()
                .Where(IsGroupableNode)
                .Cast<GraphElement>()
                .ToList();

            if (selectedNodes.Count == 0)
            {
                return;
            }

            var asset = LoadGraphAsset(graphId) ?? RequireGraphAsset();
            if (asset == null)
            {
                return;
            }

            DialogUndoUtility.RecordGraphAndNodes("Create Group", asset, CollectUndoTargetsForGroupElements(asset, selectedNodes));

            var bounds = CalculateGroupBounds(selectedNodes);
            var group = new DialogGraphGroupView(Guid.NewGuid().ToString("N"), $"Group {GetNextGroupIndex()}", DeleteGroupKeepNodes, OnGroupChanged);
            group.SetPosition(bounds);
            AddElement(group);
            _suppressGroupCallbacks = true;
            try
            {
                group.AddElements(selectedNodes);
            }
            finally
            {
                _suppressGroupCallbacks = false;
            }

            SyncGroupsToAsset(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            ClearSelection();
            AddToSelection(group);
        }

        private void DeleteGroupKeepNodes(DialogGraphGroupView group)
        {
            if (group == null)
            {
                return;
            }

            var asset = LoadGraphAsset(graphId) ?? RequireGraphAsset();
            if (asset == null)
            {
                return;
            }

            DialogUndoUtility.RecordGraphAndNodes("Delete Group", asset, CollectUndoTargetsForGroupElements(asset, group.containedElements.OfType<GraphElement>()));

            var containedNodes = group.containedElements
                .OfType<GraphElement>()
                .Where(element => element is Node)
                .ToList();

            if (containedNodes.Count > 0)
            {
                _suppressGroupCallbacks = true;
                try
                {
                    group.RemoveElements(containedNodes);
                }
                finally
                {
                    _suppressGroupCallbacks = false;
                }
            }

            RemoveElement(group);
            SyncGroupsToAsset(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private void OnGroupChanged(DialogGraphGroupView _)
        {
            var asset = LoadGraphAsset(graphId);
            if (asset == null)
            {
                return;
            }

            DialogUndoUtility.RecordGraph("Edit Group", asset);
            SyncGroupsToAsset(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private void OnElementsAddedToGroup(Group group, IEnumerable<GraphElement> elements)
        {
            if (_suppressGroupCallbacks)
            {
                return;
            }

            SyncGroupMutationToAsset("Group Nodes");
        }

        private void OnElementsRemovedFromGroup(Group group, IEnumerable<GraphElement> elements)
        {
            if (_suppressGroupCallbacks)
            {
                return;
            }

            SyncGroupMutationToAsset("Ungroup Nodes");
        }

        private void OnGroupTitleChanged(Group group, string title)
        {
            if (_suppressGroupCallbacks)
            {
                return;
            }

            SyncGroupMutationToAsset("Rename Group");
        }

        private void SyncGroupMutationToAsset(string undoLabel)
        {
            var asset = LoadGraphAsset(graphId);
            if (asset == null)
            {
                return;
            }

            DialogUndoUtility.RecordGraphAndNodes(undoLabel, asset, CollectUndoTargetsForAllGroups(asset));
            SyncGroupsToAsset(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private void ApplyDeltaToReroutePoints(DialogGraph asset, DialogGraphGroupView group, Vector2 delta)
        {
            var nodeGuids = group.EnumerateContainedNodeGuids().ToHashSet();

            foreach (var edge in edges.OfType<DialogGraphEdge>())
            {
                if (string.IsNullOrEmpty(edge.linkGuid)) continue;

                if (nodeGuids.Contains(edge.fromGuid) || nodeGuids.Contains(edge.toGuid))
                {
                    var layout = asset.GetEdgeLayout(edge.linkGuid);
                    if (layout != null && layout.reroutePoints != null && layout.reroutePoints.Count > 0)
                    {
                        for (int i = 0; i < layout.reroutePoints.Count; i++)
                        {
                            layout.reroutePoints[i] += delta;
                        }
                        edge.UpdateEdgeControl();
                        edge.RefreshGeometry();
                        EditorUtility.SetDirty(asset);
                    }
                }
            }
        }

        private void HandleDeleteEdge(DialogGraph asset, Edge edge)
        {
            var fromGuid = ExtractGuidFromView(edge.output?.node as Node);
            var toGuid = ExtractGuidFromView(edge.input?.node as Node);
            if (string.IsNullOrEmpty(fromGuid) || string.IsNullOrEmpty(toGuid)) return;

            int portIdx = GetOutputPortIndex(edge.output);
            var fromPortKey = edge is DialogGraphEdge dialogEdge && !string.IsNullOrWhiteSpace(dialogEdge.fromPortKey)
                ? dialogEdge.fromPortKey
                : GetOutputPortKey(asset, edge.output, assignMissingChoiceId: false);

            DialogGraphLinkMutationService.RemoveExactLink(asset, fromGuid, toGuid, portIdx, fromPortKey);

            // Phase 5: Cleanup edge layout metadata
            if (edge is DialogGraphEdge dgEdge)
            {
                asset.RemoveEdgeLayout(dgEdge.linkGuid, fromGuid, toGuid, portIdx);
            }
            else
            {
                asset.RemoveEdgeLayout(null, fromGuid, toGuid, portIdx);
            }

            // If it's a ChoiceNode -> clear its saved next on that port
            var cSo = asset.choiceNodes?.FirstOrDefault(c => c != null && c.GetGuid() == fromGuid);
            if (cSo != null && cSo.choices != null && portIdx >= 0 && portIdx < cSo.choices.Count)
            {
                cSo.choices[portIdx].nextNodeGUID = null;
                EditorUtility.SetDirty(cSo);
            }
        }

        private void HandleDeleteNode(DialogGraph asset, GraphElement element)
        {
            string guid = null;

            if (element is DialogNodeView dView) guid = dView.GUID;
            else if (element is ChoiceNodeView cView) guid = cView.GUID;
            else if (element is ActionNodeView aView) guid = aView.GUID;
            else if (element is ConditionNodeView cnView) guid = cnView.GUID;
            else if (element is VariableMutationNodeView vmView) guid = vmView.GUID;
            else if (element is GraphJumpNodeView gjView) guid = gjView.GUID;
            else if (element is OutcomeNodeView oView) guid = oView.GUID;
            else return;

            if (string.IsNullOrEmpty(guid)) return;

            // 1) remove links to/from this node
            DialogGraphLinkMutationService.RemoveLinksConnectedToNode(asset, guid);

            // Phase 5: Cleanup edge layout metadata for all links connected to this node
            asset.RemoveEdgeLayoutsForNode(guid);
            asset.RemoveGroupLayoutsForNode(guid);

            // 2) purge references from choice nodes (their nextNodeGUID may point to this)
            if (asset.choiceNodes != null)
            {
                foreach (var ch in asset.choiceNodes)
                {
                    if (ch?.choices == null) continue;
                    var changed = false;
                    foreach (var choice in ch.choices)
                    {
                        if (choice != null && choice.nextNodeGUID == guid)
                        {
                            choice.nextNodeGUID = null;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        EditorUtility.SetDirty(ch);
                    }
                }
            }

            // 3) remove the node sub-asset + list entry
            var dSo = asset.nodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (dSo != null)
            {
                asset.nodes.Remove(dSo);
                Undo.DestroyObjectImmediate(dSo);
            }

            var chSo = asset.choiceNodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (chSo != null)
            {
                asset.choiceNodes.Remove(chSo);
                Undo.DestroyObjectImmediate(chSo);
            }

            var aSo = asset.actionNodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (aSo != null)
            {
                asset.actionNodes.Remove(aSo);
                Undo.DestroyObjectImmediate(aSo);
            }

            var conditionSo = asset.conditionNodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (conditionSo != null)
            {
                asset.conditionNodes.Remove(conditionSo);
                Undo.DestroyObjectImmediate(conditionSo);
            }

            var variableSo = asset.variableMutationNodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (variableSo != null)
            {
                asset.variableMutationNodes.Remove(variableSo);
                Undo.DestroyObjectImmediate(variableSo);
            }

            var graphJumpSo = asset.graphJumpNodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (graphJumpSo != null)
            {
                asset.graphJumpNodes.Remove(graphJumpSo);
                Undo.DestroyObjectImmediate(graphJumpSo);
            }

            var outcomeSo = asset.outcomeNodes?.FirstOrDefault(n => n != null && n.GetGuid() == guid);
            if (outcomeSo != null)
            {
                asset.outcomeNodes.Remove(outcomeSo);
                Undo.DestroyObjectImmediate(outcomeSo);
            }
        }

        private List<ScriptableObject> CollectUndoTargetsForMovedElements(DialogGraph asset, IEnumerable<GraphElement> movedElements)
        {
            var targets = new List<ScriptableObject>();
            if (asset == null || movedElements == null)
            {
                return targets;
            }

            foreach (var element in movedElements)
            {
                var guid = ExtractGuidFromView(element as Node);
                var target = FindNodeDataByGuid(asset, guid);
                if (target != null && !targets.Contains(target))
                {
                    targets.Add(target);
                }

                if (element is Group group)
                {
                    foreach (var containedElement in group.containedElements.OfType<GraphElement>())
                    {
                        var containedGuid = ExtractGuidFromView(containedElement as Node);
                        var containedTarget = FindNodeDataByGuid(asset, containedGuid);
                        if (containedTarget != null && !targets.Contains(containedTarget))
                        {
                            targets.Add(containedTarget);
                        }
                    }
                }
            }

            return targets;
        }

        private List<ScriptableObject> CollectUndoTargetsForCreatedEdges(DialogGraph asset, IEnumerable<Edge> createdEdges)
        {
            var targets = new List<ScriptableObject>();
            if (asset == null || createdEdges == null)
            {
                return targets;
            }

            foreach (var edge in createdEdges)
            {
                if (edge?.output?.node is not ChoiceNodeView choiceNodeView)
                {
                    continue;
                }

                var choiceNode = FindNodeDataByGuid(asset, choiceNodeView.GUID);
                if (choiceNode != null && !targets.Contains(choiceNode))
                {
                    targets.Add(choiceNode);
                }
            }

            return targets;
        }

        private List<ScriptableObject> CollectUndoTargetsForRemovedElements(DialogGraph asset, IEnumerable<GraphElement> removedElements)
        {
            var targets = new List<ScriptableObject>();
            if (asset == null || removedElements == null)
            {
                return targets;
            }

            foreach (var element in removedElements)
            {
                switch (element)
                {
                    case Edge edge:
                        {
                            if (edge.output?.node is ChoiceNodeView choiceNodeView)
                            {
                                var choiceNode = FindNodeDataByGuid(asset, choiceNodeView.GUID);
                                if (choiceNode != null && !targets.Contains(choiceNode))
                                {
                                    targets.Add(choiceNode);
                                }
                            }

                            break;
                        }
                    case DialogNodeView dialogNodeView:
                        AddDeleteUndoTargets(asset, dialogNodeView.GUID, targets);
                        break;
                    case ChoiceNodeView choiceNodeView:
                        AddDeleteUndoTargets(asset, choiceNodeView.GUID, targets);
                        break;
                    case ActionNodeView actionNodeView:
                        AddDeleteUndoTargets(asset, actionNodeView.GUID, targets);
                        break;
                    case ConditionNodeView conditionNodeView:
                        AddDeleteUndoTargets(asset, conditionNodeView.GUID, targets);
                        break;
                    case VariableMutationNodeView variableNodeView:
                        AddDeleteUndoTargets(asset, variableNodeView.GUID, targets);
                        break;
                    case GraphJumpNodeView graphJumpNodeView:
                        AddDeleteUndoTargets(asset, graphJumpNodeView.GUID, targets);
                        break;
                    case OutcomeNodeView outcomeNodeView:
                        AddDeleteUndoTargets(asset, outcomeNodeView.GUID, targets);
                        break;
                    case Group group:
                        foreach (var containedElement in group.containedElements.OfType<GraphElement>())
                        {
                            var containedGuid = ExtractGuidFromView(containedElement as Node);
                            AddDeleteUndoTargets(asset, containedGuid, targets);
                        }
                        break;
                }
            }

            return targets;
        }

        private List<ScriptableObject> CollectUndoTargetsForGroupElements(DialogGraph asset, IEnumerable<GraphElement> elements)
        {
            var targets = new List<ScriptableObject>();
            if (asset == null || elements == null)
            {
                return targets;
            }

            foreach (var element in elements)
            {
                var guid = ExtractGuidFromView(element as Node);
                var target = FindNodeDataByGuid(asset, guid);
                if (target != null && !targets.Contains(target))
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private List<ScriptableObject> CollectUndoTargetsForAllGroups(DialogGraph asset)
        {
            if (asset == null)
            {
                return new List<ScriptableObject>();
            }

            var groupNodeTargets = graphElements
                .OfType<Group>()
                .SelectMany(group => group.containedElements.OfType<GraphElement>())
                .ToList();

            return CollectUndoTargetsForGroupElements(asset, groupNodeTargets);
        }

        private void AddDeleteUndoTargets(DialogGraph asset, string deletedGuid, List<ScriptableObject> targets)
        {
            if (asset == null || string.IsNullOrEmpty(deletedGuid) || targets == null)
            {
                return;
            }

            var deletedNode = FindNodeDataByGuid(asset, deletedGuid);
            if (deletedNode != null && !targets.Contains(deletedNode))
            {
                targets.Add(deletedNode);
            }

            if (asset.choiceNodes == null)
            {
                return;
            }

            foreach (var choiceNode in asset.choiceNodes)
            {
                if (choiceNode?.choices == null)
                {
                    continue;
                }

                var referencesDeletedNode = choiceNode.choices.Any(choice => choice != null && choice.nextNodeGUID == deletedGuid);
                if (referencesDeletedNode && !targets.Contains(choiceNode))
                {
                    targets.Add(choiceNode);
                }
            }
        }

        private ScriptableObject FindNodeDataByGuid(DialogGraph asset, string guid)
        {
            if (asset == null || string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var dialogNode = asset.nodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
            if (dialogNode != null)
            {
                return dialogNode;
            }

            var choiceNode = asset.choiceNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
            if (choiceNode != null)
            {
                return choiceNode;
            }

            var actionNode = asset.actionNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
            if (actionNode != null)
            {
                return actionNode;
            }

            var conditionNode = asset.conditionNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
            if (conditionNode != null)
            {
                return conditionNode;
            }

            var variableNode = asset.variableMutationNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
            if (variableNode != null)
            {
                return variableNode;
            }

            var graphJumpNode = asset.graphJumpNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
            if (graphJumpNode != null)
            {
                return graphJumpNode;
            }

            var outcomeNode = asset.outcomeNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
            if (outcomeNode != null)
            {
                return outcomeNode;
            }

            return null;
        }

        private static bool IsGroupableNode(Node node)
        {
            return node is DialogNodeView
                || node is ChoiceNodeView
                || node is ActionNodeView
                || node is ConditionNodeView
                || node is VariableMutationNodeView
                || node is GraphJumpNodeView
                || node is OutcomeNodeView;
        }

        private Rect CalculateGroupBounds(IEnumerable<GraphElement> elements)
        {
            var rects = elements?
                .OfType<Node>()
                .Select(node => node.GetPosition())
                .ToList();

            if (rects == null || rects.Count == 0)
            {
                var center = contentViewContainer.WorldToLocal(layout.center);
                return new Rect(center - new Vector2(180f, 120f), new Vector2(360f, 240f));
            }

            var minX = rects.Min(rect => rect.xMin) - kDefaultGroupPadding.x;
            var minY = rects.Min(rect => rect.yMin) - kDefaultGroupPadding.y;
            var maxX = rects.Max(rect => rect.xMax) + kDefaultGroupPadding.x;
            var maxY = rects.Max(rect => rect.yMax) + kDefaultGroupPadding.y;
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private int GetNextGroupIndex()
        {
            return graphElements.OfType<Group>().Count() + 1;
        }

        private bool SyncGroupsToAsset(DialogGraph asset)
        {
            if (asset == null)
            {
                return false;
            }

            var changed = asset.EnsureGroupLayoutDefaultsForEditor();
            var currentGroups = graphElements.OfType<DialogGraphGroupView>().ToList();
            var activeGroupIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in currentGroups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.GroupId))
                {
                    continue;
                }

                activeGroupIds.Add(group.GroupId);
                var record = asset.GetOrCreateGroupLayoutForEditor(group.GroupId, group.title);

                changed |= SetIfDifferent(ref record.title, group.title);
                changed |= SetIfDifferent(ref record.bounds, group.GetPosition());
                changed |= SetIfDifferent(ref record.colorTint, group.Tint);

                var nodeGuids = group.EnumerateContainedNodeGuids()
.Distinct(StringComparer.Ordinal)
                    .ToList();

                if (!Enumerable.SequenceEqual(record.nodeGuids ?? Enumerable.Empty<string>(), nodeGuids, StringComparer.Ordinal))
                {
                    record.nodeGuids = nodeGuids;
                    changed = true;
                }
            }

            var staleGroupIds = asset.EnumerateGroupLayouts()
                .Select(record => record.groupId)
                .Where(groupId => !string.IsNullOrWhiteSpace(groupId) && !activeGroupIds.Contains(groupId))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var staleGroupId in staleGroupIds)
            {
                changed |= asset.RemoveGroupLayout(staleGroupId);
            }

            return changed;
        }

        /// <summary>
        /// Centers the GraphView camera on a given node.
        /// Uses a delayed schedule so layout is valid before calling FrameSelection().
        /// </summary>
        private void FocusOnNode(Node node)
        {
            if (node == null) return;

            // Run after layout so the viewport has valid dimensions.
            schedule.Execute(() =>
            {
                if (node.parent == null)
                    return;

                ClearSelection();
                AddToSelection(node);

                var viewportWidth = layout.width > 0f ? layout.width : resolvedStyle.width;
                var viewportHeight = layout.height > 0f ? layout.height : resolvedStyle.height;
                if (viewportWidth <= 0f || viewportHeight <= 0f)
                {
                    FrameSelection();
                    return;
                }

                var nodeRect = node.GetPosition();
                var nodeCenter = nodeRect.center;
                var scale = Vector3.one;
                var targetPosition = new Vector3(
                    (viewportWidth * 0.5f) - (nodeCenter.x * scale.x),
                    (viewportHeight * 0.5f) - (nodeCenter.y * scale.y),
                    0f);

                UpdateViewTransform(targetPosition, scale);
            }).ExecuteLater(1);
        }

        /// <summary>
        /// Captures the current GraphView pan position.
        /// </summary>
        public Vector3 GetViewPosition()
        {
            var translate = contentViewContainer.resolvedStyle.translate;
            return new Vector3(translate.x, translate.y, translate.z);
        }

        /// <summary>
        /// Captures the current GraphView zoom scale.
        /// </summary>
        public Vector3 GetViewScale()
        {
            return contentViewContainer.resolvedStyle.scale.value;
        }

        /// <summary>
        /// Restores the GraphView pan/zoom.
        /// A small delay can be used when restoring right after a graph rebuild.
        /// </summary>
        public void RestoreViewTransform(Vector3 position, Vector3 scale, int delayMs = 0)
        {
            void ApplyTransform()
            {
                var safeScale = new Vector3(
                    Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
                    Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
                    Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);

                UpdateViewTransform(position, safeScale);
            }

            if (delayMs > 0)
            {
                schedule.Execute(ApplyTransform).ExecuteLater(delayMs);
                return;
            }

            ApplyTransform();
        }

        #endregion

        #region ---------------- Duplicate Node Functionality ----------------

        /// <summary>
        /// Duplicates all selected Dialog / Choice / Action nodes, including edges between them.
        /// New nodes are offset diagonally and fully wired up.
        /// </summary>
        public void DuplicateSelectedNodes()
        {
            _clipboardService.DuplicateSelectedNodes(this);
        }

        #endregion

        #region ---------------- Save / Load ----------------

        /// <summary>
        /// Writes graph metadata (Start/End positions + links) into the DialogGraph asset.
        /// Node sub-assets are kept in sync live while you edit.
        /// </summary>
        /// <summary>
        /// Saves the graph into a specific asset folder instead of the default Graphs folder.
        /// The folder is created if it doesn't already exist.
        /// Falls back to <see cref="SaveGraph(string)"/> when <paramref name="assetFolderPath"/> is empty.
        /// </summary>
        public void SaveGraph(string fileName, string assetFolderPath)
        {
            if (string.IsNullOrWhiteSpace(assetFolderPath))
            {
                SaveGraph(fileName);
                return;
            }

            _persistenceService.EnsureGraphAssetInFolder(fileName, assetFolderPath);

            // The standard SaveGraph now finds the asset via the discovery scan
            SaveGraph(fileName);
        }

        public void SaveGraph(string fileName)
        {
            var sourceGraphName = graphId;
            var isSaveAsNew = !string.IsNullOrWhiteSpace(sourceGraphName) &&
                              !string.Equals(sourceGraphName, fileName, StringComparison.OrdinalIgnoreCase);
            var shouldSyncViewLayoutToAsset = !isSaveAsNew;

            DialogGraph asset;
            string path;

            if (isSaveAsNew && TryPrepareSaveAsNewTarget(sourceGraphName, fileName, out asset, out path))
            {
                graphId = fileName;
            }
            else
            {
                graphId = fileName;
                asset = _persistenceService.GetOrCreateGraphAssetForSave(fileName, out path);
            }

            var savePreparation = _persistenceService.PrepareForSave(asset);
            if (savePreparation.Errors.Count > 0)
            {
                Debug.LogWarning(
                    $"[DialogGraphView] Migration/default initialization reported issues before save: {string.Join("; ", savePreparation.Errors)}",
                    asset);
            }

            var serializedDataChanged = savePreparation.Changed;

            // Start / End views
            var startView = nodes.ToList().OfType<StartNodeView>().FirstOrDefault();
            var endView = nodes.ToList().OfType<EndNodeView>().FirstOrDefault();

            if (startView != null)
            {
                serializedDataChanged |= SetIfDifferent(ref asset.startGuid, startView.GUID);
                serializedDataChanged |= SetIfDifferent(ref asset.startPosition, startView.GetPosition().position);
                serializedDataChanged |= SetIfDifferent(ref asset.startInitialized, true);
            }
            else
            {
                if (string.IsNullOrEmpty(asset.startGuid))
                {
                    asset.startGuid = Guid.NewGuid().ToString("N");
                    serializedDataChanged = true;
                }

                if (!asset.startInitialized)
                {
                    asset.startPosition = new Vector2(-320f, 80f);
                    asset.startInitialized = true;
                    serializedDataChanged = true;
                }
            }

            if (endView != null)
            {
                serializedDataChanged |= SetIfDifferent(ref asset.endGuid, endView.GUID);
                serializedDataChanged |= SetIfDifferent(ref asset.endPosition, endView.GetPosition().position);
                serializedDataChanged |= SetIfDifferent(ref asset.endInitialized, true);
            }
            else
            {
                if (string.IsNullOrEmpty(asset.endGuid))
                {
                    asset.endGuid = Guid.NewGuid().ToString("N");
                    serializedDataChanged = true;
                }

                if (!asset.endInitialized)
                {
                    asset.endPosition = new Vector2(720f, 80f);
                    asset.endInitialized = true;
                    serializedDataChanged = true;
                }
            }

            serializedDataChanged |= _edgeSyncService.ReconcileSerializedLinks(
                asset,
                edges.ToList().OfType<Edge>(),
                nodes.ToList().OfType<Node>());

            // Wire ChoiceNode nextNodeGUID based on links
            serializedDataChanged |= _edgeSyncService.SyncChoiceNextNodeGuids(asset);

            // Sync view positions back into sub-asset ScriptableObjects so
            // layout changes (including auto-format) survive save/reload.
            if (shouldSyncViewLayoutToAsset)
            {
                serializedDataChanged |= SyncNodePositionsToAsset(asset);
                serializedDataChanged |= SyncGroupsToAsset(asset);
            }

            _persistenceService.SaveAssetIfChanged(asset, serializedDataChanged);
            CaptureLoadedGraphSnapshot(fileName, asset);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Saved DialogGraph to '{path}'");
        }

        private bool TryPrepareSaveAsNewTarget(
            string sourceGraphName,
            string targetGraphName,
            out DialogGraph targetAsset,
            out string targetPath)
        {
            targetAsset = null;
            targetPath = string.Empty;

            var sourceAsset = _persistenceService.LoadGraphAsset(sourceGraphName);
            if (sourceAsset == null)
            {
                return false;
            }

            var currentSourceDto = DialogGraphJsonSerializationUtility.BuildExportDto(
                sourceAsset,
                DialogGraphJsonExportOptions.Default);

            targetAsset = _persistenceService.GetOrCreateGraphAssetForSave(targetGraphName, out targetPath);
            _persistenceService.ReplaceGraphAssetContents(
                targetAsset,
                targetPath,
                currentSourceDto,
                assignNewGraphGuid: true,
                graphNameOverride: targetGraphName);

            if (_hasLoadedGraphSnapshot &&
                string.Equals(_loadedGraphSnapshotGraphId, sourceGraphName, StringComparison.OrdinalIgnoreCase))
            {
                var sourcePath = DialogGraphAssetPaths.ResolveGraphAssetPath(sourceGraphName);
                _persistenceService.ReplaceGraphAssetContents(
                    sourceAsset,
                    sourcePath,
                    _loadedGraphSnapshot,
                    assignNewGraphGuid: false,
                    graphNameOverride: sourceGraphName);
            }

            return true;
        }

        private void CaptureLoadedGraphSnapshot(string graphName, DialogGraph asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(graphName))
            {
                _loadedGraphSnapshotGraphId = string.Empty;
                _loadedGraphSnapshot = default;
                _hasLoadedGraphSnapshot = false;
                return;
            }

            _loadedGraphSnapshot = DialogGraphJsonSerializationUtility.BuildExportDto(
                asset,
                DialogGraphJsonExportOptions.Default);
            _loadedGraphSnapshotGraphId = graphName;
            _hasLoadedGraphSnapshot = true;
        }

        /// <summary>
        /// Reads the current visual position of every node view and writes it back
        /// into the corresponding ScriptableObject sub-asset so that positions
        /// (including those set by the layout formatter) are persisted on save.
        /// </summary>
        private bool SyncNodePositionsToAsset(DialogGraph asset)
        {
            if (asset == null) return false;

            var changed = false;

            // Build a lookup: guid -> current view position
            var posLookup = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            foreach (var node in this.nodes.ToList())
            {
                string guid = ExtractGuidFromView(node as Node);
                if (!string.IsNullOrEmpty(guid))
                    posLookup[guid] = node.GetPosition().position;
            }

            // Dialog nodes
            if (asset.nodes != null)
            {
                foreach (var dn in asset.nodes)
                {
                    if (dn == null) continue;
                    if (posLookup.TryGetValue(dn.GetGuid(), out var pos))
                    {
                        changed |= SetNodePositionIfDifferent(dn, pos);
                    }
                }
            }

            // Choice nodes
            if (asset.choiceNodes != null)
            {
                foreach (var cn in asset.choiceNodes)
                {
                    if (cn == null) continue;
                    if (posLookup.TryGetValue(cn.GetGuid(), out var pos))
                    {
                        changed |= SetNodePositionIfDifferent(cn, pos);
                    }
                }
            }

            // Action nodes
            if (asset.actionNodes != null)
            {
                foreach (var an in asset.actionNodes)
                {
                    if (an == null) continue;
                    if (posLookup.TryGetValue(an.GetGuid(), out var pos))
                    {
                        changed |= SetNodePositionIfDifferent(an, pos);
                    }
                }
            }

            // Condition nodes
            if (asset.conditionNodes != null)
            {
                foreach (var cn in asset.conditionNodes)
                {
                    if (cn == null) continue;
                    if (posLookup.TryGetValue(cn.GetGuid(), out var pos))
                    {
                        changed |= SetNodePositionIfDifferent(cn, pos);
                    }
                }
            }

            // Variable mutation nodes
            if (asset.variableMutationNodes != null)
            {
                foreach (var vn in asset.variableMutationNodes)
                {
                    if (vn == null) continue;
                    if (posLookup.TryGetValue(vn.GetGuid(), out var pos))
                    {
                        changed |= SetNodePositionIfDifferent(vn, pos);
                    }
                }
            }

            if (asset.graphJumpNodes != null)
            {
                foreach (var jn in asset.graphJumpNodes)
                {
                    if (jn == null) continue;
                    if (posLookup.TryGetValue(jn.GetGuid(), out var pos))
                    {
                        changed |= SetNodePositionIfDifferent(jn, pos);
                    }
                }
            }

            if (asset.outcomeNodes != null)
            {
                foreach (var on in asset.outcomeNodes)
                {
                    if (on == null) continue;
                    if (posLookup.TryGetValue(on.GetGuid(), out var pos))
                    {
                        changed |= SetNodePositionIfDifferent(on, pos);
                    }
                }
            }

            return changed;
        }

        private static bool SetNodePositionIfDifferent(BaseNode node, Vector2 position)
        {
            if (node == null || node.GetPosition() == position)
            {
                return false;
            }

            node.SetPosition(position);
            return true;
        }

        private static bool SetIfDifferent<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            return true;
        }

        /// <summary>
        /// Clears the current view and reconstructs all nodes/edges
        /// from the DialogGraph asset on disk, then centers the view on the start node.
        /// </summary>
        public void LoadGraph(string fileName, bool onUndo, bool focusStartNode = true)
        {
            graphId = fileName;

            string path = DialogGraphAssetPaths.ResolveGraphAssetPath(fileName);
            var asset = _persistenceService.LoadGraphAsset(fileName);

            if (asset == null)
            {
                if (doDebug)
                    Debug.LogError("[DialogGraphView] DialogGraph asset not found: " + path);
                return;
            }

            CaptureLoadedGraphSnapshot(fileName, asset);

            ClearGraph();
            var viewLookup = new Dictionary<string, Node>();

            // Start node
            var startGuid = ResolveBoundaryViewGuid(asset.startGuid, StartAliasGuid);
            var startPosition = ResolveBoundaryViewPosition(asset.startInitialized, asset.startPosition, kDefaultStartPosition);
            var startView = GetOrCreateStartView(startGuid, startPosition);
            RegisterViewLookupAlias(viewLookup, startView.GUID, startView);
            RegisterViewLookupAlias(viewLookup, asset.startGuid, startView);
            RegisterViewLookupAlias(viewLookup, StartAliasGuid, startView);

            // End node
            var endGuid = ResolveBoundaryViewGuid(asset.endGuid, EndAliasGuid);
            var endPosition = ResolveBoundaryViewPosition(asset.endInitialized, asset.endPosition, kDefaultEndPosition);
            var endView = GetOrCreateEndView(endGuid, endPosition);
            RegisterViewLookupAlias(viewLookup, endView.GUID, endView);
            RegisterViewLookupAlias(viewLookup, asset.endGuid, endView);
            RegisterViewLookupAlias(viewLookup, EndAliasGuid, endView);

            // Dialog nodes
            if (asset.nodes != null)
            {
                foreach (var dNode in asset.nodes)
                {
                    if (dNode == null) continue;
                    var view = CreateDialogNodeViewFromData(dNode);
                    if (view != null)
                        viewLookup[dNode.GetGuid()] = view;
                }
            }

            // Choice nodes
            if (asset.choiceNodes != null)
            {
                foreach (var chNode in asset.choiceNodes)
                {
                    if (chNode == null) continue;
                    var view = CreateChoiceNodeViewFromData(chNode);
                    if (view != null)
                        viewLookup[chNode.GetGuid()] = view;
                }
            }

            // Action nodes
            if (asset.actionNodes != null)
            {
                foreach (var aNode in asset.actionNodes)
                {
                    if (aNode == null) continue;
                    var view = CreateActionNodeViewFromData(aNode);
                    if (view != null)
                        viewLookup[aNode.GetGuid()] = view;
                }
            }

            // Condition nodes
            if (asset.conditionNodes != null)
            {
                foreach (var cNode in asset.conditionNodes)
                {
                    if (cNode == null) continue;
                    var view = CreateConditionNodeViewFromData(cNode);
                    if (view != null)
                        viewLookup[cNode.GetGuid()] = view;
                }
            }

            // Variable mutation nodes
            if (asset.variableMutationNodes != null)
            {
                foreach (var vNode in asset.variableMutationNodes)
                {
                    if (vNode == null) continue;
                    var view = CreateVariableMutationNodeViewFromData(vNode);
                    if (view != null)
                        viewLookup[vNode.GetGuid()] = view;
                }
            }

            if (asset.graphJumpNodes != null)
            {
                foreach (var jNode in asset.graphJumpNodes)
                {
                    if (jNode == null) continue;
                    var view = CreateGraphJumpNodeViewFromData(jNode);
                    if (view != null)
                        viewLookup[jNode.GetGuid()] = view;
                }
            }

            if (asset.outcomeNodes != null)
            {
                foreach (var oNode in asset.outcomeNodes)
                {
                    if (oNode == null) continue;
                    var view = CreateOutcomeNodeViewFromData(oNode);
                    if (view != null)
                        viewLookup[oNode.GetGuid()] = view;
                }
            }

            foreach (var groupRecord in asset.EnumerateGroupLayouts())
            {
                CreateGroupViewFromRecord(groupRecord, viewLookup);
            }

            // Rebuild edges
            if (asset.links != null)
            {
                foreach (var link in asset.links)
                {
                    if (link == null) continue;
                    var edge = _edgeSyncService.CreateVisualEdgeFromLink(asset, viewLookup, link, this);
                    if (edge != null)
                    {
                        AddElement(edge);
                        edge.RefreshGeometry();
                    }
                }
            }

            if (doDebug)
                Debug.Log($"[DialogGraphView] Loaded DialogGraph from '{path}'");

            // Refresh empty-state hint
            _canvasService.UpdateCanvasHint(nodes.ToList());

            // Center the view on the Start node after everything is laid out.
            if (!onUndo && focusStartNode)
                FocusOnNode(startView);
        }

        #region ---------------- Selection ----------------

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            UpdateEdgeFocusHighlights();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            UpdateEdgeFocusHighlights();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            UpdateEdgeFocusHighlights();
        }

        private void UpdateEdgeFocusHighlights()
        {
            var selectedElements = selection.ToList();
            bool hasSelection = selectedElements.Count > 0;

            if (!hasSelection)
            {
                // Reset all to default
                foreach (var edge in edges.ToList().OfType<DialogGraphEdge>())
                {
                    edge.SetFocusState(DialogGraphEdge.EdgeFocusState.Default);
                }
                return;
            }

            // Identify selected nodes and edges
            var selectedNodes = new HashSet<Node>();
            var selectedEdges = new HashSet<DialogGraphEdge>();

            foreach (var item in selectedElements)
            {
                if (item is Node node) selectedNodes.Add(node);
                else if (item is DialogGraphEdge edge) selectedEdges.Add(edge);
            }

            // Apply focus states
            foreach (var edge in edges.ToList().OfType<DialogGraphEdge>())
            {
                if (selectedEdges.Contains(edge))
                {
                    edge.SetFocusState(DialogGraphEdge.EdgeFocusState.Highlighted);
                }
                else if (selectedNodes.Any(n => edge.input?.node == n || edge.output?.node == n))
                {
                    edge.SetFocusState(DialogGraphEdge.EdgeFocusState.Normal);
                }
                else
                {
                    edge.SetFocusState(DialogGraphEdge.EdgeFocusState.Faded);
                }
            }
        }

        #endregion

        /// <summary>
        /// Clears the current dialog graph (all Dialog / Choice / Action nodes and links),
        /// after a confirmation dialog. The operation is fully Undo-able.
        /// Start and End nodes are kept.
        /// </summary>
        public void ClearGraphWithConfirmation()
        {
            var asset = LoadGraphAsset(graphId);
            if (asset == null)
            {
                if (doDebug)
                    Debug.LogWarning($"[DialogGraphView] Cannot clear graph – asset for '{graphId}' not found.");
                return;
            }

            // Confirmation prompt
            bool confirm = EditorUtility.DisplayDialog(
                "Clear this dialog graph?",
                "This will delete all Dialog, Choice, and Action nodes and all connections in this graph.\n\n" +
                "The Start and End nodes will be kept.\n\n" +
                "You can undo this via Edit → Undo.",
                "Clear Graph",
                "Cancel");

            if (!confirm)
                return;

            // Record Undo for the graph asset (and its hierarchy via your utility)
            DialogUndoUtility.RecordGraphHierarchy("Clear Graph", asset);

            // Ensure lists exist
            asset.nodes ??= new List<DialogNode>();
            asset.choiceNodes ??= new List<ChoiceNode>();
            asset.actionNodes ??= new List<ActionNode>();
            asset.conditionNodes ??= new List<ConditionNode>();
            asset.variableMutationNodes ??= new List<VariableMutationNode>();
            asset.graphJumpNodes ??= new List<GraphJumpNode>();
            asset.outcomeNodes ??= new List<OutcomeNode>();
            asset.links ??= new List<GraphLink>();

            // 1) Clear link table
            asset.links.Clear();

            // Phase 5: Clear all edge layout metadata
            asset.ClearAllEdgeLayouts();

            // 2) Destroy node sub-assets with Undo so they can be restored
            if (asset.nodes != null)
            {
                foreach (var n in asset.nodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.nodes.Clear();
            }

            if (asset.choiceNodes != null)
            {
                foreach (var n in asset.choiceNodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.choiceNodes.Clear();
            }

            if (asset.actionNodes != null)
            {
                foreach (var n in asset.actionNodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.actionNodes.Clear();
            }

            if (asset.conditionNodes != null)
            {
                foreach (var n in asset.conditionNodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.conditionNodes.Clear();
            }

            if (asset.variableMutationNodes != null)
            {
                foreach (var n in asset.variableMutationNodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.variableMutationNodes.Clear();
            }

            if (asset.graphJumpNodes != null)
            {
                foreach (var n in asset.graphJumpNodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.graphJumpNodes.Clear();
            }

            if (asset.outcomeNodes != null)
            {
                foreach (var n in asset.outcomeNodes.ToArray())
                {
                    if (n == null) continue;
                    Undo.DestroyObjectImmediate(n);
                }
                asset.outcomeNodes.Clear();
            }

            asset.ClearAllGroupLayouts();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            // 3) Clear visual graph elements except Start/End
            var toRemove = graphElements.ToList()
                .Where(e =>
                    e is Group ||
                    e is Edge ||
                    (e is Node n && n is not StartNodeView && n is not EndNodeView))
                .ToList();

            foreach (var ge in toRemove)
                RemoveElement(ge);

            // Ensure Start/End exist (in case something went wrong visually)
            EnsureStartEndNodes();

            // 4) Re-frame the start node for a clean, centered view
            var startView = nodes.ToList().OfType<StartNodeView>().FirstOrDefault();
            FocusOnNode(startView);

            if (doDebug)
                Debug.Log($"[DialogGraphView] Cleared graph '{graphId}' (Undo available).");
        }

        #endregion

        private void AppendAiContextAction(DropdownMenu menu, string path, DialogGraphAiQuickAction action)
        {
            menu.AppendAction(
                path,
                _ => RunAiContextAction(action),
                _ => DialogGraphAiBridgeLocator.Current?.IsAvailable == true
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
        }

        private void AddAiGenericMenuItem(GenericMenu menu, string path, DialogGraphAiQuickAction action)
        {
            if (DialogGraphAiBridgeLocator.Current?.IsAvailable == true)
            {
                menu.AddItem(new GUIContent(path), false, () => RunAiContextAction(action));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(path));
            }
        }

        private void TryAutoConnectOutcomeToEnd(DialogGraph asset, OutcomeNodeView outcomeView)
        {
            if (asset == null || outcomeView?.outputPort == null)
            {
                return;
            }

            var endView = nodes.ToList().OfType<EndNodeView>().FirstOrDefault();
            if (endView?.inputPort == null)
            {
                return;
            }

            var link = DialogGraphLinkMutationService.AddOrReplaceLink(
                asset,
                outcomeView.GUID,
                endView.GUID,
                0,
                DialogGraphPortKeys.Default,
                DialogGraphPortKeys.Default);

            if (link == null)
            {
                return;
            }

            var edge = DialogGraphEdgeFactory.ConnectFromLink(outcomeView.outputPort, endView.inputPort, link);
            if (edge == null)
            {
                return;
            }

            AddElement(edge);
            edge.Initialize(asset, this);
            edge.RefreshGeometry();
            EditorUtility.SetDirty(asset);
        }

        private void RunAiContextAction(DialogGraphAiQuickAction action)
        {
            var bridge = DialogGraphAiBridgeLocator.Current;
            if (bridge == null || !bridge.IsAvailable) return;

            if (GraphOwner == null) return;

            if (!bridge.TryRunQuickAction(GraphOwner, action, out var error))
            {
                EditorUtility.DisplayDialog("AI Command Failed", error, "OK");
            }
        }

        /// <summary>
        /// GraphView wrapper for editor-only node groups.
        /// Stores a stable identity and exposes lightweight tint actions.
        /// </summary>
        internal sealed class DialogGraphGroupView : Group
        {
            private static readonly IReadOnlyList<(string Label, Color Tint)> TintPresets = new[]
            {
            ("Blue", new Color(0.26f, 0.49f, 0.93f, 0.16f)),
            ("Green", new Color(0.18f, 0.62f, 0.38f, 0.16f)),
            ("Amber", new Color(0.84f, 0.55f, 0.16f, 0.16f)),
            ("Rose", new Color(0.79f, 0.28f, 0.42f, 0.16f)),
            ("Slate", new Color(0.45f, 0.50f, 0.60f, 0.16f))
        };

            private readonly Action<DialogGraphGroupView> _onDeleteRequested;
            private readonly Action<DialogGraphGroupView> _onChanged;
            private readonly ColorField _colorField;

            public DialogGraphGroupView(
                string groupId,
                string title,
                Action<DialogGraphGroupView> onDeleteRequested,
                Action<DialogGraphGroupView> onChanged)
            {
                if (string.IsNullOrWhiteSpace(groupId))
                {
                    throw new ArgumentException("groupId must not be null or whitespace.", nameof(groupId));
                }

                GroupId = groupId;
                this.title = string.IsNullOrWhiteSpace(title) ? "Group" : title;
                _onDeleteRequested = onDeleteRequested;
                _onChanged = onChanged;
                viewDataKey = groupId;
                autoUpdateGeometry = true;

                capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable | Capabilities.Copiable;
                this.AddManipulator(new ContextualMenuManipulator(PopulateContextMenu));

                _colorField = new ColorField
                {
                    value = Tint,
                    style =
                {
                    width = 40,
                    height = 16,
                    marginLeft = 10,
                    alignSelf = Align.Center,
                    visibility = Visibility.Visible
                }
                };
                _colorField.RegisterValueChangedCallback(evt =>
                {
                    ApplyTint(evt.newValue);
                    _onChanged?.Invoke(this);
                });
                headerContainer.Add(_colorField);

                ApplyTint(DefaultTint);
            }

            public string GroupId { get; }

            public Color Tint { get; private set; }

            public Color DefaultTint => TintPresets[0].Tint;

            public void ApplyLayout(GroupLayoutRecord record)
            {
                if (record == null)
                {
                    return;
                }

                title = string.IsNullOrWhiteSpace(record.title) ? "Group" : record.title;
                SetPosition(record.bounds);
                ApplyTint(record.colorTint.a <= 0f ? DefaultTint : record.colorTint);
            }

            public void ApplyTint(Color tint)
            {
                Tint = tint;
                if (_colorField != null && _colorField.value != tint)
                {
                    _colorField.SetValueWithoutNotify(tint);
                }

                var bodyTint = tint;
                bodyTint.a = Mathf.Clamp(tint.a <= 0f ? 0.16f : tint.a, 0.08f, 0.32f);

                var headerTint = tint;
                headerTint.a = Mathf.Clamp01(Mathf.Max(bodyTint.a + 0.18f, 0.30f));

                style.backgroundColor = new StyleColor(bodyTint);
                headerContainer.style.backgroundColor = new StyleColor(headerTint);
            }

            public IEnumerable<string> EnumerateContainedNodeGuids()
            {
                return containedElements
                    .OfType<Node>()
                    .Select(ExtractGuidFromNode)
                    .Where(guid => !string.IsNullOrWhiteSpace(guid));
            }

            private void PopulateContextMenu(ContextualMenuPopulateEvent evt)
            {
                evt.menu.AppendSeparator();
                foreach (var preset in TintPresets)
                {
                    evt.menu.AppendAction(
                        $"Set Group Color/{preset.Label}",
                        _ =>
                        {
                            ApplyTint(preset.Tint);
                            _onChanged?.Invoke(this);
                        });
                }

                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete Group (Keep Nodes)", _ => _onDeleteRequested?.Invoke(this));
            }

            private static string ExtractGuidFromNode(Node node)
            {
                return node switch
                {
                    DialogNodeView dialogNode => dialogNode.GUID,
                    ChoiceNodeView choiceNode => choiceNode.GUID,
                    ActionNodeView actionNode => actionNode.GUID,
                    ConditionNodeView conditionNode => conditionNode.GUID,
                    VariableMutationNodeView variableNode => variableNode.GUID,
                    GraphJumpNodeView graphJumpNode => graphJumpNode.GUID,
                    OutcomeNodeView outcomeNode => outcomeNode.GUID,
                    _ => string.Empty
                };
            }
        }
    }
}
