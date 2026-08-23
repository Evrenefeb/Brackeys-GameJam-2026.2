using System.Linq;
using DialogSystem.EditorTools.ExportImport;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class OutcomeNodeTests
    {
        #region Model Tests

        [Test]
        public void OutcomeNode_HasDefaultValues()
        {
            var node = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                Assert.That(node.GetNodeKind(), Is.EqualTo(NodeKind.Outcome));
                Assert.That(node.HasOutcomeId, Is.False);
                Assert.That(node.outcomeId, Is.Null.Or.Empty);
                Assert.That(node.displayName, Is.Null.Or.Empty);
                Assert.That(node.description, Is.Null.Or.Empty);
                Assert.That(node.ResolvedDisplayName, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(node);
            }
        }

        [Test]
        public void OutcomeNode_ResolvedDisplayName_FallsBackToOutcomeId()
        {
            var node = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                node.outcomeId = "good_ending";
                Assert.That(node.ResolvedDisplayName, Is.EqualTo("good_ending"));

                node.displayName = "Good Ending";
                Assert.That(node.ResolvedDisplayName, Is.EqualTo("Good Ending"));
            }
            finally
            {
                Object.DestroyImmediate(node);
            }
        }

        [Test]
        public void OutcomeNode_GuidAssignment_IsStable()
        {
            var node = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                node.SetGuid("test-guid-123");
                Assert.That(node.GetGuid(), Is.EqualTo("test-guid-123"));
            }
            finally
            {
                Object.DestroyImmediate(node);
            }
        }

        [Test]
        public void DialogGraph_OutcomeNodes_NullSafe()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var node = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                // New graph should have an empty list, not null
                Assert.That(graph.outcomeNodes, Is.Not.Null);
                Assert.That(graph.outcomeNodes.Count, Is.EqualTo(0));

                // Adding a node works
                node.SetGuid("outcome-1");
                node.outcomeId = "good_ending";
                graph.outcomeNodes.Add(node);

                // EnumerateAllNodeGuids includes outcome
                var guids = graph.EnumerateAllNodeGuids().ToList();
                Assert.That(guids, Does.Contain("outcome-1"));

                // ContainsRuntimeNode finds it
                Assert.That(DialogGraphFlowUtility.ContainsRuntimeNode(graph, "outcome-1"), Is.True);
                Assert.That(graph.FindOutcomeNodeByGuid("outcome-1"), Is.SameAs(node));
                Assert.That(graph.FindOutcomeNodeById("good_ending"), Is.SameAs(node));
                Assert.That(graph.TryGetOutcomeNodeById("good_ending", out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(node));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(node);
            }
        }

        #endregion

        #region JSON Round-Trip Tests

        [Test]
        public void JsonRoundTrip_OutcomeNode_PreservesFields()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var node = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("test-graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                node.SetGuid("node-guid");
                node.outcomeId = "good_ending";
                node.displayName = "Good Ending";
                node.description = "The best possible outcome.";
                node.SetPosition(new Vector2(100f, 200f));
                graph.outcomeNodes.Add(node);

                var dto = DialogGraphJsonSerializationUtility.BuildExportDto(graph, DialogGraphJsonExportOptions.Default);
                var json = JsonUtility.ToJson(dto, prettyPrint: true);
                var dtoRoundTripped = JsonUtility.FromJson<DialogGraphExport>(json);

                Assert.That(dtoRoundTripped.outcomeNodes, Is.Not.Null);
                Assert.That(dtoRoundTripped.outcomeNodes.Count, Is.EqualTo(1));

                var roundTripped = dtoRoundTripped.outcomeNodes[0];
                Assert.That(roundTripped.guid, Is.EqualTo("node-guid"));
                Assert.That(roundTripped.outcomeId, Is.EqualTo("good_ending"));
                Assert.That(roundTripped.displayName, Is.EqualTo("Good Ending"));
                Assert.That(roundTripped.description, Is.EqualTo("The best possible outcome."));
                Assert.That(roundTripped.nodePositionX, Is.EqualTo(100f));
                Assert.That(roundTripped.nodePositionY, Is.EqualTo(200f));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(node);
            }
        }

        [Test]
        public void JsonImport_OldJsonWithoutOutcomes_StillImports()
        {
            // JSON without outcomeNodes field should parse fine (list is null/empty)
            var oldJson = @"
            {
                ""graphGuid"": ""test"",
                ""schemaVersion"": 2,
                ""startNode"": { ""guid"": ""Start"", ""nodePositionX"": 0, ""nodePositionY"": 0, ""isInitialized"": true },
                ""endNode"": { ""guid"": ""End"", ""nodePositionX"": 500, ""nodePositionY"": 0, ""isInitialized"": true },
                ""dialogNodes"": [],
                ""choiceNodes"": [],
                ""actionNodes"": [],
                ""conditionNodes"": [],
                ""variableMutationNodes"": [],
                ""graphJumpNodes"": [],
                ""links"": [],
                ""groupLayouts"": []
            }";

            var dto = JsonUtility.FromJson<DialogGraphExport>(oldJson);

            Assert.That(dto, Is.Not.Null);
            Assert.That(dto.outcomeNodes, Is.Null.Or.Empty);
        }

        #endregion

        #region Validation Tests

        [Test]
        public void Validate_OutcomeNode_MissingId_ReportsError()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var outcome = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                outcome.SetGuid("outcome-1");
                // Leave outcomeId empty
                graph.outcomeNodes.Add(outcome);

                graph.links.Add(CreateLink("link-1", "outcome-1", "End"));

                var result = DialogGraphValidator.Validate(graph);

                Assert.That(result.Issues.Select(i => i.Code), Does.Contain("OUTCOME_MISSING_ID"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(outcome);
            }
        }

        [Test]
        public void Validate_OutcomeNode_DuplicateId_ReportsError()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var outcome1 = ScriptableObject.CreateInstance<OutcomeNode>();
            var outcome2 = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                outcome1.SetGuid("outcome-1");
                outcome1.outcomeId = "same_id";
                graph.outcomeNodes.Add(outcome1);

                outcome2.SetGuid("outcome-2");
                outcome2.outcomeId = "same_id";
                graph.outcomeNodes.Add(outcome2);

                graph.links.Add(CreateLink("link-1", "outcome-1", "End"));
                graph.links.Add(CreateLink("link-2", "outcome-2", "End"));

                var result = DialogGraphValidator.Validate(graph);

                Assert.That(result.Issues.Select(i => i.Code), Does.Contain("OUTCOME_DUPLICATE_ID"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(outcome1);
                Object.DestroyImmediate(outcome2);
            }
        }

        [Test]
        public void Validate_OutcomeNode_NoOutput_ReportsError()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var outcome = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                outcome.SetGuid("outcome-1");
                outcome.outcomeId = "good_ending";
                graph.outcomeNodes.Add(outcome);

                // No link from outcome to anything

                var result = DialogGraphValidator.Validate(graph);

                Assert.That(result.Issues.Select(i => i.Code), Does.Contain("OUTCOME_NO_OUTPUT"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(outcome);
            }
        }

        [Test]
        public void Validate_OutcomeNode_NotEnd_ReportsError()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var outcome = ScriptableObject.CreateInstance<OutcomeNode>();
            var dialog = ScriptableObject.CreateInstance<DialogNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                outcome.SetGuid("outcome-1");
                outcome.outcomeId = "good_ending";
                graph.outcomeNodes.Add(outcome);

                dialog.SetGuid("dialog-1");
                dialog.speakerName = "Kira";
                dialog.questionText = "Hello";
                graph.nodes.Add(dialog);

                // Link outcome to dialog instead of End
                graph.links.Add(CreateLink("bad-link", "outcome-1", "dialog-1"));

                var result = DialogGraphValidator.Validate(graph);

                Assert.That(result.Issues.Select(i => i.Code), Does.Contain("OUTCOME_NOT_END"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(outcome);
                Object.DestroyImmediate(dialog);
            }
        }

        [Test]
        public void Validate_OutcomeNode_ValidConfiguration_Passes()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var outcome = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                outcome.SetGuid("outcome-1");
                outcome.outcomeId = "good_ending";
                outcome.displayName = "Good Ending";
                graph.outcomeNodes.Add(outcome);

                // Proper link from outcome to End
                graph.links.Add(CreateLink("good-link", "outcome-1", "End"));

                var result = DialogGraphValidator.Validate(graph);

                Assert.That(result.Issues.Where(i => i.Severity >= DialogGraphValidationSeverity.Error)
                    .Select(i => i.Code),
                    Has.None.StartsWith("OUTCOME"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(outcome);
            }
        }

        [Test]
        public void Validate_OutcomeNode_MultipleOutputs_ReportsError()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var outcome = ScriptableObject.CreateInstance<OutcomeNode>();

            try
            {
                graph.AssignGraphGuidIfMissing("graph");
                graph.MarkSchemaCurrentForMigration();
                graph.startGuid = "Start";
                graph.endGuid = "End";
                graph.startInitialized = true;
                graph.endInitialized = true;

                outcome.SetGuid("outcome-1");
                outcome.outcomeId = "good_ending";
                graph.outcomeNodes.Add(outcome);

                // Multiple outgoing links
                graph.links.Add(CreateLink("link-1", "outcome-1", "End"));
                graph.links.Add(CreateLink("link-2", "outcome-1", "End"));

                var result = DialogGraphValidator.Validate(graph);

                Assert.That(result.Issues.Select(i => i.Code), Does.Contain("OUTCOME_MULTIPLE_OUTPUTS"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(outcome);
            }
        }

        #endregion

        #region Helpers

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

        #endregion
    }
}
