using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.Localization;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Resources;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows.Tabs
{
    public partial class GraphTabController
    {
        private const string PrefKeyFavoriteGraphs = "DialogSystem_FavoriteGraphs";
        private const string PrefKeyCustomFolders = "DialogSystem_BrowserCustomFolders";
        private const string PrefKeyHiddenFolders = "DialogSystem_BrowserHiddenFolders";
        private const string FavoriteStarEmptyIconPath = "Assets/DialogGraphSystem/Scripts/Editor/Icons/star-empty.svg";
        private const string FavoriteStarFilledIconPath = "Assets/DialogGraphSystem/Scripts/Editor/Icons/star-filled.svg";

        private ToolbarSearchField _browserSearchField;
        private Button _browserFavoritesButton;
        private Button _browserRecentButton;
        private TextField _browserCreateNameField;
        private TextField _browserNewCategoryNameField;
        private PopupField<string> _browserNewCategoryParentPopup;
        private ColorField _browserNewCategoryColorField;
        private VisualElement _browserHeaderWarningHost;
        private VisualElement _browserCatalogRoot;
        private VisualElement _browserListRoot;
        private VisualElement _browserInspectorRoot;

        private DialogGraphBrowserScopeKind _browserScopeKind = DialogGraphBrowserScopeKind.All;
        private string _browserScopeValue = string.Empty;
        private string _browserSelectedGraphName = string.Empty;
        private string _browserSearchQuery = string.Empty;
        private bool _browserFavoritesOnly;
        private bool _browserRecentOnly;
        private List<DialogGraphBrowserRecord> _browserItems = new();

        private void BuildHomeBrowserUI()
        {
            var host = new VisualElement();
            host.AddToClassList("dgs-graph-browser");
            _root.Add(host);

            // ── Header ────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("dgs-graph-browser-header");
            host.Add(header);

            var titleBlock = new VisualElement { style = { flexGrow = 1 } };
            header.Add(titleBlock);

            var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            titleBlock.Add(titleRow);

            var icon = DialogGraphIconManager.CreateImage(DialogGraphIconId.ToolbarOpen, "dgs-nav-icon");
            icon.style.width = 24;
            icon.style.height = 24;
            icon.style.marginRight = 10;
            titleRow.Add(icon);

            var title = new Label("Dialogue Graph Browser");
            title.AddToClassList("dgs-graph-browser-title");
            titleRow.Add(title);

            var subtitle = new Label("Organize your conversations with categories and folders. Categories are metadata labels; Folders are physical asset locations.");
            subtitle.AddToClassList("dgs-graph-browser-subtitle");
            titleBlock.Add(subtitle);

            _browserHeaderWarningHost = new VisualElement();
            titleBlock.Add(_browserHeaderWarningHost);
            RefreshHomeBrowserHeaderWarning();

            // ── Creation Row ──────────────────────────────────────────
            var createRow = new VisualElement();
            createRow.AddToClassList("dgs-graph-browser-create-row");
            header.Add(createRow);

            _browserCreateNameField = new TextField("Name")
            {
                value = string.IsNullOrWhiteSpace(_browserCreateNameField?.value) ? "DialogGraph" : _browserCreateNameField.value
            };
            _browserCreateNameField.AddToClassList("dlg-textfield");
            _browserCreateNameField.AddToClassList("dgs-graph-browser-create-field");
            _browserCreateNameField.AddToClassList("tight-label");
            _browserCreateNameField.style.flexGrow = 1;
            _browserCreateNameField.style.minWidth = 0;
            createRow.Add(_browserCreateNameField);

            var createButton = new Button(OnClickCreateBrowserGraphDefault)
            {
                text = "Create & Open"
            };
            createButton.AddToClassList("dlg-btn");
            createButton.AddToClassList("success");
            createButton.AddToClassList("dgs-graph-browser-create-button");
            createRow.Add(createButton);

            var body = new VisualElement();
            body.AddToClassList("dgs-graph-browser-body");
            host.Add(body);

            _browserCatalogRoot = new VisualElement();
            _browserCatalogRoot.AddToClassList("dgs-graph-browser-column");
            _browserCatalogRoot.AddToClassList("dgs-graph-browser-column--catalog");
            body.Add(_browserCatalogRoot);

            _browserListRoot = new VisualElement();
            _browserListRoot.AddToClassList("dgs-graph-browser-column");
            _browserListRoot.AddToClassList("dgs-graph-browser-column--list");
            body.Add(_browserListRoot);

            _browserInspectorRoot = new VisualElement();
            _browserInspectorRoot.AddToClassList("dgs-graph-browser-column");
            _browserInspectorRoot.AddToClassList("dgs-graph-browser-column--inspector");
            body.Add(_browserInspectorRoot);

            RefreshHomeBrowser();
        }

        private void RefreshHomeBrowser()
        {
            RefreshHomeBrowserData();
            EnsureDefaultBrowserSelection();
            RefreshHomeBrowserHeaderWarning();
            RebuildHomeCatalogPane();
            RebuildHomeListPane();
            RebuildHomeInspectorPane();
        }

        public void RefreshHomeBrowserIfVisible()
        {
            if (_browserListRoot == null || _browserListRoot.panel == null)
            {
                return;
            }

            RefreshHomeBrowser();
        }

        private void RefreshHomeBrowserHeaderWarning()
        {
            if (_browserHeaderWarningHost == null)
            {
                return;
            }

            _browserHeaderWarningHost.Clear();

            var launcherWarning = BuildLauncherLocalizationWarning();
            if (!string.IsNullOrEmpty(launcherWarning))
            {
                _browserHeaderWarningHost.Add(CreateGraphBrowserWarningBanner(launcherWarning));
            }
        }

        private void RefreshHomeBrowserData()
        {
            var recentNames = new HashSet<string>(GetRecentGraphs(), StringComparer.OrdinalIgnoreCase);
            var favoriteKeys = GetFavoriteGraphKeys();

            _browserItems = DialogGraphAssetPaths.GetVisibleGraphAssetPaths()
                .Select(assetPath =>
                {
                    var graph = AssetDatabase.LoadAssetAtPath<DialogGraph>(assetPath);
                    if (graph == null)
                    {
                        return null;
                    }

                    return new DialogGraphBrowserRecord
                    {
                        GraphName = graph.name,
                        DisplayTitle = DialogGraphBrowserUtility.GetDisplayTitle(graph, graph.name),
                        AssetPath = assetPath,
                        FolderPath = DialogGraphBrowserUtility.NormalizeFolderPath(assetPath),
                        Description = graph.description ?? string.Empty,
                        Author = graph.author ?? string.Empty,
                        PrimaryCategory = DialogGraphBrowserUtility.NormalizeCategoryPath(graph.primaryCategory),
                        Categories = DialogGraphBrowserUtility.NormalizeCategoryList(graph.categories),
                        IsFavorite = favoriteKeys.Contains(assetPath),
                        IsRecent = recentNames.Contains(graph.name),
                        LastModifiedUtc = ResolveBrowserModifiedUtc(graph, assetPath)
                    };
                })
                .Where(item => item != null)
                .OrderByDescending(item => item.IsFavorite)
                .ThenBy(item => item.DisplayTitle, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(_browserSelectedGraphName) &&
                _browserItems.All(item => !string.Equals(item.GraphName, _browserSelectedGraphName, StringComparison.OrdinalIgnoreCase)))
            {
                _browserSelectedGraphName = string.Empty;
            }
        }

        private static DateTime ResolveBrowserModifiedUtc(DialogGraph graph, string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(graph?.lastModifiedUtc) &&
                DateTime.TryParse(graph.lastModifiedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var storedUtc))
            {
                return storedUtc.Kind == DateTimeKind.Utc ? storedUtc : storedUtc.ToUniversalTime();
            }

            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(absolutePath) ? File.GetLastWriteTimeUtc(absolutePath) : DateTime.MinValue;
        }

        private void RebuildHomeCatalogPane()
        {
            _browserCatalogRoot.Clear();

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("dgs-graph-browser-scroll");
            _browserCatalogRoot.Add(scroll);

            // ── Quick Filters ──────────────────────────────────────
            scroll.Add(BuildCatalogSectionHeader("Quick Filters"));
            
            var filterRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 8 } };
            scroll.Add(filterRow);

            filterRow.Add(CreateCatalogPill("All", DialogGraphBrowserScopeKind.All, string.Empty, _browserItems.Count));
            filterRow.Add(CreateCatalogPill(
                "Uncategorized",
                DialogGraphBrowserScopeKind.Uncategorized,
                string.Empty,
                _browserItems.Count(item => DialogGraphBrowserUtility.CollectAssignedCategories(item.PrimaryCategory, item.Categories).Count == 0)));

            // ── Categories ──────────────────────────────────────
            scroll.Add(BuildCatalogSectionHeader("Categories"));
            scroll.Add(BuildCategoryComposer());

            var scopeItems = GetScopeFilteredItems();
            var categories = DialogGraphCategoryCatalogService.GetOrderedCategories();
            if (categories.Count == 0)
            {
                scroll.Add(CreateCatalogHint("No categories defined yet."));
            }
            else
            {
                foreach (var category in categories)
                {
                    var normalizedPath = DialogGraphBrowserUtility.NormalizeCategoryPath(category.path);
                    var count = scopeItems.Count(item =>
                        DialogGraphBrowserUtility.CollectAssignedCategories(item.PrimaryCategory, item.Categories)
                            .Any(value => value.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase) ||
                                          value.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase)));

                    scroll.Add(CreateCatalogPathButton(
                        DialogGraphBrowserUtility.GetCategoryLeafName(normalizedPath),
                        normalizedPath,
                        DialogGraphBrowserUtility.GetCategoryDepth(normalizedPath),
                        category.color,
                        count,
                        DialogGraphBrowserScopeKind.Category));
                }
            }

            // ── Folders ──────────────────────────────────────
            var folderHeader = BuildCatalogSectionHeader("Folders");
            var addFolderBtn = new Button(OnClickAddBrowserFolder) { text = "+" };
            addFolderBtn.AddToClassList("dlg-btn");
            addFolderBtn.style.width = 24;
            addFolderBtn.style.height = 20;
            addFolderBtn.style.fontSize = 14;
            addFolderBtn.style.paddingLeft = 0;
            addFolderBtn.style.paddingRight = 0;
            addFolderBtn.style.paddingTop = 0;
            addFolderBtn.style.paddingBottom = 0;
            addFolderBtn.style.marginTop = 0;
            addFolderBtn.style.marginRight = 0;
            addFolderBtn.style.backgroundColor = new Color(0.25f, 0.3f, 0.4f, 0.3f);
            addFolderBtn.style.borderLeftWidth = 1;
            addFolderBtn.style.borderRightWidth = 1;
            addFolderBtn.style.borderTopWidth = 1;
            addFolderBtn.style.borderBottomWidth = 1;
            addFolderBtn.style.borderTopColor = new Color(0.4f, 0.45f, 0.55f, 0.4f);
            addFolderBtn.style.borderBottomColor = new Color(0.4f, 0.45f, 0.55f, 0.4f);
            addFolderBtn.style.borderLeftColor = new Color(0.4f, 0.45f, 0.55f, 0.4f);
            addFolderBtn.style.borderRightColor = new Color(0.4f, 0.45f, 0.55f, 0.4f);
            addFolderBtn.tooltip = "Add Existing Folder to Browser";
            folderHeader.Add(addFolderBtn);
            scroll.Add(folderHeader);

            var hiddenFolders = GetHiddenFolders();
            var autoFolders = _browserItems
                .Select(item => DialogGraphBrowserUtility.NormalizeFolderPath(item.FolderPath))
                .Where(path => !string.IsNullOrWhiteSpace(path) && !hiddenFolders.Contains(path));

            var customFolders = GetCustomFolders();
            var allFolders = autoFolders.Concat(customFolders)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (allFolders.Count == 0)
            {
                scroll.Add(CreateCatalogHint("No graph folders found."));
            }
            else
            {
                foreach (var folder in allFolders)
                {
                    var count = _browserItems.Count(item =>
                        item.FolderPath.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
                        item.FolderPath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));

                    scroll.Add(CreateCatalogPathButton(
                        Path.GetFileName(folder),
                        folder,
                        DialogGraphBrowserUtility.GetFolderDepth(folder),
                        new Color(0.39f, 0.44f, 0.53f, 1f),
                        count,
                        DialogGraphBrowserScopeKind.Folder));
                }
            }
        }

        private VisualElement CreateCatalogPill(string label, DialogGraphBrowserScopeKind kind, string value, int count)
        {
            var btn = new Button(() =>
            {
                _browserScopeKind = kind;
                _browserScopeValue = value ?? string.Empty;
                _browserSelectedGraphName = string.Empty;
                RebuildHomeCatalogPane();
                RebuildHomeListPane();
                RebuildHomeInspectorPane();
            });
            btn.AddToClassList("dgs-pill");
            btn.style.marginRight = 4;
            btn.style.marginBottom = 4;
            btn.style.flexShrink = 0;

            var displayLabel = kind == DialogGraphBrowserScopeKind.Uncategorized
                ? "Uncat."
                : $"{label} ({count})";

            var pillLabel = new Label(displayLabel);
            pillLabel.AddToClassList("dgs-pill-label");
            btn.Add(pillLabel);
            
            if (_browserScopeKind == kind && string.Equals(_browserScopeValue, value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                btn.AddToClassList("active");
                btn.style.backgroundColor = new Color(0.16f, 0.29f, 0.47f, 1f);
            }

            return btn;
        }

        private VisualElement BuildCategoryComposer()
        {
            var panel = new VisualElement();
            panel.AddToClassList("dgs-graph-browser-composer");

            _browserNewCategoryNameField = new TextField("Name") { value = _browserNewCategoryNameField?.value ?? string.Empty };
            _browserNewCategoryNameField.AddToClassList("dlg-textfield");
            _browserNewCategoryNameField.AddToClassList("dgs-graph-browser-stack-field");
            panel.Add(_browserNewCategoryNameField);

            var parentChoices = new List<string> { "Root" };
            parentChoices.AddRange(DialogGraphCategoryCatalogService.GetOrderedCategories().Select(category => category.path));
            var selectedParentIndex = ResolveNewCategoryParentIndex(parentChoices);
            _browserNewCategoryParentPopup = new PopupField<string>("Parent", parentChoices, selectedParentIndex);
            _browserNewCategoryParentPopup.AddToClassList("dlg-popup");
            _browserNewCategoryParentPopup.AddToClassList("dgs-graph-browser-stack-field");
            panel.Add(_browserNewCategoryParentPopup);

            var actionsRow = new VisualElement();
            actionsRow.AddToClassList("dgs-graph-browser-inline-row");
            actionsRow.AddToClassList("dgs-graph-browser-composer-actions");
            panel.Add(actionsRow);

            _browserNewCategoryColorField = new ColorField("Color")
            {
                value = _browserNewCategoryColorField != null
                    ? _browserNewCategoryColorField.value
                    : DialogGraphCategoryCatalogService.GetSuggestedColor(DialogGraphCategoryCatalogService.GetOrderedCategories().Count)
            };
            _browserNewCategoryColorField.AddToClassList("dgs-graph-browser-stack-field");
            _browserNewCategoryColorField.AddToClassList("dgs-graph-browser-color-field");
            actionsRow.Add(_browserNewCategoryColorField);

            var addButton = new Button(OnClickAddBrowserCategory) { text = "Create Category" };
            addButton.AddToClassList("dlg-btn");
            addButton.AddToClassList("secondary");
            addButton.AddToClassList("dgs-graph-browser-inline-button");
            actionsRow.Add(addButton);

            return panel;
        }

        private int ResolveNewCategoryParentIndex(IReadOnlyList<string> parentChoices)
        {
            var preferredParent = _browserScopeKind == DialogGraphBrowserScopeKind.Category
                ? _browserScopeValue
                : "Root";

            for (var index = 0; index < parentChoices.Count; index++)
            {
                if (string.Equals(parentChoices[index], preferredParent, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return 0;
        }

        private void RebuildHomeListPane()
        {
            _browserListRoot.Clear();
            EnsureDefaultBrowserSelection();

            var toolbar = new VisualElement();
            toolbar.AddToClassList("dgs-graph-browser-toolbar");
            toolbar.style.paddingLeft = 12;
            toolbar.style.paddingRight = 12;
            _browserListRoot.Add(toolbar);

            _browserSearchField = new ToolbarSearchField();
            _browserSearchField.SetValueWithoutNotify(_browserSearchQuery);
            _browserSearchField.style.flexGrow = 1;
            _browserSearchField.style.marginRight = 8;
            _browserSearchField.RegisterValueChangedCallback(evt =>
            {
                _browserSearchQuery = evt.newValue ?? string.Empty;
                RebuildHomeCatalogPane();
                RebuildHomeListPane();
                RebuildHomeInspectorPane();
            });
            toolbar.Add(_browserSearchField);

            var filterGroup = new VisualElement();
            filterGroup.AddToClassList("dgs-graph-browser-filter-group");
            toolbar.Add(filterGroup);

            _browserFavoritesButton = new Button(() =>
            {
                _browserFavoritesOnly = !_browserFavoritesOnly;
                RefreshHomeBrowser();
            })
            {
                text = "Favorites"
            };
            _browserFavoritesButton.AddToClassList("dgs-graph-browser-toggle-button");
            _browserFavoritesButton.EnableInClassList("active", _browserFavoritesOnly);
            filterGroup.Add(_browserFavoritesButton);

            _browserRecentButton = new Button(() =>
            {
                _browserRecentOnly = !_browserRecentOnly;
                RefreshHomeBrowser();
            })
            {
                text = "Recent"
            };
            _browserRecentButton.AddToClassList("dgs-graph-browser-toggle-button");
            _browserRecentButton.EnableInClassList("active", _browserRecentOnly);
            filterGroup.Add(_browserRecentButton);

            var resetButton = new Button(ResetBrowserFilters) { text = "Reset" };
            resetButton.AddToClassList("dgs-graph-browser-toggle-button");
            toolbar.Add(resetButton);

            var summary = new Label(GetBrowserSummaryText(GetVisibleBrowserItems()));
            summary.AddToClassList("dgs-graph-browser-summary");
            _browserListRoot.Add(summary);

            var listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            listScroll.AddToClassList("dgs-graph-browser-scroll");
            _browserListRoot.Add(listScroll);

            var visibleItems = GetVisibleBrowserItems();
            if (visibleItems.Count == 0)
            {
                var hint = CreateCatalogHint("No graphs match the current filters.");
                hint.style.marginTop = 20;
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                listScroll.Add(hint);
                return;
            }

            foreach (var item in visibleItems)
            {
                listScroll.Add(BuildGraphRow(item));
            }
        }

        private void RebuildHomeInspectorPane()
        {
            _browserInspectorRoot.Clear();

            var header = new Label("Details");
            header.AddToClassList("dgs-graph-browser-inspector-title");
            _browserInspectorRoot.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("dgs-graph-browser-scroll");
            _browserInspectorRoot.Add(scroll);

            var selectedGraph = _browserItems.FirstOrDefault(item =>
                string.Equals(item.GraphName, _browserSelectedGraphName, StringComparison.OrdinalIgnoreCase));
            if (selectedGraph != null)
            {
                scroll.Add(BuildGraphInspector(selectedGraph));
                return;
            }

            switch (_browserScopeKind)
            {
                case DialogGraphBrowserScopeKind.Category:
                    scroll.Add(BuildCategoryInspector(_browserScopeValue));
                    break;
                case DialogGraphBrowserScopeKind.Folder:
                    scroll.Add(BuildFolderInspector(_browserScopeValue));
                    break;
                case DialogGraphBrowserScopeKind.Uncategorized:
                    scroll.Add(BuildScopeSummaryCard("Uncategorized Graphs", "Visible graphs here do not have any browser categories assigned."));
                    break;
                default:
                    scroll.Add(BuildScopeSummaryCard("All Graphs", "Select a graph, category, or folder to inspect it."));
                    break;
            }
        }

        private VisualElement BuildGraphRow(DialogGraphBrowserRecord item)
        {
            var graphAsset = AssetDatabase.LoadAssetAtPath<DialogGraph>(item.AssetPath);
            var localizationStatus = EvaluateGraphLocalizationStatus(graphAsset);

            var row = new VisualElement();
            row.AddToClassList("dgs-graph-row");
            row.EnableInClassList("active", string.Equals(item.GraphName, _browserSelectedGraphName, StringComparison.OrdinalIgnoreCase));
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || IsGraphRowInteractiveTarget(evt.target as VisualElement, row))
                {
                    return;
                }

                SelectBrowserGraph(item.GraphName);

                if (evt.clickCount >= 2)
                {
                    LoadGraphByName(item.GraphName);
                }
            });

            var accent = new VisualElement();
            accent.AddToClassList("dgs-graph-row-accent");
            accent.style.backgroundColor = ResolveBrowserAccentColor(item);
            row.Add(accent);

            var content = new VisualElement { style = { flexGrow = 1, paddingLeft = 12, paddingRight = 12, paddingTop = 8, paddingBottom = 10 } };
            row.Add(content);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("dgs-graph-row-title-row");
            content.Add(titleRow);

            var title = new Label(item.DisplayTitle);
            title.AddToClassList("dgs-graph-row-title");
            titleRow.Add(title);

            if (localizationStatus.HasWarning)
            {
                var warningIcon = DialogGraphIconManager.CreateImage(
                    DialogGraphIconId.StatusWarning,
                    "dgs-icon--sm",
                    "dgs-icon--warning");
                warningIcon.AddToClassList("dgs-graph-row-warning-icon");
                warningIcon.tooltip = localizationStatus.WarningMessage;
                titleRow.Add(warningIcon);
            }

            if (!string.Equals(item.DisplayTitle, item.GraphName, StringComparison.Ordinal))
            {
                var fileLabel = new Label($"({item.GraphName})");
                fileLabel.AddToClassList("dgs-graph-row-file-name");
                fileLabel.style.marginLeft = 4;
                titleRow.Add(fileLabel);
            }

            var pathRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            content.Add(pathRow);

            var folderIcon = new Label("📁");
            folderIcon.style.fontSize = 10;
            folderIcon.style.marginRight = 4;
            folderIcon.style.opacity = 0.5f;
            pathRow.Add(folderIcon);
            folderIcon.style.display = DisplayStyle.None;

            var graphIcon = DialogGraphIconManager.CreateImage(DialogGraphIconId.NodeDialog, "dgs-icon--sm", "dgs-icon--dialog");
            graphIcon.style.width = 12;
            graphIcon.style.height = 12;
            graphIcon.style.marginRight = 4;
            graphIcon.style.opacity = 0.65f;
            pathRow.Add(graphIcon);

            var displayPath = item.AssetPath.Replace("Assets/DialogSystem/Dialogues/Graphs/", "").Replace("Assets/DialogGraphSystem/Graphs/", "");
            var pathLabel = new Label(displayPath);
            pathLabel.AddToClassList("dgs-graph-row-path");
            pathLabel.style.marginBottom = 0;
            pathRow.Add(pathLabel);

            var metadataLabel = new Label(BuildBrowserMetadataLine(item));
            metadataLabel.AddToClassList("dgs-graph-row-metadata");
            content.Add(metadataLabel);

            var localizationLabel = new Label(localizationStatus.Text);
            localizationLabel.AddToClassList("dgs-graph-row-metadata");
            localizationLabel.EnableInClassList("dgs-graph-row-localization-warning", localizationStatus.HasWarning);
            localizationLabel.style.marginTop = 2;
            content.Add(localizationLabel);

            var categoriesRow = new VisualElement();
            categoriesRow.AddToClassList("dgs-graph-row-categories");
            categoriesRow.style.marginTop = 4;
            var assignedCategories = DialogGraphBrowserUtility.CollectAssignedCategories(item.PrimaryCategory, item.Categories);
            foreach (var categoryPath in assignedCategories.Take(4))
            {
                categoriesRow.Add(CreateCategoryChip(categoryPath, ResolveCategoryColor(categoryPath)));
            }

            if (categoriesRow.childCount == 0)
            {
                categoriesRow.Add(CreateCategoryChip("Uncategorized", new Color(0.3f, 0.35f, 0.4f, 1f)));
            }

            content.Add(categoriesRow);

            var actions = new VisualElement();
            actions.AddToClassList("dgs-graph-row-actions");
            actions.style.paddingLeft = 4;
            actions.style.paddingRight = 8;
            row.Add(actions);

            actions.Add(CreateFavoriteButton(item.AssetPath, item.IsFavorite));

            var openButton = new Button(() => LoadGraphByName(item.GraphName))
            {
                text = "Open"
            };
            openButton.AddToClassList("dlg-btn");
            openButton.AddToClassList("primary");
            openButton.style.marginTop = 4;
            actions.Add(openButton);

            return row;
        }

        private static bool IsGraphRowInteractiveTarget(VisualElement target, VisualElement row)
        {
            for (var current = target; current != null && current != row; current = current.parent)
            {
                if (current is Button || current.ClassListContains("unity-button"))
                {
                    return true;
                }
            }

            return false;
        }

        private void DeleteGraphAsset(string graphName)
        {
            if (string.IsNullOrEmpty(graphName)) return;

            var path = DialogGraphAssetPaths.ResolveGraphAssetPath(graphName);
            if (string.IsNullOrEmpty(path)) return;

            if (!EditorUtility.DisplayDialog("Delete Graph", 
                    $"Are you sure you want to delete the dialogue graph '{graphName}'?\nThis action cannot be undone.", 
                    "Delete", "Cancel"))
            {
                return;
            }

            if (_openGraphNames.Contains(graphName))
            {
                _openGraphNames.Remove(graphName);
                if (_loadedGraphName == graphName)
                    _loadedGraphName = null;
            }

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
            
            RefreshHomeBrowser();
        }

        private VisualElement BuildGraphInspector(DialogGraphBrowserRecord item)
        {
            var asset = DialogGraphAssetPaths.LoadGraphAsset(item.GraphName);
            if (asset == null)
            {
                return BuildScopeSummaryCard("Missing Graph", "The selected graph asset could not be loaded.");
            }

            var card = new VisualElement();
            card.AddToClassList("dgs-graph-browser-card");
            card.style.paddingLeft = 14;
            card.style.paddingRight = 14;

            var title = new Label(item.DisplayTitle);
            title.AddToClassList("dgs-graph-browser-card-title");
            title.style.fontSize = 15;
            card.Add(title);

            var assetPathLabel = new Label(item.AssetPath);
            assetPathLabel.AddToClassList("dgs-graph-browser-card-subtitle");
            assetPathLabel.style.marginBottom = 12;
            card.Add(assetPathLabel);

            var favoriteButton = CreateFavoriteButton(item.AssetPath, item.IsFavorite);
            favoriteButton.AddToClassList("dgs-graph-browser-card-favorite");
            card.Add(favoriteButton);

            var actions = new VisualElement();
            actions.AddToClassList("dgs-graph-browser-card-actions");
            actions.style.marginBottom = 16;
            card.Add(actions);

            var openButton = new Button(() => LoadGraphByName(item.GraphName)) { text = "Open Graph" };
            openButton.AddToClassList("dlg-btn");
            openButton.AddToClassList("primary");
            openButton.style.height = 28;
            openButton.style.flexGrow = 1;
            actions.Add(openButton);

            var pingButton = new Button(() => DialogGraphDefinitionResolver.PingAndSelect(asset)) { text = "Ping" };
            pingButton.AddToClassList("dlg-btn");
            pingButton.AddToClassList("secondary");
            pingButton.style.height = 28;
            actions.Add(pingButton);

            var deleteButton = new Button(() => DeleteGraphAsset(item.GraphName))
            {
                text = "Delete"
            };
            deleteButton.AddToClassList("dlg-btn");
            deleteButton.AddToClassList("danger");
            deleteButton.style.height = 28;
            actions.Add(deleteButton);

            card.Add(BuildGraphMetadataEditor(asset));
            card.Add(BuildGraphLocalizationSummary(asset));
            card.Add(BuildGraphCategoryEditor(asset));
            
            var hint = BuildInspectorLabel("Categories are browser-only metadata and do not affect physical file organization.");
            hint.style.marginTop = 12;
            hint.style.opacity = 0.6f;
            card.Add(hint);
            
            return card;
        }

        private VisualElement BuildGraphMetadataEditor(DialogGraph asset)
        {
            var section = new VisualElement();
            section.AddToClassList("dgs-graph-browser-section");
            section.Add(BuildInspectorSectionTitle("Metadata"));

            var titleField = BuildBrowserEditorTextField(asset.graphTitle ?? string.Empty);
            titleField.RegisterValueChangedCallback(evt =>
                UpdateBrowserGraphMetadata(asset, "Edit Graph Title", graph => graph.graphTitle = evt.newValue ?? string.Empty));
            section.Add(BuildBrowserEditorFieldRow("Title", titleField));

            var authorField = BuildBrowserEditorTextField(asset.author ?? string.Empty);
            authorField.RegisterValueChangedCallback(evt =>
                UpdateBrowserGraphMetadata(asset, "Edit Graph Author", graph => graph.author = evt.newValue ?? string.Empty));
            section.Add(BuildBrowserEditorFieldRow("Author", authorField));

            var descriptionField = BuildBrowserEditorTextField(asset.description ?? string.Empty, multiline: true);
            descriptionField.style.minHeight = 72;
            descriptionField.RegisterValueChangedCallback(evt =>
                UpdateBrowserGraphMetadata(asset, "Edit Graph Description", graph => graph.description = evt.newValue ?? string.Empty));
            section.Add(BuildBrowserEditorFieldRow("Description", descriptionField));

            return section;
        }

        private VisualElement BuildGraphLocalizationSummary(DialogGraph asset)
        {
            var section = new VisualElement();
            section.AddToClassList("dgs-graph-browser-section");
            section.Add(BuildInspectorSectionTitle("Localization"));

            var localizationStatus = EvaluateGraphLocalizationStatus(asset);
            var statusLabel = BuildInspectorLabel(localizationStatus.Text);
            statusLabel.EnableInClassList("dgs-graph-browser-localization-warning", localizationStatus.HasWarning);
            section.Add(statusLabel);

            if (localizationStatus.HasWarning)
            {
                section.Add(CreateGraphBrowserWarningBanner(localizationStatus.WarningMessage));
            }

            var openButton = new Button(() => DialogSystemMainWindow.OpenLocalizationForGraph(asset))
            {
                text = "Open Localization"
            };
            openButton.AddToClassList("dlg-btn");
            openButton.AddToClassList("secondary");
            openButton.style.marginTop = 6;
            section.Add(openButton);

            return section;
        }

        private static string BuildLocalizationStatusText(DialogGraph graph)
            => EvaluateGraphLocalizationStatus(graph).Text;

        private static GraphLocalizationStatus EvaluateGraphLocalizationStatus(DialogGraph graph)
        {
            if (graph == null)
            {
                return GraphLocalizationStatus.Info("Localization: graph unavailable.");
            }

            var registry = new DialogLocalizationRegistryService();
            var allTables = registry.GetAllTables();
            var targetLocales = allTables
                .Where(table => table != null && !table.IsSourceLanguage && !string.IsNullOrWhiteSpace(table.LocaleCode))
                .Select(table => table.LocaleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (targetLocales.Count == 0)
            {
                return GraphLocalizationStatus.Info("Localization: no target languages configured.");
            }

            var localeKeys = CollectGraphLocalizationKeys(graph);
            if (localeKeys.Count == 0)
            {
                return GraphLocalizationStatus.Info("Localization: no localization keys found.");
            }

            var missingEntryCount = 0;
            var readyLocales = targetLocales
                .Where(locale =>
                {
                    var table = allTables.FirstOrDefault(candidate =>
                        candidate != null &&
                        !candidate.IsSourceLanguage &&
                        string.Equals(candidate.LocaleCode, locale, StringComparison.OrdinalIgnoreCase));
                    if (table == null)
                    {
                        missingEntryCount += localeKeys.Count;
                        return false;
                    }

                    var localeMissingCount = localeKeys.Count(key => string.IsNullOrWhiteSpace(table.TryResolve(key)));
                    missingEntryCount += localeMissingCount;
                    return localeMissingCount == 0;
                })
                .ToList();

            if (readyLocales.Count == 0)
            {
                return GraphLocalizationStatus.Warning(
                    $"Localization: 0/{targetLocales.Count} languages ready.",
                    $"All {targetLocales.Count} target language(s) still need translations for this graph ({missingEntryCount} missing entr{(missingEntryCount == 1 ? "y" : "ies")}).");
            }

            var display = readyLocales
                .Take(4)
                .Select(LocalizationTableView.FormatLocaleDisplayName)
                .ToList();
            var suffix = readyLocales.Count > display.Count
                ? $" +{readyLocales.Count - display.Count} more"
                : string.Empty;

            var text = $"Localization ready: {string.Join(", ", display)}{suffix} ({readyLocales.Count}/{targetLocales.Count}).";
            if (readyLocales.Count == targetLocales.Count)
            {
                return GraphLocalizationStatus.Info(text);
            }

            var missingLocales = targetLocales.Count - readyLocales.Count;
            return GraphLocalizationStatus.Warning(
                text,
                $"{missingLocales}/{targetLocales.Count} target language(s) still have missing translations for this graph ({missingEntryCount} missing entr{(missingEntryCount == 1 ? "y" : "ies")}).");
        }

        private static List<string> CollectGraphLocalizationKeys(DialogGraph graph)
        {
            var keys = new List<string>();
            if (graph == null)
            {
                return keys;
            }

            foreach (var node in graph.nodes ?? Enumerable.Empty<DialogNode>())
            {
                if (node == null)
                {
                    continue;
                }

                AddLocalizationKey(keys, node.speakerNameLocaleKey);
                AddLocalizationKey(keys, node.questionTextLocaleKey);
            }

            foreach (var node in graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (node == null)
                {
                    continue;
                }

                AddLocalizationKey(keys, node.textLocaleKey);
                foreach (var choice in node.choices ?? Enumerable.Empty<Choice>())
                {
                    AddLocalizationKey(keys, choice?.answerTextLocaleKey);
                }
            }

            return keys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddLocalizationKey(List<string> keys, string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys.Add(key.Trim());
            }
        }

        private VisualElement BuildGraphCategoryEditor(DialogGraph asset)
        {
            var section = new VisualElement();
            section.AddToClassList("dgs-graph-browser-section");
            section.Add(BuildInspectorSectionTitle("Category Assignment"));

            var catalogPaths = DialogGraphCategoryCatalogService.GetOrderedCategories()
                .Select(category => category.path)
                .ToList();
            var primaryChoices = new List<string> { "None" };
            primaryChoices.AddRange(catalogPaths);

            var currentPrimary = DialogGraphBrowserUtility.NormalizeCategoryPath(asset.primaryCategory);
            var selectedPrimary = string.IsNullOrEmpty(currentPrimary) ? "None" : currentPrimary;
            var selectedIndex = Math.Max(0, primaryChoices.FindIndex(choice =>
                string.Equals(choice, selectedPrimary, StringComparison.OrdinalIgnoreCase)));
            var primaryPopup = new PopupField<string>(primaryChoices, selectedIndex);
            primaryPopup.AddToClassList("dlg-popup");
            primaryPopup.AddToClassList("dgs-graph-browser-form-control");
            primaryPopup.RegisterValueChangedCallback(evt =>
            {
                var nextPrimary = evt.newValue == "None" ? string.Empty : evt.newValue;
                UpdateBrowserGraphMetadata(asset, "Assign Primary Category", graph =>
                {
                    graph.primaryCategory = nextPrimary;
                    var assigned = DialogGraphBrowserUtility.NormalizeCategoryList(graph.categories);
                    if (!string.IsNullOrWhiteSpace(nextPrimary) && !assigned.Contains(nextPrimary, StringComparer.OrdinalIgnoreCase))
                    {
                        assigned.Add(nextPrimary);
                    }

                    graph.categories = assigned
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                });
            });
            section.Add(BuildBrowserEditorFieldRow("Primary", primaryPopup));

            var assigned = DialogGraphBrowserUtility.CollectAssignedCategories(asset.primaryCategory, asset.categories);
            if (assigned.Count == 0)
            {
                section.Add(BuildInspectorLabel("No categories assigned."));
            }
            else
            {
                foreach (var categoryPath in assigned)
                {
                    section.Add(BuildAssignedCategoryRow(asset, categoryPath));
                }
            }

            var available = catalogPaths
                .Where(path => !assigned.Contains(path, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (available.Count > 0)
            {
                var addRow = new VisualElement();
                addRow.AddToClassList("dgs-graph-browser-inline-row");

                var addPopup = new PopupField<string>(available, 0);
                addPopup.AddToClassList("dlg-popup");
                addPopup.AddToClassList("dgs-graph-browser-form-control");
                addPopup.style.flexGrow = 1;
                addRow.Add(addPopup);

                var addButton = new Button(() =>
                {
                    UpdateBrowserGraphMetadata(asset, "Add Graph Category", graph =>
                    {
                        var categories = DialogGraphBrowserUtility.NormalizeCategoryList(graph.categories);
                        categories.Add(addPopup.value);
                        graph.categories = categories
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    });
                })
                {
                    text = "Add"
                };
                addButton.AddToClassList("dlg-btn");
                addButton.AddToClassList("secondary");
                addButton.AddToClassList("dgs-graph-browser-inline-button");
                addRow.Add(addButton);
                section.Add(BuildBrowserEditorFieldRow("Add", addRow));
            }

            return section;
        }

        private VisualElement BuildAssignedCategoryRow(DialogGraph asset, string categoryPath)
        {
            var row = new VisualElement();
            row.AddToClassList("dgs-graph-browser-inline-row");

            var chip = CreateCategoryChip(categoryPath, ResolveCategoryColor(categoryPath));
            chip.style.flexGrow = 1;
            row.Add(chip);

            var removeButton = new Button(() =>
            {
                UpdateBrowserGraphMetadata(asset, "Remove Graph Category", graph =>
                {
                    graph.categories = DialogGraphBrowserUtility.NormalizeCategoryList(graph.categories)
                        .Where(value => !string.Equals(value, categoryPath, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (string.Equals(DialogGraphBrowserUtility.NormalizeCategoryPath(graph.primaryCategory), categoryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        graph.primaryCategory = string.Empty;
                    }
                });
            })
            {
                text = "Remove"
            };
            removeButton.AddToClassList("dlg-btn");
            removeButton.AddToClassList("danger");
            row.Add(removeButton);
            return row;
        }

        private VisualElement BuildCategoryInspector(string categoryPath)
        {
            var normalizedPath = DialogGraphBrowserUtility.NormalizeCategoryPath(categoryPath);
            var definition = DialogGraphCategoryCatalogService.FindByPath(normalizedPath);
            if (definition == null)
            {
                return BuildScopeSummaryCard("Missing Category", "The selected category no longer exists in the catalog.");
            }

            var usageCount = _browserItems.Count(item =>
                DialogGraphBrowserUtility.CollectAssignedCategories(item.PrimaryCategory, item.Categories)
                    .Any(value => string.Equals(value, normalizedPath, StringComparison.OrdinalIgnoreCase) ||
                                  value.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase)));

            var card = new VisualElement();
            card.AddToClassList("dgs-graph-browser-card");

            var title = new Label(DialogGraphBrowserUtility.GetCategoryLeafName(normalizedPath));
            title.AddToClassList("dgs-graph-browser-card-title");
            card.Add(title);

            var pathLabel = new Label(normalizedPath);
            pathLabel.AddToClassList("dgs-graph-browser-card-subtitle");
            card.Add(pathLabel);

            card.Add(BuildInspectorLabel($"{usageCount} graph(s) currently match this category path."));

            var colorField = new ColorField("Color") { value = definition.color };
            colorField.RegisterValueChangedCallback(evt =>
            {
                DialogGraphCategoryCatalogService.UpdateCategoryColor(normalizedPath, evt.newValue);
                RefreshHomeBrowser();
            });
            card.Add(colorField);

            var deleteButton = new Button(() => DeleteBrowserCategory(normalizedPath))
            {
                text = "Delete Category"
            };
            deleteButton.AddToClassList("dlg-btn");
            deleteButton.AddToClassList("danger");
            card.Add(deleteButton);
            return card;
        }

        private VisualElement BuildFolderInspector(string folderPath)
        {
            var normalizedFolder = DialogGraphBrowserUtility.NormalizeFolderPath(folderPath);
            var matchingGraphs = _browserItems.Count(item =>
                item.FolderPath.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase) ||
                item.FolderPath.StartsWith(normalizedFolder + "/", StringComparison.OrdinalIgnoreCase));

            var card = BuildScopeSummaryCard(
                Path.GetFileName(normalizedFolder),
                $"{matchingGraphs} graph(s) are currently stored under {normalizedFolder}.\nFolder filters are read-only and independent from browser categories.");

            var removeButton = new Button(() => RemoveBrowserFolder(normalizedFolder, matchingGraphs))
            {
                text = "Remove From Browser"
            };
            removeButton.AddToClassList("dlg-btn");
            removeButton.AddToClassList("danger");
            removeButton.style.marginTop = 10;
            card.Add(removeButton);

            return card;
        }

        private VisualElement BuildScopeSummaryCard(string titleText, string message)
        {
            var card = new VisualElement();
            card.AddToClassList("dgs-graph-browser-card");

            var title = new Label(titleText);
            title.AddToClassList("dgs-graph-browser-card-title");
            card.Add(title);

            card.Add(BuildInspectorLabel(message));
            return card;
        }

        private void UpdateBrowserGraphMetadata(DialogGraph asset, string undoLabel, Action<DialogGraph> applyChanges)
        {
            if (asset == null || applyChanges == null)
            {
                return;
            }

            Undo.RecordObject(asset, undoLabel);
            applyChanges(asset);
            asset.primaryCategory = DialogGraphBrowserUtility.NormalizeCategoryPath(asset.primaryCategory);
            asset.categories = DialogGraphBrowserUtility.NormalizeCategoryList(asset.categories);
            asset.lastModifiedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            asset.editorVersion = Application.unityVersion;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
            RefreshHomeBrowser();
            SelectBrowserGraph(asset.name);
        }

        private void OnClickAddBrowserFolder()
        {
            var path = EditorUtility.OpenFolderPanel("Select Graph Folder", Application.dataPath, "");
            if (string.IsNullOrEmpty(path)) return;

            var assetRelative = AbsoluteToAssetFolderPath(path);
            if (string.IsNullOrEmpty(assetRelative))
            {
                EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside the project Assets directory.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(assetRelative))
            {
                AssetDatabase.Refresh();
                if (!AssetDatabase.IsValidFolder(assetRelative))
                {
                    EditorUtility.DisplayDialog(
                        "Invalid Folder",
                        "Unity has not imported this folder yet. Create or refresh it under Assets, then try again.",
                        "OK");
                    return;
                }
            }

            var custom = GetCustomFolders();
            var hidden = GetHiddenFolders();
            if (hidden.Remove(assetRelative))
            {
                SaveHiddenFolders(hidden);
            }

            if (custom.Add(assetRelative))
            {
                SaveCustomFolders(custom);
            }

            _browserScopeKind = DialogGraphBrowserScopeKind.Folder;
            _browserScopeValue = assetRelative;
            RefreshHomeBrowser();
        }

        private static string AbsoluteToAssetFolderPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            var normalized = absolutePath.Replace('\\', '/').TrimEnd('/');
            var dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            if (string.Equals(normalized, dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            if (!normalized.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return "Assets" + normalized.Substring(dataPath.Length);
        }

        private HashSet<string> GetCustomFolders()
        {
            var raw = EditorPrefs.GetString(PrefKeyCustomFolders, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return new HashSet<string>(raw.Split('|', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        }

        private void SaveCustomFolders(IEnumerable<string> folders)
        {
            EditorPrefs.SetString(PrefKeyCustomFolders, string.Join("|", folders.OrderBy(f => f)));
        }

        private HashSet<string> GetHiddenFolders()
        {
            var raw = EditorPrefs.GetString(PrefKeyHiddenFolders, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return new HashSet<string>(raw.Split('|', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        }

        private void SaveHiddenFolders(IEnumerable<string> folders)
        {
            EditorPrefs.SetString(PrefKeyHiddenFolders, string.Join("|", folders.OrderBy(f => f)));
        }

        private void RemoveBrowserFolder(string folderPath, int matchingGraphCount)
        {
            var normalized = DialogGraphBrowserUtility.NormalizeFolderPath(folderPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (matchingGraphCount > 0 &&
                !EditorUtility.DisplayDialog(
                    "Remove Folder From Browser",
                    $"'{normalized}' contains {matchingGraphCount} graph(s).\n\nRemove it from the browser folder list? This does not delete the folder or any graph assets.",
                    "Remove From List",
                    "Cancel"))
            {
                return;
            }

            var custom = GetCustomFolders();
            custom.Remove(normalized);
            SaveCustomFolders(custom);

            var hidden = GetHiddenFolders();
            hidden.Add(normalized);
            SaveHiddenFolders(hidden);

            _browserScopeKind = DialogGraphBrowserScopeKind.All;
            _browserScopeValue = string.Empty;
            _browserSelectedGraphName = string.Empty;
            RefreshHomeBrowser();
        }

        private void OnClickCreateBrowserGraphDefault()
        {
            OnClickCreateBrowserGraph(_browserCreateNameField.value);
        }

        private void OnClickCreateBrowserGraph(string rawName)
        {
            var finalName = (rawName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(finalName))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Please enter a valid graph name.", "OK");
                return;
            }

            var allGraphs = DialogGraphAssetPaths.GetVisibleGraphNames();
            if (allGraphs.Contains(finalName))
            {
                var choice = EditorUtility.DisplayDialogComplex(
                    "Graph Already Exists",
                    $"A graph named '{finalName}' already exists.\n\nWhat would you like to do?",
                    "Open Existing",
                    "Cancel",
                    "Overwrite");
                if (choice == 0)
                {
                    LoadGraphByName(finalName);
                    return;
                }
                if (choice == 1) return;
            }

            var targetFolder = string.Empty;
            if (_browserScopeKind == DialogGraphBrowserScopeKind.Folder)
                targetFolder = _browserScopeValue;

            var selectedCategory = _browserScopeKind == DialogGraphBrowserScopeKind.Category
                ? _browserScopeValue
                : string.Empty;

            CreateBrowserGraphAsset(finalName, selectedCategory, targetFolder);
            LoadGraphByName(finalName);
        }

        private void CreateBrowserGraphAsset(string graphName, string selectedCategory, string folderPath = "")
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                folderPath = TextResources.GRAPHS_FOLDER;

            DialogGraphAssetPaths.EnsureFolderExists(folderPath);
            
            var path = Path.Combine(folderPath, $"{graphName}.asset").Replace('\\', '/');
            var existing = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.AssignGraphGuidIfMissing(Guid.NewGuid().ToString("N"));
            graph.MarkSchemaCurrentForMigration();

            var normalizedCategory = DialogGraphBrowserUtility.NormalizeCategoryPath(selectedCategory);
            graph.primaryCategory = normalizedCategory;
            graph.categories = string.IsNullOrWhiteSpace(normalizedCategory)
                ? new List<string>()
                : new List<string> { normalizedCategory };
            graph.lastModifiedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            graph.editorVersion = Application.unityVersion;

            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
        }

        private void OnClickAddBrowserCategory()
        {
            var parentPath = _browserNewCategoryParentPopup?.value == "Root"
                ? string.Empty
                : _browserNewCategoryParentPopup?.value ?? string.Empty;
            var created = DialogGraphCategoryCatalogService.AddCategory(
                _browserNewCategoryNameField?.value ?? string.Empty,
                parentPath,
                _browserNewCategoryColorField != null
                    ? _browserNewCategoryColorField.value
                    : DialogGraphCategoryCatalogService.GetSuggestedColor(0));

            if (created == null)
            {
                EditorUtility.DisplayDialog("Category", "Enter a valid category name first.", "OK");
                return;
            }

            _browserNewCategoryNameField?.SetValueWithoutNotify(string.Empty);
            _browserScopeKind = DialogGraphBrowserScopeKind.Category;
            _browserScopeValue = created.path;
            _browserSelectedGraphName = string.Empty;
            RefreshHomeBrowser();
        }

        private void DeleteBrowserCategory(string categoryPath)
        {
            var normalizedPath = DialogGraphBrowserUtility.NormalizeCategoryPath(categoryPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Delete Category",
                    $"Delete category '{normalizedPath}'?\nAssigned graphs will have this category removed from their browser metadata.",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            foreach (var item in _browserItems)
            {
                var graph = DialogGraphAssetPaths.LoadGraphAsset(item.GraphName);
                if (graph == null)
                {
                    continue;
                }

                var assigned = DialogGraphBrowserUtility.CollectAssignedCategories(graph.primaryCategory, graph.categories);
                if (!assigned.Any(value =>
                        string.Equals(value, normalizedPath, StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                Undo.RecordObject(graph, "Delete Graph Category");
                graph.categories = assigned
                    .Where(value =>
                        !string.Equals(value, normalizedPath, StringComparison.OrdinalIgnoreCase) &&
                        !value.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var normalizedPrimary = DialogGraphBrowserUtility.NormalizeCategoryPath(graph.primaryCategory);
                if (string.Equals(normalizedPrimary, normalizedPath, StringComparison.OrdinalIgnoreCase) ||
                    normalizedPrimary.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase))
                {
                    graph.primaryCategory = string.Empty;
                }

                graph.lastModifiedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                graph.editorVersion = Application.unityVersion;
                EditorUtility.SetDirty(graph);
            }

            DialogGraphCategoryCatalogService.RemoveCategory(normalizedPath);
            AssetDatabase.SaveAssets();
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
            _browserScopeKind = DialogGraphBrowserScopeKind.All;
            _browserScopeValue = string.Empty;
            _browserSelectedGraphName = string.Empty;
            RefreshHomeBrowser();
        }

        private void SelectBrowserGraph(string graphName)
        {
            _browserSelectedGraphName = graphName ?? string.Empty;
            RebuildHomeListPane();
            RebuildHomeInspectorPane();
        }

        private void EnsureDefaultBrowserSelection()
        {
            var visibleItems = GetVisibleBrowserItems();
            if (visibleItems.Count == 0)
            {
                _browserSelectedGraphName = string.Empty;
                return;
            }

            if (visibleItems.Any(item => string.Equals(item.GraphName, _browserSelectedGraphName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _browserSelectedGraphName = visibleItems[0].GraphName;
        }

        private void ResetBrowserFilters()
        {
            _browserSearchQuery = string.Empty;
            _browserFavoritesOnly = false;
            _browserRecentOnly = false;
            _browserScopeKind = DialogGraphBrowserScopeKind.All;
            _browserScopeValue = string.Empty;
            _browserSelectedGraphName = string.Empty;
            RefreshHomeBrowser();
        }

        private List<DialogGraphBrowserRecord> GetScopeFilteredItems()
        {
            var filters = new DialogGraphBrowserFilters
            {
                SearchQuery = _browserSearchQuery,
                FavoritesOnly = _browserFavoritesOnly,
                RecentOnly = _browserRecentOnly
            };

            return _browserItems
                .Where(item => DialogGraphBrowserUtility.MatchesFilters(item, filters))
                .ToList();
        }

        private List<DialogGraphBrowserRecord> GetVisibleBrowserItems()
        {
            var filters = new DialogGraphBrowserFilters
            {
                SearchQuery = _browserSearchQuery,
                FavoritesOnly = _browserFavoritesOnly,
                RecentOnly = _browserRecentOnly
            };

            switch (_browserScopeKind)
            {
                case DialogGraphBrowserScopeKind.Uncategorized:
                    filters.UncategorizedOnly = true;
                    break;
                case DialogGraphBrowserScopeKind.Category:
                    filters.CategoryPath = _browserScopeValue;
                    break;
                case DialogGraphBrowserScopeKind.Folder:
                    filters.FolderPath = _browserScopeValue;
                    break;
            }

            return _browserItems
                .Where(item => DialogGraphBrowserUtility.MatchesFilters(item, filters))
                .OrderByDescending(item => item.IsFavorite)
                .ThenByDescending(item => item.IsRecent)
                .ThenByDescending(item => item.LastModifiedUtc)
                .ThenBy(item => item.DisplayTitle, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetBrowserSummaryText(IReadOnlyCollection<DialogGraphBrowserRecord> items)
        {
            if (items == null || items.Count == 0)
            {
                return "No graph assets match the current browser filters.";
            }

            var favorites = items.Count(item => item.IsFavorite);
            var recents = items.Count(item => item.IsRecent);
            return $"{items.Count} graph(s) visible  |  {favorites} favorite(s)  |  {recents} recent";
        }

        private static string BuildLauncherLocalizationWarning()
        {
            var warningCount = DialogGraphAssetPaths.LoadAllGraphAssets()
                .Count(graph => EvaluateGraphLocalizationStatus(graph).HasWarning);

            if (warningCount == 0)
            {
                return string.Empty;
            }

            return $"{warningCount} graph{(warningCount == 1 ? string.Empty : "s")} have incomplete localization translations.";
        }

        private static VisualElement CreateGraphBrowserWarningBanner(string message)
        {
            var banner = new VisualElement();
            banner.AddToClassList("dgs-graph-browser-warning-banner");

            var icon = DialogGraphIconManager.CreateImage(
                DialogGraphIconId.StatusWarning,
                "dgs-icon--sm",
                "dgs-icon--warning");
            icon.AddToClassList("dgs-graph-browser-warning-icon");
            banner.Add(icon);

            var label = new Label(message ?? string.Empty);
            label.AddToClassList("dgs-graph-browser-warning-label");
            banner.Add(label);

            return banner;
        }

        private static string BuildBrowserMetadataLine(DialogGraphBrowserRecord item)
        {
            var modified = item.LastModifiedUtc == DateTime.MinValue
                ? "Modified: unknown"
                : "Modified: " + item.LastModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            return string.IsNullOrWhiteSpace(item.Author)
                ? modified
                : $"Author: {item.Author}  |  {modified}";
        }

        private static Color ResolveBrowserAccentColor(DialogGraphBrowserRecord item)
        {
            var primary = DialogGraphBrowserUtility.NormalizeCategoryPath(item.PrimaryCategory);
            if (!string.IsNullOrEmpty(primary))
            {
                return ResolveCategoryColor(primary);
            }

            return item.IsFavorite
                ? new Color(0.83f, 0.66f, 0.16f, 1f)
                : new Color(0.25f, 0.29f, 0.35f, 1f);
        }

        private static Color ResolveCategoryColor(string categoryPath)
        {
            return DialogGraphCategoryCatalogService.FindByPath(categoryPath)?.color
                   ?? new Color(0.33f, 0.39f, 0.49f, 1f);
        }

        private static VisualElement BuildCatalogSectionHeader(string title)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;
            row.style.marginTop = 12;

            var label = new Label(title);
            label.AddToClassList("dgs-graph-browser-section-title");
            label.style.marginBottom = 0;
            row.Add(label);

            return row;
        }

        private Button CreateCatalogScopeButton(string label, DialogGraphBrowserScopeKind kind, string value, int count)
        {
            var button = new Button(() =>
            {
                _browserScopeKind = kind;
                _browserScopeValue = value ?? string.Empty;
                _browserSelectedGraphName = string.Empty;
                RebuildHomeCatalogPane();
                RebuildHomeListPane();
                RebuildHomeInspectorPane();
            });

            button.AddToClassList("dgs-graph-browser-scope-button");
            if (_browserScopeKind == kind &&
                string.Equals(_browserScopeValue, value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                button.AddToClassList("active");
            }

            button.text = $"{label} ({count})";
            return button;
        }

        private Button CreateCatalogPathButton(
            string label,
            string fullPath,
            int depth,
            Color accentColor,
            int count,
            DialogGraphBrowserScopeKind kind)
        {
            var button = CreateCatalogScopeButton(label, kind, fullPath, count);
            button.style.paddingLeft = 14 + depth * 12;
            button.style.borderLeftWidth = 4;
            button.style.borderLeftColor = accentColor;
            button.style.backgroundColor = new Color(0.18f, 0.2f, 0.25f, 0.4f);
            button.style.marginTop = 2;
            button.style.marginBottom = 2;
            button.style.height = 24;
            button.style.fontSize = 11;
            button.tooltip = fullPath;
            
            if (_browserScopeKind == kind && string.Equals(_browserScopeValue, fullPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                button.style.backgroundColor = new Color(0.25f, 0.35f, 0.5f, 0.6f);
            }
            
            return button;
        }

        private static VisualElement CreateCatalogHint(string message)
        {
            var label = new Label(message);
            label.AddToClassList("dgs-graph-browser-hint");
            return label;
        }

        private static Label BuildInspectorSectionTitle(string title)
        {
            var label = new Label(title);
            label.AddToClassList("dgs-graph-browser-section-title");
            return label;
        }

        private static Label BuildInspectorLabel(string message)
        {
            var label = new Label(message);
            label.AddToClassList("dgs-graph-browser-card-subtitle");
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static TextField BuildBrowserEditorTextField(string value, bool multiline = false)
        {
            return new TextField
            {
                value = value,
                isDelayed = true,
                multiline = multiline
            };
        }

        private static VisualElement BuildBrowserEditorFieldRow(string labelText, VisualElement control)
        {
            var row = new VisualElement();
            row.AddToClassList("dgs-graph-browser-form-row");

            var label = new Label(labelText);
            label.AddToClassList("dgs-graph-browser-form-label");
            row.Add(label);

            var controlWrap = new VisualElement();
            controlWrap.AddToClassList("dgs-graph-browser-form-control");
            controlWrap.Add(control);
            row.Add(controlWrap);
            return row;
        }

        private static VisualElement CreateCategoryChip(string categoryPath, Color color)
        {
            var chip = new VisualElement();
            chip.AddToClassList("dgs-graph-browser-chip");

            var swatch = new VisualElement();
            swatch.AddToClassList("dgs-graph-browser-chip-swatch");
            swatch.style.backgroundColor = color;
            chip.Add(swatch);

            var label = new Label(DialogGraphBrowserUtility.GetCategoryLeafName(categoryPath));
            label.AddToClassList("dgs-graph-browser-chip-label");
            label.tooltip = categoryPath;
            chip.Add(label);
            return chip;
        }

        private Button CreateFavoriteButton(string assetPath, bool isFavorite)
        {
            var button = new Button(() => ToggleGraphFavorite(assetPath));
            button.AddToClassList("dgs-favorite-button");
            button.EnableInClassList("dgs-favorite-button--active", isFavorite);
            button.tooltip = isFavorite ? "Remove from favorites" : "Add to favorites";

            var icon = new Image
            {
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.AddToClassList("dgs-favorite-button-icon");

            var texture = LoadFavoriteIcon(isFavorite);
            if (texture != null)
            {
                icon.image = texture;
                ApplyFavoriteButtonIconTint(icon, isFavorite, isHovered: false);
                button.RegisterCallback<MouseEnterEvent>(_ => ApplyFavoriteButtonIconTint(icon, isFavorite, isHovered: true));
                button.RegisterCallback<MouseLeaveEvent>(_ => ApplyFavoriteButtonIconTint(icon, isFavorite, isHovered: false));
            }
            else
            {
                button.text = isFavorite ? "★" : "☆";
            }

            button.Add(icon);
            return button;
        }

        private static Texture2D LoadFavoriteIcon(bool isFavorite)
        {
            var assetPath = isFavorite ? FavoriteStarFilledIconPath : FavoriteStarEmptyIconPath;
            // Use PNG fallback since Vector Graphics is no longer required
            var pngPath = System.IO.Path.ChangeExtension(assetPath, ".png");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            if (texture != null)
                return texture;

            // Try loading from SVG's sub-assets as a last resort (may still work if Vector Graphics is installed)
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            return assets?.OfType<Texture2D>().FirstOrDefault();
        }

        private static void ApplyFavoriteButtonIconTint(Image icon, bool isFavorite, bool isHovered)
        {
            if (icon == null)
            {
                return;
            }

            var tint = isFavorite
                ? (isHovered ? new Color(0.95f, 0.61f, 0.07f, 1f) : new Color(0.95f, 0.77f, 0.06f, 1f))
                : (isHovered ? new Color(0.86f, 0.89f, 0.93f, 1f) : new Color(0.49f, 0.53f, 0.6f, 1f));

            icon.tintColor = tint;
            icon.style.unityBackgroundImageTintColor = tint;
        }

        private static HashSet<string> GetFavoriteGraphKeys()
        {
            var raw = EditorPrefs.GetString(PrefKeyFavoriteGraphs, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value)),
                StringComparer.OrdinalIgnoreCase);
        }

        private void ToggleGraphFavorite(string assetPath)
        {
            var favorites = GetFavoriteGraphKeys();
            if (!favorites.Add(assetPath))
            {
                favorites.Remove(assetPath);
            }

            EditorPrefs.SetString(PrefKeyFavoriteGraphs, string.Join("|", favorites.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)));
            RefreshHomeBrowser();
        }

        private sealed class GraphLocalizationStatus
        {
            private GraphLocalizationStatus(string text, bool hasWarning, string warningMessage)
            {
                Text = text ?? string.Empty;
                HasWarning = hasWarning;
                WarningMessage = warningMessage ?? string.Empty;
            }

            public string Text { get; }
            public bool HasWarning { get; }
            public string WarningMessage { get; }

            public static GraphLocalizationStatus Info(string text)
                => new GraphLocalizationStatus(text, false, string.Empty);

            public static GraphLocalizationStatus Warning(string text, string warningMessage)
                => new GraphLocalizationStatus(text, true, warningMessage);
        }
    }
}
