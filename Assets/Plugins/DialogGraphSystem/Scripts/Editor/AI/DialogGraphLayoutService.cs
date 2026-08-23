using System.Collections.Generic;
using System.Linq;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements.Nodes;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DialogSystem.EditorTools.AI
{
    /// <summary>
    /// Centralized, deterministic layout logic for AI-created graph changes.
    /// This service owns node positions for AI workflows and ignores AI-provided coordinates.
    /// </summary>
    public sealed class DialogGraphLayoutService
    {
        private const float HorizontalSpacing = 420f;
        private const float VerticalSpacing = 280f;
        private const float CollisionPadding = 40f;
        private const float CollisionStepY = 140f;
        private const float CollisionStepX = 80f;
        private const float GridSnap = 20f;

        private static readonly Vector2 DefaultDialogSize = new Vector2(220f, 170f);
        private static readonly Vector2 DefaultChoiceSize = new Vector2(260f, 220f);
        private static readonly Vector2 DefaultActionSize = new Vector2(240f, 170f);
        private static readonly Vector2 DefaultBoundarySize = new Vector2(200f, 120f);

        public Vector2 GetNextNodeAfterSelectedPosition(DialogGraphView graphView, Node selectedNode, Node nodeToPlace = null)
        {
            if (selectedNode == null)
            {
                return Vector2.zero;
            }

            var selectedRect = selectedNode.GetPosition();
            var selectedSize = GetNodeSize(selectedNode);
            var desiredSize = GetNodeSize(nodeToPlace);
            var desired = new Vector2(
                selectedRect.xMin + selectedSize.x + HorizontalSpacing,
                selectedRect.yMin + (selectedSize.y - desiredSize.y) * 0.5f);

            return ResolveFreePosition(graphView, desired, desiredSize, selectedNode, nodeToPlace);
        }

        public List<Vector2> GetChainBeforeEndPositions(DialogGraphView graphView, EndNodeView endNode, int count)
        {
            var positions = new List<Vector2>();
            if (endNode == null || count <= 0)
            {
                return positions;
            }

            var endRect = endNode.GetPosition();
            var startX = endRect.xMin - HorizontalSpacing * count;
            var baseY = endRect.yMin;

            for (var i = 0; i < count; i++)
            {
                var desired = new Vector2(startX + i * HorizontalSpacing, baseY);
                positions.Add(ResolveFreePosition(graphView, desired, DefaultDialogSize, endNode));
            }

            return positions;
        }

        public List<Vector2> GetBranchChildrenPositions(DialogGraphView graphView, Node parentNode, int childCount)
        {
            var positions = new List<Vector2>();
            if (parentNode == null || childCount <= 0)
            {
                return positions;
            }

            var parentRect = parentNode.GetPosition();
            var baseX = parentRect.xMin + GetNodeSize(parentNode).x + HorizontalSpacing;
            var startY = parentRect.yMin - ((childCount - 1) * VerticalSpacing * 0.5f);

            for (var i = 0; i < childCount; i++)
            {
                var desired = new Vector2(baseX, startY + i * VerticalSpacing);
                positions.Add(ResolveFreePosition(graphView, desired, DefaultDialogSize, parentNode));
            }

            return positions;
        }

        public List<Vector2> GetChainBetweenPositions(DialogGraphView graphView, Node fromNode, Node toNode, int count)
        {
            var positions = new List<Vector2>();
            if (count <= 0 || fromNode == null)
            {
                return positions;
            }

            if (toNode == null)
            {
                var next = GetNextNodeAfterSelectedPosition(graphView, fromNode);
                for (var i = 0; i < count; i++)
                {
                    var desired = new Vector2(next.x + i * HorizontalSpacing, next.y);
                    positions.Add(ResolveFreePosition(graphView, desired, DefaultDialogSize, fromNode));
                }

                return positions;
            }

            var fromRect = fromNode.GetPosition();
            var toRect = toNode.GetPosition();
            var spanX = (toRect.xMin - fromRect.xMin) / (count + 1);
            var baseY = (fromRect.yMin + toRect.yMin) * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var desired = new Vector2(fromRect.xMin + spanX * (i + 1), baseY);
                positions.Add(ResolveFreePosition(graphView, desired, DefaultDialogSize, fromNode, toNode));
            }

            return positions;
        }

        public Vector2 GetInsertedActionNodePosition(DialogGraphView graphView, Node fromNode, Node toNode)
        {
            return GetInsertedNodeBetweenPosition(graphView, fromNode, toNode, DefaultActionSize);
        }

        public Vector2 GetInsertedChoiceNodePosition(DialogGraphView graphView, Node fromNode, Node toNode)
        {
            return GetInsertedNodeBetweenPosition(graphView, fromNode, toNode, DefaultChoiceSize);
        }

        public Vector2 GetInsertedNodeBetweenPosition(DialogGraphView graphView, Node fromNode, Node toNode, Node insertedNode = null)
        {
            return GetInsertedNodeBetweenPosition(graphView, fromNode, toNode, GetNodeSize(insertedNode));
        }

        private Vector2 GetInsertedNodeBetweenPosition(DialogGraphView graphView, Node fromNode, Node toNode, Vector2 insertedSize)
        {
            if (fromNode == null && toNode == null)
            {
                return Vector2.zero;
            }

            if (fromNode != null && toNode == null)
            {
                return GetNextNodeAfterSelectedPosition(graphView, fromNode);
            }

            if (fromNode == null)
            {
                var targetRect = toNode.GetPosition();
                var desired = new Vector2(targetRect.xMin - HorizontalSpacing, targetRect.yMin);
                return ResolveFreePosition(graphView, desired, insertedSize, toNode);
            }

            var fromRect = fromNode.GetPosition();
            var toRect = toNode.GetPosition();
            var desiredMidpoint = new Vector2(
                (fromRect.center.x + toRect.center.x) * 0.5f - insertedSize.x * 0.5f,
                (fromRect.center.y + toRect.center.y) * 0.5f - insertedSize.y * 0.5f);

            return ResolveFreePosition(graphView, desiredMidpoint, insertedSize, fromNode, toNode);
        }

        private Vector2 ResolveFreePosition(DialogGraphView graphView, Vector2 desired, Vector2 size, params Node[] ignoreNodes)
        {
            var candidate = Snap(desired);
            var ignoreSet = new HashSet<Node>(ignoreNodes.Where(node => node != null));
            var occupied = graphView?.nodes
                .ToList()
                .OfType<Node>()
                .Where(node => !ignoreSet.Contains(node))
                .Select(node => ExpandRect(new Rect(node.GetPosition().position, GetNodeSize(node)), CollisionPadding))
                .ToList() ?? new List<Rect>();

            var candidateRect = new Rect(candidate, size);
            var pass = 0;

            while (occupied.Any(rect => rect.Overlaps(candidateRect)))
            {
                pass++;
                candidate.y += CollisionStepY;

                if (pass % 4 == 0)
                {
                    candidate.x += CollisionStepX;
                }

                candidate = Snap(candidate);
                candidateRect.position = candidate;
            }

            return candidate;
        }

        private static Rect ExpandRect(Rect rect, float padding)
        {
            rect.xMin -= padding;
            rect.yMin -= padding;
            rect.xMax += padding;
            rect.yMax += padding;
            return rect;
        }

        private static Vector2 GetNodeSize(Node node)
        {
            if (node == null)
            {
                return DefaultDialogSize;
            }

            var rect = node.GetPosition();
            if (rect.width > 0f && rect.height > 0f)
            {
                return rect.size;
            }

            return node switch
            {
                ChoiceNodeView => DefaultChoiceSize,
                ActionNodeView => DefaultActionSize,
                StartNodeView => DefaultBoundarySize,
                EndNodeView => DefaultBoundarySize,
                _ => DefaultDialogSize,
            };
        }

        private static Vector2 Snap(Vector2 value)
        {
            return new Vector2(
                Mathf.Round(value.x / GridSnap) * GridSnap,
                Mathf.Round(value.y / GridSnap) * GridSnap);
        }
    }
}
