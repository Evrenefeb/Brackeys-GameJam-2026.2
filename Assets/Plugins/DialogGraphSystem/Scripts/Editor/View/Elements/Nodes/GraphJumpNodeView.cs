using System;
using DialogSystem.EditorTools.Resources;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// GraphView editor node for <see cref="GraphJumpNode"/>.
    /// </summary>
    public sealed class GraphJumpNodeView : BaseNodeView<GraphJumpNode>
    {
        public string GUID { get; set; }
        public DialogGraphView graphView;

        public Port inputPort { get; private set; }
        public Port outputPort { get; private set; }

        private ObjectField _targetGraphField;
        private TextField _runtimeDialogIdField;
        private TextField _graphGuidField;
        private TextField _graphNameField;
        private TextField _assetPathField;
        private TextField _entryGuidField;
        private Foldout _optionalReferenceFoldout;

        public GraphJumpNodeView(string guid, DialogGraphView graph)
        {
            GUID = string.IsNullOrWhiteSpace(guid) ? Guid.NewGuid().ToString("N") : guid;
            graphView = graph;
            title = "Graph Jump";

            titleButtonContainer.Clear();
            capabilities &= ~Capabilities.Collapsible;

            NodeUssLoader.ApplyTo(this);
            AddToClassList("dlg-node");
            AddToClassList("type-action");

            style.width = 340f;
            style.minHeight = 160f;

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
            mainContainer.AddToClassList("node-section");

            _targetGraphField = new ObjectField("Target Graph")
            {
                objectType = typeof(DialogGraph),
                allowSceneObjects = false,
                tooltip = "Optional direct asset reference to the target graph."
            };
            _targetGraphField.AddToClassList("node-field");
            _targetGraphField.RegisterValueChangedCallback(e =>
            {
                WithData("Assign Target Graph", node =>
                {
                    node.EnsureReference();
                    var graph = e.newValue as DialogGraph;
                    node.targetGraph.graphAsset = graph;
                    if (graph != null)
                    {
                        node.targetGraph.graphGuid = graph.GraphGuid;
                        node.targetGraph.graphName = graph.name;
                        node.targetGraph.assetPath = AssetDatabase.GetAssetPath(graph);

                        if (string.IsNullOrWhiteSpace(node.targetGraph.entryGuid))
                        {
                            node.targetGraph.entryGuid = graph.startGuid;
                        }
                    }
                });

                RefreshReferenceFields();
            });
            mainContainer.Add(_targetGraphField);

            _optionalReferenceFoldout = new Foldout
            {
                text = "Optional Reference Data",
                value = false
            };
            _optionalReferenceFoldout.AddToClassList("node-section");
            mainContainer.Add(_optionalReferenceFoldout);

            _graphGuidField = CreateTextField("Graph GUID", "Stable graph GUID for the target graph.");
            _graphGuidField.RegisterValueChangedCallback(e =>
            {
                WithData("Edit Target Graph GUID", node =>
                {
                    node.EnsureReference();
                    node.targetGraph.graphGuid = e.newValue ?? string.Empty;
                });
            });
            _optionalReferenceFoldout.Add(_graphGuidField);

            _runtimeDialogIdField = CreateTextField("Runtime Dialog ID", "Optional DialogManager registry ID used to resolve the target graph at runtime.");
            _runtimeDialogIdField.RegisterValueChangedCallback(e =>
            {
                WithData("Edit Target Runtime Dialog ID", node =>
                {
                    node.EnsureReference();
                    node.targetGraph.runtimeDialogId = e.newValue ?? string.Empty;
                });
            });
            _optionalReferenceFoldout.Add(_runtimeDialogIdField);

            _graphNameField = CreateTextField("Graph Name", "Cached graph asset name for import/export and editor tooling.");
            _graphNameField.RegisterValueChangedCallback(e =>
            {
                WithData("Edit Target Graph Name", node =>
                {
                    node.EnsureReference();
                    node.targetGraph.graphName = e.newValue ?? string.Empty;
                });
            });
            _optionalReferenceFoldout.Add(_graphNameField);

            _assetPathField = CreateTextField("Asset Path", "Optional cached asset path for the referenced graph.");
            _assetPathField.RegisterValueChangedCallback(e =>
            {
                WithData("Edit Target Graph Path", node =>
                {
                    node.EnsureReference();
                    node.targetGraph.assetPath = e.newValue ?? string.Empty;
                });
            });
            _optionalReferenceFoldout.Add(_assetPathField);

            _entryGuidField = CreateTextField("Entry GUID", "Optional entry GUID inside the referenced graph. Leave blank to use that graph's Start boundary.");
            _entryGuidField.RegisterValueChangedCallback(e =>
            {
                WithData("Edit Target Entry GUID", node =>
                {
                    node.EnsureReference();
                    node.targetGraph.entryGuid = e.newValue ?? string.Empty;
                });
            });
            _optionalReferenceFoldout.Add(_entryGuidField);
        }

        private static TextField CreateTextField(string label, string tooltip)
        {
            var field = new TextField(label)
            {
                isDelayed = true,
                tooltip = tooltip
            };
            field.AddToClassList("node-field");
            return field;
        }

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

        public void LoadNodeData(GraphReference graphReference)
        {
            _targetGraphField?.SetValueWithoutNotify(graphReference?.graphAsset);
            _runtimeDialogIdField?.SetValueWithoutNotify(graphReference?.runtimeDialogId ?? string.Empty);
            _graphGuidField?.SetValueWithoutNotify(graphReference?.graphGuid ?? string.Empty);
            _graphNameField?.SetValueWithoutNotify(graphReference?.graphName ?? string.Empty);
            _assetPathField?.SetValueWithoutNotify(graphReference?.assetPath ?? string.Empty);
            _entryGuidField?.SetValueWithoutNotify(graphReference?.entryGuid ?? string.Empty);
        }

        private void RefreshReferenceFields()
        {
            if (data?.targetGraph == null)
            {
                return;
            }

            _runtimeDialogIdField?.SetValueWithoutNotify(data.targetGraph.runtimeDialogId ?? string.Empty);
            _graphGuidField?.SetValueWithoutNotify(data.targetGraph.graphGuid ?? string.Empty);
            _graphNameField?.SetValueWithoutNotify(data.targetGraph.graphName ?? string.Empty);
            _assetPathField?.SetValueWithoutNotify(data.targetGraph.assetPath ?? string.Empty);
            _entryGuidField?.SetValueWithoutNotify(data.targetGraph.entryGuid ?? string.Empty);
        }

        private void WithData(string undoLabel, Action<GraphJumpNode> apply)
        {
            if (data == null || apply == null)
            {
                return;
            }

            Undo.RecordObject(data, undoLabel);
            apply(data);
            MarkDirty(data);
        }
    }
}
