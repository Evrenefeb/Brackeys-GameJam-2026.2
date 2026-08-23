using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.ExportImport
{
    /// <summary>
    /// Editor window for localization export and import. Defaults to the current
    /// graph and uses dropdown lists for project graphs instead of raw object fields.
    /// </summary>
    public class DialogLocalizationIOWindow : EditorWindow
    {
        private enum Tab
        {
            Export,
            Import
        }

        private const string RootKey = "DialogLocalizationIOWindow";
        private const string ExportFolderKey = RootKey + ".exportFolder";
        private const string ImportPathKey = RootKey + ".importPath";
        private const string DefaultExportFolder = "Assets/DialogGraphSystem/Localization/Export";

        private Tab _tab = Tab.Export;
        private DialogGraph _exportGraph;
        private DialogLocalizationTable _exportTargetTable;
        private string _exportFolder = DefaultExportFolder;
        private bool _exportAsTemplate = false;

        private string _importFilePath = string.Empty;
        private DialogLocalizationTable _importTargetTable;

        private readonly List<DialogGraph> _allGraphs = new();

        private VisualElement _contentArea;
        private Button _exportTabBtn;
        private Button _importTabBtn;
        private Button _exportBtn;
        private Button _importBtn;
        private Label _exportStatusLabel;
        private Label _importStatusLabel;

        public static void Open()
        {
            Open(null);
        }

        public static void Open(DialogGraph initialGraph)
        {
            var window = GetWindow<DialogLocalizationIOWindow>("Localization Export / Import");
            window.minSize = new Vector2(620, 480);
            if (initialGraph != null)
            {
                window._exportGraph = initialGraph;
            }

            window.Show();
        }

        private void OnEnable()
        {
            _exportFolder = EditorPrefs.GetString(ExportFolderKey, _exportFolder);
            _importFilePath = EditorPrefs.GetString(ImportPathKey, _importFilePath);

            _allGraphs.Clear();
            _allGraphs.AddRange(DialogGraphAssetPaths.LoadAllGraphAssets());

            if (_exportGraph == null)
            {
                _exportGraph = Selection.activeObject as DialogGraph
                    ?? _allGraphs.FirstOrDefault();
            }

            // Load brand tokens and standard styles
            var settingsSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.SETTINGS_STYLE_PATH);
            if (settingsSs != null && !rootVisualElement.styleSheets.Contains(settingsSs))
                rootVisualElement.styleSheets.Add(settingsSs);

            var mainSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.MAIN_WINDOW_STYLE_PATH);
            if (mainSs != null && !rootVisualElement.styleSheets.Contains(mainSs))
                rootVisualElement.styleSheets.Add(mainSs);

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null && !rootVisualElement.styleSheets.Contains(ss))
                rootVisualElement.styleSheets.Add(ss);

            BuildUI();
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("ds-root");

            var root = new VisualElement();
            root.AddToClassList("ds-content");
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            rootVisualElement.Add(root);

            root.Add(BuildHeader());
            root.Add(BuildTabBar());

            _contentArea = new ScrollView(ScrollViewMode.Vertical);
            _contentArea.AddToClassList("ds-tab-scroll");
            ((ScrollView)_contentArea).horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _contentArea.style.flexGrow = 1;
            _contentArea.style.backgroundColor = new StyleColor(StyleKeyword.None); // Transparent
            root.Add(_contentArea);

            LoadTabContent();
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("dgs-page-header");
            header.style.paddingLeft = 0;
            header.style.paddingRight = 0;
            header.style.paddingTop = 0;
            header.style.borderBottomWidth = 0;

            var title = new Label("Localization Export / Import");
            title.AddToClassList("dgs-page-title");
            header.Add(title);

            var subtitle = new Label("Export dialogue strings to JSON for translation, or import translated JSON files back into localization tables.");
            subtitle.AddToClassList("dgs-page-subtitle");
            header.Add(subtitle);

            return header;
        }

        private VisualElement BuildTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("ds-tab-bar");
            bar.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            bar.style.borderTopLeftRadius = 6;
            bar.style.borderTopRightRadius = 6;
            bar.style.borderBottomLeftRadius = 6;
            bar.style.borderBottomRightRadius = 6;
bar.style.marginBottom = 12;
            bar.style.paddingLeft = 4;
            bar.style.paddingRight = 4;
            bar.style.height = 34;
            bar.style.minHeight = 34;

            _exportTabBtn = new Button(() => SwitchTab(Tab.Export)) { text = "Export Strings" };
            _exportTabBtn.AddToClassList("ds-tab-button");
            _exportTabBtn.style.height = 32;
            _exportTabBtn.style.minHeight = 32;
            bar.Add(_exportTabBtn);

            _importTabBtn = new Button(() => SwitchTab(Tab.Import)) { text = "Import Translations" };
            _importTabBtn.AddToClassList("ds-tab-button");
            _importTabBtn.style.height = 32;
            _importTabBtn.style.minHeight = 32;
            bar.Add(_importTabBtn);

            return bar;
        }

        private void SwitchTab(Tab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            LoadTabContent();
        }

        private void LoadTabContent()
        {
            _exportTabBtn?.EnableInClassList("ds-tab-button--active", _tab == Tab.Export);
            _importTabBtn?.EnableInClassList("ds-tab-button--active", _tab == Tab.Import);

            _contentArea.Clear();
            var panel = _tab == Tab.Export ? BuildExportPanel() : BuildImportPanel();
            panel.style.paddingTop = 4;
            _contentArea.Add(panel);
        }

        private VisualElement BuildExportPanel()
        {
            var root = new VisualElement();
            var card = MakeCard("Configuration");

            var graphChoices = GetGraphChoiceLabels();
            var graphIndex = GetCurrentGraphIndex();
            var graphDropdown = new PopupField<string>("Source Graph", graphChoices, graphIndex);
            graphDropdown.style.marginBottom = 8;
            graphDropdown.RegisterValueChangedCallback(evt =>
            {
                _exportGraph = GetGraphByChoiceLabel(evt.newValue);
                RefreshExportButton();
            });
            card.Add(graphDropdown);

            var tableField = new ObjectField("Target Locale Table")
            {
                objectType = typeof(DialogLocalizationTable),
                value = _exportTargetTable
            };
            tableField.style.marginBottom = 8;
            tableField.RegisterValueChangedCallback(evt =>
            {
                _exportTargetTable = evt.newValue as DialogLocalizationTable;
            });
            card.Add(tableField);

            var templateToggle = new Toggle("Export as Empty Template (source strings only)")
            {
                value = _exportAsTemplate
            };
            templateToggle.style.marginBottom = 8;
            templateToggle.RegisterValueChangedCallback(evt => _exportAsTemplate = evt.newValue);
            card.Add(templateToggle);

            card.Add(BuildPathRow(
                "Output Folder",
                () => _exportFolder,
                value => _exportFolder = value,
                () =>
                {
                    var path = EditorUtility.OpenFolderPanel("Choose export folder", "Assets", string.Empty);
                    if (string.IsNullOrEmpty(path)) return null;
                    if (path.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    return path.Replace('\\', '/');
                }));

            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;
            buttonsRow.style.justifyContent = Justify.FlexEnd;
            buttonsRow.style.marginTop = 12;
            buttonsRow.style.paddingTop = 8;
            buttonsRow.style.borderTopWidth = 1;
            buttonsRow.style.borderTopColor = new Color(0.16f, 0.18f, 0.27f);

            _exportBtn = new Button(DoExport) { text = "Export to JSON" };
            _exportBtn.AddToClassList("dlg-btn");
            _exportBtn.AddToClassList("primary");
            buttonsRow.Add(_exportBtn);
            RefreshExportButton();

            card.Add(buttonsRow);

            _exportStatusLabel = MakeStatusLabel();
            card.Add(_exportStatusLabel);

            root.Add(card);
            return root;
        }

        private VisualElement BuildImportPanel()
        {
            var root = new VisualElement();
            var card = MakeCard("Configuration");

            card.Add(BuildPathRow(
                "JSON File",
                () => _importFilePath,
                value =>
                {
                    _importFilePath = value;
                    RefreshImportButton();
                },
                () => EditorUtility.OpenFilePanel("Select Localization JSON", Application.dataPath, "json")));

            var tableField = new ObjectField("Target Locale Table")
            {
                objectType = typeof(DialogLocalizationTable),
                value = _importTargetTable
            };
            tableField.style.marginBottom = 8;
            tableField.RegisterValueChangedCallback(evt =>
            {
                _importTargetTable = evt.newValue as DialogLocalizationTable;
                RefreshImportButton();
            });
            card.Add(tableField);

            var buttonsRow = new VisualElement();
            buttonsRow.style.flexDirection = FlexDirection.Row;
            buttonsRow.style.justifyContent = Justify.FlexEnd;
            buttonsRow.style.marginTop = 12;
            buttonsRow.style.paddingTop = 8;
            buttonsRow.style.borderTopWidth = 1;
            buttonsRow.style.borderTopColor = new Color(0.16f, 0.18f, 0.27f);

            _importBtn = new Button(DoImport) { text = "Import Translations" };
            _importBtn.AddToClassList("dlg-btn");
            _importBtn.AddToClassList("success");
            buttonsRow.Add(_importBtn);
            RefreshImportButton();

            card.Add(buttonsRow);

            _importStatusLabel = MakeStatusLabel();
            card.Add(_importStatusLabel);

            root.Add(card);
            return root;
        }

        private void RefreshExportButton()
        {
            _exportBtn?.SetEnabled(_exportGraph != null);
        }

        private void RefreshImportButton()
        {
            _importBtn?.SetEnabled(
                !string.IsNullOrWhiteSpace(_importFilePath) &&
                _importTargetTable != null);
        }

        private void DoExport()
        {
            if (_exportGraph == null)
            {
                SetStatus(_exportStatusLabel, "Select a graph before exporting.", true);
                return;
            }

            try
            {
                if (_exportFolder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureFolder(_exportFolder);
                }
                else
                {
                    Directory.CreateDirectory(_exportFolder);
                }

                LocalizationExportRecord record = _exportAsTemplate || _exportTargetTable == null
                    ? DialogLocalizationExporter.BuildSourceExport(_exportGraph)
                    : DialogLocalizationExporter.BuildTranslationExport(_exportGraph, _exportTargetTable);

                var localeCode = _exportTargetTable != null
                    ? _exportTargetTable.LocaleCode
                    : "template";

                var safeTitle = SanitizeFileName(_exportGraph.graphTitle ?? _exportGraph.name);
                var safeLocale = SanitizeFileName(localeCode);
                var fileName = $"{safeTitle}_{safeLocale}_loc.json";
                var assetPath = Path.Combine(_exportFolder, fileName).Replace("\\", "/");
                var absPath = Path.GetFullPath(assetPath);

                File.WriteAllText(absPath, DialogLocalizationExporter.ToJson(record));
                AssetDatabase.Refresh();

                SetStatus(_exportStatusLabel, $"Exported {record.entries.Count} strings to {assetPath}", false);
            }
            catch (Exception e)
            {
                SetStatus(_exportStatusLabel, $"Export failed: {e.Message}", true);
                Debug.LogException(e);
            }
        }

        private void DoImport()
        {
            if (string.IsNullOrWhiteSpace(_importFilePath) || _importTargetTable == null)
            {
                SetStatus(_importStatusLabel, "Select a JSON file and a target table.", true);
                return;
            }

            var result = DialogLocalizationImporter.ImportFromFile(_importFilePath, _importTargetTable);
            if (!result.IsSuccess)
            {
                SetStatus(_importStatusLabel, result.Summary, true);
                return;
            }

            var warningsSuffix = result.Warnings.Count > 0
                ? $" ({result.Warnings.Count} warning(s) - see Console.)"
                : string.Empty;

            foreach (var warning in result.Warnings)
            {
                Debug.LogWarning($"[DialogLocalizationImporter] {warning}");
            }

            SetStatus(_importStatusLabel, result.Summary + warningsSuffix, false);
        }

        private List<string> GetGraphChoiceLabels()
        {
            return _allGraphs.Count == 0
                ? new List<string> { "(no graphs found)" }
                : _allGraphs.Select(GetGraphChoiceLabel).ToList();
        }

        private int GetCurrentGraphIndex()
        {
            if (_allGraphs.Count == 0) return 0;
            var index = _allGraphs.FindIndex(graph => graph == _exportGraph);
            return index >= 0 ? index : 0;
        }

        private DialogGraph GetGraphByChoiceLabel(string choiceLabel)
        {
            return _allGraphs.FirstOrDefault(graph =>
                string.Equals(GetGraphChoiceLabel(graph), choiceLabel, StringComparison.Ordinal));
        }

        private static string GetGraphChoiceLabel(DialogGraph graph)
        {
            if (graph == null) return "(none)";
            var title = string.IsNullOrWhiteSpace(graph.graphTitle) ? graph.name : graph.graphTitle;
            return $"{title} ({graph.name})";
        }

        private static VisualElement BuildPathRow(
            string labelText,
            Func<string> getter,
            Action<string> setter,
            Func<string> browseAction)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;

            var field = new TextField(labelText) { value = getter() };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => setter(evt.newValue));
            row.Add(field);

            var browseBtn = new Button(() =>
            {
                var path = browseAction?.Invoke();
                if (string.IsNullOrWhiteSpace(path)) return;
                setter(path);
                field.value = path;
            })
            { text = "Browse" };
            browseBtn.AddToClassList("dlg-btn");
            browseBtn.style.marginLeft = 6;
            browseBtn.style.width = 72;
            browseBtn.style.minWidth = 72;
            row.Add(browseBtn);

            return row;
        }

        private static VisualElement MakeCard(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("dgs-card");

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingBottom = 8;
            header.style.marginBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.16f, 0.18f, 0.27f);
            card.Add(header);

            var lbl = new Label(title);
            lbl.AddToClassList("dgs-card-title");
            lbl.style.marginBottom = 0;
            header.Add(lbl);

            return card;
        }

        private static Label MakeStatusLabel()
        {
            var label = new Label(string.Empty);
            label.AddToClassList("dgs-page-subtitle");
            label.style.marginTop = 12;
            label.style.paddingTop = 8;
            label.style.borderTopWidth = 1;
            label.style.borderTopColor = new Color(0.16f, 0.18f, 0.27f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.display = DisplayStyle.None;
            return label;
        }

        private static void SetStatus(Label label, string message, bool isError)
        {
            if (label == null) return;
            label.text = message;
            label.style.display = DisplayStyle.Flex;
            label.style.color = isError
                ? new StyleColor(new Color(0.9f, 0.35f, 0.35f))
                : new StyleColor(new Color(0.35f, 0.9f, 0.45f));
        }

        private static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "graph";
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }

            return input.Replace(' ', '_').ToLowerInvariant();
        }
    }
}
