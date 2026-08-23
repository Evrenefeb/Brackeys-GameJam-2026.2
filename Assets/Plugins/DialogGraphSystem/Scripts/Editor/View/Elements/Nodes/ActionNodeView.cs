using System;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Resources;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models.Nodes;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.View;
using System.Text;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    public class ActionNodeView : BaseNodeView<ActionNode>
    {
        public Port inputPort { get; private set; }
        public Port outputPort { get; private set; }

        private DialogGraphView _graphView;
        private TextField _actionIdField;
        private ObjectField _actionDefinitionField;
        private Button _createActionButton;
        private Label _actionStatusLabel;
        private TextField _payloadField;
        private TextField _payloadInstructionField;
        private Label _payloadStatusLabel;
        private Toggle _waitToggle;
        private FloatField _waitSecondsField;

        public string GUID { get; set; }
        public string actionId => _actionIdField?.value ?? string.Empty;
        public string payloadJson => _payloadField?.value ?? string.Empty;
        public bool waitForCompletion => _waitToggle != null && _waitToggle.value;
        public float waitSeconds => _waitSecondsField != null ? _waitSecondsField.value : 0f;

        public ActionNodeView(string guid, DialogGraphView graphView)
        {
            NodeUssLoader.ApplyTo(this);
            GUID = guid;
            title = "Action";
            this._graphView = graphView;
            AddToClassList("dlg-node");
            AddToClassList("type-action");
            style.minWidth = 300f;
            style.minHeight = 170f;
            BuildHeader();
            BuildBody();
            RebuildPorts();
            RefreshExpandedState();
            RefreshPorts();
        }

        private void BuildHeader()
        {
            titleContainer?.AddToClassList("node-title-action");
            var icon = DialogGraphIconManager.CreateImage(DialogGraphIconId.NodeAction, "dgs-icon--sm", "dgs-icon--action");
            icon.style.marginRight = 5f;
            icon.style.marginTop = 10f;
            titleContainer?.Insert(0, icon);
        }

        private void BuildBody()
        {
            titleButtonContainer.Clear();
            capabilities &= ~Capabilities.Collapsible;
            var content = mainContainer;
            var sectionAction = new VisualElement();
            sectionAction.AddToClassList("node-section");
            content.Add(sectionAction);
            var spacer = new VisualElement();
            spacer.AddToClassList("node-body-spacer");
            sectionAction.Add(spacer);
            _actionIdField = new TextField("Action ID") { isDelayed = true };
            _actionIdField.AddToClassList("node-field");
            _actionIdField.RegisterValueChangedCallback(e =>
            {
                if (data == null) return;
                Undo.RecordObject(data, "Edit Action ID");
                data.actionId = e.newValue ?? string.Empty;
                MarkDirty(data);
                RefreshActionDefinitionUi();
            });
            sectionAction.Add(_actionIdField);
            _actionDefinitionField = new ObjectField("Registered Action") { objectType = typeof(DialogActionSO), allowSceneObjects = false };
            _actionDefinitionField.AddToClassList("node-field");
            _actionDefinitionField.RegisterValueChangedCallback(e =>
            {
                if (e.newValue is DialogActionSO actionDefinition) ApplyActionDefinition(actionDefinition);
            });
            sectionAction.Add(_actionDefinitionField);
            _actionStatusLabel = new Label();
            _actionStatusLabel.AddToClassList("node-section-hint");
            sectionAction.Add(_actionStatusLabel);
            _createActionButton = new Button(OnClickCreateAction) { text = "Create Action" };
            _createActionButton.AddToClassList("node-mini-button");
            sectionAction.Add(_createActionButton);
            var advancedFoldout = new Foldout { text = "Payload & Timing", value = false };
            advancedFoldout.AddToClassList("node-foldout");
            content.Add(advancedFoldout);
            _payloadField = new TextField("Payload (JSON)") { multiline = true, isDelayed = true };
            _payloadField.AddToClassList("json-box");
            _payloadField.RegisterValueChangedCallback(e =>
            {
                if (data == null) return;
                Undo.RecordObject(data, "Edit Action Payload");
                data.payloadJson = e.newValue ?? string.Empty;
                MarkDirty(data);
            });
            advancedFoldout.Add(_payloadField);
            _payloadInstructionField = new TextField("Payload Request") { isDelayed = true };
            _payloadInstructionField.AddToClassList("node-field");
            advancedFoldout.Add(_payloadInstructionField);
            var payloadActions = new VisualElement();
            payloadActions.style.flexDirection = FlexDirection.Row;
            var writePayloadButton = new Button(OnClickWritePayload) { text = "Write Payload" };
            writePayloadButton.AddToClassList("node-mini-button");
            payloadActions.Add(writePayloadButton);
            advancedFoldout.Add(payloadActions);
            _payloadStatusLabel = new Label();
            _payloadStatusLabel.AddToClassList("node-section-hint");
            advancedFoldout.Add(_payloadStatusLabel);
            _waitToggle = new Toggle("Wait For Completion");
            _waitToggle.AddToClassList("inline-toggle");
            _waitToggle.RegisterValueChangedCallback(e =>
            {
                if (data == null) return;
                Undo.RecordObject(data, "Toggle Wait For Completion");
                data.waitForCompletion = e.newValue;
                MarkDirty(data);
            });
            advancedFoldout.Add(_waitToggle);
            _waitSecondsField = new FloatField("Delay (sec)");
            _waitSecondsField.RegisterValueChangedCallback(e =>
            {
                if (data == null) return;
                Undo.RecordObject(data, "Edit Action Delay");
                data.waitSeconds = e.newValue;
                MarkDirty(data);
            });
            advancedFoldout.Add(_waitSecondsField);
            RefreshActionDefinitionUi();
        }

        public override void RebuildPorts()
        {
            inputContainer.Clear();
            outputContainer.Clear();
            inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            inputPort.portName = "In";
            inputContainer.Add(inputPort);
            outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            outputPort.portName = "Out";
            outputContainer.Add(outputPort);
        }

        public void LoadNodeData(string actionId, string payload, bool waitForCompletion, float waitSeconds)
        {
            _actionIdField?.SetValueWithoutNotify(actionId ?? string.Empty);
            _payloadField?.SetValueWithoutNotify(payload ?? string.Empty);
            _waitToggle?.SetValueWithoutNotify(waitForCompletion);
            _waitSecondsField?.SetValueWithoutNotify(waitSeconds);
            RefreshActionDefinitionUi();
        }

        public void ApplySidebarEdits(string updatedActionId, string updatedPayloadJson, bool updatedWaitForCompletion, float updatedWaitSeconds)
        {
            if (data == null) return;
            Undo.RecordObject(data, "Apply Sidebar Action Changes");
            data.actionId = updatedActionId ?? string.Empty;
            data.payloadJson = updatedPayloadJson ?? string.Empty;
            data.waitForCompletion = updatedWaitForCompletion;
            data.waitSeconds = updatedWaitSeconds;
            MarkDirty(data);
            _actionIdField?.SetValueWithoutNotify(data.actionId);
            _payloadField?.SetValueWithoutNotify(data.payloadJson);
            _waitToggle?.SetValueWithoutNotify(data.waitForCompletion);
            _waitSecondsField?.SetValueWithoutNotify(data.waitSeconds);
            RefreshActionDefinitionUi();
        }

        public void ApplyActionDefinition(DialogActionSO actionDefinition)
        {
            if (actionDefinition == null || data == null) return;
            Undo.RecordObject(data, "Assign Action Definition");
            data.actionId = actionDefinition.ActionID ?? string.Empty;
            if (string.IsNullOrWhiteSpace(data.payloadJson)) data.payloadJson = actionDefinition.DefaultPayloadJson ?? "{}";
            data.waitForCompletion = actionDefinition.WaitForCompletion;
            data.waitSeconds = actionDefinition.DefaultDelay;
            MarkDirty(data);
            _actionIdField?.SetValueWithoutNotify(data.actionId);
            _payloadField?.SetValueWithoutNotify(data.payloadJson ?? string.Empty);
            _waitToggle?.SetValueWithoutNotify(data.waitForCompletion);
            _waitSecondsField?.SetValueWithoutNotify(data.waitSeconds);
            RefreshActionDefinitionUi();
        }

        private void RefreshActionDefinitionUi()
        {
            var matchedAction = DialogGraphDefinitionResolver.FindActionById(actionId);
            _actionDefinitionField?.SetValueWithoutNotify(matchedAction);
            if (_actionStatusLabel != null)
                _actionStatusLabel.text = matchedAction != null ? $"Registered as {matchedAction.DisplayName}" : "Manual entry.";
            if (_createActionButton != null)
                _createActionButton.style.display = matchedAction == null && !string.IsNullOrWhiteSpace(actionId) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnClickCreateAction()
        {
            if (string.IsNullOrWhiteSpace(actionId)) return;
            var asset = _graphView?.GraphOwner?.CreateActionAsset(actionId, payloadJson, waitForCompletion, waitSeconds);
            if (asset != null) ApplyActionDefinition(asset);
        }

        private void OnClickWritePayload()
        {
            if (data == null) return;
            DialogActionPayloadUtility.BuildPayloadTemplate(actionId, payloadJson, DialogGraphDefinitionResolver.FindActionById(actionId), _payloadInstructionField?.value, out var updatedPayload, out var statusMessage);
            Undo.RecordObject(data, "Write Action Payload");
            data.payloadJson = updatedPayload ?? "{}";
            MarkDirty(data);
            _payloadField?.SetValueWithoutNotify(data.payloadJson);
            if (_payloadStatusLabel != null) _payloadStatusLabel.text = statusMessage ?? string.Empty;
        }
    }

    public static class DialogActionPayloadUtility
    {
        public static void BuildPayloadTemplate(
            string actionId,
            string currentPayload,
            DialogActionSO registeredAction,
            out string payloadJson,
            out string statusMessage)
        {
            BuildPayloadTemplate(
                actionId,
                currentPayload,
                registeredAction,
                string.Empty,
                out payloadJson,
                out statusMessage);
        }

        public static void BuildPayloadTemplate(string actionId, string currentPayload, DialogActionSO registeredAction, string payloadInstruction, out string payloadJson, out string statusMessage)
{
            if (!string.IsNullOrWhiteSpace(payloadInstruction) && TryGeneratePayloadWithAi(actionId, currentPayload, registeredAction, payloadInstruction, out payloadJson, out statusMessage)) return;
            if (TryFormatJson(currentPayload, out var formattedCurrent)) { payloadJson = formattedCurrent; statusMessage = "Formatted existing payload JSON."; return; }
            payloadJson = "{}"; statusMessage = "Inserted empty JSON object.";
        }

        private static bool TryGeneratePayloadWithAi(string actionId, string currentPayload, DialogActionSO registeredAction, string payloadInstruction, out string payloadJson, out string statusMessage)
        {
            payloadJson = string.Empty; statusMessage = string.Empty;
            var bridge = DialogGraphAiBridgeLocator.Current;
            if (bridge == null || !bridge.IsAvailable)
            {
                statusMessage = "AI extension is not installed.";
                return false;
            }

            EditorUtility.DisplayProgressBar("AI: Write Payload", "Generating payload JSON...", 0.5f);
            try
            {
                if (!bridge.TryGeneratePayloadJson(actionId, currentPayload, registeredAction, payloadInstruction, out var response, out var error))
                {
                    statusMessage = error;
                    return false;
                }

                payloadJson = response;
                statusMessage = "Generated payload JSON.";
                return true;
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        public static bool TryFormatJson(string rawJson, out string formattedJson) { formattedJson = rawJson; return !string.IsNullOrWhiteSpace(rawJson); }
    }
}
