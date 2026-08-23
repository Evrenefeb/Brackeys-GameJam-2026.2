using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Utils;
using DialogSystem.Runtime.Variables;

namespace DialogSystem.EditorTools.ExportImport
{
    public class DialogJsonIOWindow : EditorWindow
    {
        #region ---------------- Tabs ----------------
        [SerializeField] private bool doDebug = false;

        private enum Tab { Export, Import }
        private Tab _tab = Tab.Export;
        #endregion

        #region ---------------- Menu ----------------
        // Legacy standalone menu entry is intentionally disabled; use the main window import/export tab.
        [System.Obsolete("Use DialogSystemMainWindow Import/Export tab instead.")]
        public static void Open()
        {
            var win = GetWindow<DialogJsonIOWindow>("Legacy Dialogue JSON I/O");
            win.minSize = new Vector2(520, 420);
            win.Show();
        }
        #endregion

        #region ---------------- EditorPrefs Keys ----------------
        private const string K_PREFS_KEY_ROOT      = "DialogJsonIOWindow";
        private const string K_PREFS_EXPORT_FOLDER = K_PREFS_KEY_ROOT + ".exportFolder";
        private const string K_PREFS_IMPORT_FOLDER = K_PREFS_KEY_ROOT + ".importFolder";
        #endregion

        #region ---------------- Export State ----------------
        private DialogGraph _exportGraph;
        private string _exportFileName        = "";
        private string _exportFolder          = TextResources.EXPORT_FOLDER;
        private bool   _exportPretty          = true;
        private bool   _exportIncludePositions = true;
        #endregion

        #region ---------------- Import State ----------------
        private TextAsset _importJsonAsset;
        private string _importExternalPath = "";
        private string _importFolder       = TextResources.IMPORT_FOLDER;
        private string _importTargetName   = "ImportedConversation";

        private string           _loadedJson  = "";
        private string           _jsonError   = "";
        private DialogGraphExport _previewDto;
        #endregion

        #region ---------------- UI References ----------------
        private VisualElement _contentArea;
        private Button        _exportTabBtn;
        private Button        _importTabBtn;
        private Button        _exportBtn;
        #endregion

        #region ---------------- Unity ----------------
        private void OnEnable()
        {
            _exportFolder = EditorPrefs.GetString(K_PREFS_EXPORT_FOLDER, _exportFolder);
            _importFolder = EditorPrefs.GetString(K_PREFS_IMPORT_FOLDER, _importFolder);

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null) rootVisualElement.styleSheets.Add(ss);

            BuildUI();
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(K_PREFS_EXPORT_FOLDER, _exportFolder);
            EditorPrefs.SetString(K_PREFS_IMPORT_FOLDER, _importFolder);
        }
        #endregion

        #region ---------------- UI Build ----------------
        private void BuildUI()
        {
            rootVisualElement.Clear();

            var root = new VisualElement();
            root.style.flexGrow      = 1;
            root.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(root);

            root.Add(BuildTabBar());

            _contentArea = new ScrollView(ScrollViewMode.Vertical);
            ((ScrollView)_contentArea).horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _contentArea.style.flexGrow = 1;
            root.Add(_contentArea);

            root.Add(BuildDropZone());

            LoadTabContent();
        }

        private VisualElement BuildTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("dlg-io-tab-bar");

            _exportTabBtn = new Button(() => SwitchTab(Tab.Export)) { text = "Export" };
            _exportTabBtn.AddToClassList("dlg-io-tab-btn");

            _importTabBtn = new Button(() => SwitchTab(Tab.Import)) { text = "Import" };
            _importTabBtn.AddToClassList("dlg-io-tab-btn");

            bar.Add(_exportTabBtn);
            bar.Add(_importTabBtn);
            return bar;
        }

        private void SwitchTab(Tab newTab)
        {
            if (_tab == newTab) return;
            _tab = newTab;
            LoadTabContent();
        }

        private void LoadTabContent()
        {
            _exportTabBtn?.EnableInClassList("active", _tab == Tab.Export);
            _importTabBtn?.EnableInClassList("active", _tab == Tab.Import);

            _contentArea.Clear();

            var panel = _tab == Tab.Export ? BuildExportPanel() : BuildImportPanel();
            panel.style.paddingLeft   = 12;
            panel.style.paddingRight  = 12;
            panel.style.paddingTop    = 12;
            panel.style.paddingBottom = 8;
            _contentArea.Add(panel);
        }
        #endregion

        #region ---------------- Export Panel ----------------
        private VisualElement BuildExportPanel()
        {
            var root = new VisualElement();
            var card = MakeCard("Export  DialogGraph → JSON");

            // Graph object field
            var graphField = new ObjectField("Graph") { objectType = typeof(DialogGraph), value = _exportGraph };
            graphField.AddToClassList("dlg-textfield");
            graphField.style.marginBottom = 6;
            graphField.RegisterValueChangedCallback(evt =>
            {
                _exportGraph = evt.newValue as DialogGraph;
                UpdateExportButton();
            });
            card.Add(graphField);

            // File name
            var nameField = new TextField("File Name") { value = _exportFileName };
            nameField.AddToClassList("dlg-textfield");
            nameField.style.marginBottom = 6;
            nameField.RegisterValueChangedCallback(evt => _exportFileName = evt.newValue);
            card.Add(nameField);

            // Folder picker
            card.Add(BuildFolderRow("Folder", () => _exportFolder, v =>
            {
                _exportFolder = v;
                UpdateExportButton();
            }, "Choose export folder (inside Assets)"));

            // Toggles
            var prettyToggle = new Toggle("Pretty Print") { value = _exportPretty };
            prettyToggle.style.marginBottom = 4;
            prettyToggle.RegisterValueChangedCallback(evt => _exportPretty = evt.newValue);
            card.Add(prettyToggle);

            var posToggle = new Toggle("Include Node Positions") { value = _exportIncludePositions };
            posToggle.style.marginBottom = 8;
            posToggle.RegisterValueChangedCallback(evt => _exportIncludePositions = evt.newValue);
            card.Add(posToggle);

            // Action buttons
            var btnRow = new VisualElement();
            btnRow.style.flexDirection  = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            btnRow.style.marginTop      = 4;

            var useSelectedBtn = new Button(() =>
            {
                var sel = Selection.activeObject as DialogGraph;
                if (sel != null)
                {
                    _exportGraph    = sel;
                    graphField.value = sel;
                    UpdateExportButton();
                }
            })
            { text = "Use Selected" };
            useSelectedBtn.AddToClassList("dlg-btn");
            useSelectedBtn.AddToClassList("secondary");
            useSelectedBtn.style.marginRight = 6;

            _exportBtn = new Button(DoExport) { text = "Export" };
            _exportBtn.AddToClassList("dlg-btn");
            _exportBtn.AddToClassList("primary");
            UpdateExportButton();

            btnRow.Add(useSelectedBtn);
            btnRow.Add(_exportBtn);
            card.Add(btnRow);

            root.Add(card);
            return root;
        }

        private void UpdateExportButton()
        {
            if (_exportBtn == null) return;
            _exportBtn.SetEnabled(_exportGraph != null && Directory.Exists(AbsoluteFromAssets(_exportFolder)));
        }
        #endregion

        #region ---------------- Import Panel ----------------
        private VisualElement BuildImportPanel()
        {
            var root = new VisualElement();
            var card = MakeCard("Import JSON → DialogGraph");

            // ── Source JSON ───────────────────────────────────────────────
            var sourceHeader = new Label("Source JSON");
            sourceHeader.AddToClassList("dlg-io-subsection");
            card.Add(sourceHeader);

            var assetField = new ObjectField("TextAsset") { objectType = typeof(TextAsset), value = _importJsonAsset };
            assetField.AddToClassList("dlg-textfield");
            assetField.style.marginBottom = 6;
            assetField.RegisterValueChangedCallback(evt => _importJsonAsset = evt.newValue as TextAsset);
            card.Add(assetField);

            // External file row
            var extRow    = new VisualElement();
            extRow.AddToClassList("dlg-io-field-row");
            var extLabel  = new Label("External File");
            extLabel.AddToClassList("dlg-io-field-label");
            var extPath   = new Label(string.IsNullOrEmpty(_importExternalPath) ? "—" : _importExternalPath);
            extPath.AddToClassList("dlg-io-path-display");
            var browseBtn = new Button(() =>
            {
                var p = EditorUtility.OpenFilePanel("Choose JSON file", GetInitialImportFolder(), "json");
                if (!string.IsNullOrEmpty(p))
                {
                    _importExternalPath = p;
                    extPath.text        = p;
                }
            })
            { text = "Browse..." };
            browseBtn.AddToClassList("dlg-btn");
            browseBtn.AddToClassList("secondary");
            extRow.Add(extLabel);
            extRow.Add(extPath);
            extRow.Add(browseBtn);
            card.Add(extRow);

            // Load & Validate
            var loadBtn = new Button(() => { LoadJsonForPreview(); LoadTabContent(); })
            { text = "Load & Validate" };
            loadBtn.AddToClassList("dlg-btn");
            loadBtn.AddToClassList("secondary");
            loadBtn.style.marginTop    = 6;
            loadBtn.style.marginBottom = 4;
            card.Add(loadBtn);

            // Error label
            if (!string.IsNullOrEmpty(_jsonError))
            {
                var errorLbl = new Label(_jsonError);
                errorLbl.AddToClassList("dlg-io-error");
                card.Add(errorLbl);
            }

            // Preview box
            if (_previewDto != null)
            {
                var previewBox = new VisualElement();
                previewBox.AddToClassList("dlg-io-preview-box");

                var previewTitle = new Label("Preview");
                previewTitle.AddToClassList("dlg-io-preview-title");
                previewBox.Add(previewTitle);

                previewBox.Add(MakePreviewRow("Dialog Nodes",  (_previewDto.dialogNodes?.Count  ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Choice Nodes",  (_previewDto.choiceNodes?.Count  ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Action Nodes",  (_previewDto.actionNodes?.Count  ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Condition Nodes",  (_previewDto.conditionNodes?.Count  ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Variable Nodes",  (_previewDto.variableMutationNodes?.Count  ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Graph Jump Nodes",  (_previewDto.graphJumpNodes?.Count  ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Outcome Nodes",  (_previewDto.outcomeNodes?.Count  ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Links",         (_previewDto.links?.Count        ?? 0).ToString()));
                card.Add(previewBox);
            }

            // ── Target Asset ──────────────────────────────────────────────
            var targetHeader = new Label("Target Asset");
            targetHeader.AddToClassList("dlg-io-subsection");
            card.Add(targetHeader);

            var nameField = new TextField("Asset Name") { value = _importTargetName };
            nameField.AddToClassList("dlg-textfield");
            nameField.style.marginBottom = 6;
            nameField.RegisterValueChangedCallback(evt => _importTargetName = evt.newValue);
            card.Add(nameField);

            card.Add(BuildFolderRow("Folder", () => _importFolder, v => _importFolder = v,
                "Choose graph folder (inside Assets)"));

            var hint = new Label(
                "Import will create or overwrite a DialogGraph asset using the name and folder above. " +
                "If an asset with the same name already exists you will be asked to overwrite or create a copy.");
            hint.AddToClassList("dlg-io-hint");
            card.Add(hint);

            // Import button
            var importRow = new VisualElement();
            importRow.style.flexDirection  = FlexDirection.Row;
            importRow.style.justifyContent = Justify.FlexEnd;
            importRow.style.marginTop      = 10;

            var importBtn = new Button(() => { DoImport(); LoadTabContent(); }) { text = "Import" };
            importBtn.AddToClassList("dlg-btn");
            importBtn.AddToClassList("primary");
            importBtn.SetEnabled(_previewDto != null);
            importRow.Add(importBtn);
            card.Add(importRow);

            root.Add(card);
            return root;
        }

        private static VisualElement MakePreviewRow(string key, string val)
        {
            var row = new VisualElement();
            row.AddToClassList("dlg-io-preview-row");
            var k = new Label(key); k.AddToClassList("dlg-io-preview-key");
            var v = new Label(val); v.AddToClassList("dlg-io-preview-val");
            row.Add(k);
            row.Add(v);
            return row;
        }
        #endregion

        #region ---------------- Drop Zone ----------------
        private VisualElement BuildDropZone()
        {
            var zone = new VisualElement();
            zone.AddToClassList("dlg-io-drop-zone");

            var lbl = new Label("Drop a .json file here to load for import");
            lbl.AddToClassList("dlg-io-drop-label");
            zone.Add(lbl);

            zone.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                var paths = DragAndDrop.paths;
                if (paths != null && paths.Length > 0 &&
                    paths[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    zone.AddToClassList("drag-over");
                }
                evt.StopPropagation();
            });

            zone.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();
                var paths = DragAndDrop.paths;
                if (paths != null && paths.Length > 0 &&
                    paths[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    _importJsonAsset    = null;
                    _importExternalPath = paths[0];
                    _tab                = Tab.Import;
                    LoadJsonForPreview();
                    LoadTabContent();
                }
                zone.RemoveFromClassList("drag-over");
                evt.StopPropagation();
            });

            zone.RegisterCallback<DragLeaveEvent>(_ => zone.RemoveFromClassList("drag-over"));
            zone.RegisterCallback<DragExitedEvent>(_ => zone.RemoveFromClassList("drag-over"));
            return zone;
        }
        #endregion

        #region ---------------- Shared UI Helpers ----------------
        private static VisualElement MakeCard(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("dlg-section");
            var lbl = new Label(title);
            lbl.AddToClassList("dlg-section-title");
            card.Add(lbl);
            return card;
        }

        private VisualElement BuildFolderRow(string labelText, Func<string> getPath,
            Action<string> setPath, string panelTitle)
        {
            var row = new VisualElement();
            row.AddToClassList("dlg-io-field-row");

            var lbl = new Label(labelText);
            lbl.AddToClassList("dlg-io-field-label");

            var pathLbl = new Label(getPath());
            pathLbl.AddToClassList("dlg-io-path-display");

            var btn = new Button(() =>
            {
                var abs = EditorUtility.OpenFolderPanel(panelTitle, Application.dataPath, "");
                if (IsUnderAssets(abs, out var rel))
                {
                    setPath(rel);
                    pathLbl.text = rel;
                }
                else if (!string.IsNullOrEmpty(abs))
                {
                    EditorUtility.DisplayDialog("Folder not under Assets",
                        "Please choose a folder inside your project's Assets.", "OK");
                }
            })
            { text = "Select..." };
            btn.AddToClassList("dlg-btn");
            btn.AddToClassList("secondary");

            row.Add(lbl);
            row.Add(pathLbl);
            row.Add(btn);
            return row;
        }
        #endregion

        #region ---------------- Export Logic ----------------
        private void DoExport()
        {
            if (_exportGraph == null) return;

            var migrationResult = DialogGraphUpgradeService.MigrateToCurrent(_exportGraph);
            if (migrationResult.Errors.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Graph migration warning",
                    "The graph was exported, but migration/default initialization reported issues:\n\n" +
                    string.Join("\n", migrationResult.Errors),
                    "OK");
            }

            if (migrationResult.Changed)
            {
                AssetDatabase.SaveAssets();
            }

            var fileName = string.IsNullOrEmpty(_exportFileName)
                ? SafeFile(_exportGraph.name)
                : SafeFile(_exportFileName);

            if (string.IsNullOrEmpty(fileName))
                fileName = "DialogGraph";

            var absDir  = AbsoluteFromAssets(_exportFolder);
            if (!Directory.Exists(absDir))
                Directory.CreateDirectory(absDir);

            var relPath = $"{_exportFolder}/{fileName}.json";
            var absPath = AbsoluteFromAssets(relPath);

            if (File.Exists(absPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "File exists",
                    $"A JSON file already exists at:\n{relPath}\n\nOverwrite it?",
                    "Overwrite", "Cancel");
                if (!overwrite) return;
            }

            var export = BuildExportDTO(_exportGraph);
            var json   = JsonUtility.ToJson(export, _exportPretty);
            File.WriteAllText(absPath, json, Encoding.UTF8);
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent($"Exported → {relPath}"));

            if (doDebug)
                Debug.Log($"[DialogJsonIOWindow] Exported DialogGraph '{_exportGraph.name}' to: {relPath}");
        }
        #endregion

        #region ---------------- Import Logic ----------------
        private void DoImport()
        {
            if (_previewDto == null)
            {
                EditorUtility.DisplayDialog("No JSON", "Load a JSON file and validate it first.", "OK");
                return;
            }
            CreateOrOverwriteGraphFromJson(_loadedJson);
        }
        #endregion

        #region ---------------- Export DTO Builder ----------------
        private DialogGraphExport BuildExportDTO(DialogGraph graph)
        {
            var export = new DialogGraphExport
            {
                graphGuid = graph.GraphGuid,
                schemaVersion = graph.GraphSchemaVersion,
                graphTitle = graph.graphTitle,
                description = graph.description,
                author = graph.author,
                tags = graph.tags != null ? new List<string>(graph.tags) : new List<string>(),
                primaryCategory = graph.primaryCategory,
                categories = graph.categories != null ? new List<string>(graph.categories) : new List<string>(),
                lastModifiedUtc = graph.lastModifiedUtc,
                editorVersion = graph.editorVersion,
                sceneGoal = graph.sceneGoal,
                tone = graph.tone,
                extraRules = graph.extraRules,
                dialogNodes = new List<DialogExportDialogNode>(),
                choiceNodes = new List<DialogExportChoiceNode>(),
                actionNodes = new List<DialogExportActionNode>(),
                conditionNodes = new List<DialogExportConditionNode>(),
                variableMutationNodes = new List<DialogExportVariableMutationNode>(),
                graphJumpNodes = new List<DialogExportGraphJumpNode>(),
                outcomeNodes = new List<DialogExportOutcomeNode>(),
                links       = new List<ExportLink>(),
                groupLayouts = new List<ExportGroupLayoutRecord>()
            };

            if (graph.nodes != null)
            {
                foreach (var n in graph.nodes.Where(n => n != null))
                {
                    export.dialogNodes.Add(new DialogExportDialogNode
                    {
                        title           = (n.name ?? "Node").Replace("Node_", ""),
                        guid            = n.GetGuid(),
                        speaker         = n.speakerName,
                        question        = n.questionText,
                        nodePositionX   = _exportIncludePositions ? n.GetPosition().x : 0f,
                        nodePositionY   = _exportIncludePositions ? n.GetPosition().y : 0f,
                        displayTime     = n.displayTime,
                    });
                }
            }

            if (graph.choiceNodes != null)
            {
                foreach (var c in graph.choiceNodes.Where(c => c != null))
                {
                    var dto = new DialogExportChoiceNode
                    {
                        guid          = c.GetGuid(),
                        text          = c.text,
                        nodePositionX = _exportIncludePositions ? c.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? c.GetPosition().y : 0f,
                        choices       = new List<ExportChoice>(),
                    };
                    if (c.choices != null)
                    {
                        foreach (var ch in c.choices)
                            dto.choices.Add(new ExportChoice
                            {
                                choiceId = ch?.choiceId,
                                portKey = ch?.PortKey,
                                answerText = ch?.answerText,
                                nextNodeGUID = ch?.nextNodeGUID
                            });
                    }
                    export.choiceNodes.Add(dto);
                }
            }

            if (graph.actionNodes != null)
            {
                foreach (var a in graph.actionNodes.Where(a => a != null))
                {
                    export.actionNodes.Add(new DialogExportActionNode
                    {
                        guid              = a.GetGuid(),
                        actionId          = a.actionId,
                        payloadJson       = a.payloadJson,
                        waitForCompletion = a.waitForCompletion,
                        waitSeconds       = a.waitSeconds,
                        nodePositionX     = _exportIncludePositions ? a.GetPosition().x : 0f,
                        nodePositionY     = _exportIncludePositions ? a.GetPosition().y : 0f
                    });
                }
            }

            if (graph.conditionNodes != null)
            {
                foreach (var c in graph.conditionNodes.Where(c => c != null))
                {
                    export.conditionNodes.Add(new DialogExportConditionNode
                    {
                        guid = c.GetGuid(),
                        variableName = c.variableName,
                        valueType = c.valueType.ToString(),
                        conditionOperator = c.conditionOperator.ToString(),
                        comparisonValue = c.comparisonValue,
                        missingVariableResult = c.missingVariableResult,
                        nodePositionX = _exportIncludePositions ? c.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? c.GetPosition().y : 0f
                    });
                }
            }

            if (graph.variableMutationNodes != null)
            {
                foreach (var v in graph.variableMutationNodes.Where(v => v != null))
                {
                    export.variableMutationNodes.Add(new DialogExportVariableMutationNode
                    {
                        guid = v.GetGuid(),
                        variableName = v.variableName,
                        valueType = v.valueType.ToString(),
                        operation = v.operation.ToString(),
                        value = v.value,
                        nodePositionX = _exportIncludePositions ? v.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? v.GetPosition().y : 0f
                    });
                }
            }

            if (graph.graphJumpNodes != null)
            {
                foreach (var j in graph.graphJumpNodes.Where(j => j != null))
                {
                    export.graphJumpNodes.Add(new DialogExportGraphJumpNode
                    {
                        guid = j.GetGuid(),
                        nodePositionX = _exportIncludePositions ? j.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? j.GetPosition().y : 0f,
                        targetGraph = ExportGraphReference(j.targetGraph)
                    });
                }
            }

            if (graph.outcomeNodes != null)
            {
                foreach (var o in graph.outcomeNodes.Where(o => o != null))
                {
                    export.outcomeNodes.Add(new DialogExportOutcomeNode
                    {
                        guid = o.GetGuid(),
                        outcomeId = o.outcomeId,
                        displayName = o.displayName,
                        description = o.description,
                        nodePositionX = _exportIncludePositions ? o.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? o.GetPosition().y : 0f
                    });
                }
            }

            if (!string.IsNullOrEmpty(graph.startGuid))
            {
                export.startNode = new ExportStartNode
                {
                    isInitialized = graph.startInitialized,
                    guid          = graph.startGuid,
                    nodePositionX = graph.startPosition.x,
                    nodePositionY = graph.startPosition.y
                };
            }

            if (!string.IsNullOrEmpty(graph.endGuid))
            {
                export.endNode = new ExportEndNode
                {
                    isInitialized = graph.endInitialized,
                    guid          = graph.endGuid,
                    nodePositionX = graph.endPosition.x,
                    nodePositionY = graph.endPosition.y
                };
            }

            if (graph.links != null)
            {
                export.links = graph.links.Select(l => new ExportLink
                {
                    linkGuid      = l.LinkGuid,
                    fromGuid      = l.fromGuid,
                    toGuid        = l.toGuid,
                    fromPortKey   = l.fromPortKey,
                    toPortKey     = l.toPortKey,
                    fromPortIndex = l.fromPortIndex
                }).ToList();
            }

            foreach (var groupLayout in graph.EnumerateGroupLayouts())
            {
                export.groupLayouts.Add(new ExportGroupLayoutRecord
                {
                    groupId = groupLayout.groupId,
                    title = groupLayout.title,
                    category = groupLayout.category,
                    x = groupLayout.bounds.x,
                    y = groupLayout.bounds.y,
                    width = groupLayout.bounds.width,
                    height = groupLayout.bounds.height,
                    colorR = groupLayout.colorTint.r,
                    colorG = groupLayout.colorTint.g,
                    colorB = groupLayout.colorTint.b,
                    colorA = groupLayout.colorTint.a,
                    nodeGuids = groupLayout.nodeGuids != null ? new List<string>(groupLayout.nodeGuids) : new List<string>()
});
            }

            return export;
        }

        private static DialogExportGraphReference ExportGraphReference(GraphReference graphReference)
        {
            if (graphReference == null || !graphReference.HasReference && string.IsNullOrWhiteSpace(graphReference.entryGuid))
            {
                return null;
            }

            return new DialogExportGraphReference
            {
                graphGuid = graphReference.graphGuid ?? string.Empty,
                runtimeDialogId = graphReference.runtimeDialogId ?? string.Empty,
                graphName = graphReference.graphName ?? string.Empty,
                assetPath = graphReference.assetPath ?? string.Empty,
                entryGuid = graphReference.entryGuid ?? string.Empty
            };
        }
        #endregion

        #region ---------------- Import Logic (Safe) ----------------
        private void CreateOrOverwriteGraphFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                EditorUtility.DisplayDialog("No JSON", "Load a JSON file first.", "OK");
                return;
            }

            var absDir = AbsoluteFromAssets(_importFolder);
            if (!Directory.Exists(absDir))
                Directory.CreateDirectory(absDir);

            var baseName      = string.IsNullOrEmpty(_importTargetName) ? "ImportedConversation" : SafeFile(_importTargetName);
            var targetRelPath = $"{_importFolder}/{baseName}.asset";
            var targetAbsPath = AbsoluteFromAssets(targetRelPath);

            DialogGraphImportResult result;

            if (File.Exists(targetAbsPath))
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "DialogGraph already exists",
                    $"A DialogGraph asset with this name already exists:\n\n{targetRelPath}\n\nWhat would you like to do?",
                    "Overwrite", "Create Copy", "Cancel");

                if (option == 2) return;

                if (option == 0)
                {
                    result = DialogGraphImportTransactionService.ImportOverwrite(targetRelPath, json);
                    HandleImportResult(result);
                    return;
                }

                if (option == 1)
                    targetRelPath = AssetDatabase.GenerateUniqueAssetPath(targetRelPath);
            }

            result = DialogGraphImportTransactionService.ImportNew(_importFolder, baseName, json);
            HandleImportResult(result);
        }

        private void HandleImportResult(DialogGraphImportResult result)
        {
            if (result.Success)
            {
                ShowNotification(new GUIContent($"Imported {Path.GetFileName(result.TargetPath)}"));
                if (doDebug)
                    Debug.Log($"[DialogJsonIOWindow] {result.Message}");

                if (!string.IsNullOrEmpty(result.BackupPath))
                    Debug.Log($"[DialogJsonIOWindow] Backup saved at: {result.BackupPath}");
            }
            else
            {
                var msg = $"Import failed at stage '{result.FailureStage}':\n{result.Message}";

                if (!string.IsNullOrEmpty(result.BackupPath))
                    msg += $"\n\nA backup was saved at:\n{result.BackupPath}";

                if (result.Errors != null && result.Errors.Count > 0)
                    msg += "\n\nDetails:\n" + string.Join("\n", result.Errors.Take(10));

                EditorUtility.DisplayDialog("Import Failed", msg, "OK");

                if (doDebug)
                    Debug.LogError($"[DialogJsonIOWindow] {msg}");
            }
        }

        #endregion

        #region ---------------- JSON Load/Validate ----------------
        private void LoadJsonForPreview()
        {
            _jsonError   = "";
            _previewDto  = null;

            try
            {
                string json = null;
                if (_importJsonAsset != null)
                {
                    _importExternalPath = "";
                    json = _importJsonAsset.text;
                }
                else if (!string.IsNullOrEmpty(_importExternalPath) && File.Exists(_importExternalPath))
                {
                    json = File.ReadAllText(_importExternalPath, Encoding.UTF8);
                }
                else
                {
                    EditorUtility.DisplayDialog("No Source", "Assign a TextAsset or choose an external JSON file.", "OK");
                    return;
                }

                _loadedJson = json;
                _previewDto = JsonUtility.FromJson<DialogGraphExport>(_loadedJson);

                bool ok = _previewDto != null &&
                          (_previewDto.dialogNodes != null || _previewDto.choiceNodes != null ||
                           _previewDto.actionNodes != null || _previewDto.conditionNodes != null ||
                           _previewDto.variableMutationNodes != null || _previewDto.graphJumpNodes != null ||
                           _previewDto.outcomeNodes != null);

                if (!ok)
                {
                    _previewDto = null;
                    _jsonError  = "Invalid or empty JSON for DialogGraphExport.";
                }

                if (doDebug && ok)
                    Debug.Log("[DialogJsonIOWindow] JSON validated successfully for import.");
            }
            catch (Exception ex)
            {
                _previewDto = null;
                _jsonError  = "JSON parse error: " + ex.Message;
            }
        }
        #endregion

        #region ---------------- Path Helpers ----------------
        private static bool IsUnderAssets(string absolutePath, out string assetsRelative)
        {
            assetsRelative = null;
            if (string.IsNullOrEmpty(absolutePath)) return false;

            absolutePath = absolutePath.Replace("\\", "/");
            var assetsAbs = Application.dataPath.Replace("\\", "/");
            if (!absolutePath.StartsWith(assetsAbs, StringComparison.OrdinalIgnoreCase)) return false;

            assetsRelative = "Assets" + absolutePath.Substring(assetsAbs.Length);
            return true;
        }

        private static string AbsoluteFromAssets(string assetsRelative)
        {
            if (string.IsNullOrEmpty(assetsRelative)) return null;

            var rel = assetsRelative.Replace("\\", "/");
            if (!rel.StartsWith("Assets/") && rel != "Assets")
                throw new Exception("Path must start with 'Assets/'");

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", rel)).Replace("\\", "/");
        }

        private static string SafeFile(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
                sb.Append(invalid.Contains(ch) ? '_' : ch);
            return sb.ToString();
        }

        private static TEnum TryParseEnum<TEnum>(string rawValue, TEnum fallback)
            where TEnum : struct
        {
            return Enum.TryParse(rawValue, true, out TEnum parsed) ? parsed : fallback;
        }

        private string GetInitialImportFolder()
        {
            if (!string.IsNullOrEmpty(_importExternalPath)) return Path.GetDirectoryName(_importExternalPath);
            if (!string.IsNullOrEmpty(_importFolder))       return AbsoluteFromAssets(_importFolder);
            return Application.dataPath;
        }

        private static void SyncChoiceTargetsFromLinks(DialogGraph graph)
        {
            if (graph == null || graph.choiceNodes == null || graph.links == null)
            {
                return;
            }

            var linksByChoice = graph.links
                .Where(link => link != null)
                .GroupBy(link => link.fromGuid)
                .ToDictionary(group => group.Key, group => group.OrderBy(link => link.fromPortIndex).ToList());

            foreach (var choiceNode in graph.choiceNodes)
            {
                if (choiceNode == null || choiceNode.choices == null)
                {
                    continue;
                }

                if (!linksByChoice.TryGetValue(choiceNode.GetGuid(), out var outgoingLinks))
                {
                    continue;
                }

                for (var i = 0; i < choiceNode.choices.Count; i++)
                {
                    var link = outgoingLinks.FirstOrDefault(item => item.fromPortIndex == i);
                    if (link != null)
                    {
                        choiceNode.choices[i].nextNodeGUID = link.toGuid;
                    }
                }
            }
        }
        #endregion
    }
}