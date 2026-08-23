using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Embeddable version of the localization manager shown in the main Dialogue
    /// System window.
    /// </summary>
    public class LocalizationManagerEmbedded : VisualElement
    {
        private DialogLocalizationRegistryService _registry;
        private IReadOnlyList<DialogLocalizationTable> _allTables;
        private DialogLocalizationTable _sourceTable;

        private readonly List<string> _availableLocaleCodes = new();
        private readonly HashSet<string> _activeLocaleCodes = new();

        private DialogGraph _selectedGraph;
        private string _bulkTranslateLocaleCode = string.Empty;

        private List<LocRow> _rows = new();
        private readonly List<LocRow> _filteredRows = new();

        private string _filter = string.Empty;
        private string _statusFilter = "All";
        private bool _shouldOverwriteExisting;

        private LocalizationTableView _tableView;
        private LocalizationEditPanel _editPanel;
        private TwoPaneSplitView _splitView;
        private VisualElement _bodyRoot;
        private ToolbarSearchField _searchField;
        private DropdownField _statusDropdown;
        private ObjectField _graphField;
        private VisualElement _langPillRow;
        private Label _statusLabel;
        private bool _detailsVisible;
        private bool _isInitialized;
        private LocRow _selectedRow;

        public LocalizationManagerEmbedded()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            _registry = new DialogLocalizationRegistryService();
            _selectedGraph = ResolveInitialGraphSelection();

            Reload();
            BuildUI();
        }

        public void SelectGraph(DialogGraph graph)
        {
            if (graph == null || graph == _selectedGraph)
            {
                return;
            }

            _selectedGraph = graph;
            _graphField?.SetValueWithoutNotify(_selectedGraph);
            Reload();
            RebuildLangPills();
            RebuildBody();
        }

        private static DialogGraph ResolveInitialGraphSelection()
        {
            return Selection.activeObject as DialogGraph
                ?? DialogGraphAssetPaths.LoadAllGraphAssets().FirstOrDefault();
        }

        private void Reload()
        {
            _allTables = _registry.GetAllTables();
            _sourceTable = _registry.GetSourceTable();

            if (_selectedGraph == null)
            {
                _selectedGraph = ResolveInitialGraphSelection();
            }

            _availableLocaleCodes.Clear();
            foreach (var table in _allTables ?? Array.Empty<DialogLocalizationTable>())
            {
                if (table == null || table.IsSourceLanguage || string.IsNullOrWhiteSpace(table.LocaleCode))
                {
                    continue;
                }

                _availableLocaleCodes.Add(table.LocaleCode);
            }

            if (!_isInitialized)
            {
                foreach (var code in _availableLocaleCodes)
                {
                    _activeLocaleCodes.Add(code);
                }
                _isInitialized = true;
            }
            else
            {
                _activeLocaleCodes.RemoveWhere(code => !_availableLocaleCodes.Contains(code));
            }

            if (string.IsNullOrWhiteSpace(_bulkTranslateLocaleCode) ||
                !DialogLocalizationLanguageCatalog.TryGetOption(_bulkTranslateLocaleCode, out _))
            {
                _bulkTranslateLocaleCode = ResolveDefaultTargetLocaleCode();
            }

            RebuildRows();
        }

        private void RebuildRows()
        {
            _rows = DialogLocalizationRowBuilder.BuildRows(
                _selectedGraph,
                _allTables,
                _activeLocaleCodes);
        }

        private void BuildUI()
        {
            Clear();

            Add(BuildToolbar());
            Add(BuildLangBar());
            Add(BuildStatusBar());

            _bodyRoot = new VisualElement();
            _bodyRoot.style.flexGrow = 1;
            _bodyRoot.style.flexDirection = FlexDirection.Column;
            Add(_bodyRoot);

            if (_selectedGraph == null)
            {
                ShowNoGraphHelp();
                return;
            }

            BuildSplitView();
        }

        private void ShowNoGraphHelp()
        {
            var helpBox = new HelpBox(
                "No dialog graph is selected. Pick a graph in the toolbar to build or translate localization rows.",
                HelpBoxMessageType.Info);
            helpBox.AddToClassList("loc-help-box");
            _bodyRoot.Add(helpBox);
        }

        private void BuildSplitView()
        {
            _bodyRoot.Clear();

            _tableView = new LocalizationTableView();
            _editPanel = new LocalizationEditPanel();

            _tableView.OnRowSelected += OnRowSelected;
            _tableView.OnTranslateRowRequested += OnTranslateRowRequested;
            _tableView.OnUseSourceRequested += OnUseSourceRequested;
            _tableView.OnTranslationEdited += OnTranslationEdited;
            _editPanel.OnCloseRequested += HideDetailsPanel;
            _editPanel.OnEntryChanged += row =>
            {
                if (row != null)
                {
                    row.Status = GetOverallStatus(row);
                }

                RefreshTable();
            };
            _editPanel.OnEntryDeleted += _ =>
            {
                Reload();
                RefreshTable();
            };

            if (_detailsVisible)
            {
                _splitView = new TwoPaneSplitView(1, 520f, TwoPaneSplitViewOrientation.Horizontal);
                _splitView.style.flexGrow = 1;
                _splitView.Add(_tableView);
                _splitView.Add(_editPanel);
                _bodyRoot.Add(_splitView);
            }
            else
            {
                _bodyRoot.Add(_tableView);
            }

            RefreshTable();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("loc-toolbar");

            // --- Left Group: Selection & Filters ---
            var leftGroup = new VisualElement();
            leftGroup.AddToClassList("loc-toolbar-group");
            leftGroup.AddToClassList("loc-toolbar-group--filters");
            
            var graphLbl = new Label("Graph");
            graphLbl.AddToClassList("loc-toolbar-label");
            leftGroup.Add(graphLbl);

            _graphField = new ObjectField
            {
                objectType = typeof(DialogGraph),
                allowSceneObjects = false,
                value = _selectedGraph
            };
            _graphField.AddToClassList("loc-toolbar-graph-field");
            _graphField.RegisterValueChangedCallback(evt =>
            {
                _selectedGraph = evt.newValue as DialogGraph;
                Reload();
                RebuildLangPills();
                RebuildBody();
            });
            leftGroup.Add(_graphField);

            leftGroup.Add(MakeToolbarSep());

            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("loc-toolbar-search-field");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? string.Empty;
                RefreshTable();
            });
            leftGroup.Add(_searchField);

            var statusLbl = new Label("Status");
            statusLbl.AddToClassList("loc-toolbar-label");
            leftGroup.Add(statusLbl);

            _statusDropdown = new DropdownField(
                new List<string> { "All", "Missing", "Translated" }, 0);
            _statusDropdown.AddToClassList("loc-toolbar-status-field");
            _statusDropdown.RegisterValueChangedCallback(evt =>
            {
                _statusFilter = evt.newValue ?? "All";
                RefreshTable();
            });
            leftGroup.Add(_statusDropdown);

            toolbar.Add(leftGroup);

            // Spacer to push the action group
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            // --- Center-Right Group: Management & Utility ---
            var actionGroup = new VisualElement();
            actionGroup.AddToClassList("loc-toolbar-group");
            actionGroup.AddToClassList("loc-toolbar-group--actions");

            var setupBtn = new Button(OnSetupLocalization) { text = "Setup" };
            setupBtn.AddToClassList("loc-btn");
            setupBtn.AddToClassList("loc-btn--primary");
            actionGroup.Add(setupBtn);

            var addLangBtn = new Button(OnAddLanguage) { text = "+ Language" };
            addLangBtn.AddToClassList("loc-btn");
            actionGroup.Add(addLangBtn);

            var syncRuntimeBtn = new Button(OnUpdateRuntimeLanguages) { text = "Sync" };
            syncRuntimeBtn.AddToClassList("loc-btn");
            actionGroup.Add(syncRuntimeBtn);

            var clearBtn = new Button(OnClearActiveTranslations) { text = "Clear" };
            clearBtn.AddToClassList("loc-btn");
            actionGroup.Add(clearBtn);

            var importBtn = new Button(() => DialogLocalizationIOWindow.Open(_selectedGraph)) { text = "I/O" };
            importBtn.AddToClassList("loc-btn");
            actionGroup.Add(importBtn);

            var translateVisibleBtn = new Button(OnTranslateVisibleLanguagesWithAi) { text = "AI Translate All" };
            translateVisibleBtn.AddToClassList("loc-btn");
            translateVisibleBtn.AddToClassList("loc-btn--ai");
            actionGroup.Add(translateVisibleBtn);

            toolbar.Add(actionGroup);
            
            return toolbar;
        }

        private VisualElement BuildLangBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("loc-lang-bar");

            var lbl = new Label("Languages:");
            lbl.AddToClassList("loc-toolbar-label");
            lbl.style.marginRight = 8;
            bar.Add(lbl);

            _langPillRow = new VisualElement();
            _langPillRow.style.flexDirection = FlexDirection.Row;
            _langPillRow.style.flexWrap = Wrap.Wrap;
            _langPillRow.style.alignItems = Align.Center;
            bar.Add(_langPillRow);

            var noneBtn = new Button(() =>
            {
                _activeLocaleCodes.Clear();
                RebuildLangPills();
                Reload();
                RefreshTable();
            }) { text = "None" };
            noneBtn.AddToClassList("dlg-btn");
            noneBtn.style.marginLeft = 8;
            noneBtn.style.fontSize = 10;
            bar.Add(noneBtn);

            var allBtn = new Button(() =>
            {
                foreach (var c in _availableLocaleCodes)
                {
                    _activeLocaleCodes.Add(c);
                }

                RebuildLangPills();
                Reload();
                RefreshTable();
            }) { text = "All" };
            allBtn.AddToClassList("dlg-btn");
            allBtn.style.marginLeft = 4;
            allBtn.style.fontSize = 10;
            bar.Add(allBtn);

            RebuildLangPills();
            return bar;
        }

        private VisualElement BuildStatusBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("loc-status-bar");

            _statusLabel = new Label(string.Empty);
            _statusLabel.AddToClassList("loc-status-label");
            bar.Add(_statusLabel);

            return bar;
        }

        private void RebuildLangPills()
        {
            if (_langPillRow == null)
            {
                return;
            }

            _langPillRow.Clear();

            if (_availableLocaleCodes.Count == 0)
            {
                var hint = new Label(DialogLocalizationAiBridgeLocator.Current?.IsAvailable == true
                    ? "No target languages yet - use + Language or Translate with AI to create one."
                    : "No target languages - use + Language to add one.");
                hint.AddToClassList("loc-status-label");
                hint.style.marginRight = 8;
                _langPillRow.Add(hint);

                var addBtn = new Button(OnAddLanguage) { text = "Add Languages" };
                addBtn.AddToClassList("loc-btn");
                _langPillRow.Add(addBtn);

                var popularBtn = new Button(AddPopularLanguages) { text = "Add Popular" };
                popularBtn.AddToClassList("loc-btn");
                _langPillRow.Add(popularBtn);

                var aiBtn = new Button(OnTranslateVisibleLanguagesWithAi) { text = "Translate" };
                aiBtn.AddToClassList("loc-btn");
                aiBtn.AddToClassList("loc-btn--ai");
                _langPillRow.Add(aiBtn);
                return;
            }

            foreach (var code in _availableLocaleCodes)
            {
                var capturedCode = code;
                var isActive = _activeLocaleCodes.Contains(code);
                var displayName = LocalizationTableView.FormatLocaleDisplayName(code);

                var pillContainer = new VisualElement();
                pillContainer.AddToClassList("loc-lang-pill-wrap");

                var pill = new Button(() =>
                {
                    if (_activeLocaleCodes.Contains(capturedCode))
                    {
                        _activeLocaleCodes.Remove(capturedCode);
                    }
                    else
                    {
                        _activeLocaleCodes.Add(capturedCode);
                    }

                    RebuildLangPills();
                    Reload();
                    RefreshTable();
                });

                pill.text = displayName;
                pill.AddToClassList("loc-lang-pill");
                pill.EnableInClassList("loc-lang-pill--active", isActive);
                pill.EnableInClassList("loc-lang-pill--inactive", !isActive);
                pill.tooltip = $"{displayName} ({capturedCode})";

                pillContainer.Add(pill);

                var removeBtn = new Button(() => RemoveLanguage(capturedCode))
                {
                    text = "x",
                    tooltip = $"Remove language {displayName}"
                };
                removeBtn.AddToClassList("loc-lang-remove-btn");
                pillContainer.Add(removeBtn);

                _langPillRow.Add(pillContainer);
            }
        }

        private void RefreshTable()
        {
            _filteredRows.Clear();

            foreach (var row in _rows)
            {
                if (!MatchesSearch(row) || !MatchesStatus(row))
                {
                    continue;
                }

                _filteredRows.Add(row);
            }

            if (_tableView == null)
            {
                return;
            }

            var displayCodes = new List<string>();
            if (_sourceTable != null)
            {
                displayCodes.Add(_sourceTable.LocaleCode);
            }

            displayCodes.AddRange(GetOrderedLocaleCodes(fallbackToAvailable: false));
            _tableView.SetData(_filteredRows, displayCodes, _sourceTable?.LocaleCode ?? string.Empty);
        }

        private bool MatchesSearch(LocRow row)
        {
            if (string.IsNullOrEmpty(_filter))
            {
                return true;
            }

            var f = _filter.ToLowerInvariant();
            return (row.Key?.ToLowerInvariant().Contains(f) ?? false) ||
                   (row.SourceText?.ToLowerInvariant().Contains(f) ?? false) ||
                   (row.Context?.ToLowerInvariant().Contains(f) ?? false) ||
                   (row.DisplayName?.ToLowerInvariant().Contains(f) ?? false) ||
                   (row.Speaker?.ToLowerInvariant().Contains(f) ?? false) ||
                   row.Translations.Values.Any(v => v?.ToLowerInvariant().Contains(f) ?? false);
        }

        private bool MatchesStatus(LocRow row)
        {
            if (string.IsNullOrEmpty(_statusFilter) || _statusFilter == "All")
            {
                return true;
            }

            return _statusFilter.Equals(row.Status.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private void OnRowSelected(LocRow row)
        {
            _selectedRow = row;
            if (!_detailsVisible)
            {
                _detailsVisible = true;
                RebuildBody();
            }

            _editPanel?.Show(row, _allTables, _sourceTable, _selectedGraph);
        }

        private void OnAddLanguage()
        {
            AddLanguageDialog.Show(HandleLanguagesCreated);
        }

        private void AddPopularLanguages()
        {
            var localeCodes = new[]
            {
                "fr-FR",
                "de-DE",
                "es-ES",
                "it-IT",
                "pt-BR",
                "ja-JP"
            };

            var createdTables = new List<DialogLocalizationTable>();
            foreach (var localeCode in localeCodes)
            {
                var table = _registry.GetTableForLocale(localeCode) ?? _registry.CreateNewTable(localeCode);
                if (table != null)
                {
                    createdTables.Add(table);
                }
            }

            HandleLanguagesCreated(createdTables);
        }

        private void HandleLanguageCreated(DialogLocalizationTable newTable)
        {
            if (newTable == null || string.IsNullOrWhiteSpace(newTable.LocaleCode))
            {
                return;
            }

            Reload();
            DialogLocalizationRuntimeSettingsService.SyncFromTables(_allTables, _sourceTable?.LocaleCode ?? DialogLocalizationSetupService.DefaultSourceLocaleCode);
            _activeLocaleCodes.Add(newTable.LocaleCode);
            _bulkTranslateLocaleCode = newTable.LocaleCode;
            RebuildLangPills();

            if (_tableView == null)
            {
                RebuildBody();
            }
            else
            {
                RefreshTable();
            }

            SetStatus($"Added language: {LocalizationTableView.FormatLocaleDisplayName(newTable.LocaleCode)}");
            NotifyHomeBrowserLocalizationChanged();
        }

        private void HandleLanguagesCreated(IReadOnlyList<DialogLocalizationTable> newTables)
        {
            if (newTables == null || newTables.Count == 0)
            {
                return;
            }

            Reload();
            DialogLocalizationRuntimeSettingsService.SyncFromTables(_allTables, _sourceTable?.LocaleCode ?? DialogLocalizationSetupService.DefaultSourceLocaleCode);

            foreach (var table in newTables)
            {
                if (table == null || string.IsNullOrWhiteSpace(table.LocaleCode))
                {
                    continue;
                }

                _activeLocaleCodes.Add(table.LocaleCode);
                _bulkTranslateLocaleCode = table.LocaleCode;
            }

            RebuildLangPills();

            if (_tableView == null)
            {
                RebuildBody();
            }
            else
            {
                RefreshTable();
            }

            SetStatus($"Added {newTables.Count} language{(newTables.Count == 1 ? string.Empty : "s")}.");
            NotifyHomeBrowserLocalizationChanged();
        }

        private void OnSetupLocalization()
        {
            if (_selectedGraph == null)
            {
                SetStatus("Select a graph before running setup.", true);
                return;
            }

            var result = DialogLocalizationSetupService.SetupGraph(_selectedGraph);
            Reload();
            DialogLocalizationRuntimeSettingsService.SyncFromTables(_allTables, _sourceTable?.LocaleCode ?? DialogLocalizationSetupService.DefaultSourceLocaleCode);
            RebuildLangPills();
            RebuildBody();
            SetStatus(result.Summary);
            NotifyHomeBrowserLocalizationChanged();
        }

        private void OnUpdateRuntimeLanguages()
        {
            var settings = DialogLocalizationRuntimeSettingsService.SyncFromProjectTables(
                _sourceTable?.LocaleCode ?? DialogLocalizationSetupService.DefaultSourceLocaleCode);
            Reload();
            SetStatus(settings != null
                ? $"Runtime languages updated: {settings.localizationTables.Count} table(s) available in play mode."
                : "Could not update runtime languages.",
                settings == null);
        }

        private void OnTranslateVisibleLanguagesWithAi()
        {
            if (_selectedGraph == null)
            {
                SetStatus("Select a graph before translating.", true);
                return;
            }

            if (!TryValidateUniqueRowKeys("AI translation"))
            {
                return;
            }

            var localeCodes = GetOrderedLocaleCodes(fallbackToAvailable: true);

            if (localeCodes.Count == 0)
            {
                SetStatus("Add or activate at least one target language before translating.", true);
                return;
            }

            AiTranslationChoiceDialog.Show(overwrite =>
            {
                _shouldOverwriteExisting = overwrite;
                ExecuteTranslationWorkflow(localeCodes);
            });
        }

        private void ExecuteTranslationWorkflow(List<string> localeCodes)
        {
            if (!DialogLocalizationAiBridgeLocator.IsAvailable)
            {
                DialogLocalizationAiBridgeLocator.ShowUnavailableMessage();
                return;
            }

            foreach (var localeCode in localeCodes)
            {
                if (EnsureTargetTable(localeCode) == null)
                {
                    SetStatus($"Could not create localization table for '{localeCode}'.", true);
                    return;
                }
            }

            TranslateGraphToTable(_registry.GetTableForLocale(localeCodes[0]), localeCodes.Skip(1).ToList());
        }

        private void OnTranslateRowRequested(LocRow row)
        {
            if (row == null)
            {
                return;
            }

            if (!TryValidateUniqueRowKeys("Row translation"))
            {
                return;
            }

            if (!DialogLocalizationAiBridgeLocator.IsAvailable)
            {
                DialogLocalizationAiBridgeLocator.ShowUnavailableMessage();
                return;
            }

            var localeCodes = GetOrderedLocaleCodes(fallbackToAvailable: true);

            if (localeCodes.Count == 0)
            {
                SetStatus("Add or activate at least one target language before translating.", true);
                return;
            }

            TranslateRowAcrossLocales(row, localeCodes);
        }

        private void TranslateGraphToTable(
            DialogLocalizationTable targetTable,
            IReadOnlyList<string> queuedLocaleCodes = null)
        {
            if (targetTable == null)
            {
                SetStatus("No target localization table is available.", true);
                return;
            }

            var bridge = DialogLocalizationAiBridgeLocator.Current;
            var targetLocaleCode = targetTable.LocaleCode;
            var entries = _rows
                .Where(row =>
                {
                    if (_shouldOverwriteExisting) return true;
                    var savedTranslation = targetTable.TryResolve(row.Key);
                    return string.IsNullOrWhiteSpace(savedTranslation);
                })
                .Select(row => new DialogLocalizationAiRequestEntry
{
                    Key = row.Key,
                    SourceText = row.SourceText,
                    Context = $"{row.DisplayName}: {row.Context}"
                })
                .ToList();

            if (entries.Count == 0)
            {
                if (queuedLocaleCodes != null && queuedLocaleCodes.Count > 0)
                {
                    TranslateNextQueuedLocale(queuedLocaleCodes);
                    return;
                }

                SetStatus("No missing entries remain for the selected AI target language.");
                return;
            }

            if (!bridge.TryTranslate(new DialogLocalizationAiRequest
                {
                    Graph = _selectedGraph,
                    TargetTable = targetTable,
                    TargetLocaleCode = targetLocaleCode,
                    ScopeLabel = "Current Graph",
                    Entries = entries,
                    OnCompleted = message =>
                    {
                        Reload();
                        RefreshTable();

                        if (queuedLocaleCodes != null && queuedLocaleCodes.Count > 0)
                        {
                            TranslateNextQueuedLocale(queuedLocaleCodes);
                            return;
                        }

                        SetStatus(message);
                        NotifyHomeBrowserLocalizationChanged();
                    },
                    OnCancelled = () => SetStatus("AI translation cancelled.")
                },
                out var error))
            {
                SetStatus(error, true);
            }
        }

        private void TranslateNextQueuedLocale(IReadOnlyList<string> queuedLocaleCodes)
        {
            if (queuedLocaleCodes == null || queuedLocaleCodes.Count == 0)
            {
                Reload();
                RefreshTable();
                SetStatus("Finished translating visible languages.");
                return;
            }

            var nextLocaleCode = queuedLocaleCodes[0];
            var nextTable = EnsureTargetTable(nextLocaleCode);
            if (nextTable == null)
            {
                SetStatus($"Could not create localization table for '{nextLocaleCode}'.", true);
                return;
            }

            TranslateGraphToTable(nextTable, queuedLocaleCodes.Skip(1).ToList());
        }

        private void TranslateRowAcrossLocales(LocRow row, IReadOnlyList<string> localeCodes)
        {
            if (row == null || localeCodes == null || localeCodes.Count == 0)
            {
                return;
            }

            var localeCode = localeCodes[0];
            var table = EnsureTargetTable(localeCode);
            if (table == null)
            {
                SetStatus($"Could not create localization table for '{localeCode}'.", true);
                return;
            }

            var bridge = DialogLocalizationAiBridgeLocator.Current;
            if (!bridge.TryTranslate(new DialogLocalizationAiRequest
                {
                    Graph = _selectedGraph,
                    TargetTable = table,
                    TargetLocaleCode = localeCode,
                    ScopeLabel = "Selected Row",
                    Entries = new[]
                    {
                        new DialogLocalizationAiRequestEntry
                        {
                            Key = row.Key,
                            SourceText = row.SourceText,
                            Context = $"{row.DisplayName}: {row.Context}"
                        }
                    },
                    OnCompleted = _ =>
                    {
                        row.Translations[localeCode] = table.TryResolve(row.Key) ?? string.Empty;
                        row.Status = GetOverallStatus(row);
                        RefreshTable();
                        NotifyHomeBrowserLocalizationChanged();
                        TranslateRowAcrossLocales(row, localeCodes.Skip(1).ToList());
                    },
                    OnCancelled = () => SetStatus("AI translation cancelled.")
                },
                out var error))
            {
                SetStatus(error, true);
            }
        }

        private DialogLocalizationTable EnsureTargetTable(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return null;
            }

            var targetTable = _registry.GetTableForLocale(localeCode);
            if (targetTable != null)
            {
                DialogLocalizationRuntimeSettingsService.SyncFromProjectTables(_sourceTable?.LocaleCode ?? DialogLocalizationSetupService.DefaultSourceLocaleCode);
                if (_activeLocaleCodes.Add(localeCode))
                {
                    Reload();
                    RebuildLangPills();
                    RefreshTable();
                }

                return targetTable;
            }

            targetTable = _registry.CreateNewTable(localeCode);
            if (targetTable != null)
            {
                DialogLocalizationRuntimeSettingsService.SyncFromProjectTables(_sourceTable?.LocaleCode ?? DialogLocalizationSetupService.DefaultSourceLocaleCode);
                HandleLanguageCreated(targetTable);
            }

            return targetTable;
        }

        private void OnTranslationEdited(LocRow row, string localeCode, string newValue)
        {
            if (row == null || string.IsNullOrWhiteSpace(localeCode))
            {
                return;
            }

            if (!TryValidateUniqueRowKeys("Manual translation edit"))
            {
                return;
            }

            var table = EnsureTargetTable(localeCode);
            if (table == null)
            {
                SetStatus($"Could not create localization table for '{localeCode}'.", true);
                return;
            }

            var value = newValue ?? string.Empty;
            row.Translations[localeCode] = value;
            table.SetEntry(row.Key, value);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            row.Status = GetOverallStatus(row);
            RefreshTable();
            NotifyHomeBrowserLocalizationChanged();
        }

        private void OnUseSourceRequested(LocRow row)
        {
            if (row == null)
            {
                return;
            }

            if (!TryValidateUniqueRowKeys("Use Source"))
            {
                return;
            }

            var localeCodes = GetOrderedLocaleCodes(fallbackToAvailable: true);

            if (localeCodes.Count == 0)
            {
                SetStatus("Add or activate at least one target language before using source text.", true);
                return;
            }

            foreach (var localeCode in localeCodes)
            {
                var table = EnsureTargetTable(localeCode);
                if (table == null)
                {
                    continue;
                }

                var sourceText = row.SourceText ?? string.Empty;
                row.Translations[localeCode] = sourceText;
                table.SetEntry(row.Key, sourceText);
                EditorUtility.SetDirty(table);
            }

            AssetDatabase.SaveAssets();
            row.Status = GetOverallStatus(row);
            RefreshTable();
            SetStatus($"Copied source text to {localeCodes.Count} active language{(localeCodes.Count == 1 ? string.Empty : "s")} for the selected row.");
            NotifyHomeBrowserLocalizationChanged();
        }

        private void OnClearActiveTranslations()
        {
            if (_activeLocaleCodes.Count == 0)
            {
                SetStatus("No languages are currently active to clear.", true);
                return;
            }

            if (!TryValidateUniqueRowKeys("Clear translations"))
            {
                return;
            }

            var confirmed = EditorUtility.DisplayDialog(
                "Clear Translations",
                $"Are you sure you want to clear all translations for the {_activeLocaleCodes.Count} active language(s) in the current graph?\n\nThis will set them to empty strings. Source text will remain untouched.",
                "Clear All",
                "Cancel");

            if (!confirmed) return;

            var tablesToClear = GetOrderedLocaleCodes(fallbackToAvailable: false)
                .Select(code => _registry.GetTableForLocale(code))
                .Where(t => t != null)
                .ToList();

            if (tablesToClear.Count == 0) return;

            foreach (var row in _rows)
            {
                foreach (var table in tablesToClear)
                {
                    table.RemoveEntry(row.Key);
                }
            }

            foreach (var table in tablesToClear)
            {
                EditorUtility.SetDirty(table);
            }

            AssetDatabase.SaveAssets();
            Reload();
            RefreshTable();
            SetStatus($"Cleared translations for {_activeLocaleCodes.Count} active language(s).");
            NotifyHomeBrowserLocalizationChanged();
        }

        private void RebuildBody()
        {
            if (_bodyRoot == null)
            {
                return;
            }

            if (_selectedGraph == null)
            {
                _bodyRoot.Clear();
                ShowNoGraphHelp();
                return;
            }

            BuildSplitView();
        }

        private void RemoveLanguage(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return;
            }

            var table = _registry.GetTableForLocale(localeCode);
            if (table == null)
            {
                SetStatus($"Language '{localeCode}' could not be found.", true);
                return;
            }

            var displayName = LocalizationTableView.FormatLocaleDisplayName(localeCode);
            var confirmed = EditorUtility.DisplayDialog(
                "Remove Language",
                $"Delete the localization table for {displayName} ({localeCode})?\n\nThis removes the language from the project and cannot be undone.",
                "Remove",
                "Cancel");

            if (!confirmed)
            {
                return;
            }

            if (!_registry.DeleteTable(table, out var error))
            {
                SetStatus(error, true);
                return;
            }

            _activeLocaleCodes.Remove(localeCode);
            if (string.Equals(_bulkTranslateLocaleCode, localeCode, StringComparison.OrdinalIgnoreCase))
            {
                _bulkTranslateLocaleCode = string.Empty;
            }

            Reload();
            DialogLocalizationRuntimeSettingsService.SyncFromProjectTables(
                _sourceTable?.LocaleCode ?? DialogLocalizationSetupService.DefaultSourceLocaleCode);
            RebuildLangPills();
            RebuildBody();
            SetStatus($"Removed language: {displayName} ({localeCode}).");
            NotifyHomeBrowserLocalizationChanged();
        }

        private List<string> GetOrderedLocaleCodes(bool fallbackToAvailable)
        {
            var ordered = _availableLocaleCodes
                .Where(code => _activeLocaleCodes.Contains(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ordered.Count == 0 && fallbackToAvailable)
            {
                ordered = _availableLocaleCodes
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return ordered;
        }

        private bool TryValidateUniqueRowKeys(string operationLabel)
        {
            var duplicateKeys = DialogLocalizationRowBuilder.FindDuplicateKeys(_selectedGraph);
            if (duplicateKeys.Count == 0)
            {
                return true;
            }

            var preview = string.Join(", ", duplicateKeys.Take(3));
            var suffix = duplicateKeys.Count > 3
                ? $" and {duplicateKeys.Count - 3} more"
                : string.Empty;

            SetStatus(
                $"{operationLabel} blocked: duplicate localization keys found in this graph ({preview}{suffix}). Run graph validation and assign unique locale keys before applying translations.",
                true);
            return false;
        }

        private void HideDetailsPanel()
        {
            if (!_detailsVisible)
            {
                return;
            }

            _detailsVisible = false;
            _editPanel?.Hide();
            RebuildBody();
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (_statusLabel == null)
            {
                return;
            }

            _statusLabel.text = message ?? string.Empty;
            _statusLabel.EnableInClassList("loc-status-label--error", isError);
        }

        private static VisualElement MakeToolbarSep()
        {
            var sep = new VisualElement();
            sep.AddToClassList("loc-toolbar-sep");
            return sep;
        }

        private string ResolveDefaultTargetLocaleCode()
        {
            return _availableLocaleCodes.FirstOrDefault() ?? "fr-FR";
        }

        private static OverallStatus GetOverallStatus(LocRow row)
        {
            if (row == null || row.Translations == null || row.Translations.Count == 0)
            {
                return OverallStatus.Translated;
            }

            foreach (var translation in row.Translations.Values)
            {
                if (string.IsNullOrWhiteSpace(translation))
                {
                    return OverallStatus.Missing;
                }
            }

            return OverallStatus.Translated;
        }

        private static void NotifyHomeBrowserLocalizationChanged()
        {
            DialogSystemMainWindow.NotifyHomeBrowserDataChanged();
        }
    }
}
