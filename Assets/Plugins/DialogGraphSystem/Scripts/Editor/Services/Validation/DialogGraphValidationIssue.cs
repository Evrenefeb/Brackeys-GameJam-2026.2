namespace DialogSystem.EditorTools.Services.Validation
{
    /// <summary>
    /// A single structured problem found during graph validation.
    /// </summary>
    public sealed class DialogGraphValidationIssue
    {
        /// <summary>Severity of the issue.</summary>
        public DialogGraphValidationSeverity Severity { get; }

        /// <summary>
        /// Short machine-readable code (e.g. "DUPLICATE_GUID", "ORPHAN_NODE").
        /// Stable across runs — suitable for filtering in future tooling.
        /// </summary>
        public string Code { get; }

        /// <summary>Human-readable description of the problem.</summary>
        public string Message { get; }

        /// <summary>
        /// GUID of the node that caused this issue, or <c>null</c> when the
        /// problem is graph-level (e.g. duplicate GUIDs, missing Start/End).
        /// </summary>
        public string NodeGuid { get; }

        /// <summary>GUID of the link that caused this issue, when available.</summary>
        public string LinkGuid { get; }

        /// <summary>Stable choice ID that caused this issue, when available.</summary>
        public string ChoiceId { get; }

        /// <summary>Stable graph ID that caused this issue, when available.</summary>
        public string GraphGuid { get; }

        public DialogGraphValidationIssue(
            DialogGraphValidationSeverity severity,
            string code,
            string message,
            string nodeGuid = null,
            string linkGuid = null,
            string choiceId = null,
            string graphGuid = null)
        {
            Severity  = severity;
            Code      = code      ?? string.Empty;
            Message   = message   ?? string.Empty;
            NodeGuid  = nodeGuid;
            LinkGuid  = linkGuid;
            ChoiceId  = choiceId;
            GraphGuid = graphGuid;
        }

        public override string ToString()
        {
            var loc = string.IsNullOrEmpty(NodeGuid) ? "Graph" : $"Node {NodeGuid}";
            if (!string.IsNullOrEmpty(LinkGuid)) loc += $", Link {LinkGuid}";
            if (!string.IsNullOrEmpty(ChoiceId)) loc += $", Choice {ChoiceId}";
            if (!string.IsNullOrEmpty(GraphGuid)) loc += $", GraphId {GraphGuid}";
            return $"[{Severity}] {Code} ({loc}): {Message}";
        }
    }
}
