using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    internal class SaveGraphPromptWindow : EditorWindow
    {
        #region ---------------- Debug ----------------
        [SerializeField] private bool doDebug = false;
        #endregion

        #region ---------------- Types ----------------
        public enum SaveMode
        {
            UseLoaded,
            OverwriteExisting,
            SaveAsNew
        }
        #endregion

        #region ---------------- Callbacks & State ----------------
        private Action<string> _onConfirm;
        private Action _onCancel;

        private string _currentSuggestedName;
        private string _loadedGraphName;
        private List<string> _existingNames = new();
        private bool _isGraphEmpty;
        #endregion

        #region ---------------- UI Elements ----------------
        private EnumField    _modeField;
        private TextField    _newNameField;
        private PopupField<string> _existingPopup;
        private Label        _modeDescriptionLabel;
        private Label        _targetPreviewLabel;
        private Label        _warningLabel;
        private Button       _saveBtn;
        #endregion

        #region ---------------- Open ----------------
        public static void Open(
            string currentName,
            string loadedGraphName,
            List<string> existingNames,
            bool isGraphEmpty,
            Action<string> onConfirm,
            Action onCancel = null)
        {
            var w = CreateInstance<SaveGraphPromptWindow>();
            w.titleContent = new GUIContent("Save Dialogue Graph");

            w._onConfirm              = onConfirm;
            w._onCancel               = onCancel;
            w._currentSuggestedName   = string.IsNullOrEmpty(currentName) ? "NewDialogue" : currentName;
            w._loadedGraphName        = loadedGraphName;
            w._existingNames          = existingNames != null
                ? existingNames.Distinct().OrderBy(n => n).ToList()
                : new List<string>();
            w._isGraphEmpty = isGraphEmpty;

            w.minSize = new Vector2(480, 300);
            w.maxSize = new Vector2(860, 400);
            w.ShowUtility();
            w.BuildUI();
            w.Focus();

            if (w.doDebug)
                Debug.Log($"[SaveGraphPromptWindow] Opened – current='{w._currentSuggestedName}', loaded='{w._loadedGraphName}', existing={w._existingNames.Count}, empty={w._isGraphEmpty}");
        }
        #endregion

        #region ---------------- UI Build ----------------
        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (ss != null) root.styleSheets.Add(ss);

            root.style.flexDirection   = FlexDirection.Column;
            root.style.backgroundColor = new StyleColor(new Color32(0x1a, 0x1c, 0x20, 0xff));

            // ── Header ────────────────────────────────────────────────────────
            var header = new VisualElement();
            header.style.backgroundColor   = new StyleColor(new Color32(0x1e, 0x21, 0x26, 0xff));
            header.style.paddingLeft       = 16;
            header.style.paddingRight      = 16;
            header.style.paddingTop        = 14;
            header.style.paddingBottom     = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color32(0x2a, 0x2e, 0x35, 0xff));

            var titleLbl = new Label("Save Dialogue Graph");
            titleLbl.style.fontSize               = 15;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color                  = new StyleColor(new Color32(0xd4, 0xd8, 0xe0, 0xff));
            titleLbl.style.marginBottom           = 3;

            var subtitleLbl = new Label("Choose how to save the current graph.");
            subtitleLbl.style.fontSize = 11;
            subtitleLbl.style.color    = new StyleColor(new Color32(0x5a, 0x63, 0x73, 0xff));

            header.Add(titleLbl);
            header.Add(subtitleLbl);
            root.Add(header);

            // ── Body ──────────────────────────────────────────────────────────
            var body = new VisualElement();
            body.style.flexGrow    = 1;
            body.style.paddingLeft = body.style.paddingRight  = 16;
            body.style.paddingTop  = body.style.paddingBottom = 14;
            root.Add(body);

            // Mode selector
            var defaultMode = !string.IsNullOrEmpty(_loadedGraphName)
                ? SaveMode.UseLoaded
                : SaveMode.SaveAsNew;

            _modeField = new EnumField("Mode", defaultMode);
            _modeField.AddToClassList("dlg-textfield");
            _modeField.AddToClassList("tight-label");
            _modeField.style.marginBottom = 6;
            body.Add(_modeField);

            // Mode description
            _modeDescriptionLabel = new Label();
            _modeDescriptionLabel.style.fontSize   = 10;
            _modeDescriptionLabel.style.color      = new StyleColor(new Color32(0x5a, 0x63, 0x73, 0xff));
            _modeDescriptionLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _modeDescriptionLabel.style.marginBottom = 10;
            _modeDescriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            body.Add(_modeDescriptionLabel);

            // Currently loaded info
            if (!string.IsNullOrEmpty(_loadedGraphName))
            {
                var loadedRow = new VisualElement();
                loadedRow.style.flexDirection  = FlexDirection.Row;
                loadedRow.style.alignItems     = Align.Center;
                loadedRow.style.marginBottom   = 8;

                var loadedKey = new Label("Loaded:");
                loadedKey.style.fontSize    = 10;
                loadedKey.style.color       = new StyleColor(new Color32(0x5a, 0x60, 0x70, 0xff));
                loadedKey.style.minWidth    = 52;
                loadedKey.style.marginRight = 6;
                loadedKey.style.flexShrink  = 0;

                var loadedVal = new Label(_loadedGraphName);
                loadedVal.style.fontSize = 11;
                loadedVal.style.color    = new StyleColor(new Color32(0xd4, 0xd8, 0xe0, 0xff));
                loadedVal.style.unityFontStyleAndWeight = FontStyle.Bold;

                loadedRow.Add(loadedKey);
                loadedRow.Add(loadedVal);
                body.Add(loadedRow);
            }

            // Overwrite-existing popup
            var popupChoices = _existingNames.Count > 0
                ? _existingNames
                : new List<string> { "(no dialogue graphs found)" };

            _existingPopup = new PopupField<string>("Overwrite", popupChoices, 0);
            _existingPopup.AddToClassList("dlg-popup");
            _existingPopup.AddToClassList("tight-label");
            _existingPopup.SetEnabled(_existingNames.Count > 0);
            _existingPopup.style.marginBottom = 8;
            body.Add(_existingPopup);

            // Save-As name field
            _newNameField = new TextField("Save As")
            {
                value     = _currentSuggestedName,
                isDelayed = true
            };
            _newNameField.AddToClassList("dlg-textfield");
            _newNameField.AddToClassList("tight-label");
            _newNameField.style.marginBottom = 8;
            body.Add(_newNameField);

            // Target preview
            _targetPreviewLabel = new Label();
            _targetPreviewLabel.style.fontSize   = 10;
            _targetPreviewLabel.style.color      = new StyleColor(new Color32(0x4a, 0x52, 0x64, 0xff));
            _targetPreviewLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _targetPreviewLabel.style.marginBottom = 4;
            _targetPreviewLabel.style.whiteSpace = WhiteSpace.Normal;
            body.Add(_targetPreviewLabel);

            // Warning label
            _warningLabel = new Label();
            _warningLabel.style.fontSize   = 10;
            _warningLabel.style.marginTop  = 2;
            _warningLabel.style.whiteSpace = WhiteSpace.Normal;
            body.Add(_warningLabel);

            if (_isGraphEmpty)
            {
                _warningLabel.text        = "Warning: current graph appears to be empty. Overwriting an existing graph will erase its content.";
                _warningLabel.style.color = new StyleColor(new Color32(0xe0, 0xa0, 0x30, 0xff));
            }

            // ── Footer ────────────────────────────────────────────────────────
            var footer = new VisualElement();
            footer.style.flexDirection  = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.alignItems     = Align.Center;
            footer.style.paddingLeft    = 16;
            footer.style.paddingRight   = 16;
            footer.style.paddingTop     = 10;
            footer.style.paddingBottom  = 12;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new StyleColor(new Color32(0x2a, 0x2e, 0x35, 0xff));
            root.Add(footer);

            var cancelBtn = new Button(() => { _onCancel?.Invoke(); Close(); }) { text = "Cancel" };
            cancelBtn.AddToClassList("dlg-btn");
            cancelBtn.style.marginRight = 6;
            cancelBtn.style.minWidth    = 76;

            _saveBtn = new Button(OnClickSave) { text = "Save" };
            _saveBtn.AddToClassList("dlg-btn");
            _saveBtn.AddToClassList("primary");
            _saveBtn.style.minWidth = 76;

            footer.Add(cancelBtn);
            footer.Add(_saveBtn);

            // ── Events ────────────────────────────────────────────────────────
            _modeField.RegisterValueChangedCallback(_ => UpdateUIState());
            _newNameField.RegisterValueChangedCallback(_ => UpdateUIState());
            _existingPopup?.RegisterValueChangedCallback(_ => UpdateUIState());

            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnClickSave();
                    evt.StopImmediatePropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    _onCancel?.Invoke();
                    Close();
                    evt.StopImmediatePropagation();
                }
            });

            UpdateUIState();
        }
        #endregion

        #region ---------------- Logic ----------------
        private void UpdateUIState()
        {
            var mode = (SaveMode)_modeField.value;

            // Defensive: revert UseLoaded if nothing is loaded
            if (string.IsNullOrEmpty(_loadedGraphName) && mode == SaveMode.UseLoaded)
            {
                _modeField.SetValueWithoutNotify(SaveMode.SaveAsNew);
                mode = SaveMode.SaveAsNew;
            }

            bool canOverwrite = mode == SaveMode.OverwriteExisting && _existingNames.Count > 0;
            bool canSaveAsNew = mode == SaveMode.SaveAsNew;

            _existingPopup?.SetEnabled(canOverwrite);
            _newNameField?.SetEnabled(canSaveAsNew);

            // Mode description
            _modeDescriptionLabel.text = mode switch
            {
                SaveMode.UseLoaded        => "Save directly into the currently loaded graph.",
                SaveMode.OverwriteExisting => "Overwrite another existing dialogue graph with the current content.",
                SaveMode.SaveAsNew        => "Create a new dialogue graph asset with the specified name.",
                _                         => string.Empty
            };

            // Target preview
            string previewName = mode switch
            {
                SaveMode.UseLoaded        => _loadedGraphName,
                SaveMode.OverwriteExisting => _existingNames.Count > 0 ? _existingPopup.value : "(no target)",
                SaveMode.SaveAsNew        => SanitizeName(_newNameField.value),
                _                         => null
            };

            _targetPreviewLabel.text = string.IsNullOrEmpty(previewName)
                ? string.Empty
                : $"→ {previewName}.asset";

            // Warning (reset unless graph is empty)
            if (!_isGraphEmpty)
            {
                _warningLabel.text        = string.Empty;
                _warningLabel.style.color = StyleKeyword.Null;
            }

            if (mode == SaveMode.SaveAsNew && !string.IsNullOrEmpty(previewName))
            {
                bool nameExists = _existingNames.Any(n =>
                    string.Equals(n, previewName, StringComparison.OrdinalIgnoreCase));

                if (nameExists)
                {
                    _warningLabel.text        = $"\"{previewName}\" already exists — saving will overwrite it.";
                    _warningLabel.style.color = new StyleColor(new Color32(0xe0, 0xa0, 0x30, 0xff));
                }
            }

            if (_isGraphEmpty)
            {
                _warningLabel.text = string.IsNullOrEmpty(_warningLabel.text)
                    ? "Warning: current graph appears to be empty. Overwriting an existing graph will erase its content."
                    : _warningLabel.text;
                _warningLabel.style.color = new StyleColor(new Color32(0xe0, 0xa0, 0x30, 0xff));
            }

            bool canSave =
                (mode == SaveMode.UseLoaded        && !string.IsNullOrEmpty(_loadedGraphName)) ||
                (mode == SaveMode.OverwriteExisting && _existingNames.Count > 0)               ||
                (mode == SaveMode.SaveAsNew        && !string.IsNullOrEmpty(previewName));

            _saveBtn.SetEnabled(canSave);
        }

        private void OnClickSave()
        {
            var mode = (SaveMode)_modeField.value;
            string finalName = null;

            switch (mode)
            {
                case SaveMode.UseLoaded:
                    finalName = _loadedGraphName;
                    break;

                case SaveMode.OverwriteExisting:
                    finalName = _existingNames.Count > 0 ? _existingPopup.value : null;
                    break;

                case SaveMode.SaveAsNew:
                    finalName = SanitizeName(_newNameField.value);
                    if (string.IsNullOrEmpty(finalName))
                    {
                        EditorUtility.DisplayDialog("Invalid Name", "Please enter a valid file name.", "OK");
                        return;
                    }

                    if (_existingNames.Any(n => string.Equals(n, finalName, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!EditorUtility.DisplayDialog(
                                "Name Already Exists",
                                $"A dialogue graph named \"{finalName}\" already exists.\n\nOverwrite it?",
                                "Overwrite", "Cancel"))
                            return;
                    }
                    break;
            }

            if (string.IsNullOrEmpty(finalName))
            {
                EditorUtility.DisplayDialog("No Selection", "Please choose a valid target to save.", "OK");
                return;
            }

            if (_isGraphEmpty)
            {
                if (!EditorUtility.DisplayDialog(
                        "Save Empty Graph?",
                        $"You are about to save an empty graph as \"{finalName}\". " +
                        "If a graph with this name exists, its content will be lost.",
                        "Save Anyway", "Cancel"))
                    return;
            }

            if (doDebug)
                Debug.Log($"[SaveGraphPromptWindow] Confirmed save as '{finalName}' (mode={mode}).");

            _onConfirm?.Invoke(finalName);
            Close();
        }
        #endregion

        #region ---------------- Helpers ----------------
        private static string SanitizeName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            return new string(s.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        }
        #endregion
    }
}
