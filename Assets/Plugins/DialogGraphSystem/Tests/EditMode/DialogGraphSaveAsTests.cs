using System;
using System.Collections.Generic;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.View;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphSaveAsTests
    {
        private readonly List<string> _assetPathsToDelete = new();

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
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
        }

        [Test]
        public void SaveGraph_AsNew_CopiesEditedContentAndRestoresOriginalSource()
        {
            var sourceGraph = CreateGraphAsset("SaveAsSource");
            var sourceNode = AddDialogNode(sourceGraph, "node-source", "Original line.", new Vector2(120f, 45f));
            AddLink(sourceGraph, sourceGraph.startGuid, sourceNode.GetGuid(), 0);
            AddLink(sourceGraph, sourceNode.GetGuid(), sourceGraph.endGuid, 0);
            AssetDatabase.SaveAssets();

            var view = new DialogGraphView();
            view.LoadGraph(sourceGraph.name, false, focusStartNode: false);

            sourceNode.questionText = "Edited clone line.";
            sourceNode.SetPosition(new Vector2(300f, 160f));
            EditorUtility.SetDirty(sourceNode);
            EditorUtility.SetDirty(sourceGraph);
            AssetDatabase.SaveAssets();

            var targetGraphName = $"SaveAsTarget_{Guid.NewGuid():N}";
            view.SaveGraph(targetGraphName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _assetPathsToDelete.Add(DialogGraphAssetPaths.ResolveGraphAssetPath(targetGraphName));

            var reloadedSource = DialogGraphAssetPaths.LoadGraphAsset(sourceGraph.name);
            var savedClone = DialogGraphAssetPaths.LoadGraphAsset(targetGraphName);

            Assert.That(reloadedSource, Is.Not.Null);
            Assert.That(savedClone, Is.Not.Null);
            Assert.That(reloadedSource.nodes, Has.Count.EqualTo(1));
            Assert.That(savedClone.nodes, Has.Count.EqualTo(1));
            Assert.That(reloadedSource.nodes[0].questionText, Is.EqualTo("Original line."));
            Assert.That(savedClone.nodes[0].questionText, Is.EqualTo("Edited clone line."));
            Assert.That(savedClone.nodes[0].GetPosition(), Is.EqualTo(new Vector2(300f, 160f)));
            Assert.That(savedClone.links, Has.Count.EqualTo(2));
            Assert.That(savedClone.GraphGuid, Is.Not.EqualTo(reloadedSource.GraphGuid));
            Assert.That(savedClone.nodes[0], Is.Not.SameAs(reloadedSource.nodes[0]));
        }

        private DialogGraph CreateGraphAsset(string prefix)
        {
            var graphName = $"{prefix}_{Guid.NewGuid():N}";
            var path = DialogGraphAssetPaths.GetPrimaryGraphAssetPath(graphName);
            DialogGraphAssetPaths.EnsurePrimaryGraphFolderExists();

            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.name = graphName;
            graph.AssignGraphGuidIfMissing(Guid.NewGuid().ToString("N"));
            graph.MarkSchemaCurrentForMigration();
            graph.startGuid = "start";
            graph.startInitialized = true;
            graph.startPosition = new Vector2(-320f, 80f);
            graph.endGuid = "end";
            graph.endInitialized = true;
            graph.endPosition = new Vector2(720f, 80f);

            AssetDatabase.CreateAsset(graph, path);
            _assetPathsToDelete.Add(path);
            return graph;
        }

        private static DialogNode AddDialogNode(DialogGraph graph, string guid, string text, Vector2 position)
        {
            var node = ScriptableObject.CreateInstance<DialogNode>();
            node.name = $"Node_{guid}";
            node.SetGuid(guid);
            node.questionText = text;
            node.SetPosition(position);

            graph.nodes.Add(node);
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
