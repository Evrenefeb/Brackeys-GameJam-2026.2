using System;
using DialogSystem.EditorTools.Resources;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// GraphView editor node for <see cref="VariableMutationNode"/>.
    /// </summary>
    public sealed class VariableMutationNodeView : BaseNodeView<VariableMutationNode>
    {
        #region ---------------- Data / Graph ----------------
        public string GUID { get; set; }
        public DialogGraphView graphView;

        public string variableName => _variableField?.value ?? string.Empty;
        public DialogueVariableValueType valueType => _typeField?.value is DialogueVariableValueType type ? type : DialogueVariableValueType.Boolean;
        public VariableMutationOperation operation => _operationField?.value is VariableMutationOperation op ? op : VariableMutationOperation.Set;
        public string value => _valueField?.value ?? string.Empty;
        #endregion

        #region ---------------- Ports & UI ----------------
        public Port inputPort { get; private set; }
        public Port outputPort { get; private set; }

        private TextField _variableField;
        private ObjectField _variableDefinitionField;
        private Button _createVariableButton;
        private Label _variableStatusLabel;
        private EnumField _typeField;
        private EnumField _operationField;
        private TextField _valueField;
        #endregion

        #region ---------------- Ctor ----------------
        public VariableMutationNodeView(string guid, DialogGraphView graph)
        {
            GUID = string.IsNullOrWhiteSpace(guid) ? Guid.NewGuid().ToString("N") : guid;
            graphView = graph;
            title = "Set Variable";

            titleButtonContainer.Clear();
            capabilities &= ~Capabilities.Collapsible;

            NodeUssLoader.ApplyTo(this);
            AddToClassList("dlg-node");
            AddToClassList("type-action");

            style.width = 340f;
            style.minHeight = 190f;

            BuildHeader();
            BuildBody();
            RebuildPorts();

            RefreshExpandedState();
            RefreshPorts();
        }
        #endregion

        #region ---------------- UI ----------------
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
            mainContainer.AddToClassList("node-section");

            _variableField = new TextField("Variable")
            {
                isDelayed = true,
                tooltip = "DialogueVariableStore key to modify at runtime."
            };
            _variableField.AddToClassList("node-field");
            _variableField.RegisterValueChangedCallback(e =>
            {
                WithData("Edit Variable Key", node => node.variableName = e.newValue ?? string.Empty);
                RefreshVariableDefinitionUi();
            });
            mainContainer.Add(_variableField);

            _variableDefinitionField = new ObjectField("Registered Variable")
            {
                objectType = typeof(DialogVariableSO),
                allowSceneObjects = false,
                tooltip = "Optional registered variable definition."
            };
            _variableDefinitionField.AddToClassList("node-field");
            _variableDefinitionField.RegisterValueChangedCallback(e =>
            {
                if (e.newValue is DialogVariableSO variable)
                {
                    ApplyVariableDefinition(variable);
                }
            });
            mainContainer.Add(_variableDefinitionField);

            _variableStatusLabel = new Label();
            _variableStatusLabel.AddToClassList("node-section-hint");
            mainContainer.Add(_variableStatusLabel);

            _createVariableButton = new Button(OnClickCreateVariable)
            {
                text = "Create Variable",
                tooltip = "Create a registered variable definition from the current variable key."
            };
            _createVariableButton.AddToClassList("node-mini-button");
            mainContainer.Add(_createVariableButton);

            _typeField = new EnumField("Type", DialogueVariableValueType.Boolean)
            {
                tooltip = "Variable value type."
            };
            _typeField.AddToClassList("node-field");
            _typeField.RegisterValueChangedCallback(e =>
            {
                WithData("Edit Variable Type", node => node.valueType = (DialogueVariableValueType)(object)e.newValue);
                RefreshValueVisibility();
            });
            mainContainer.Add(_typeField);

            _operationField = new EnumField("Operation", VariableMutationOperation.Set)
            {
                tooltip = "Operation to apply when the conversation reaches this node."
            };
            _operationField.AddToClassList("node-field");
            _operationField.RegisterValueChangedCallback(e =>
            {
                WithData("Edit Variable Operation", node => node.operation = (VariableMutationOperation)(object)e.newValue);
                RefreshValueVisibility();
            });
            mainContainer.Add(_operationField);

            _valueField = new TextField("Value")
            {
                isDelayed = true,
                tooltip = "Set value or numeric delta. Use invariant number formatting, for example 3.5."
            };
            _valueField.AddToClassList("node-field");
            _valueField.RegisterValueChangedCallback(e =>
            {
                WithData("Edit Variable Value", node => node.value = e.newValue ?? string.Empty);
            });
            mainContainer.Add(_valueField);

            RefreshVariableDefinitionUi();
            RefreshValueVisibility();
        }
        #endregion

        #region ---------------- Ports ----------------
        public override void RebuildPorts()
        {
            inputContainer.Clear();
            outputContainer.Clear();

            inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            inputPort.portName = "In";
            inputContainer.Add(inputPort);

            outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            outputPort.portName = "Out";
            outputContainer.Add(outputPort);
        }
        #endregion

        #region ---------------- Load / Apply ----------------
        public void LoadNodeData(string variable, DialogueVariableValueType type, VariableMutationOperation op, string mutationValue)
        {
            _variableField?.SetValueWithoutNotify(variable ?? string.Empty);
            _typeField?.SetValueWithoutNotify(type);
            _operationField?.SetValueWithoutNotify(op);
            _valueField?.SetValueWithoutNotify(mutationValue ?? string.Empty);
            RefreshVariableDefinitionUi();
            RefreshValueVisibility();
        }

        public void ApplyVariableDefinition(DialogVariableSO variable)
        {
            if (variable == null || data == null)
            {
                return;
            }

            Undo.RecordObject(data, "Assign Variable Definition");
            data.variableName = variable.Key;
            data.valueType = variable.ValueType;
            if (string.IsNullOrWhiteSpace(data.value) && data.operation == VariableMutationOperation.Set)
            {
                data.value = variable.GetDefaultValueAsString();
            }

            MarkDirty(data);

            _variableField?.SetValueWithoutNotify(data.variableName);
            _typeField?.SetValueWithoutNotify(data.valueType);
            _valueField?.SetValueWithoutNotify(data.value ?? string.Empty);
            RefreshVariableDefinitionUi();
            RefreshValueVisibility();
        }
        #endregion

        #region ---------------- Internals ----------------
        private void WithData(string undoLabel, Action<VariableMutationNode> apply)
        {
            if (data == null || apply == null)
            {
                return;
            }

            Undo.RecordObject(data, undoLabel);
            apply(data);
            MarkDirty(data);
        }

        private void RefreshVariableDefinitionUi()
        {
            var matchedVariable = DialogGraphDefinitionResolver.FindVariableByKey(variableName);
            _variableDefinitionField?.SetValueWithoutNotify(matchedVariable);

            if (_variableStatusLabel != null)
            {
                _variableStatusLabel.text = matchedVariable != null
                    ? $"Registered as {matchedVariable.Key} ({matchedVariable.ValueType})"
                    : string.IsNullOrWhiteSpace(variableName)
                        ? "Manual variable entry."
                        : "Variable key is not registered.";
            }

            if (_createVariableButton != null)
            {
                _createVariableButton.style.display =
                    matchedVariable == null && !string.IsNullOrWhiteSpace(variableName)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }
        }

        private void RefreshValueVisibility()
        {
            if (_valueField == null)
            {
                return;
            }

            var op = operation;
            _valueField.style.display = op == VariableMutationOperation.Toggle || op == VariableMutationOperation.ClearString
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        private void OnClickCreateVariable()
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return;
            }

            var asset = graphView?.GraphOwner?.CreateVariableAsset(variableName, valueType, value);
            if (asset != null)
            {
                ApplyVariableDefinition(asset);
            }
        }
        #endregion
    }
}
