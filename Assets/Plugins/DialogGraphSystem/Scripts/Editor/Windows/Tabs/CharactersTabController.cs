using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows.Tabs
{
    /// <summary>
    /// Global tab for managing all registered <see cref="DialogCharacterSO"/> assets.
    /// Left panel: filterable list. Right panel: inline inspector for the selected asset.
    /// </summary>
    public class CharactersTabController
    {
        #region ---------------- Constants ----------------

        private const float ListPanelWidth = 240f;

        #endregion

        #region ---------------- State ----------------

        private VisualElement     _root;
        private ScrollView        _listScroll;
        private VisualElement     _inspectorPanel;
        private ToolbarSearchField _searchField;
        private string            _searchQuery = string.Empty;
        private DialogCharacterSO _selected;
        private readonly Func<IDialogGraphOwner> _graphOwnerResolver;

        #endregion

        public CharactersTabController(Func<IDialogGraphOwner> graphOwnerResolver = null)
        {
            _graphOwnerResolver = graphOwnerResolver;
        }

        #region ---------------- Public API ----------------

        /// <summary>Builds the Characters tab content into the given root.</summary>
        public void BuildUI(VisualElement root)
        {
            _root = root;
            _root.Clear();

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.SETTINGS_STYLE_PATH);
            if (ss != null && !_root.styleSheets.Contains(ss))
                _root.styleSheets.Add(ss);

            // ── Body: left list | right inspector ────────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow      = 1;
            body.style.overflow      = Overflow.Hidden;
            _root.Add(body);

            body.Add(BuildListPanel());
            body.Add(BuildDivider());

            _inspectorPanel = new VisualElement();
            _inspectorPanel.style.flexGrow   = 1;
            _inspectorPanel.style.flexShrink = 1;
            _inspectorPanel.style.overflow   = Overflow.Hidden;
            body.Add(_inspectorPanel);

            RefreshList();
            ShowInspector(_selected);
        }

        #endregion

        #region ---------------- List Panel ----------------

        private VisualElement BuildListPanel()
        {
            var panel = new VisualElement();
            panel.style.width      = ListPanelWidth;
            panel.style.minWidth   = 160;
            panel.style.flexShrink = 0;
            panel.style.flexGrow   = 0;
            panel.style.flexDirection = FlexDirection.Column;

            // Top bar: search + create button
            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.alignItems    = Align.Center;
            topBar.style.paddingLeft   = 8;
            topBar.style.paddingRight  = 8;
            topBar.style.paddingTop    = 8;
            topBar.style.paddingBottom = 6;
            topBar.style.flexShrink    = 0;
            panel.Add(topBar);

            _searchField = new ToolbarSearchField();
            _searchField.style.flexGrow   = 1;
            _searchField.style.flexShrink = 1;
            _searchField.style.minWidth   = 0;
            _searchField.SetValueWithoutNotify(_searchQuery);
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue ?? string.Empty;
                RefreshList();
            });
            topBar.Add(_searchField);

            var createBtn = new Button(OnClickCreate) { text = "+", tooltip = "Create New Character" };
            createBtn.AddToClassList("dlg-btn");
            createBtn.AddToClassList("success");
            createBtn.style.minWidth   = 26;
            createBtn.style.minHeight  = 26;
            createBtn.style.marginLeft = 4;
            topBar.Add(createBtn);

            // Scrollable list
            _listScroll = new ScrollView(ScrollViewMode.Vertical);
            _listScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _listScroll.style.flexGrow = 1;
            panel.Add(_listScroll);

            return panel;
        }

        private void RefreshList()
        {
            if (_listScroll == null) return;
            _listScroll.Clear();

            var registry   = new DialogCharacterRegistryService();
            var characters = registry.GetAllRegisteredDefinitions()
                .Where(c => string.IsNullOrEmpty(_searchQuery) ||
                            c.DisplayName.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            c.CharacterID.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(c => c.DisplayName)
                .ToList();

            if (characters.Count == 0)
            {
                var lbl = new Label(string.IsNullOrEmpty(_searchQuery)
                    ? "No characters yet.\nPress + to create one."
                    : "No characters match the search.");
                lbl.AddToClassList("dgs-page-subtitle");
                lbl.style.paddingTop    = 12;
                lbl.style.paddingBottom = 12;
                lbl.style.paddingLeft   = 12;
                lbl.style.paddingRight  = 12;
                lbl.style.whiteSpace = WhiteSpace.Normal;
                _listScroll.Add(lbl);
                return;
            }

            foreach (var character in characters)
            {
                var c   = character;
                var row = new Button(() => SelectCharacter(c));
                row.AddToClassList("dgs-nav-button");
                row.AddToClassList("dgs-asset-list-row");
                row.style.height        = StyleKeyword.Auto;
                row.style.minHeight     = 34;
                row.style.paddingTop    = 5;
                row.style.paddingBottom = 5;
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems    = Align.Center;
                row.style.unityTextAlign = TextAnchor.MiddleLeft;

                if (_selected == c)
                    row.AddToClassList("active");

                if (c.Portrait != null)
                {
                    var thumb = new Image { scaleMode = ScaleMode.ScaleToFit, image = c.Portrait.texture };
                    thumb.style.width    = 20;
                    thumb.style.height   = 20;
                    thumb.style.marginRight = 6;
                    thumb.style.flexShrink  = 0;
                    row.Add(thumb);
                }

                var nameLbl = new Label(c.DisplayName);
                nameLbl.style.flexGrow = 1;
                nameLbl.style.overflow = Overflow.Hidden;
                row.Add(nameLbl);

                _listScroll.Add(row);
            }
        }

        private void SelectCharacter(DialogCharacterSO character)
        {
            _selected = character;
            RefreshList();
            ShowInspector(character);
        }

        #endregion

        #region ---------------- Inspector Panel ----------------

        private void ShowInspector(DialogCharacterSO character)
        {
            if (_inspectorPanel == null) return;
            _inspectorPanel.Clear();

            if (character == null)
            {
                var placeholder = new Label("Select a character from the list to inspect it.");
                placeholder.AddToClassList("dgs-page-subtitle");
                placeholder.style.paddingTop    = 24;
                placeholder.style.paddingBottom = 24;
                placeholder.style.paddingLeft   = 24;
                placeholder.style.paddingRight  = 24;
                placeholder.style.whiteSpace = WhiteSpace.Normal;
                _inspectorPanel.Add(placeholder);
                return;
            }

            // Header row
            var header = new VisualElement();
            header.style.flexDirection   = FlexDirection.Row;
            header.style.alignItems      = Align.Center;
            header.style.paddingTop      = 12;
            header.style.paddingBottom   = 8;
            header.style.paddingLeft     = 12;
            header.style.paddingRight    = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.16f, 0.18f, 0.27f);
            header.style.flexShrink      = 0;

            var headerTitle = new Label(character.DisplayName);
            headerTitle.AddToClassList("dgs-page-title");
            headerTitle.style.flexGrow = 1;
            header.Add(headerTitle);

            var pingBtn = new Button(() => DialogGraphDefinitionResolver.PingAndSelect(character)) { text = "Ping" };
            pingBtn.AddToClassList("dlg-btn");
            pingBtn.AddToClassList("secondary");
            pingBtn.style.fontSize    = 10;
            pingBtn.style.minHeight   = 22;
            pingBtn.style.marginRight = 4;
            header.Add(pingBtn);

            var deleteBtn = new Button(() => OnClickDelete(character)) { text = "Delete" };
            deleteBtn.AddToClassList("dlg-btn");
            deleteBtn.AddToClassList("danger");
            deleteBtn.style.fontSize  = 10;
            deleteBtn.style.minHeight = 22;
            header.Add(deleteBtn);

            _inspectorPanel.Add(header);

            // Scrollable fields
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1;
            _inspectorPanel.Add(scroll);

            var card = new VisualElement();
            card.AddToClassList("dgs-card");
            card.style.marginTop    = 12;
            card.style.marginBottom = 12;
            card.style.marginLeft   = 12;
            card.style.marginRight  = 12;
            scroll.Add(card);

            // Display Name
            var nameField = new TextField("Display Name") { value = character.DisplayName };
            nameField.style.marginBottom = 6;
            nameField.RegisterValueChangedCallback(evt =>
            {
                var previousDisplayName = character.DisplayName;
                var previousCharacterId = character.CharacterID;
                Undo.RecordObject(character, "Edit Character Name");
                character.DisplayName = evt.newValue ?? string.Empty;
                EditorUtility.SetDirty(character);
                AssetDatabase.SaveAssets();
                headerTitle.text = character.DisplayName;
                PropagateCharacterToActiveGraph(character, character.Portrait != null, previousDisplayName, previousCharacterId);
                RefreshList();
            });
            card.Add(nameField);

            // Character ID
            var idField = new TextField("Character ID") { value = character.CharacterID };
            idField.style.marginBottom = 6;
            idField.RegisterValueChangedCallback(evt =>
            {
                var previousCharacterId = character.CharacterID;
                var currentDisplayName = character.DisplayName;
                Undo.RecordObject(character, "Edit Character ID");
                character.CharacterID = evt.newValue ?? string.Empty;
                EditorUtility.SetDirty(character);
                AssetDatabase.SaveAssets();
                PropagateCharacterToActiveGraph(character, character.Portrait != null, previousCharacterId, currentDisplayName);
            });
            card.Add(idField);

            // Short Description
            var descField = new TextField("Description")
            {
                value     = character.ShortDescription ?? string.Empty,
                multiline = true
            };
            descField.style.marginBottom = 6;
            descField.style.minHeight    = 60;
            descField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(character, "Edit Character Description");
                character.ShortDescription = evt.newValue ?? string.Empty;
                EditorUtility.SetDirty(character);
                AssetDatabase.SaveAssets();
            });
            card.Add(descField);

            // Portrait
            var portraitRow = new VisualElement();
            portraitRow.style.flexDirection = FlexDirection.Row;
            portraitRow.style.alignItems    = Align.Center;
            portraitRow.style.marginTop     = 6;

            var preview = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                image     = character.Portrait ? character.Portrait.texture : null
            };
            preview.style.width       = 64;
            preview.style.height      = 64;
            preview.style.marginRight = 10;
            preview.style.flexShrink  = 0;
            portraitRow.Add(preview);

            var portraitField = new ObjectField("Portrait")
            {
                objectType        = typeof(Sprite),
                allowSceneObjects = false,
                value             = character.Portrait
            };
            portraitField.style.width = 360;
            portraitField.style.maxWidth = 360;
            portraitField.style.flexGrow = 0;
            portraitField.style.flexShrink = 0;
            portraitField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(character, "Change Portrait");
                character.Portrait = evt.newValue as Sprite;
                EditorUtility.SetDirty(character);
                AssetDatabase.SaveAssets();
                preview.image = character.Portrait ? character.Portrait.texture : null;
                PropagateCharacterToActiveGraph(character, true, character.DisplayName, character.CharacterID);
                RefreshList();
            });
            portraitRow.Add(portraitField);
            card.Add(portraitRow);
        }

        #endregion

        #region ---------------- Create / Delete ----------------

        /// <summary>
        /// Creates a character asset and immediately selects it in this tab.
        /// Used by both the tab create button and the AI creation flow.
        /// </summary>
        public DialogCharacterSO CreateAndSelectCharacter(
            string displayName,
            string description = null,
            string speechStyle = null,
            string[] personalityTraits = null)
        {
            var registry = new DialogCharacterRegistryService();
            var asset = registry.CreateNewAsset(displayName);
            if (asset == null)
            {
                return null;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Character");
            var undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCreatedObjectUndo(asset, "Create Character");
            Undo.RecordObject(asset, "Initialize Character");

            DialogAssetInitializer.InitializeCharacter(asset, displayName, description, speechStyle, personalityTraits);
            DialogAssetInitializer.FinalizeAsset(asset, "Create Character", pingAsset: false);

            Undo.CollapseUndoOperations(undoGroup);

            _searchQuery = string.Empty;
            _searchField?.SetValueWithoutNotify(_searchQuery);
            _selected = asset;
            RefreshList();
            ShowInspector(asset);
            DialogGraphDefinitionResolver.PingAndSelect(asset);
            PropagateCharacterToActiveGraph(asset, asset.Portrait != null, asset.DisplayName, asset.CharacterID);

            return asset;
        }

        private void OnClickCreate()
        {
            CreateAndSelectCharacter("New Character");
        }

        private void OnClickDelete(DialogCharacterSO character)
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete Character",
                    $"Are you sure you want to delete '{character.DisplayName}'?\nThis cannot be undone.",
                    "Delete", "Cancel"))
                return;

            PropagateCharacterRemovalFromActiveGraph(character);

            var path = AssetDatabase.GetAssetPath(character);
            if (!string.IsNullOrEmpty(path))
            {
                if (Selection.activeObject == character)
                {
                    Selection.activeObject = null;
                }

                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (_selected == character) _selected = null;
            RefreshList();
            ShowInspector(null);
        }

        #endregion

        #region ---------------- Helpers ----------------

        private static VisualElement BuildDivider()
        {
            var divider = new VisualElement();
            divider.style.width            = 1;
            divider.style.flexShrink       = 0;
            divider.style.backgroundColor  = new Color(0.16f, 0.18f, 0.27f);
            return divider;
        }

        private void PropagateCharacterToActiveGraph(DialogCharacterSO character, bool applySprite, params string[] matchSpeakers)
        {
            var owner = _graphOwnerResolver?.Invoke();
            if (owner == null || character == null)
            {
                return;
            }

            var resolvedSpeakerName = ResolveSpeakerName(character);
            if (string.IsNullOrWhiteSpace(resolvedSpeakerName))
            {
                return;
            }

            var bindings = (matchSpeakers ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new DialogGraphEditorWindow.CharacterBinding
                {
                    originalName = name,
                    currentName = resolvedSpeakerName,
                    sprite = character.Portrait,
                    applySprite = applySprite,
                    forceRefresh = true
                })
                .ToList();

            if (bindings.Count == 0)
            {
                return;
            }

            owner.ApplySpritesToNodes(bindings);
            owner.RefreshGraphEditorState();
        }

        private void PropagateCharacterRemovalFromActiveGraph(DialogCharacterSO character)
        {
            var owner = _graphOwnerResolver?.Invoke();
            if (owner == null || character == null)
            {
                return;
            }

            var candidateNames = new[]
            {
                character.DisplayName,
                character.CharacterID
            };

            var bindings = candidateNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new DialogGraphEditorWindow.CharacterBinding
                {
                    originalName = name,
                    currentName = name,
                    sprite = null,
                    applySprite = true,
                    forceRefresh = true
                })
                .ToList();

            if (bindings.Count == 0)
            {
                return;
            }

            owner.ApplySpritesToNodes(bindings);
            owner.RefreshGraphEditorState();
        }

        private static string ResolveSpeakerName(DialogCharacterSO character)
        {
            if (character == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(character.DisplayName))
            {
                return character.DisplayName.Trim();
            }

            return character.CharacterID?.Trim() ?? string.Empty;
        }

        #endregion
    }
}
