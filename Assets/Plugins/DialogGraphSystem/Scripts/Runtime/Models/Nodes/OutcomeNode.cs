using UnityEngine;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Hidden runtime flow node that records a named ending result
    /// before routing through its single output to the existing End boundary.
    /// Not a UI-facing dialogue/choice screen.
    /// </summary>
    public class OutcomeNode : BaseNode
    {
        #region -------- Outcome --------
        [Header("Outcome")]
        [Tooltip("Unique identifier for this outcome within the graph (e.g. \"good_ending\", \"bad_ending\").")]
        public string outcomeId;

        [Tooltip("Human-readable name shown in results and editor (e.g. \"Good Ending\", \"Bad Ending\").")]
        public string displayName;

        [TextArea(1, 5)]
        [Tooltip("Optional description of what this outcome means in the narrative.")]
        public string description;
        #endregion

        public OutcomeNode()
        {
            nodeKind = NodeKind.Outcome;
        }

        /// <summary>
        /// Returns true when this outcome has a non-empty unique identifier.
        /// </summary>
        public bool HasOutcomeId => !string.IsNullOrWhiteSpace(outcomeId);

        /// <summary>
        /// Returns the display name, falling back to the outcomeId when displayName is blank.
        /// </summary>
        public string ResolvedDisplayName =>
            !string.IsNullOrWhiteSpace(displayName) ? displayName : outcomeId ?? string.Empty;
    }
}
