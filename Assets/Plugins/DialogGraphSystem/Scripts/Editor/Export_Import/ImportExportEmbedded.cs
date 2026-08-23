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
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.ExportImport
{
    /// <summary>
    /// Embeddable version of the Import/Export tool UI.
    /// Renders Export and Import panels as cards suitable for the unified main window.
    /// </summary>
    public class ImportExportEmbedded : VisualElement
    {
        #region ---------------- Constants ----------------

        private const string K_EXPORT_FOLDER = "DialogSystem_EmbeddedExportFolder";
        private const string K_IMPORT_FOLDER = "DialogSystem_EmbeddedImportFolder";

        #endregion

        #region ---------------- Export State ----------------

        private DialogGraph _exportGraph;
        private string      _exportFileName        = "";
        private string      _exportFolder;
        private bool        _exportPretty          = true;
        private bool        _exportIncludePositions = true;
        private Button      _exportBtn;

        #endregion

        #region ---------------- Import State ----------------

        private TextAsset         _importJsonAsset;
        private string            _importExternalPath = "";
        private string            _importFolder;
        private string            _importTargetName   = "ImportedConversation";
        private string            _loadedJson  = "";
        private string            _jsonError   = "";
        private DialogGraphExport _previewDto;

        #endregion

        #region ---------------- UI References ----------------

        private VisualElement _scrollContent;

        #endregion

        #region ---------------- Constructor ----------------

        public ImportExportEmbedded()
        {
            _exportFolder = EditorPrefs.GetString(K_EXPORT_FOLDER, TextResources.EXPORT_FOLDER);
            _importFolder = EditorPrefs.GetString(K_IMPORT_FOLDER, TextResources.IMPORT_FOLDER);
            _exportGraph = Selection.activeObject as DialogGraph;

            style.flexGrow      = 1;
            style.flexDirection = FlexDirection.Column;

            BuildUI();
        }

        #endregion

        #region ---------------- UI Build ----------------

        private void BuildUI()
        {
            Clear();

            // Scrollable content
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("ds-tab-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.style.flexGrow = 1;
            Add(scroll);

            _scrollContent = new VisualElement();
            _scrollContent.style.flexDirection = FlexDirection.Column;
            _scrollContent.style.paddingLeft = 16;
            _scrollContent.style.paddingRight = 16;
            _scrollContent.style.paddingTop = 12;
            _scrollContent.style.paddingBottom = 16;
            scroll.Add(_scrollContent);

            _scrollContent.Add(BuildHeader());
            _scrollContent.Add(BuildExportCard());
            _scrollContent.Add(BuildImportCard());

            // Drop zone
            Add(BuildDropZone());
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("dgs-page-header");
            header.style.paddingLeft = 0;
            header.style.paddingRight = 0;
            header.style.paddingTop = 0;
            header.style.borderBottomWidth = 0; // The cards will provide separation

            var title = new Label("Import / Export Tool");
            title.AddToClassList("dgs-page-title");
            header.Add(title);

            var subtitle = new Label("Export your dialogue graphs to JSON for external editing, or import JSON files to create new graph assets.");
            subtitle.AddToClassList("dgs-page-subtitle");
            header.Add(subtitle);

            return header;
        }

        private VisualElement BuildExportCard()
        {
            var card = MakeCard("Export  DialogGraph → JSON");

            // Graph object field
            var graphField = new ObjectField("Graph") { objectType = typeof(DialogGraph), value = _exportGraph };
            graphField.style.marginBottom = 6;
            graphField.RegisterValueChangedCallback(evt =>
            {
                _exportGraph = evt.newValue as DialogGraph;
                UpdateExportButton();
            });
            card.Add(graphField);

            // File name
            var nameField = new TextField("File Name") { value = _exportFileName };
            nameField.style.marginBottom = 6;
            nameField.RegisterValueChangedCallback(evt => _exportFileName = evt.newValue);
            card.Add(nameField);

            // Folder
            card.Add(BuildFolderRow("Folder", () => _exportFolder, v =>
            {
                _exportFolder = v;
                EditorPrefs.SetString(K_EXPORT_FOLDER, v);
                UpdateExportButton();
            }, "Choose export folder (inside Assets)"));

            // Toggles container
            var toggles = new VisualElement();
            toggles.style.flexDirection = FlexDirection.Row;
            toggles.style.marginTop = 4;
            toggles.style.marginBottom = 8;
            card.Add(toggles);

            var prettyToggle = new Toggle("Pretty Print") { value = _exportPretty };
            prettyToggle.style.marginRight = 12;
            prettyToggle.style.width = 120;
            prettyToggle.style.flexGrow = 0;
            prettyToggle.style.flexShrink = 0;
            prettyToggle.RegisterValueChangedCallback(evt => _exportPretty = evt.newValue);
            toggles.Add(prettyToggle);

            var posToggle = new Toggle("Include Node Positions") { value = _exportIncludePositions };
            posToggle.style.width = 170;
            posToggle.style.flexGrow = 0;
            posToggle.style.flexShrink = 0;
            posToggle.RegisterValueChangedCallback(evt => _exportIncludePositions = evt.newValue);
            toggles.Add(posToggle);

            // Action buttons
            var btnRow = new VisualElement();
            btnRow.style.flexDirection  = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            btnRow.style.marginTop      = 8;
            btnRow.style.paddingTop     = 8;
            btnRow.style.borderTopWidth = 1;
            btnRow.style.borderTopColor = new Color(0.16f, 0.18f, 0.27f);

            var useSelectedBtn = new Button(() =>
            {
                var sel = Selection.activeObject as DialogGraph;
                if (sel != null)
                {
                    _exportGraph      = sel;
                    graphField.value  = sel;
                    UpdateExportButton();
                }
            }) { text = "Use Selected" };
            useSelectedBtn.AddToClassList("dlg-btn");
            useSelectedBtn.AddToClassList("secondary");
            useSelectedBtn.style.marginRight = 6;

            _exportBtn = new Button(DoExport) { text = "Export Graph" };
            _exportBtn.AddToClassList("dlg-btn");
            _exportBtn.AddToClassList("primary");
            UpdateExportButton();

            btnRow.Add(useSelectedBtn);
            btnRow.Add(_exportBtn);
            card.Add(btnRow);

            return card;
        }

        private VisualElement BuildImportCard()
        {
            var card = MakeCard("Import JSON → DialogGraph");

            // Source JSON
            var sourceHeader = new Label("Source JSON");
            sourceHeader.AddToClassList("dlg-io-subsection");
            sourceHeader.style.marginTop = 0;
            card.Add(sourceHeader);

            var assetField = new ObjectField("TextAsset") { objectType = typeof(TextAsset), value = _importJsonAsset };
            assetField.style.marginBottom = 6;
            assetField.RegisterValueChangedCallback(evt =>
            {
                _importJsonAsset = evt.newValue as TextAsset;
                if (_importJsonAsset != null) _importExternalPath = string.Empty;
            });
            card.Add(assetField);

            // External file row
            var extRow   = new VisualElement();
            extRow.style.flexDirection = FlexDirection.Row;
            extRow.style.alignItems = Align.Center;
            extRow.style.marginBottom = 6;

            var extLabel = new Label("External File");
            extLabel.style.width = 90;
            extLabel.style.fontSize = 11;
            extLabel.style.color = new Color(0.6f, 0.64f, 0.72f);

            var extPath  = new Label(string.IsNullOrEmpty(_importExternalPath) ? "—" : _importExternalPath);
            extPath.AddToClassList("dlg-io-path-display");
            extPath.style.flexGrow = 1;

            var browseBtn = new Button(() =>
            {
                var p = EditorUtility.OpenFilePanel("Choose JSON file",
                    string.IsNullOrEmpty(_importExternalPath) ? Application.dataPath : Path.GetDirectoryName(_importExternalPath),
                    "json");
                if (!string.IsNullOrEmpty(p))
                {
                    _importExternalPath = p;
                    extPath.text        = p;
                    _importJsonAsset    = null;
                    assetField.value    = null;
                }
            }) { text = "Browse..." };
            browseBtn.AddToClassList("dlg-btn");
            browseBtn.AddToClassList("secondary");

            extRow.Add(extLabel);
            extRow.Add(extPath);
            extRow.Add(browseBtn);
            card.Add(extRow);

            // Load & Validate
            var loadBtn = new Button(() => { LoadJsonForPreview(); RebuildImportCard(); }) { text = "Load & Validate JSON" };
            loadBtn.AddToClassList("dlg-btn");
            loadBtn.AddToClassList("secondary");
            loadBtn.style.marginTop    = 4;
            loadBtn.style.marginBottom = 8;
            card.Add(loadBtn);

            AppendImportPreview(card);

            // Target asset
            var targetHeader = new Label("Target Asset");
            targetHeader.AddToClassList("dlg-io-subsection");
            card.Add(targetHeader);

            var nameField = new TextField("Asset Name") { value = _importTargetName };
            nameField.style.marginBottom = 6;
            nameField.RegisterValueChangedCallback(evt => _importTargetName = evt.newValue);
            card.Add(nameField);

            card.Add(BuildFolderRow("Folder", () => _importFolder, v =>
            {
                _importFolder = v;
                EditorPrefs.SetString(K_IMPORT_FOLDER, v);
            }, "Choose graph folder (inside Assets)"));

            var hint = new Label(
                "Import will create or overwrite a DialogGraph asset using the name and folder above.");
            hint.AddToClassList("dgs-page-subtitle");
            hint.style.marginTop = 4;
            hint.style.fontSize = 10;
            card.Add(hint);

            // Import button
            var importRow = new VisualElement();
            importRow.style.flexDirection  = FlexDirection.Row;
            importRow.style.justifyContent = Justify.FlexEnd;
            importRow.style.marginTop      = 12;
            importRow.style.paddingTop     = 8;
            importRow.style.borderTopWidth = 1;
            importRow.style.borderTopColor = new Color(0.16f, 0.18f, 0.27f);

            var importBtn = new Button(() => { DoImport(); RebuildImportCard(); }) { text = "Import to Project" };
            importBtn.AddToClassList("dlg-btn");
            importBtn.AddToClassList("success");
            importBtn.SetEnabled(_previewDto != null);
            importRow.Add(importBtn);
            card.Add(importRow);

            return card;
        }

        private void AppendImportPreview(VisualElement card)
        {
            if (!string.IsNullOrEmpty(_jsonError))
            {
                var errorLbl = new Label(_jsonError);
                errorLbl.AddToClassList("dlg-io-error");
                errorLbl.style.marginBottom = 8;
                card.Add(errorLbl);
            }

            if (_previewDto != null)
            {
                var previewBox = new VisualElement();
                previewBox.AddToClassList("dlg-io-preview-box");
                previewBox.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
                previewBox.style.paddingBottom = 8;

                var previewTitle = new Label("JSON Content Preview");
                previewTitle.AddToClassList("dlg-io-preview-title");
                previewBox.Add(previewTitle);

                previewBox.Add(MakePreviewRow("Dialog Nodes",   (_previewDto.dialogNodes?.Count ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Choice Nodes",   (_previewDto.choiceNodes?.Count ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Action Nodes",   (_previewDto.actionNodes?.Count ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Condition Nodes", (_previewDto.conditionNodes?.Count ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Variable Nodes", (_previewDto.variableMutationNodes?.Count ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Graph Jump Nodes", (_previewDto.graphJumpNodes?.Count ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Outcome Nodes", (_previewDto.outcomeNodes?.Count ?? 0).ToString()));
                previewBox.Add(MakePreviewRow("Links",          (_previewDto.links?.Count ?? 0).ToString()));
                card.Add(previewBox);
            }
        }

        private void RebuildImportCard()
        {
            if (_scrollContent == null) return;
            // Remove and rebuild just the import card (third child, as header is first, export is second)
            if (_scrollContent.childCount >= 3)
                _scrollContent.RemoveAt(2);
            _scrollContent.Add(BuildImportCard());
        }

        #endregion

        #region ---------------- Drop Zone ----------------

        private VisualElement BuildDropZone()
        {
            var zone = new VisualElement();
            zone.AddToClassList("dlg-io-drop-zone");
            zone.style.height = 36;
            zone.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f);

            var lbl = new Label("Drop a .json file here to quick-load for import");
            lbl.AddToClassList("dlg-io-drop-label");
            lbl.style.fontSize = 10;
            zone.Add(lbl);

            zone.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                var paths = DragAndDrop.paths;
                if (paths != null && paths.Length > 0 &&
                    paths[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    zone.AddToClassList("drag-over");
                    zone.style.backgroundColor = new Color(0.1f, 0.15f, 0.25f);
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
                    LoadJsonForPreview();
                    RebuildImportCard();
                }
                zone.RemoveFromClassList("drag-over");
                zone.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
                evt.StopPropagation();
            });

            zone.RegisterCallback<DragLeaveEvent>(_ => {
                zone.RemoveFromClassList("drag-over");
                zone.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            });
            zone.RegisterCallback<DragExitedEvent>(_ => {
                zone.RemoveFromClassList("drag-over");
                zone.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            });
            return zone;
        }

        #endregion

        #region ---------------- Export Logic ----------------

        private void UpdateExportButton()
        {
            if (_exportBtn == null) return;
            _exportBtn.SetEnabled(CanExport());
        }

        private void DoExport()
        {
            if (_exportGraph == null)
            {
                EditorUtility.DisplayDialog("No Graph Selected", "Select a DialogGraph asset to export.", "OK");
                return;
            }

            if (!IsProjectAssetPath(_exportFolder))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Export Folder",
                    "Choose an export folder inside this project's Assets directory.",
                    "OK");
                return;
            }

            var fileName = string.IsNullOrEmpty(_exportFileName)
                ? SafeFile(_exportGraph.name)
                : SafeFile(_exportFileName);
            if (string.IsNullOrEmpty(fileName)) fileName = "DialogGraph";

            var absDir  = AbsoluteFromAssets(_exportFolder);
            if (!Directory.Exists(absDir)) Directory.CreateDirectory(absDir);

            var relPath = $"{_exportFolder}/{fileName}.json";
            var absPath = AbsoluteFromAssets(relPath);

            if (File.Exists(absPath))
            {
                if (!EditorUtility.DisplayDialog("File exists",
                        $"A file already exists at:\n{relPath}\n\nOverwrite it?",
                        "Overwrite", "Cancel"))
                    return;
            }

            // Delegate to the DialogJsonIOWindow's static helper by opening it temporarily
            // and using the same export serialization approach
            var dto  = BuildExportDto(_exportGraph);
            var json = JsonUtility.ToJson(dto, _exportPretty);
            File.WriteAllText(absPath, json, Encoding.UTF8);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Export Complete", $"Exported to:\n{relPath}", "OK");
        }

        private bool CanExport()
        {
            return _exportGraph != null && IsProjectAssetPath(_exportFolder);
        }

        private DialogGraphExport BuildExportDto(DialogGraph graph)
        {
            var export = new DialogGraphExport
            {
                graphGuid        = graph.GraphGuid,
                schemaVersion    = graph.GraphSchemaVersion,
                graphTitle       = graph.graphTitle,
                description      = graph.description,
                author           = graph.author,
                tags             = graph.tags != null ? new List<string>(graph.tags) : new List<string>(),
                primaryCategory  = graph.primaryCategory,
                categories       = graph.categories != null ? new List<string>(graph.categories) : new List<string>(),
                lastModifiedUtc  = graph.lastModifiedUtc,
                editorVersion    = graph.editorVersion,
                sceneGoal        = graph.sceneGoal,
                tone             = graph.tone,
                extraRules       = graph.extraRules,
                dialogNodes      = new List<DialogExportDialogNode>(),
                choiceNodes      = new List<DialogExportChoiceNode>(),
                actionNodes      = new List<DialogExportActionNode>(),
                conditionNodes   = new List<DialogExportConditionNode>(),
                variableMutationNodes = new List<DialogExportVariableMutationNode>(),
                graphJumpNodes   = new List<DialogExportGraphJumpNode>(),
                outcomeNodes     = new List<DialogExportOutcomeNode>(),
                links            = new List<ExportLink>(),
                groupLayouts     = new List<ExportGroupLayoutRecord>()
            };

            if (graph.nodes != null)
            {
                foreach (var n in graph.nodes.Where(n => n != null))
                {
                    export.dialogNodes.Add(new DialogExportDialogNode
                    {
                        guid          = n.GetGuid(),
                        speaker       = n.speakerName,
                        question      = n.questionText,
                        nodePositionX = _exportIncludePositions ? n.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? n.GetPosition().y : 0f,
                        displayTime   = n.displayTime,
                    });
                }
            }

            if (graph.choiceNodes != null)
            {
                foreach (var c in graph.choiceNodes.Where(c => c != null))
                {
                    var dto2 = new DialogExportChoiceNode
                    {
                        guid          = c.GetGuid(),
                        text          = c.text,
                        nodePositionX = _exportIncludePositions ? c.GetPosition().x : 0f,
                        nodePositionY = _exportIncludePositions ? c.GetPosition().y : 0f,
                        choices       = new List<ExportChoice>()
                    };
                    if (c.choices != null)
                    {
                        foreach (var ch in c.choices)
                            dto2.choices.Add(new ExportChoice
                            {
                                choiceId     = ch?.choiceId,
                                portKey      = ch?.PortKey,
                                answerText   = ch?.answerText,
                                nextNodeGUID = ch?.nextNodeGUID
                            });
                    }
                    export.choiceNodes.Add(dto2);
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
                        guid                  = c.GetGuid(),
                        variableName          = c.variableName,
                        valueType             = c.valueType.ToString(),
                        conditionOperator     = c.conditionOperator.ToString(),
                        comparisonValue       = c.comparisonValue,
                        missingVariableResult = c.missingVariableResult,
                        nodePositionX         = _exportIncludePositions ? c.GetPosition().x : 0f,
                        nodePositionY         = _exportIncludePositions ? c.GetPosition().y : 0f
                    });
                }
            }

            if (graph.variableMutationNodes != null)
            {
                foreach (var v in graph.variableMutationNodes.Where(v => v != null))
                {
                    export.variableMutationNodes.Add(new DialogExportVariableMutationNode
                    {
                        guid          = v.GetGuid(),
                        variableName  = v.variableName,
                        valueType     = v.valueType.ToString(),
                        operation     = v.operation.ToString(),
                        value         = v.value,
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
                        targetGraph = new DialogExportGraphReference
                        {
                            graphGuid = j.targetGraph?.graphGuid ?? string.Empty,
                            runtimeDialogId = j.targetGraph?.runtimeDialogId ?? string.Empty,
                            graphName = j.targetGraph?.graphName ?? string.Empty,
                            assetPath = j.targetGraph?.assetPath ?? string.Empty,
                            entryGuid = j.targetGraph?.entryGuid ?? string.Empty
                        }
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

        #endregion

        #region ---------------- Import Logic ----------------

        private void LoadJsonForPreview()
        {
            _previewDto = null;
            _jsonError  = string.Empty;
            _loadedJson = string.Empty;

            try
            {
                if (_importJsonAsset != null)
                {
                    _loadedJson = _importJsonAsset.text;
                }
                else if (!string.IsNullOrEmpty(_importExternalPath) && File.Exists(_importExternalPath))
                {
                    _loadedJson = File.ReadAllText(_importExternalPath, Encoding.UTF8);
                }
                else
                {
                    _jsonError = "No JSON source selected.";
                    return;
                }

                _previewDto = JsonUtility.FromJson<DialogGraphExport>(_loadedJson);
                if (_previewDto == null)
                    _jsonError = "JSON parsed to null — check format.";
            }
            catch (Exception ex)
            {
                _jsonError  = $"Parse error: {ex.Message}";
                _previewDto = null;
            }
        }

        private void DoImport()
        {
            if (string.IsNullOrWhiteSpace(_loadedJson))
            {
                EditorUtility.DisplayDialog("No JSON", "Load a JSON file and validate it first.", "OK");
                return;
            }

            var absDir = AbsoluteFromAssets(_importFolder);
            if (!Directory.Exists(absDir)) Directory.CreateDirectory(absDir);

            var baseName      = string.IsNullOrEmpty(_importTargetName) ? "ImportedConversation" : SafeFile(_importTargetName);
            var targetRelPath = $"{_importFolder}/{baseName}.asset";
            var targetAbsPath = AbsoluteFromAssets(targetRelPath);

            DialogGraphImportResult result;

            if (File.Exists(targetAbsPath))
            {
                int option = EditorUtility.DisplayDialogComplex(
                    "DialogGraph already exists",
                    $"A DialogGraph asset already exists at:\n\n{targetRelPath}\n\nWhat would you like to do?",
                    "Overwrite", "Create Copy", "Cancel");

                if (option == 2) return;

                if (option == 0)
                {
                    result = DialogGraphImportTransactionService.ImportOverwrite(targetRelPath, _loadedJson);
                    HandleImportResult(result);
                    return;
                }

                if (option == 1) targetRelPath = AssetDatabase.GenerateUniqueAssetPath(targetRelPath);
            }

            result = DialogGraphImportTransactionService.ImportNew(_importFolder, baseName, _loadedJson);
            HandleImportResult(result);
        }

        private void HandleImportResult(DialogGraphImportResult result)
        {
            if (result.Success)
            {
                EditorUtility.DisplayDialog("Import Complete",
                    $"Imported DialogGraph at:\n{result.TargetPath}\n\nOpen the graph in the editor to review it.", "OK");
            }
            else
            {
                var msg = $"Import failed at stage '{result.FailureStage}':\n{result.Message}";

                if (!string.IsNullOrEmpty(result.BackupPath))
                    msg += $"\n\nA backup was saved at:\n{result.BackupPath}";

                if (result.Errors != null && result.Errors.Count > 0)
                    msg += "\n\nDetails:\n" + string.Join("\n", result.Errors.Take(10));

                EditorUtility.DisplayDialog("Import Failed", msg, "OK");
            }
        }


        #endregion

        #region ---------------- Shared UI Helpers ----------------

        private static VisualElement MakeCard(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("dgs-card");
            card.style.marginBottom = 12;

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
            lbl.style.paddingBottom = 0;
            lbl.style.borderBottomWidth = 0;
            header.Add(lbl);

            return card;
        }

        private VisualElement BuildFolderRow(string labelText, Func<string> getPath,
            Action<string> setPath, string panelTitle)
        {
            var row = new VisualElement();
            row.AddToClassList("dlg-io-field-row");

            var lbl     = new Label(labelText);
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
            }) { text = "Select..." };
            btn.AddToClassList("dlg-btn");
            btn.AddToClassList("secondary");

            row.Add(lbl);
            row.Add(pathLbl);
            row.Add(btn);
            return row;
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

        private static bool IsUnderAssets(string abs, out string rel)
        {
            rel = string.Empty;
            if (string.IsNullOrEmpty(abs)) return false;
            var dataPath = Application.dataPath.Replace("\\", "/");
            abs = abs.Replace("\\", "/");
            if (!abs.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return false;
            var suffix = abs.Substring(dataPath.Length).TrimStart('/');
            rel = string.IsNullOrEmpty(suffix) ? "Assets" : $"Assets/{suffix}";
            return true;
        }

        private static bool IsProjectAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalized = path.Replace("\\", "/").Trim();
            return string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }

        private static string AbsoluteFromAssets(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return Application.dataPath;
            var dataPath = Application.dataPath.Replace("\\", "/");
            if (relPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return dataPath + "/" + relPath.Substring("Assets/".Length);
            return dataPath + "/" + relPath;
        }

        private static string SafeFile(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "DialogGraph";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim('_').Trim();
        }

        #endregion
    }
}
