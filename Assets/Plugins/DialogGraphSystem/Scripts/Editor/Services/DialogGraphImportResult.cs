using System;
using System.Collections.Generic;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Structured result returned by <see cref="DialogGraphImportTransactionService"/>.
    /// Carries success/failure status, stage information, backup path, and original snapshot.
    /// </summary>
    public readonly struct DialogGraphImportResult
    {
        /// <summary>
        /// True when the import transaction completed without error.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Human-readable stage label describing where a failure occurred.
        /// Examples: "Parse", "Validate", "Backup", "Build", "Migration", "Commit".
        /// </summary>
        public string FailureStage { get; }

        /// <summary>
        /// Primary user-facing message for success or failure.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Project-relative path of the target graph asset (e.g. "Assets/.../MyGraph.asset").
        /// </summary>
        public string TargetPath { get; }

        /// <summary>
        /// Project-relative path of the backup asset created before overwrite, or null if no backup was made.
        /// </summary>
        public string BackupPath { get; }

        /// <summary>
        /// Project-relative path of the temporary build asset used during the transaction, or null.
        /// </summary>
        public string TempPath { get; }

        /// <summary>
        /// Detailed error/warning strings collected during the transaction.
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Snapshot of the original graph state captured before any mutation, or null for new-graph imports.
        /// </summary>
        public DialogGraphImportSnapshot? OriginalSnapshot { get; }

        private DialogGraphImportResult(
            bool success,
            string failureStage,
            string message,
            string targetPath,
            string backupPath,
            string tempPath,
            IReadOnlyList<string> errors,
            DialogGraphImportSnapshot? originalSnapshot)
        {
            Success = success;
            FailureStage = failureStage;
            Message = message;
            TargetPath = targetPath;
            BackupPath = backupPath;
            TempPath = tempPath;
            Errors = errors ?? Array.Empty<string>();
            OriginalSnapshot = originalSnapshot;
        }

        /// <summary>
        /// Create a successful result.
        /// </summary>
        public static DialogGraphImportResult Succeeded(
            string targetPath,
            string backupPath = null,
            DialogGraphImportSnapshot? originalSnapshot = null)
        {
            return new DialogGraphImportResult(
                success: true,
                failureStage: null,
                message: $"Import completed successfully to {targetPath}.",
                targetPath: targetPath,
                backupPath: backupPath,
                tempPath: null,
                errors: null,
                originalSnapshot: originalSnapshot);
        }

        /// <summary>
        /// Create a failure result for a specific stage.
        /// </summary>
        public static DialogGraphImportResult Failed(
            string failureStage,
            string message,
            string targetPath = null,
            string backupPath = null,
            string tempPath = null,
            IReadOnlyList<string> errors = null,
            DialogGraphImportSnapshot? originalSnapshot = null)
        {
            return new DialogGraphImportResult(
                success: false,
                failureStage: failureStage,
                message: message ?? "Import failed.",
                targetPath: targetPath,
                backupPath: backupPath,
                tempPath: tempPath,
                errors: errors,
                originalSnapshot: originalSnapshot);
        }
    }
}
