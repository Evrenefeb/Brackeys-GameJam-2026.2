using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Utils;
using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows.Tabs
{
    /// <summary>
    /// Global tab for managing all <see cref="DialogVariableSO"/> assets in the project.
    /// Left panel: filterable + type-filtered list. Right panel: inline inspector for the selected asset.
    /// </summary>
    public class VariablesTabController
    {
        #region ---------------- Constants ----------------

        private const float ListPanelWidth = 240f;

        private static readonly List<string> TypeFilterOptions = new()
        {
            "All", "Boolean", "Integer", "Float", "String"
        };

        #endregion

        #region ---------------- State ----------------

        private VisualElement    _root;
        private ScrollView       _listScroll;
        private VisualElement    _inspectorPanel;
        private ToolbarSearchField _searchField;
        private DropdownField    _typeDropdown;
        private string           _searchQuery = string.Empty;
        private string           _typeFilter  = "All";
        private DialogVariableSO _selected;

        #endregion

        #region ---------------- Public API ----------------

        /// <summary>Builds the Variables tab content into the given root.</summary>
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
            topBar.style.paddingBottom = 4;
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

            var createBtn = new Button(OnClickCreate) { text = "+", tooltip = "Create New Variable" };
            createBtn.AddToClassList("dlg-btn");
            createBtn.AddToClassList("success");
            createBtn.style.minWidth   = 26;
            createBtn.style.minHeight  = 26;
            createBtn.style.marginLeft = 4;
            topBar.Add(createBtn);

            // Type filter dropdown
            var currentTypeIndex = Mathf.Max(0, TypeFilterOptions.IndexOf(_typeFilter));
            _typeDropdown = new DropdownField(TypeFilterOptions, currentTypeIndex) { label = string.Empty };
            _typeDropdown.style.marginLeft   = 8;
            _typeDropdown.style.marginRight  = 8;
            _typeDropdown.style.marginBottom = 4;
            _typeDropdown.style.flexShrink   = 0;
            _typeDropdown.RegisterValueChangedCallback(evt =>
            {
                _typeFilter = evt.newValue ?? "All";
                RefreshList();
            });
            panel.Add(_typeDropdown);

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

            var registry  = new DialogVariableRegistryService();
            var variables = registry.GetAllRegisteredDefinitions()
                .Where(v => v != null)
                .Where(MatchesFilter)
                .OrderBy(v => v.ValueType.ToString())
                .ThenBy(v => v.Key)
                .ToList();

            if (variables.Count == 0)
            {
                var lbl = new Label(string.IsNullOrEmpty(_searchQuery) && _typeFilter == "All"
                    ? "No variables yet.\nPress + to create one."
                    : "No variables match the current filters.");
                lbl.AddToClassList("dgs-page-subtitle");
                lbl.style.paddingTop    = 12;
                lbl.style.paddingBottom = 12;
                lbl.style.paddingLeft   = 12;
                lbl.style.paddingRight  = 12;
                lbl.style.whiteSpace = WhiteSpace.Normal;
                _listScroll.Add(lbl);
                return;
            }

            foreach (var variable in variables)
            {
                var v   = variable;
                var row = new Button(() => SelectVariable(v));
                row.AddToClassList("dgs-nav-button");
                row.AddToClassList("dgs-asset-list-row");
                row.style.height         = StyleKeyword.Auto;
                row.style.minHeight      = 34;
                row.style.paddingTop     = 5;
                row.style.paddingBottom  = 5;
                row.style.flexDirection  = FlexDirection.Row;
                row.style.alignItems     = Align.Center;
                row.style.unityTextAlign = TextAnchor.MiddleLeft;

                if (_selected == v)
                    row.AddToClassList("active");

                var keyLbl = new Label(v.Key);
                keyLbl.style.flexGrow = 1;
                keyLbl.style.overflow = Overflow.Hidden;
                row.Add(keyLbl);

                var typeBadge = new Label(v.ValueType.ToString().Substring(0, 1));
                typeBadge.AddToClassList("dgs-adv-value-badge");
                typeBadge.style.marginLeft = 4;
                typeBadge.style.flexShrink = 0;
                typeBadge.tooltip = v.ValueType.ToString();
                row.Add(typeBadge);

                _listScroll.Add(row);
            }
        }

        private bool MatchesFilter(DialogVariableSO variable)
        {
            if (!string.IsNullOrEmpty(_searchQuery) &&
                variable.Key.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (_typeFilter != "All" &&
                !string.Equals(variable.ValueType.ToString(), _typeFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private void SelectVariable(DialogVariableSO variable)
        {
            _selected = variable;
            RefreshList();
            ShowInspector(variable);
        }

        #endregion

        #region ---------------- Inspector Panel ----------------

        private void ShowInspector(DialogVariableSO variable)
        {
            if (_inspectorPanel == null) return;
            _inspectorPanel.Clear();

            if (variable == null)
            {
                var placeholder = new Label("Select a variable from the list to inspect it.");
                placeholder.AddToClassList("dgs-page-subtitle");
                placeholder.style.paddingTop    = 24;
                placeholder.style.paddingBottom = 24;
                placeholder.style.paddingLeft   = 24;
                placeholder.style.paddingRight  = 24;
                placeholder.style.whiteSpace = WhiteSpace.Normal;
                _inspectorPanel.Add(placeholder);
                return;
            }

            var so = new SerializedObject(variable);

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

            var headerTitle = new Label(variable.Key);
            headerTitle.AddToClassList("dgs-page-title");
            headerTitle.style.flexGrow = 1;
            header.Add(headerTitle);

            var pingBtn = new Button(() => EditorGUIUtility.PingObject(variable)) { text = "Ping" };
            pingBtn.AddToClassList("dlg-btn");
            pingBtn.AddToClassList("secondary");
            pingBtn.style.fontSize    = 10;
            pingBtn.style.minHeight   = 22;
            pingBtn.style.marginRight = 4;
            header.Add(pingBtn);

            var deleteBtn = new Button(() => OnClickDelete(variable)) { text = "Delete" };
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

            // Variable Key (maps to the private 'variableKey' field)
            var keyProp  = so.FindProperty("variableKey");
            var keyField = new PropertyField(keyProp, "Variable Key");
            keyField.style.marginBottom = 6;
            keyField.Bind(so);
            keyField.RegisterValueChangeCallback(_ =>
            {
                so.ApplyModifiedProperties();
                headerTitle.text = variable.Key;
                RefreshList();
            });
            card.Add(keyField);

            // Value Type
            var typeProp  = so.FindProperty("valueType");
            var typeField = new PropertyField(typeProp, "Value Type");
            typeField.style.marginBottom = 6;
            typeField.Bind(so);

            // Default value fields — show/hide based on current type
            var boolField   = MakeBoundPropertyField(so, "boolDefaultValue",   "Default Bool Value");
            var intField    = MakeBoundPropertyField(so, "intDefaultValue",     "Default Int Value");
            var floatField  = MakeBoundPropertyField(so, "floatDefaultValue",   "Default Float Value");
            var stringField = MakeBoundPropertyField(so, "stringDefaultValue",  "Default String Value");

            void SyncDefaultFields()
            {
                so.Update();
                var vt = (DialogueVariableValueType)typeProp.enumValueIndex;
                boolField.style.display   = vt == DialogueVariableValueType.Boolean ? DisplayStyle.Flex : DisplayStyle.None;
                intField.style.display    = vt == DialogueVariableValueType.Integer  ? DisplayStyle.Flex : DisplayStyle.None;
                floatField.style.display  = vt == DialogueVariableValueType.Float    ? DisplayStyle.Flex : DisplayStyle.None;
                stringField.style.display = vt == DialogueVariableValueType.String   ? DisplayStyle.Flex : DisplayStyle.None;
            }

            typeField.RegisterValueChangeCallback(_ =>
            {
                so.ApplyModifiedProperties();
                SyncDefaultFields();
                RefreshList();
            });

            card.Add(typeField);
            card.Add(boolField);
            card.Add(intField);
            card.Add(floatField);
            card.Add(stringField);

            // Set initial visibility
            SyncDefaultFields();
        }

        private static PropertyField MakeBoundPropertyField(SerializedObject so, string propertyName, string label)
        {
            var prop  = so.FindProperty(propertyName);
            var field = new PropertyField(prop, label);
            field.style.marginBottom = 6;
            field.Bind(so);
            return field;
        }

        #endregion

        #region ---------------- Create / Delete ----------------

        /// <summary>
        /// Creates a variable asset and immediately selects it in this tab.
        /// Used by both the tab create button and the AI creation flow.
        /// </summary>
        public DialogVariableSO CreateAndSelectVariable(
            string variableLabel,
            DialogueVariableValueType valueType = DialogueVariableValueType.Boolean,
            string defaultValue = null)
        {
            var registry = new DialogVariableRegistryService();
            var variable = registry.CreateNewAsset(variableLabel);
            if (variable == null)
            {
                return null;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Variable");
            var undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterCreatedObjectUndo(variable, "Create Variable");
            Undo.RecordObject(variable, "Initialize Variable");

            DialogAssetInitializer.InitializeVariable(variable, variableLabel, valueType, defaultValue);
            DialogAssetInitializer.FinalizeAsset(variable, "Create Variable", pingAsset: false);

            Undo.CollapseUndoOperations(undoGroup);

            _searchQuery = string.Empty;
            _typeFilter = "All";
            _searchField?.SetValueWithoutNotify(_searchQuery);
            _typeDropdown?.SetValueWithoutNotify(_typeFilter);
            _selected = variable;
            RefreshList();
            ShowInspector(variable);
            DialogGraphDefinitionResolver.PingAndSelect(variable);

            return variable;
        }

        private void OnClickCreate()
        {
            CreateAndSelectVariable("New Variable");
        }

        private void OnClickDelete(DialogVariableSO variable)
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete Variable",
                    $"Are you sure you want to delete '{variable.Key}'?\nThis cannot be undone.",
                    "Delete", "Cancel"))
                return;

            var path = AssetDatabase.GetAssetPath(variable);
            if (!string.IsNullOrEmpty(path))
            {
                if (Selection.activeObject == variable)
                {
                    Selection.activeObject = null;
                }

                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (_selected == variable) _selected = null;
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
