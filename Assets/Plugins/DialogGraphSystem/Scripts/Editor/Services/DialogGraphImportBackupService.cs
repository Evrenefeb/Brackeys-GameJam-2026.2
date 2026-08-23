using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Creates and verifies backups of DialogGraph assets before import overwrite.
    /// Backups are created through AssetDatabase.CopyAsset in a Backups subdirectory
    /// within the graph folder so Unity owns the copied asset and meta file.
    /// </summary>
    public static class DialogGraphImportBackupService
    {
        private const string BackupFolderName = "Backups";
        private const int DefaultMaxBackups = 5;

        /// <summary>
        /// Creates a backup of the asset at <paramref name="assetPath"/>.
        /// Returns the project-relative path of the backup asset, or null on failure.
        /// </summary>
        public static string CreateBackup(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            var backupDir = GetBackupDirectory(assetPath);
            EnsureBackupDirectory(backupDir);

            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var backupName = $"{fileName}_backup_{timestamp}.asset";
            var backupPath = Path.Combine(backupDir, backupName).Replace("\\", "/");

            // Convert to project-relative paths
            var projectRelativeBackup = backupPath;
            if (projectRelativeBackup.StartsWith(Application.dataPath.Replace("\\", "/")))
            {
                projectRelativeBackup = "Assets" + projectRelativeBackup.Substring(
                    Application.dataPath.Replace("\\", "/").Length);
            }

            if (!AssetDatabase.CopyAsset(assetPath, projectRelativeBackup))
            {
                Debug.LogError($"[DialogGraphImportBackupService] Failed to copy asset from {assetPath} to {projectRelativeBackup}.");
                return null;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DialogGraphImportBackupService] Backup created at: {projectRelativeBackup}");
            return projectRelativeBackup;
        }

        /// <summary>
        /// Verifies that the backup exists and can be loaded.
        /// </summary>
        public static bool VerifyBackup(string backupPath, string originalPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath) || string.IsNullOrWhiteSpace(originalPath))
                return false;

            if (!File.Exists(GetAbsolutePath(backupPath)))
            {
                Debug.LogWarning($"[DialogGraphImportBackupService] Backup file not found at: {backupPath}");
                return false;
            }

            var backupAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(backupPath);
            if (backupAsset == null)
            {
                Debug.LogWarning($"[DialogGraphImportBackupService] Backup could not be loaded at: {backupPath}");
                return false;
            }

            Debug.Log($"[DialogGraphImportBackupService] Backup verified at: {backupPath}");
            return true;
        }

        /// <summary>
        /// Removes old backups exceeding <paramref name="maxBackups"/> in the backup directory.
        /// </summary>
        public static void CleanupOldBackups(string assetPath, int maxBackups = DefaultMaxBackups)
        {
            if (maxBackups <= 0)
                return;

            var backupDir = GetBackupDirectory(assetPath);
            var absBackupDir = GetAbsolutePath(backupDir);
            if (!Directory.Exists(absBackupDir))
                return;

            var baseName = Path.GetFileNameWithoutExtension(assetPath);
            var backupFiles = Directory.GetFiles(absBackupDir, $"{baseName}_backup_*.asset")
                .OrderByDescending(f => f)
                .ToList();

            // Skip the most recent maxBackups files
            for (var i = maxBackups; i < backupFiles.Count; i++)
            {
                var file = backupFiles[i].Replace("\\", "/");
                var projectRelativeFile = ToProjectRelativePath(file);

                try
                {
                    if (!AssetDatabase.DeleteAsset(projectRelativeFile))
                    {
                        Debug.LogWarning($"[DialogGraphImportBackupService] Failed to remove old backup through AssetDatabase: {projectRelativeFile}");
                        continue;
                    }

                    Debug.Log($"[DialogGraphImportBackupService] Removed old backup: {Path.GetFileName(projectRelativeFile)}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DialogGraphImportBackupService] Failed to remove old backup {projectRelativeFile}: {ex.Message}");
                }
            }

            if (backupFiles.Count > maxBackups)
                AssetDatabase.Refresh();
        }

        /// <summary>
        /// Returns the backup directory path relative to the graph folder.
        /// </summary>
        public static string GetBackupDirectory(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            var dir = Path.GetDirectoryName(assetPath).Replace("\\", "/");
            return $"{dir}/{BackupFolderName}";
        }

        #region ---------------- Helpers ----------------

        private static void EnsureBackupDirectory(string projectRelativeDir)
        {
            var absDir = GetAbsolutePath(projectRelativeDir);
            if (!Directory.Exists(absDir))
                Directory.CreateDirectory(absDir);
        }

        private static string GetAbsolutePath(string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath))
                return Application.dataPath;

            var dataPath = Application.dataPath.Replace("\\", "/");
            var rel = projectRelativePath.Replace("\\", "/");

            if (rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || rel == "Assets")
                return dataPath + "/" + rel.Substring("Assets/".Length);

            return rel;
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            var dataPath = Application.dataPath.Replace("\\", "/");
            var normalized = absolutePath.Replace("\\", "/");

            if (normalized.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + normalized.Substring(dataPath.Length + 1);
            }

            return normalized;
        }

        #endregion
    }
}
