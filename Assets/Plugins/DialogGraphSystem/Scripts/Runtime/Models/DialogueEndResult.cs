namespace DialogSystem.Runtime.Models
{
    /// <summary>
    /// Result produced when a conversation reaches an OutcomeNode.
    /// </summary>
    [System.Serializable]
    public struct DialogueEndResult
    {
        /// <summary>The runtime dialog ID of the conversation that ended.</summary>
        public string ConversationId;

        /// <summary>The stable outcomeId from the OutcomeNode.</summary>
        public string OutcomeId;

        /// <summary>The human-readable display name from the OutcomeNode.</summary>
        public string OutcomeDisplayName;

        /// <summary>Optional description from the OutcomeNode.</summary>
        public string OutcomeDescription;

        public bool HasOutcome => !string.IsNullOrWhiteSpace(OutcomeId);

        public override string ToString()
        {
            return HasOutcome
                ? $"Outcome: {OutcomeDisplayName} ({OutcomeId})"
                : "No outcome";
        }
    }
}
