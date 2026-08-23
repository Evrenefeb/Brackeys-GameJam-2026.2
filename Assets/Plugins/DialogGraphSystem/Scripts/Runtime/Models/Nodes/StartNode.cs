using UnityEngine;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Graph start marker. Points to the first playable node in the conversation.
    /// </summary>
    public class StartNode : BaseNode
    {
        /// <summary>
        /// Optional serialized target for integrations that model Start as a ScriptableObject.
        /// The built-in graph runtime uses <see cref="DialogSystem.Runtime.Models.GraphLink"/> records instead.
        /// </summary>
        [Tooltip("First node to jump to (GUID).")]
        public string nextNodeGUID;

        public StartNode()
        {
            nodeKind = NodeKind.Start;
        }
    }
}
