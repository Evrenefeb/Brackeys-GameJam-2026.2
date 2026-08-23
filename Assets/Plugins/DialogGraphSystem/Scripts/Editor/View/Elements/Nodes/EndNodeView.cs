using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.Resources;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.View.Elements.Nodes
{
    /// <summary>
    /// Non-deletable end node with a single input port.
    /// All visual styling (red accent, border, bg) is in NodeTypes.uss (.type-end).
    /// </summary>
    public class EndNodeView : Node
    {
        public string GUID;
        public Port inputPort;

        public EndNodeView(string guid)
        {
            NodeUssLoader.ApplyTo(this);

            AddToClassList("dlg-node");
            AddToClassList("type-end");

            GUID  = guid;
            title = "End";

            capabilities &= ~Capabilities.Deletable;
            capabilities &= ~Capabilities.Renamable;
            capabilities &= ~Capabilities.Resizable;
            capabilities &= ~Capabilities.Collapsible;
            titleButtonContainer.Clear();

            style.width  = 150;
            style.height = 50;

            inputPort = Port.Create<Edge>(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Multi,
                typeof(float));
            inputPort.portName = "In";
            titleContainer.Insert(0, inputPort);

            var icon = DialogGraphIconManager.CreateImage(DialogGraphIconId.NodeEnd, "dgs-icon--sm", "dgs-icon--end");
            icon.style.marginRight = 5f;
            icon.style.marginTop = 10f;
            titleContainer.Insert(1, icon);

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}
