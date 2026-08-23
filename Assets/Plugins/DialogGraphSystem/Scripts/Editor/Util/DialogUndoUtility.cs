using UnityEditor;
using UnityEngine;
using DialogSystem.Runtime.Models;
using System.Collections.Generic;

namespace DialogSystem.EditorTools.Util
{
    public static class DialogUndoUtility
    {
        /// <summary>
        /// Records an undo snapshot for the DialogGraph root.
        /// Use this before structural changes: adding/removing nodes, links, etc.
        /// </summary>
        public static void RecordGraph(string label, DialogGraph graph)
        {
            if (graph == null) return;
            Undo.RegisterCompleteObjectUndo(graph, label);
        }

        /// <summary>
        /// Records an undo snapshot for the graph root plus every authored node sub-asset.
        /// Use this before broad structural changes that can touch multiple node objects.
        /// </summary>
        public static void RecordGraphHierarchy(string label, DialogGraph graph)
        {
            if (graph == null) return;

            var targets = new List<Object>();
            AddUndoTarget(targets, graph);
            AddUndoTargets(targets, graph.nodes);
            AddUndoTargets(targets, graph.choiceNodes);
            AddUndoTargets(targets, graph.actionNodes);
            AddUndoTargets(targets, graph.conditionNodes);
            AddUndoTargets(targets, graph.variableMutationNodes);
            AddUndoTargets(targets, graph.graphJumpNodes);
            AddUndoTargets(targets, graph.outcomeNodes);

            Undo.RegisterCompleteObjectUndo(targets.ToArray(), label);
        }

        /// <summary>
        /// Records an undo snapshot for a single node ScriptableObject.
        /// Use this for small, local changes (text, speaker, flags…).
        /// </summary>
        public static void RecordNode(string label, ScriptableObject node)
        {
            if (node == null) return;
            Undo.RecordObject(node, label);
        }

        /// <summary>
        /// Records undo for both the graph and a node in a single step.
        /// Helpful when a node change also affects graph data (links, lists, etc.).
        /// </summary>
        public static void RecordGraphAndNode(string label, DialogGraph graph, ScriptableObject node)
        {
            if (graph == null && node == null) return;

            if (graph != null && node != null)
            {
                Undo.RegisterCompleteObjectUndo(new Object[] { graph, node }, label);
            }
            else if (graph != null)
            {
                Undo.RegisterCompleteObjectUndo(graph, label);
            }
            else
            {
                Undo.RecordObject(node, label);
            }
        }

        /// <summary>
        /// Records undo for the graph and a set of node sub-assets in a single step.
        /// Use this when a graph mutation also changes serialized state on one or more nodes.
        /// </summary>
        public static void RecordGraphAndNodes(string label, DialogGraph graph, IEnumerable<ScriptableObject> nodes)
        {
            var targets = new List<Object>();
            AddUndoTarget(targets, graph);
            AddUndoTargets(targets, nodes);

            if (targets.Count == 0)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(targets.ToArray(), label);
        }

        /// <summary>
        /// Use when creating a new node sub-asset and adding it to the graph.
        /// </summary>
        public static void RegisterCreatedNode(string label, DialogGraph graph, ScriptableObject node)
        {
            if (node == null) return;

            Undo.RegisterCreatedObjectUndo(node, label);

            if (graph != null)
            {
                Undo.RegisterCompleteObjectUndo(graph, label);
            }
        }

        /// <summary>
        /// Use when deleting a node sub-asset from the graph.
        /// Wraps graph snapshot + Undo.DestroyObjectImmediate.
        /// </summary>
        public static void DestroyNodeWithUndo(string label, DialogGraph graph, ScriptableObject node)
        {
            if (node == null) return;

            if (graph != null)
            {
                Undo.RegisterCompleteObjectUndo(graph, label);
            }

            Undo.DestroyObjectImmediate(node);
        }

        private static void AddUndoTarget(List<Object> targets, Object target)
        {
            if (targets == null || target == null || targets.Contains(target))
            {
                return;
            }

            targets.Add(target);
        }

        private static void AddUndoTargets<T>(List<Object> targets, IEnumerable<T> source)
            where T : Object
        {
            if (targets == null || source == null)
            {
                return;
            }

            foreach (var entry in source)
            {
                AddUndoTarget(targets, entry);
            }
        }
    }
}