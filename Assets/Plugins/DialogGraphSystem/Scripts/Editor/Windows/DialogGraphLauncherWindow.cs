using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    public class DialogGraphLauncherWindow : EditorWindow
    {
        #region -------------------- Settings --------------------
        [SerializeField] private bool doDebug = false;
        private const string LAST_GRAPH_PREF_KEY = "DialogGraph_LastGraphName";
        #endregion

        #region -------------------- UI State --------------------
        private PopupField<string> _existingGraphsPopup;
        private TextField _newGraphNameField;
        private Button _openLastButton;
        private Label _lastGraphLabel;

        private string _lastGraphName;
        #endregion

        #region -------------------- Menu --------------------
        //[MenuItem(TextResources.MENU_DIALOGUE_GRAPH_SYSTEM_GRAPHS, priority = 1)]
        private static void OpenFromLegacyMenu()
        {
            DialogSystemMainWindow.OpenGraphs();
        }

        [System.Obsolete("Use DialogSystemMainWindow instead. This window is kept for compatibility.")]
        public static void Open()
        {
            DialogSystemMainWindow.OpenGraphs();
        }
        #endregion

        #region -------------------- Unity --------------------
        private void OnEnable()
        {
            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null)
                rootVisualElement.styleSheets.Add(ss);

            BuildUI();
        }
        #endregion

        #region -------------------- UI Build --------------------
        private void BuildUI()
        {
            rootVisualElement.Clear();

            var allGraphs = GetAllGraphAssetNames();
            _lastGraphName = PlayerPrefs.GetString(LAST_GRAPH_PREF_KEY, string.Empty);
            bool lastExists = !string.IsNullOrEmpty(_lastGraphName) && allGraphs.Contains(_lastGraphName);

            // Root — fills the entire window with dark background via USS
            var root = new VisualElement();
            root.AddToClassList("dlg-launcher-root");
            rootVisualElement.Add(root);

            // ── Header bar ────────────────────────────────────────────
            var header = new VisualElement();
            header.AddToClassList("dlg-launcher-header");

            var title = new Label("Dialogue Graphs");
            title.AddToClassList("dlg-launcher-title");
            header.Add(title);

            var subtitle = new Label("Choose a graph to edit or create a new one.");
            subtitle.AddToClassList("dlg-launcher-subtitle");
            header.Add(subtitle);

            root.Add(header);

            // ── Body (cards) ──────────────────────────────────────────
            var body = new VisualElement();
            body.AddToClassList("dlg-launcher-body");
            root.Add(body);

            // Shadow 'root' for cards now points to body
            root = body;

            // ---------------- LAST GRAPH SECTION ----------------
            if (lastExists)
            {
                var lastBox = MakeSectionBox("Last Opened Graph");

                var row = new VisualElement();
                row.AddToClassList("dlg-launcher-row");
                row.AddToClassList("space-between");

                _lastGraphLabel = new Label(_lastGraphName);
                _lastGraphLabel.AddToClassList("dlg-launcher-graph-name");

                _openLastButton = new Button(OnClickOpenLast) { text = "Open Last" };
                _openLastButton.AddToClassList("dlg-btn");
                _openLastButton.AddToClassList("primary");
                _openLastButton.AddToClassList("dlg-launcher-action-button");

                row.Add(_lastGraphLabel);
                row.Add(_openLastButton);

                lastBox.Add(row);
                root.Add(lastBox);
            }

            // ---------------- EXISTING GRAPHS SECTION ----------------
            var existingBox = MakeSectionBox("Existing Graphs");

            if (allGraphs.Count > 0)
            {
                _existingGraphsPopup = new PopupField<string>(
                    label: "Graph:",
                    choices: allGraphs,
                    defaultIndex: 0
                );
                _existingGraphsPopup.style.flexGrow = 1;
                _existingGraphsPopup.AddToClassList("dlg-popup");
                _existingGraphsPopup.AddToClassList("tight-label");

                var openBtn = new Button(OnClickOpenSelected) { text = "Open Selected" };
                openBtn.AddToClassList("dlg-btn");
                openBtn.AddToClassList("secondary");
                openBtn.AddToClassList("dlg-launcher-action-button");

                var row = new VisualElement();
                row.AddToClassList("dlg-launcher-row");
                row.Add(_existingGraphsPopup);
                row.Add(openBtn);

                existingBox.Add(row);
            }
            else
            {
                existingBox.Add(new Label("No dialogue graph assets found yet."));
            }

            root.Add(existingBox);

            // ---------------- CREATE NEW SECTION ----------------
            var createBox = MakeSectionBox("Create New Graph");

            _newGraphNameField = new TextField("Name:")
            {
                value = string.IsNullOrEmpty(_lastGraphName) ? "DialogGraph" : _lastGraphName + "_Copy"
            };
            _newGraphNameField.style.flexGrow = 1;
            _newGraphNameField.AddToClassList("dlg-textfield");
            _newGraphNameField.AddToClassList("dlg-launcher-textfield");

            var createBtn = new Button(OnClickCreateNew) { text = "Create & Open" };
            createBtn.AddToClassList("dlg-btn");
            createBtn.AddToClassList("success");
            createBtn.AddToClassList("dlg-launcher-action-button");

            var createActions = new VisualElement();
            createActions.AddToClassList("dlg-launcher-actions");
            createActions.Add(createBtn);

            createBox.Add(_newGraphNameField);
            createBox.Add(createActions);

            root.Add(createBox);

            // If absolutely no graphs exist and no last graph, focus on create
            if (allGraphs.Count == 0 && _newGraphNameField != null)
                _newGraphNameField.Focus();
        }

        private VisualElement MakeSectionBox(string header)
        {
            var box = new VisualElement();
            box.AddToClassList("dlg-section");

            var headerLabel = new Label(header);
            headerLabel.AddToClassList("dlg-section-title");
            box.Add(headerLabel);

            return box;
        }
        #endregion

        #region -------------------- Button Handlers --------------------
        private void OnClickOpenLast()
        {
            if (string.IsNullOrEmpty(_lastGraphName))
                return;

            var all = GetAllGraphAssetNames();
            if (!all.Contains(_lastGraphName))
            {
                EditorUtility.DisplayDialog("Graph Not Found",
                    $"The last graph '{_lastGraphName}' no longer exists.", "OK");
                return;
            }

            OpenGraphAndClose(_lastGraphName);
        }

        private void OnClickOpenSelected()
        {
            if (_existingGraphsPopup == null || string.IsNullOrEmpty(_existingGraphsPopup.value))
            {
                EditorUtility.DisplayDialog("No Graph Selected", "Please select a graph from the list.", "OK");
                return;
            }

            OpenGraphAndClose(_existingGraphsPopup.value);
        }

        private void OnClickCreateNew()
        {
            var rawName = _newGraphNameField != null ? _newGraphNameField.value : "DialogGraph";
            var finalName = rawName.Trim();

            if (string.IsNullOrEmpty(finalName))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Please enter a valid graph name.", "OK");
                return;
            }

            var allGraphs = GetAllGraphAssetNames();
            if (allGraphs.Contains(finalName))
            {
                var choice = EditorUtility.DisplayDialogComplex(
                    "Graph Already Exists",
                    $"A dialogue graph named '{finalName}' already exists.\n\nWhat would you like to do?",
                    "Open Existing",
                    "Cancel",
                    "Create Anyway (Overwrite)"
                );

                // 0 = Open Existing
                if (choice == 0)
                {
                    OpenGraphAndClose(finalName);
                    return;
                }
                // 1 = Cancel
                if (choice == 1)
                    return;
                // 2 = Overwrite → continue to create new asset with same name
            }

            CreateDialogGraphAsset(finalName);
            OpenGraphAndClose(finalName);
        }
        #endregion

        #region -------------------- Helpers --------------------
        private void OpenGraphAndClose(string graphName)
        {
            if (doDebug)
                Debug.Log($"[DialogGraphLauncherWindow] Opening graph '{graphName}'");

            PlayerPrefs.SetString(LAST_GRAPH_PREF_KEY, graphName);
            PlayerPrefs.Save();

            DialogSystemMainWindow.OpenWithGraph(graphName);
            Close(); // launcher closes, main window takes over
        }

        private void CreateDialogGraphAsset(string graphName)
        {
            DialogGraphAssetPaths.EnsurePrimaryGraphFolderExists();
            var path = DialogGraphAssetPaths.GetPrimaryGraphAssetPath(graphName);
            var existing = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.AssignGraphGuidIfMissing(Guid.NewGuid().ToString("N"));
            graph.MarkSchemaCurrentForMigration();
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (doDebug)
                Debug.Log($"[DialogGraphLauncherWindow] Created DialogGraph asset at: {path}");
        }

        private void EnsureFolderExistsUnderAssets(string folder)
        {
            var normalized = folder.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            var parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                Debug.LogError($"[DialogGraphLauncherWindow] Folder must be under 'Assets/'. Current: '{folder}'");
                return;
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private List<string> GetAllGraphAssetNames()
        {
            return DialogGraphAssetPaths.GetVisibleGraphNames();
        }
        #endregion
    }
}