using UnityEngine;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Terminal node type indicating conversation end.
    /// </summary>
    public class EndNode : BaseNode
    {
        public EndNode()
        {
            nodeKind = NodeKind.End;
        }
    }
}
