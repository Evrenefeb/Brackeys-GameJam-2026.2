using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphRecoveryTests
    {
        private const string TempFolder = "Assets/DialogGraphSystem/Tests/GeneratedRecoveryAssets";

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
        public void PrepareSaveThenReloadPreservesMigratedGraphIdentityAndLinks()
        {
            EnsureTempFolder();
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var node = CreateDialogNode("node-a", "Kira", "Recovered line.");
            var path = $"{TempFolder}/RecoveryGraph.asset";
            var legacyLink = new GraphLink
            {
                fromGuid = "Start",
                toGuid = node.GetGuid(),
                fromPortIndex = 0
            };

            graph.name = "RecoveryGraph";
            graph.nodes.Add(node);
            graph.links.Add(legacyLink);

            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.AddObjectToAsset(node, graph);
            _assetPaths.Add(path);

            var persistence = new DialogGraphPersistenceService();
            var preparation = persistence.PrepareForSave(graph);
            persistence.SaveAssetIfChanged(graph, preparation.Changed);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var loaded = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);

            Assert.That(preparation.Errors, Is.Empty);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.HasGraphGuid, Is.True);
            Assert.That(loaded.IsCurrentSchemaVersion, Is.True);
            Assert.That(loaded.startGuid, Is.Not.Empty);
            Assert.That(loaded.endGuid, Is.Not.Empty);
            Assert.That(loaded.nodes.Single().GetGuid(), Is.EqualTo("node-a"));
            Assert.That(loaded.links.Single().LinkGuid, Is.Not.Empty);
            Assert.That(loaded.links.Single().fromPortKey, Is.EqualTo(DialogGraphPortKeys.Default));
            Assert.That(loaded.links.Single().toPortKey, Is.EqualTo(DialogGraphPortKeys.Default));
        }

        [Test]
        public void MigrateToCurrentInitializesDefaultsAndIsIdempotent()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var choiceNode = ScriptableObject.CreateInstance<ChoiceNode>();

            try
            {
                choiceNode.SetGuid("choice-node");
                choiceNode.choices.Add(new Choice { answerText = "Go left", nextNodeGUID = "End" });
                graph.choiceNodes.Add(choiceNode);
                graph.links.Add(new GraphLink
                {
                    fromGuid = "Start",
                    toGuid = "choice-node",
                    fromPortIndex = 0
                });

                var first = DialogGraphUpgradeService.MigrateToCurrent(graph);
                var graphGuid = graph.GraphGuid;
                var startGuid = graph.startGuid;
                var endGuid = graph.endGuid;
                var choiceId = choiceNode.choices[0].choiceId;
                var linkGuid = graph.links[0].LinkGuid;

                var second = DialogGraphUpgradeService.MigrateToCurrent(graph);

                Assert.That(first.Success, Is.True);
                Assert.That(first.Changed, Is.True);
                Assert.That(graphGuid, Is.Not.Empty);
                Assert.That(startGuid, Is.Not.Empty);
                Assert.That(endGuid, Is.Not.Empty);
                Assert.That(choiceId, Is.Not.Empty);
                Assert.That(linkGuid, Is.Not.Empty);
                Assert.That(graph.links[0].fromPortKey, Is.EqualTo(DialogGraphPortKeys.Default));
                Assert.That(graph.links[0].toPortKey, Is.EqualTo(DialogGraphPortKeys.Default));
                Assert.That(graph.IsCurrentSchemaVersion, Is.True);

                Assert.That(second.Success, Is.True);
                Assert.That(second.Changed, Is.False);
                Assert.That(graph.GraphGuid, Is.EqualTo(graphGuid));
                Assert.That(graph.startGuid, Is.EqualTo(startGuid));
                Assert.That(graph.endGuid, Is.EqualTo(endGuid));
                Assert.That(choiceNode.choices[0].choiceId, Is.EqualTo(choiceId));
                Assert.That(graph.links[0].LinkGuid, Is.EqualTo(linkGuid));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(choiceNode);
            }
        }

        [Test]
        public void ValidatorReportsBrokenLinksAndOrphansAfterStaleEdgeLayoutCleanup()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var reachable = CreateDialogNode("reachable", "Kira", "This node is linked.");
            var orphan = CreateDialogNode("orphan", "Arjan", "This node is floating.");

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;
                graph.nodes.Add(reachable);
                graph.nodes.Add(orphan);

                var validLink = CreateLink("valid-link", "Start", reachable.GetGuid());
                var brokenLink = CreateLink("broken-link", reachable.GetGuid(), "missing-node");
                graph.links.Add(validLink);
                graph.links.Add(brokenLink);
                graph.GetOrCreateEdgeLayoutForEditor("valid-link", "Start", reachable.GetGuid(), 0);
                graph.GetOrCreateEdgeLayoutForEditor("stale-link", "missing-source", "missing-target", 0);

                var removed = graph.RemoveEdgeLayoutsForMissingLinks();
                var result = DialogGraphValidator.Validate(graph);
                var codes = result.Issues.Select(issue => issue.Code).ToArray();

                Assert.That(removed, Is.EqualTo(1));
                Assert.That(graph.HasEdgeLayout("valid-link"), Is.True);
                Assert.That(graph.HasEdgeLayout("stale-link"), Is.False);
                Assert.That(codes, Does.Contain("UNKNOWN_TO_GUID"));
                Assert.That(codes, Does.Contain("ORPHAN_NODE"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(reachable);
                Object.DestroyImmediate(orphan);
            }
        }

        [Test]
        public void ValidatorReportsMissingRegistryIdsAndAcceptsRegisteredIds()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var dialogNode = CreateDialogNode("dialog", "Kira", "Registered speaker.");
            var actionNode = ScriptableObject.CreateInstance<ActionNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                actionNode.SetGuid("action");
                actionNode.actionId = "TurnOnTV";
                graph.nodes.Add(dialogNode);
                graph.actionNodes.Add(actionNode);
                graph.links.Add(CreateLink("start-link", "Start", dialogNode.GetGuid()));
                graph.links.Add(CreateLink("dialog-link", dialogNode.GetGuid(), actionNode.GetGuid()));
                graph.links.Add(CreateLink("action-link", actionNode.GetGuid(), "End", DialogGraphPortKeys.ActionSuccess));

                var missingResult = DialogGraphValidator.Validate(
                    graph,
                    knownSpeakerIds: new[] { "Arjan" },
                    knownActionIds: new[] { "TurnOffTV" });
                var registeredResult = DialogGraphValidator.Validate(
                    graph,
                    knownSpeakerIds: new[] { "Kira" },
                    knownActionIds: new[] { "TurnOnTV" });

                Assert.That(missingResult.Issues.Select(issue => issue.Code), Does.Contain("UNKNOWN_SPEAKER_ID"));
                Assert.That(missingResult.Issues.Select(issue => issue.Code), Does.Contain("UNKNOWN_ACTION_ID"));
                Assert.That(registeredResult.Issues.Select(issue => issue.Code), Does.Not.Contain("UNKNOWN_SPEAKER_ID"));
                Assert.That(registeredResult.Issues.Select(issue => issue.Code), Does.Not.Contain("UNKNOWN_ACTION_ID"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(dialogNode);
                Object.DestroyImmediate(actionNode);
            }
        }

        [Test]
        public void RegistryServicesReportDuplicateDefinitionIdsAndDefinitionsRejectMissingIds()
        {
            EnsureTempFolder();
            var firstCharacter = CreateCharacterAsset($"{TempFolder}/KiraA.asset", "kira", "Kira");
            var secondCharacter = CreateCharacterAsset($"{TempFolder}/KiraB.asset", " KIRA ", "Kira Copy");
            var missingCharacter = CreateCharacterAsset($"{TempFolder}/MissingCharacter.asset", "", "No ID");
            var firstAction = CreateActionAsset($"{TempFolder}/TurnOnTVA.asset", "TurnOnTV");
            var secondAction = CreateActionAsset($"{TempFolder}/TurnOnTVB.asset", " turnontv ");
            var missingAction = CreateActionAsset($"{TempFolder}/MissingAction.asset", "");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Assert.That(new DialogCharacterRegistryService().GetDuplicateIds(), Does.Contain("kira"));
            Assert.That(new DialogActionRegistryService().GetDuplicateIds(), Does.Contain("TurnOnTV"));
            Assert.That(firstCharacter.IsValid(out _), Is.True);
            Assert.That(secondCharacter.IsValid(out _), Is.True);
            Assert.That(firstAction.IsValid(out _), Is.True);
            Assert.That(secondAction.IsValid(out _), Is.True);
            Assert.That(missingCharacter.IsValid(out var characterError), Is.False);
            Assert.That(characterError, Does.Contain("empty CharacterID"));
            Assert.That(missingAction.IsValid(out var actionError), Is.False);
            Assert.That(actionError, Does.Contain("empty ActionID"));
        }

        private static DialogNode CreateDialogNode(string guid, string speaker, string text)
        {
            var node = ScriptableObject.CreateInstance<DialogNode>();
            node.SetGuid(guid);
            node.speakerName = speaker;
            node.questionText = text;
            return node;
        }

        private static GraphLink CreateLink(
            string linkGuid,
            string fromGuid,
            string toGuid,
            string fromPortKey = DialogGraphPortKeys.Default)
        {
            var link = new GraphLink
            {
                fromGuid = fromGuid,
                toGuid = toGuid,
                fromPortKey = fromPortKey,
                toPortKey = DialogGraphPortKeys.Default,
                fromPortIndex = 0
            };
            link.AssignLinkGuidIfMissing(linkGuid);
            return link;
        }

        private DialogCharacterSO CreateCharacterAsset(string path, string characterId, string displayName)
        {
            var asset = ScriptableObject.CreateInstance<DialogCharacterSO>();
            asset.CharacterID = characterId;
            asset.DisplayName = displayName;
            AssetDatabase.CreateAsset(asset, path);
            _assetPaths.Add(path);
            return asset;
        }

        private DialogActionSO CreateActionAsset(string path, string actionId)
        {
            var asset = ScriptableObject.CreateInstance<DialogActionSO>();
            asset.ActionID = actionId;
            AssetDatabase.CreateAsset(asset, path);
            _assetPaths.Add(path);
            return asset;
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
    }
}
