using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    /// <summary>
    /// Edit-mode tests for the safe JSON import transaction infrastructure.
    /// These tests require the Unity Editor and AssetDatabase.
    /// Run from Unity: Window -> General -> Test Runner -> EditMode -> DialogGraphImportTransactionTests.
    /// </summary>
    public sealed class DialogGraphImportTransactionTests
    {
        private const string TempFolder = "Assets/DialogGraphSystem/Tests/GeneratedImportAssets";

        private readonly List<string> _assetPaths = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _assetPaths)
            {
                AssetDatabase.DeleteAsset(path);
            }

            _assetPaths.Clear();

            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }

            AssetDatabase.SaveAssets();
        }

        #region ---------------- Snapshot Tests ----------------

        [Test]
        public void SnapshotCaptureReturnsNullForInvalidPath()
        {
            var snapshot = DialogGraphImportSnapshot.Capture("Assets/Nonexistent/Graph.asset");
            Assert.That(snapshot, Is.Null, "Snapshot should be null for non-existent path.");
        }

        [Test]
        public void SnapshotCaptureReturnsNullForEmptyPath()
        {
            var snapshot = DialogGraphImportSnapshot.Capture(null);
            Assert.That(snapshot, Is.Null);

            snapshot = DialogGraphImportSnapshot.Capture(string.Empty);
            Assert.That(snapshot, Is.Null);

            snapshot = DialogGraphImportSnapshot.Capture("   ");
            Assert.That(snapshot, Is.Null);
        }

        [Test]
        public void SnapshotCaptureCapturesGraphIdentity()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("SnapshotIdentityGraph", out var graph, out _);

            var snapshot = DialogGraphImportSnapshot.Capture(path);
            Assert.That(snapshot, Is.Not.Null);

            var snap = snapshot.Value;
            Assert.That(snap.AssetPath, Is.EqualTo(path));
            Assert.That(snap.MainAssetGuid, Is.Not.Empty, "Main asset GUID should be populated.");
            Assert.That(snap.GraphId, Is.Not.Empty, "Graph ID should be populated.");
            Assert.That(snap.NodeCount, Is.GreaterThan(0), "Should count at least start and end nodes.");
            Assert.That(snap.LinkCount, Is.EqualTo(graph.links?.Count ?? 0));
        }

        [Test]
        public void SnapshotMatchesIdenticalGraph()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("SnapshotMatchGraph", out _, out _);

            var snap1 = DialogGraphImportSnapshot.Capture(path);
            var snap2 = DialogGraphImportSnapshot.Capture(path);

            Assert.That(snap1, Is.Not.Null);
            Assert.That(snap2, Is.Not.Null);

            Assert.That(snap1.Value.Matches(snap2, out var diff), Is.True, diff);
        }

        [Test]
        public void SnapshotDetectsNodeCountChange()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("SnapshotDeltaGraph", out var graph, out _);

            var snap1 = DialogGraphImportSnapshot.Capture(path);
            Assert.That(snap1, Is.Not.Null);

            // Mutate the graph
            var extraNode = CreateDialogNode("extra-node", "Test", "Extra line.");
            graph.nodes.Add(extraNode);

            var snap2 = DialogGraphImportSnapshot.Capture(path);
            Assert.That(snap2, Is.Not.Null);

            Assert.That(snap1.Value.Matches(snap2, out var diff), Is.False,
                "Snapshots should differ when node count changes.");
            Assert.That(diff, Does.Contain("NodeCount"));
        }

        [Test]
        public void SnapshotDetectsGraphIdChange()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("SnapshotGuidGraph", out var graph, out _);

            var snap1 = DialogGraphImportSnapshot.Capture(path);

            // Change graph GUID through the migration setter so the snapshot diff is deterministic.
            graph.SetGraphGuidForMigration(System.Guid.NewGuid().ToString("N"));
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();

            var snap2 = DialogGraphImportSnapshot.Capture(path);

            Assert.That(snap1, Is.Not.Null);
            Assert.That(snap2, Is.Not.Null);
            Assert.That(snap1.Value.Matches(snap2, out var diff), Is.False,
                "Snapshots should differ when graph ID changes.");
            Assert.That(diff, Does.Contain("GraphId"));
        }

        #endregion

        #region ---------------- Backup Service Tests ----------------

        [Test]
        public void BackupServiceCreatesBackup()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("BackupGraph", out _, out _);

            var backupPath = DialogGraphImportBackupService.CreateBackup(path);
            Assert.That(backupPath, Is.Not.Null, "Backup path should not be null.");
            Assert.That(backupPath, Does.Contain("Backups"),
                "Backup should be in a Backups subdirectory.");
            Assert.That(backupPath, Does.EndWith(".asset"),
                "Backup should be a .asset file.");

            // Verify backup exists
            var backupAsset = AssetDatabase.LoadAssetAtPath<DialogGraph>(backupPath);
            Assert.That(backupAsset, Is.Not.Null, "Backup asset should be loadable.");

            // Cleanup backup
            _assetPaths.Add(backupPath);
            var backupDir = DialogGraphImportBackupService.GetBackupDirectory(path);
            if (AssetDatabase.IsValidFolder(backupDir))
            {
                _assetPaths.Add(backupDir);
            }
        }

        [Test]
        public void BackupServiceVerifyReturnsTrueForValidBackup()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("VerifyBackupGraph", out _, out _);

            var backupPath = DialogGraphImportBackupService.CreateBackup(path);
            Assert.That(backupPath, Is.Not.Null);

            var verified = DialogGraphImportBackupService.VerifyBackup(backupPath, path);
            Assert.That(verified, Is.True, "Backup verification should succeed.");

            // Cleanup
            _assetPaths.Add(backupPath);
            var backupDir = DialogGraphImportBackupService.GetBackupDirectory(path);
            if (AssetDatabase.IsValidFolder(backupDir))
            {
                _assetPaths.Add(backupDir);
            }
        }

        [Test]
        public void BackupServiceVerifyReturnsFalseForInvalidPath()
        {
            var result = DialogGraphImportBackupService.VerifyBackup("Assets/Nonexistent/backup.asset", "Assets/Nonexistent/original.asset");
            Assert.That(result, Is.False);
        }

        #endregion

        #region ---------------- Transaction Service Tests (New Import) ----------------

        [Test]
        public void ImportNewCreatesGraphFromValidJson()
        {
            EnsureTempFolder();
            var json = BuildMinimalJson();

            var result = DialogGraphImportTransactionService.ImportNew(TempFolder, "ImportedNewGraph", json);
            Assert.That(result.Success, Is.True, result.Message);

            var graph = AssetDatabase.LoadAssetAtPath<DialogGraph>(result.TargetPath);
            Assert.That(graph, Is.Not.Null, "Graph should be loadable.");
            Assert.That(graph.nodes, Is.Not.Null);
            Assert.That(graph.nodes.Count, Is.GreaterThan(0), "Should have at least one dialog node.");

            _assetPaths.Add(result.TargetPath);
        }

        [Test]
        public void ImportNewFailsWithEmptyJson()
        {
            var result = DialogGraphImportTransactionService.ImportNew(TempFolder, "EmptyGraph", string.Empty);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo("Parse"));
        }

        [Test]
        public void ImportNewFailsWithInvalidJson()
        {
            var result = DialogGraphImportTransactionService.ImportNew(TempFolder, "BadGraph", "not valid json {{{");
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo("Parse"));
        }

        [Test]
        public void ImportNewFailsWithEmptyDto()
        {
            var emptyJson = "{\"graphGuid\":\"test\",\"schemaVersion\":2,\"graphTitle\":\"Empty\"}";
            var result = DialogGraphImportTransactionService.ImportNew(TempFolder, "EmptyDtoGraph", emptyJson);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo("Validate"));
        }

        #endregion

        #region ---------------- Transaction Service Tests (Overwrite) ----------------

        [Test]
        public void ImportOverwritePreservesOriginalOnJsonParseFailure()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("OverwriteParseFailGraph", out var originalGraph, out _);

            var snapshot = DialogGraphImportSnapshot.Capture(path);
            Assert.That(snapshot, Is.Not.Null);

            var result = DialogGraphImportTransactionService.ImportOverwrite(path, "invalid json {{{");
            Assert.That(result.Success, Is.False, "Import should fail on bad JSON.");
            Assert.That(result.FailureStage, Is.EqualTo("Parse"));

            var postSnapshot = DialogGraphImportSnapshot.Capture(path);
            Assert.That(postSnapshot, Is.Not.Null);
            Assert.That(snapshot.Value.Matches(postSnapshot, out var diff), Is.True,
                $"Original graph should be unchanged after parse failure. Diff: {diff}");
        }

        [Test]
        public void ImportOverwriteCreatesBackupBeforeMutation()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("OverwriteBackupGraph", out _, out _);

            var json = BuildMinimalJson();
            var result = DialogGraphImportTransactionService.ImportOverwrite(path, json);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.BackupPath, Is.Not.Null, "Backup should be created before overwrite.");
            Assert.That(result.BackupPath, Does.Contain("Backups"));

            var backupExists = AssetDatabase.LoadAssetAtPath<DialogGraph>(result.BackupPath) != null;
            Assert.That(backupExists, Is.True, "Backup asset should exist after successful overwrite.");

            // Cleanup backup
            _assetPaths.Add(result.BackupPath);
            var backupDir = DialogGraphImportBackupService.GetBackupDirectory(path);
            if (AssetDatabase.IsValidFolder(backupDir))
            {
                _assetPaths.Add(backupDir);
            }
        }

        [Test]
        public void ImportOverwriteUpdatesGraphContent()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("OverwriteUpdateGraph", out var originalGraph, out var originalNode);

            var json = BuildMinimalJson(graphTitle: "Updated Title");
            var result = DialogGraphImportTransactionService.ImportOverwrite(path, json);

            Assert.That(result.Success, Is.True, result.Message);

            var updatedGraph = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            Assert.That(updatedGraph, Is.Not.Null);
            Assert.That(updatedGraph.graphTitle, Is.EqualTo("Updated Title"),
                "Graph title should be updated after overwrite.");
        }

        [Test]
        public void ImportOverwriteSnapshotIsCaptured()
        {
            EnsureTempFolder();
            var path = CreateSimpleGraph("OverwriteSnapshotGraph", out _, out _);

            var json = BuildMinimalJson();
            var result = DialogGraphImportTransactionService.ImportOverwrite(path, json);

            Assert.That(result.Success, Is.True);
            Assert.That(result.OriginalSnapshot, Is.Not.Null,
                "Original snapshot should be captured before overwrite.");
            Assert.That(result.OriginalSnapshot.Value.AssetPath, Is.EqualTo(path));
        }

        [Test]
        public void ImportOverwriteFailureWithInvalidTargetPath()
        {
            var result = DialogGraphImportTransactionService.ImportOverwrite(
                "Assets/Nonexistent/Graph.asset", BuildMinimalJson());

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo("Validate"));
        }

        #endregion

        #region ---------------- Helpers ----------------

        private void EnsureTempFolder()
        {
            var parts = TempFolder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private string CreateSimpleGraph(string graphName, out DialogGraph graph, out DialogNode dialogNode)
        {
            graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.name = graphName;

            dialogNode = CreateDialogNode("node-1", "Test Speaker", "Hello world.");
            graph.nodes.Add(dialogNode);

            graph.links.Add(new GraphLink
            {
                fromGuid = "Start",
                toGuid = "node-1",
                fromPortIndex = 0
            });

            var path = $"{TempFolder}/{graphName}.asset";
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.AddObjectToAsset(dialogNode, graph);
            _assetPaths.Add(path);

            // Run migration to initialize defaults
            DialogGraphUpgradeService.MigrateToCurrent(graph);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();

            return path;
        }

        private static DialogNode CreateDialogNode(string guid, string speaker, string text)
        {
            var node = ScriptableObject.CreateInstance<DialogNode>();
            node.SetGuid(guid);
            node.speakerName = speaker;
            node.questionText = text;
            return node;
        }

        private static string BuildMinimalJson(string graphTitle = "Test Graph")
        {
            var dto = new DialogGraphExport
            {
                graphGuid = System.Guid.NewGuid().ToString("N"),
                schemaVersion = 2,
                graphTitle = graphTitle,
                startNode = new ExportStartNode
                {
                    guid = "start-guid",
                    nodePositionX = 0,
                    nodePositionY = 0,
                    isInitialized = true
                },
                endNode = new ExportEndNode
                {
                    guid = "end-guid",
                    nodePositionX = 400,
                    nodePositionY = 0,
                    isInitialized = true
                },
                dialogNodes = new List<DialogExportDialogNode>
                {
                    new DialogExportDialogNode
                    {
                        title = "Imported Node",
                        guid = "imported-node",
                        speaker = "Test Speaker",
                        question = "Imported dialogue text.",
                        nodePositionX = 200,
                        nodePositionY = 0,
                        displayTime = 3f
                    }
                },
                links = new List<ExportLink>
                {
                    new ExportLink
                    {
                        fromGuid = "start-guid",
                        toGuid = "imported-node",
                        fromPortIndex = 0
                    },
                    new ExportLink
                    {
                        fromGuid = "imported-node",
                        toGuid = "end-guid",
                        fromPortIndex = 0
                    }
                }
            };

            return JsonUtility.ToJson(dto);
        }

        #endregion
    }
}
