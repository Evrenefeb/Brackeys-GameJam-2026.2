using CoreActionRegistryService = DialogSystem.EditorTools.Services.DialogActionRegistryService;
using CoreCharacterRegistryService = DialogSystem.EditorTools.Services.DialogCharacterRegistryService;
using DialogSystem.EditorTools.Resources;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.EditorTools.Settings;
using DialogSystem.Runtime.Definitions;
using DialogSystem.EditorTools.View.Elements.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Sidebar panel with tabs: Characters / Actions.
    ///
    /// Characters  - rename speakers and assign portrait sprites in bulk.
    /// Actions     - edit Action IDs, payloads and timing in bulk.
    /// AI          - per-node AI tools, rewrite, and command routing.
    /// </summary>
    public class EditorSidebarWindow : VisualElement
    {
        private readonly IDialogGraphOwner _owner;

        private enum Tab
        {
            Characters,
            Actions,
            Validate
        }

        private Tab _currentTab = Tab.Characters;

        #region ---------------- Header ----------------

        private Toolbar _headerBar;
        private Button _collapseBtn;
        private Button _rescanBtn;
        private Button _applyBtn;
        private Label _titleLabel;

        #endregion

        #region ---------------- Tab Bar & Search ----------------

        private Toolbar _tabBar;
        private ToolbarToggle _tabCharacters;
        private ToolbarToggle _tabActions;
        private ToolbarToggle _tabValidate;
        private VisualElement _searchRow;
        private ToolbarSearchField _searchField;
        private Label _summaryLabel;

        // Sub-tab bar — Registered / In Graph
        private VisualElement _subTabBar;
        private ToolbarToggle _subTabRegistered;
        private ToolbarToggle _subTabInGraph;
        private int _actionsSubTab = 1;    // 0 = Registered, 1 = In Graph
        private int _charactersSubTab = 1; // 0 = Registered, 1 = In Graph

        #endregion

        #region ---------------- Content ----------------

        private ScrollView _listView;

        #endregion

        #region ---------------- Characters ----------------

        private readonly Dictionary<string, CharacterRow> _characterRows = new();

        #endregion

        #region ---------------- Actions ----------------

        private readonly Dictionary<string, ActionRow> _actionRows = new();

        #endregion

        #region ---------------- Context ----------------

        private ObjectField _sceneContextField;
        private ObjectField _environmentField;
        private TextField _sceneGoalField;
        private TextField _toneField;
        private TextField _extraRulesField;

        #endregion

        #region ---------------- Validation ----------------

        private DialogGraphValidationResult _lastValidationResult;
        private string _lastValidationGraphName;

        #endregion


        #region ---------------- Constructor ----------------

        public EditorSidebarWindow(IDialogGraphOwner owner)
        {
            _owner = owner;

            AddToClassList("dlg-char-sidebar");

            BuildHeader();
            BuildTabs();
            BuildSubTabs();
            BuildList();

            SetTab(Tab.Characters, refresh: true);
        }

        #endregion

        #region ---------------- Build UI ----------------

        private void BuildHeader()
        {
            _headerBar = new Toolbar();
            _headerBar.AddToClassList("dlg-char-toolbar");

            _collapseBtn = MakeToolbarButton(
                "⮌",
                DialogGraphIconId.ActionCollapse,
                "Collapse sidebar",
                () => _owner.ToggleSidebar(),
                false);

            _headerBar.Add(_collapseBtn);

            _titleLabel = new Label("Graph Inspector");
            _titleLabel.AddToClassList("dlg-char-title");
            _headerBar.Add(_titleLabel);

            _rescanBtn = MakeToolbarButton(
                "SCAN",
                null,
                "Rescan content from nodes",
                OnClickRescan,
                false);

            _rescanBtn.AddToClassList("dlg-char-btn");
            _headerBar.Add(_rescanBtn);

            _applyBtn = MakeToolbarButton(
                "APPLY",
                null,
                "Apply edits to matching nodes",
                OnClickApply,
                false);

            _applyBtn.AddToClassList("dlg-char-btn");
            _headerBar.Add(_applyBtn);

            Add(_headerBar);
        }

        private void BuildTabs()
        {
            _tabBar = new Toolbar();
            _tabBar.AddToClassList("dlg-tab-bar");

            _tabCharacters = new ToolbarToggle { text = "Characters" };
            _tabCharacters.AddToClassList("dlg-tab");
            _tabCharacters.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    _tabActions.SetValueWithoutNotify(false);
                    _tabValidate.SetValueWithoutNotify(false);
                    SetTab(Tab.Characters, refresh: true);
                }
                else if (!_tabActions.value && !_tabValidate.value)
                {
                    _tabCharacters.SetValueWithoutNotify(true);
                }
            });
            _tabBar.Add(_tabCharacters);

            _tabActions = new ToolbarToggle { text = "Actions" };
            _tabActions.AddToClassList("dlg-tab");
            _tabActions.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    _tabCharacters.SetValueWithoutNotify(false);
                    _tabValidate.SetValueWithoutNotify(false);
                    SetTab(Tab.Actions, refresh: true);
                }
                else if (!_tabCharacters.value && !_tabValidate.value)
                {
                    _tabActions.SetValueWithoutNotify(true);
                }
            });
            _tabBar.Add(_tabActions);

            _tabValidate = new ToolbarToggle { text = "Validate" };
            _tabValidate.AddToClassList("dlg-tab");
            _tabValidate.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    _tabCharacters.SetValueWithoutNotify(false);
                    _tabActions.SetValueWithoutNotify(false);
                    SetTab(Tab.Validate, refresh: true);
                }
                else if (!_tabCharacters.value && !_tabActions.value)
                {
                    _tabValidate.SetValueWithoutNotify(true);
                }
            });
            _tabBar.Add(_tabValidate);

            _tabCharacters.SetValueWithoutNotify(true);

            Add(_tabBar);

            _searchRow = new VisualElement();
            _searchRow.AddToClassList("dlg-search-row");

            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("dlg-sidebar-search");
            _searchField.RegisterValueChangedCallback(_ => FilterVisible());
            _searchRow.Add(_searchField);
            Add(_searchRow);

            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("dlg-sidebar-summary");
            Add(_summaryLabel);
        }

        private void BuildList()
        {
            _listView = new ScrollView(ScrollViewMode.Vertical);
            _listView.AddToClassList("dlg-char-list");
            Add(_listView);
        }

        private void BuildSubTabs()
        {
            _subTabBar = new VisualElement();
            _subTabBar.AddToClassList("dlg-subtab-bar");
            _subTabBar.style.display = DisplayStyle.None;

            // Only "In Graph" sub-tab is kept. The "Registered" registry is now in the global tabs.
            _subTabInGraph = new ToolbarToggle { text = "In Graph" };
            _subTabInGraph.AddToClassList("dlg-subtab");
            _subTabInGraph.SetValueWithoutNotify(true);
            _subTabInGraph.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue) { _subTabInGraph.SetValueWithoutNotify(true); return; }
                if (_currentTab == Tab.Actions)    _actionsSubTab    = 1;
                else if (_currentTab == Tab.Characters) _charactersSubTab = 1;
                RebuildFromGraph();
            });

            _subTabBar.Add(_subTabInGraph);
            Add(_subTabBar);
        }

        private Button MakeToolbarButton(
            string fallbackText,
            DialogGraphIconId? iconId,
            string tooltip,
            Action onClick,
            bool showTextWithIcon)
        {
            var btn = new Button(onClick) { tooltip = tooltip };
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;

            if (iconId.HasValue && DialogGraphIconManager.HasIcon(iconId.Value))
            {
                var img = DialogGraphIconManager.CreateImage(iconId.Value, "dgs-icon--sm");
                btn.Add(img);

                if (!showTextWithIcon)
                {
                    return btn;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackText))
            {
                if (!iconId.HasValue)
                {
                    btn.text = fallbackText;
                    return btn;
                }

                var label = new Label(fallbackText);
                label.AddToClassList("dlg-toolbar-button-text");
                btn.Add(label);
            }

            return btn;
        }

        #endregion

        #region ---------------- External API ----------------

        public void ClearAll()
        {
            _characterRows.Clear();
            _actionRows.Clear();
            _lastValidationResult = null;
            _lastValidationGraphName = null;
            _listView?.Clear();
        }

        public void RebuildFromGraph()
        {
            _listView.Clear();

            switch (_currentTab)
            {
                case Tab.Characters:
                    BuildCharactersFace();
                    break;

                case Tab.Actions:
                    BuildActionsFace();
                    break;

                case Tab.Validate:
                    BuildValidationFace();
                    break;
            }

            FilterVisible();
        }

        /// <summary>
        /// Shows validation results inline in the sidebar list view,
        /// replacing whatever was displayed without opening a separate window.
        /// </summary>
        private void ShowValidationResultLegacy(DialogGraphValidationResult result)
        {
            if (result == null) return;
            _listView.Clear();
            _searchRow.style.display  = DisplayStyle.None;
            _subTabBar.style.display  = DisplayStyle.None;

            // Summary header
            var summary = new Label(result.IsValid
                ? $"✓ Graph is valid  ({result.WarningCount} warnings, {result.InfoCount} info)"
                : $"✗ {result.ErrorCount} error(s)  ·  {result.WarningCount} warning(s)  ·  {result.InfoCount} info");
            summary.AddToClassList("dlg-sidebar-summary");
            summary.style.whiteSpace = WhiteSpace.Normal;
            _listView.Add(summary);

            // Back button
            var backBtn = new Button(() =>
            {
                _searchRow.style.display = DisplayStyle.Flex;
                SetTab(_currentTab, refresh: true);
            }) { text = "← Back" };
            backBtn.AddToClassList("dlg-ai-btn");
            backBtn.style.marginBottom = 6;
            _listView.Add(backBtn);

            if (result.Issues.Count == 0)
            {
                _listView.Add(CreateEmptyState("No issues found", "The graph passed all validation checks."));
                return;
            }

            AddValidationIssueSection(result, DialogGraphValidationSeverity.Error, "Errors", "invalid");
            AddValidationIssueSection(result, DialogGraphValidationSeverity.Warning, "Warnings", "used");
            AddValidationIssueSection(result, DialogGraphValidationSeverity.Info, "Info", "registered");
        }

        public void ShowValidationResult(DialogGraphValidationResult result)
        {
            _lastValidationResult = result;
            _lastValidationGraphName = _owner.GetCurrentGraphName();
            SetTab(Tab.Validate, refresh: true);
        }

        private void BuildValidationFace()
        {
            var currentGraphName = _owner.GetCurrentGraphName();
            var hasCurrentResult = _lastValidationResult != null &&
                                   string.Equals(_lastValidationGraphName, currentGraphName, StringComparison.Ordinal);
            var result = hasCurrentResult ? _lastValidationResult : null;

            _summaryLabel.text = result == null
                ? "Run validation to inspect graph errors, warnings, and info."
                : result.IsValid
                    ? $"Valid: {result.WarningCount} warning(s), {result.InfoCount} info."
                    : $"{result.ErrorCount} error(s), {result.WarningCount} warning(s), {result.InfoCount} info.";

            var actionsRow = new VisualElement();
            actionsRow.AddToClassList("action-top-bar");
            actionsRow.Add(CreateAiButton("Validate Graph", true, () => _owner.ValidateGraph()));
            actionsRow.Add(CreateAiButton("Fix Locale Keys", true, FixMissingLocaleKeys));
            _listView.Add(actionsRow);

            if (result == null)
            {
                _listView.Add(CreateEmptyState("No validation run yet", "Use Validate Graph to populate issues for the current graph."));
                return;
            }

            _listView.Add(CreateActionSectionHeader("Validation Results", result.IsValid ? "registered" : "invalid"));

            if (result.Issues.Count == 0)
            {
                _listView.Add(CreateEmptyState("No issues found", "The graph passed all validation checks."));
                return;
            }

            AddValidationIssueSection(result, DialogGraphValidationSeverity.Error, "Errors", "invalid");
            AddValidationIssueSection(result, DialogGraphValidationSeverity.Warning, "Warnings", "used");
            AddValidationIssueSection(result, DialogGraphValidationSeverity.Info, "Info", "registered");
        }

        private void AddValidationIssueSection(
            DialogGraphValidationResult result,
            DialogGraphValidationSeverity severity,
            string title,
            string variant)
        {
            var issues = result?.Issues
                .Where(issue => issue.Severity == severity)
                .ToList() ?? new List<DialogGraphValidationIssue>();

            _listView.Add(CreateActionSectionHeader($"{title} ({issues.Count})", variant));

            if (issues.Count == 0)
            {
                _listView.Add(CreateEmptyState($"No {title.ToLowerInvariant()}", $"Validation found no {title.ToLowerInvariant()} for this graph."));
                return;
            }

            foreach (var issue in issues)
            {
                _listView.Add(BuildValidationIssueRow(issue));
            }
        }

        private VisualElement BuildValidationIssueRow(DialogGraphValidationIssue issue)
        {
            var row = new VisualElement();
            row.AddToClassList("char-row");
            row.AddToClassList(issue.Severity switch
            {
                DialogGraphValidationSeverity.Error => "action-invalid-card",
                DialogGraphValidationSeverity.Warning => "action-used-card",
                _ => "action-registered-card"
            });
            row.name = $"{issue.Severity} {issue.Code} {issue.Message}";

            var severityLabel = new Label(issue.Severity.ToString().ToUpperInvariant());
            severityLabel.AddToClassList("action-sub-label");
            row.Add(severityLabel);

            var msgLabel = new Label(issue.Message);
            msgLabel.AddToClassList("action-card-desc");
            msgLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(msgLabel);

            if (!string.IsNullOrWhiteSpace(issue.Code))
            {
                var codeLabel = new Label(issue.Code);
                codeLabel.AddToClassList("action-invalid-id");
                row.Add(codeLabel);
            }

            if (string.Equals(issue.Code, "MISSING_LOCALE_KEY", StringComparison.Ordinal))
            {
                var fixBtn = new Button(FixMissingLocaleKeys) { text = "Fix Locale Keys" };
                fixBtn.AddToClassList("dlg-ai-btn");
                row.Add(fixBtn);
            }

            if (!string.IsNullOrWhiteSpace(issue.NodeGuid))
            {
                var guid = issue.NodeGuid;
                var jumpBtn = new Button(() => _owner.FocusNodeByGuid(guid)) { text = "Go to Node" };
                jumpBtn.AddToClassList("dlg-ai-btn");
                row.Add(jumpBtn);
            }

            return row;
        }

        private void FixMissingLocaleKeys()
        {
            var graph = _owner.LoadCurrentGraphAsset();
            if (graph == null)
            {
                Debug.LogWarning("[EditorSidebarWindow] Could not load the current graph asset for localization setup.");
                return;
            }

            DialogLocalizationSetupService.SetupGraph(graph);
            _owner.RefreshGraphEditorState();
            ShowValidationResult(DialogGraphValidationRunner.ValidateGraphAsset(graph));
        }

        #endregion

        #region ---------------- Characters Face ----------------

        private void BuildCharactersFace()
        {
            _characterRows.Clear();
            var characterRegistry = new CoreCharacterRegistryService();
            var registeredCharacters = characterRegistry.GetAllRegisteredDefinitions().ToList();
            var usedSpeakers = _owner.CollectSpeakersFromNodes()
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unknownSpeakers = usedSpeakers
                .Where(speaker => DialogGraphDefinitionResolver.FindCharacterBySpeaker(speaker) == null)
                .ToList();

            _summaryLabel.text =
                $"{registeredCharacters.Count} registered, {usedSpeakers.Count} in graph, {unknownSpeakers.Count} unknown.";

            if (_charactersSubTab == 0) // ── Registered ──────────────────
            {
                var actionsRow = new VisualElement();
                actionsRow.AddToClassList("action-top-bar");
                actionsRow.Add(CreateAiButton("Create Character", false, () =>
                {
                    DialogCreateAssetWindow.OpenForCharacter(asset =>
                    {
                        if (asset != null) RebuildFromGraph();
                    });
                }));
                _listView.Add(actionsRow);

                _listView.Add(CreateActionSectionHeader("Registered Characters", "registered"));
                if (registeredCharacters.Count == 0)
                {
                    _listView.Add(CreateEmptyState("No registered characters", "Create reusable character assets from here or from a node."));
                }
                else
                {
                    foreach (var character in registeredCharacters)
                    {
                        _listView.Add(BuildRegisteredCharacterRow(character));
                    }
                }
            }
            else // ── In Graph ────────────────────────────────────────────
            {
                _listView.Add(CreateActionSectionHeader("Used In Graph", "used"));
                if (usedSpeakers.Count == 0)
                {
                    _listView.Add(CreateEmptyState("No graph speakers yet", "Speakers appear here once you add dialog nodes."));
                }
                else
                {
                    foreach (var name in usedSpeakers)
                    {
                        var firstSprite = _owner.FindFirstSpriteForSpeaker(name);
                        var row = new CharacterRow(name, name, firstSprite);
                        _characterRows[name] = row;
                        _listView.Add(row);
                    }
                }

                _listView.Add(CreateActionSectionHeader("Unknown / Unregistered Speakers", "invalid"));
                if (unknownSpeakers.Count == 0)
                {
                    _listView.Add(CreateEmptyState("No unknown speakers", "All speakers match a registered character."));
                }
                else
                {
                    foreach (var speaker in unknownSpeakers)
                    {
                        _listView.Add(BuildUnknownSpeakerRow(speaker));
                    }
                }
            }
        }

        private class CharacterRow : VisualElement
        {
            public string OriginalName { get; }

            public string CurrentName => nameField.value?.Trim();

            public Sprite Sprite => (Sprite)spriteField.value;

            private readonly TextField nameField;
            private readonly ObjectField spriteField;
            private readonly Image preview;

            public CharacterRow(string originalName, string currentName, Sprite sprite)
            {
                OriginalName = originalName;
                AddToClassList("char-row");
                AddToClassList("action-used-card");

                var header = new VisualElement();
                header.AddToClassList("char-row-header");
                header.AddToClassList("char-row-header-line");

                nameField = new TextField("Name") { value = currentName };
                nameField.AddToClassList("char-name-field");
                header.Add(nameField);
                Add(header);

                var body = new VisualElement();
                body.AddToClassList("char-row-body");

                preview = new Image { scaleMode = ScaleMode.ScaleToFit };
                preview.AddToClassList("char-preview");
                body.Add(preview);

                var right = new VisualElement();
                right.AddToClassList("char-row-right");

                spriteField = new ObjectField("Portrait")
                {
                    objectType = typeof(Sprite),
                    allowSceneObjects = false,
                    value = sprite
                };

                spriteField.AddToClassList("char-sprite-field");
                spriteField.RegisterValueChangedCallback(_ => UpdatePreview(spriteField.value as Sprite));
                right.Add(spriteField);

                body.Add(right);
                Add(body);

                UpdatePreview(sprite);
            }

            private void UpdatePreview(Sprite sprite)
            {
                preview.image = sprite ? sprite.texture : null;
            }
        }

        private VisualElement BuildRegisteredCharacterRow(DialogCharacterSO character)
        {
            var row = new VisualElement();
            row.AddToClassList("char-row");
            row.AddToClassList("action-registered-card");
            row.name = $"{character.DisplayName} {character.CharacterID}";

            var title = new Label($"{character.DisplayName} ({character.CharacterID})");
            title.AddToClassList("action-card-title");
            row.Add(title);

            if (!string.IsNullOrWhiteSpace(character.ShortDescription))
            {
                var desc = new Label(character.ShortDescription);
                desc.AddToClassList("action-card-desc");
                row.Add(desc);
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("node-inline-actions");

            var useSelectedButton = CreateAiButton("Use On Selected Node", true, () =>
            {
                if (!_owner.TryAssignRegisteredCharacterToSelectedDialogNode(character, out var error))
                {
                    EditorUtility.DisplayDialog("Assign Character", error, "OK");
                }
                else
                {
                    RebuildFromGraph();
                }
            });

            var pingButton = CreateAiButton("Ping Asset", false, () => DialogGraphDefinitionResolver.PingAndSelect(character));
            buttons.Add(useSelectedButton);
            buttons.Add(pingButton);
            row.Add(buttons);

            return row;
        }

        private VisualElement BuildUnknownSpeakerRow(string speaker)
        {
            var row = new VisualElement();
            row.AddToClassList("char-row");
            row.AddToClassList("action-invalid-card");
            row.name = speaker;

            var speakerLabel = new Label(speaker);
            speakerLabel.name = $"unknown-speaker-{speaker}";
            speakerLabel.AddToClassList("action-invalid-id");
            row.Add(speakerLabel);

            var hint = new Label("Not registered as a character asset.");
            hint.AddToClassList("action-card-desc");
            row.Add(hint);

            var buttons = new VisualElement();
            buttons.AddToClassList("node-inline-actions");
            buttons.Add(CreateAiButton("Create Character Asset", false, () =>
            {
                var portrait = _owner.FindFirstSpriteForSpeaker(speaker);
                DialogCreateAssetWindow.OpenForCharacter(asset =>
                {
                    if (asset != null) RebuildFromGraph();
                }, prefillName: speaker);
            }));

            row.Add(buttons);
            return row;
        }

        #endregion

        #region ---------------- Actions Face ----------------

        private void BuildActionsFace()
        {
            _actionRows.Clear();
            var actionRegistry = new CoreActionRegistryService();
            var registeredActions = actionRegistry.GetAllRegisteredDefinitions().ToList();
            var nodes = _owner.CollectActionNodes()
                .OrderBy(v => string.IsNullOrEmpty(v.actionId) ? "~" : v.actionId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.GUID, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var invalidActionIds = _owner.CollectActionIdsFromNodes()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(actionId => DialogGraphDefinitionResolver.FindActionById(actionId) == null)
                .OrderBy(actionId => actionId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _summaryLabel.text =
                $"{registeredActions.Count} registered, {nodes.Count} in graph, {invalidActionIds.Count} invalid.";

            if (_actionsSubTab == 0) // ── Registered ──────────────────────
            {
                var actionsRow = new VisualElement();
                actionsRow.AddToClassList("action-top-bar");
                actionsRow.Add(CreateAiButton("Create Action", false, () =>
                {
                    DialogCreateAssetWindow.OpenForAction(asset =>
                    {
                        if (asset != null) RebuildFromGraph();
                    });
                }));
                actionsRow.Add(CreateAiButton("Register Missing From Graph", false, () =>
                {
                    var createdCount = _owner.RegisterMissingActionAssetsFromCurrentGraph(out var createdActionIds);
                    EditorUtility.DisplayDialog(
                        "Registered Actions",
                        createdCount > 0
                            ? $"Created {createdCount} action asset(s):\n{string.Join("\n", createdActionIds)}"
                            : "Every action ID in this graph is already registered.",
                        "OK");
                    RebuildFromGraph();
                }));
                _listView.Add(actionsRow);

                _listView.Add(CreateActionSectionHeader("Registered Actions", "registered"));
                if (registeredActions.Count == 0)
                {
                    _listView.Add(CreateEmptyState("No registered actions", "Create reusable action definitions from here or from an Action node."));
                }
                else
                {
                    foreach (var action in registeredActions)
                    {
                        _listView.Add(BuildRegisteredActionRow(action));
                    }
                }
            }
            else // ── In Graph ────────────────────────────────────────────
            {
                _listView.Add(CreateActionSectionHeader("Actions Used In Graph", "used"));
                if (nodes.Count == 0)
                {
                    _listView.Add(CreateEmptyState("No action nodes yet", "Add Action nodes to your graph to see them here."));
                }
                else
                {
                    var groups = nodes.GroupBy(v => v.actionId ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                    foreach (var group in groups)
                    {
                        var first = group.First();

                        var row = new ActionRow(
                            originalActionId: group.Key,
                            actionId: first.actionId ?? string.Empty,
                            payload: first.payloadJson ?? string.Empty,
                            wait: first.waitForCompletion,
                            waitSeconds: first.waitSeconds);

                        row.OnSelectAll = () =>
                        {
                            var graphView = _owner.GetGraphView();
                            if (graphView == null)
                            {
                                return;
                            }

                            var matching = nodes
                                .Where(v => string.Equals(v.actionId ?? string.Empty, group.Key, StringComparison.OrdinalIgnoreCase))
                                .Cast<GraphElement>();

                            graphView.ClearSelection();

                            var any = false;
                            foreach (var match in matching)
                            {
                                graphView.AddToSelection(match);
                                any = true;
                            }

                            if (any)
                            {
                                graphView.FrameSelection();
                            }
                        };

                        _actionRows[group.Key] = row;
                        _listView.Add(row);
                    }
                }

                _listView.Add(CreateActionSectionHeader("Invalid / Unregistered Action IDs", "invalid"));
                if (invalidActionIds.Count == 0)
                {
                    _listView.Add(CreateEmptyState("No invalid action IDs", "All action IDs in this graph are registered or the graph is still empty."));
                }
                else
                {
                    foreach (var actionId in invalidActionIds)
                    {
                        _listView.Add(BuildInvalidActionRow(actionId, nodes.FirstOrDefault(node =>
                            string.Equals(node.actionId ?? string.Empty, actionId, StringComparison.OrdinalIgnoreCase))));
                    }
                }
            }
        }

        private class ActionRow : VisualElement
        {
            public string OriginalActionId { get; }

            public string ActionId => idField.value?.Trim();

            public string Payload => payloadField.value ?? string.Empty;

            public bool WaitForCompletion => waitToggle.value;

            public float WaitSeconds => waitField.value;

            public Action OnSelectAll;

            private readonly TextField idField;
            private readonly TextField payloadField;
            private readonly TextField payloadInstructionField;
            private readonly Label payloadStatusLabel;
            private readonly Toggle waitToggle;
            private readonly FloatField waitField;

            public ActionRow(
                string originalActionId,
                string actionId,
                string payload,
                bool wait,
                float waitSeconds)
            {
                OriginalActionId = originalActionId ?? string.Empty;

                AddToClassList("char-row");
                AddToClassList("action-row");
                AddToClassList("action-used-card");

                // ── Header: Action ID field + Go to button ──────────────
                var header = new VisualElement();
                header.AddToClassList("char-row-header");
                header.AddToClassList("char-row-header-line");

                idField = new TextField("Action ID") { value = actionId ?? string.Empty };
                idField.AddToClassList("char-name-field");
                header.Add(idField);

                var selectBtn = new Button(() => OnSelectAll?.Invoke()) { text = "Go to" };
                selectBtn.AddToClassList("dlg-btn");
                selectBtn.AddToClassList("secondary");
                selectBtn.style.height = 22;
                selectBtn.style.minWidth = 44;
                selectBtn.style.fontSize = 10;
                selectBtn.style.marginLeft = 6;
                header.Add(selectBtn);

                Add(header);

                // ── Payload sub-card ────────────────────────────────────
                var payloadCard = new VisualElement();
                payloadCard.AddToClassList("action-payload-card");

                var payloadCap = new Label("PAYLOAD  ·  JSON");
                payloadCap.AddToClassList("action-sub-label");
                payloadCard.Add(payloadCap);

                payloadField = new TextField { multiline = true, value = payload ?? string.Empty };
                payloadField.style.minHeight = 64;
                payloadCard.Add(payloadField);

                payloadInstructionField = new TextField("Payload Request")
                {
                    multiline = false,
                    value = string.Empty,
                    isDelayed = true,
                    tooltip = "Optional natural-language request for payload generation, for example: turn on the light to 20%."
                };
                payloadCard.Add(payloadInstructionField);

                var payloadBtnRow = new VisualElement();
                payloadBtnRow.AddToClassList("node-inline-actions");
                payloadBtnRow.Add(CreateAiButton("Write Payload", false, OnClickWritePayload));
                payloadCard.Add(payloadBtnRow);

                payloadStatusLabel = new Label();
                payloadStatusLabel.AddToClassList("node-section-hint");
                payloadCard.Add(payloadStatusLabel);

                Add(payloadCard);

                // ── Flow control sub-card ───────────────────────────────
                var flowCard = new VisualElement();
                flowCard.AddToClassList("action-flow-card");

                var flowCap = new Label("FLOW CONTROL");
                flowCap.AddToClassList("action-sub-label");
                flowCard.Add(flowCap);

                waitToggle = new Toggle("Wait For Completion") { value = wait };
                flowCard.Add(waitToggle);

                waitField = new FloatField("Delay (sec)") { value = waitSeconds };
                flowCard.Add(waitField);

                Add(flowCard);
            }

            private void OnClickWritePayload()
            {
                var matchedAction = DialogGraphDefinitionResolver.FindActionById(ActionId);
                DialogActionPayloadUtility.BuildPayloadTemplate(
                    ActionId,
                    Payload,
                    matchedAction,
                    payloadInstructionField?.value,
                    out var updatedPayload,
                    out var statusMessage);

                payloadField.value = updatedPayload ?? "{}";
                payloadStatusLabel.text = statusMessage ?? string.Empty;
            }
        }

        private VisualElement BuildRegisteredActionRow(DialogActionSO action)
        {
            var row = new VisualElement();
            row.AddToClassList("char-row");
            row.AddToClassList("action-registered-card");
            row.name = $"{action.DisplayName} {action.ActionID} {action.Description}";

            var title = new Label($"{action.DisplayName} ({action.ActionID})");
            title.name = $"registered-action-{action.ActionID}";
            title.AddToClassList("action-card-title");
            row.Add(title);

            if (!string.IsNullOrWhiteSpace(action.Description))
            {
                var desc = new Label(action.Description);
                desc.AddToClassList("action-card-desc");
                row.Add(desc);
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("node-inline-actions");
            buttons.Add(CreateAiButton("Insert After Selected", true, () =>
            {
                if (!_owner.TryInsertRegisteredActionAfterSelectedNode(action, out var error))
                {
                    EditorUtility.DisplayDialog("Insert Action", error, "OK");
                }
                else
                {
                    RebuildFromGraph();
                }
            }));
            buttons.Add(CreateAiButton("Ping Asset", false, () => DialogGraphDefinitionResolver.PingAndSelect(action)));
            row.Add(buttons);

            return row;
        }

        private VisualElement BuildInvalidActionRow(string actionId, ActionNodeView sourceNode)
        {
            var row = new VisualElement();
            row.AddToClassList("char-row");
            row.AddToClassList("action-invalid-card");
            row.name = actionId;

            var idLabel = new Label(actionId);
            idLabel.name = $"invalid-action-{actionId}";
            idLabel.AddToClassList("action-invalid-id");
            row.Add(idLabel);

            var hint = new Label("Not registered — create an asset to link it.");
            hint.AddToClassList("action-card-desc");
            row.Add(hint);

            var buttons = new VisualElement();
            buttons.AddToClassList("node-inline-actions");
            buttons.Add(CreateAiButton("Create Registered Action", false, () =>
            {
                DialogCreateAssetWindow.OpenForAction(
                    asset => { if (asset != null) RebuildFromGraph(); },
                    prefillId:      actionId,
                    prefillPayload: sourceNode?.payloadJson ?? "{}");
            }));
            row.Add(buttons);

            return row;
        }

        #endregion

        #region ---------------- Context Face ----------------

        private void BuildContextFace()
        {
            var graph = _owner.LoadCurrentGraphAsset(createIfMissing: true);
            _summaryLabel.text = graph?.sceneContext != null
                ? $"Scene context assigned: {graph.sceneContext.name}"
                : "Scene context is optional. All fields below are saved on the graph asset.";

            if (graph == null)
            {
                _listView.Add(CreateEmptyState("No graph loaded", "Load or create a graph before editing scene context."));
                return;
            }

            var sceneSection = new VisualElement();
            sceneSection.AddToClassList("context-section-card");
            sceneSection.Add(CreateActionSectionHeader("Scene Context", "context"));

            _sceneContextField = new ObjectField("Context Asset")
            {
                objectType = typeof(DialogSceneContextSO),
                allowSceneObjects = false,
                value = graph.sceneContext
            };
            _sceneContextField.RegisterValueChangedCallback(evt =>
            {
                _owner.UpdateGraphContext("Assign Scene Context", asset =>
                {
                    asset.sceneContext = evt.newValue as DialogSceneContextSO;
                });
            });
            sceneSection.Add(_sceneContextField);

            sceneSection.Add(CreateAiButton("Create Context SO", false, () =>
            {
                DialogCreateAssetWindow.OpenForSceneContext(asset =>
                {
                    if (asset != null)
                    {
                        _sceneContextField.value = asset;
                        _owner.UpdateGraphContext("Assign Scene Context", a => a.sceneContext = asset);
                    }
                });
            }));

            var loadFromAssetButton = CreateAiButton("Load Values From Context Asset", false, () =>
            {
                if (_sceneContextField?.value is not DialogSceneContextSO contextAsset)
                {
                    EditorUtility.DisplayDialog("Scene Context", "Assign a DialogSceneContextSO first.", "OK");
                    return;
                }

                _owner.UpdateGraphContext("Load Scene Context Values", asset =>
                {
                    asset.sceneContext = contextAsset;
                    asset.environment = contextAsset.Environment;
                    asset.participatingCharacters = new List<DialogCharacterSO>(contextAsset.ParticipatingCharacters.Where(item => item != null));
                    asset.availableActions = new List<DialogActionSO>(contextAsset.AvailableActions.Where(item => item != null));
                    asset.sceneGoal = contextAsset.SceneGoal;
                    asset.tone = contextAsset.Tone;
                    asset.extraRules = contextAsset.ExtraRules;
                });

                RebuildFromGraph();
            });
            sceneSection.Add(loadFromAssetButton);
            _listView.Add(sceneSection);

            var detailsSection = new VisualElement();
            detailsSection.AddToClassList("context-section-card");
            detailsSection.Add(CreateActionSectionHeader("Context Details", "context"));

            _environmentField = new ObjectField("Environment")
            {
                objectType = typeof(DialogEnvironmentSO),
                allowSceneObjects = false,
                value = graph.environment
            };
            _environmentField.RegisterValueChangedCallback(evt =>
            {
                _owner.UpdateGraphContext("Assign Environment", asset => asset.environment = evt.newValue as DialogEnvironmentSO);
            });
            detailsSection.Add(_environmentField);

            detailsSection.Add(CreateAiButton("Create Environment", false, () =>
            {
                DialogCreateAssetWindow.OpenForEnvironment(asset =>
                {
                    if (asset != null)
                    {
                        _environmentField.value = asset;
                        _owner.UpdateGraphContext("Assign Environment", a => a.environment = asset);
                    }
                });
            }));

            _sceneGoalField = new TextField("Scene Goal")
            {
                multiline = true,
                value = graph.sceneGoal ?? string.Empty
            };
            _sceneGoalField.RegisterValueChangedCallback(evt =>
            {
                _owner.UpdateGraphContext("Edit Scene Goal", asset => asset.sceneGoal = evt.newValue ?? string.Empty);
            });
            detailsSection.Add(_sceneGoalField);

            _toneField = new TextField("Tone")
            {
                value = graph.tone ?? string.Empty
            };
            _toneField.RegisterValueChangedCallback(evt =>
            {
                _owner.UpdateGraphContext("Edit Scene Tone", asset => asset.tone = evt.newValue ?? string.Empty);
            });
            detailsSection.Add(_toneField);

            _extraRulesField = new TextField("Extra Rules")
            {
                multiline = true,
                value = graph.extraRules ?? string.Empty
            };
            _extraRulesField.RegisterValueChangedCallback(evt =>
            {
                _owner.UpdateGraphContext("Edit Extra Rules", asset => asset.extraRules = evt.newValue ?? string.Empty);
            });
            detailsSection.Add(_extraRulesField);
            _listView.Add(detailsSection);

            _listView.Add(BuildDefinitionListEditor(
                "Participating Characters",
                graph.participatingCharacters?.Where(item => item != null).Cast<UnityEngine.Object>().ToList() ?? new List<UnityEngine.Object>(),
                typeof(DialogCharacterSO),
                values => _owner.UpdateGraphContext("Edit Participating Characters", asset =>
                {
                    asset.participatingCharacters = values.Cast<DialogCharacterSO>().Where(item => item != null).ToList();
                })));

            var availableActionsValues =
                graph.availableActions?.Where(item => item != null).Cast<UnityEngine.Object>().ToList() ??
                new List<UnityEngine.Object>();

            var availableActionsSection = BuildDefinitionListEditor(
                "Available Actions",
                availableActionsValues,
                typeof(DialogActionSO),
                values => _owner.UpdateGraphContext("Edit Available Actions", asset =>
                {
                    asset.availableActions = values.Cast<DialogActionSO>().Where(item => item != null).ToList();
                }));

            availableActionsSection.Add(CreateAiButton("Autofill From Action Nodes", false, () =>
            {
                var assignedCount = _owner.AutofillAvailableActionsFromCurrentGraph(out var missingActionIds, out var assignedActionIds);
                var message = assignedCount > 0
                    ? $"Assigned {assignedCount} registered action(s) from the current graph:\n{string.Join("\n", assignedActionIds)}"
                    : "No registered action assets matched the action IDs used in the current graph.";

                if (missingActionIds.Count > 0)
                {
                    message += $"\n\nUnregistered action IDs:\n{string.Join("\n", missingActionIds)}";
                }

                EditorUtility.DisplayDialog("Available Actions", message, "OK");
                RebuildFromGraph();
            }));

            _listView.Add(availableActionsSection);
        }

        private VisualElement BuildDefinitionListEditor(
            string title,
            IList<UnityEngine.Object> values,
            Type objectType,
            Action<List<UnityEngine.Object>> onSave)
        {
            var section = new VisualElement();
            section.AddToClassList("context-section-card");
            section.Add(CreateActionSectionHeader(title, "context"));

            var listRoot = new VisualElement();
            listRoot.AddToClassList("char-row-right");
            section.Add(listRoot);

            void Render()
            {
                listRoot.Clear();

                if (values.Count == 0)
                {
                    listRoot.Add(new Label($"No {title.ToLowerInvariant()} assigned.") { name = $"empty-{title}" });
                }

                for (var i = 0; i < values.Count; i++)
                {
                    var index = i;
                    var row = new VisualElement();
                    row.AddToClassList("node-inline-actions");

                    var field = new ObjectField
                    {
                        objectType = objectType,
                        allowSceneObjects = false,
                        value = values[index]
                    };
                    field.style.flexGrow = 1;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        values[index] = evt.newValue as UnityEngine.Object;
                        onSave(values.ToList());
                    });
                    row.Add(field);

                    row.Add(CreateAiButton("Remove", false, () =>
                    {
                        values.RemoveAt(index);
                        onSave(values.ToList());
                        Render();
                    }));

                    listRoot.Add(row);
                }
            }

            section.Add(CreateAiButton($"Add {title.Substring(0, title.Length - 1)}", false, () =>
            {
                values.Add(null);
                onSave(values.ToList());
                Render();
            }));

            Render();
            return section;
        }

        #endregion

        private static Button CreateAiButton(string text, bool isPrimary, Action onClick)
        {
            var button = onClick != null ? new Button(onClick) : new Button();
            button.text = text;
            button.AddToClassList("dlg-ai-btn");

            if (isPrimary)
            {
                button.AddToClassList("primary");
            }

            return button;
        }

        #region ---------------- Tab / Filter / Helpers ----------------

        private void SetTab(Tab tab, bool refresh)
        {
            _currentTab = tab;

            // Show sub-tab bar only for Characters and Actions, but only the "In Graph" toggle
            var showSubTabs = tab == Tab.Characters || tab == Tab.Actions;
            if (_subTabBar != null)
            {
                _subTabBar.style.display = showSubTabs ? DisplayStyle.Flex : DisplayStyle.None;
                if (showSubTabs)
                {
                    // Always default to In Graph (index 1) since Registered is now in global tabs
                    if (tab == Tab.Actions)    _actionsSubTab    = 1;
                    else if (tab == Tab.Characters) _charactersSubTab = 1;
                    _subTabInGraph?.SetValueWithoutNotify(true);
                }
            }

            _tabCharacters?.SetValueWithoutNotify(tab == Tab.Characters);
            _tabActions?.SetValueWithoutNotify(tab == Tab.Actions);
            _tabValidate?.SetValueWithoutNotify(tab == Tab.Validate);

            _titleLabel.text = tab switch
            {
                Tab.Characters => "Characters",
                Tab.Actions => "Actions",
                _ => "Validate"
            };

            var hideSearch = false;
            var canApply   = tab == Tab.Characters || tab == Tab.Actions;

            if (_searchRow != null)
            {
                _searchRow.style.display = hideSearch
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            _applyBtn.SetEnabled(canApply);
            _applyBtn.tooltip = canApply
                ? "Apply edits to matching nodes"
                : "Validation results do not have editable changes to apply";

            if (refresh)
            {
                RebuildFromGraph();
            }
        }

        private static VisualElement CreateEmptyState(string title, string message)
        {
            var empty = new VisualElement();
            empty.AddToClassList("dlg-empty-state");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("dlg-empty-title");
            empty.Add(titleLabel);

            var messageLabel = new Label(message);
            messageLabel.AddToClassList("dlg-empty-message");
            empty.Add(messageLabel);

            return empty;
        }

        private static VisualElement CreateGroupHeader(string title)
        {
            var label = new Label(title);
            label.AddToClassList("dlg-section-header");
            return label;
        }

        /// <summary>
        /// Colored left-border section divider for the Actions tab.
        /// variant: "registered" | "used" | "invalid"
        /// </summary>
        private static VisualElement CreateActionSectionHeader(string title, string variant)
        {
            var container = new VisualElement();
            container.AddToClassList("action-section-divider");
            container.AddToClassList($"action-section-divider--{variant}");

            var label = new Label(title.ToUpperInvariant());
            label.AddToClassList("action-section-label");
            container.Add(label);

            return container;
        }

        private void OnClickRescan()
        {
            RebuildFromGraph();
        }

        private void OnClickApply()
        {
            if (_currentTab == Tab.Characters)
            {
                var data = ExportCharacterBindings();
                _owner.ApplySpritesToNodes(data);

                if (data.Any(binding => !string.Equals(
                        binding.originalName?.Trim(),
                        binding.currentName?.Trim(),
                        StringComparison.Ordinal)))
                {
                    RebuildFromGraph();
                }
            }
            else if (_currentTab == Tab.Actions)
            {
                var data = ExportActionBindings();
                _owner.ApplyActionsToNodes(data);

                if (data.Any(binding => !string.Equals(
                        binding.originalActionId ?? string.Empty,
                        binding.actionId ?? string.Empty,
                        StringComparison.Ordinal)))
                {
                    RebuildFromGraph();
                }
            }
        }

        private void FilterVisible()
        {
            var query = (_searchField?.value ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrEmpty(query);

            foreach (var child in _listView.Children())
            {
                if (!hasQuery)
                {
                    child.style.display = DisplayStyle.Flex;
                    continue;
                }

                var haystack = child is CharacterRow characterRow
                    ? characterRow.CurrentName ?? string.Empty
                    : child is ActionRow actionRow
                        ? (actionRow.ActionId ?? string.Empty) + "\n" + (actionRow.Payload ?? string.Empty)
                        : child.name ?? string.Empty;

                child.style.display = haystack.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        private List<DialogGraphEditorWindow.CharacterBinding> ExportCharacterBindings()
        {
            var list = new List<DialogGraphEditorWindow.CharacterBinding>(_characterRows.Count);

            foreach (var row in _characterRows.Values)
            {
                list.Add(new DialogGraphEditorWindow.CharacterBinding
                {
                    originalName = row.OriginalName,
                    currentName  = row.CurrentName,
                    sprite       = row.Sprite
                });
            }

            return list;
        }

        private List<DialogGraphEditorWindow.ActionBinding> ExportActionBindings()
        {
            var list = new List<DialogGraphEditorWindow.ActionBinding>(_actionRows.Count);

            foreach (var row in _actionRows.Values)
            {
                list.Add(new DialogGraphEditorWindow.ActionBinding
                {
                    originalActionId  = row.OriginalActionId,
                    actionId          = row.ActionId,
                    payloadJson       = row.Payload,
                    waitForCompletion = row.WaitForCompletion,
                    waitSeconds       = row.WaitSeconds
                });
            }

            return list;
        }
        #endregion
    }
}
