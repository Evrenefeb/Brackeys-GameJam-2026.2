using System.Collections;
using UnityEngine;

namespace DialogSystem.Runtime.Interfaces
{
    /// <summary>
    /// Coroutine-based action handler used by <c>DialogActionRunner</c> for waitable actions.
    /// Add MonoBehaviours implementing this interface to the appropriate
    /// dialogue action set's <c>handlers</c> list in the inspector.
    /// </summary>
    public interface IActionHandler
    {
        #region -------- Contract --------
        /// <summary>
        /// Returns true if this handler can process the given <paramref name="actionId"/>.
        /// </summary>
        /// <param name="actionId">Logical action identifier (must match ActionNode.actionId or runner call).</param>
        bool CanHandle(string actionId);

        /// <summary>
        /// Performs the action and yields until it completes.
        /// Return a coroutine (may be nested) that finishes when the action is done.
        /// </summary>
        /// <param name="actionId">Logical action identifier.</param>
        /// <param name="payloadJson">Arbitrary JSON payload passed from the node/runner (may be empty).</param>
        IEnumerator Handle(string actionId, string payloadJson);
        #endregion
    }
}