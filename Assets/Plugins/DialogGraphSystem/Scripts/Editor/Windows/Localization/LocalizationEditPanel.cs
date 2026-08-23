using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Right-side edit panel in the localization manager.
    /// </summary>
    public class LocalizationEditPanel : VisualElement
    {
        public event Action<LocRow> OnEntryChanged;
        public event Action<LocRow> OnEntryDeleted;
        public event Action OnCloseRequested;

        private LocRow _currentRow;
        private IReadOnlyList<DialogLocalizationTable> _allTables;
        private DialogLocalizationTable _sourceTable;
        private DialogGraph _currentGraph;

        private readonly ScrollView _scrollView;
        private readonly VisualElement _content;

        public LocalizationEditPanel()
        {
            AddToClassList("loc-edit-panel");
            style.display = DisplayStyle.None;
            style.flexGrow = 1;

            var header = new VisualElement();
            header.AddToClassList("loc-edit-header");

            var titleLabel = new Label("Edit Entry");
            header.Add(titleLabel);

            var closeBtn = new Button(() => OnCloseRequested?.Invoke()) { text = "x" };
            closeBtn.AddToClassList("loc-edit-close-btn");
            header.Add(closeBtn);

            Add(header);

            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.AddToClassList("loc-edit-scroll");
            _scrollView.style.flexGrow = 1;
            _scrollView.style.minHeight = 0;
            _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Add(_scrollView);

            _content = new VisualElement();
            _content.AddToClassList("loc-edit-content");
            _content.style.flexGrow = 1;
            _scrollView.Add(_content);
        }

        public void Show(
            LocRow row,
            IReadOnlyList<DialogLocalizationTable> allTables,
            DialogLocalizationTable sourceTable,
            DialogGraph graph = null)
        {
            _currentRow = row;
            _allTables = allTables;
            _sourceTable = sourceTable;
            _currentGraph = graph;

            RebuildContent();
            style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
        }

        private void RebuildContent()
        {
            _content.Clear();

            if (_currentRow == null)
            {
                return;
            }

            AddMetaRow("GRAPH", string.IsNullOrEmpty(_currentRow.GraphTitle) ? "-" : _currentRow.GraphTitle);
            AddMetaRow("FIELD", string.IsNullOrEmpty(_currentRow.DisplayName) ? "-" : _currentRow.DisplayName);
            AddMetaRow("CONTEXT", string.IsNullOrEmpty(_currentRow.Context) ? "-" : _currentRow.Context);
            AddMetaRow("SPEAKER", string.IsNullOrEmpty(_currentRow.Speaker) ? "-" : _currentRow.Speaker);

            var sourceBlock = new VisualElement();
            sourceBlock.AddToClassList("loc-source-block");

            var sourceLocaleLabel = new Label(FormatSourceLabel());
            sourceLocaleLabel.AddToClassList("loc-source-label");
            var sourceHeader = new VisualElement();
            sourceHeader.style.flexDirection = FlexDirection.Row;
            sourceHeader.style.justifyContent = Justify.SpaceBetween;
            sourceHeader.style.alignItems = Align.Center;
            sourceHeader.Add(sourceLocaleLabel);

            var copySourceBtn = new Button(CopySourceToClipboard) { text = "Copy Source" };
            copySourceBtn.AddToClassList("loc-btn");
            sourceHeader.Add(copySourceBtn);
sourceBlock.Add(sourceHeader);

            var sourceTextField = new TextField
            {
                value = _currentRow.SourceText ?? string.Empty,
                isReadOnly = true,
                multiline = true
            };
            sourceTextField.style.whiteSpace = WhiteSpace.Normal;
            sourceBlock.Add(sourceTextField);

            _content.Add(sourceBlock);

            foreach (var table in _allTables ?? Array.Empty<DialogLocalizationTable>())
            {
                if (table == null || table.IsSourceLanguage)
                {
                    continue;
                }

                AddLanguageBlock(table);
            }

            var deleteBtn = new Button(OnDeleteClicked) { text = "Delete Entry" };
            deleteBtn.AddToClassList("loc-delete-btn");
            _content.Add(deleteBtn);
        }

        private void AddMetaRow(string label, string value)
        {
            var metaRow = new VisualElement();
            metaRow.style.marginBottom = 6;

            var metaLabel = new Label(label);
            metaLabel.AddToClassList("loc-edit-field-label");
            metaRow.Add(metaLabel);

            var metaValue = new Label(value);
            metaValue.AddToClassList("loc-edit-meta-value");
            metaRow.Add(metaValue);

            _content.Add(metaRow);
        }

        private void AddLanguageBlock(DialogLocalizationTable table)
        {
            var localeCode = table.LocaleCode;
            var displayName = LocalizationTableView.FormatLocaleDisplayName(localeCode);
            var currentValue = _currentRow.Translations.TryGetValue(localeCode, out var tv)
                ? (tv ?? string.Empty)
                : string.Empty;

            var block = new VisualElement();
            block.AddToClassList("loc-lang-block");

            var rowHeader = new VisualElement();
            rowHeader.AddToClassList("loc-lang-row-header");

            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("loc-lang-name");
            rowHeader.Add(nameLabel);

            var rightGroup = new VisualElement();
            rightGroup.style.flexDirection = FlexDirection.Row;
            rightGroup.style.alignItems = Align.Center;

            var charCount = new Label($"{currentValue.Length} chars");
            charCount.AddToClassList("loc-char-count");
            charCount.style.marginRight = 6;
            rightGroup.Add(charCount);

            var status = GetEntryStatus(currentValue);
            var statusBadge = new Label(StatusText(status));
            statusBadge.AddToClassList("loc-status-badge");
            ApplyStatusClass(statusBadge, status);
            rightGroup.Add(statusBadge);

            var copyBtn = new Button(() => CopySourceToClipboard())
            {
                text = "Copy Source"
            };
            copyBtn.AddToClassList("loc-btn");
            copyBtn.style.marginLeft = 8;
            rightGroup.Add(copyBtn);

            var pasteSourceBtn = new Button(() => ApplySourceTextToLanguage(table))
            {
                text = "Use Source"
            };
            pasteSourceBtn.AddToClassList("loc-btn");
            pasteSourceBtn.style.marginLeft = 4;
            rightGroup.Add(pasteSourceBtn);

            var aiBtn = new Button(() => OnTranslateEntryWithAi(table))
            {
                text = "AI"
            };
            aiBtn.AddToClassList("loc-btn");
            aiBtn.AddToClassList("loc-btn--ai");
            aiBtn.style.marginLeft = 4;
            rightGroup.Add(aiBtn);

            rowHeader.Add(rightGroup);
            block.Add(rowHeader);

            var field = new TextField { value = currentValue, multiline = true, isDelayed = true };
            field.style.whiteSpace = WhiteSpace.Normal;
            field.style.minHeight = 60;
            field.RegisterValueChangedCallback(evt =>
            {
                if (!CanApplyChanges("Manual translation edit"))
                {
                    field.SetValueWithoutNotify(_currentRow.Translations.TryGetValue(localeCode, out var existingValue)
                        ? existingValue ?? string.Empty
                        : string.Empty);
                    return;
                }

                var newValue = evt.newValue ?? string.Empty;
                _currentRow.Translations[localeCode] = newValue;

                table.SetEntry(_currentRow.Key, newValue);
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssets();

                charCount.text = $"{newValue.Length} chars";
                var newStatus = GetEntryStatus(newValue);
                statusBadge.text = StatusText(newStatus);
                ApplyStatusClass(statusBadge, newStatus);

                OnEntryChanged?.Invoke(_currentRow);
            });

            block.Add(field);
            _content.Add(block);
        }

        private void OnTranslateEntryWithAi(DialogLocalizationTable table)
        {
            if (!DialogLocalizationAiBridgeLocator.IsAvailable)
            {
                DialogLocalizationAiBridgeLocator.ShowUnavailableMessage();
                return;
            }

            var bridge = DialogLocalizationAiBridgeLocator.Current;
            if (_currentRow == null || table == null || _currentGraph == null)
            {
                return;
            }

            if (!CanApplyChanges("AI translation"))
            {
                return;
            }

            if (!bridge.TryTranslate(new DialogLocalizationAiRequest
                {
                    Graph = _currentGraph,
                    TargetTable = table,
                    TargetLocaleCode = table.LocaleCode,
                    ScopeLabel = "Selected Entry",
                    Entries = new[]
                    {
                        new DialogLocalizationAiRequestEntry
                        {
                            Key = _currentRow.Key,
                            SourceText = _currentRow.SourceText,
                            Context = $"{_currentRow.DisplayName}: {_currentRow.Context}"
                        }
                    },
                    OnCompleted = _ =>
                    {
                        _currentRow.Translations[table.LocaleCode] = table.TryResolve(_currentRow.Key) ?? string.Empty;
                        RebuildContent();
                        OnEntryChanged?.Invoke(_currentRow);
                    },
                    OnCancelled = null
                },
                out var error))
            {
                Debug.LogWarning($"[LocalizationEditPanel] {error}");
            }
        }

        private void CopySourceToClipboard()
        {
            EditorGUIUtility.systemCopyBuffer = _currentRow?.SourceText ?? string.Empty;
        }

        private void ApplySourceTextToLanguage(DialogLocalizationTable table)
        {
            if (_currentRow == null || table == null)
            {
                return;
            }

            if (!CanApplyChanges("Use Source"))
            {
                return;
            }

            var sourceText = _currentRow.SourceText ?? string.Empty;
            _currentRow.Translations[table.LocaleCode] = sourceText;
            table.SetEntry(_currentRow.Key, sourceText);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            RebuildContent();
            OnEntryChanged?.Invoke(_currentRow);
        }

        private bool CanApplyChanges(string operationLabel)
        {
            if (_currentGraph == null)
            {
                return true;
            }

            var duplicateKeys = DialogLocalizationRowBuilder.FindDuplicateKeys(_currentGraph);
            if (duplicateKeys.Count == 0)
            {
                return true;
            }

            var preview = string.Join(", ", duplicateKeys.Take(3));
            var suffix = duplicateKeys.Count > 3
                ? $" and {duplicateKeys.Count - 3} more"
                : string.Empty;

            EditorUtility.DisplayDialog(
                "Duplicate Localization Keys",
                $"{operationLabel} is blocked because this graph has duplicate localization keys.\n\n{preview}{suffix}\n\nRun graph validation and assign unique locale keys before applying translations.",
                "OK");
            return false;
        }

        private void AddFieldLabel(string text)
        {
            var lbl = new Label(text);
            lbl.AddToClassList("loc-edit-field-label");
            _content.Add(lbl);
        }

        private string FormatSourceLabel()
        {
            if (_sourceTable == null)
            {
                return "SOURCE";
            }

            var display = LocalizationTableView.FormatLocaleDisplayName(_sourceTable.LocaleCode);
            return $"{display} (SOURCE)";
        }

        private static OverallStatus GetEntryStatus(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? OverallStatus.Missing : OverallStatus.Translated;
        }

        private static string StatusText(OverallStatus s) => s switch
        {
            OverallStatus.Missing => "MISSING",
            OverallStatus.Translated => "TRANSLATED",
            _ => string.Empty
        };

        private static void ApplyStatusClass(Label badge, OverallStatus status)
        {
            badge.EnableInClassList("loc-status--missing", status == OverallStatus.Missing);
            badge.EnableInClassList("loc-status--translated", status == OverallStatus.Translated);
        }

        private void OnDeleteClicked()
        {
            if (_currentRow == null)
            {
                return;
            }

            var confirmed = EditorUtility.DisplayDialog(
                "Delete Entry",
                $"Remove key '{_currentRow.Key}' from all tables?\nThis action cannot be undone.",
                "Delete",
                "Cancel");

            if (!confirmed)
            {
                return;
            }

            foreach (var table in _allTables ?? Array.Empty<DialogLocalizationTable>())
            {
                if (table == null)
                {
                    continue;
                }

                if (table.RemoveEntry(_currentRow.Key))
                {
                    EditorUtility.SetDirty(table);
                }
            }

            AssetDatabase.SaveAssets();

            var deleted = _currentRow;
            Hide();
            OnEntryDeleted?.Invoke(deleted);
        }
    }
}
