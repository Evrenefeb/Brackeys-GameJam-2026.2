using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Transcript;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphFlowUtilityTests
    {
        [Test]
        public void ResolveEntryGuidUsesLegacyStartAliasWhenStartGuidIsEmpty()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();

            try
            {
                graph.links.Add(new GraphLink
                {
                    fromGuid = DialogGraphFlowUtility.StartAliasGuid,
                    toGuid = "node-a",
                    fromPortIndex = 0
                });

                var entryGuid = DialogGraphFlowUtility.ResolveEntryGuid(graph);

                Assert.That(entryGuid, Is.EqualTo("node-a"));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void IsEndGuidMatchesExplicitGuidAndLegacyAlias()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();

            try
            {
                graph.endGuid = "end-guid";

                Assert.That(DialogGraphFlowUtility.IsEndGuid(graph, "end-guid"), Is.True);
                Assert.That(DialogGraphFlowUtility.IsEndGuid(graph, DialogGraphFlowUtility.EndAliasGuid), Is.True);
                Assert.That(DialogGraphFlowUtility.IsEndGuid(graph, "other"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void TranscriptStopsCleanlyAtExplicitEndNode()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            var node = ScriptableObject.CreateInstance<DialogNode>();

            try
            {
                graph.startGuid = "start-guid";
                graph.endGuid = "end-guid";

                node.SetGuid("node-a");
                node.speakerName = "Kira";
                node.questionText = "We are finished here.";
                graph.nodes.Add(node);

                graph.links.Add(new GraphLink
                {
                    fromGuid = graph.startGuid,
                    toGuid = node.GetGuid(),
                    fromPortIndex = 0
                });
                graph.links.Add(new GraphLink
                {
                    fromGuid = node.GetGuid(),
                    toGuid = graph.endGuid,
                    fromPortIndex = 0
                });

                var success = DialogGraphTranscriptBuilder.TryBuildTranscript(
                    graph,
                    DialogGraphTranscriptBuildMode.SpecificBranchPath,
                    branchPath: null,
                    out var transcript,
                    out var error);

                Assert.That(success, Is.True, error);
                Assert.That(transcript, Is.EqualTo("Kira: We are finished here."));
            }
            finally
            {
                Object.DestroyImmediate(graph);
                Object.DestroyImmediate(node);
            }
        }
    }
}
