using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Popup that asks for a graph name and save folder before creating a new dialogue graph.
    /// Styled to match the Dialog Graph Editor design system.
    /// </summary>
    internal class NewGraphPromptWindow : EditorWindow
    {
        #region ---------------- State ----------------
        private Action<string, string> _onConfirm; // (graphName, assetFolderPath)
        private string _suggestedName;
        private List<string> _existingNames = new();

        private string _selectedFolder;
        #endregion

        #region ---------------- UI refs ----------------
        private TextField _nameField;
        private Label     _folderDisplay;
        private Label     _previewLabel;
        private Label     _warningLabel;
        private Button    _createBtn;
        #endregion

        #region ---------------- Open ----------------
        public static void Open(
            string suggestedName,
            List<string> existingNames,
            Action<string, string> onConfirm)
        {
            var w = CreateInstance<NewGraphPromptWindow>();
            w.titleContent    = new GUIContent("New Dialogue Graph");
            w._suggestedName  = string.IsNullOrWhiteSpace(suggestedName) ? "NewDialogue" : suggestedName.Trim();
            w._existingNames  = existingNames ?? new List<string>();
            w._selectedFolder = TextResources.GRAPHS_FOLDER;
            w._onConfirm      = onConfirm;

            w.minSize = new Vector2(440, 230);
            w.maxSize = new Vector2(800, 310);
            w.ShowUtility();
            w.BuildUI();
            w.Focus();
        }
        #endregion

        #region ---------------- UI Build ----------------
        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null) root.styleSheets.Add(ss);

            // Window root — dark background to match the editor
            root.style.flexDirection    = FlexDirection.Column;
            root.style.backgroundColor  = new StyleColor(new Color32(0x1a, 0x1c, 0x20, 0xff));

            // ── Header ────────────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.backgroundColor = new StyleColor(new Color32(0x1e, 0x21, 0x26, 0xff));
            header.style.paddingLeft     = 16;
            header.style.paddingRight    = 16;
            header.style.paddingTop      = 14;
            header.style.paddingBottom   = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color32(0x2a, 0x2e, 0x35, 0xff));

            var title = new Label("New Dialogue Graph");
            title.style.fontSize                  = 15;
            title.style.unityFontStyleAndWeight    = FontStyle.Bold;
            title.style.color                      = new StyleColor(new Color32(0xd4, 0xd8, 0xe0, 0xff));
            title.style.marginBottom               = 3;

            var subtitle = new Label("Name your graph and choose where to save it.");
            subtitle.style.fontSize = 11;
            subtitle.style.color    = new StyleColor(new Color32(0x5a, 0x63, 0x73, 0xff));

            header.Add(title);
            header.Add(subtitle);
            root.Add(header);

            // ── Body ──────────────────────────────────────────────────────────
            var body = new VisualElement();
            body.style.flexGrow    = 1;
            body.style.paddingLeft = body.style.paddingRight  = 16;
            body.style.paddingTop  = body.style.paddingBottom = 14;
            root.Add(body);

            // ── Name row ──────────────────────────────────────────────────────
            _nameField = new TextField("Name")
            {
                value     = _suggestedName,
                isDelayed = false
            };
            _nameField.AddToClassList("dlg-textfield");
            _nameField.AddToClassList("tight-label");
            StyleField(_nameField);
            body.Add(_nameField);

            // ── Folder row ────────────────────────────────────────────────────
            // Uses a label + path display + Browse button so the button never clips
            var folderRow = new VisualElement();
            folderRow.style.flexDirection = FlexDirection.Row;
            folderRow.style.alignItems    = Align.Center;
            folderRow.style.marginBottom  = 2;

            var folderLabel = new Label("Folder");
            folderLabel.style.fontSize    = 11;
            folderLabel.style.color       = new StyleColor(new Color32(0x8c, 0x92, 0xa0, 0xff));
            folderLabel.style.minWidth    = 42;
            folderLabel.style.width       = 42;
            folderLabel.style.marginRight = 4;
            folderLabel.style.flexShrink  = 0;

            _folderDisplay = new Label(_selectedFolder);
            _folderDisplay.AddToClassList("dlg-io-path-display");
            _folderDisplay.style.flexGrow   = 1;
            _folderDisplay.style.minWidth   = 0;
            _folderDisplay.style.marginRight = 6;

            var browseBtn = new Button(OnClickBrowse) { text = "Browse…" };
            browseBtn.AddToClassList("dlg-btn");
            browseBtn.AddToClassList("secondary");
            browseBtn.style.flexShrink = 0;
            browseBtn.style.minWidth   = 72;
            browseBtn.style.height     = 24;
            browseBtn.style.marginLeft = 0;

            folderRow.Add(folderLabel);
            folderRow.Add(_folderDisplay);
            folderRow.Add(browseBtn);
            body.Add(folderRow);

            // ── Preview ───────────────────────────────────────────────────────
            _previewLabel = new Label();
            _previewLabel.style.fontSize    = 10;
            _previewLabel.style.color       = new StyleColor(new Color32(0x4a, 0x52, 0x64, 0xff));
            _previewLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _previewLabel.style.marginTop   = 5;
            _previewLabel.style.marginBottom = 2;
            _previewLabel.style.whiteSpace  = WhiteSpace.Normal;
            body.Add(_previewLabel);

            // ── Warning ───────────────────────────────────────────────────────
            _warningLabel = new Label();
            _warningLabel.style.fontSize   = 10;
            _warningLabel.style.marginTop  = 2;
            _warningLabel.style.whiteSpace = WhiteSpace.Normal;
            body.Add(_warningLabel);

            // ── Footer / button row ───────────────────────────────────────────
            var footer = new VisualElement();
            footer.style.flexDirection   = FlexDirection.Row;
            footer.style.justifyContent  = Justify.FlexEnd;
            footer.style.alignItems      = Align.Center;
            footer.style.paddingLeft     = 16;
            footer.style.paddingRight    = 16;
            footer.style.paddingTop      = 10;
            footer.style.paddingBottom   = 12;
            footer.style.borderTopWidth  = 1;
            footer.style.borderTopColor  = new StyleColor(new Color32(0x2a, 0x2e, 0x35, 0xff));
            root.Add(footer);

            var cancelBtn = new Button(() => Close()) { text = "Cancel" };
            cancelBtn.AddToClassList("dlg-btn");
            cancelBtn.style.marginRight = 6;
            cancelBtn.style.minWidth    = 76;

            _createBtn = new Button(OnClickCreate) { text = "Create" };
            _createBtn.AddToClassList("dlg-btn");
            _createBtn.AddToClassList("primary");
            _createBtn.style.minWidth = 76;

            footer.Add(cancelBtn);
            footer.Add(_createBtn);

            // ── Events ────────────────────────────────────────────────────────
            _nameField.RegisterValueChangedCallback(_ => Refresh());

            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnClickCreate();
                    evt.StopImmediatePropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopImmediatePropagation();
                }
            });

            Refresh();

            // Auto-focus the name field so the user can type right away
            _nameField.schedule
                      .Execute(() => _nameField.Q<VisualElement>("unity-text-input")?.Focus())
                      .ExecuteLater(50);
        }

        /// <summary>Applies consistent bottom-margin to form fields.</summary>
        private static void StyleField(VisualElement field)
        {
            field.style.marginBottom = 10;
        }
        #endregion

        #region ---------------- Logic ----------------
        private void OnClickBrowse()
        {
            var startPath = Application.dataPath;
            var picked    = EditorUtility.OpenFolderPanel("Choose save folder", startPath, "");
            if (string.IsNullOrEmpty(picked)) return;

            var relative = AbsoluteToAssetPath(picked);
            if (string.IsNullOrEmpty(relative))
            {
                EditorUtility.DisplayDialog(
                    "Folder outside project",
                    "Please choose a folder inside this project's Assets directory.",
                    "OK");
                return;
            }

            _selectedFolder = relative;
            if (_folderDisplay != null)
                _folderDisplay.text = _selectedFolder;

            Refresh();
        }

        private void OnClickCreate()
        {
            var name   = SanitizeName(_nameField.value);
            var folder = NormalizeFolder(_selectedFolder);

            if (string.IsNullOrEmpty(name))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Please enter a valid graph name.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid Folder",
                    "The save folder must be inside the project's Assets directory.", "OK");
                return;
            }

            bool exists = _existingNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                if (!EditorUtility.DisplayDialog(
                        "Name Already Exists",
                        $"A DialogGraph named \"{name}\" already exists.\n\nCreate anyway? The existing asset will be overwritten.",
                        "Create", "Cancel"))
                    return;
            }

            _onConfirm?.Invoke(name, folder);
            Close();
        }

        private void Refresh()
        {
            var name   = SanitizeName(_nameField?.value ?? "");
            var folder = NormalizeFolder(_selectedFolder ?? "");

            // Preview
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(folder))
                _previewLabel.text = $"→ {folder}/{name}.asset";
            else
                _previewLabel.text = string.Empty;

            // Duplicate warning
            bool exists = !string.IsNullOrEmpty(name) &&
                          _existingNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                _warningLabel.text        = $"\"{name}\" already exists — creating will overwrite it.";
                _warningLabel.style.color = new StyleColor(new Color32(0xe0, 0xa0, 0x30, 0xff));
            }
            else
            {
                _warningLabel.text        = string.Empty;
                _warningLabel.style.color = StyleKeyword.Null;
            }

            bool valid = !string.IsNullOrEmpty(name) &&
                         !string.IsNullOrEmpty(folder) &&
                         folder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase);

            if (_createBtn != null) _createBtn.SetEnabled(valid);
        }
        #endregion

        #region ---------------- Helpers ----------------
        private static string SanitizeName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            return new string(s.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        }

        private static string NormalizeFolder(string path) =>
            (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');

        private static string AbsoluteToAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return null;

            var normalized  = absolutePath.Replace('\\', '/');
            var dataPath    = Application.dataPath.Replace('\\', '/'); // .../Assets
            var projectRoot = dataPath.Length > 6
                ? dataPath.Substring(0, dataPath.Length - 6)           // strip "Assets"
                : dataPath;

            if (!normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return null;

            var relative = normalized.Substring(projectRoot.Length).TrimStart('/');
            return relative.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) ? relative : null;
        }
        #endregion
    }
}
