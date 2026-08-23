using System;
using System.Linq;
using DialogSystem.EditorTools.Resources;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Models;
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
    /// GraphView editor node for <see cref="ConditionNode"/>.
    /// Exposes one input and two outputs: True and False.
    /// </summary>
    public class ConditionNodeView : BaseNodeView<ConditionNode>
    {
        #region ---------------- Data / Graph ----------------
        public string GUID { get; set; }
        public DialogGraphView graphView;

        public string variableName => _variableField?.value ?? string.Empty;
        public DialogueVariableValueType valueType => _typeField?.value is DialogueVariableValueType type ? type : DialogueVariableValueType.Boolean;
        public ConditionOperator conditionOperator => _operatorField?.value is ConditionOperator op ? op : ConditionOperator.IsTrue;
        public string comparisonValue => _comparisonField?.value ?? string.Empty;
        public bool missingVariableResult => _missingVariableToggle != null && _missingVariableToggle.value;
        #endregion

        #region ---------------- Ports & UI ----------------
        public Port inputPort { get; private set; }
        public Port trueOutputPort { get; private set; }
        public Port falseOutputPort { get; private set; }

        private TextField _variableField;
        private EnumField _typeField;
        private EnumField _operatorField;
        private TextField _comparisonField;
        private Toggle _missingVariableToggle;
        #endregion

        #region ---------------- Ctor ----------------
        public ConditionNodeView(string guid, DialogGraphView graph)
        {
            GUID = string.IsNullOrWhiteSpace(guid) ? Guid.NewGuid().ToString("N") : guid;
            graphView = graph;
            title = "Condition";

            titleButtonContainer.Clear();
            capabilities &= ~Capabilities.Collapsible;

            NodeUssLoader.ApplyTo(this);
            AddToClassList("dlg-node");
            AddToClassList("type-condition");

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
            titleContainer?.AddToClassList("node-title-condition");
            var icon = DialogGraphIconManager.CreateImage(DialogGraphIconId.NodeCondition, "dgs-icon--sm", "dgs-icon--condition");
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
                tooltip = "DialogueVariableStore key evaluated at runtime."
            };
            _variableField.AddToClassList("node-field");
            _variableField.RegisterValueChangedCallback(e =>
            {
                WithAssetNode("Edit Condition Variable", (_, node) => node.variableName = e.newValue ?? string.Empty);
            });
            mainContainer.Add(_variableField);

            _typeField = new EnumField("Type", DialogueVariableValueType.Boolean)
            {
                tooltip = "Expected variable type."
            };
            _typeField.AddToClassList("node-field");
            _typeField.RegisterValueChangedCallback(e =>
            {
                WithAssetNode("Edit Condition Type", (_, node) => node.valueType = (DialogueVariableValueType)(object)e.newValue);
                RefreshComparisonVisibility();
            });
            mainContainer.Add(_typeField);

            _operatorField = new EnumField("Operator", ConditionOperator.IsTrue)
            {
                tooltip = "Condition operator."
            };
            _operatorField.AddToClassList("node-field");
            _operatorField.RegisterValueChangedCallback(e =>
            {
                WithAssetNode("Edit Condition Operator", (_, node) => node.conditionOperator = (ConditionOperator)(object)e.newValue);
                RefreshComparisonVisibility();
            });
            mainContainer.Add(_operatorField);

            _comparisonField = new TextField("Value")
            {
                isDelayed = true,
                tooltip = "Comparison value. Use invariant number formatting, for example 3.5."
            };
            _comparisonField.AddToClassList("node-field");
            _comparisonField.RegisterValueChangedCallback(e =>
            {
                WithAssetNode("Edit Condition Value", (_, node) => node.comparisonValue = e.newValue ?? string.Empty);
            });
            mainContainer.Add(_comparisonField);

            _missingVariableToggle = new Toggle("Missing Variable = True")
            {
                tooltip = "Fallback result when the variable is missing or cannot be parsed."
            };
            _missingVariableToggle.AddToClassList("inline-toggle");
            _missingVariableToggle.RegisterValueChangedCallback(e =>
            {
                WithAssetNode("Edit Condition Fallback", (_, node) => node.missingVariableResult = e.newValue);
            });
            mainContainer.Add(_missingVariableToggle);

            RefreshComparisonVisibility();
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

            trueOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            trueOutputPort.portName = "True";
            outputContainer.Add(trueOutputPort);

            falseOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            falseOutputPort.portName = "False";
            outputContainer.Add(falseOutputPort);
        }

        public int GetPortIndex(Port port)
        {
            if (port == trueOutputPort)
            {
                return ConditionNode.TruePortIndex;
            }

            if (port == falseOutputPort)
            {
                return ConditionNode.FalsePortIndex;
            }

            return -1;
        }

        public Port GetOutputPort(int portIndex)
        {
            return portIndex == ConditionNode.FalsePortIndex ? falseOutputPort : trueOutputPort;
        }
        #endregion

        #region ---------------- Load ----------------
        /// <summary>
        /// Populates the UI from existing condition data without recording Undo.
        /// </summary>
        public void LoadNodeData(
            string variable,
            DialogueVariableValueType type,
            ConditionOperator op,
            string value,
            bool fallbackResult)
        {
            _variableField?.SetValueWithoutNotify(variable ?? string.Empty);
            _typeField?.SetValueWithoutNotify(type);
            _operatorField?.SetValueWithoutNotify(op);
            _comparisonField?.SetValueWithoutNotify(value ?? string.Empty);
            _missingVariableToggle?.SetValueWithoutNotify(fallbackResult);
            RefreshComparisonVisibility();
        }
        #endregion

        #region ---------------- Asset Helpers ----------------
        private DialogGraph GetAssetSafe()
        {
            if (graphView == null || string.IsNullOrEmpty(graphView.graphId))
            {
                return null;
            }

            return DialogGraphAssetPaths.LoadGraphAsset(graphView.graphId);
        }

        private ConditionNode FindSoNode(DialogGraph asset)
        {
            if (asset == null || string.IsNullOrEmpty(GUID))
            {
                return null;
            }

            return asset.conditionNodes?.FirstOrDefault(node => node != null && node.GetGuid() == GUID);
        }

        private void WithAssetNode(string undoLabel, Action<DialogGraph, ConditionNode> apply)
        {
            var asset = GetAssetSafe();
            var node = FindSoNode(asset);
            if (node == null || apply == null)
            {
                return;
            }

            Undo.RecordObject(node, undoLabel);
            apply(asset, node);
            EditorUtility.SetDirty(node);
            EditorUtility.SetDirty(asset);
        }

        private void RefreshComparisonVisibility()
        {
            if (_comparisonField == null)
            {
                return;
            }

            var op = conditionOperator;
            _comparisonField.style.display = op == ConditionOperator.IsTrue || op == ConditionOperator.IsFalse
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
        #endregion
    }
}
