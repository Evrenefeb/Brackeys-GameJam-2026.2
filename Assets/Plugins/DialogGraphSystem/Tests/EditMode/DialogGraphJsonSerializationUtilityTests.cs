using System.Collections.Generic;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphJsonSerializationUtilityTests
    {
        [Test]
        public void BuildExportDto_WhenOptionalFlagsDisabled_OmitsOptionalMetadata()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var jumpNode = ScriptableObject.CreateInstance<GraphJumpNode>();

            try
            {
                graph.graphTitle = "Export Me";
                graph.primaryCategory = "Story/Intro";
                graph.categories = new List<string> { "Story/Intro", "Characters/Guide" };

                jumpNode.SetGuid("jump-node");
                jumpNode.targetGraph.graphGuid = "target-graph";
                jumpNode.targetGraph.graphName = "Target";
                jumpNode.targetGraph.entryGuid = "entry-guid";
                graph.graphJumpNodes.Add(jumpNode);

                var groupLayout = graph.GetOrCreateGroupLayoutForEditor("group-1", "Act 1");
                groupLayout.category = "Acts";
                groupLayout.nodeGuids.Add("jump-node");

                var dto = DialogGraphJsonSerializationUtility.BuildExportDto(
                    graph,
                    new DialogGraphJsonExportOptions
                    {
                        includeNodePositions = true,
                        includeCategoryMetadata = false,
                        includeGroupLayouts = false,
                        includeJumpReferenceData = false
                    });

                Assert.That(dto.primaryCategory, Is.Empty);
                Assert.That(dto.categories, Is.Empty);
                Assert.That(dto.groupLayouts, Is.Empty);
                Assert.That(dto.graphJumpNodes, Has.Count.EqualTo(1));
                Assert.That(dto.graphJumpNodes[0].targetGraph, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(jumpNode);
            }
        }

        [Test]
        public void BuildPreviewSummary_ReportsOptionalMetadataCounts()
        {
            var dto = new DialogGraphExport
            {
                graphTitle = "Cafe Intro",
                primaryCategory = "Story/Intro",
                categories = new List<string> { "Characters/Guide", "Story/Intro" },
                dialogNodes = new List<DialogExportDialogNode> { new() { guid = "dialog-1" } },
                graphJumpNodes = new List<DialogExportGraphJumpNode>
                {
                    new() { guid = "jump-a", targetGraph = new DialogExportGraphReference { graphGuid = "target-a" } },
                    new() { guid = "jump-b" }
                },
                groupLayouts = new List<ExportGroupLayoutRecord> { new() { groupId = "group-1" } },
                links = new List<ExportLink> { new() { fromGuid = "Start", toGuid = "dialog-1" } }
            };

            var summary = DialogGraphJsonSerializationUtility.BuildPreviewSummary(dto);

            Assert.That(summary.graphTitle, Is.EqualTo("Cafe Intro"));
            Assert.That(summary.dialogNodeCount, Is.EqualTo(1));
            Assert.That(summary.graphJumpNodeCount, Is.EqualTo(2));
            Assert.That(summary.jumpReferenceCount, Is.EqualTo(1));
            Assert.That(summary.groupLayoutCount, Is.EqualTo(1));
            Assert.That(summary.assignedCategoryCount, Is.EqualTo(2));
            Assert.That(summary.hasPrimaryCategory, Is.True);
            Assert.That(summary.hasImportableContent, Is.True);
        }

        [Test]
        public void HasImportableContent_ReturnsFalseForMetadataOnlyPayload()
        {
            var dto = new DialogGraphExport
            {
                graphTitle = "Metadata Only",
                primaryCategory = "Story/Intro",
                categories = new List<string> { "Characters/Guide" },
                groupLayouts = new List<ExportGroupLayoutRecord> { new() { groupId = "group-1" } }
            };

            Assert.That(DialogGraphJsonSerializationUtility.HasImportableContent(dto), Is.False);
        }
    }
}
