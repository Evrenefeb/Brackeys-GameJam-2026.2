using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.Runtime.Models;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Orchestrates safe JSON import transactions for DialogGraph assets.
    /// The overwrite path proves the DTO can build and migrate in a temporary asset before the target graph is mutated.
    /// </summary>
    public static class DialogGraphImportTransactionService
    {
        private const string TempRootFolder = "Assets/DialogGraphSystem/Temp";
        private const string TempImportFolder = "Assets/DialogGraphSystem/Temp/ImportValidation";

        #region ---------------- Public API ----------------

        /// <summary>
        /// Imports JSON content as a new DialogGraph asset at a chosen folder.
        /// No snapshot or backup is needed for new-graph imports.
        /// </summary>
        public static DialogGraphImportResult ImportNew(string folderPath, string graphName, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return DialogGraphImportResult.Failed("Parse", "JSON content is empty.");

            if (string.IsNullOrWhiteSpace(folderPath))
                return DialogGraphImportResult.Failed("Build", "Target folder path is empty.");

            if (string.IsNullOrWhiteSpace(graphName))
                graphName = "ImportedConversation";

            graphName = MakeSafeFileName(graphName);

            DialogGraphExport dto;
            try
            {
                dto = JsonUtility.FromJson<DialogGraphExport>(json);
            }
            catch (Exception ex)
            {
                return DialogGraphImportResult.Failed("Parse", $"JSON parse error: {ex.Message}");
            }

            if (dto == null || !HasAnyNodes(dto))
                return DialogGraphImportResult.Failed("Validate", "Parsed DTO is empty or contains no nodes.");

            var validateResult = ValidateDto(dto);
            if (!validateResult.Success)
                return validateResult;

            var targetPath = $"{folderPath.TrimEnd('/')}/{graphName}.asset";
            targetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);

            DialogGraph graph;
            try
            {
                graph = ScriptableObject.CreateInstance<DialogGraph>();
                AssetDatabase.CreateAsset(graph, targetPath);
            }
            catch (Exception ex)
            {
                return DialogGraphImportResult.Failed("Build", $"Failed to create asset: {ex.Message}", targetPath);
            }

            try
            {
                DialogJsonImportBridge.BuildFromDto(graph, dto);
                DialogGraphUpgradeService.MigrateToCurrent(graph);
            }
            catch (Exception ex)
            {
                AssetDatabase.DeleteAsset(targetPath);
                return DialogGraphImportResult.Failed("Build", $"Failed to build or migrate graph from DTO: {ex.Message}", targetPath);
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = graph;

            Debug.Log($"[DialogGraphImportTransactionService] New graph created at: {targetPath}");
            return DialogGraphImportResult.Succeeded(targetPath);
        }

        /// <summary>
        /// Imports JSON content by overwriting an existing DialogGraph at targetPath.
        /// Transaction order: snapshot, parse, DTO validate, temporary build/migrate, temporary validate,
        /// backup, backup verify, then mutate target. Unity asset edits are not truly atomic, so failures
        /// after target mutation restore from the verified backup where possible.
        /// </summary>
        public static DialogGraphImportResult ImportOverwrite(string targetPath, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return DialogGraphImportResult.Failed("Parse", "JSON content is empty.");

            if (string.IsNullOrWhiteSpace(targetPath))
                return DialogGraphImportResult.Failed("Build", "Target asset path is empty.");

            var snapshot = DialogGraphImportSnapshot.Capture(targetPath);
            if (snapshot == null)
            {
                return DialogGraphImportResult.Failed(
                    "Validate",
                    $"Target asset is not a valid DialogGraph at path: {targetPath}",
                    targetPath);
            }

            DialogGraphExport dto;
            try
            {
                dto = JsonUtility.FromJson<DialogGraphExport>(json);
            }
            catch (Exception ex)
            {
                return DialogGraphImportResult.Failed(
                    "Parse",
                    $"JSON parse error: {ex.Message}",
                    targetPath,
                    originalSnapshot: snapshot);
            }

            if (dto == null || !HasAnyNodes(dto))
            {
                return DialogGraphImportResult.Failed(
                    "Validate",
                    "Parsed DTO is empty or contains no nodes.",
                    targetPath,
                    originalSnapshot: snapshot);
            }

            var validateResult = ValidateDto(dto);
            if (!validateResult.Success)
            {
                return DialogGraphImportResult.Failed(
                    validateResult.FailureStage,
                    validateResult.Message,
                    targetPath,
                    errors: validateResult.Errors,
                    originalSnapshot: snapshot);
            }

            var tempPath = CreateTempPath(targetPath);
            if (!TryBuildTemporaryGraph(dto, tempPath, out var tempGraph, out var tempError))
            {
                CleanupTempAsset(tempPath);
                return DialogGraphImportResult.Failed(
                    "Build",
                    tempError,
                    targetPath,
                    tempPath: tempPath,
                    originalSnapshot: snapshot);
            }

            var tempValidation = ValidateBuiltGraph(tempGraph);
            if (!tempValidation.Success)
            {
                CleanupTempAsset(tempPath);
                return DialogGraphImportResult.Failed(
                    tempValidation.FailureStage,
                    tempValidation.Message,
                    targetPath,
                    tempPath: tempPath,
                    errors: tempValidation.Errors,
                    originalSnapshot: snapshot);
            }

            var backupPath = DialogGraphImportBackupService.CreateBackup(targetPath);
            if (string.IsNullOrEmpty(backupPath))
            {
                CleanupTempAsset(tempPath);
                return DialogGraphImportResult.Failed(
                    "Backup",
                    "Failed to create backup before overwrite. Import cancelled.",
                    targetPath,
                    tempPath: tempPath,
                    originalSnapshot: snapshot);
            }

            if (!DialogGraphImportBackupService.VerifyBackup(backupPath, targetPath))
            {
                CleanupTempAsset(tempPath);
                return DialogGraphImportResult.Failed(
                    "Backup",
                    $"Backup created at {backupPath} but verification failed. Import cancelled.",
                    targetPath,
                    backupPath,
                    tempPath,
                    originalSnapshot: snapshot);
            }

            var graph = AssetDatabase.LoadAssetAtPath<DialogGraph>(targetPath);
            if (graph == null)
            {
                CleanupTempAsset(tempPath);
                return DialogGraphImportResult.Failed(
                    "Validate",
                    $"Target asset at {targetPath} could not be loaded as DialogGraph.",
                    targetPath,
                    backupPath,
                    tempPath,
                    originalSnapshot: snapshot);
            }

            try
            {
                RemoveSubAssets(targetPath, graph);
                ClearGraphData(graph);
                DialogJsonImportBridge.BuildFromDto(graph, dto);
                DialogGraphUpgradeService.MigrateToCurrent(graph);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogGraphImportTransactionService] Commit failed, restoring from backup. Error: {ex.Message}");

                try
                {
                    RestoreFromBackup(targetPath, backupPath);
                    CleanupTempAsset(tempPath);
                    return DialogGraphImportResult.Failed(
                        "Commit",
                        $"Failed to overwrite graph. Original graph has been restored from backup at {backupPath}. Error: {ex.Message}",
                        targetPath,
                        backupPath,
                        tempPath,
                        originalSnapshot: snapshot);
                }
                catch (Exception restoreEx)
                {
                    CleanupTempAsset(tempPath);
                    return DialogGraphImportResult.Failed(
                        "Commit",
                        $"Overwrite failed and restore failed. Backup is at {backupPath}. Original error: {ex.Message}. Restore error: {restoreEx.Message}",
                        targetPath,
                        backupPath,
                        tempPath,
                        originalSnapshot: snapshot);
                }
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = graph;
            CleanupTempAsset(tempPath);

            var postSnapshot = DialogGraphImportSnapshot.Capture(targetPath);
            if (postSnapshot == null)
            {
                Debug.LogError($"[DialogGraphImportTransactionService] Post-import snapshot failed at {targetPath}. Graph may be corrupted.");
                return DialogGraphImportResult.Failed(
                    "Commit",
                    "Import succeeded but post-import snapshot failed. Check the graph manually.",
                    targetPath,
                    backupPath,
                    originalSnapshot: snapshot);
            }

            Debug.Log($"[DialogGraphImportTransactionService] Overwrote DialogGraph at: {targetPath}. Backup at: {backupPath}");
            DialogGraphImportBackupService.CleanupOldBackups(targetPath);
            return DialogGraphImportResult.Succeeded(targetPath, backupPath, snapshot);
        }

        #endregion

        #region ---------------- Validation ----------------

        private static bool HasAnyNodes(DialogGraphExport dto)
        {
            return (dto.dialogNodes?.Count ?? 0) > 0 ||
                   (dto.choiceNodes?.Count ?? 0) > 0 ||
                   (dto.actionNodes?.Count ?? 0) > 0 ||
                   (dto.conditionNodes?.Count ?? 0) > 0 ||
                   (dto.variableMutationNodes?.Count ?? 0) > 0 ||
                   (dto.graphJumpNodes?.Count ?? 0) > 0 ||
                   (dto.outcomeNodes?.Count ?? 0) > 0;
        }

        private static DialogGraphImportResult ValidateDto(DialogGraphExport dto)
        {
            if (dto == null)
                return DialogGraphImportResult.Failed("Validate", "DTO is null.");

            return DialogGraphImportResult.Succeeded(null);
        }

        private static DialogGraphImportResult ValidateBuiltGraph(DialogGraph graph)
        {
            if (graph == null)
            {
                return DialogGraphImportResult.Failed("Validate", "Temporary graph could not be loaded after build.");
            }

            if (!HasBuiltNodes(graph))
            {
                return DialogGraphImportResult.Failed("Validate", "Temporary graph contains no importable nodes after build.");
            }

            return DialogGraphImportResult.Succeeded(null);
        }

        private static bool HasBuiltNodes(DialogGraph graph)
        {
            return (graph.nodes?.Count ?? 0) > 0 ||
                   (graph.choiceNodes?.Count ?? 0) > 0 ||
                   (graph.actionNodes?.Count ?? 0) > 0 ||
                   (graph.conditionNodes?.Count ?? 0) > 0 ||
                   (graph.variableMutationNodes?.Count ?? 0) > 0 ||
                   (graph.graphJumpNodes?.Count ?? 0) > 0 ||
                   (graph.outcomeNodes?.Count ?? 0) > 0;
        }

        #endregion

        #region ---------------- Temporary Build ----------------

        private static bool TryBuildTemporaryGraph(
            DialogGraphExport dto,
            string tempPath,
            out DialogGraph graph,
            out string error)
        {
            graph = null;
            error = null;

            try
            {
                EnsureTempFolder();
                graph = ScriptableObject.CreateInstance<DialogGraph>();
                AssetDatabase.CreateAsset(graph, tempPath);
                DialogJsonImportBridge.BuildFromDto(graph, dto);
                DialogGraphUpgradeService.MigrateToCurrent(graph);
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                graph = AssetDatabase.LoadAssetAtPath<DialogGraph>(tempPath);
                return graph != null;
            }
            catch (Exception ex)
            {
                error = $"Failed to build and migrate temporary graph: {ex.Message}";
                return false;
            }
        }

        private static string CreateTempPath(string targetPath)
        {
            var targetName = string.IsNullOrWhiteSpace(targetPath)
                ? "DialogGraph"
                : Path.GetFileNameWithoutExtension(targetPath);
            return $"{TempImportFolder}/{MakeSafeFileName(targetName)}_import_validation_{Guid.NewGuid():N}.asset";
        }

        private static void EnsureTempFolder()
        {
            EnsureFolder(TempImportFolder);
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            var normalizedPath = assetFolderPath?.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                throw new ArgumentException("Asset folder path must be non-empty.", nameof(assetFolderPath));
            }

            if (!normalizedPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Folder must live under Assets. Received '{assetFolderPath}'.");
            }

            if (AssetDatabase.IsValidFolder(normalizedPath))
            {
                return;
            }

            var relativePath = normalizedPath.Length == "Assets".Length
                ? string.Empty
                : normalizedPath.Substring("Assets".Length).TrimStart('/');

            var fullPath = string.IsNullOrWhiteSpace(relativePath)
                ? Application.dataPath
                : Path.Combine(Application.dataPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (!AssetDatabase.IsValidFolder(normalizedPath))
            {
                throw new InvalidOperationException($"Failed to create import temp folder at '{normalizedPath}'.");
            }
        }

        private static void CleanupTempAsset(string tempPath)
        {
            if (!string.IsNullOrWhiteSpace(tempPath))
            {
                AssetDatabase.DeleteAsset(tempPath);
            }

            DeleteFolderIfEmpty(TempImportFolder);
            DeleteFolderIfEmpty(TempRootFolder);
            AssetDatabase.Refresh();
        }

        private static void DeleteFolderIfEmpty(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", folderPath));
            if (Directory.Exists(fullPath) && Directory.EnumerateFileSystemEntries(fullPath).Any())
            {
                return;
            }

            AssetDatabase.DeleteAsset(folderPath);
        }

        #endregion

        #region ---------------- Graph Mutation ----------------

        private static void RemoveSubAssets(string targetPath, DialogGraph graph)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(targetPath);
            foreach (var sub in allAssets)
            {
                if (sub != null && sub != graph)
                    AssetDatabase.RemoveObjectFromAsset(sub);
            }
        }

        private static void ClearGraphData(DialogGraph graph)
        {
            graph.nodes?.Clear();
            graph.choiceNodes?.Clear();
            graph.actionNodes?.Clear();
            graph.conditionNodes?.Clear();
            graph.variableMutationNodes?.Clear();
            graph.graphJumpNodes?.Clear();
            graph.outcomeNodes?.Clear();
            graph.links?.Clear();
            graph.ClearAllGroupLayouts();
        }

        #endregion

        #region ---------------- Restore ----------------

        private static void RestoreFromBackup(string targetPath, string backupPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(backupPath))
                throw new ArgumentException("Target path and backup path must be non-empty.");

            var targetGraph = AssetDatabase.LoadAssetAtPath<DialogGraph>(targetPath);
            if (targetGraph == null)
            {
                throw new InvalidOperationException($"Target graph could not be loaded for restore at {targetPath}.");
            }

            var backupGraph = AssetDatabase.LoadAssetAtPath<DialogGraph>(backupPath);
            if (backupGraph == null)
            {
                throw new InvalidOperationException($"Backup graph could not be loaded for restore at {backupPath}.");
            }

            var backupDto = DialogGraphJsonSerializationUtility.BuildExportDto(
                backupGraph,
                DialogGraphJsonExportOptions.Default);

            RemoveSubAssets(targetPath, targetGraph);
            ClearGraphData(targetGraph);
            DialogJsonImportBridge.BuildFromDto(targetGraph, backupDto);
            DialogGraphUpgradeService.MigrateToCurrent(targetGraph);
            EditorUtility.SetDirty(targetGraph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DialogGraphImportTransactionService] Restored content of {targetPath} from backup {backupPath}.");
        }

        #endregion

        #region ---------------- Helpers ----------------

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unnamed";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (invalid.Contains(chars[i]))
                    chars[i] = '_';
            }

            var safeName = new string(chars).Trim('_').Trim();
            return string.IsNullOrWhiteSpace(safeName) ? "Unnamed" : safeName;
        }

        #endregion
    }
}
