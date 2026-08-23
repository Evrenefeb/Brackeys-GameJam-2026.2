using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.Resources;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// Non-deletable start node with a single output port.
    /// All visual styling (green accent, border, bg) is in NodeTypes.uss (.type-start).
    /// </summary>
    public class StartNodeView : Node
    {
        public string GUID;
        public Port outputPort;

        public StartNodeView(string guid)
        {
            NodeUssLoader.ApplyTo(this);

            AddToClassList("dlg-node");
            AddToClassList("type-start");

            GUID  = guid;
            title = "Start";

            capabilities &= ~Capabilities.Deletable;
            capabilities &= ~Capabilities.Renamable;
            capabilities &= ~Capabilities.Resizable;
            capabilities &= ~Capabilities.Collapsible;
            titleButtonContainer.Clear();

            style.width  = 150;
            style.height = 50;

            var icon = DialogGraphIconManager.CreateImage(DialogGraphIconId.NodeStart, "dgs-icon--sm", "dgs-icon--start");
            icon.style.marginRight = 5f;
            icon.style.marginTop = 10f;
            titleContainer.Insert(0, icon);

            outputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Single,
                typeof(float));
            outputPort.portName = "Out";
            titleContainer.Add(outputPort);

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}
