using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Modal dialog for selecting one or more target languages to create at once.
    /// </summary>
    public class AddLanguageDialog : EditorWindow
    {
        private Action<IReadOnlyList<DialogLocalizationTable>> _onCreated;
        private readonly Dictionary<string, Toggle> _togglesByChoice = new();

        private TextField _searchField;
        private TextField _customLocaleField;
        private Label _selectionSummaryLabel;
        private Label _validationLabel;
        private ScrollView _languageList;
        private Button _createBtn;

        private List<string> _allChoices = new();
        private HashSet<string> _existingLocaleCodes;
        private string _sourceLocaleCode;

        public static void Show(Action<IReadOnlyList<DialogLocalizationTable>> onCreated)
        {
            var win = CreateInstance<AddLanguageDialog>();
            win.titleContent = new GUIContent("Add Languages");
            win._onCreated = onCreated;
            win.minSize = new Vector2(520, 560);
            win.maxSize = new Vector2(520, 760);
            win.ShowModal();
        }

        private void OnEnable()
        {
            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null)
            {
                rootVisualElement.styleSheets.Add(ss);
            }

            var locSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.LOCALIZATION_MANAGER_STYLE_PATH);
            if (locSs != null)
            {
                rootVisualElement.styleSheets.Add(locSs);
            }

            BuildUI();
        }

        private void BuildUI()
        {
            var service = new DialogLocalizationRegistryService();
            _existingLocaleCodes = new HashSet<string>(
                service.GetAllLocaleCodes(),
                StringComparer.OrdinalIgnoreCase);
            _sourceLocaleCode = service.GetSourceTable()?.LocaleCode;

            _allChoices = new List<string>(DialogLocalizationLanguageCatalog.GetChoiceLabels(
                _existingLocaleCodes,
                _sourceLocaleCode));

            rootVisualElement.Clear();
            _togglesByChoice.Clear();

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 12;
            root.style.backgroundColor = new StyleColor(new Color(0.07f, 0.08f, 0.12f)); // Matches Canvas

            var titleLabel = new Label("Add Target Languages");
            titleLabel.style.fontSize = 15;
            titleLabel.style.color = new StyleColor(new Color(0.91f, 0.93f, 0.96f)); // Matches High emphasis
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(titleLabel);

            var hintLabel = new Label("Select one or more languages to add as translation columns. Source language remains the master.");
            hintLabel.AddToClassList("loc-status-label");
            hintLabel.style.marginBottom = 12;
            root.Add(hintLabel);

            _searchField = new TextField("Search");
            _searchField.AddToClassList("dlg-textfield");
            _searchField.RegisterValueChangedCallback(_ => RebuildLanguageList());
            root.Add(_searchField);

            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.marginBottom = 8;

            var selectPopularBtn = new Button(SelectPopular) { text = "Popular" };
            selectPopularBtn.AddToClassList("loc-btn");
            actionsRow.Add(selectPopularBtn);

            var selectAllBtn = new Button(SetAllSelected) { text = "All" };
            selectAllBtn.AddToClassList("loc-btn");
            actionsRow.Add(selectAllBtn);

            var clearBtn = new Button(ClearSelection) { text = "None" };
            clearBtn.AddToClassList("loc-btn");
            actionsRow.Add(clearBtn);

            root.Add(actionsRow);

            _selectionSummaryLabel = new Label(string.Empty);
            _selectionSummaryLabel.AddToClassList("loc-status-label");
            _selectionSummaryLabel.style.marginBottom = 4;
            root.Add(_selectionSummaryLabel);

            _languageList = new ScrollView(ScrollViewMode.Vertical);
            _languageList.style.flexGrow = 1;
            _languageList.style.height = 320;
            _languageList.style.backgroundColor = new StyleColor(new Color(0.05f, 0.06f, 0.09f)); // Deep
            _languageList.style.borderTopLeftRadius = 4;
            _languageList.style.borderTopRightRadius = 4;
            _languageList.style.borderBottomLeftRadius = 4;
            _languageList.style.borderBottomRightRadius = 4;
_languageList.style.paddingLeft = 8;
            _languageList.style.paddingRight = 8;
            root.Add(_languageList);

            _customLocaleField = new TextField("Custom Locale Code");
            _customLocaleField.AddToClassList("dlg-textfield");
            _customLocaleField.style.marginTop = 12;
            _customLocaleField.tooltip = "Optional BCP 47 locale code, e.g. sr-RS or fil-PH";
            _customLocaleField.RegisterValueChangedCallback(_ => ValidateInput());
            root.Add(_customLocaleField);

            _validationLabel = new Label(string.Empty);
            _validationLabel.AddToClassList("loc-status-label--error");
            _validationLabel.style.fontSize = 10;
            _validationLabel.style.display = DisplayStyle.None;
            root.Add(_validationLabel);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            btnRow.style.marginTop = 12;

            var cancelBtn = new Button(Close) { text = "Cancel" };
            cancelBtn.AddToClassList("loc-btn");
            cancelBtn.style.marginRight = 8;
            btnRow.Add(cancelBtn);

            _createBtn = new Button(OnCreate) { text = "Add Selected Languages" };
            _createBtn.AddToClassList("loc-btn");
            _createBtn.AddToClassList("loc-btn--primary");
            btnRow.Add(_createBtn);

            root.Add(btnRow);
            rootVisualElement.Add(root);

            RebuildLanguageList();
            UpdateSelectionSummary();
            ValidateInput();
        }

        private void RebuildLanguageList()
        {
            _languageList.Clear();
            var filter = _searchField?.value?.Trim() ?? string.Empty;
            var visibleChoices = _allChoices.Where(choice => MatchesFilter(choice, filter)).ToList();

            if (visibleChoices.Count == 0)
            {
                var emptyLabel = new Label("No matching languages.");
                emptyLabel.AddToClassList("loc-status-label");
                emptyLabel.style.marginTop = 12;
                _languageList.Add(emptyLabel);
                return;
            }

            foreach (var choice in visibleChoices)
            {
                var toggle = GetOrCreateToggle(choice);
                toggle.style.color = new StyleColor(new Color(0.78f, 0.82f, 0.88f));
                _languageList.Add(toggle);
            }
        }

        private Toggle GetOrCreateToggle(string choice)
        {
            if (_togglesByChoice.TryGetValue(choice, out var existing))
            {
                return existing;
            }

            var toggle = new Toggle(choice);
            toggle.style.marginBottom = 2;
            toggle.RegisterValueChangedCallback(_ =>
            {
                UpdateSelectionSummary();
                ValidateInput();
            });

            _togglesByChoice[choice] = toggle;
            return toggle;
        }

        private void SelectPopular()
        {
            ClearSelectionSilently();

            var preferred = new[]
            {
                "French",
                "German",
                "Spanish",
                "Italian",
                "Portuguese",
                "Japanese",
                "Korean",
                "Chinese",
                "Russian",
                "Polish"
            };

            foreach (var choice in _allChoices)
            {
                if (preferred.Any(label => choice.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    GetOrCreateToggle(choice).SetValueWithoutNotify(true);
                }
            }

            UpdateSelectionSummary();
            ValidateInput();
            RebuildLanguageList();
        }

        private void SetAllSelected()
        {
            foreach (var choice in _allChoices)
            {
                GetOrCreateToggle(choice).SetValueWithoutNotify(true);
            }

            UpdateSelectionSummary();
            ValidateInput();
            RebuildLanguageList();
        }

        private void ClearSelection()
        {
            ClearSelectionSilently();
            UpdateSelectionSummary();
            ValidateInput();
            RebuildLanguageList();
        }

        private void ClearSelectionSilently()
        {
            foreach (var toggle in _togglesByChoice.Values)
            {
                toggle.SetValueWithoutNotify(false);
            }
        }

        private void UpdateSelectionSummary()
        {
            var selectedCount = GetSelectedChoices().Count;
            _selectionSummaryLabel.text = selectedCount == 0
                ? "No built-in languages selected."
                : $"{selectedCount} language{(selectedCount == 1 ? string.Empty : "s")} selected.";
        }

        private void ValidateInput()
        {
            var selectedCodes = GetSelectedLocaleCodes();
            var customCode = _customLocaleField?.value?.Trim() ?? string.Empty;

            if (selectedCodes.Count == 0 && string.IsNullOrWhiteSpace(customCode))
            {
                SetValidation("Select at least one language or enter a custom locale code.", false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(customCode) &&
                _existingLocaleCodes.Contains(customCode))
            {
                SetValidation($"A table for '{customCode}' already exists.", false);
                return;
            }

            SetValidation(string.Empty, true);
        }

        private void SetValidation(string message, bool valid)
        {
            _createBtn?.SetEnabled(valid);
            _validationLabel.text = message;
            _validationLabel.style.display = string.IsNullOrEmpty(message)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private List<string> GetSelectedChoices()
        {
            return _togglesByChoice
                .Where(pair => pair.Value.value)
                .Select(pair => pair.Key)
                .ToList();
        }

        private List<string> GetSelectedLocaleCodes()
        {
            return GetSelectedChoices()
                .Select(choice => DialogLocalizationLanguageCatalog.TryGetLocaleCode(choice, out var localeCode)
                    ? localeCode
                    : string.Empty)
                .Where(localeCode => !string.IsNullOrWhiteSpace(localeCode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void OnCreate()
        {
            var service = new DialogLocalizationRegistryService();
            var createdTables = new List<DialogLocalizationTable>();

            foreach (var localeCode in GetSelectedLocaleCodes())
            {
                var existing = service.GetTableForLocale(localeCode);
                if (existing != null)
                {
                    continue;
                }

                var table = service.CreateNewTable(localeCode);
                if (table != null)
                {
                    createdTables.Add(table);
                }
            }

            var customCode = _customLocaleField?.value?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(customCode) &&
                service.GetTableForLocale(customCode) == null)
            {
                var customTable = service.CreateNewTable(customCode);
                if (customTable != null)
                {
                    createdTables.Add(customTable);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _onCreated?.Invoke(createdTables);
            Close();
        }

        private static bool MatchesFilter(string choice, string filter)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                   choice.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
