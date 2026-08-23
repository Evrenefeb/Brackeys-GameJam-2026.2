using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DialogSystem.EditorTools.Services;
using DialogSystem.EditorTools.Utils;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphLayoutFormatterEditorTests
    {
        private readonly List<string> _assetPathsToDelete = new();

        [TearDown]
        public void TearDown()
        {
            foreach (string path in _assetPathsToDelete)
            {
                if (!string.IsNullOrWhiteSpace(path))
                    AssetDatabase.DeleteAsset(path);
            }

            _assetPathsToDelete.Clear();
            AssetDatabase.SaveAssets();
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();
        }

        [Test]
        public void DefaultFormat_LinearGraph_StabilizesAndKeepsLinks()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DefaultFormat_LinearGraph_StabilizesAndKeepsLinks));
            DialogNode a = AddDialogNode(graph, "a", new Vector2(0.3f, 20.7f));
            DialogNode b = AddDialogNode(graph, "b", new Vector2(10.4f, 22.2f));

            AddLink(graph, graph.startGuid, a.GetGuid(), 0);
            AddLink(graph, a.GetGuid(), b.GetGuid(), 0);
            AddLink(graph, b.GetGuid(), graph.endGuid, 0);

            DialogGraphView view = LoadView(graph);
            string[] linksBefore = SnapshotLinks(graph);
            string[] edgeLayoutsBefore = SnapshotEdgeLayouts(graph);

            Assert.That(FormatDefault(view), Is.True);
            AssertNoNodeOverlaps(view);

            Dictionary<string, Vector2> firstPositions = SnapshotViewPositions(view);
            Assert.That(FormatDefault(view), Is.False);
            Assert.That(SnapshotViewPositions(view), Is.EqualTo(firstPositions));
            AssertAllPositionsSnapped(view);
            Assert.That(SnapshotLinks(graph), Is.EqualTo(linksBefore));
            Assert.That(SnapshotEdgeLayouts(graph), Is.EqualTo(edgeLayoutsBefore));
        }

        [Test]
        public void DefaultFormat_ChoiceBranches_NoOverlapAndStablePortOrder()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DefaultFormat_ChoiceBranches_NoOverlapAndStablePortOrder));
            ChoiceNode choice = AddChoiceNode(graph, "choice", new Vector2(0f, 0f), 5);
            DialogNode[] branches = Enumerable.Range(0, 5)
                .Select(index => AddDialogNode(graph, $"branch-{index}", new Vector2(10f, 5f)))
                .ToArray();
            DialogNode merge = AddDialogNode(graph, "merge", new Vector2(20f, 5f));

            AddLink(graph, graph.startGuid, choice.GetGuid(), 0);
            for (int i = 0; i < branches.Length; i++)
            {
                choice.choices[i].nextNodeGUID = branches[i].GetGuid();
                AddLink(graph, choice.GetGuid(), branches[i].GetGuid(), i);
                AddLink(graph, branches[i].GetGuid(), merge.GetGuid(), 0);
            }

            AddLink(graph, merge.GetGuid(), graph.endGuid, 0);

            DialogGraphView view = LoadView(graph);
            string[] linksBefore = SnapshotLinks(graph);

            FormatDefaultRepeated(view, 5);

            AssertNoNodeOverlaps(view);
            AssertBranchOrder(view, branches.Select(node => node.GetGuid()).ToArray());
            Assert.That(SnapshotLinks(graph), Is.EqualTo(linksBefore));
        }

        [Test]
        public void DefaultFormat_ConditionBranches_NoOverlapAndStableTrueFalseOrder()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DefaultFormat_ConditionBranches_NoOverlapAndStableTrueFalseOrder));
            ConditionNode condition = AddConditionNode(graph, "condition", new Vector2(0f, 0f));
            DialogNode trueNode = AddDialogNode(graph, "true", new Vector2(5f, 5f));
            DialogNode falseNode = AddDialogNode(graph, "false", new Vector2(5f, 5f));
            DialogNode merge = AddDialogNode(graph, "merge", new Vector2(10f, 5f));

            AddLink(graph, graph.startGuid, condition.GetGuid(), 0);
            AddLink(graph, condition.GetGuid(), trueNode.GetGuid(), ConditionNode.TruePortIndex);
            AddLink(graph, condition.GetGuid(), falseNode.GetGuid(), ConditionNode.FalsePortIndex);
            AddLink(graph, trueNode.GetGuid(), merge.GetGuid(), 0);
            AddLink(graph, falseNode.GetGuid(), merge.GetGuid(), 0);
            AddLink(graph, merge.GetGuid(), graph.endGuid, 0);

            DialogGraphView view = LoadView(graph);
            string[] linksBefore = SnapshotLinks(graph);
            string[] edgeLayoutsBefore = SnapshotEdgeLayouts(graph);

            FormatDefaultRepeated(view, 5);

            AssertNoNodeOverlaps(view);
            Dictionary<string, Vector2> positions = SnapshotViewPositions(view);
            Assert.That(positions[trueNode.GetGuid()].y, Is.LessThan(positions[falseNode.GetGuid()].y));
            Assert.That(SnapshotLinks(graph), Is.EqualTo(linksBefore));
            Assert.That(SnapshotEdgeLayouts(graph), Is.EqualTo(edgeLayoutsBefore));
        }

        [Test]
        public void DefaultFormat_ConditionBranches_UsePortSemanticsWhenVisualEdgeIndexIsStale()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DefaultFormat_ConditionBranches_UsePortSemanticsWhenVisualEdgeIndexIsStale));
            ConditionNode condition = AddConditionNode(graph, "condition", new Vector2(0f, 0f));
            DialogNode trueNode = AddDialogNode(graph, "true", new Vector2(5f, 5f));
            DialogNode falseNode = AddDialogNode(graph, "false", new Vector2(5f, 5f));

            AddLink(graph, graph.startGuid, condition.GetGuid(), 0);
            AddLink(graph, condition.GetGuid(), trueNode.GetGuid(), ConditionNode.TruePortIndex);
            AddLink(graph, condition.GetGuid(), falseNode.GetGuid(), ConditionNode.FalsePortIndex);
            AddLink(graph, trueNode.GetGuid(), graph.endGuid, 0);
            AddLink(graph, falseNode.GetGuid(), graph.endGuid, 0);

            DialogGraphView view = LoadView(graph);
            string[] linksBefore = SnapshotLinks(graph);
            string[] edgeLayoutsBefore = SnapshotEdgeLayouts(graph);

            DialogGraphEdge falseEdge = view.edges
                .ToList()
                .OfType<DialogGraphEdge>()
                .FirstOrDefault(edge =>
                    edge.output?.node is ConditionNodeView conditionView &&
                    conditionView.GUID == condition.GetGuid() &&
                    edge.input?.node is DialogNodeView dialogView &&
                    dialogView.GUID == falseNode.GetGuid());

            Assert.That(falseEdge, Is.Not.Null);
            falseEdge.fromPortIndex = ConditionNode.TruePortIndex;

            FormatDefaultRepeated(view, 5);

            AssertNoNodeOverlaps(view);
            Dictionary<string, Vector2> positions = SnapshotViewPositions(view);
            Assert.That(positions[trueNode.GetGuid()].y, Is.LessThan(positions[falseNode.GetGuid()].y));
            Assert.That(SnapshotLinks(graph), Is.EqualTo(linksBefore));
            Assert.That(SnapshotEdgeLayouts(graph), Is.EqualTo(edgeLayoutsBefore));
        }

        [Test]
        public void DefaultFormat_ConditionComponent_KeepsStartAnchoredAndMovesConditionIntoFlow()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DefaultFormat_ConditionComponent_KeepsStartAnchoredAndMovesConditionIntoFlow));
            ConditionNode condition = AddConditionNode(graph, "condition", new Vector2(1800f, -900f));
            DialogNode trueNode = AddDialogNode(graph, "true", new Vector2(1850f, -900f));
            DialogNode falseNode = AddDialogNode(graph, "false", new Vector2(1850f, -900f));

            AddLink(graph, graph.startGuid, condition.GetGuid(), 0);
            AddLink(graph, condition.GetGuid(), trueNode.GetGuid(), ConditionNode.TruePortIndex);
            AddLink(graph, condition.GetGuid(), falseNode.GetGuid(), ConditionNode.FalsePortIndex);
            AddLink(graph, trueNode.GetGuid(), graph.endGuid, 0);
            AddLink(graph, falseNode.GetGuid(), graph.endGuid, 0);

            DialogGraphView view = LoadView(graph);
            string[] linksBefore = SnapshotLinks(graph);
            string[] edgeLayoutsBefore = SnapshotEdgeLayouts(graph);

            FormatDefaultRepeated(view, 5);

            Dictionary<string, Vector2> positions = SnapshotViewPositions(view);
            AssertNoNodeOverlaps(view);
            Assert.That(positions[graph.startGuid], Is.EqualTo(new Vector2(-300f, 100f)));
            Assert.That(positions[condition.GetGuid()].x, Is.GreaterThan(positions[graph.startGuid].x));
            Assert.That(positions[condition.GetGuid()].x, Is.LessThan(positions[trueNode.GetGuid()].x));
            Assert.That(positions[trueNode.GetGuid()].y, Is.LessThan(positions[falseNode.GetGuid()].y));
            Assert.That(SnapshotLinks(graph), Is.EqualTo(linksBefore));
            Assert.That(SnapshotEdgeLayouts(graph), Is.EqualTo(edgeLayoutsBefore));
        }

        [Test]
        public void DefaultFormat_ConditionOutputsDirectlyToEnd_StabilizesWithoutChangingLinks()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DefaultFormat_ConditionOutputsDirectlyToEnd_StabilizesWithoutChangingLinks));
            ConditionNode condition = AddConditionNode(graph, "condition", new Vector2(1200f, 700f));

            AddLink(graph, graph.startGuid, condition.GetGuid(), 0);
            AddLink(graph, condition.GetGuid(), graph.endGuid, ConditionNode.TruePortIndex);
            AddLink(graph, condition.GetGuid(), graph.endGuid, ConditionNode.FalsePortIndex);

            DialogGraphView view = LoadView(graph);
            string[] linksBefore = SnapshotLinks(graph);
            string[] edgeLayoutsBefore = SnapshotEdgeLayouts(graph);

            FormatDefaultRepeated(view, 5);

            Dictionary<string, Vector2> positions = SnapshotViewPositions(view);
            AssertNoNodeOverlaps(view);
            Assert.That(positions[condition.GetGuid()].x, Is.GreaterThan(positions[graph.startGuid].x));
            Assert.That(positions[graph.endGuid].x, Is.GreaterThan(positions[condition.GetGuid()].x));
            Assert.That(SnapshotLinks(graph), Is.EqualTo(linksBefore));
            Assert.That(SnapshotEdgeLayouts(graph), Is.EqualTo(edgeLayoutsBefore));
        }

        [Test]
        public void DefaultFormat_RetryLoopBackEdge_DoesNotPushTargetRight()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DefaultFormat_RetryLoopBackEdge_DoesNotPushTargetRight));
            DialogNode a = AddDialogNode(graph, "a", new Vector2(900f, -450f));
            ConditionNode condition = AddConditionNode(graph, "condition", new Vector2(-600f, 300f));
            DialogNode b = AddDialogNode(graph, "b", new Vector2(-900f, 650f));

            AddLink(graph, graph.startGuid, a.GetGuid(), 0);
            AddLink(graph, a.GetGuid(), condition.GetGuid(), 0);
            AddLink(graph, condition.GetGuid(), b.GetGuid(), ConditionNode.TruePortIndex);
            AddLink(graph, condition.GetGuid(), graph.endGuid, ConditionNode.FalsePortIndex);
            AddLink(graph, b.GetGuid(), a.GetGuid(), 0);

            DialogGraphView view = LoadView(graph);
            string[] linksBefore = SnapshotLinks(graph);
            string[] edgeLayoutsBefore = SnapshotEdgeLayouts(graph);

            FormatDefaultRepeated(view, 5);

            Dictionary<string, Vector2> positions = SnapshotViewPositions(view);
            AssertNoNodeOverlaps(view);
            Assert.That(positions[graph.startGuid].x, Is.LessThan(positions[a.GetGuid()].x));
            Assert.That(positions[a.GetGuid()].x, Is.LessThan(positions[condition.GetGuid()].x));
            Assert.That(positions[condition.GetGuid()].x, Is.LessThan(positions[b.GetGuid()].x));
            Assert.That(SnapshotLinks(graph), Is.EqualTo(linksBefore));
            Assert.That(SnapshotEdgeLayouts(graph), Is.EqualTo(edgeLayoutsBefore));
        }

        [Test]
        public void DefaultFormat_MergeAndLoopGraph_StabilizesWithoutChangingLinks()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DefaultFormat_MergeAndLoopGraph_StabilizesWithoutChangingLinks));
            DialogNode a = AddDialogNode(graph, "a", new Vector2(0f, 0f));
            DialogNode b = AddDialogNode(graph, "b", new Vector2(0f, 0f));
            DialogNode c = AddDialogNode(graph, "c", new Vector2(0f, 0f));
            DialogNode merge = AddDialogNode(graph, "merge", new Vector2(0f, 0f));

            AddLink(graph, graph.startGuid, a.GetGuid(), 0);
            AddLink(graph, a.GetGuid(), b.GetGuid(), 0);
            AddLink(graph, b.GetGuid(), c.GetGuid(), 0);
            AddLink(graph, c.GetGuid(), a.GetGuid(), 0);
            AddLink(graph, b.GetGuid(), merge.GetGuid(), 1);
            AddLink(graph, c.GetGuid(), merge.GetGuid(), 1);
            AddLink(graph, merge.GetGuid(), graph.endGuid, 0);

            DialogGraphView view = LoadView(graph);
            string[] linksBefore = SnapshotLinks(graph);

            FormatDefaultRepeated(view, 5);

            AssertNoNodeOverlaps(view);
            Assert.That(SnapshotLinks(graph), Is.EqualTo(linksBefore));
        }

        [Test]
        public void DefaultFormat_ManualReroutes_RemainUnchangedAcrossRepeatedFormatting()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DefaultFormat_ManualReroutes_RemainUnchangedAcrossRepeatedFormatting));
            DialogNode a = AddDialogNode(graph, "a", new Vector2(0f, 0f));
            DialogNode b = AddDialogNode(graph, "b", new Vector2(0f, 0f));

            AddLink(graph, graph.startGuid, a.GetGuid(), 0, "link-start-a");
            AddLink(graph, a.GetGuid(), b.GetGuid(), 0, "link-a-b");
            AddLink(graph, b.GetGuid(), graph.endGuid, 0, "link-b-end");

            EdgeLayoutRecord layout = graph.GetOrCreateEdgeLayoutForEditor("link-a-b", a.GetGuid(), b.GetGuid(), 0);
            layout.reroutePoints.Add(new Vector2(125.5f, -250.25f));
            layout.reroutePoints.Add(new Vector2(275.75f, 325.5f));

            DialogGraphView view = LoadView(graph);
            string[] linksBefore = SnapshotLinks(graph);
            string[] edgeLayoutsBefore = SnapshotEdgeLayouts(graph);

            FormatDefaultRepeated(view, 5);

            AssertNoNodeOverlaps(view);
            Assert.That(SnapshotLinks(graph), Is.EqualTo(linksBefore));
            Assert.That(SnapshotEdgeLayouts(graph), Is.EqualTo(edgeLayoutsBefore));
        }

        [Test]
        public void GroupSelectedNodes_SaveAndReload_PersistsMembershipTitleAndTint()
        {
            DialogGraph graph = CreateGraphAsset(nameof(GroupSelectedNodes_SaveAndReload_PersistsMembershipTitleAndTint));
            AddDialogNode(graph, "a", new Vector2(120f, 80f));
            AddDialogNode(graph, "b", new Vector2(460f, 220f));

            DialogGraphView view = LoadView(graph);
            var dialogNodes = view.nodes.ToList().OfType<DialogNodeView>().OrderBy(node => node.GUID, StringComparer.Ordinal).ToList();

            view.AddToSelection(dialogNodes[0]);
            view.AddToSelection(dialogNodes[1]);
            view.GroupSelectedNodes();

            Group group = view.graphElements.ToList().OfType<Group>().Single();
            group.title = "Act 1";
            group.SetPosition(new Rect(new Vector2(64f, 24f), new Vector2(620f, 360f)));
            group.GetType().GetMethod("ApplyTint")?.Invoke(group, new object[] { new Color(0.84f, 0.55f, 0.16f, 0.16f) });

            view.SaveGraph(graph.name);

            GroupLayoutRecord layout = graph.EnumerateGroupLayouts().Single();
            Assert.That(layout.title, Is.EqualTo("Act 1"));
            Assert.That(layout.nodeGuids.OrderBy(guid => guid, StringComparer.Ordinal), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(layout.bounds.position, Is.EqualTo(new Vector2(64f, 24f)));
            Assert.That(layout.colorTint, Is.EqualTo(new Color(0.84f, 0.55f, 0.16f, 0.16f)));

            DialogGraphView reloadedView = LoadView(graph);
            Group reloadedGroup = reloadedView.graphElements.ToList().OfType<Group>().Single();

            Assert.That(reloadedGroup.title, Is.EqualTo("Act 1"));
            Assert.That(reloadedGroup.containedElements.OfType<Node>().Count(), Is.EqualTo(2));
            Assert.That(reloadedGroup.GetPosition().position, Is.EqualTo(new Vector2(64f, 24f)));
        }

        [Test]
        public void DeleteGroupKeepNodes_RemovesLayoutButLeavesNodeAssetsAndViews()
        {
            DialogGraph graph = CreateGraphAsset(nameof(DeleteGroupKeepNodes_RemovesLayoutButLeavesNodeAssetsAndViews));
            AddDialogNode(graph, "a", new Vector2(120f, 80f));
            AddDialogNode(graph, "b", new Vector2(460f, 220f));

            DialogGraphView view = LoadView(graph);
            foreach (var node in view.nodes.ToList().OfType<DialogNodeView>())
            {
                view.AddToSelection(node);
            }

            view.GroupSelectedNodes();
            Group group = view.graphElements.ToList().OfType<Group>().Single();

            MethodInfo deleteGroupMethod = typeof(DialogGraphView).GetMethod("DeleteGroupKeepNodes", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(deleteGroupMethod, Is.Not.Null);

            deleteGroupMethod.Invoke(view, new object[] { group });

            Assert.That(graph.EnumerateGroupLayouts(), Is.Empty);
            Assert.That(graph.nodes.Count, Is.EqualTo(2));
            Assert.That(view.nodes.ToList().OfType<DialogNodeView>().Count(), Is.EqualTo(2));
            Assert.That(view.graphElements.ToList().OfType<Group>(), Is.Empty);
        }

        private DialogGraph CreateGraphAsset(string testName)
        {
            string graphName = $"FormatterPhase4_{testName}_{Guid.NewGuid():N}";
            string path = DialogGraphAssetPaths.GetPrimaryGraphAssetPath(graphName);
            DialogGraphAssetPaths.EnsurePrimaryGraphFolderExists();

            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.name = graphName;
            graph.startGuid = "start";
            graph.startPosition = new Vector2(-320.2f, 80.6f);
            graph.startInitialized = true;
            graph.endGuid = "end";
            graph.endPosition = new Vector2(720.9f, 80.1f);
            graph.endInitialized = true;

            AssetDatabase.CreateAsset(graph, path);
            _assetPathsToDelete.Add(path);
            return graph;
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

        private static ChoiceNode AddChoiceNode(DialogGraph graph, string guid, Vector2 position, int choiceCount)
        {
            var node = ScriptableObject.CreateInstance<ChoiceNode>();
            node.name = $"Choice_{guid}";
            node.SetGuid(guid);
            node.SetPosition(position);
            for (int i = 0; i < choiceCount; i++)
            {
                node.choices.Add(new Choice
                {
                    answerText = $"Choice {i + 1}"
                });
            }

            graph.choiceNodes.Add(node);
            AssetDatabase.AddObjectToAsset(node, graph);
            return node;
        }

        private static ConditionNode AddConditionNode(DialogGraph graph, string guid, Vector2 position)
        {
            var node = ScriptableObject.CreateInstance<ConditionNode>();
            node.name = $"Condition_{guid}";
            node.SetGuid(guid);
            node.SetPosition(position);
            node.variableName = "flag";
            graph.conditionNodes.Add(node);
            AssetDatabase.AddObjectToAsset(node, graph);
            return node;
        }

        private static GraphLink AddLink(DialogGraph graph, string fromGuid, string toGuid, int fromPortIndex, string linkGuid = null)
        {
            var link = new GraphLink
            {
                fromGuid = fromGuid,
                toGuid = toGuid,
                fromPortIndex = fromPortIndex
            };

            if (!string.IsNullOrWhiteSpace(linkGuid))
                link.AssignLinkGuidIfMissing(linkGuid);

            graph.links.Add(link);
            return link;
        }

        private static DialogGraphView LoadView(DialogGraph graph)
        {
            AssetDatabase.SaveAssets();
            DialogGraphAssetPaths.InvalidateVisibleGraphCache();

            var view = new DialogGraphView();
            view.LoadGraph(graph.name, onUndo: false, focusStartNode: false);
            return view;
        }

        private static bool FormatDefault(DialogGraphView view)
        {
            return DialogGraphLayoutFormatterEditor.FormatLinkedSubgraphOnly(
                view,
                new DialogGraphLayoutFormatterEditor.Settings
                {
                    Mode = DialogGraphLayoutFormatterEditor.FormatterMode.FormatNodesOnlyPreserveReroutes,
                    ManageReroutePoints = false
                });
        }

        private static void FormatDefaultRepeated(DialogGraphView view, int count)
        {
            Dictionary<string, Vector2> previous = null;

            for (int i = 0; i < count; i++)
            {
                FormatDefault(view);
                Dictionary<string, Vector2> current = SnapshotViewPositions(view);

                if (previous != null && current.SequenceEqual(previous))
                {
                    Assert.That(FormatDefault(view), Is.False);
                    Assert.That(SnapshotViewPositions(view), Is.EqualTo(current));
                    return;
                }

                previous = current;
            }

            Assert.Fail("Default formatter did not stabilize within the requested repeat count.");
        }

        private static Dictionary<string, Vector2> SnapshotViewPositions(DialogGraphView view)
        {
            DialogGraph graph = string.IsNullOrWhiteSpace(view.graphId)
                ? null
                : DialogGraphAssetPaths.LoadGraphAsset(view.graphId);

            return view.nodes
                .ToList()
                .OfType<Node>()
                .Select(node => new { Guid = GetNodeGuid(node), Position = GetEffectiveRect(view, graph, node).position })
                .Where(item => !string.IsNullOrWhiteSpace(item.Guid))
                .OrderBy(item => item.Guid, StringComparer.Ordinal)
                .ToDictionary(item => item.Guid, item => item.Position, StringComparer.Ordinal);
        }

        private static string[] SnapshotLinks(DialogGraph graph)
        {
            return graph.links
                .Select(link => $"{link.fromGuid}>{link.toGuid}:{link.fromPortIndex}:{link.LinkGuid ?? string.Empty}")
                .ToArray();
        }

        private static string[] SnapshotEdgeLayouts(DialogGraph graph)
        {
            return graph.links
                .Where(link => !string.IsNullOrWhiteSpace(link.LinkGuid))
                .Select(link =>
                {
                    EdgeLayoutRecord layout = graph.GetEdgeLayout(link.LinkGuid);
                    if (layout == null)
                        return $"{link.LinkGuid}:<null>";

                    string points = string.Join("|", layout.reroutePoints.Select(point => $"{point.x:F3},{point.y:F3}"));
                    return $"{layout.linkGuid}:{layout.fromGuid}>{layout.toGuid}:{layout.fromPortIndex}:{points}";
                })
                .ToArray();
        }

        private static void AssertNoNodeOverlaps(DialogGraphView view)
        {
            List<Node> nodes = view.nodes.ToList().OfType<Node>().ToList();
            DialogGraph graph = string.IsNullOrWhiteSpace(view.graphId)
                ? null
                : DialogGraphAssetPaths.LoadGraphAsset(view.graphId);

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    Rect a = NormalizeRect(GetEffectiveRect(view, graph, nodes[i]));
                    Rect b = NormalizeRect(GetEffectiveRect(view, graph, nodes[j]));
                    Assert.That(a.Overlaps(b), Is.False, $"{GetNodeGuid(nodes[i])} overlaps {GetNodeGuid(nodes[j])}");
                }
            }
        }

        private static Rect GetEffectiveRect(DialogGraphView view, DialogGraph graph, Node node)
        {
            Rect rect = node.GetPosition();
            if (IsUsableRect(rect))
                return rect;

            string guid = GetNodeGuid(node);
            Vector2 position = TryGetSerializedPosition(graph, guid, out Vector2 serializedPosition)
                ? serializedPosition
                : rect.position;

            return new Rect(position, GetFallbackSize(node));
        }

        private static bool IsUsableRect(Rect rect)
        {
            return IsFinite(rect.x) &&
                   IsFinite(rect.y) &&
                   IsFinite(rect.width) &&
                   IsFinite(rect.height) &&
                   rect.width > 0f &&
                   rect.height > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Vector2 GetFallbackSize(Node node)
        {
            if (node is StartNodeView || node is EndNodeView)
                return new Vector2(200f, 120f);

            if (node is ChoiceNodeView)
                return new Vector2(260f, 220f);

            if (node is ConditionNodeView)
                return new Vector2(340f, 220f);

            return new Vector2(200f, 150f);
        }

        private static bool TryGetSerializedPosition(DialogGraph graph, string guid, out Vector2 position)
        {
            position = Vector2.zero;

            if (graph == null || string.IsNullOrWhiteSpace(guid))
                return false;

            if (string.Equals(guid, graph.startGuid, StringComparison.Ordinal) && graph.startInitialized)
            {
                position = graph.startPosition;
                return true;
            }

            if (string.Equals(guid, graph.endGuid, StringComparison.Ordinal) && graph.endInitialized)
            {
                position = graph.endPosition;
                return true;
            }

            BaseNode nodeData = graph.nodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= graph.choiceNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= graph.actionNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= graph.conditionNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= graph.variableMutationNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));

            if (nodeData == null)
                return false;

            position = nodeData.GetPosition();
            return true;
        }

        private static void AssertAllPositionsSnapped(DialogGraphView view)
        {
            foreach (Vector2 position in SnapshotViewPositions(view).Values)
            {
                Assert.That(Mathf.Approximately(position.x % 50f, 0f), Is.True, $"X position {position.x} is not snapped.");
                Assert.That(Mathf.Approximately(position.y % 50f, 0f), Is.True, $"Y position {position.y} is not snapped.");
            }
        }

        private static void AssertBranchOrder(DialogGraphView view, string[] branchGuids)
        {
            Dictionary<string, Vector2> positions = SnapshotViewPositions(view);

            for (int i = 1; i < branchGuids.Length; i++)
            {
                Assert.That(
                    positions[branchGuids[i - 1]].y,
                    Is.LessThan(positions[branchGuids[i]].y),
                    $"{branchGuids[i - 1]} should remain above {branchGuids[i]}.");
            }
        }

        private static Rect NormalizeRect(Rect rect)
        {
            Vector2 size = rect.size;
            if (size.x <= 0f)
                size.x = 200f;
            if (size.y <= 0f)
                size.y = 120f;

            return new Rect(rect.position, size);
        }

        private static string GetNodeGuid(Node node)
        {
            return node switch
            {
                DialogNodeView dialogNode => dialogNode.GUID,
                ChoiceNodeView choiceNode => choiceNode.GUID,
                ConditionNodeView conditionNode => conditionNode.GUID,
                StartNodeView startNode => startNode.GUID,
                EndNodeView endNode => endNode.GUID,
                _ => string.Empty
            };
        }
    }
}
