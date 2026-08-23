using System;
using System.Linq;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.EditorTools.Resources;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Dockable editor panel for graph validation results.
    /// </summary>
    public sealed class DialogGraphValidationPanelWindow : EditorWindow
    {
        private static readonly Color Background = new Color(0.13f, 0.14f, 0.16f);
        private static readonly Color HeaderBackground = new Color(0.11f, 0.12f, 0.14f);
        private static readonly Color BorderColor = new Color(0.24f, 0.26f, 0.30f);
        private static readonly Color TextPrimary = new Color(0.86f, 0.88f, 0.92f);
        private static readonly Color TextMuted = new Color(0.55f, 0.58f, 0.64f);
        private static readonly Color ErrorColor = new Color(0.95f, 0.48f, 0.48f);
        private static readonly Color WarningColor = new Color(0.95f, 0.74f, 0.33f);
        private static readonly Color InfoColor = new Color(0.50f, 0.70f, 0.92f);
        private static readonly Color SuccessColor = new Color(0.44f, 0.82f, 0.54f);

        private IDialogGraphOwner _owner;
        private DialogGraphValidationResult _result;
        private ScrollView _issueList;
        private Label _summaryLabel;
        private Label _graphLabel;
        private ToolbarToggle _showErrorsToggle;
        private ToolbarToggle _showWarningsToggle;
        private ToolbarToggle _showInfosToggle;

        /// <summary>
        /// Opens or updates the validation panel.
        /// </summary>
        public static void Open(IDialogGraphOwner owner, DialogGraphValidationResult result = null)
        {
            var window = GetWindow<DialogGraphValidationPanelWindow>();
            window.titleContent = new GUIContent("Graph Validation");
            window.minSize = new Vector2(480f, 360f);
            window._owner = owner ?? FindOpenGraphOwner();
            window._result = result ?? DialogGraphValidationRunner.ValidateOpenGraph(window._owner);
            window.Show();
            window.BuildUi();
        }

        /// <summary>
        /// Opens the validation panel from the Unity menu.
        /// </summary>
        //[MenuItem(TextResources.MENU_DIALOGUE_GRAPH_SYSTEM_VALIDATION_PANEL)]
        public static void OpenFromMenu()
        {
            Open(FindOpenGraphOwner());
        }

        private void OnEnable()
        {
            if (_owner == null)
            {
                _owner = FindOpenGraphOwner();
            }

            BuildUi();
        }

        private void BuildUi()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.backgroundColor = Background;
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1f;

            root.Add(BuildHeader());
            root.Add(BuildFilterBar());

            _issueList = new ScrollView(ScrollViewMode.Vertical);
            _issueList.style.flexGrow = 1f;
            _issueList.style.paddingLeft = 10f;
            _issueList.style.paddingRight = 10f;
            _issueList.style.paddingTop = 8f;
            _issueList.style.paddingBottom = 8f;
            root.Add(_issueList);

            RefreshIssueList();
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.style.backgroundColor = HeaderBackground;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = BorderColor;
            header.style.paddingLeft = 12f;
            header.style.paddingRight = 12f;
            header.style.paddingTop = 10f;
            header.style.paddingBottom = 10f;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var icon = DialogGraphIconManager.CreateImage(DialogGraphIconId.ToolbarValidate, "dgs-icon--sm", "dgs-icon--brand");
            icon.style.width = 18f;
            icon.style.height = 18f;
            icon.style.marginRight = 6f;
            titleRow.Add(icon);

            var title = new Label("Graph Validation");
            title.style.color = TextPrimary;
            title.style.fontSize = 14f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1f;
            titleRow.Add(title);

            var refreshButton = new Button(RefreshValidation) { text = "Refresh" };
            refreshButton.tooltip = "Validate the open graph again.";
            refreshButton.style.height = 24f;
            titleRow.Add(refreshButton);
            header.Add(titleRow);

            _graphLabel = new Label(ResolveGraphLabel());
            _graphLabel.style.color = TextMuted;
            _graphLabel.style.fontSize = 11f;
            _graphLabel.style.marginTop = 4f;
            header.Add(_graphLabel);

            _summaryLabel = new Label();
            _summaryLabel.style.color = TextPrimary;
            _summaryLabel.style.fontSize = 12f;
            _summaryLabel.style.marginTop = 6f;
            header.Add(_summaryLabel);
            RefreshSummary();

            return header;
        }

        private VisualElement BuildFilterBar()
        {
            var bar = new Toolbar();
            bar.style.backgroundColor = HeaderBackground;
            bar.style.borderBottomWidth = 1f;
            bar.style.borderBottomColor = BorderColor;

            _showErrorsToggle = BuildFilterToggle("Errors", true);
            _showWarningsToggle = BuildFilterToggle("Warnings", true);
            _showInfosToggle = BuildFilterToggle("Info", true);

            bar.Add(_showErrorsToggle);
            bar.Add(_showWarningsToggle);
            bar.Add(_showInfosToggle);
            return bar;
        }

        private ToolbarToggle BuildFilterToggle(string label, bool value)
        {
            var toggle = new ToolbarToggle { text = label };
            toggle.SetValueWithoutNotify(value);
            toggle.RegisterValueChangedCallback(_ => RefreshIssueList());
            return toggle;
        }

        private void RefreshValidation()
        {
            if (_owner == null)
            {
                _owner = FindOpenGraphOwner();
            }

            _result = DialogGraphValidationRunner.ValidateOpenGraph(_owner);
            if (_graphLabel != null)
            {
                _graphLabel.text = ResolveGraphLabel();
            }

            RefreshSummary();
            RefreshIssueList();
        }

        private void RefreshSummary()
        {
            if (_summaryLabel == null)
            {
                return;
            }

            if (_result == null)
            {
                _summaryLabel.text = "No graph is open.";
                _summaryLabel.style.color = WarningColor;
                return;
            }

            var clean = _result.Issues.Count == 0;
            _summaryLabel.text = clean
                ? "No issues found."
                : $"{_result.ErrorCount} error(s), {_result.WarningCount} warning(s), {_result.InfoCount} info.";
            _summaryLabel.style.color = clean ? SuccessColor : TextPrimary;
        }

        private void RefreshIssueList()
        {
            if (_issueList == null)
            {
                return;
            }

            _issueList.Clear();
            if (_result == null)
            {
                _issueList.Add(BuildEmptyState("No graph open", "Open a dialogue graph, then refresh validation."));
                return;
            }

            var visibleIssues = _result.Issues
                .Where(IsVisibleSeverity)
                .ToList();

            if (visibleIssues.Count == 0)
            {
                _issueList.Add(BuildEmptyState(
                    _result.Issues.Count == 0 ? "Graph is valid" : "No visible issues",
                    _result.Issues.Count == 0
                        ? "No validation errors, warnings, or notes were found."
                        : "Change the severity filters to show hidden results."));
                return;
            }

            foreach (var issue in visibleIssues)
            {
                _issueList.Add(BuildIssueRow(issue));
            }
        }

        private bool IsVisibleSeverity(DialogGraphValidationIssue issue)
        {
            return issue.Severity switch
            {
                DialogGraphValidationSeverity.Error => _showErrorsToggle == null || _showErrorsToggle.value,
                DialogGraphValidationSeverity.Warning => _showWarningsToggle == null || _showWarningsToggle.value,
                DialogGraphValidationSeverity.Info => _showInfosToggle == null || _showInfosToggle.value,
                _ => true
            };
        }

        private VisualElement BuildIssueRow(DialogGraphValidationIssue issue)
        {
            var color = GetSeverityColor(issue.Severity);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = new Color(color.r, color.g, color.b, 0.13f);
            row.style.borderLeftWidth = 4f;
            row.style.borderTopWidth = 1f;
            row.style.borderRightWidth = 1f;
            row.style.borderBottomWidth = 1f;
            row.style.borderLeftColor = color;
            row.style.borderTopColor = BorderColor;
            row.style.borderRightColor = BorderColor;
            row.style.borderBottomColor = BorderColor;
            row.style.borderTopLeftRadius = 4f;
            row.style.borderTopRightRadius = 4f;
            row.style.borderBottomLeftRadius = 4f;
            row.style.borderBottomRightRadius = 4f;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            row.style.paddingTop = 7f;
            row.style.paddingBottom = 7f;
            row.style.marginBottom = 6f;

            var textBlock = new VisualElement();
            textBlock.style.flexGrow = 1f;

            var code = new Label($"{issue.Severity} - {issue.Code}");
            code.style.color = color;
            code.style.fontSize = 10f;
            code.style.unityFontStyleAndWeight = FontStyle.Bold;
            textBlock.Add(code);

            var message = new Label(issue.Message);
            message.style.color = TextPrimary;
            message.style.fontSize = 11f;
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.marginTop = 2f;
            textBlock.Add(message);

            if (!string.IsNullOrWhiteSpace(issue.NodeGuid))
            {
                var guid = new Label(issue.NodeGuid);
                guid.style.color = TextMuted;
                guid.style.fontSize = 10f;
                guid.style.marginTop = 3f;
                textBlock.Add(guid);
            }

            row.Add(textBlock);

            if (!string.IsNullOrWhiteSpace(issue.NodeGuid))
            {
                row.RegisterCallback<MouseDownEvent>(_ => FocusIssue(issue.NodeGuid));

                var focusButton = new Button(() => FocusIssue(issue.NodeGuid)) { text = "Focus" };
                focusButton.tooltip = "Select and frame this node in the graph editor.";
                focusButton.style.marginLeft = 8f;
                focusButton.style.height = 24f;
                row.Add(focusButton);
            }

            return row;
        }

        private static VisualElement BuildEmptyState(string title, string message)
        {
            var box = new VisualElement();
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            box.style.flexGrow = 1f;
            box.style.paddingTop = 32f;

            var titleLabel = new Label(title);
            titleLabel.style.color = TextPrimary;
            titleLabel.style.fontSize = 13f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(titleLabel);

            var messageLabel = new Label(message);
            messageLabel.style.color = TextMuted;
            messageLabel.style.fontSize = 11f;
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            messageLabel.style.marginTop = 4f;
            box.Add(messageLabel);

            return box;
        }

        private void FocusIssue(string nodeGuid)
        {
            if (_owner == null)
            {
                _owner = FindOpenGraphOwner();
            }

            if (_owner == null || string.IsNullOrWhiteSpace(nodeGuid))
            {
                return;
            }

            if (!_owner.FocusNodeByGuid(nodeGuid))
            {
                Debug.LogWarning($"[DialogGraphValidationPanelWindow] Could not focus validation issue node '{nodeGuid}'. The graph may no longer be open or the node may have been removed.");
            }
        }

        private string ResolveGraphLabel()
        {
            if (_owner == null)
            {
                return "No graph editor detected.";
            }

            var graphName = _owner.GetCurrentGraphName();
            return string.IsNullOrWhiteSpace(graphName)
                ? "Graph editor open, no graph selected."
                : $"Current graph: {graphName}";
        }

        private static Color GetSeverityColor(DialogGraphValidationSeverity severity)
        {
            return severity switch
            {
                DialogGraphValidationSeverity.Error => ErrorColor,
                DialogGraphValidationSeverity.Warning => WarningColor,
                DialogGraphValidationSeverity.Info => InfoColor,
                _ => TextPrimary
            };
        }

        private static IDialogGraphOwner FindOpenGraphOwner()
        {
            var mainWindow = UnityEngine.Resources
                .FindObjectsOfTypeAll<DialogSystemMainWindow>()
                .FirstOrDefault(window =>
                    window?.GetGraphOwner() != null &&
                    !string.IsNullOrWhiteSpace(window.GetGraphOwner().GetCurrentGraphName()));
            if (mainWindow != null)
            {
                return mainWindow.GetGraphOwner();
            }

            var legacyWindow = UnityEngine.Resources
                .FindObjectsOfTypeAll<DialogGraphEditorWindow>()
                .FirstOrDefault(window => window != null && !string.IsNullOrWhiteSpace(window.GetCurrentGraphName()));
            if (legacyWindow != null)
            {
                return legacyWindow;
            }

            mainWindow = UnityEngine.Resources
                .FindObjectsOfTypeAll<DialogSystemMainWindow>()
                .FirstOrDefault(window => window != null);
            return mainWindow?.GetGraphOwner();
        }
    }
}
