using System.Collections.Generic;
using System.Linq;

namespace DialogSystem.EditorTools.Services.Validation
{
    /// <summary>
    /// Aggregated result returned by <see cref="DialogGraphValidator.Validate"/>.
    /// Immutable once constructed; safe to cache and display across frames.
    /// </summary>
    public sealed class DialogGraphValidationResult
    {
        /// <summary>All issues found, ordered by severity (Error first).</summary>
        public IReadOnlyList<DialogGraphValidationIssue> Issues { get; }

        /// <summary>True when there are no Error-severity issues.</summary>
        public bool IsValid { get; }

        /// <summary>Convenience: all Error-level issues.</summary>
        public IEnumerable<DialogGraphValidationIssue> Errors =>
            Issues.Where(i => i.Severity == DialogGraphValidationSeverity.Error);

        /// <summary>Convenience: all Warning-level issues.</summary>
        public IEnumerable<DialogGraphValidationIssue> Warnings =>
            Issues.Where(i => i.Severity == DialogGraphValidationSeverity.Warning);

        /// <summary>Convenience: all Info-level issues.</summary>
        public IEnumerable<DialogGraphValidationIssue> Infos =>
            Issues.Where(i => i.Severity == DialogGraphValidationSeverity.Info);

        public int ErrorCount   => Issues.Count(i => i.Severity == DialogGraphValidationSeverity.Error);
        public int WarningCount => Issues.Count(i => i.Severity == DialogGraphValidationSeverity.Warning);

        /// <summary>Convenience count for Info-level issues.</summary>
        public int InfoCount    => Issues.Count(i => i.Severity == DialogGraphValidationSeverity.Info);

        public DialogGraphValidationResult(IEnumerable<DialogGraphValidationIssue> issues)
        {
            var sorted = (issues ?? Enumerable.Empty<DialogGraphValidationIssue>())
                .OrderByDescending(i => (int)i.Severity)
                .ToList();

            Issues  = sorted;
            IsValid = sorted.All(i => i.Severity != DialogGraphValidationSeverity.Error);
        }

        /// <summary>Convenience factory for an empty (clean) result.</summary>
        public static DialogGraphValidationResult Clean() =>
            new DialogGraphValidationResult(Enumerable.Empty<DialogGraphValidationIssue>());
    }
}
