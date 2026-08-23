using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphMigrationTests
    {
        private const string TempFolder = "Assets/DialogGraphSystem/Tests/GeneratedMigrationAssets";

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

        [Test]
        public void MigrateSchemaZeroSimpleGraphPreservesFlowAndValidates()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var dialog = CreateDialogNode("dialog", "Kira", "Legacy line.");

            try
            {
                graph.SetGraphSchemaVersionForMigration(0);
                graph.nodes.Add(dialog);
                graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "dialog", fromPortIndex = 0 });
                graph.links.Add(new GraphLink { fromGuid = "dialog", toGuid = "End", fromPortIndex = 0 });

                var migration = DialogGraphUpgradeService.MigrateToCurrent(graph);
                var validation = DialogGraphValidator.Validate(graph);

                Assert.That(migration.Success, Is.True);
                Assert.That(graph.IsCurrentSchemaVersion, Is.True);
                Assert.That(graph.nodes.Single().questionText, Is.EqualTo("Legacy line."));
                Assert.That(graph.links.Select(link => link.LinkGuid), Has.All.Not.Empty);
                Assert.That(validation.Issues.Where(issue => issue.Severity == DialogGraphValidationSeverity.Error), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(dialog);
            }
        }

        [Test]
        public void MigrateSchemaZeroChoiceAndActionGraphPreservesContentAndValidates()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var choice = ScriptableObject.CreateInstance<ChoiceNode>();
            var action = ScriptableObject.CreateInstance<ActionNode>();

            try
            {
                graph.SetGraphSchemaVersionForMigration(0);
                choice.SetGuid("choice");
                choice.choices.Add(new Choice { answerText = "Turn it on", nextNodeGUID = "action" });
                action.SetGuid("action");
                action.actionId = "TurnOnTV";
                graph.choiceNodes.Add(choice);
                graph.actionNodes.Add(action);
                graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "choice", fromPortIndex = 0 });
                graph.links.Add(new GraphLink { fromGuid = "choice", toGuid = "action", fromPortIndex = 0 });
                graph.links.Add(new GraphLink { fromGuid = "action", toGuid = "End", fromPortIndex = 0 });

                var migration = DialogGraphUpgradeService.MigrateToCurrent(graph);
                var validation = DialogGraphValidator.Validate(graph);

                Assert.That(migration.Success, Is.True);
                Assert.That(choice.choices[0].choiceId, Is.Not.Empty);
                Assert.That(graph.links[1].fromPortKey, Is.EqualTo(DialogGraphPortKeys.ForChoiceId(choice.choices[0].choiceId)));
                Assert.That(graph.links[2].fromPortKey, Is.EqualTo(DialogGraphPortKeys.ActionSuccess));
                Assert.That(action.actionId, Is.EqualTo("TurnOnTV"));
                Assert.That(validation.Issues.Where(issue => issue.Severity == DialogGraphValidationSeverity.Error), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(choice);
                Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void MigrateSchemaZeroRepairsDuplicateNodeAndLinkIdentities()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var first = CreateDialogNode("duplicate", "Arjan", "First.");
            var second = CreateDialogNode("duplicate", "Kira", "Second.");
            var firstLink = new GraphLink { fromGuid = "Start", toGuid = "duplicate", fromPortIndex = 0 };
            var duplicateLink = new GraphLink { fromGuid = "duplicate", toGuid = "End", fromPortIndex = 0 };
            firstLink.AssignLinkGuidIfMissing("same-link");
            duplicateLink.AssignLinkGuidIfMissing("same-link");

            try
            {
                graph.SetGraphSchemaVersionForMigration(0);
                graph.nodes.Add(first);
                graph.nodes.Add(second);
                graph.links.Add(firstLink);
                graph.links.Add(duplicateLink);
                graph.links.Add(null);
                graph.links.Add(new GraphLink());

                var migration = DialogGraphUpgradeService.MigrateToCurrent(graph);
                var validation = DialogGraphValidator.Validate(graph);

                Assert.That(migration.Success, Is.True);
                Assert.That(migration.DuplicateNodeGuidFixedCount, Is.EqualTo(1));
                Assert.That(migration.DuplicateLinkGuidFixedCount, Is.EqualTo(1));
                Assert.That(migration.InvalidLinkRemovedCount, Is.EqualTo(2));
                Assert.That(first.GetGuid(), Is.Not.EqualTo(second.GetGuid()));
                Assert.That(graph.links.Count, Is.EqualTo(2));
                Assert.That(validation.Issues.Where(issue => issue.Severity == DialogGraphValidationSeverity.Error), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void MigrateToCurrent_PreservesStableGuidsAndLinks()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var node = CreateDialogNode("stable-node", "Arjan", "Stay same.");
            var link = new GraphLink { fromGuid = "Start", toGuid = "stable-node", fromPortIndex = 0 };
            link.AssignLinkGuidIfMissing("stable-link");

            try
            {
                graph.SetGraphSchemaVersionForMigration(0);
                graph.nodes.Add(node);
                graph.links.Add(link);

                var migration = DialogGraphUpgradeService.MigrateToCurrent(graph);

                Assert.That(migration.Success, Is.True);
                Assert.That(node.GetGuid(), Is.EqualTo("stable-node"));
                Assert.That(graph.links[0].LinkGuid, Is.EqualTo("stable-link"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(node);
            }
        }

        [Test]
        public void MigrateToCurrent_PreventsDataLossOnBranchPortIdentity()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var choiceNode = ScriptableObject.CreateInstance<ChoiceNode>();
            choiceNode.SetGuid("choice");
            var choiceId = "my-choice-id";
            choiceNode.choices.Add(new Choice { choiceId = choiceId, answerText = "Go", nextNodeGUID = "End" });

            var link = new GraphLink
            {
                fromGuid = "choice",
                toGuid = "End",
                fromPortKey = DialogGraphPortKeys.ForChoiceId(choiceId),
                fromPortIndex = 0
            };
            link.AssignLinkGuidIfMissing("link-a");

            try
            {
                graph.SetGraphSchemaVersionForMigration(1);
                graph.choiceNodes.Add(choiceNode);
                graph.links.Add(link);

                var migration = DialogGraphUpgradeService.MigrateToCurrent(graph);

                Assert.That(migration.Success, Is.True);
                Assert.That(graph.links[0].fromPortKey, Is.EqualTo(DialogGraphPortKeys.ForChoiceId(choiceId)));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(choiceNode);
            }
        }

        [Test]
        public void MigrateSchemaOne_InitializesCategoriesGroupLayoutsAndJumpReferences()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var graphJumpNode = ScriptableObject.CreateInstance<GraphJumpNode>();

            try
            {
                graph.SetGraphSchemaVersionForMigration(1);
                graph.categories = null;
                graphJumpNode.SetGuid("jump-node");
                graphJumpNode.targetGraph = null;
                graph.graphJumpNodes = new System.Collections.Generic.List<GraphJumpNode> { graphJumpNode };

                var groupLayoutsField = typeof(DialogGraph).GetField("groupLayouts", BindingFlags.NonPublic | BindingFlags.Instance);
                groupLayoutsField.SetValue(graph, null);

                var migration = DialogGraphUpgradeService.MigrateToCurrent(graph);

                Assert.That(migration.Success, Is.True);
                Assert.That(graph.GraphSchemaVersion, Is.EqualTo(DialogGraph.CurrentSchemaVersion));
                Assert.That(graph.categories, Is.Not.Null);
                Assert.That(graph.graphJumpNodes, Is.Not.Null);
                Assert.That(graph.graphJumpNodes, Has.Count.EqualTo(1));
                Assert.That(graph.graphJumpNodes[0].targetGraph, Is.Not.Null);
                Assert.That(graph.EnumerateGroupLayouts(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(graphJumpNode);
            }
        }

        [Test]
        public void UpgradeGraphWithBackup_CreatesBackupBeforeMigratingOriginal()
        {
            EnsureTempFolder();
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var dialog = CreateDialogNode("dialog", "Kira", "Legacy line.");
            var action = ScriptableObject.CreateInstance<ActionNode>();
            var choice = ScriptableObject.CreateInstance<ChoiceNode>();
            var path = $"{TempFolder}/LegacyGraph.asset";

            graph.name = "LegacyGraph";
            graph.SetGraphSchemaVersionForMigration(1);
            graph.AssignGraphGuidIfMissing("legacy-graph-guid");
            graph.nodes.Add(dialog);
            graph.actionNodes.Add(action);
            graph.choiceNodes.Add(choice);
            var link = new GraphLink { fromGuid = "Start", toGuid = "dialog", fromPortIndex = 0 };
            link.AssignLinkGuidIfMissing("legacy-link-guid");
            graph.links.Add(link);
            action.SetGuid("action");
            action.actionId = "TurnOnTV";
            choice.SetGuid("choice");
            choice.choices.Add(new Choice
            {
                choiceId = "legacy-choice",
                answerText = "Turn it on",
                answerTextLocaleKey = "choice.turn_on",
                nextNodeGUID = "action"
            });

            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.AddObjectToAsset(dialog, graph);
            AssetDatabase.AddObjectToAsset(action, graph);
            AssetDatabase.AddObjectToAsset(choice, graph);
            _assetPaths.Add(path);
            AssetDatabase.SaveAssets();

            var result = DialogGraphUpgradeService.UpgradeGraph(graph, createBackup: true);
            var backup = AssetDatabase.LoadAssetAtPath<DialogGraph>(result.BackupPath);

            Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.BackupPath, Is.Not.Empty);
            Assert.That(backup, Is.Not.Null);
            Assert.That(backup.GraphSchemaVersion, Is.EqualTo(1));
            Assert.That(backup.GraphGuid, Is.EqualTo("legacy-graph-guid"));
            Assert.That(backup.nodes.Single().questionText, Is.EqualTo("Legacy line."));
            Assert.That(backup.nodes.Single().GetGuid(), Is.EqualTo("dialog"));
            Assert.That(backup.actionNodes.Single().actionId, Is.EqualTo("TurnOnTV"));
            Assert.That(backup.choiceNodes.Single().choices.Single().answerTextLocaleKey, Is.EqualTo("choice.turn_on"));
            Assert.That(backup.links.Single().LinkGuid, Is.EqualTo("legacy-link-guid"));
            Assert.That(graph.GraphSchemaVersion, Is.EqualTo(DialogGraph.CurrentSchemaVersion));
        }

        [Test]
        public void BackupCreation_GeneratesUniquePathAndDoesNotOverwriteExistingBackup()
        {
            EnsureTempFolder();
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var path = $"{TempFolder}/BackupNameGraph.asset";

            graph.name = "BackupNameGraph";
            graph.SetGraphSchemaVersionForMigration(1);
            AssetDatabase.CreateAsset(graph, path);
            _assetPaths.Add(path);
            AssetDatabase.SaveAssets();

            var preview = DialogGraphUpgradeService.PreviewUpgrade(graph);
            var firstResult = DialogGraphUpgradeResult.FromPreview(preview);
            var secondResult = DialogGraphUpgradeResult.FromPreview(preview);
            var firstPath = CreateBackupViaPrivateApi(graph, firstResult);
            var secondPath = CreateBackupViaPrivateApi(graph, secondResult);

            Assert.That(firstPath, Is.Not.Empty);
            Assert.That(secondPath, Is.Not.Empty);
            Assert.That(secondPath, Is.Not.EqualTo(firstPath));
            Assert.That(AssetDatabase.LoadAssetAtPath<DialogGraph>(firstPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<DialogGraph>(secondPath), Is.Not.Null);
        }

        [Test]
        public void MigrateToCurrent_DoesNotDowngradeNewerSchemaGraph()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();

            try
            {
                graph.SetGraphSchemaVersionForMigration(DialogGraph.CurrentSchemaVersion + 1);

                var migration = DialogGraphUpgradeService.MigrateToCurrent(graph);
                var upgrade = DialogGraphUpgradeService.UpgradeGraph(graph, createBackup: false);

                Assert.That(migration.Success, Is.False);
                Assert.That(migration.Changed, Is.False);
                Assert.That(migration.Errors.Single(), Does.Contain("newer than supported"));
                Assert.That(upgrade.Success, Is.False);
                Assert.That(upgrade.Errors.Single(), Does.Contain("newer than supported"));
                Assert.That(graph.GraphSchemaVersion, Is.EqualTo(DialogGraph.CurrentSchemaVersion + 1));
                Assert.That(graph.HasGraphGuid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void MigrateCurrentSchema_RepairsDuplicateChoiceIdsAndUpdatesMatchingPortKey()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var choiceNode = ScriptableObject.CreateInstance<ChoiceNode>();

            try
            {
                graph.MarkSchemaCurrentForMigration();
                choiceNode.SetGuid("choice");
                choiceNode.choices.Add(new Choice { choiceId = "same-choice", answerText = "A", nextNodeGUID = "End" });
                choiceNode.choices.Add(new Choice { choiceId = "same-choice", answerText = "B", nextNodeGUID = "End" });
                graph.choiceNodes.Add(choiceNode);
                graph.links.Add(new GraphLink
                {
                    fromGuid = "choice",
                    toGuid = "End",
                    fromPortIndex = 1,
                    fromPortKey = DialogGraphPortKeys.ForChoiceId("same-choice"),
                    toPortKey = DialogGraphPortKeys.Default
                });

                var preview = DialogGraphUpgradeService.PreviewUpgrade(graph);
                var migration = DialogGraphUpgradeService.MigrateToCurrent(graph);

                Assert.That(preview.IsUpgradeNeeded, Is.True);
                Assert.That(preview.DuplicateChoiceIdCountBefore, Is.EqualTo(1));
                Assert.That(migration.Success, Is.True);
                Assert.That(migration.DuplicateChoiceIdFixedCount, Is.EqualTo(1));
                Assert.That(choiceNode.choices.Select(choice => choice.choiceId).Distinct().Count(), Is.EqualTo(2));
                Assert.That(graph.links.Single().fromPortKey, Is.EqualTo(DialogGraphPortKeys.ForChoiceId(choiceNode.choices[1].choiceId)));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(choiceNode);
            }
        }

        [Test]
        public void LoadNewerSchema_LogsWarningAndDoesNotMutate()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            try
            {
                // We use reflection to set private field since it's normally serialized
                var field = typeof(DialogGraph).GetField("graphSchemaVersion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field.SetValue(graph, DialogGraph.CurrentSchemaVersion + 1);

                // ScriptableObject.CreateInstance calls OnEnable during creation, which may set
                // newerSchemaWarningLogged to true. Reset it so the second OnEnable logs the warning.
                var warningFlagField = typeof(DialogGraph).GetField("newerSchemaWarningLogged",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                warningFlagField?.SetValue(graph, false);

                // Manually trigger OnEnable logic via reflection
                var method = typeof(DialogGraph).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Warning,
                    new Regex(@"^Graph '.*' was created with schema version \d+, but this Dialogue Graph System version supports schema \d+\. Some features may not be available\. The graph was not downgraded or modified\.$"));
                method.Invoke(graph, null);

                Assert.That(graph.GraphSchemaVersion, Is.EqualTo(DialogGraph.CurrentSchemaVersion + 1));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        private static string CreateBackupViaPrivateApi(DialogGraph graph, DialogGraphUpgradeResult result)
        {
            var method = typeof(DialogGraphUpgradeService).GetMethod(
                "TryCreateBackup",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            var args = new object[] { graph, result, string.Empty };
            var created = (bool)method.Invoke(null, args);

            Assert.That(created, Is.True, string.Join("\n", result.Errors));
            return (string)args[2];
        }

        private static void EnsureTempFolder()
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

        private static DialogNode CreateDialogNode(string guid, string speaker, string text)
        {
            var node = ScriptableObject.CreateInstance<DialogNode>();
            node.SetGuid(guid);
            node.speakerName = speaker;
            node.questionText = text;
            return node;
        }
    }
}
