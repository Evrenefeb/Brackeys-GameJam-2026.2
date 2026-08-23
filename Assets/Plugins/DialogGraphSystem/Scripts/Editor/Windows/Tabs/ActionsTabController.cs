using System;
using System.Linq;
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
    /// Global tab for managing all registered <see cref="DialogActionSO"/> assets.
    /// Left panel: filterable list. Right panel: inline inspector for the selected asset.
    /// </summary>
    public class ActionsTabController
    {
        #region ---------------- Constants ----------------

        private const float ListPanelWidth = 240f;

        #endregion

        #region ---------------- State ----------------

        private VisualElement   _root;
        private ScrollView      _listScroll;
        private VisualElement   _inspectorPanel;
        private UnityEditor.UIElements.ToolbarSearchField _searchField;
        private string          _searchQuery = string.Empty;
        private DialogActionSO  _selected;

        #endregion

        #region ---------------- Public API ----------------

        /// <summary>Builds the Actions tab content into the given root.</summary>
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
            panel.style.width         = ListPanelWidth;
            panel.style.minWidth      = 160;
            panel.style.flexShrink    = 0;
            panel.style.flexGrow      = 0;
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

            _searchField = new UnityEditor.UIElements.ToolbarSearchField();
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

            var createBtn = new Button(OnClickCreate) { text = "+", tooltip = "Create New Action" };
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

            var registry = new DialogActionRegistryService();
            var actions  = registry.GetAllRegisteredDefinitions()
                .Where(a => string.IsNullOrEmpty(_searchQuery) ||
                            a.DisplayName.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            a.ActionID.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (a.Description != null &&
                             a.Description.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(a => a.DisplayName)
                .ToList();

            if (actions.Count == 0)
            {
                var lbl = new Label(string.IsNullOrEmpty(_searchQuery)
                    ? "No actions yet.\nPress + to create one."
                    : "No actions match the search.");
                lbl.AddToClassList("dgs-page-subtitle");
                lbl.style.paddingTop    = 12;
                lbl.style.paddingBottom = 12;
                lbl.style.paddingLeft   = 12;
                lbl.style.paddingRight  = 12;
                lbl.style.whiteSpace = WhiteSpace.Normal;
                _listScroll.Add(lbl);
                return;
            }

            foreach (var action in actions)
            {
                var a   = action;
                var row = new Button(() => SelectAction(a));
                row.AddToClassList("dgs-nav-button");
                row.AddToClassList("dgs-asset-list-row");
                row.style.height         = StyleKeyword.Auto;
                row.style.minHeight      = 34;
                row.style.paddingTop     = 5;
                row.style.paddingBottom  = 5;
                row.style.unityTextAlign = TextAnchor.MiddleLeft;

                if (_selected == a)
                    row.AddToClassList("active");

                var nameLbl = new Label(a.DisplayName);
                nameLbl.style.flexGrow = 1;
                nameLbl.style.overflow = Overflow.Hidden;
                row.Add(nameLbl);

                _listScroll.Add(row);
            }
        }

        private void SelectAction(DialogActionSO action)
        {
            _selected = action;
            RefreshList();
            ShowInspector(action);
        }

        #endregion

        #region ---------------- Inspector Panel ----------------

        private void ShowInspector(DialogActionSO action)
        {
            if (_inspectorPanel == null) return;
            _inspectorPanel.Clear();

            if (action == null)
            {
                var placeholder = new Label("Select an action from the list to inspect it.");
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
            header.style.flexDirection     = FlexDirection.Row;
            header.style.alignItems        = Align.Center;
            header.style.paddingTop        = 12;
            header.style.paddingBottom     = 8;
            header.style.paddingLeft       = 12;
            header.style.paddingRight      = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.16f, 0.18f, 0.27f);
            header.style.flexShrink        = 0;

            var headerTitle = new Label(action.DisplayName);
            headerTitle.AddToClassList("dgs-page-title");
            headerTitle.style.flexGrow = 1;
            header.Add(headerTitle);

            var pingBtn = new Button(() => DialogGraphDefinitionResolver.PingAndSelect(action)) { text = "Ping" };
            pingBtn.AddToClassList("dlg-btn");
            pingBtn.AddToClassList("secondary");
            pingBtn.style.fontSize    = 10;
            pingBtn.style.minHeight   = 22;
            pingBtn.style.marginRight = 4;
            header.Add(pingBtn);

            var deleteBtn = new Button(() => OnClickDelete(action)) { text = "Delete" };
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
            var nameField = new TextField("Display Name") { value = action.DisplayName };
            nameField.style.marginBottom = 6;
            nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(action, "Edit Action Name");
                action.DisplayName = evt.newValue ?? string.Empty;
                EditorUtility.SetDirty(action);
                AssetDatabase.SaveAssets();
                headerTitle.text = action.DisplayName;
                RefreshList();
            });
            card.Add(nameField);

            // Action ID
            var idField = new TextField("Action ID") { value = action.ActionID };
            idField.style.marginBottom = 6;
            idField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(action, "Edit Action ID");
                action.ActionID = evt.newValue ?? string.Empty;
                EditorUtility.SetDirty(action);
                AssetDatabase.SaveAssets();
            });
            card.Add(idField);

            // Description
            var descField = new TextField("Description")
            {
                value     = action.Description ?? string.Empty,
                multiline = true
            };
            descField.style.marginBottom = 6;
            descField.style.minHeight    = 60;
            descField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(action, "Edit Action Description");
                action.Description = evt.newValue ?? string.Empty;
                EditorUtility.SetDirty(action);
                AssetDatabase.SaveAssets();
            });
            card.Add(descField);

            // Default Payload JSON
            var payloadField = new TextField("Default Payload JSON")
            {
                value     = action.DefaultPayloadJson,
                multiline = true
            };
            payloadField.style.marginBottom = 6;
            payloadField.style.minHeight    = 60;
            payloadField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(action, "Edit Action Payload");
                action.DefaultPayloadJson = evt.newValue ?? "{}";
                EditorUtility.SetDirty(action);
                AssetDatabase.SaveAssets();
            });
            card.Add(payloadField);

            // Wait For Completion
            var waitToggle = new Toggle("Wait For Completion") { value = action.WaitForCompletion };
            waitToggle.style.marginBottom = 6;

            Label delayLabel = null;
            FloatField delayField = null;

            void SyncDelayVisibility(bool visible)
            {
                if (delayLabel != null) delayLabel.style.display  = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (delayField != null) delayField.style.display  = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            waitToggle.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(action, "Edit Wait For Completion");
                action.WaitForCompletion = evt.newValue;
                EditorUtility.SetDirty(action);
                AssetDatabase.SaveAssets();
                SyncDelayVisibility(evt.newValue);
            });
            card.Add(waitToggle);

            // Default Delay (only visible when WaitForCompletion is true)
            delayLabel = new Label("Default Delay (s)");
            delayLabel.AddToClassList("dgs-muted");
            delayLabel.style.marginTop = 2;
            delayLabel.style.display   = action.WaitForCompletion ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(delayLabel);

            delayField = new FloatField { value = action.DefaultDelay };
            delayField.style.marginBottom = 6;
            delayField.style.display      = action.WaitForCompletion ? DisplayStyle.Flex : DisplayStyle.None;
            delayField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(action, "Edit Action Delay");
                action.DefaultDelay = Mathf.Max(0f, evt.newValue);
                EditorUtility.SetDirty(action);
                AssetDatabase.SaveAssets();
            });
            card.Add(delayField);
        }

        #endregion

        #region ---------------- Create / Delete ----------------

        /// <summary>
        /// Creates an action asset and immediately selects it in this tab.
        /// Used by both the tab create button and the AI creation flow.
        /// </summary>
        public DialogActionSO CreateAndSelectAction(
            string actionLabel,
            string description = null)
        {
            var registry = new DialogActionRegistryService();
            var asset = registry.CreateNewAsset(actionLabel);
            if (asset == null)
            {
                return null;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Action");
            var undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCreatedObjectUndo(asset, "Create Action");
            Undo.RecordObject(asset, "Initialize Action");

            DialogAssetInitializer.InitializeAction(asset, actionLabel, description);
            DialogAssetInitializer.FinalizeAsset(asset, "Create Action", pingAsset: false);

            Undo.CollapseUndoOperations(undoGroup);

            _searchQuery = string.Empty;
            _searchField?.SetValueWithoutNotify(_searchQuery);
            _selected = asset;
            RefreshList();
            ShowInspector(asset);
            DialogGraphDefinitionResolver.PingAndSelect(asset);

            return asset;
        }

        private void OnClickCreate()
        {
            CreateAndSelectAction("New Action");
        }

        private void OnClickDelete(DialogActionSO action)
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete Action",
                    $"Are you sure you want to delete '{action.DisplayName}'?\nThis cannot be undone.",
                    "Delete", "Cancel"))
                return;

            var path = AssetDatabase.GetAssetPath(action);
            if (!string.IsNullOrEmpty(path))
            {
                if (Selection.activeObject == action)
                {
                    Selection.activeObject = null;
                }

                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (_selected == action) _selected = null;
            RefreshList();
            ShowInspector(null);
        }

        #endregion

        #region ---------------- Helpers ----------------

        private static VisualElement BuildDivider()
        {
            var divider = new VisualElement();
            divider.style.width           = 1;
            divider.style.flexShrink      = 0;
            divider.style.backgroundColor = new Color(0.16f, 0.18f, 0.27f);
            return divider;
        }

        #endregion
    }
}
