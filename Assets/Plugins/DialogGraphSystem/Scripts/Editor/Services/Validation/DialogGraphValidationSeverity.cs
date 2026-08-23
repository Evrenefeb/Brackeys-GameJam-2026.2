namespace DialogSystem.EditorTools.Services.Validation
{
    /// <summary>
    /// Severity level of a single graph validation issue.
    /// </summary>
    public enum DialogGraphValidationSeverity
    {
        /// <summary>Informational note; graph is still playable.</summary>
        Info = 0,

        /// <summary>Non-blocking problem that may cause unexpected behaviour.</summary>
        Warning = 1,

        /// <summary>Structural error that will prevent the graph from running correctly.</summary>
        Error = 2,
    }
}
