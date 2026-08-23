using System.Reflection;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Models;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphSchemaDetectorTests
    {
        private static readonly FieldInfo GraphSchemaVersionField =
            typeof(DialogGraph).GetField("graphSchemaVersion", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo LinkGuidField =
            typeof(GraphLink).GetField("linkGuid", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void InspectReportsLegacyAndMissingLinkGuidsWithoutMutating()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();

            try
            {
                graph.links.Add(new GraphLink
                {
                    fromGuid = "Start",
                    toGuid = "node-a",
                    fromPortIndex = 0
                });

                var beforeSchemaVersion = graph.GraphSchemaVersion;
                var beforeLinkGuid = graph.links[0].LinkGuid;

                var report = DialogGraphSchemaDetector.Inspect(graph);

                Assert.That(report.GraphSchemaVersion, Is.EqualTo(0));
                Assert.That(report.CurrentSchemaVersion, Is.EqualTo(DialogGraph.CurrentSchemaVersion));
                Assert.That(report.IsLegacy, Is.True);
                Assert.That(report.LinkCount, Is.EqualTo(1));
                Assert.That(report.LinksMissingLinkGuidCount, Is.EqualTo(1));
                Assert.That(report.DuplicateLinkGuidCount, Is.EqualTo(0));
                Assert.That(report.IsUpgradeNeeded, Is.True);

                Assert.That(graph.GraphSchemaVersion, Is.EqualTo(beforeSchemaVersion));
                Assert.That(graph.links[0].LinkGuid, Is.EqualTo(beforeLinkGuid));
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        [Test]
        public void InspectReportsDuplicateAndEmptyOrNullLinks()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();

            try
            {
                SetGraphSchemaVersion(graph, DialogGraph.CurrentSchemaVersion);

                var first = CreateLink("link-1", "Start", "node-a");
                var duplicate = CreateLink("link-1", "node-a", "node-b");
                var empty = new GraphLink();

                graph.links.Add(first);
                graph.links.Add(duplicate);
                graph.links.Add(empty);
                graph.links.Add(null);

                var report = DialogGraphSchemaDetector.Inspect(graph);

                Assert.That(report.GraphSchemaVersion, Is.EqualTo(DialogGraph.CurrentSchemaVersion));
                Assert.That(report.IsLegacy, Is.False);
                Assert.That(report.LinkCount, Is.EqualTo(4));
                Assert.That(report.LinksMissingLinkGuidCount, Is.EqualTo(1));
                Assert.That(report.DuplicateLinkGuidCount, Is.EqualTo(1));
                Assert.That(report.EmptyLinkCount, Is.EqualTo(1));
                Assert.That(report.NullLinkCount, Is.EqualTo(1));
                Assert.That(report.HasEmptyOrNullLinks, Is.True);
                Assert.That(report.IsUpgradeNeeded, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(graph);
            }
        }

        private static GraphLink CreateLink(string linkGuid, string fromGuid, string toGuid)
        {
            var link = new GraphLink
            {
                fromGuid = fromGuid,
                toGuid = toGuid,
                fromPortIndex = 0
            };
            LinkGuidField.SetValue(link, linkGuid);
            return link;
        }

        private static void SetGraphSchemaVersion(DialogGraph graph, int schemaVersion)
        {
            GraphSchemaVersionField.SetValue(graph, schemaVersion);
        }
    }
}
