using DialogSystem.EditorTools.Resources;
using DialogSystem.Runtime.Models.Nodes;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.View;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// Editor view for OutcomeNode.
    /// Multi-input, single-output. Fields for outcomeId, displayName, and description.
    /// Non-collapsible hidden flow node styled as type-outcome.
    /// </summary>
    public class OutcomeNodeView : BaseNodeView<OutcomeNode>
    {
        public Port inputPort { get; private set; }
        public Port outputPort { get; private set; }

        private TextField _outcomeIdField;
        private TextField _displayNameField;
        private TextField _descriptionField;

        public string GUID { get; set; }
        public string outcomeId => _outcomeIdField?.value ?? string.Empty;
        public string displayName => _displayNameField?.value ?? string.Empty;
        public string description => _descriptionField?.value ?? string.Empty;

        public OutcomeNodeView(string guid)
        {
            NodeUssLoader.ApplyTo(this);
            GUID = string.IsNullOrWhiteSpace(guid) ? System.Guid.NewGuid().ToString("N") : guid;
            title = "Outcome";

            AddToClassList("dlg-node");
            AddToClassList("type-outcome");

            style.width = 320f;
            style.minWidth = 320f;
            style.maxWidth = 320f;
            style.minHeight = 220f;

            capabilities &= ~Capabilities.Collapsible;

            BuildHeader();
            BuildBody();
            RebuildPorts();
            RefreshExpandedState();
            RefreshPorts();
        }

        private void BuildHeader()
        {
            titleContainer?.AddToClassList("node-title-outcome");

            var icon = DialogGraphIconManager.CreateImage(
                DialogGraphIconId.NodeOutcome,
                "dgs-icon--sm",
                "dgs-icon--outcome");
            icon.style.marginRight = 5f;
            icon.style.marginTop = 10f;
            titleContainer?.Insert(0, icon);
        }

        private void BuildBody()
        {
            titleButtonContainer.Clear();

            var content = mainContainer;
            var section = new VisualElement();
            section.AddToClassList("node-section");
            content.Add(section);

            var spacer = new VisualElement();
            spacer.AddToClassList("node-body-spacer");
            section.Add(spacer);

            // Outcome ID
            _outcomeIdField = new TextField("Outcome ID")
            {
                isDelayed = true
            };
            _outcomeIdField.AddToClassList("node-field");
            _outcomeIdField.RegisterValueChangedCallback(evt =>
            {
                if (data == null) return;
                Undo.RecordObject(data, "Edit Outcome ID");
                data.outcomeId = evt.newValue ?? string.Empty;
                MarkDirty(data);
            });
            section.Add(_outcomeIdField);

            // Display Name
            _displayNameField = new TextField("Display Name")
            {
                isDelayed = true
            };
            _displayNameField.AddToClassList("node-field");
            _displayNameField.RegisterValueChangedCallback(evt =>
            {
                if (data == null) return;
                Undo.RecordObject(data, "Edit Outcome Display Name");
                data.displayName = evt.newValue ?? string.Empty;
                MarkDirty(data);
            });
            section.Add(_displayNameField);

            // Description
            _descriptionField = new TextField("Description")
            {
                isDelayed = true,
                multiline = true
            };
            _descriptionField.AddToClassList("node-field");
            _descriptionField.style.minHeight = 96f;
            _descriptionField.style.maxHeight = 96f;
            _descriptionField.style.whiteSpace = WhiteSpace.Normal;
            _descriptionField.style.flexGrow = 1f;
            _descriptionField.RegisterValueChangedCallback(evt =>
            {
                if (data == null) return;
                Undo.RecordObject(data, "Edit Outcome Description");
                data.description = evt.newValue ?? string.Empty;
                MarkDirty(data);
            });
            section.Add(_descriptionField);
        }

        /// <summary>
        /// Loads saved fields from the backing ScriptableObject into the UI.
        /// </summary>
        public void LoadNodeData(string outcomeIdValue, string displayNameValue, string descriptionValue)
        {
            _outcomeIdField?.SetValueWithoutNotify(outcomeIdValue ?? string.Empty);
            _displayNameField?.SetValueWithoutNotify(displayNameValue ?? string.Empty);
            _descriptionField?.SetValueWithoutNotify(descriptionValue ?? string.Empty);
        }

        #region Ports

        public override void RebuildPorts()
        {
            inputContainer.Clear();
            outputContainer.Clear();

            inputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(float));
            inputPort.portName = "In";
            inputContainer.Add(inputPort);

            outputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Single,
                typeof(float));
            outputPort.portName = "Out";
            outputContainer.Add(outputPort);
        }

        #endregion
    }
}
