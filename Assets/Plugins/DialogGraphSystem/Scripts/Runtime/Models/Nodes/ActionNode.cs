using UnityEngine;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Action node that triggers a runner action by <see cref="actionId"/>.
    /// Can optionally wait for completion and/or apply a pre-delay.
    /// </summary>
    public class ActionNode : BaseNode
    {
        #region -------- Action --------
        [Header("Action")]
        [Tooltip("Your handler key, e.g. \"PlaySFX\", \"SetVar\", \"CallEvent\".")]
        public string actionId;

        [TextArea(1, 10)]
        [Tooltip("Arbitrary JSON payload to parse at runtime.")]
        public string payloadJson;

        [Tooltip("If true, runtime should wait for the action to complete.")]
        public bool waitForCompletion;

        [Tooltip("Optional delay (seconds) before continuing. Use 0 for none.")]
        [Min(0f)] public float waitSeconds = 0f;
        #endregion

        public ActionNode()
        {
            nodeKind = NodeKind.Action;
        }
    }
}
