using DialogSystem.Runtime.Models;
using UnityEngine;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Hidden flow node that transfers traversal into another dialog graph.
    /// </summary>
    public class GraphJumpNode : BaseNode
    {
        [Header("Target Graph")]
        [Tooltip("Serialized target graph reference used by future cross-graph traversal features.")]
        public GraphReference targetGraph = new GraphReference();

        public GraphJumpNode()
        {
            nodeKind = NodeKind.GraphJump;
        }

        public bool EnsureReference()
        {
            if (targetGraph != null)
            {
                return false;
            }

            targetGraph = new GraphReference();
            return true;
        }
    }
}
