using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Explicit editor-only upgrade utility for migrating legacy dialog graph schema data.
    /// </summary>
    public static class DialogGraphUpgradeService
    {
        #region ---------------- Public API ----------------

        /// <summary>
        /// Inspects a graph and returns the expected migration work without mutating the graph.
        /// </summary>
        public static DialogGraphUpgradePreview PreviewUpgrade(DialogGraph graph)
        {
            if (graph == null)
            {
                return DialogGraphUpgradePreview.Failed("No graph was provided.");
            }

            var report = DialogGraphSchemaDetector.Inspect(graph);
            var preview = new DialogGraphUpgradePreview(
                graph.name,
                ResolveGraphPath(graph),
                report.GraphSchemaVersion,
                report.CurrentSchemaVersion,
                report.LinkCount,
                report.LinksMissingLinkGuidCount,
                report.DuplicateLinkGuidCount,
                report.DuplicateNodeGuidCount,
                report.DuplicateChoiceIdCount,
                report.IsUpgradeNeeded);

            if (report.IsNewerSchema)
            {
                preview.Errors.Add(
                    $"Graph schema version {report.GraphSchemaVersion} is newer than supported schema {report.CurrentSchemaVersion}. " +
                    "Upgrade is blocked to avoid downgrading or corrupting the asset.");
            }

            if (report.NullLinkCount > 0)
            {
                preview.Warnings.Add(
                    $"Graph contains {report.NullLinkCount} null link entry or entries. " +
                    "The upgrade will remove those empty entries.");
            }

            if (report.EmptyLinkCount > 0)
            {
                preview.Warnings.Add(
                    $"Graph contains {report.EmptyLinkCount} empty link entry or entries. " +
                    "The upgrade will remove entries that do not contain endpoints.");
            }

            if (report.MissingGraphGuidCount > 0)
            {
                preview.Warnings.Add("Graph is missing a stable graph identity. The upgrade will assign one.");
            }

            if (report.MissingNodeGuidCount > 0)
            {
                preview.Warnings.Add($"Graph is missing {report.MissingNodeGuidCount} node identity value(s). The upgrade will fill only blank identities.");
            }

            if (report.DuplicateNodeGuidCount > 0)
            {
                preview.Warnings.Add($"Graph contains {report.DuplicateNodeGuidCount} duplicate node identity value(s). The upgrade will assign replacement identities to later duplicates.");
            }

            if (report.MissingChoiceIdCount > 0)
            {
                preview.Warnings.Add($"Graph is missing {report.MissingChoiceIdCount} choice identity value(s). The upgrade will fill only blank choices.");
            }

            if (report.DuplicateChoiceIdCount > 0)
            {
                preview.Warnings.Add($"Graph contains {report.DuplicateChoiceIdCount} duplicate choice identity value(s). The upgrade will assign replacement identities to later duplicates.");
            }

            if (report.MissingFromPortKeyCount > 0 || report.MissingToPortKeyCount > 0)
            {
                preview.Warnings.Add("Graph has links missing stable port keys. The upgrade will infer keys from existing node types and legacy port indexes.");
            }

            return preview;
        }

        /// <summary>
        /// Idempotently fills missing v2.1 identity, schema, metadata, choice, link, and port-key defaults.
        /// Existing valid identities and graph structure are preserved.
        /// </summary>
        public static DialogGraphMigrationResult MigrateToCurrent(DialogGraph graph)
        {
            var result = new DialogGraphMigrationResult();
            if (graph == null)
            {
                result.Errors.Add("No graph was provided.");
                return result;
            }

            result.GraphName = graph.name;
            result.GraphPath = ResolveGraphPath(graph);

            if (graph.GraphSchemaVersion > DialogGraph.CurrentSchemaVersion)
            {
                result.Errors.Add(
                    $"Graph schema version {graph.GraphSchemaVersion} is newer than supported schema {DialogGraph.CurrentSchemaVersion}. " +
                    "Migration was skipped so the asset is not downgraded.");
                return result;
            }

            try
            {
                result.Changed |= EnsureGraphIdentity(graph, result);
                result.Changed |= EnsureMetadataDefaults(graph, result);
                result.Changed |= EnsureNodeIdentities(graph, result);
                result.Changed |= EnsureChoiceIdentities(graph, result);
                result.Changed |= RemoveInvalidLinks(graph, result);
                result.Changed |= EnsureLinkIdentities(graph, result);

                if (result.Errors.Count == 0)
                {
                    result.Changed |= EnsurePortKeys(graph, result);
                }

                result.Success = result.Errors.Count == 0;

                if (result.Success && graph.GraphSchemaVersion != DialogGraph.CurrentSchemaVersion)
                {
                    graph.MarkSchemaCurrentForMigration();
                    result.SchemaUpdated = true;
                    result.Changed = true;
                }

                if (result.Success && result.Changed)
                {
                    MarkGraphHierarchyDirty(graph);
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add($"Migration failed: {exception.Message}");
            }

            return result;
        }

        /// <summary>
        /// Assigns a missing graph identity without changing an existing graph GUID.
        /// </summary>
        public static bool EnsureGraphIdentity(DialogGraph graph)
        {
            return EnsureGraphIdentity(graph, null);
        }

        /// <summary>
        /// Assigns missing Start/End and authored-node identities without regenerating existing GUIDs.
        /// </summary>
        public static bool EnsureNodeIdentities(DialogGraph graph)
        {
            return EnsureNodeIdentities(graph, null);
        }

        /// <summary>
        /// Assigns missing stable link identities and replaces duplicate link identities.
        /// </summary>
        public static bool EnsureLinkIdentities(DialogGraph graph)
        {
            return EnsureLinkIdentities(graph, null);
        }

        /// <summary>
        /// Assigns missing stable choice identities without changing existing IDs.
        /// </summary>
        public static bool EnsureChoiceIdentities(DialogGraph graph)
        {
            return EnsureChoiceIdentities(graph, null);
        }

        /// <summary>
        /// Infers missing stable link port keys from existing node type and legacy port index data.
        /// </summary>
        public static bool EnsurePortKeys(DialogGraph graph)
        {
            return EnsurePortKeys(graph, null);
        }

        /// <summary>
        /// Explicitly upgrades a graph by assigning missing/duplicate link identities and marking schema current.
        /// </summary>
        public static DialogGraphUpgradeResult UpgradeGraph(DialogGraph graph, bool createBackup)
        {
            if (graph == null)
            {
                return DialogGraphUpgradeResult.Failed("No graph was provided.");
            }

            var preview = PreviewUpgrade(graph);
            var result = DialogGraphUpgradeResult.FromPreview(preview);
            if (preview.Errors.Count > 0)
            {
                result.Errors.AddRange(preview.Errors);
                return result;
            }

            if (!preview.IsUpgradeNeeded)
            {
                result.Success = true;
                result.Warnings.Add("Graph is already current; no migration was needed.");
                return result;
            }

            var rollbackSnapshot = DialogGraphUpgradeRollbackSnapshot.Capture(graph);
            var safetySnapshot = DialogGraphUpgradeSafetySnapshot.Capture(graph);
            var backupPath = string.Empty;
            if (createBackup && !TryCreateBackup(graph, result, out backupPath))
            {
                return result;
            }

            result.BackupPath = backupPath;
            Undo.RecordObject(graph, "Upgrade Dialogue Graph");

            try
            {
                var migrationResult = MigrateToCurrent(graph);
                result.LinkGuidAssignedCount = migrationResult.LinkGuidAssignedCount;
                result.DuplicateLinkGuidFixedCount = migrationResult.DuplicateLinkGuidFixedCount;
                result.DuplicateNodeGuidFixedCount = migrationResult.DuplicateNodeGuidFixedCount;
                result.DuplicateChoiceIdFixedCount = migrationResult.DuplicateChoiceIdFixedCount;

                if (!migrationResult.Success)
                {
                    result.Errors.AddRange(migrationResult.Errors);
                    rollbackSnapshot.Restore();
                    result.NewSchemaVersion = graph.GraphSchemaVersion;
                    return result;
                }

                if (!safetySnapshot.ValidateProtectedState(graph, result.Errors))
                {
                    result.Errors.Add("Upgrade aborted because protected graph data changed during migration.");
                    rollbackSnapshot.Restore();
                    result.NewSchemaVersion = graph.GraphSchemaVersion;
                    return result;
                }

                AssetDatabase.SaveAssets();

                result.Success = true;
                result.NewSchemaVersion = graph.GraphSchemaVersion;
                return result;
            }
            catch (Exception exception)
            {
                result.Errors.Add($"Upgrade failed: {exception.Message}");
                rollbackSnapshot.Restore();
                result.NewSchemaVersion = graph.GraphSchemaVersion;
                return result;
            }
        }

        #endregion

        #region ---------------- Upgrade Implementation ----------------

        private static bool EnsureGraphIdentity(DialogGraph graph, DialogGraphMigrationResult result)
        {
            if (graph == null || graph.HasGraphGuid)
            {
                return false;
            }

            graph.AssignGraphGuidIfMissing(Guid.NewGuid().ToString("N"));
            if (result != null)
            {
                result.GraphGuidAssigned = true;
            }

            return true;
        }

        private static bool EnsureMetadataDefaults(DialogGraph graph, DialogGraphMigrationResult result)
        {
            if (graph == null)
            {
                return false;
            }

            var changed = false;
            changed |= EnsureList(ref graph.links);
            changed |= EnsureList(ref graph.nodes);
            changed |= EnsureList(ref graph.choiceNodes);
            changed |= EnsureList(ref graph.actionNodes);
            changed |= EnsureList(ref graph.conditionNodes);
            changed |= EnsureList(ref graph.variableMutationNodes);
            changed |= EnsureList(ref graph.graphJumpNodes);
            changed |= EnsureList(ref graph.tags);
            changed |= EnsureList(ref graph.categories);
            changed |= EnsureList(ref graph.participatingCharacters);
            changed |= EnsureList(ref graph.availableActions);
            changed |= EnsureList(ref graph.availableVariables);
            changed |= graph.EnsureGroupLayoutDefaultsForEditor();

            if (graph.graphJumpNodes != null)
            {
                foreach (var graphJumpNode in graph.graphJumpNodes)
                {
                    if (graphJumpNode != null && graphJumpNode.EnsureReference())
                    {
                        changed = true;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(graph.editorVersion))
            {
                graph.editorVersion = Application.unityVersion;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(graph.lastModifiedUtc))
            {
                graph.lastModifiedUtc = DateTime.UtcNow.ToString("o");
                changed = true;
            }

            if (changed && result != null)
            {
                result.MetadataDefaultAssignedCount++;
            }

            return changed;
        }

        private static bool EnsureList<T>(ref List<T> list)
        {
            if (list != null)
            {
                return false;
            }

            list = new List<T>();
            return true;
        }

        private static bool EnsureNodeIdentities(DialogGraph graph, DialogGraphMigrationResult result)
        {
            if (graph == null)
            {
                return false;
            }

            var changed = false;
            var usedGuids = new HashSet<string>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(graph.startGuid))
            {
                graph.startGuid = CreateUniqueNodeGuid(usedGuids);
                if (!graph.startInitialized)
                {
                    graph.startPosition = new Vector2(-320f, 80f);
                    graph.startInitialized = true;
                }

                changed = true;
                if (result != null)
                {
                    result.NodeGuidAssignedCount++;
                }
            }
            else
            {
                usedGuids.Add(graph.startGuid);
            }

            if (string.IsNullOrWhiteSpace(graph.endGuid) || usedGuids.Contains(graph.endGuid))
            {
                graph.endGuid = CreateUniqueNodeGuid(usedGuids);
                if (!graph.endInitialized)
                {
                    graph.endPosition = new Vector2(720f, 80f);
                    graph.endInitialized = true;
                }

                changed = true;
                if (result != null)
                {
                    result.NodeGuidAssignedCount++;
                }
            }
            else
            {
                usedGuids.Add(graph.endGuid);
            }

            changed |= EnsureNodeListIdentities(graph.nodes, usedGuids, result);
            changed |= EnsureNodeListIdentities(graph.choiceNodes, usedGuids, result);
            changed |= EnsureNodeListIdentities(graph.actionNodes, usedGuids, result);
            changed |= EnsureNodeListIdentities(graph.conditionNodes, usedGuids, result);
            changed |= EnsureNodeListIdentities(graph.variableMutationNodes, usedGuids, result);
            changed |= EnsureNodeListIdentities(graph.graphJumpNodes, usedGuids, result);

            return changed;
        }

        private static bool EnsureNodeListIdentities<TNode>(
            IEnumerable<TNode> nodes,
            HashSet<string> usedGuids,
            DialogGraphMigrationResult result)
            where TNode : BaseNode
        {
            if (nodes == null)
            {
                return false;
            }

            var changed = false;
            foreach (var node in nodes)
            {
                if (node == null)
                {
                    continue;
                }

                var existingGuid = node.GetGuid();
                if (!string.IsNullOrWhiteSpace(existingGuid) && usedGuids.Add(existingGuid))
                {
                    continue;
                }

                node.SetGuid(CreateUniqueNodeGuid(usedGuids));
                changed = true;
                if (result != null)
                {
                    result.NodeGuidAssignedCount++;
                    if (!string.IsNullOrWhiteSpace(existingGuid))
                    {
                        result.DuplicateNodeGuidFixedCount++;
                    }
                }
            }

            return changed;
        }

        private static string CreateUniqueNodeGuid(HashSet<string> usedGuids)
        {
            string guid;
            do
            {
                guid = Guid.NewGuid().ToString("N");
            }
            while (usedGuids != null && !usedGuids.Add(guid));

            return guid;
        }

        private static bool EnsureChoiceIdentities(DialogGraph graph, DialogGraphMigrationResult result)
        {
            if (graph?.choiceNodes == null)
            {
                return false;
            }

            var changed = false;
            var usedChoiceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choiceNode in graph.choiceNodes)
            {
                if (choiceNode == null)
                {
                    continue;
                }

                if (choiceNode.choices == null)
                {
                    continue;
                }

                var nodeChanged = false;
                for (var choiceIndex = 0; choiceIndex < choiceNode.choices.Count; choiceIndex++)
                {
                    var choice = choiceNode.choices[choiceIndex];
                    if (choice == null)
                    {
                        continue;
                    }

                    var previousChoiceId = choice.choiceId;
                    if (!string.IsNullOrWhiteSpace(previousChoiceId) && usedChoiceIds.Add(previousChoiceId))
                    {
                        continue;
                    }

                    choice.choiceId = CreateUniqueChoiceId(usedChoiceIds);
                    changed = true;
                    nodeChanged = true;
                    if (!string.IsNullOrWhiteSpace(previousChoiceId))
                    {
                        UpdateChoicePortKeyReferences(graph, choiceNode.GetGuid(), choiceIndex, previousChoiceId, choice.choiceId);
                    }

                    if (result != null)
                    {
                        if (string.IsNullOrWhiteSpace(previousChoiceId))
                        {
                            result.ChoiceIdAssignedCount++;
                        }
                        else
                        {
                            result.DuplicateChoiceIdFixedCount++;
                        }
                    }

                }

                if (nodeChanged)
                {
                    EditorUtility.SetDirty(choiceNode);
                }
            }

            return changed;
        }

        private static void UpdateChoicePortKeyReferences(
            DialogGraph graph,
            string choiceNodeGuid,
            int choiceIndex,
            string previousChoiceId,
            string newChoiceId)
        {
            if (graph?.links == null ||
                string.IsNullOrWhiteSpace(choiceNodeGuid) ||
                string.IsNullOrWhiteSpace(previousChoiceId) ||
                string.IsNullOrWhiteSpace(newChoiceId))
            {
                return;
            }

            var previousPortKey = DialogGraphPortKeys.ForChoiceId(previousChoiceId);
            var newPortKey = DialogGraphPortKeys.ForChoiceId(newChoiceId);
            foreach (var link in graph.links)
            {
                if (link == null ||
                    !string.Equals(link.fromGuid, choiceNodeGuid, StringComparison.Ordinal) ||
                    link.fromPortIndex != choiceIndex ||
                    !string.Equals(link.fromPortKey, previousPortKey, StringComparison.Ordinal))
                {
                    continue;
                }

                link.fromPortKey = newPortKey;
            }
        }

        private static string CreateUniqueChoiceId(HashSet<string> usedChoiceIds)
        {
            string choiceId;
            do
            {
                choiceId = Choice.CreateChoiceId();
            }
            while (usedChoiceIds != null && !usedChoiceIds.Add(choiceId));

            return choiceId;
        }

        private static bool EnsureLinkIdentities(DialogGraph graph, DialogGraphMigrationResult result)
        {
            if (graph?.links == null || graph.links.Count == 0)
            {
                return false;
            }

            var serializedGraph = new SerializedObject(graph);
            serializedGraph.Update();

            var linksProperty = serializedGraph.FindProperty("links");
            if (linksProperty == null || !linksProperty.isArray || linksProperty.arraySize != graph.links.Count)
            {
                result?.Errors.Add("Serialized link list could not be resolved for migration.");
                return false;
            }

            var changed = false;
            var usedLinkGuids = graph.links
                .Where(link => link != null && !string.IsNullOrWhiteSpace(link.LinkGuid))
                .Select(link => link.LinkGuid)
                .ToHashSet(StringComparer.Ordinal);

            var seenOriginalLinkGuids = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < graph.links.Count; i++)
            {
                var link = graph.links[i];
                if (link == null)
                {
                    continue;
                }

                var linkGuidProperty = linksProperty.GetArrayElementAtIndex(i).FindPropertyRelative("linkGuid");
                if (linkGuidProperty == null)
                {
                    result?.Errors.Add($"Serialized linkGuid field could not be resolved for link index {i}.");
                    continue;
                }

                var existingLinkGuid = link.LinkGuid;
                if (string.IsNullOrWhiteSpace(existingLinkGuid))
                {
                    var linkGuid = CreateUniqueLinkGuid(usedLinkGuids);
                    linkGuidProperty.stringValue = linkGuid;
                    usedLinkGuids.Add(linkGuid);
                    changed = true;
                    if (result != null)
                    {
                        result.LinkGuidAssignedCount++;
                    }

                    continue;
                }

                if (seenOriginalLinkGuids.Add(existingLinkGuid))
                {
                    continue;
                }

                var replacementLinkGuid = CreateUniqueLinkGuid(usedLinkGuids);
                linkGuidProperty.stringValue = replacementLinkGuid;
                usedLinkGuids.Add(replacementLinkGuid);
                changed = true;
                if (result != null)
                {
                    result.DuplicateLinkGuidFixedCount++;
                }
            }

            if (changed)
            {
                serializedGraph.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static bool RemoveInvalidLinks(DialogGraph graph, DialogGraphMigrationResult result)
        {
            if (graph?.links == null || graph.links.Count == 0)
            {
                return false;
            }

            var before = graph.links.Count;
            graph.links.RemoveAll(link =>
                link == null ||
                string.IsNullOrWhiteSpace(link.fromGuid) ||
                string.IsNullOrWhiteSpace(link.toGuid));

            var removed = before - graph.links.Count;
            if (removed <= 0)
            {
                return false;
            }

            if (result != null)
            {
                result.InvalidLinkRemovedCount += removed;
            }

            return true;
        }

        private static bool EnsurePortKeys(DialogGraph graph, DialogGraphMigrationResult result)
        {
            if (graph?.links == null || graph.links.Count == 0)
            {
                return false;
            }

            var changed = false;
            foreach (var link in graph.links)
            {
                if (link == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(link.fromPortKey))
                {
                    var fromPortKey = InferFromPortKey(graph, link.fromGuid, link.fromPortIndex);
                    if (!string.IsNullOrWhiteSpace(fromPortKey))
                    {
                        link.fromPortKey = fromPortKey;
                        changed = true;
                        if (result != null)
                        {
                            result.FromPortKeyAssignedCount++;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(link.toPortKey))
                {
                    link.toPortKey = DialogGraphPortKeys.Default;
                    changed = true;
                    if (result != null)
                    {
                        result.ToPortKeyAssignedCount++;
                    }
                }
            }

            return changed;
        }

        private static string InferFromPortKey(DialogGraph graph, string fromGuid, int fromPortIndex)
        {
            if (graph == null || string.IsNullOrWhiteSpace(fromGuid))
            {
                return DialogGraphPortKeys.Default;
            }

            var choiceNode = graph.choiceNodes?.FirstOrDefault(node =>
                node != null &&
                string.Equals(node.GetGuid(), fromGuid, StringComparison.Ordinal));

            if (choiceNode?.choices != null &&
                fromPortIndex >= 0 &&
                fromPortIndex < choiceNode.choices.Count)
            {
                return choiceNode.choices[fromPortIndex]?.PortKey ?? string.Empty;
            }

            var conditionNode = graph.conditionNodes?.FirstOrDefault(node =>
                node != null &&
                string.Equals(node.GetGuid(), fromGuid, StringComparison.Ordinal));

            if (conditionNode != null)
            {
                return fromPortIndex == ConditionNode.FalsePortIndex
                    ? DialogGraphPortKeys.False
                    : DialogGraphPortKeys.True;
            }

            var actionNode = graph.actionNodes?.FirstOrDefault(node =>
                node != null &&
                string.Equals(node.GetGuid(), fromGuid, StringComparison.Ordinal));

            return actionNode != null ? DialogGraphPortKeys.ActionSuccess : DialogGraphPortKeys.Default;
        }

        private static void MarkGraphHierarchyDirty(DialogGraph graph)
        {
            if (graph == null)
            {
                return;
            }

            EditorUtility.SetDirty(graph);
            MarkNodeListDirty(graph.nodes);
            MarkNodeListDirty(graph.choiceNodes);
            MarkNodeListDirty(graph.actionNodes);
            MarkNodeListDirty(graph.conditionNodes);
            MarkNodeListDirty(graph.variableMutationNodes);
            MarkNodeListDirty(graph.graphJumpNodes);
        }

        private static void MarkNodeListDirty<TNode>(IEnumerable<TNode> nodes)
            where TNode : BaseNode
        {
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                if (node != null)
                {
                    EditorUtility.SetDirty(node);
                }
            }
        }

        private static void UpgradeLinkGuids(SerializedObject serializedGraph, DialogGraph graph, DialogGraphUpgradeResult result)
        {
            if (graph.links == null || graph.links.Count == 0)
            {
                return;
            }

            var linksProperty = serializedGraph.FindProperty("links");
            if (linksProperty == null || !linksProperty.isArray || linksProperty.arraySize != graph.links.Count)
            {
                result.Errors.Add("Serialized link list could not be resolved for upgrade.");
                return;
            }

            var usedLinkGuids = graph.links
                .Where(link => link != null && !string.IsNullOrWhiteSpace(link.LinkGuid))
                .Select(link => link.LinkGuid)
                .ToHashSet(StringComparer.Ordinal);

            var seenOriginalLinkGuids = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < graph.links.Count; i++)
            {
                var link = graph.links[i];
                if (link == null)
                {
                    continue;
                }

                var linkGuidProperty = linksProperty.GetArrayElementAtIndex(i).FindPropertyRelative("linkGuid");
                if (linkGuidProperty == null)
                {
                    result.Errors.Add($"Serialized linkGuid field could not be resolved for link index {i}.");
                    continue;
                }

                var existingLinkGuid = link.LinkGuid;
                if (string.IsNullOrWhiteSpace(existingLinkGuid))
                {
                    var linkGuid = CreateUniqueLinkGuid(usedLinkGuids);
                    linkGuidProperty.stringValue = linkGuid;
                    usedLinkGuids.Add(linkGuid);
                    result.LinkGuidAssignedCount++;
                    continue;
                }

                if (seenOriginalLinkGuids.Add(existingLinkGuid))
                {
                    continue;
                }

                var replacementLinkGuid = CreateUniqueLinkGuid(usedLinkGuids);
                linkGuidProperty.stringValue = replacementLinkGuid;
                usedLinkGuids.Add(replacementLinkGuid);
                result.DuplicateLinkGuidFixedCount++;
            }

            serializedGraph.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSchemaVersion(SerializedObject serializedGraph, int schemaVersion)
        {
            var schemaProperty = serializedGraph.FindProperty("graphSchemaVersion");
            if (schemaProperty == null)
            {
                throw new InvalidOperationException("Serialized graphSchemaVersion field could not be resolved.");
            }

            schemaProperty.intValue = schemaVersion;
        }

        private static string CreateUniqueLinkGuid(HashSet<string> usedLinkGuids)
        {
            string linkGuid;
            do
            {
                linkGuid = Guid.NewGuid().ToString("N");
            }
            while (usedLinkGuids.Contains(linkGuid));

            return linkGuid;
        }

        #endregion

        #region ---------------- Backup ----------------

        private static bool TryCreateBackup(DialogGraph graph, DialogGraphUpgradeResult result, out string backupPath)
        {
            backupPath = string.Empty;

            var graphPath = AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrWhiteSpace(graphPath))
            {
                result.Errors.Add("Backup was requested, but the graph is not a saved asset.");
                return false;
            }

            var graphDirectory = Path.GetDirectoryName(graphPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(graphDirectory))
            {
                result.Errors.Add($"Backup was requested, but the graph path is invalid: {graphPath}");
                return false;
            }

            var backupFolder = $"{graphDirectory}/Backups";
            if (!AssetDatabase.IsValidFolder(backupFolder) &&
                string.IsNullOrWhiteSpace(AssetDatabase.CreateFolder(graphDirectory, "Backups")))
            {
                result.Errors.Add($"Could not create backup folder: {backupFolder}");
                return false;
            }

            var graphFileName = SanitizeFileName(Path.GetFileNameWithoutExtension(graphPath));
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{graphFileName}_schema{result.PreviousSchemaVersion}_{timestamp}.asset";
            backupPath = AssetDatabase.GenerateUniqueAssetPath($"{backupFolder}/{backupFileName}");

            if (AssetDatabase.CopyAsset(graphPath, backupPath))
            {
                return true;
            }

            result.Errors.Add($"Could not create graph backup at: {backupPath}");
            backupPath = string.Empty;
            return false;
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "DialogGraph";
            }

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName;
        }

        #endregion

        #region ---------------- Utilities ----------------

        private static string ResolveGraphPath(DialogGraph graph)
        {
            var path = AssetDatabase.GetAssetPath(graph);
            return string.IsNullOrWhiteSpace(path) ? "(unsaved graph)" : path;
        }

        #endregion
    }

    /// <summary>
    /// Read-only description of pending dialog graph schema upgrade work.
    /// </summary>
    public sealed class DialogGraphUpgradePreview
    {
        public string GraphName { get; }
        public string GraphPath { get; }
        public int PreviousSchemaVersion { get; }
        public int NewSchemaVersion { get; }
        public int LinkCount { get; }
        public int MissingLinkGuidCountBefore { get; }
        public int DuplicateLinkGuidCountBefore { get; }
        public int DuplicateNodeGuidCountBefore { get; }
        public int DuplicateChoiceIdCountBefore { get; }
        public bool IsUpgradeNeeded { get; }
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public DialogGraphUpgradePreview(
            string graphName,
            string graphPath,
            int previousSchemaVersion,
            int newSchemaVersion,
            int linkCount,
            int missingLinkGuidCountBefore,
            int duplicateLinkGuidCountBefore,
            int duplicateNodeGuidCountBefore,
            int duplicateChoiceIdCountBefore,
            bool isUpgradeNeeded)
        {
            GraphName = graphName ?? string.Empty;
            GraphPath = graphPath ?? string.Empty;
            PreviousSchemaVersion = previousSchemaVersion;
            NewSchemaVersion = newSchemaVersion;
            LinkCount = linkCount;
            MissingLinkGuidCountBefore = missingLinkGuidCountBefore;
            DuplicateLinkGuidCountBefore = duplicateLinkGuidCountBefore;
            DuplicateNodeGuidCountBefore = duplicateNodeGuidCountBefore;
            DuplicateChoiceIdCountBefore = duplicateChoiceIdCountBefore;
            IsUpgradeNeeded = isUpgradeNeeded;
        }

        public static DialogGraphUpgradePreview Failed(string error)
        {
            var preview = new DialogGraphUpgradePreview(string.Empty, string.Empty, 0, DialogGraph.CurrentSchemaVersion, 0, 0, 0, 0, 0, false);
            preview.Errors.Add(error);
            return preview;
        }
    }

    /// <summary>
    /// Structured result returned after an explicit graph schema upgrade attempt.
    /// </summary>
    public sealed class DialogGraphUpgradeResult
    {
        public bool Success { get; internal set; }
        public string GraphName { get; private set; }
        public string GraphPath { get; private set; }
        public int PreviousSchemaVersion { get; private set; }
        public int NewSchemaVersion { get; internal set; }
        public int LinkCount { get; private set; }
        public int MissingLinkGuidCountBefore { get; private set; }
        public int DuplicateNodeGuidCountBefore { get; private set; }
        public int DuplicateChoiceIdCountBefore { get; private set; }
        public int LinkGuidAssignedCount { get; internal set; }
        public int DuplicateLinkGuidFixedCount { get; internal set; }
        public int DuplicateNodeGuidFixedCount { get; internal set; }
        public int DuplicateChoiceIdFixedCount { get; internal set; }
        public string BackupPath { get; internal set; }
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public static DialogGraphUpgradeResult FromPreview(DialogGraphUpgradePreview preview)
        {
            var result = new DialogGraphUpgradeResult
            {
                GraphName = preview.GraphName,
                GraphPath = preview.GraphPath,
                PreviousSchemaVersion = preview.PreviousSchemaVersion,
                NewSchemaVersion = preview.NewSchemaVersion,
                LinkCount = preview.LinkCount,
                MissingLinkGuidCountBefore = preview.MissingLinkGuidCountBefore,
                DuplicateNodeGuidCountBefore = preview.DuplicateNodeGuidCountBefore,
                DuplicateChoiceIdCountBefore = preview.DuplicateChoiceIdCountBefore
            };
            result.Warnings.AddRange(preview.Warnings);
            return result;
        }

        public static DialogGraphUpgradeResult Failed(string error)
        {
            var result = new DialogGraphUpgradeResult();
            result.Errors.Add(error);
            return result;
        }
    }

    /// <summary>
    /// Structured result returned by the idempotent migration/default initialization pipeline.
    /// </summary>
    public sealed class DialogGraphMigrationResult
    {
        public bool Success { get; internal set; }
        public bool Changed { get; internal set; }
        public string GraphName { get; internal set; }
        public string GraphPath { get; internal set; }
        public bool GraphGuidAssigned { get; internal set; }
        public bool SchemaUpdated { get; internal set; }
        public int MetadataDefaultAssignedCount { get; internal set; }
        public int NodeGuidAssignedCount { get; internal set; }
        public int DuplicateNodeGuidFixedCount { get; internal set; }
        public int InvalidLinkRemovedCount { get; internal set; }
        public int LinkGuidAssignedCount { get; internal set; }
        public int DuplicateLinkGuidFixedCount { get; internal set; }
        public int ChoiceIdAssignedCount { get; internal set; }
        public int DuplicateChoiceIdFixedCount { get; internal set; }
        public int FromPortKeyAssignedCount { get; internal set; }
        public int ToPortKeyAssignedCount { get; internal set; }
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
    }

    internal sealed class DialogGraphUpgradeRollbackSnapshot
    {
        private readonly DialogGraph _graph;
        private readonly string _graphJson;
        private readonly List<ObjectJsonSnapshot> _nodeSnapshots;

        private DialogGraphUpgradeRollbackSnapshot(
            DialogGraph graph,
            string graphJson,
            List<ObjectJsonSnapshot> nodeSnapshots)
        {
            _graph = graph;
            _graphJson = graphJson;
            _nodeSnapshots = nodeSnapshots;
        }

        public static DialogGraphUpgradeRollbackSnapshot Capture(DialogGraph graph)
        {
            var nodeSnapshots = new List<ObjectJsonSnapshot>();
            CaptureNodeList(nodeSnapshots, graph.nodes);
            CaptureNodeList(nodeSnapshots, graph.choiceNodes);
            CaptureNodeList(nodeSnapshots, graph.actionNodes);
            CaptureNodeList(nodeSnapshots, graph.conditionNodes);
            CaptureNodeList(nodeSnapshots, graph.variableMutationNodes);
            CaptureNodeList(nodeSnapshots, graph.graphJumpNodes);

            return new DialogGraphUpgradeRollbackSnapshot(
                graph,
                EditorJsonUtility.ToJson(graph),
                nodeSnapshots);
        }

        public void Restore()
        {
            if (_graph == null)
            {
                return;
            }

            EditorJsonUtility.FromJsonOverwrite(_graphJson, _graph);
            foreach (var snapshot in _nodeSnapshots)
            {
                snapshot.Restore();
            }
        }

        private static void CaptureNodeList<TNode>(List<ObjectJsonSnapshot> snapshots, IEnumerable<TNode> nodes)
            where TNode : BaseNode
        {
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                if (node != null)
                {
                    snapshots.Add(ObjectJsonSnapshot.Capture(node));
                }
            }
        }

        private readonly struct ObjectJsonSnapshot
        {
            private readonly UnityEngine.Object _target;
            private readonly string _json;

            private ObjectJsonSnapshot(UnityEngine.Object target, string json)
            {
                _target = target;
                _json = json;
            }

            public static ObjectJsonSnapshot Capture(UnityEngine.Object target)
            {
                return new ObjectJsonSnapshot(target, EditorJsonUtility.ToJson(target));
            }

            public void Restore()
            {
                if (_target != null)
                {
                    EditorJsonUtility.FromJsonOverwrite(_json, _target);
                }
            }
        }
    }

    internal sealed class DialogGraphUpgradeSafetySnapshot
    {
        private readonly string _startGuid;
        private readonly string _endGuid;
        private readonly bool _startGuidMayChange;
        private readonly bool _endGuidMayChange;
        private readonly Vector2 _startPosition;
        private readonly Vector2 _endPosition;
        private readonly List<LinkEndpointSnapshot> _links;
        private readonly List<NodeSnapshot> _nodes;
        private readonly List<ChoiceListSnapshot> _choiceLists;

        private DialogGraphUpgradeSafetySnapshot(
            string startGuid,
            string endGuid,
            bool startGuidMayChange,
            bool endGuidMayChange,
            Vector2 startPosition,
            Vector2 endPosition,
            List<LinkEndpointSnapshot> links,
            List<NodeSnapshot> nodes,
            List<ChoiceListSnapshot> choiceLists)
        {
            _startGuid = startGuid;
            _endGuid = endGuid;
            _startGuidMayChange = startGuidMayChange;
            _endGuidMayChange = endGuidMayChange;
            _startPosition = startPosition;
            _endPosition = endPosition;
            _links = links;
            _nodes = nodes;
            _choiceLists = choiceLists;
        }

        public static DialogGraphUpgradeSafetySnapshot Capture(DialogGraph graph)
        {
            var nodeGuidCounts = CountNodeGuidOccurrences(graph);
            return new DialogGraphUpgradeSafetySnapshot(
                graph.startGuid,
                graph.endGuid,
                CanIdentityChange(graph.startGuid, nodeGuidCounts),
                CanIdentityChange(graph.endGuid, nodeGuidCounts),
                graph.startPosition,
                graph.endPosition,
                CaptureLinks(graph),
                CaptureNodes(graph, nodeGuidCounts),
                CaptureChoiceLists(graph));
        }

        public bool ValidateProtectedState(DialogGraph graph, List<string> errors)
        {
            var valid = true;
            valid &= ValidateScalar(IdentityPreservedOrSafelyRepaired(_startGuid, graph.startGuid, _startGuidMayChange), errors, "Start GUID changed.");
            valid &= ValidateScalar(IdentityPreservedOrSafelyRepaired(_endGuid, graph.endGuid, _endGuidMayChange), errors, "End GUID changed.");
            valid &= ValidateScalar(string.IsNullOrWhiteSpace(_startGuid) || _startPosition == graph.startPosition, errors, "Start position changed.");
            valid &= ValidateScalar(string.IsNullOrWhiteSpace(_endGuid) || _endPosition == graph.endPosition, errors, "End position changed.");
            valid &= ValidateLinks(graph, errors);
            valid &= ValidateNodes(graph, errors);
            valid &= ValidateChoiceLists(graph, errors);
            return valid;
        }

        private static List<LinkEndpointSnapshot> CaptureLinks(DialogGraph graph)
        {
            var links = new List<LinkEndpointSnapshot>();
            if (graph.links == null)
            {
                return links;
            }

            for (var i = 0; i < graph.links.Count; i++)
            {
                links.Add(LinkEndpointSnapshot.Capture(i, graph.links[i]));
            }

            return links;
        }

        private static List<NodeSnapshot> CaptureNodes(DialogGraph graph, Dictionary<string, int> nodeGuidCounts)
        {
            var nodes = new List<NodeSnapshot>();
            CaptureNodeList(nodes, "Dialog", graph.nodes, nodeGuidCounts);
            CaptureNodeList(nodes, "Choice", graph.choiceNodes, nodeGuidCounts);
            CaptureNodeList(nodes, "Action", graph.actionNodes, nodeGuidCounts);
            CaptureNodeList(nodes, "Condition", graph.conditionNodes, nodeGuidCounts);
            CaptureNodeList(nodes, "VariableMutation", graph.variableMutationNodes, nodeGuidCounts);
            CaptureNodeList(nodes, "GraphJump", graph.graphJumpNodes, nodeGuidCounts);
            return nodes;
        }

        private static void CaptureNodeList<TNode>(
            List<NodeSnapshot> snapshots,
            string listName,
            List<TNode> nodes,
            Dictionary<string, int> nodeGuidCounts)
            where TNode : BaseNode
        {
            if (nodes == null)
            {
                snapshots.Add(NodeSnapshot.CaptureMissingList(listName));
                return;
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                snapshots.Add(NodeSnapshot.Capture(listName, i, nodes[i], nodeGuidCounts));
            }
        }

        private static List<ChoiceListSnapshot> CaptureChoiceLists(DialogGraph graph)
        {
            var choiceLists = new List<ChoiceListSnapshot>();
            if (graph.choiceNodes == null)
            {
                return choiceLists;
            }

            for (var i = 0; i < graph.choiceNodes.Count; i++)
            {
                choiceLists.Add(ChoiceListSnapshot.Capture(i, graph.choiceNodes[i]));
            }

            return choiceLists;
        }

        private static bool ValidateScalar(bool condition, List<string> errors, string message)
        {
            if (condition)
            {
                return true;
            }

            errors.Add(message);
            return false;
        }

        private static bool IdentityPreservedOrSafelyRepaired(string before, string after, bool mayChange)
        {
            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                return true;
            }

            return mayChange && !string.IsNullOrWhiteSpace(after);
        }

        private static bool CanIdentityChange(string guid, Dictionary<string, int> nodeGuidCounts)
        {
            return string.IsNullOrWhiteSpace(guid) ||
                   (nodeGuidCounts != null &&
                    nodeGuidCounts.TryGetValue(guid, out var count) &&
                    count > 1);
        }

        private static Dictionary<string, int> CountNodeGuidOccurrences(DialogGraph graph)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            AddGuidCount(counts, graph.startGuid);
            AddGuidCount(counts, graph.endGuid);
            CountNodeListGuids(counts, graph.nodes);
            CountNodeListGuids(counts, graph.choiceNodes);
            CountNodeListGuids(counts, graph.actionNodes);
            CountNodeListGuids(counts, graph.conditionNodes);
            CountNodeListGuids(counts, graph.variableMutationNodes);
            CountNodeListGuids(counts, graph.graphJumpNodes);
            return counts;
        }

        private static void CountNodeListGuids<TNode>(Dictionary<string, int> counts, IEnumerable<TNode> nodes)
            where TNode : BaseNode
        {
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                AddGuidCount(counts, node?.GetGuid());
            }
        }

        private static void AddGuidCount(Dictionary<string, int> counts, string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return;
            }

            counts.TryGetValue(guid, out var count);
            counts[guid] = count + 1;
        }

        private bool ValidateLinks(DialogGraph graph, List<string> errors)
        {
            if (graph.links == null)
            {
                if (_links.Count == 0 || _links.All(link => link.IsInvalidOrEmpty))
                {
                    return true;
                }

                errors.Add("Link list changed to null.");
                return false;
            }

            var expectedLinks = _links
                .Where(link => !link.IsInvalidOrEmpty)
                .ToList();

            if (graph.links.Count != expectedLinks.Count)
            {
                errors.Add("Link count changed.");
                return false;
            }

            var valid = true;
            for (var i = 0; i < expectedLinks.Count; i++)
            {
                valid &= expectedLinks[i].Validate(graph.links[i], errors);
            }

            return valid;
        }

        private bool ValidateNodes(DialogGraph graph, List<string> errors)
        {
            var currentSnapshots = CaptureNodes(graph, CountNodeGuidOccurrences(graph));
            if (currentSnapshots.Count != _nodes.Count)
            {
                errors.Add("Node list shape changed.");
                return false;
            }

            var valid = true;
            for (var i = 0; i < _nodes.Count; i++)
            {
                valid &= _nodes[i].Matches(currentSnapshots[i], errors);
            }

            return valid;
        }

        private bool ValidateChoiceLists(DialogGraph graph, List<string> errors)
        {
            var currentSnapshots = CaptureChoiceLists(graph);
            if (currentSnapshots.Count != _choiceLists.Count)
            {
                errors.Add("Choice node list shape changed.");
                return false;
            }

            var valid = true;
            for (var i = 0; i < _choiceLists.Count; i++)
            {
                valid &= _choiceLists[i].Matches(currentSnapshots[i], errors);
            }

            return valid;
        }

        private readonly struct LinkEndpointSnapshot
        {
            public int Index { get; }
            private bool IsNull { get; }
            private string FromGuid { get; }
            private string ToGuid { get; }
            private int FromPortIndex { get; }
            public bool IsInvalidOrEmpty => IsNull || string.IsNullOrWhiteSpace(FromGuid) || string.IsNullOrWhiteSpace(ToGuid);

            private LinkEndpointSnapshot(int index, bool isNull, string fromGuid, string toGuid, int fromPortIndex)
            {
                Index = index;
                IsNull = isNull;
                FromGuid = fromGuid;
                ToGuid = toGuid;
                FromPortIndex = fromPortIndex;
            }

            public static LinkEndpointSnapshot Capture(int index, GraphLink link)
            {
                return link == null
                    ? new LinkEndpointSnapshot(index, true, string.Empty, string.Empty, 0)
                    : new LinkEndpointSnapshot(index, false, link.fromGuid, link.toGuid, link.fromPortIndex);
            }

            public bool Validate(GraphLink link, List<string> errors)
            {
                if (IsNull)
                {
                    if (link == null)
                    {
                        return true;
                    }

                    errors.Add($"Link at index {Index} changed from null.");
                    return false;
                }

                if (link == null)
                {
                    errors.Add($"Link at index {Index} changed to null.");
                    return false;
                }

                if (string.Equals(FromGuid, link.fromGuid, StringComparison.Ordinal) &&
                    string.Equals(ToGuid, link.toGuid, StringComparison.Ordinal) &&
                    FromPortIndex == link.fromPortIndex)
                {
                    return true;
                }

                errors.Add($"Link endpoint triple changed at index {Index}.");
                return false;
            }
        }

        private readonly struct NodeSnapshot
        {
            private string ListName { get; }
            private int Index { get; }
            private bool ListMissing { get; }
            private bool IsNull { get; }
            private string Guid { get; }
            private bool GuidMayChange { get; }
            private Vector2 Position { get; }

            private NodeSnapshot(string listName, int index, bool listMissing, bool isNull, string guid, bool guidMayChange, Vector2 position)
            {
                ListName = listName;
                Index = index;
                ListMissing = listMissing;
                IsNull = isNull;
                Guid = guid;
                GuidMayChange = guidMayChange;
                Position = position;
            }

            public static NodeSnapshot CaptureMissingList(string listName)
            {
                return new NodeSnapshot(listName, -1, true, true, string.Empty, false, default);
            }

            public static NodeSnapshot Capture(
                string listName,
                int index,
                BaseNode node,
                Dictionary<string, int> nodeGuidCounts)
            {
                var guid = node?.GetGuid();
                return node == null
                    ? new NodeSnapshot(listName, index, false, true, string.Empty, false, default)
                    : new NodeSnapshot(listName, index, false, false, guid, CanIdentityChange(guid, nodeGuidCounts), node.GetPosition());
            }

            public bool Matches(NodeSnapshot other, List<string> errors)
            {
                if (ListName != other.ListName || Index != other.Index || ListMissing != other.ListMissing || IsNull != other.IsNull)
                {
                    errors.Add($"Node list shape changed for {ListName} at index {Index}.");
                    return false;
                }

                if (IsNull || ListMissing)
                {
                    return true;
                }

                var guidPreservedOrFilled =
                    string.Equals(Guid, other.Guid, StringComparison.Ordinal) ||
                    (GuidMayChange && !string.IsNullOrWhiteSpace(other.Guid));

                if (guidPreservedOrFilled && Position == other.Position)
                {
                    return true;
                }

                errors.Add($"Node GUID or position changed for {ListName} node at index {Index}.");
                return false;
            }
        }

        private readonly struct ChoiceListSnapshot
        {
            private int NodeIndex { get; }
            private bool IsNull { get; }
            private List<string> NextNodeGuids { get; }

            private ChoiceListSnapshot(int nodeIndex, bool isNull, List<string> nextNodeGuids)
            {
                NodeIndex = nodeIndex;
                IsNull = isNull;
                NextNodeGuids = nextNodeGuids;
            }

            public static ChoiceListSnapshot Capture(int nodeIndex, ChoiceNode choiceNode)
            {
                if (choiceNode == null)
                {
                    return new ChoiceListSnapshot(nodeIndex, true, new List<string>());
                }

                var nextNodeGuids = choiceNode.choices == null
                    ? null
                    : choiceNode.choices.Select(choice => choice?.nextNodeGUID).ToList();

                return new ChoiceListSnapshot(nodeIndex, false, nextNodeGuids);
            }

            public bool Matches(ChoiceListSnapshot other, List<string> errors)
            {
                if (NodeIndex != other.NodeIndex || IsNull != other.IsNull)
                {
                    errors.Add($"Choice node list shape changed at index {NodeIndex}.");
                    return false;
                }

                if (IsNull)
                {
                    return true;
                }

                if (NextNodeGuids == null || other.NextNodeGuids == null)
                {
                    if (NextNodeGuids == null && other.NextNodeGuids == null)
                    {
                        return true;
                    }

                    errors.Add($"Choice list null state changed at node index {NodeIndex}.");
                    return false;
                }

                if (NextNodeGuids.Count != other.NextNodeGuids.Count)
                {
                    errors.Add($"Choice count changed at node index {NodeIndex}.");
                    return false;
                }

                for (var i = 0; i < NextNodeGuids.Count; i++)
                {
                    if (string.Equals(NextNodeGuids[i], other.NextNodeGuids[i], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    errors.Add($"Choice.nextNodeGUID changed at node index {NodeIndex}, choice index {i}.");
                    return false;
                }

                return true;
            }
        }
    }
}
