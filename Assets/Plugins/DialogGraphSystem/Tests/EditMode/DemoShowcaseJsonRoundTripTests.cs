using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Models;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DemoShowcaseJsonRoundTripTests
    {
        private const string JsonRoot = "Assets/DialogGraphSystem/Resources/JSON";
        private const string ImportRoot = "Assets/DialogGraphSystem/Tests/GeneratedShowcaseRoundTrip";

        private static readonly IReadOnlyDictionary<string, string> GraphToJson =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DemoShowcaseGraphSpecs.ProductPath] = JsonRoot + "/Demo_ProductTour.json",
                [DemoShowcaseGraphSpecs.ShopPath] = JsonRoot + "/Demo_ShopGate.json",
                [DemoShowcaseGraphSpecs.ReactorPath] = JsonRoot + "/Demo_ControlRoomActions.json",
                [DemoShowcaseGraphSpecs.AftermathPath] = JsonRoot + "/Demo_ReactorAftermath.json"
            };

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(ImportRoot))
            {
                AssetDatabase.DeleteAsset(ImportRoot);
                AssetDatabase.SaveAssets();
            }
        }

        [Test]
        public void JsonCatalog_ContainsExactlyTheFourShowcaseExports()
        {
            var actual = Directory.GetFiles(JsonRoot, "*.json", SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(actual, Is.EqualTo(GraphToJson.Values.OrderBy(path => path, StringComparer.Ordinal)));
        }

        [Test]
        public void JsonExports_MatchTheAuthoredGraphStructureAndStableIdentity()
        {
            foreach (var pair in GraphToJson)
            {
                var graph = AssetDatabase.LoadAssetAtPath<DialogGraph>(pair.Key);
                var dto = JsonUtility.FromJson<DialogGraphExport>(File.ReadAllText(pair.Value));
                var authored = DialogGraphJsonSerializationUtility.BuildExportDto(
                    graph,
                    DialogGraphJsonExportOptions.Default);

                AssertDtoEquivalent(authored, dto, pair.Value);
            }
        }

        [Test]
        public void JsonExports_ImportThroughTransactionAndPreserveStructure()
        {
            EnsureFolder(ImportRoot);
            foreach (var pair in GraphToJson)
            {
                var source = AssetDatabase.LoadAssetAtPath<DialogGraph>(pair.Key);
                var json = File.ReadAllText(pair.Value);
                var assetName = Path.GetFileNameWithoutExtension(pair.Value) + "_RoundTrip";
                var result = DialogGraphImportTransactionService.ImportNew(ImportRoot, assetName, json);

                Assert.That(result.Success, Is.True, pair.Value + ": " + result.Message);
                var imported = AssetDatabase.LoadAssetAtPath<DialogGraph>(result.TargetPath);
                Assert.That(imported, Is.Not.Null, result.TargetPath);
                AssertGraphEquivalent(source, imported, pair.Value);
            }
        }

        private static void AssertDtoEquivalent(DialogGraphExport expected, DialogGraphExport actual, string context)
        {
            Assert.That(actual, Is.Not.Null, context);
            Assert.That(actual.graphGuid, Is.EqualTo(expected.graphGuid), context);
            Assert.That(actual.schemaVersion, Is.EqualTo(expected.schemaVersion), context);
            Assert.That(actual.graphTitle, Is.EqualTo(expected.graphTitle), context);
            Assert.That(actual.description, Is.EqualTo(expected.description), context);
            Assert.That(actual.author, Is.EqualTo(expected.author), context);
            Assert.That(actual.tags, Is.EqualTo(expected.tags), context);
            Assert.That(actual.primaryCategory, Is.EqualTo(expected.primaryCategory), context);
            Assert.That(actual.categories, Is.EqualTo(expected.categories), context);
            Assert.That(actual.sceneGoal, Is.EqualTo(expected.sceneGoal), context);
            Assert.That(actual.tone, Is.EqualTo(expected.tone), context);
            Assert.That(actual.extraRules, Is.EqualTo(expected.extraRules), context);
            Assert.That(actual.startNode.guid, Is.EqualTo(expected.startNode.guid), context);
            Assert.That(actual.endNode.guid, Is.EqualTo(expected.endNode.guid), context);
            Assert.That(actual.dialogNodes.Select(node => node.guid), Is.EqualTo(expected.dialogNodes.Select(node => node.guid)), context);
            Assert.That(actual.choiceNodes.Select(node => node.guid), Is.EqualTo(expected.choiceNodes.Select(node => node.guid)), context);
            Assert.That(actual.actionNodes.Select(node => node.guid), Is.EqualTo(expected.actionNodes.Select(node => node.guid)), context);
            Assert.That(actual.conditionNodes.Select(node => node.guid), Is.EqualTo(expected.conditionNodes.Select(node => node.guid)), context);
            Assert.That(actual.variableMutationNodes.Select(node => node.guid), Is.EqualTo(expected.variableMutationNodes.Select(node => node.guid)), context);
            Assert.That(actual.graphJumpNodes.Select(node => node.guid), Is.EqualTo(expected.graphJumpNodes.Select(node => node.guid)), context);
            Assert.That(actual.outcomeNodes.Select(node => node.guid), Is.EqualTo(expected.outcomeNodes.Select(node => node.guid)), context);
            Assert.That(actual.links.Select(LinkIdentity), Is.EqualTo(expected.links.Select(LinkIdentity)), context);

            var expectedChoices = expected.choiceNodes.SelectMany(node => node.choices).Select(ChoiceIdentity);
            var actualChoices = actual.choiceNodes.SelectMany(node => node.choices).Select(ChoiceIdentity);
            Assert.That(actualChoices, Is.EqualTo(expectedChoices), context);

            var expectedJumps = expected.graphJumpNodes.Select(node => JumpIdentity(node.targetGraph));
            var actualJumps = actual.graphJumpNodes.Select(node => JumpIdentity(node.targetGraph));
            Assert.That(actualJumps, Is.EqualTo(expectedJumps), context);
        }

        private static void AssertGraphEquivalent(DialogGraph expected, DialogGraph actual, string context)
        {
            Assert.That(actual.GraphGuid, Is.EqualTo(expected.GraphGuid), context);
            Assert.That(actual.GraphSchemaVersion, Is.EqualTo(expected.GraphSchemaVersion), context);
            Assert.That(actual.graphTitle, Is.EqualTo(expected.graphTitle), context);
            Assert.That(actual.EnumerateAllNodeGuids(), Is.EquivalentTo(expected.EnumerateAllNodeGuids()), context);
            Assert.That(actual.links.Select(link => LinkIdentity(
                    new ExportLink
                    {
                        linkGuid = link.LinkGuid,
                        fromGuid = link.fromGuid,
                        toGuid = link.toGuid,
                        fromPortKey = link.fromPortKey,
                        toPortKey = link.toPortKey,
                        fromPortIndex = link.fromPortIndex
                    })),
                Is.EqualTo(expected.links.Select(link => LinkIdentity(
                    new ExportLink
                    {
                        linkGuid = link.LinkGuid,
                        fromGuid = link.fromGuid,
                        toGuid = link.toGuid,
                        fromPortKey = link.fromPortKey,
                        toPortKey = link.toPortKey,
                        fromPortIndex = link.fromPortIndex
                    }))), context);
            Assert.That(actual.actionNodes.Select(node => node.actionId), Is.EqualTo(expected.actionNodes.Select(node => node.actionId)), context);
            Assert.That(actual.conditionNodes.Select(node => node.variableName), Is.EqualTo(expected.conditionNodes.Select(node => node.variableName)), context);
            Assert.That(actual.variableMutationNodes.Select(node => node.variableName), Is.EqualTo(expected.variableMutationNodes.Select(node => node.variableName)), context);
            Assert.That(actual.outcomeNodes.Select(node => node.outcomeId), Is.EqualTo(expected.outcomeNodes.Select(node => node.outcomeId)), context);
            Assert.That(actual.choiceNodes.SelectMany(node => node.choices).Select(choice => choice.choiceId),
                Is.EqualTo(expected.choiceNodes.SelectMany(node => node.choices).Select(choice => choice.choiceId)), context);
            Assert.That(actual.graphJumpNodes.Select(node => JumpIdentity(node.targetGraph)),
                Is.EqualTo(expected.graphJumpNodes.Select(node => JumpIdentity(node.targetGraph))), context);
        }

        private static string LinkIdentity(ExportLink link)
        {
            return string.Join("|", link.linkGuid, link.fromGuid, link.fromPortKey, link.fromPortIndex, link.toGuid, link.toPortKey);
        }

        private static string ChoiceIdentity(ExportChoice choice)
        {
            return string.Join("|", choice.choiceId, choice.portKey, choice.answerText, choice.nextNodeGUID);
        }

        private static string JumpIdentity(DialogExportGraphReference reference)
        {
            if (reference == null)
                return string.Empty;
            return string.Join("|", reference.graphGuid, reference.runtimeDialogId, reference.graphName, reference.assetPath, reference.entryGuid);
        }

        private static string JumpIdentity(GraphReference reference)
        {
            if (reference == null)
                return string.Empty;
            return string.Join("|", reference.graphGuid, reference.runtimeDialogId, reference.graphName, reference.assetPath, reference.entryGuid);
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
