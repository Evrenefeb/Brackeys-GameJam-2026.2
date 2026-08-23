using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.Localization;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphClipboardServiceTests
    {
        private readonly List<string> _assetPathsToDelete = new();
        private string _sourceTablePath;
        private byte[] _sourceTableSnapshot;

        [SetUp]
        public void SetUp()
        {
            var sourceTable = new DialogLocalizationRegistryService().GetSourceTable();
            _sourceTablePath = AssetDatabase.GetAssetPath(sourceTable);
            _sourceTableSnapshot = string.IsNullOrWhiteSpace(_sourceTablePath)
                ? null
                : File.ReadAllBytes(Path.GetFullPath(_sourceTablePath));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var path in _assetPathsToDelete)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }

            _assetPathsToDelete.Clear();
            AssetDatabase.SaveAssets();

            if (_sourceTableSnapshot != null && !string.IsNullOrWhiteSpace(_sourceTablePath))
            {
                File.WriteAllBytes(Path.GetFullPath(_sourceTablePath), _sourceTableSnapshot);
                AssetDatabase.ImportAsset(_sourceTablePath, ImportAssetOptions.ForceUpdate);
            }

            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
        }

        [Test]
        public void DuplicateSelectedNodes_GraphJump_CopiesReferenceAndInternalEdges()
        {
            var targetGraph = CreateGraphAsset("ClipboardTarget");
            targetGraph.SetGraphGuidForMigration("target-graph-guid");
            var graph = CreateGraphAsset(nameof(DuplicateSelectedNodes_GraphJump_CopiesReferenceAndInternalEdges));

            var jumpNode = AddGraphJumpNode(graph, "jump", new Vector2(10f, 20f), targetGraph);
            var dialogNode = AddDialogNode(graph, "after-jump", new Vector2(360f, 20f));
            AddLink(graph, graph.startGuid, jumpNode.GetGuid(), 0);
            AddLink(graph, jumpNode.GetGuid(), dialogNode.GetGuid(), 0);
            AddLink(graph, dialogNode.GetGuid(), graph.endGuid, 0);

            var view = LoadView(graph);
            var jumpView = view.nodes.ToList().OfType<GraphJumpNodeView>().Single(node => node.GUID == "jump");
            var dialogView = view.nodes.ToList().OfType<DialogNodeView>().Single(node => node.GUID == "after-jump");

            view.AddToSelection(jumpView);
            view.AddToSelection(dialogView);
            view.DuplicateSelectedNodes();

            var duplicatedJumpView = view.selection.OfType<GraphJumpNodeView>().Single();
            var duplicatedDialogView = view.selection.OfType<DialogNodeView>().Single();
            var duplicatedJumpNode = graph.graphJumpNodes.Single(node => node != null && node.GetGuid() == duplicatedJumpView.GUID);

            Assert.That(graph.graphJumpNodes, Has.Count.EqualTo(2));
            Assert.That(duplicatedJumpNode.targetGraph.graphAsset, Is.SameAs(targetGraph));
            Assert.That(duplicatedJumpNode.targetGraph.graphGuid, Is.EqualTo(targetGraph.GraphGuid));
            Assert.That(duplicatedJumpNode.targetGraph.graphName, Is.EqualTo(targetGraph.name));
            Assert.That(duplicatedJumpNode.targetGraph.assetPath, Is.EqualTo(AssetDatabase.GetAssetPath(targetGraph)));
            Assert.That(duplicatedJumpNode.targetGraph.entryGuid, Is.EqualTo(targetGraph.startGuid));

            Assert.That(
                graph.links.Any(link =>
                    link.fromGuid == duplicatedJumpView.GUID &&
                    link.toGuid == duplicatedDialogView.GUID),
                Is.True);
        }

        [Test]
        public void DuplicateSelectedNodes_Outcome_PreservesFieldsAndEndLink()
        {
            var graph = CreateGraphAsset(nameof(DuplicateSelectedNodes_Outcome_PreservesFieldsAndEndLink));

            var outcomeNode = AddOutcomeNode(graph, "outcome", new Vector2(120f, 40f), "good_ending", "Good Ending", "Reached the best path.");
            AddLink(graph, graph.startGuid, outcomeNode.GetGuid(), 0);
            AddLink(graph, outcomeNode.GetGuid(), graph.endGuid, 0);

            var view = LoadView(graph);
            var outcomeView = view.nodes.ToList().OfType<OutcomeNodeView>().Single(node => node.GUID == "outcome");

            view.AddToSelection(outcomeView);
            view.DuplicateSelectedNodes();

            var duplicatedOutcomeView = view.selection.OfType<OutcomeNodeView>().Single();
            var duplicatedOutcomeNode = graph.outcomeNodes.Single(node => node != null && node.GetGuid() == duplicatedOutcomeView.GUID);

            Assert.That(graph.outcomeNodes, Has.Count.EqualTo(2));
            Assert.That(duplicatedOutcomeNode.outcomeId, Is.EqualTo("good_ending"));
            Assert.That(duplicatedOutcomeNode.displayName, Is.EqualTo("Good Ending"));
            Assert.That(duplicatedOutcomeNode.description, Is.EqualTo("Reached the best path."));
            Assert.That(
                graph.links.Any(link =>
                    link.fromGuid == duplicatedOutcomeView.GUID &&
                    link.toGuid == graph.endGuid &&
                    link.fromPortKey == DialogGraphPortKeys.Default),
                Is.True);
        }

        private DialogGraph CreateGraphAsset(string testName)
        {
            var graphName = $"Clipboard_{testName}_{Guid.NewGuid():N}";
            var path = DialogGraphAssetPaths.GetPrimaryGraphAssetPath(graphName);
            DialogGraphAssetPaths.EnsurePrimaryGraphFolderExists();

            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.name = graphName;
            graph.startGuid = "start";
            graph.startInitialized = true;
            graph.endGuid = "end";
            graph.endInitialized = true;

            AssetDatabase.CreateAsset(graph, path);
            _assetPathsToDelete.Add(path);
            return graph;
        }

        private static DialogGraphView LoadView(DialogGraph graph)
        {
            AssetDatabase.SaveAssets();
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();

            var view = new DialogGraphView();
            view.LoadGraph(graph.name, onUndo: false, focusStartNode: false);
            return view;
        }

        private static GraphJumpNode AddGraphJumpNode(DialogGraph graph, string guid, Vector2 position, DialogGraph targetGraph)
        {
            var node = ScriptableObject.CreateInstance<GraphJumpNode>();
            node.name = $"GraphJump_{guid}";
            node.SetGuid(guid);
            node.SetPosition(position);
            node.targetGraph.CopyIdentityFrom(targetGraph);
            node.targetGraph.assetPath = AssetDatabase.GetAssetPath(targetGraph);

            graph.graphJumpNodes.Add(node);
            AssetDatabase.AddObjectToAsset(node, graph);
            return node;
        }

        private static DialogNode AddDialogNode(DialogGraph graph, string guid, Vector2 position)
        {
            var node = ScriptableObject.CreateInstance<DialogNode>();
            node.name = $"Node_{guid}";
            node.SetGuid(guid);
            node.SetPosition(position);
            node.questionText = guid;

            graph.nodes.Add(node);
            AssetDatabase.AddObjectToAsset(node, graph);
            return node;
        }

        private static OutcomeNode AddOutcomeNode(DialogGraph graph, string guid, Vector2 position, string outcomeId, string displayName, string description)
        {
            var node = ScriptableObject.CreateInstance<OutcomeNode>();
            node.name = $"Outcome_{guid}";
            node.SetGuid(guid);
            node.SetPosition(position);
            node.outcomeId = outcomeId;
            node.displayName = displayName;
            node.description = description;

            graph.outcomeNodes.Add(node);
            AssetDatabase.AddObjectToAsset(node, graph);
            return node;
        }

        private static void AddLink(DialogGraph graph, string fromGuid, string toGuid, int fromPortIndex)
        {
            graph.links.Add(new GraphLink
            {
                fromGuid = fromGuid,
                toGuid = toGuid,
                fromPortIndex = fromPortIndex,
                fromPortKey = DialogGraphPortKeys.Default,
                toPortKey = DialogGraphPortKeys.Default
            });
        }
    }
}
