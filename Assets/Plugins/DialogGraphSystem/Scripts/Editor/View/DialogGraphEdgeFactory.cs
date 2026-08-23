using DialogSystem.EditorTools.View.Elements;
using DialogSystem.Runtime.Models;
using UnityEditor.Experimental.GraphView;

namespace DialogSystem.EditorTools.View
{
    /// <summary>
    /// Factory for creating <see cref="DialogGraphEdge"/> instances in the editor graph view.
    /// Centralises edge construction so all creation paths — load, user-drag, duplicate,
    /// and AI mutation — use the custom edge type uniformly.
    /// <para>
    /// This factory never touches <c>DialogGraph.edgeLayouts</c> or reroute metadata.
    /// It only wires ports and optionally stamps link identity.
    /// </para>
    /// </summary>
    public static class DialogGraphEdgeFactory
    {
        /// <summary>
        /// Connects <paramref name="outputPort"/> to <paramref name="inputPort"/> and returns
        /// the new <see cref="DialogGraphEdge"/>. Identity fields are left at their defaults;
        /// use <see cref="ConnectFromLink"/> or <see cref="ApplyLinkIdentity"/> when the
        /// backing <see cref="GraphLink"/> is available.
        /// Returns <c>null</c> if either port is <c>null</c>.
        /// </summary>
        public static DialogGraphEdge Connect(Port outputPort, Port inputPort)
        {
            if (outputPort == null || inputPort == null)
            {
                return null;
            }

            return outputPort.ConnectTo<DialogGraphEdge>(inputPort);
        }

        /// <summary>
        /// Connects <paramref name="outputPort"/> to <paramref name="inputPort"/>, stamps the
        /// identity fields from <paramref name="link"/>, and returns the new
        /// <see cref="DialogGraphEdge"/>.
        /// Returns <c>null</c> if either port is <c>null</c>.
        /// </summary>
        public static DialogGraphEdge ConnectFromLink(Port outputPort, Port inputPort, GraphLink link)
        {
            var edge = Connect(outputPort, inputPort);
            if (edge == null)
            {
                return null;
            }

            if (link != null)
            {
                ApplyLinkIdentity(edge, link);
            }

            return edge;
        }

        /// <summary>
        /// Stamps the serialized link identity fields onto an existing <see cref="DialogGraphEdge"/>
        /// from a <see cref="GraphLink"/>.
        /// Does nothing if either argument is <c>null</c>.
        /// Does not create or modify <c>DialogGraph.edgeLayouts</c>.
        /// </summary>
        public static void ApplyLinkIdentity(DialogGraphEdge edge, GraphLink link)
        {
            if (edge == null || link == null)
            {
                return;
            }

            edge.linkGuid      = link.LinkGuid      ?? string.Empty;
            edge.fromGuid      = link.fromGuid      ?? string.Empty;
            edge.toGuid        = link.toGuid        ?? string.Empty;
            edge.fromPortKey   = link.fromPortKey   ?? string.Empty;
            edge.toPortKey     = link.toPortKey     ?? string.Empty;
            edge.fromPortIndex = link.fromPortIndex;
        }
    }
}
