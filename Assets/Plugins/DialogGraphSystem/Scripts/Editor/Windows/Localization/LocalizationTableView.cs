using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Spreadsheet-style editor for localization rows. Source text and target
    /// translations are edited directly in the table.
    /// </summary>
    public class LocalizationTableView : VisualElement
    {
        private const float MinContextWidth = 180f;
        private const float MaxContextWidth = 240f;
        private const float ActionsWidth = 120f;
        private const float MinSourceWidth = 300f;
        private const float MaxSourceWidth = 520f;
        private const float MinLangWidth = 220f;
        private const float MaxLangWidth = 320f;
        private const float StatusWidth = 88f;
        private const float RowHeight = 116f;
        private const float CellTextHeight = 92f;
        private const float TablePaddingWidth = 18f;

        private IReadOnlyList<LocRow> _rows;
        private IReadOnlyList<string> _localeCodes;
        private string _sourceLocale;

        private readonly VisualElement _viewport;
        private readonly VisualElement _tableRoot;
        private readonly VisualElement _header;
        private readonly ListView _listView;
        private readonly Scroller _horizontalScroller;

        private float _contextWidth = MinContextWidth;
        private float _sourceWidth = MinSourceWidth;
        private float _langWidth = MinLangWidth;
        private float _tableWidth;
        private float _horizontalOffset;

        public event Action<LocRow> OnRowSelected;
        public event Action<LocRow> OnTranslateRowRequested;
        public event Action<LocRow> OnUseSourceRequested;
        public event Action<LocRow, string, string> OnTranslationEdited;

        public LocalizationTableView()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            style.overflow = Overflow.Hidden;

            _viewport = new VisualElement();
            _viewport.style.flexGrow = 1;
            _viewport.style.overflow = Overflow.Hidden;
            Add(_viewport);

            _tableRoot = new VisualElement();
            _tableRoot.style.flexDirection = FlexDirection.Column;
            _tableRoot.style.flexGrow = 1;
            _tableRoot.style.minHeight = 0;
            _viewport.Add(_tableRoot);

            _header = new VisualElement();
            _header.AddToClassList("loc-table-header");
            _header.style.overflow = Overflow.Hidden;
            _tableRoot.Add(_header);

            _listView = new ListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single
            };
            _listView.style.flexGrow = 1;
            _listView.makeItem = MakeRow;
            _listView.bindItem = BindRow;
#if UNITY_2022_2_OR_NEWER
            _listView.selectionChanged += OnSelectionChanged;
#else
            _listView.onSelectionChange += OnSelectionChanged;
#endif
            _tableRoot.Add(_listView);

            _horizontalScroller = new Scroller(0f, 0f, OnHorizontalScrollChanged, SliderDirection.Horizontal);
            _horizontalScroller.style.height = 18;
            _horizontalScroller.style.minHeight = 18;
            _horizontalScroller.style.marginLeft = 4;
            _horizontalScroller.style.marginRight = 4;
            Add(_horizontalScroller);

            _viewport.RegisterCallback<WheelEvent>(OnViewportWheel);
            RegisterCallback<GeometryChangedEvent>(_ => UpdateResponsiveWidths(rebuildRows: true));
        }

        public void SetData(IReadOnlyList<LocRow> rows, IReadOnlyList<string> localeCodes, string sourceLocale)
        {
            _rows = rows ?? Array.Empty<LocRow>();
            _localeCodes = localeCodes ?? Array.Empty<string>();
            _sourceLocale = sourceLocale ?? string.Empty;

            UpdateResponsiveWidths(rebuildRows: false);
            RebuildHeader();
            Refresh();
        }

        public void Refresh()
        {
            _listView.itemsSource = (System.Collections.IList)_rows;
            _listView.Rebuild();
        }

        private void RebuildHeader()
        {
            _header.Clear();
            _header.Add(MakeHeaderCell("CONTEXT", _contextWidth, "loc-col-context"));
            _header.Add(MakeHeaderCell("ACTIONS", ActionsWidth, "loc-col-actions"));
            _header.Add(MakeHeaderCell($"{FormatLocaleDisplayName(_sourceLocale)} (SOURCE)", _sourceWidth, "loc-col-source"));

            foreach (var code in _localeCodes ?? Array.Empty<string>())
            {
                if (string.Equals(code, _sourceLocale, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _header.Add(MakeHeaderCell(FormatLocaleDisplayName(code), _langWidth, "loc-col-lang"));
            }

            _header.Add(MakeHeaderCell("STATUS", StatusWidth, "loc-col-status"));
        }

        private void UpdateResponsiveWidths(bool rebuildRows)
        {
            var targetLanguageCount = 0;
            foreach (var code in _localeCodes ?? Array.Empty<string>())
            {
                if (!string.Equals(code, _sourceLocale, StringComparison.OrdinalIgnoreCase))
                {
                    targetLanguageCount++;
                }
            }

            var availableWidth = Mathf.Max(0f, resolvedStyle.width);
            var minWidth = MinContextWidth + ActionsWidth + MinSourceWidth + (targetLanguageCount * MinLangWidth) + StatusWidth + TablePaddingWidth;

            _contextWidth = MinContextWidth;
            _sourceWidth = MinSourceWidth;
            _langWidth = MinLangWidth;

            if (availableWidth > minWidth)
            {
                var extra = availableWidth - minWidth;
                var contextExtra = Mathf.Min(MaxContextWidth - MinContextWidth, extra * 0.12f);
                _contextWidth += contextExtra;
                extra -= contextExtra;

                var sourceExtra = Mathf.Min(MaxSourceWidth - MinSourceWidth, extra * 0.45f);
                _sourceWidth += sourceExtra;
                extra -= sourceExtra;

                if (targetLanguageCount > 0)
                {
                    var langExtraEach = Mathf.Min(MaxLangWidth - MinLangWidth, extra / targetLanguageCount);
                    _langWidth += langExtraEach;
                }
                else
                {
                    _sourceWidth = Mathf.Min(MaxSourceWidth, _sourceWidth + extra);
                }
            }

            var newTableWidth = _contextWidth + ActionsWidth + _sourceWidth + (targetLanguageCount * _langWidth) + StatusWidth + TablePaddingWidth;
            if (availableWidth > 0f)
            {
                newTableWidth = Mathf.Max(newTableWidth, availableWidth);
            }

            var changed = Mathf.Abs(newTableWidth - _tableWidth) > 0.5f;
            _tableWidth = newTableWidth;
            _tableRoot.style.width = _tableWidth;
            _tableRoot.style.minWidth = _tableWidth;
            _header.style.width = _tableWidth;
            _listView.style.width = _tableWidth;
            UpdateHorizontalScroller(availableWidth);

            if (changed)
            {
                RebuildHeader();
                if (rebuildRows)
                {
                    _listView.Rebuild();
                }
            }
        }

        private static VisualElement MakeHeaderCell(string text, float width, string ussClass)
        {
            var label = new Label(text);
            label.AddToClassList(ussClass);
            label.style.width = width;
            label.style.minWidth = width;
            label.style.maxWidth = width;
            return label;
        }

        private void UpdateHorizontalScroller(float availableWidth)
        {
            var maxOffset = Mathf.Max(0f, _tableWidth - availableWidth);
            _horizontalScroller.lowValue = 0f;
            _horizontalScroller.highValue = maxOffset;
            _horizontalScroller.SetEnabled(maxOffset > 0.5f);

            if (_horizontalOffset > maxOffset)
            {
                _horizontalOffset = maxOffset;
            }

            _horizontalScroller.value = _horizontalOffset;
            ApplyHorizontalOffset();
        }

        private void OnHorizontalScrollChanged(float value)
        {
            _horizontalOffset = Mathf.Max(0f, value);
            ApplyHorizontalOffset();
        }

        private void ApplyHorizontalOffset()
        {
            _tableRoot.style.left = -_horizontalOffset;
        }

        private void OnViewportWheel(WheelEvent evt)
        {
            if (!evt.shiftKey && Mathf.Abs(evt.delta.x) < 0.01f)
            {
                return;
            }

            var delta = Mathf.Abs(evt.delta.x) > 0.01f ? evt.delta.x : evt.delta.y;
            var maxOffset = Mathf.Max(0f, _tableWidth - Mathf.Max(0f, resolvedStyle.width));
            _horizontalOffset = Mathf.Clamp(_horizontalOffset + delta * 28f, 0f, maxOffset);
            _horizontalScroller.value = _horizontalOffset;
            ApplyHorizontalOffset();
            evt.StopPropagation();
        }

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("loc-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.height = RowHeight;
            row.style.minHeight = RowHeight;
            row.style.maxHeight = RowHeight;

            row.Add(MakeContextCell());
            row.Add(MakeActionsCell());
            row.Add(MakeSourceCell());

            var langContainer = new VisualElement();
            langContainer.name = "lang-container";
            langContainer.style.flexDirection = FlexDirection.Row;
            row.Add(langContainer);

            var statusBadge = new Label();
            statusBadge.name = "status-badge";
            statusBadge.AddToClassList("loc-col-status");
            statusBadge.AddToClassList("loc-status-badge");
            statusBadge.style.alignSelf = Align.Center;
            row.Add(statusBadge);

            return row;
        }

        private static VisualElement MakeContextCell()
        {
            var container = new VisualElement();
            container.name = "context-container";
            container.AddToClassList("loc-col-context");
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;

            var title = new Label();
            title.name = "context-title";
            title.AddToClassList("loc-context-title");
            container.Add(title);

            var subtitle = new Label();
            subtitle.name = "context-subtitle";
            subtitle.AddToClassList("loc-context-subtitle");
            container.Add(subtitle);

            return container;
        }

        private static VisualElement MakeActionsCell()
        {
            var container = new VisualElement();
            container.name = "actions-container";
            SetFixedWidth(container, ActionsWidth);
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            container.style.justifyContent = Justify.Center;

            var copyBtn = new Button { text = "Copy" };
            copyBtn.name = "copy-btn";
            copyBtn.AddToClassList("loc-btn");
            copyBtn.style.marginBottom = 4;
            container.Add(copyBtn);

            var useSourceBtn = new Button { text = "Use Source" };
            useSourceBtn.name = "use-source-btn";
            useSourceBtn.AddToClassList("loc-btn");
            useSourceBtn.style.marginBottom = 4;
            container.Add(useSourceBtn);

            var aiBtn = new Button { text = "AI" };
            aiBtn.name = "ai-btn";
            aiBtn.AddToClassList("loc-btn");
            aiBtn.AddToClassList("loc-btn--ai");
            container.Add(aiBtn);

            return container;
        }

        private static VisualElement MakeSourceCell()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "source-scroll";
            scroll.AddToClassList("loc-col-source");
            scroll.style.height = CellTextHeight;
            scroll.style.minHeight = CellTextHeight;
            scroll.style.maxHeight = CellTextHeight;
            scroll.style.marginTop = 10;
            scroll.style.marginBottom = 10;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            var field = new TextField
            {
                isReadOnly = true,
                multiline = true
            };
            field.name = "source";
            field.style.flexGrow = 1;
            field.style.minHeight = CellTextHeight;
            field.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(field);
            return scroll;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (_rows == null || index < 0 || index >= _rows.Count)
            {
                return;
            }

            var row = _rows[index];
            element.userData = row;
            element.EnableInClassList("loc-row--even", index % 2 == 0);
            element.EnableInClassList("loc-row--odd", index % 2 != 0);

            SetFixedWidth(element.Q<VisualElement>("context-container"), _contextWidth);
            SetFixedWidth(element.Q<VisualElement>("actions-container"), ActionsWidth);
            SetFixedWidth(element.Q<VisualElement>("source-scroll"), _sourceWidth);

            var contextTitle = element.Q<Label>("context-title");
            if (contextTitle != null)
            {
                contextTitle.text = string.IsNullOrWhiteSpace(row.DisplayName)
                    ? (row.Speaker ?? "Entry")
                    : row.DisplayName;
            }

            var contextSubtitle = element.Q<Label>("context-subtitle");
            if (contextSubtitle != null)
            {
                var speakerText = string.IsNullOrWhiteSpace(row.Speaker) ? string.Empty : $"{row.Speaker} | ";
                contextSubtitle.text = $"{speakerText}{row.Context}";
                contextSubtitle.tooltip = row.Key;
            }

            var copyBtn = element.Q<Button>("copy-btn");
            if (copyBtn != null)
            {
                if (copyBtn.userData is Action oldAction)
                {
                    copyBtn.clicked -= oldAction;
                }

                Action copyAction = () => EditorGUIUtility.systemCopyBuffer = row.SourceText ?? string.Empty;
                copyBtn.userData = copyAction;
                copyBtn.clicked += copyAction;
            }

            var useSourceBtn = element.Q<Button>("use-source-btn");
            if (useSourceBtn != null)
            {
                if (useSourceBtn.userData is Action oldAction)
                {
                    useSourceBtn.clicked -= oldAction;
                }

                Action useSourceAction = () => OnUseSourceRequested?.Invoke(row);
                useSourceBtn.userData = useSourceAction;
                useSourceBtn.clicked += useSourceAction;
            }

            var aiBtn = element.Q<Button>("ai-btn");
            if (aiBtn != null)
            {
                if (aiBtn.userData is Action oldAction)
                {
                    aiBtn.clicked -= oldAction;
                }

                Action aiAction = () => OnTranslateRowRequested?.Invoke(row);
                aiBtn.userData = aiAction;
                aiBtn.clicked += aiAction;
            }

            var sourceField = element.Q<TextField>("source");
            if (sourceField != null)
            {
                sourceField.SetValueWithoutNotify(row.SourceText ?? string.Empty);
                sourceField.tooltip = row.SourceText ?? string.Empty;
                sourceField.style.height = EstimateTextHeight(row.SourceText, _sourceWidth);
            }

            var langContainer = element.Q<VisualElement>("lang-container");
            if (langContainer != null)
            {
                langContainer.Clear();
                foreach (var code in _localeCodes ?? Array.Empty<string>())
                {
                    if (string.Equals(code, _sourceLocale, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var scroll = new ScrollView(ScrollViewMode.Vertical);
                    scroll.AddToClassList("loc-col-lang");
                    SetFixedWidth(scroll, _langWidth);
                    scroll.style.height = CellTextHeight;
                    scroll.style.minHeight = CellTextHeight;
                    scroll.style.maxHeight = CellTextHeight;
                    scroll.style.marginTop = 10;
                    scroll.style.marginBottom = 10;
                    scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

                    var currentValue = row.Translations.TryGetValue(code, out var value) ? (value ?? string.Empty) : string.Empty;
                    var field = new TextField
                    {
                        value = currentValue,
                        multiline = true,
                        isDelayed = true
                    };
                    field.style.flexGrow = 1;
                    field.style.minHeight = CellTextHeight;
                    field.style.height = EstimateTextHeight(currentValue, _langWidth);
                    field.style.whiteSpace = WhiteSpace.Normal;
                    field.tooltip = $"Translate to {FormatLocaleDisplayName(code)}";
                    field.RegisterValueChangedCallback(evt =>
                    {
                        OnTranslationEdited?.Invoke(row, code, evt.newValue ?? string.Empty);
                    });
                    scroll.Add(field);
                    langContainer.Add(scroll);
                }
            }

            var statusBadge = element.Q<Label>("status-badge");
            if (statusBadge != null)
            {
                SetFixedWidth(statusBadge, StatusWidth);
                ApplyStatusBadge(statusBadge, row.Status);
            }

            element.UnregisterCallback<ClickEvent>(OnRowClicked);
            element.RegisterCallback<ClickEvent>(OnRowClicked);
        }

        private void OnRowClicked(ClickEvent evt)
        {
            if (evt.currentTarget is VisualElement ve && ve.userData is LocRow row)
            {
                OnRowSelected?.Invoke(row);
            }
        }

        private static void ApplyStatusBadge(Label badge, OverallStatus status)
        {
            badge.EnableInClassList("loc-status--missing", false);
            badge.EnableInClassList("loc-status--translated", false);

            switch (status)
            {
                case OverallStatus.Missing:
                    badge.text = "MISSING";
                    badge.EnableInClassList("loc-status--missing", true);
                    break;
                case OverallStatus.Translated:
                    badge.text = "TRANSLATED";
                    badge.EnableInClassList("loc-status--translated", true);
                    break;
            }
        }

        private void OnSelectionChanged(IEnumerable<object> items)
        {
            foreach (var item in items)
            {
                if (item is LocRow row)
                {
                    OnRowSelected?.Invoke(row);
                    return;
                }
            }
        }

        internal static string FormatLocaleDisplayName(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return localeCode ?? string.Empty;
            }

            try
            {
                return System.Globalization.CultureInfo.GetCultureInfo(localeCode).DisplayName;
            }
            catch
            {
                return localeCode;
            }
        }

        private static float EstimateTextHeight(string text, float width)
        {
            if (string.IsNullOrEmpty(text))
            {
                return CellTextHeight;
            }

            var charsPerLine = Mathf.Max(18, Mathf.FloorToInt(width / 7f));
            var lineCount = 1;
            var currentLineLength = 0;
            foreach (var ch in text)
            {
                if (ch == '\n')
                {
                    lineCount++;
                    currentLineLength = 0;
                    continue;
                }

                currentLineLength++;
                if (currentLineLength >= charsPerLine)
                {
                    lineCount++;
                    currentLineLength = 0;
                }
            }

            return Mathf.Max(CellTextHeight, 28f + (lineCount * 17f));
        }

        private static void SetFixedWidth(VisualElement element, float width)
        {
            if (element == null)
            {
                return;
            }

            element.style.width = width;
            element.style.minWidth = width;
            element.style.maxWidth = width;
            element.style.flexShrink = 0;
        }
    }
}
