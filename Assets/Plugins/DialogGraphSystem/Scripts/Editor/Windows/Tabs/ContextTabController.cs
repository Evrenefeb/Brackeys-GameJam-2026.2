using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.AI;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows.Tabs
{
    /// <summary>
    /// Global tab for managing reusable environment and scene context assets.
    /// </summary>
    public class ContextTabController
    {
        private enum ContextAssetMode
        {
            Environments,
            SceneContexts
        }

        private const float ListPanelWidth = 260f;
        private const float InlinePickerWidth = 460f;
        private const float InlinePickerMinWidth = 240f;

        private VisualElement _root;
        private ScrollView _listScroll;
        private VisualElement _inspectorPanel;
        private ToolbarSearchField _searchField;
        private Button _environmentsButton;
        private Button _sceneContextsButton;
        private string _searchQuery = string.Empty;
        private ContextAssetMode _mode = ContextAssetMode.SceneContexts;
        private UnityEngine.Object _selected;

        public void BuildUI(VisualElement root)
        {
            _root = root;
            _root.Clear();

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.SETTINGS_STYLE_PATH);
            if (ss != null && !_root.styleSheets.Contains(ss))
            {
                _root.styleSheets.Add(ss);
            }

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.overflow = Overflow.Hidden;
            _root.Add(body);

            body.Add(BuildListPanel());
            body.Add(BuildDivider());

            _inspectorPanel = new VisualElement();
            _inspectorPanel.style.flexGrow = 1;
            _inspectorPanel.style.flexShrink = 1;
            _inspectorPanel.style.overflow = Overflow.Hidden;
            body.Add(_inspectorPanel);

            RefreshList();
            ShowInspector(_selected);
        }

        private VisualElement BuildListPanel()
        {
            var panel = new VisualElement();
            panel.style.width = ListPanelWidth;
            panel.style.minWidth = 180;
            panel.style.flexShrink = 0;
            panel.style.flexGrow = 0;
            panel.style.flexDirection = FlexDirection.Column;

            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems = Align.Center;
            topBar.style.paddingLeft = 8;
            topBar.style.paddingRight = 8;
            topBar.style.paddingTop = 8;
            topBar.style.paddingBottom = 6;
            topBar.style.flexShrink = 0;
            panel.Add(topBar);

            _searchField = new ToolbarSearchField();
            _searchField.style.flexGrow = 1;
            _searchField.style.flexShrink = 1;
            _searchField.style.minWidth = 0;
            _searchField.SetValueWithoutNotify(_searchQuery);
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue ?? string.Empty;
                RefreshList();
            });
            topBar.Add(_searchField);

            var createBtn = new Button(OnClickCreate)
            {
                text = "+",
                tooltip = "Create a new context asset"
            };
            createBtn.AddToClassList("dlg-btn");
            createBtn.AddToClassList("success");
            createBtn.style.minWidth = 26;
            createBtn.style.minHeight = 26;
            createBtn.style.marginLeft = 4;
            topBar.Add(createBtn);

            var modeBar = new VisualElement();
            modeBar.style.flexDirection = FlexDirection.Row;
            modeBar.style.paddingLeft = 8;
            modeBar.style.paddingRight = 8;
            modeBar.style.paddingBottom = 6;
            panel.Add(modeBar);

            _sceneContextsButton = BuildModeButton("Scene Contexts", ContextAssetMode.SceneContexts);
            _sceneContextsButton.style.marginRight = 3;
            modeBar.Add(_sceneContextsButton);

            _environmentsButton = BuildModeButton("Environments", ContextAssetMode.Environments);
            _environmentsButton.style.marginLeft = 3;
            modeBar.Add(_environmentsButton);
            RefreshModeButtons();

            _listScroll = new ScrollView(ScrollViewMode.Vertical);
            _listScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _listScroll.style.flexGrow = 1;
            panel.Add(_listScroll);

            return panel;
        }

        private void SetMode(ContextAssetMode mode)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            _selected = null;
            RefreshModeButtons();
            RefreshList();
            ShowInspector(null);
        }

        private Button BuildModeButton(string label, ContextAssetMode mode)
        {
            var button = new Button(() => SetMode(mode)) { text = label };
            button.AddToClassList("dgs-context-mode-button");
            button.userData = mode;
            return button;
        }

        private void RefreshModeButtons()
        {
            _sceneContextsButton?.EnableInClassList("active", _mode == ContextAssetMode.SceneContexts);
            _environmentsButton?.EnableInClassList("active", _mode == ContextAssetMode.Environments);
        }

        private void RefreshList()
        {
            if (_listScroll == null)
            {
                return;
            }

            _listScroll.Clear();

            if (_mode == ContextAssetMode.Environments)
            {
                BuildEnvironmentList();
                return;
            }

            BuildSceneContextList();
        }

        private void BuildEnvironmentList()
        {
            var registry = new DialogEnvironmentRegistryService();
            var environments = registry.GetAllRegisteredDefinitions()
                .Where(environment => MatchesEnvironment(environment))
                .OrderBy(environment => string.IsNullOrWhiteSpace(environment.DisplayName) ? environment.name : environment.DisplayName)
                .ToList();

            if (environments.Count == 0)
            {
                _listScroll.Add(BuildEmptyLabel(
                    string.IsNullOrEmpty(_searchQuery)
                        ? "No environments yet.\nPress + to create one."
                        : "No environments match the search."));
                return;
            }

            foreach (var environment in environments)
            {
                var row = BuildListRow(
                    ResolveEnvironmentLabel(environment),
                    environment.EnvironmentID,
                    _selected == environment,
                    () => SelectAsset(environment));
                _listScroll.Add(row);
            }
        }

        private void BuildSceneContextList()
        {
            var registry = new DialogSceneContextRegistryService();
            var sceneContexts = registry.GetAllRegisteredDefinitions()
                .Where(sceneContext => MatchesSceneContext(sceneContext))
                .OrderBy(sceneContext => sceneContext.name)
                .ToList();

            if (sceneContexts.Count == 0)
            {
                _listScroll.Add(BuildEmptyLabel(
                    string.IsNullOrEmpty(_searchQuery)
                        ? "No scene contexts yet.\nPress + to create one."
                        : "No scene contexts match the search."));
                return;
            }

            foreach (var sceneContext in sceneContexts)
            {
                var environmentName = sceneContext.Environment != null
                    ? sceneContext.Environment.DisplayName
                    : "No environment";
                var subtitle = $"{environmentName}  |  {sceneContext.ParticipatingCharacters.Count} chars  |  {sceneContext.AvailableActions.Count} actions";
                var row = BuildListRow(sceneContext.name, subtitle, _selected == sceneContext, () => SelectAsset(sceneContext));
                _listScroll.Add(row);
            }
        }

        private static Button BuildListRow(string title, string subtitle, bool isSelected, Action onClick)
        {
            var row = new Button(onClick);
            row.AddToClassList("dgs-nav-button");
            row.AddToClassList("dgs-asset-list-row");
            if (isSelected)
            {
                row.AddToClassList("active");
            }

            row.style.height = StyleKeyword.Auto;
            row.style.minHeight = 40;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.unityTextAlign = TextAnchor.MiddleLeft;

            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Column;
            content.style.alignItems = Align.FlexStart;
            content.style.flexGrow = 1;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.flexGrow = 1;
            content.Add(titleLabel);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var subtitleLabel = new Label(subtitle);
                subtitleLabel.AddToClassList("dgs-muted");
                subtitleLabel.style.fontSize = 10;
                content.Add(subtitleLabel);
            }

            row.Add(content);
            return row;
        }

        private void SelectAsset(UnityEngine.Object asset)
        {
            _selected = asset;
            RefreshList();
            ShowInspector(asset);
        }

        private void ShowInspector(UnityEngine.Object asset)
        {
            if (_inspectorPanel == null)
            {
                return;
            }

            _inspectorPanel.Clear();

            if (asset == null)
            {
                var placeholder = new Label("Select an environment or scene context to inspect it.");
                placeholder.AddToClassList("dgs-page-subtitle");
                placeholder.style.paddingTop = 24;
                placeholder.style.paddingBottom = 24;
                placeholder.style.paddingLeft = 24;
                placeholder.style.paddingRight = 24;
                placeholder.style.whiteSpace = WhiteSpace.Normal;
                _inspectorPanel.Add(placeholder);
                return;
            }

            var header = BuildInspectorHeader(asset);
            _inspectorPanel.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1;
            _inspectorPanel.Add(scroll);

            var card = new VisualElement();
            card.AddToClassList("dgs-card");
            card.style.marginTop = 12;
            card.style.marginBottom = 12;
            card.style.marginLeft = 12;
            card.style.marginRight = 12;
            scroll.Add(card);

            if (asset is DialogEnvironmentSO environment)
            {
                BuildEnvironmentInspector(card, header.Q<Label>(), environment);
            }
            else if (asset is DialogSceneContextSO sceneContext)
            {
                BuildSceneContextInspector(card, header.Q<Label>(), sceneContext);
            }
        }

        private VisualElement BuildInspectorHeader(UnityEngine.Object asset)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 12;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.16f, 0.18f, 0.27f);
            header.style.flexShrink = 0;

            var titleLabel = new Label(ResolveSelectionTitle(asset));
            titleLabel.AddToClassList("dgs-page-title");
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);

            var pingBtn = new Button(() => DialogGraphDefinitionResolver.PingAndSelect(asset)) { text = "Ping" };
            pingBtn.AddToClassList("dlg-btn");
            pingBtn.AddToClassList("secondary");
            pingBtn.style.fontSize = 10;
            pingBtn.style.minHeight = 22;
            pingBtn.style.marginRight = 4;
            header.Add(pingBtn);

            var deleteBtn = new Button(() => OnClickDelete(asset)) { text = "Delete" };
            deleteBtn.AddToClassList("dlg-btn");
            deleteBtn.AddToClassList("danger");
            deleteBtn.style.fontSize = 10;
            deleteBtn.style.minHeight = 22;
            header.Add(deleteBtn);

            return header;
        }

        private void BuildEnvironmentInspector(VisualElement card, Label headerTitle, DialogEnvironmentSO environment)
        {
            card.Add(BuildRenameField(environment, headerTitle));

            var displayNameField = new TextField("Display Name") { value = environment.DisplayName };
            displayNameField.style.marginBottom = 6;
            displayNameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(environment, "Edit Environment Name");
                environment.DisplayName = evt.newValue ?? string.Empty;
                SaveAsset(environment);
                headerTitle.text = ResolveSelectionTitle(environment);
                RefreshList();
            });
            card.Add(displayNameField);

            var idField = new TextField("Environment ID") { value = environment.EnvironmentID };
            idField.style.marginBottom = 6;
            idField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(environment, "Edit Environment ID");
                environment.EnvironmentID = evt.newValue ?? string.Empty;
                SaveAsset(environment);
                RefreshList();
            });
            card.Add(idField);

            var descriptionField = new TextField("Description")
            {
                value = environment.Description ?? string.Empty,
                multiline = true
            };
            descriptionField.style.marginBottom = 6;
            descriptionField.style.minHeight = 72;
            void ApplyDescription(string value)
            {
                Undo.RecordObject(environment, "Edit Environment Description");
                environment.Description = value ?? string.Empty;
                SaveAsset(environment);
            }
            descriptionField.RegisterValueChangedCallback(evt => ApplyDescription(evt.newValue));
            card.Add(BuildAiRewriteField(
                descriptionField,
                "Environment Description",
                "Rewrite this environment description for clarity and stronger worldbuilding while preserving the same setting facts.",
                environment.DefaultTone,
                () => BuildEnvironmentRewriteContext(environment),
                ApplyDescription));

            var atmosphereField = new TextField("Atmosphere") { value = environment.Atmosphere ?? string.Empty };
            atmosphereField.style.marginBottom = 6;
            atmosphereField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(environment, "Edit Environment Atmosphere");
                environment.Atmosphere = evt.newValue ?? string.Empty;
                SaveAsset(environment);
            });
            card.Add(atmosphereField);

            var canonRulesField = new TextField("Canon Rules")
            {
                value = environment.CanonRules ?? string.Empty,
                multiline = true
            };
            canonRulesField.style.marginBottom = 6;
            canonRulesField.style.minHeight = 72;
            void ApplyCanonRules(string value)
            {
                Undo.RecordObject(environment, "Edit Environment Canon Rules");
                environment.CanonRules = value ?? string.Empty;
                SaveAsset(environment);
            }
            canonRulesField.RegisterValueChangedCallback(evt => ApplyCanonRules(evt.newValue));
            card.Add(BuildAiRewriteField(
                canonRulesField,
                "Environment Canon Rules",
                "Rewrite these canon rules to be clearer and more actionable without changing their meaning.",
                environment.DefaultTone,
                () => BuildEnvironmentRewriteContext(environment),
                ApplyCanonRules));

            var defaultToneField = new TextField("Default Tone") { value = environment.DefaultTone ?? string.Empty };
            defaultToneField.style.marginBottom = 6;
            defaultToneField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(environment, "Edit Environment Tone");
                environment.DefaultTone = evt.newValue ?? string.Empty;
                SaveAsset(environment);
            });
            card.Add(defaultToneField);
        }

        private void BuildSceneContextInspector(VisualElement card, Label headerTitle, DialogSceneContextSO sceneContext)
        {
            card.Add(BuildRenameField(sceneContext, headerTitle));

            card.Add(BuildEnvironmentPicker(sceneContext));

            var goalField = new TextField("Scene Goal")
            {
                value = sceneContext.SceneGoal ?? string.Empty,
                multiline = true
            };
            goalField.style.marginBottom = 6;
            goalField.style.minHeight = 72;
            void ApplySceneGoal(string value)
            {
                Undo.RecordObject(sceneContext, "Edit Scene Goal");
                sceneContext.SceneGoal = value ?? string.Empty;
                SaveAsset(sceneContext);
            }
            goalField.RegisterValueChangedCallback(evt => ApplySceneGoal(evt.newValue));
            card.Add(BuildAiRewriteField(
                goalField,
                "Scene Goal",
                "Rewrite this scene goal so it is clearer and stronger as an authoring prompt, while preserving intent and constraints.",
                sceneContext.Tone,
                () => BuildSceneContextRewriteContext(sceneContext),
                ApplySceneGoal));

            var toneField = new TextField("Tone") { value = sceneContext.Tone ?? string.Empty };
            toneField.style.marginBottom = 6;
            toneField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(sceneContext, "Edit Scene Tone");
                sceneContext.Tone = evt.newValue ?? string.Empty;
                SaveAsset(sceneContext);
            });
            card.Add(toneField);

            var extraRulesField = new TextField("Extra Rules")
            {
                value = sceneContext.ExtraRules ?? string.Empty,
                multiline = true
            };
            extraRulesField.style.marginBottom = 6;
            extraRulesField.style.minHeight = 72;
            void ApplyExtraRules(string value)
            {
                Undo.RecordObject(sceneContext, "Edit Extra Rules");
                sceneContext.ExtraRules = value ?? string.Empty;
                SaveAsset(sceneContext);
            }
            extraRulesField.RegisterValueChangedCallback(evt => ApplyExtraRules(evt.newValue));
            card.Add(BuildAiRewriteField(
                extraRulesField,
                "Extra Rules",
                "Rewrite these extra scene rules so they are concise, clear, and useful for dialogue generation without changing the actual constraints.",
                sceneContext.Tone,
                () => BuildSceneContextRewriteContext(sceneContext),
                ApplyExtraRules));

            card.Add(BuildCharacterPicker(sceneContext));
            card.Add(BuildActionPicker(sceneContext));
        }

        private VisualElement BuildEnvironmentPicker(DialogSceneContextSO sceneContext)
        {
            var section = new VisualElement();
            section.style.marginBottom = 8;

            var choices = new List<DialogEnvironmentSO> { null };
            choices.AddRange(new DialogEnvironmentRegistryService()
                .GetAllRegisteredDefinitions()
                .Where(environment => environment != null)
                .OrderBy(environment => ResolveEnvironmentLabel(environment), StringComparer.OrdinalIgnoreCase));

            if (sceneContext.Environment != null && !choices.Contains(sceneContext.Environment))
            {
                choices.Add(sceneContext.Environment);
            }

            var selectedIndex = Mathf.Max(0, choices.IndexOf(sceneContext.Environment));
            var dropdown = new PopupField<DialogEnvironmentSO>(
                "Environment",
                choices,
                selectedIndex,
                FormatEnvironmentChoice,
                FormatEnvironmentChoice);
            dropdown.style.marginBottom = 6;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(sceneContext, "Edit Scene Context Environment");
                sceneContext.Environment = evt.newValue;
                SaveAsset(sceneContext);
                RefreshList();
                ShowInspector(sceneContext);
            });
            section.Add(dropdown);

            return section;
        }

        private VisualElement BuildCharacterPicker(DialogSceneContextSO sceneContext)
        {
            var section = new VisualElement();
            section.style.marginBottom = 8;

            var title = new Label("Participating Characters");
            title.AddToClassList("dgs-muted");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            section.Add(title);

            foreach (var character in sceneContext.ParticipatingCharacters.Where(character => character != null).ToList())
            {
                section.Add(BuildAssetListRow(
                    ResolveCharacterChoice(character),
                    () =>
                    {
                        Undo.RecordObject(sceneContext, "Remove Scene Context Character");
                        sceneContext.ParticipatingCharacters.Remove(character);
                        SaveAsset(sceneContext);
                        ShowInspector(sceneContext);
                    }));
            }

            var availableCharacters = new DialogCharacterRegistryService()
                .GetAllRegisteredDefinitions()
                .Where(character => character != null && !sceneContext.ParticipatingCharacters.Contains(character))
                .OrderBy(character => ResolveCharacterChoice(character), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (availableCharacters.Count == 0)
            {
                section.Add(BuildHintLabel("All registered characters are already included."));
                return section;
            }

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            addRow.style.alignItems = Align.Center;
            addRow.style.flexWrap = Wrap.Wrap;
            addRow.style.marginBottom = 2;

            var dropdown = new PopupField<DialogCharacterSO>(
                "Add Character",
                availableCharacters,
                0,
                ResolveCharacterChoice,
                ResolveCharacterChoice);
            ConfigureInlinePicker(dropdown);
            addRow.Add(dropdown);

            var addButton = new Button(() =>
            {
                var selectedCharacter = dropdown.value;
                if (selectedCharacter == null)
                {
                    return;
                }

                Undo.RecordObject(sceneContext, "Add Scene Context Character");
                sceneContext.ParticipatingCharacters.Add(selectedCharacter);
                SaveAsset(sceneContext);
                ShowInspector(sceneContext);
            })
            {
                text = "Add"
            };
            addButton.AddToClassList("dlg-btn");
            addButton.AddToClassList("secondary");
            addButton.style.marginLeft = 4;
            addButton.style.marginTop = 2;
            addButton.style.marginBottom = 2;
            addButton.style.flexShrink = 0;
            addRow.Add(addButton);

            section.Add(addRow);
            return section;
        }

        private VisualElement BuildActionPicker(DialogSceneContextSO sceneContext)
        {
            var section = new VisualElement();
            section.style.marginBottom = 8;

            var title = new Label("Available Actions");
            title.AddToClassList("dgs-muted");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            section.Add(title);

            foreach (var action in sceneContext.AvailableActions.Where(action => action != null).ToList())
            {
                section.Add(BuildAssetListRow(
                    ResolveActionChoice(action),
                    () =>
                    {
                        Undo.RecordObject(sceneContext, "Remove Scene Context Action");
                        sceneContext.AvailableActions.Remove(action);
                        SaveAsset(sceneContext);
                        ShowInspector(sceneContext);
                    }));
            }

            var availableActions = new DialogActionRegistryService()
                .GetAllRegisteredDefinitions()
                .Where(action => action != null && !sceneContext.AvailableActions.Contains(action))
                .OrderBy(action => ResolveActionChoice(action), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (availableActions.Count == 0)
            {
                section.Add(BuildHintLabel("All registered actions are already included."));
                return section;
            }

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            addRow.style.alignItems = Align.Center;
            addRow.style.flexWrap = Wrap.Wrap;
            addRow.style.marginBottom = 2;

            var dropdown = new PopupField<DialogActionSO>(
                "Add Action",
                availableActions,
                0,
                ResolveActionChoice,
                ResolveActionChoice);
            ConfigureInlinePicker(dropdown);
            addRow.Add(dropdown);

            var addButton = new Button(() =>
            {
                var selectedAction = dropdown.value;
                if (selectedAction == null)
                {
                    return;
                }

                Undo.RecordObject(sceneContext, "Add Scene Context Action");
                sceneContext.AvailableActions.Add(selectedAction);
                SaveAsset(sceneContext);
                ShowInspector(sceneContext);
            })
            {
                text = "Add"
            };
            addButton.AddToClassList("dlg-btn");
            addButton.AddToClassList("secondary");
            addButton.style.marginLeft = 4;
            addButton.style.marginTop = 2;
            addButton.style.marginBottom = 2;
            addButton.style.flexShrink = 0;
            addRow.Add(addButton);

            section.Add(addRow);
            return section;
        }

        private static VisualElement BuildAssetListRow(string label, Action onRemove)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var textLabel = new Label(label);
            textLabel.style.flexGrow = 1;
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(textLabel);

            var removeButton = new Button(onRemove) { text = "Remove" };
            removeButton.AddToClassList("dlg-btn");
            removeButton.AddToClassList("danger");
            removeButton.style.fontSize = 10;
            removeButton.style.minHeight = 22;
            removeButton.style.marginLeft = 4;
            row.Add(removeButton);

            return row;
        }

        private static void ConfigureInlinePicker<T>(PopupField<T> dropdown)
        {
            dropdown.style.width = InlinePickerWidth;
            dropdown.style.maxWidth = InlinePickerWidth;
            dropdown.style.minWidth = InlinePickerMinWidth;
            dropdown.style.flexGrow = 0;
            dropdown.style.flexShrink = 1;
            dropdown.style.marginTop = 2;
            dropdown.style.marginBottom = 2;
        }

        private static Label BuildHintLabel(string message)
        {
            var label = new Label(message);
            label.AddToClassList("dgs-muted");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4;
            return label;
        }

        private static string FormatEnvironmentChoice(DialogEnvironmentSO environment)
        {
            if (environment == null)
            {
                return "None";
            }

            return $"{ResolveEnvironmentLabel(environment)} ({environment.EnvironmentID})";
        }

        private static string ResolveCharacterChoice(DialogCharacterSO character)
        {
            if (character == null)
            {
                return "None";
            }

            return $"{character.DisplayName} ({character.CharacterID})";
        }

        private static string ResolveActionChoice(DialogActionSO action)
        {
            if (action == null)
            {
                return "None";
            }

            return $"{action.DisplayName} ({action.ActionID})";
        }

        private VisualElement BuildRenameField(UnityEngine.Object asset, Label headerTitle)
        {
            var container = new VisualElement();
            container.style.marginBottom = 6;

            var nameField = new TextField("Asset Name") { value = asset.name };
            nameField.RegisterValueChangedCallback(evt =>
            {
                var newName = (evt.newValue ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(newName) || string.Equals(asset.name, newName, StringComparison.Ordinal))
                {
                    return;
                }

                var path = AssetDatabase.GetAssetPath(asset);
                var error = AssetDatabase.RenameAsset(path, newName);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning($"[ContextTabController] Failed to rename asset: {error}");
                    nameField.SetValueWithoutNotify(asset.name);
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                headerTitle.text = ResolveSelectionTitle(asset);
                RefreshList();
            });
            container.Add(nameField);
            return container;
        }

        private void OnClickCreate()
        {
            if (_mode == ContextAssetMode.Environments)
            {
                CreateEnvironment();
                return;
            }

            CreateSceneContext();
        }

        /// <summary>
        /// Creates an environment asset and immediately selects it in this tab.
        /// Switches to Environments mode if needed. Used by both the tab create button and the AI flow.
        /// </summary>
        public DialogEnvironmentSO CreateAndSelectEnvironment(
            string displayName,
            string description = null,
            string atmosphere = null,
            string tone = null)
        {
            var registry = new DialogEnvironmentRegistryService();
            var asset = registry.CreateNewAsset(displayName);
            if (asset == null)
            {
                return null;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Environment");
            var undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCreatedObjectUndo(asset, "Create Environment");
            Undo.RecordObject(asset, "Initialize Environment");

            DialogAssetInitializer.InitializeEnvironment(asset, displayName, description, atmosphere, tone);
            DialogAssetInitializer.FinalizeAsset(asset, "Create Environment", pingAsset: false);

            Undo.CollapseUndoOperations(undoGroup);

            _mode = ContextAssetMode.Environments;
            SelectCreatedAsset(asset);
            return asset;
        }

        /// <summary>
        /// Creates a scene context asset and immediately selects it in this tab.
        /// Switches to SceneContexts mode if needed. Used by both the tab create button and the AI flow.
        /// </summary>
        public DialogSceneContextSO CreateAndSelectSceneContext(
            string assetName,
            DialogEnvironmentSO environment = null,
            string sceneGoal = null,
            string tone = null,
            string extraRules = null)
        {
            var registry = new DialogSceneContextRegistryService();
            var asset = registry.CreateNewAsset(assetName);
            if (asset == null)
            {
                return null;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Scene Context");
            var undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCreatedObjectUndo(asset, "Create Scene Context");
            Undo.RecordObject(asset, "Initialize Scene Context");

            DialogAssetInitializer.InitializeSceneContext(asset, assetName, environment, sceneGoal, tone, extraRules);
            DialogAssetInitializer.FinalizeAsset(asset, "Create Scene Context", pingAsset: false);

            Undo.CollapseUndoOperations(undoGroup);

            _mode = ContextAssetMode.SceneContexts;
            SelectCreatedAsset(asset);
            return asset;
        }

        private void CreateEnvironment()
        {
            CreateAndSelectEnvironment("New Environment");
        }

        private void CreateSceneContext()
        {
            CreateAndSelectSceneContext("New Context");
        }

        private void SelectCreatedAsset(UnityEngine.Object asset)
        {
            _searchQuery = string.Empty;
            _searchField?.SetValueWithoutNotify(_searchQuery);
            _selected = asset;
            RefreshList();
            ShowInspector(asset);
            DialogGraphDefinitionResolver.PingAndSelect(asset);
        }

        private void OnClickDelete(UnityEngine.Object asset)
        {
            var label = ResolveSelectionTitle(asset);
            if (!EditorUtility.DisplayDialog(
                    "Delete Context Asset",
                    $"Are you sure you want to delete '{label}'?\nThis cannot be undone.",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                if (Selection.activeObject == asset)
                {
                    Selection.activeObject = null;
                }

                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (_selected == asset)
            {
                _selected = null;
            }

            RefreshList();
            ShowInspector(null);
        }

        private bool MatchesEnvironment(DialogEnvironmentSO environment)
        {
            if (environment == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                return true;
            }

            return ContainsIgnoreCase(environment.DisplayName, _searchQuery)
                   || ContainsIgnoreCase(environment.EnvironmentID, _searchQuery)
                   || ContainsIgnoreCase(environment.Description, _searchQuery)
                   || ContainsIgnoreCase(environment.name, _searchQuery);
        }

        private bool MatchesSceneContext(DialogSceneContextSO sceneContext)
        {
            if (sceneContext == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                return true;
            }

            return ContainsIgnoreCase(sceneContext.name, _searchQuery)
                   || ContainsIgnoreCase(sceneContext.SceneGoal, _searchQuery)
                   || ContainsIgnoreCase(sceneContext.Tone, _searchQuery)
                   || ContainsIgnoreCase(sceneContext.Environment?.DisplayName, _searchQuery);
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value)
                   && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveEnvironmentLabel(DialogEnvironmentSO environment)
        {
            return string.IsNullOrWhiteSpace(environment.DisplayName)
                ? environment.name
                : environment.DisplayName;
        }

        private static string ResolveSelectionTitle(UnityEngine.Object asset)
        {
            return asset switch
            {
                DialogEnvironmentSO environment => ResolveEnvironmentLabel(environment),
                DialogSceneContextSO sceneContext => sceneContext.name,
                _ => asset != null ? asset.name : string.Empty
            };
        }

        private static Label BuildEmptyLabel(string message)
        {
            var label = new Label(message);
            label.AddToClassList("dgs-page-subtitle");
            label.style.paddingTop = 12;
            label.style.paddingBottom = 12;
            label.style.paddingLeft = 12;
            label.style.paddingRight = 12;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static void SaveAsset(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static VisualElement BuildDivider()
        {
            var divider = new VisualElement();
            divider.style.width = 1;
            divider.style.flexShrink = 0;
            divider.style.backgroundColor = new Color(0.16f, 0.18f, 0.27f);
            return divider;
        }

        private VisualElement BuildAiRewriteField(
            TextField field,
            string fieldTitle,
            string customPrompt,
            string desiredTone,
            Func<DialogGraphAiContext> contextFactory,
            Action<string> applyValue)
        {
            var container = new VisualElement();
            container.style.marginBottom = 6;

            field.style.marginBottom = 4;
            container.Add(field);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;

            var rewriteButton = new Button(() =>
                RewriteFieldWithAi(field, fieldTitle, customPrompt, desiredTone, contextFactory, applyValue))
            {
                text = "AI Rewrite"
            };
            rewriteButton.AddToClassList("dlg-btn");
            rewriteButton.AddToClassList("secondary");
            rewriteButton.style.minHeight = 22;
            rewriteButton.style.fontSize = 10;

            var bridge = DialogGraphAiBridgeLocator.Current;
            var reason = string.Empty;
            if (bridge == null || !bridge.IsAvailable || !bridge.CanRewriteText(out reason))
            {
                rewriteButton.SetEnabled(false);
                rewriteButton.tooltip = string.IsNullOrWhiteSpace(reason)
                    ? "AI rewrite is not available."
                    : reason;
            }
            else
            {
                rewriteButton.tooltip = $"Rewrite {fieldTitle} with AI and review before applying.";
            }

            buttonRow.Add(rewriteButton);
            container.Add(buttonRow);
            return container;
        }

        private void RewriteFieldWithAi(
            TextField field,
            string fieldTitle,
            string customPrompt,
            string desiredTone,
            Func<DialogGraphAiContext> contextFactory,
            Action<string> applyValue)
        {
            var bridge = DialogGraphAiBridgeLocator.Current;
            if (bridge == null || !bridge.IsAvailable)
            {
                EditorUtility.DisplayDialog("AI Rewrite Failed", "AI rewrite is not available in this version.", "OK");
                return;
            }

            if (!bridge.CanRewriteText(out var reason))
            {
                EditorUtility.DisplayDialog("AI Rewrite Failed", reason, "OK");
                return;
            }

            var originalText = field?.value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(originalText))
            {
                EditorUtility.DisplayDialog("AI Rewrite Failed", $"{fieldTitle} is empty.", "OK");
                return;
            }

            if (!bridge.TryRewriteText(
                    fieldTitle,
                    string.Empty,
                    originalText,
                    customPrompt,
                    desiredTone,
                    null,
                    contextFactory?.Invoke(),
                    out var rewrittenText,
                    out var error))
            {
                EditorUtility.DisplayDialog("AI Rewrite Failed", error, "OK");
                return;
            }

            DialogRewritePreviewWindow.Open(
                originalText,
                rewrittenText,
                approvedText =>
                {
                    field.SetValueWithoutNotify(approvedText);
                    applyValue?.Invoke(approvedText);
                });
        }

        private static DialogGraphAiContext BuildEnvironmentRewriteContext(DialogEnvironmentSO environment)
        {
            return new DialogGraphAiContext
            {
                graphName = string.Empty,
                environment = environment == null
                    ? null
                    : new DialogEnvironmentAiContext
                    {
                        environmentId = environment.EnvironmentID,
                        displayName = environment.DisplayName,
                        description = environment.Description,
                        atmosphere = environment.Atmosphere,
                        canonRules = environment.CanonRules,
                        defaultTone = environment.DefaultTone,
                    },
                sceneTone = environment?.DefaultTone ?? string.Empty,
                extraRules = environment?.CanonRules ?? string.Empty,
                availableCharacters = new List<string>(),
                availableActions = new List<string>(),
            };
        }

        private static DialogGraphAiContext BuildSceneContextRewriteContext(DialogSceneContextSO sceneContext)
        {
            return new DialogGraphAiContext
            {
                graphName = string.Empty,
                sceneContextAssetName = sceneContext?.name ?? string.Empty,
                sceneGoal = sceneContext?.SceneGoal ?? string.Empty,
                sceneTone = sceneContext?.Tone ?? string.Empty,
                extraRules = sceneContext?.ExtraRules ?? string.Empty,
                environment = sceneContext?.Environment == null
                    ? null
                    : new DialogEnvironmentAiContext
                    {
                        environmentId = sceneContext.Environment.EnvironmentID,
                        displayName = sceneContext.Environment.DisplayName,
                        description = sceneContext.Environment.Description,
                        atmosphere = sceneContext.Environment.Atmosphere,
                        canonRules = sceneContext.Environment.CanonRules,
                        defaultTone = sceneContext.Environment.DefaultTone,
                    },
                availableCharacters = sceneContext?.ParticipatingCharacters?
                    .Where(character => character != null)
                    .Select(ResolveCharacterChoice)
                    .ToList() ?? new List<string>(),
                availableActions = sceneContext?.AvailableActions?
                    .Where(action => action != null)
                    .Select(ResolveActionChoice)
                    .ToList() ?? new List<string>(),
            };
        }
    }
}
