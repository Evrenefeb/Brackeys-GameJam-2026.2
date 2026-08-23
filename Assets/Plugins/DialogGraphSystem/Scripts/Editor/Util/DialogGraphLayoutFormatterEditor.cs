#if UNITY_EDITOR
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Utils
{
    public static class DialogGraphLayoutFormatterEditor
    {
        #region ---------------- Settings ----------------

        public enum FormatterMode
        {
            /// <summary>Move node positions only. Existing manual reroute metadata is left untouched.</summary>
            FormatNodesOnlyPreserveReroutes,
            /// <summary>Explicit mode: remove redundant points only from simple forward routes.</summary>
            CleanupSimpleForwardReroutes,
            /// <summary>Explicit destructive mode: clear all editor reroute metadata.</summary>
            ClearAllReroutes,
            /// <summary>Explicit experimental mode reserved for basic loop route work.</summary>
            ExperimentalBasicLoopRouting
        }

        public enum RerouteManagementMode
        {
            /// <summary> Shift points based on the average movement of the connected nodes. </summary>
            PreserveAndShift,
            /// <summary> Remove points that are now redundant (near-linear or overlapping nodes). </summary>
            CleanupRedundant,
            /// <summary> Clear all reroute points to start with clean lines. </summary>
            ResetAll
        }

        public sealed class Settings
        {
            // Used only when Start node is missing from the view.
            public float StartX = -500f;
            public float StartY = 0f;

            public float XSpacing = 500f;
            public float XPadding = 120f; // Dynamic padding between nodes.
            public float YStep = 500f;
            public float Snap = 50f;

            public bool PreferStartRoot = true;
            public bool FormatDisconnectedLinkedComponents = true;

            // Orders Choice branches using the vertical order of output ports.
            public bool OrderChoiceBranchesByOutputPort = true;

            // For non-choice multi-output nodes, preserve the current child vertical order where possible.
            public bool PreserveManualOrderForNonChoiceBranches = true;

            // Centers a parent above/between its child branches.
            public bool CenterParentsOverChildren = true;

            // Keeps Start node exactly where the user placed it.
            public bool KeepStartFixed = true;

            // End is placed after the deepest incoming branch and centered back to the root lane.
            public bool KeepEndOnRootLane = true;

            // Extra vertical distance between disconnected linked components.
            public float DisconnectedComponentYOffset = 1000f;

            // Prevents two nodes in the same X layer from landing on the same Y position.
            public bool ResolveSameLayerCollisions = true;

            // Keeps the formatted subgraph centered on its previous footprint.
            public bool PreserveGroupBoundsCenter = true;

            // Reroute (Link) point management
            public FormatterMode Mode = FormatterMode.FormatNodesOnlyPreserveReroutes;
            public bool ManageReroutePoints = false;
            public RerouteManagementMode RerouteMode = RerouteManagementMode.PreserveAndShift;
        }

        public static readonly Settings DefaultSettings = new Settings();

        #endregion

        #region ---------------- Internal Data ----------------

        private const float FallbackNodeWidth = 400f;
        private const float FallbackNodeHeight = 240f;
        private const float FallbackBoundaryWidth = 200f;
        private const float FallbackBoundaryHeight = 120f;
        private const float FallbackActionWidth = 320f;
        private const float FallbackActionHeight = 240f;
        private const float FallbackConditionWidth = 340f;
        private const float FallbackConditionHeight = 220f;
        private const float SameLayerVerticalPadding = 90f;
        private const float CrossLayerVerticalPadding = 70f;
        private const float BranchRootVerticalPadding = 140f;
        private const float PositionEpsilon = 0.01f;
        private const int MaxGlobalOverlapPasses = 8;
        private static readonly bool LayoutDiagnosticsEnabled = false;
        // When true, always logs a one-line summary of All/Layout/Excluded edges.
        // Set to false once the back-edge fix is validated.
        private const bool LayoutConstraintDiagnosticsEnabled = false;
        // ------------------------------------------------------------------ //
        //  Layout edge classification.                                        //
        //  Edges are classified once during BuildLayoutConstraintEdges() and  //
        //  must not be reclassified later.                                    //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Describes why an edge is (or is not) part of the layout constraint graph.
        /// Only Tree, Merge, and safe Cross edges have AffectsLayout == true.
        /// </summary>
        public enum LayoutEdgeKind
        {
            /// <summary>Primary tree edge from BuildPrimaryTree; always a layout edge.</summary>
            Tree,
            /// <summary>Forward edge into a node with 2+ forward incoming; layout edge.</summary>
            Merge,
            /// <summary>Forward edge that is not the tree parent and target has only one forward incoming; visual only.</summary>
            Cross,
            /// <summary>Edge pointing to a tree ancestor; visual only.</summary>
            Back,
            /// <summary>Edge that would close a cycle in the layout constraint graph; visual only.</summary>
            Cycle,
            /// <summary>Edge whose source and target are the same node; visual only.</summary>
            SelfLoop,
        }

        private sealed class EdgeInfo
        {
            public readonly string FromGuid;
            public readonly string ToGuid;
            public readonly int FromPortIndex;

            public readonly float OutputPortX;
            public readonly float OutputPortY;

            // ---- Set by BuildLayoutConstraintEdges ----
            /// <summary>
            /// True if this edge belongs to the layout constraint graph (acyclic, forward).
            /// False for back/cycle/self-loop/cross edges.
            /// ONLY edges with AffectsLayout == true are added to outMap / inMap / edgeLookup.
            /// </summary>
            public bool AffectsLayout;

            /// <summary>Classification assigned during cycle-safe BFS traversal.</summary>
            public LayoutEdgeKind Kind;

            public EdgeInfo(string fromGuid, string toGuid, int fromPortIndex, float outputPortX, float outputPortY)
            {
                FromGuid = fromGuid;
                ToGuid = toGuid;
                FromPortIndex = fromPortIndex;
                OutputPortX = outputPortX;
                OutputPortY = outputPortY;
                AffectsLayout = false;
                Kind = LayoutEdgeKind.Cross;
            }
        }

        #endregion

        #region ---------------- Public API ----------------

        /// <summary>
        /// Formats only nodes that participate in at least one valid edge.
        /// Unlinked nodes remain untouched.
        /// Returns true only when at least one node position changed.
        /// </summary>
        public static bool FormatLinkedSubgraphOnly(DialogGraphView view, Settings s, string preservedNodeGuid = null)
        {
            if (view == null)
                return false;

            if (s == null)
                s = DefaultSettings;

            List<Node> nodes = view.nodes.ToList().OfType<Node>().ToList();
            List<Edge> edges = view.edges.ToList().OfType<Edge>().ToList();

            string startGuid = GetBoundaryGuid<StartNodeView>(nodes);
            string endGuid = GetBoundaryGuid<EndNodeView>(nodes);

            var guidToNode = new Dictionary<string, Node>(StringComparer.Ordinal);

            foreach (Node node in nodes)
            {
                string guid = GetNodeGuid(node);

                if (string.IsNullOrWhiteSpace(guid))
                    continue;

                if (!guidToNode.ContainsKey(guid))
                    guidToNode.Add(guid, node);
            }

            if (guidToNode.Count == 0)
                return false;

            NormalizeInvalidViewRectsFromSerializedData(view, guidToNode);
            Dictionary<string, Vector2> positionsBefore = SnapshotNodePositions(guidToNode);
            List<EdgeInfo> edgeInfos = BuildEdgeInfoList(edges, guidToNode);

            if (edgeInfos.Count == 0)
            {
                PlaceDetachedEndNode(view, guidToNode, startGuid, endGuid, s, preservedNodeGuid);
                return HaveNodePositionsChanged(positionsBefore, guidToNode);
            }

            // ---------------------------------------------------------------- //
            //  Step 1: Classify every edge into the layout constraint graph.   //
            //  After this call each EdgeInfo carries AffectsLayout + Kind.     //
            //  Back/cycle edges are excluded from outMap/inMap/edgeLookup so  //
            //  that ALL downstream passes see only an acyclic forward graph.   //
            // ---------------------------------------------------------------- //
            BuildLayoutConstraintEdges(edgeInfos, startGuid);
            LogLayoutConstraintDiagnostics(edgeInfos);

            var linked = new HashSet<string>(StringComparer.Ordinal);
            var outMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var inMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var undirected = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var edgeLookup = new Dictionary<string, List<EdgeInfo>>(StringComparer.Ordinal);

            // BuildGraphMaps now routes only AffectsLayout==true edges into
            // outMap / inMap / edgeLookup.  The undirected map still receives
            // all edges for connected-component detection.
            BuildGraphMaps(
                edgeInfos,
                linked,
                outMap,
                inMap,
                undirected,
                edgeLookup
            );

            if (linked.Count == 0)
            {
                PlaceDetachedEndNode(view, guidToNode, startGuid, endGuid, s, preservedNodeGuid);
                return HaveNodePositionsChanged(positionsBefore, guidToNode);
            }

            List<HashSet<string>> components = ConnectedComponents(linked, undirected);
            var formatted = new HashSet<string>(StringComparer.Ordinal);
            bool nodePositionsChanged = false;

            bool hasLinkedStart = !string.IsNullOrWhiteSpace(startGuid) &&
                                  linked.Contains(startGuid) &&
                                  guidToNode.ContainsKey(startGuid);

            if (hasLinkedStart && s.PreferStartRoot)
            {
                HashSet<string> startComponent = components.FirstOrDefault(component => component.Contains(startGuid));

                if (startComponent != null && startComponent.Count > 0)
                {
                    Vector2 startPosition = guidToNode[startGuid].GetPosition().position;
                    Vector2 anchor = new Vector2(
                        Snap(startPosition.x, s.Snap),
                        Snap(startPosition.y, s.Snap)
                    );

                    nodePositionsChanged |= FormatOneGroup(
                        view,
                        guidToNode,
                        outMap,
                        inMap,
                        edgeLookup,
                        startComponent,
                        startGuid,
                        anchor,
                        startGuid,
                        endGuid,
                        s,
                        preservedNodeGuid
                    );

                    foreach (string guid in startComponent)
                        formatted.Add(guid);
                }

                if (s.FormatDisconnectedLinkedComponents)
                {
                    int disconnectedIndex = 0;

                    foreach (HashSet<string> component in components)
                    {
                        if (component.All(formatted.Contains))
                            continue;

                        Vector2 componentAnchor = ComputeCentroid(component, guidToNode);
                        componentAnchor.y += disconnectedIndex * s.DisconnectedComponentYOffset;

                        string root = FindBestRoot(component, inMap, startGuid);

                        nodePositionsChanged |= FormatOneGroup(
                            view,
                            guidToNode,
                            outMap,
                            inMap,
                            edgeLookup,
                            component,
                            root,
                            componentAnchor,
                            startGuid,
                            endGuid,
                            s,
                            preservedNodeGuid
                        );

                        foreach (string guid in component)
                            formatted.Add(guid);

                        disconnectedIndex++;
                    }
                }
            }
            else
            {
                int componentIndex = 0;

                foreach (HashSet<string> component in components)
                {
                    Vector2 anchor = ComputeCentroid(component, guidToNode);
                    anchor.y += componentIndex * s.DisconnectedComponentYOffset;

                    if (!string.IsNullOrWhiteSpace(startGuid) &&
                        component.Contains(startGuid) &&
                        guidToNode.TryGetValue(startGuid, out Node startNode))
                        anchor = startNode.GetPosition().position;

                    string root = FindBestRoot(component, inMap, startGuid);

                    nodePositionsChanged |= FormatOneGroup(
                        view,
                        guidToNode,
                        outMap,
                        inMap,
                        edgeLookup,
                        component,
                        root,
                        anchor,
                        startGuid,
                        endGuid,
                        s,
                        preservedNodeGuid
                    );

                    componentIndex++;
                }
            }

            if (string.IsNullOrWhiteSpace(endGuid) || !linked.Contains(endGuid))
                nodePositionsChanged |= PlaceDetachedEndNode(view, guidToNode, startGuid, endGuid, s, preservedNodeGuid);

            return nodePositionsChanged;
        }

        #endregion

        #region ---------------- Graph Map Building ----------------

        // ================================================================== //
        //  AUTHORITATIVE LAYOUT CONSTRAINT GRAPH                              //
        //                                                                     //
        //  BuildLayoutConstraintEdges() is the single point that decides      //
        //  whether an edge affects node placement.  Every downstream pass     //
        //  (layer assignment, relaxation, centering, collision, end-node)     //
        //  operates only on edges that were admitted here.                    //
        //                                                                     //
        //  Rules                                                              //
        //   • Self-loops   → SelfLoop,  AffectsLayout = false                //
        //   • Adds cycle   → Cycle,     AffectsLayout = false                //
        //   • Otherwise    → Cross,     AffectsLayout = true                 //
        //     (Tree / Merge refinement happens inside BuildPrimaryTree and    //
        //      ShouldEdgeAffectLayer, which now operate on the clean graph.)  //
        //                                                                     //
        //  Traversal order (deterministic, start-biased)                     //
        //   1. BFS from Start node if present.                                //
        //   2. Remaining nodes in GUID-alphabetical order.                   //
        //   For each node: outgoing edges are tested by ascending            //
        //   fromPortIndex → outputPortY → outputPortX → toGuid.              //
        // ================================================================== //

        /// <summary>
        /// Classifies every edge in <paramref name="allEdges"/> by setting
        /// <see cref="EdgeInfo.AffectsLayout"/> and <see cref="EdgeInfo.Kind"/>.
        ///
        /// Must be called before <see cref="BuildGraphMaps"/>.
        /// Does NOT modify DialogGraph.links or any serialised data.
        /// </summary>
        private static void BuildLayoutConstraintEdges(
            List<EdgeInfo> allEdges,
            string startGuid)
        {
            if (allEdges == null || allEdges.Count == 0)
                return;

            // Current layout adjacency (from → {to}).  Used only for cycle detection.
            var layoutAdj = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            // ---- Build per-node outgoing edge lookup (all edges) ---- //
            var allOutEdges = new Dictionary<string, List<EdgeInfo>>(StringComparer.Ordinal);
            var allGuids   = new HashSet<string>(StringComparer.Ordinal);

            foreach (EdgeInfo e in allEdges)
            {
                if (string.IsNullOrWhiteSpace(e.FromGuid) || string.IsNullOrWhiteSpace(e.ToGuid))
                    continue;

                allGuids.Add(e.FromGuid);
                allGuids.Add(e.ToGuid);

                if (!allOutEdges.TryGetValue(e.FromGuid, out List<EdgeInfo> lst))
                {
                    lst = new List<EdgeInfo>();
                    allOutEdges[e.FromGuid] = lst;
                }
                lst.Add(e);
            }

            // ---------------------------------------------------------------- //
            //  CRITICAL: nodes are processed in the BFS wave rooted at Start.  //
            //  A node must NOT be enqueued until it is first REACHED via a     //
            //  safe forward edge.  Pre-enqueueing all nodes alphabetically     //
            //  would allow a back-edge source to be processed before its       //
            //  target is connected, defeating the cycle check.                 //
            //                                                                  //
            //  Algorithm:                                                      //
            //  1. ProcessComponent(seed) — BFS from seed; only enqueues a     //
            //     node when it is reached by a layout-safe edge.               //
            //  2. Call ProcessComponent(Start) first.                          //
            //  3. For every node not yet reached, call ProcessComponent(g)     //
            //     in alphabetical order (handles disconnected subgraphs).      //
            // ---------------------------------------------------------------- //

            var processedNodes = new HashSet<string>(StringComparer.Ordinal);

            void ProcessComponent(string seed)
            {
                if (string.IsNullOrWhiteSpace(seed) || !allGuids.Contains(seed))
                    return;

                // Mark seed as processed so it is not restarted.
                processedNodes.Add(seed);

                var bfsQueue = new Queue<string>();
                bfsQueue.Enqueue(seed);

                while (bfsQueue.Count > 0)
                {
                    string current = bfsQueue.Dequeue();

                    if (!allOutEdges.TryGetValue(current, out List<EdgeInfo> outEdges))
                        continue;

                    // Process this node's outgoing edges in deterministic order:
                    // fromPortIndex → outputPortY → outputPortX → toGuid
                    List<EdgeInfo> sorted = outEdges
                        .OrderBy(e => e.FromPortIndex < 0 ? int.MaxValue : e.FromPortIndex)
                        .ThenBy(e => e.OutputPortY)
                        .ThenBy(e => e.OutputPortX)
                        .ThenBy(e => e.ToGuid, StringComparer.Ordinal)
                        .ToList();

                    foreach (EdgeInfo edge in sorted)
                    {
                        string from = edge.FromGuid;
                        string to   = edge.ToGuid;

                        // --- Self-loop ----------------------------------------- //
                        if (string.Equals(from, to, StringComparison.Ordinal))
                        {
                            edge.AffectsLayout = false;
                            edge.Kind = LayoutEdgeKind.SelfLoop;
                            continue;
                        }

                        // --- Cycle check --------------------------------------- //
                        // Adding from→to is safe only if 'to' cannot already reach
                        // 'from' through the edges already admitted to layoutAdj.
                        if (WouldCreateCycle(layoutAdj, from, to))
                        {
                            edge.AffectsLayout = false;
                            edge.Kind = LayoutEdgeKind.Cycle;
                            continue;
                        }

                        // --- Safe forward edge ---------------------------------- //
                        if (!layoutAdj.TryGetValue(from, out HashSet<string> adjSet))
                        {
                            adjSet = new HashSet<string>(StringComparer.Ordinal);
                            layoutAdj[from] = adjSet;
                        }
                        adjSet.Add(to);

                        edge.AffectsLayout = true;
                        edge.Kind = LayoutEdgeKind.Cross; // refined later by ShouldEdgeAffectLayer

                        // Enqueue the target the FIRST time it is reached so its own
                        // outgoing edges are evaluated only after the forward path to
                        // it has been established in layoutAdj.
                        if (processedNodes.Add(to))
                            bfsQueue.Enqueue(to);
                    }
                }
            }

            // Phase 1: process the component containing Start first.
            if (!string.IsNullOrWhiteSpace(startGuid) && allGuids.Contains(startGuid))
                ProcessComponent(startGuid);

            // Phase 2: process any remaining nodes (disconnected subgraphs) alphabetically.
            foreach (string g in allGuids.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!processedNodes.Contains(g))
                    ProcessComponent(g);
            }
        }

        /// <summary>
        /// Returns true if adding the directed edge <paramref name="from"/> →
        /// <paramref name="to"/> to <paramref name="adjacency"/> would create a cycle,
        /// i.e. if <paramref name="to"/> can already reach <paramref name="from"/>
        /// through the current layout constraint graph.
        /// </summary>
        private static bool WouldCreateCycle(
            Dictionary<string, HashSet<string>> adjacency,
            string from,
            string to)
        {
            // Quick exit: if 'to' has no outgoing layout edges yet it cannot reach 'from'.
            if (!adjacency.ContainsKey(to))
                return false;

            // DFS from 'to'; if we reach 'from', a cycle would be formed.
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack   = new Stack<string>();
            stack.Push(to);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                if (!visited.Add(current))
                    continue;

                if (string.Equals(current, from, StringComparison.Ordinal))
                    return true;

                if (adjacency.TryGetValue(current, out HashSet<string> neighbors))
                {
                    foreach (string n in neighbors)
                        stack.Push(n);
                }
            }

            return false;
        }

        /// <summary>
        /// Emits a Debug.Log summary of the edge classification result.
        /// Controlled by <see cref="LayoutConstraintDiagnosticsEnabled"/>.
        /// </summary>
        private static void LogLayoutConstraintDiagnostics(List<EdgeInfo> allEdges)
        {
            if (!LayoutConstraintDiagnosticsEnabled || allEdges == null)
                return;

            int total     = allEdges.Count;
            int layout    = 0;
            int excluded  = 0;

            var excludedLines = new System.Text.StringBuilder();

            foreach (EdgeInfo e in allEdges)
            {
                if (e.AffectsLayout)
                {
                    layout++;
                }
                else
                {
                    excluded++;
                    excludedLines.AppendLine(
                        $"    Excluded: {e.FromGuid} → {e.ToGuid}  [Kind={e.Kind}]");
                }
            }

            string summary =
                $"[LayoutConstraintGraph] AllEdges={total}  LayoutEdges={layout}  Excluded={excluded}\n" +
                excludedLines;

            Debug.Log(summary);
        }

        private static List<EdgeInfo> BuildEdgeInfoList(
            List<Edge> edges,
            Dictionary<string, Node> guidToNode)
        {
            var result = new List<EdgeInfo>();

            foreach (Edge edge in edges)
            {
                if (edge == null || edge.output == null || edge.input == null)
                    continue;

                if (edge.output.node == null || edge.input.node == null)
                    continue;

                string fromGuid = GetNodeGuid(edge.output.node);
                string toGuid = GetNodeGuid(edge.input.node);

                if (string.IsNullOrWhiteSpace(fromGuid) || string.IsNullOrWhiteSpace(toGuid))
                    continue;

                if (!guidToNode.ContainsKey(fromGuid) || !guidToNode.ContainsKey(toGuid))
                    continue;

                // FIX: Use graph space coordinates instead of worldBound (screen space)
                var graphView = edge.GetFirstAncestorOfType<GraphView>();
                Vector2 outputCenter = edge.output.layout.center;
                if (graphView != null)
                {
                    outputCenter = edge.output.ChangeCoordinatesTo(graphView.contentViewContainer, edge.output.layout.center);
                }

                result.Add(new EdgeInfo(
                    fromGuid,
                    toGuid,
                    GetOutputPortIndexForLayout(edge),
                    outputCenter.x,
                    outputCenter.y
                ));
            }

            return result;
        }

        private static int GetOutputPortIndexForLayout(Edge edge)
        {
            Port output = edge?.output;
            if (output?.node == null)
                return -1;

            if (output.node is ChoiceNodeView choiceNodeView)
            {
                int choicePortIndex = choiceNodeView.GetPortIndex(output);
                if (choicePortIndex >= 0)
                    return choicePortIndex;
            }

            if (output.node is ConditionNodeView conditionNodeView)
            {
                int conditionPortIndex = conditionNodeView.GetPortIndex(output);
                if (conditionPortIndex >= 0)
                    return conditionPortIndex;
            }

            if (edge is DialogGraphEdge dialogGraphEdge && dialogGraphEdge.fromPortIndex >= 0)
                return dialogGraphEdge.fromPortIndex;

            return 0;
        }

        /// <summary>
        /// Populates graph maps from the classified edge list.
        ///
        /// IMPORTANT — edge routing rules:
        ///  • <paramref name="linked"/> and <paramref name="undirected"/> receive ALL edges
        ///    so that connected-component detection still spans visual back-edges.
        ///  • <paramref name="outMap"/>, <paramref name="inMap"/>, and
        ///    <paramref name="edgeLookup"/> receive ONLY edges whose
        ///    <see cref="EdgeInfo.AffectsLayout"/> flag is true.
        ///    Back/cycle/self-loop edges are intentionally excluded here, so every
        ///    downstream layout pass operates on an acyclic constraint graph without
        ///    needing to filter individual edges.
        ///
        /// Call <see cref="BuildLayoutConstraintEdges"/> before this method.
        /// </summary>
        private static void BuildGraphMaps(
            List<EdgeInfo> edgeInfos,
            HashSet<string> linked,
            Dictionary<string, List<string>> outMap,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, HashSet<string>> undirected,
            Dictionary<string, List<EdgeInfo>> edgeLookup)
        {
            foreach (EdgeInfo edgeInfo in edgeInfos)
            {
                linked.Add(edgeInfo.FromGuid);
                linked.Add(edgeInfo.ToGuid);

                // Undirected uses ALL edges so that nodes connected only by a visual
                // back-edge are still grouped in the same connected component.
                AddUndirected(undirected, edgeInfo.FromGuid, edgeInfo.ToGuid);

                // Layout maps are restricted to cycle-free, forward-only edges.
                // Back/cycle/self-loop edges must NOT affect node positioning.
                if (!edgeInfo.AffectsLayout)
                    continue;

                AddUnique(outMap, edgeInfo.FromGuid, edgeInfo.ToGuid);
                AddUnique(inMap, edgeInfo.ToGuid, edgeInfo.FromGuid);

                if (!edgeLookup.TryGetValue(edgeInfo.FromGuid, out List<EdgeInfo> list))
                {
                    list = new List<EdgeInfo>();
                    edgeLookup.Add(edgeInfo.FromGuid, list);
                }

                bool alreadyExists = false;

                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].ToGuid, edgeInfo.ToGuid, StringComparison.Ordinal))
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                    list.Add(edgeInfo);
            }
        }

        #endregion

        #region ---------------- Core Layout ----------------

        private static bool FormatOneGroup(
            DialogGraphView view,
            Dictionary<string, Node> guidToNode,
            Dictionary<string, List<string>> outMap,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, List<EdgeInfo>> edgeLookup,
            HashSet<string> group,
            string rootGuid,
            Vector2 anchorPos,
            string startGuid,
            string endGuid,
            Settings s,
            string preservedNodeGuid)
        {
            if (group == null || group.Count == 0)
                return false;

            Dictionary<string, Vector2> nodeSizes = BuildNodeSizeMap(group, guidToNode);

            string root = !string.IsNullOrWhiteSpace(rootGuid) && group.Contains(rootGuid)
                ? rootGuid
                : group.OrderBy(x => x, StringComparer.Ordinal).First();

            float rootX = Snap(anchorPos.x, s.Snap);
            float rootY = Snap(anchorPos.y, s.Snap);

            // 1) Build a primary layout tree.
            // outMap / inMap / edgeLookup contain only layout-safe (acyclic, forward) edges.
            // Back/cycle edges have already been removed by BuildLayoutConstraintEdges()
            // and are not present in any of these maps.  No per-call edge filtering is
            // required inside the tree / layer / Y passes below.
            var parent = new Dictionary<string, string>(StringComparer.Ordinal);
            var treeChildren = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);

            BuildPrimaryTree(
                root,
                group,
                guidToNode,
                outMap,
                edgeLookup,
                treeChildren,
                parent,
                visited,
                s
            );

            // Some nodes may belong to the same undirected component but not be reachable
            // from the chosen root through outgoing edges. Layout them as additional roots.
            foreach (string guid in group
                .OrderBy(x => GetCurrentPositionX(x, guidToNode))
                .ThenBy(x => GetCurrentPositionY(x, guidToNode))
                .ThenBy(x => x, StringComparer.Ordinal))
            {
                if (visited.Contains(guid))
                    continue;

                BuildPrimaryTree(
                    guid,
                    group,
                    guidToNode,
                    outMap,
                    edgeLookup,
                    treeChildren,
                    parent,
                    visited,
                    s
                );
            }

            // 2) Assign X layers.
            Dictionary<string, int> layer = AssignLayers(root, group, outMap, inMap, parent);

            // 3) Assign Y positions by subtree lanes.
            Dictionary<string, float> y = AssignSubtreeY(root, group, treeChildren, guidToNode, rootY, s);

            AddFallbackYValues(group, y, rootY, s);

            // Keep immediate branch roots visually separated before shared nodes are centered.
            SpaceBranchRoots(group, treeChildren, guidToNode, nodeSizes, y, s);

            // 4) Center merge/shared nodes between their incoming parents.
            RelaxSharedNodeY(group, inMap, parent, layer, y, root, rootY, s, endGuid);

            // 5) Keep post-merge single-child chains aligned where safe.
            AlignSingleChildChains(group, outMap, inMap, parent, layer, y, s);

            // 6) End node behavior.
            if (!string.IsNullOrWhiteSpace(endGuid) &&
                group.Contains(endGuid) &&
                guidToNode.ContainsKey(endGuid))
            {
                int endLayer = ComputeEndLayer(group, inMap, parent, layer, endGuid);
                layer[endGuid] = endLayer;

                y[endGuid] = ComputeEndY(endGuid, group, inMap, parent, layer, y, rootY, s);
            }

            // 7) Convert layers and lanes to final positions.
            Dictionary<string, Vector2> desiredPos = BuildDesiredPositions(
                group,
                layer,
                y,
                guidToNode,
                nodeSizes,
                rootX,
                rootY,
                s
            );

            Dictionary<string, Vector2> desiredBeforeOffset = CopyPositions(desiredPos);

            // Start should remain exactly where the user placed it.
            if (s.KeepStartFixed &&
                !string.IsNullOrWhiteSpace(startGuid) &&
                group.Contains(startGuid) &&
                guidToNode.TryGetValue(startGuid, out Node startNode))
            {
                Vector2 startPos = GetComparableNodePosition(GetGraphOwner(view), startGuid, startNode);
                desiredPos[startGuid] = new Vector2(
                    Snap(startPos.x, s.Snap),
                    Snap(startPos.y, s.Snap)
                );
            }

            if (s.ResolveSameLayerCollisions)
                ResolveCollisions(desiredPos, nodeSizes, s, startGuid, endGuid);

            if (!TryOffsetDesiredPositionsToPreserveNode(guidToNode, desiredPos, preservedNodeGuid, s) &&
                !ShouldSkipGroupCenterOffsetForAnchoredStart(group, root, startGuid, s) &&
                s.PreserveGroupBoundsCenter)
            {
                OffsetDesiredPositionsToMatchCurrentBounds(guidToNode, desiredPos, s);
            }

            Dictionary<string, Vector2> desiredAfterOffset = CopyPositions(desiredPos);

            NormalizeDesiredPositions(desiredPos, s);

            if (s.ResolveSameLayerCollisions)
                ResolveCollisions(desiredPos, nodeSizes, s, startGuid, endGuid);

            NormalizeDesiredPositions(desiredPos, s);
            LogLayoutDiagnostics(
                view,
                group,
                root,
                guidToNode,
                edgeLookup,
                outMap,
                inMap,
                parent,
                layer,
                nodeSizes,
                desiredBeforeOffset,
                desiredAfterOffset,
                desiredPos);

            // 8) Apply positions and, only in explicit non-default modes, manage link points.
            return ApplyPositionsAndManagePoints(view, guidToNode, desiredPos, inMap, parent, s);
            }

            private static void AddFallbackYValues(
            HashSet<string> group,
            Dictionary<string, float> y,
            float rootY,
            Settings s)
            {
            float fallbackY = rootY + s.YStep;

            foreach (string guid in group.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (y.ContainsKey(guid))
                    continue;

                y[guid] = Snap(fallbackY, s.Snap);
                fallbackY += s.YStep;
            }
            }

            private static Dictionary<string, Vector2> BuildDesiredPositions(
            HashSet<string> group,
            Dictionary<string, int> layer,
            Dictionary<string, float> y,
            Dictionary<string, Node> guidToNode,
            Dictionary<string, Vector2> nodeSizes,
            float rootX,
            float rootY,
            Settings s)
            {
            var desiredPos = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            var layerWidths = new Dictionary<int, float>();

            // Calculate max width for each layer
            foreach (var kv in layer)
            {
                if (!group.Contains(kv.Key)) continue;
                if (!guidToNode.TryGetValue(kv.Key, out Node node)) continue;

                float width = GetNodeSize(kv.Key, node, nodeSizes).x;

                if (!layerWidths.TryGetValue(kv.Value, out float maxWidth) || width > maxWidth)
                {
                    layerWidths[kv.Value] = width;
                }
            }

            // Calculate layer start X offsets
            var layerXOffsets = new Dictionary<int, float>();
            float currentX = rootX;
            int maxLayer = layer.Values.Count > 0 ? layer.Values.Max() : 0;

            for (int i = 0; i <= maxLayer; i++)
            {
                layerXOffsets[i] = Snap(currentX, s.Snap);
                if (layerWidths.TryGetValue(i, out float width))
                {
                    currentX += width + s.XPadding;
                }
                else
                {
                    currentX += s.XSpacing;
                }
            }

            foreach (string guid in group.OrderBy(x => x, StringComparer.Ordinal))
            {
                int nodeLayer = layer.TryGetValue(guid, out int l) ? l : 0;
                float x = layerXOffsets.TryGetValue(nodeLayer, out float lx) ? lx : Snap(rootX + nodeLayer * s.XSpacing, s.Snap);
                float nodeY = y.TryGetValue(guid, out float yy) ? yy : rootY;

                desiredPos[guid] = new Vector2(x, Snap(nodeY, s.Snap));
            }

            return desiredPos;
            }

            private static bool ApplyPositionsAndManagePoints(
                DialogGraphView view,
                Dictionary<string, Node> guidToNode,
                Dictionary<string, Vector2> desiredPos,
                Dictionary<string, List<string>> inMap,
                Dictionary<string, string> primaryParent,
                Settings s)
            {
                if (view == null) return false;

                DialogGraph owner = GetGraphOwner(view);
                var changedNodeGuids = new List<string>();
                foreach (KeyValuePair<string, Vector2> kv in desiredPos)
                {
                    if (!guidToNode.TryGetValue(kv.Key, out Node node))
                        continue;

                    Vector2 currentPosition = GetComparableNodePosition(owner, kv.Key, node);
                    if (!ArePositionsEquivalent(currentPosition, kv.Value))
                        changedNodeGuids.Add(kv.Key);
                }

                if (changedNodeGuids.Count > 0)
                    RecordFormatUndo(view, changedNodeGuids);

                // Apply node positions
                foreach (KeyValuePair<string, Vector2> kv in desiredPos)
                {
                    if (!guidToNode.TryGetValue(kv.Key, out Node node))
                        continue;

                    Rect rect = node.GetPosition();
                    if (ArePositionsEquivalent(rect.position, kv.Value))
                    {
                        SetSerializedNodePosition(owner, kv.Key, kv.Value);
                        continue;
                    }

                    rect = new Rect(kv.Value, IsUsableRect(rect) ? rect.size : GetFallbackSizeForNode(node));
                    node.SetPosition(rect);
                    SetSerializedNodePosition(owner, kv.Key, kv.Value);
                }

                ManageReroutePointsIfExplicit(view, desiredPos, inMap, primaryParent, s);
                return changedNodeGuids.Count > 0;
            }

            private static void ManageReroutePointsIfExplicit(
                DialogGraphView view,
                Dictionary<string, Vector2> desiredPos,
                Dictionary<string, List<string>> inMap,
                Dictionary<string, string> primaryParent,
                Settings s)
            {
                FormatterMode mode = GetEffectiveFormatterMode(s);
                if (mode == FormatterMode.FormatNodesOnlyPreserveReroutes)
                    return;

                DialogGraph owner = GetGraphOwner(view);
                if (owner == null)
                    return;

                if (mode == FormatterMode.ClearAllReroutes)
                {
                    UnityEditor.Undo.RecordObject(owner, "Clear Reroute Points");
                    owner.ClearAllEdgeLayouts();
                    UnityEditor.EditorUtility.SetDirty(owner);
                    foreach (DialogGraphEdge edge in view.edges.ToList().OfType<DialogGraphEdge>())
                        edge.UpdateEdgeControl();
                    return;
                }

                if (mode == FormatterMode.CleanupSimpleForwardReroutes)
                {
                    CleanupSimpleForwardReroutes(view, owner, desiredPos, inMap, primaryParent, s);
                    return;
                }

                // ExperimentalBasicLoopRouting is intentionally opt-in and reserved for a later phase.
                // Phase 1 must not generate or rebuild reroute points.
            }

            private static FormatterMode GetEffectiveFormatterMode(Settings s)
            {
                if (s == null)
                    return FormatterMode.FormatNodesOnlyPreserveReroutes;

                if (s.Mode != FormatterMode.FormatNodesOnlyPreserveReroutes)
                    return s.Mode;

                if (!s.ManageReroutePoints)
                    return FormatterMode.FormatNodesOnlyPreserveReroutes;

                return s.RerouteMode switch
                {
                    RerouteManagementMode.CleanupRedundant => FormatterMode.CleanupSimpleForwardReroutes,
                    RerouteManagementMode.ResetAll => FormatterMode.ClearAllReroutes,
                    _ => FormatterMode.FormatNodesOnlyPreserveReroutes
                };
            }

            private static void CleanupSimpleForwardReroutes(
                DialogGraphView view,
                DialogGraph owner,
                Dictionary<string, Vector2> desiredPos,
                Dictionary<string, List<string>> inMap,
                Dictionary<string, string> primaryParent,
                Settings s)
            {
                bool undoRecorded = false;

                foreach (DialogGraphEdge edge in view.edges.ToList().OfType<DialogGraphEdge>())
                {
                    if (string.IsNullOrEmpty(edge.linkGuid))
                        continue;

                    if (!IsSimpleForwardRerouteCandidate(edge, desiredPos, inMap, primaryParent))
                        continue;

                    EdgeLayoutRecord layout = owner.GetEdgeLayout(edge.linkGuid);
                    if (layout == null || layout.reroutePoints == null || layout.reroutePoints.Count == 0)
                        continue;

                    Vector2 newStart = GetPortCenterInGraph(view, edge.output, edge.fromGuid, desiredPos);
                    Vector2 newEnd = GetPortCenterInGraph(view, edge.input, edge.toGuid, desiredPos);

                    var pList = new List<Vector2> { newStart };
                    pList.AddRange(layout.reroutePoints);
                    pList.Add(newEnd);

                    var resultPoints = new List<Vector2>(layout.reroutePoints);
                    bool changed = false;

                    for (int i = pList.Count - 2; i >= 1; i--)
                    {
                        Vector2 prev = pList[i - 1];
                        Vector2 curr = pList[i];
                        Vector2 next = pList[i + 1];

                        float distToLine = UnityEditor.HandleUtility.DistancePointToLineSegment(curr, prev, next);
                        float distToStart = Vector2.Distance(curr, newStart);
                        float distToEnd = Vector2.Distance(curr, newEnd);

                        if (distToLine < 15f || distToStart < 80f || distToEnd < 80f)
                        {
                            resultPoints.RemoveAt(i - 1);
                            changed = true;
                            pList.RemoveAt(i);
                        }
                    }

                    if (!changed)
                        continue;

                    if (!undoRecorded)
                    {
                        UnityEditor.Undo.RecordObject(owner, "Cleanup Simple Reroutes");
                        undoRecorded = true;
                    }

                    layout.reroutePoints.Clear();
                    layout.reroutePoints.AddRange(resultPoints);
                    UnityEditor.EditorUtility.SetDirty(owner);
                    edge.UpdateEdgeControl();
                }
            }

            private static bool IsSimpleForwardRerouteCandidate(
                DialogGraphEdge edge,
                Dictionary<string, Vector2> desiredPos,
                Dictionary<string, List<string>> inMap,
                Dictionary<string, string> primaryParent)
            {
                if (edge == null ||
                    string.IsNullOrEmpty(edge.fromGuid) ||
                    string.IsNullOrEmpty(edge.toGuid) ||
                    string.Equals(edge.fromGuid, edge.toGuid, StringComparison.Ordinal))
                {
                    return false;
                }

                if (!desiredPos.TryGetValue(edge.fromGuid, out Vector2 fromPos) ||
                    !desiredPos.TryGetValue(edge.toGuid, out Vector2 toPos))
                {
                    return false;
                }

                if (primaryParent == null ||
                    !primaryParent.TryGetValue(edge.toGuid, out string parentGuid) ||
                    !string.Equals(parentGuid, edge.fromGuid, StringComparison.Ordinal))
                {
                    return false;
                }

                if (toPos.x <= fromPos.x)
                    return false;

                if (inMap != null && inMap.TryGetValue(edge.toGuid, out List<string> incoming))
                {
                    int incomingCount = incoming
                        .Where(guid => desiredPos.ContainsKey(guid))
                        .Distinct(StringComparer.Ordinal)
                        .Count();

                    if (incomingCount > 1)
                        return false;
                }

                return true;
            }

            private static void RecordFormatUndo(DialogGraphView view, List<string> changedNodeGuids)
            {
                DialogGraph owner = GetGraphOwner(view);
                if (owner == null)
                    return;

                var targets = new List<UnityEngine.Object> { owner };

                foreach (string guid in changedNodeGuids)
                {
                    ScriptableObject nodeData = FindNodeDataByGuid(owner, guid);
                    if (nodeData != null && !targets.Contains(nodeData))
                        targets.Add(nodeData);
                }

                UnityEditor.Undo.RegisterCompleteObjectUndo(targets.ToArray(), "Format Layout");
            }

            private static DialogGraph GetGraphOwner(DialogGraphView view)
            {
                if (view == null)
                    return null;

                DialogGraph edgeOwner = view.edges
                    .ToList()
                    .OfType<DialogGraphEdge>()
                    .Select(edge => edge.Owner)
                    .FirstOrDefault(owner => owner != null);

                if (edgeOwner != null)
                    return edgeOwner;

                return string.IsNullOrWhiteSpace(view.graphId)
                    ? null
                    : DialogGraphAssetPaths.LoadGraphAsset(view.graphId);
            }

            private static ScriptableObject FindNodeDataByGuid(DialogGraph owner, string guid)
            {
                if (owner == null || string.IsNullOrEmpty(guid))
                    return null;

                ScriptableObject result = owner.nodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
                if (result != null)
                    return result;

                result = owner.choiceNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
                if (result != null)
                    return result;

                result = owner.actionNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
                if (result != null)
                    return result;

                result = owner.conditionNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
                if (result != null)
                    return result;

                return owner.variableMutationNodes?.FirstOrDefault(node => node != null && node.GetGuid() == guid);
            }

            private static Vector2 GetPortCenterInGraph(
                DialogGraphView view, 
                Port port, 
                string nodeGuid, 
                Dictionary<string, Vector2> nodePositions)
            {
                if (port == null || view == null) return Vector2.zero;

                // Get port center relative to node
                Vector2 portLocalToNode = port.ChangeCoordinatesTo(port.node, port.layout.center);
            
                // If we have a calculated position for this node, use it as the base
                if (!string.IsNullOrEmpty(nodeGuid) && nodePositions.TryGetValue(nodeGuid, out Vector2 nodePos))
                {
                    return nodePos + portLocalToNode;
                }

                // Fallback: use current visual position in graph space
                return port.ChangeCoordinatesTo(view.contentViewContainer, port.layout.center);
            }

            private static void ApplyPositions(
            Dictionary<string, Node> guidToNode,
            Dictionary<string, Vector2> desiredPos)
            {
            foreach (KeyValuePair<string, Vector2> kv in desiredPos)
            {
                if (!guidToNode.TryGetValue(kv.Key, out Node node))
                    continue;

                Rect rect = node.GetPosition();
                rect.position = kv.Value;
                node.SetPosition(rect);
            }
            }

        private static bool ShouldSkipGroupCenterOffsetForAnchoredStart(
            HashSet<string> group,
            string root,
            string startGuid,
            Settings s)
        {
            return s != null &&
                   s.KeepStartFixed &&
                   !string.IsNullOrWhiteSpace(startGuid) &&
                   group != null &&
                   group.Contains(startGuid) &&
                   string.Equals(root, startGuid, StringComparison.Ordinal);
        }

        private static void OffsetDesiredPositionsToMatchCurrentBounds(
            Dictionary<string, Node> guidToNode,
            Dictionary<string, Vector2> desiredPos,
            Settings s)
        {
            if (guidToNode == null || desiredPos == null || desiredPos.Count == 0)
                return;

            if (!TryGetCurrentBounds(guidToNode, desiredPos.Keys, out Rect currentBounds))
                return;

            if (!TryGetDesiredBounds(guidToNode, desiredPos, out Rect desiredBounds))
                return;

            Vector2 delta = currentBounds.center - desiredBounds.center;
            if (delta.sqrMagnitude <= 0.001f)
                return;

            var keys = desiredPos.Keys.ToList();
            foreach (string guid in keys)
                desiredPos[guid] = SnapVector(desiredPos[guid] + delta, s?.Snap ?? 0f);
        }

        private static bool TryOffsetDesiredPositionsToPreserveNode(
            Dictionary<string, Node> guidToNode,
            Dictionary<string, Vector2> desiredPos,
            string preservedNodeGuid,
            Settings s)
        {
            if (guidToNode == null || desiredPos == null || string.IsNullOrWhiteSpace(preservedNodeGuid))
                return false;

            if (!desiredPos.TryGetValue(preservedNodeGuid, out Vector2 desiredAnchorPosition))
                return false;

            if (!guidToNode.TryGetValue(preservedNodeGuid, out Node anchorNode) || anchorNode == null)
                return false;

            Vector2 anchorPosition = SnapVector(anchorNode.GetPosition().position, s?.Snap ?? 0f);
            Vector2 delta = anchorPosition - desiredAnchorPosition;
            if (delta.sqrMagnitude <= 0.001f)
                return true;

            var keys = desiredPos.Keys.ToList();
            foreach (string guid in keys)
                desiredPos[guid] = SnapVector(desiredPos[guid] + delta, s?.Snap ?? 0f);

            return true;
        }

        private static bool TryGetCurrentBounds(
            Dictionary<string, Node> guidToNode,
            IEnumerable<string> guids,
            out Rect bounds)
        {
            bounds = default;
            bool hasBounds = false;

            foreach (string guid in guids)
            {
                if (!guidToNode.TryGetValue(guid, out Node node) || node == null)
                    continue;

                Rect rect = node.GetPosition();
                if (!hasBounds)
                {
                    bounds = rect;
                    hasBounds = true;
                    continue;
                }

                bounds.xMin = Mathf.Min(bounds.xMin, rect.xMin);
                bounds.yMin = Mathf.Min(bounds.yMin, rect.yMin);
                bounds.xMax = Mathf.Max(bounds.xMax, rect.xMax);
                bounds.yMax = Mathf.Max(bounds.yMax, rect.yMax);
            }

            return hasBounds;
        }

        private static bool TryGetDesiredBounds(
            Dictionary<string, Node> guidToNode,
            Dictionary<string, Vector2> desiredPos,
            out Rect bounds)
        {
            bounds = default;
            bool hasBounds = false;

            foreach (KeyValuePair<string, Vector2> kv in desiredPos)
            {
                if (!guidToNode.TryGetValue(kv.Key, out Node node) || node == null)
                    continue;

                Rect currentRect = node.GetPosition();
                Rect desiredRect = new Rect(kv.Value, currentRect.size);

                if (!hasBounds)
                {
                    bounds = desiredRect;
                    hasBounds = true;
                    continue;
                }

                bounds.xMin = Mathf.Min(bounds.xMin, desiredRect.xMin);
                bounds.yMin = Mathf.Min(bounds.yMin, desiredRect.yMin);
                bounds.xMax = Mathf.Max(bounds.xMax, desiredRect.xMax);
                bounds.yMax = Mathf.Max(bounds.yMax, desiredRect.yMax);
            }

            return hasBounds;
        }

        private static Dictionary<string, Vector2> CopyPositions(Dictionary<string, Vector2> source)
        {
            return source == null
                ? new Dictionary<string, Vector2>(StringComparer.Ordinal)
                : source.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        }

        private static void LogLayoutDiagnostics(
            DialogGraphView view,
            HashSet<string> group,
            string root,
            Dictionary<string, Node> guidToNode,
            Dictionary<string, List<EdgeInfo>> edgeLookup,
            Dictionary<string, List<string>> outMap,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer,
            Dictionary<string, Vector2> nodeSizes,
            Dictionary<string, Vector2> desiredBeforeOffset,
            Dictionary<string, Vector2> desiredAfterOffset,
            Dictionary<string, Vector2> finalDesired)
        {
            if (!LayoutDiagnosticsEnabled)
                return;

            var orderByLayer = BuildLayerOrder(finalDesired);
            string componentId = root ?? "<none>";

            foreach (string guid in group.OrderBy(guid => layer.TryGetValue(guid, out int l) ? l : int.MaxValue)
                         .ThenBy(guid => orderByLayer.TryGetValue(guid, out int order) ? order : int.MaxValue)
                         .ThenBy(guid => guid, StringComparer.Ordinal))
            {
                guidToNode.TryGetValue(guid, out Node node);
                Vector2 oldPosition = node != null ? node.GetPosition().position : Vector2.zero;
                desiredBeforeOffset.TryGetValue(guid, out Vector2 beforeOffset);
                desiredAfterOffset.TryGetValue(guid, out Vector2 afterOffset);
                finalDesired.TryGetValue(guid, out Vector2 finalPosition);
                nodeSizes.TryGetValue(guid, out Vector2 size);
                layer.TryGetValue(guid, out int nodeLayer);
                orderByLayer.TryGetValue(guid, out int orderIndex);

                Debug.Log(
                    $"[DialogGraphLayoutFormatter] Node guid={guid}, type={node?.GetType().Name ?? "<missing>"}, " +
                    $"component={componentId}, root={root}, linked={group.Contains(guid)}, skipped=false, " +
                    $"old={oldPosition}, desiredBeforeOffset={beforeOffset}, desiredAfterOffset={afterOffset}, " +
                    $"final={finalPosition}, layer={nodeLayer}, order={orderIndex}, size={size}, rect={new Rect(finalPosition, size)}");
            }

            LogConditionEdgeDiagnostics(view, group, edgeLookup, inMap, parent, layer);
        }

        private static Dictionary<string, int> BuildLayerOrder(Dictionary<string, Vector2> positions)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (positions == null)
                return result;

            foreach (var layerGroup in positions
                         .GroupBy(kv => kv.Value.x)
                         .OrderBy(group => group.Key))
            {
                int order = 0;
                foreach (var item in layerGroup
                             .OrderBy(kv => kv.Value.y)
                             .ThenBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    result[item.Key] = order;
                    order++;
                }
            }

            return result;
        }

        private static void LogConditionEdgeDiagnostics(
            DialogGraphView view,
            HashSet<string> group,
            Dictionary<string, List<EdgeInfo>> edgeLookup,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer)
        {
            if (view == null)
                return;

            DialogGraph owner = GetGraphOwner(view);
            var graphLinkLookup = owner?.links?
                .GroupBy(link => $"{link.fromGuid}>{link.toGuid}:{link.fromPortIndex}", StringComparer.Ordinal)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.First(), StringComparer.Ordinal);

            foreach (DialogGraphEdge edge in view.edges.ToList().OfType<DialogGraphEdge>())
            {
                if (edge.output?.node is not ConditionNodeView conditionNode)
                    continue;

                string fromGuid = GetNodeGuid(edge.output.node);
                string toGuid = GetNodeGuid(edge.input?.node);
                int resolvedIndex = GetOutputPortIndexForLayout(edge);
                int graphLinkIndex = -1;

                if (graphLinkLookup != null)
                {
                    GraphLink link = graphLinkLookup.Values.FirstOrDefault(candidate =>
                        string.Equals(candidate.fromGuid, fromGuid, StringComparison.Ordinal) &&
                        string.Equals(candidate.toGuid, toGuid, StringComparison.Ordinal) &&
                        candidate.fromPortIndex == resolvedIndex);
                    graphLinkIndex = link?.fromPortIndex ?? -1;
                }

                bool affectsLayer = ShouldEdgeAffectLayer(fromGuid, toGuid, group, inMap, parent, layer);
                string edgeKind = ClassifyEdgeForDiagnostics(fromGuid, toGuid, group, inMap, parent, layer);

                Debug.Log(
                    $"[DialogGraphLayoutFormatter] ConditionEdge from={fromGuid}, to={toGuid}, " +
                    $"graphLinkPort={graphLinkIndex}, visualEdgePort={edge.fromPortIndex}, resolvedPort={resolvedIndex}, " +
                    $"sourceType={conditionNode.GetType().Name}, targetType={edge.input?.node?.GetType().Name ?? "<missing>"}, " +
                    $"kind={edgeKind}, affectsLayer={affectsLayer}, " +
                    $"fromLayer={(layer.TryGetValue(fromGuid, out int fromLayer) ? fromLayer : -1)}, " +
                    $"toLayer={(layer.TryGetValue(toGuid, out int toLayer) ? toLayer : -1)}");
            }
        }

        private static string ClassifyEdgeForDiagnostics(
            string from,
            string to,
            HashSet<string> group,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer)
        {
            if (string.Equals(from, to, StringComparison.Ordinal))
                return "SelfLoop";

            if (IsAncestor(to, from, parent))
                return "Back";

            if (IsSameOrEarlierLayerEdge(from, to, layer))
                return "Back";

            if (parent != null &&
                parent.TryGetValue(to, out string primaryParent) &&
                string.Equals(primaryParent, from, StringComparison.Ordinal))
            {
                return "Tree";
            }

            if (inMap != null && inMap.TryGetValue(to, out List<string> incoming))
            {
                int forwardIncomingCount = incoming
                    .Where(candidate => group.Contains(candidate))
                    .Where(candidate => !string.Equals(candidate, to, StringComparison.Ordinal))
                    .Count(candidate => !IsAncestor(to, candidate, parent) &&
                                        !IsSameOrEarlierLayerEdge(candidate, to, layer));

                if (forwardIncomingCount > 1)
                    return "Merge";
            }

            return "Cross";
        }

        #endregion

        #region ---------------- Tree Construction ----------------

        private static void BuildPrimaryTree(
            string root,
            HashSet<string> group,
            Dictionary<string, Node> guidToNode,
            Dictionary<string, List<string>> outMap,
            Dictionary<string, List<EdgeInfo>> edgeLookup,
            Dictionary<string, List<string>> treeChildren,
            Dictionary<string, string> parent,
            HashSet<string> visited,
            Settings s)
        {
            if (string.IsNullOrWhiteSpace(root))
                return;

            if (!group.Contains(root))
                return;

            if (!visited.Add(root))
                return;

            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                List<string> orderedChildren = GetOrderedChildren(
                    current,
                    group,
                    guidToNode,
                    outMap,
                    edgeLookup,
                    s
                );

                // Critical detail:
                // treeChildren must remain in visual top-to-bottom order because
                // AssignNodeYRecursive uses that order to assign vertical lanes.
                // Only the stack push is reversed because Stack is LIFO.
                var acceptedChildren = new List<string>();

                foreach (string child in orderedChildren)
                {
                    if (!group.Contains(child))
                        continue;

                    // If already visited, this is a merge, cross-link, or cycle edge.
                    // It should not become another tree child.
                    if (visited.Contains(child))
                        continue;

                    visited.Add(child);
                    parent[child] = current;

                    AddUnique(treeChildren, current, child);
                    acceptedChildren.Add(child);
                }

                for (int i = acceptedChildren.Count - 1; i >= 0; i--)
                    stack.Push(acceptedChildren[i]);
            }
        }

        private static List<string> GetOrderedChildren(
            string guid,
            HashSet<string> group,
            Dictionary<string, Node> guidToNode,
            Dictionary<string, List<string>> outMap,
            Dictionary<string, List<EdgeInfo>> edgeLookup,
            Settings s)
        {
            if (!outMap.TryGetValue(guid, out List<string> children))
                return new List<string>();

            bool isChoice = guidToNode.TryGetValue(guid, out Node node) && node is ChoiceNodeView;
            bool isCondition = node is ConditionNodeView;

            if (s.OrderChoiceBranchesByOutputPort && isChoice && edgeLookup.TryGetValue(guid, out List<EdgeInfo> edgeInfos))
            {
                return edgeInfos
                    .Where(e => group.Contains(e.ToGuid))
                    .OrderBy(e => e.FromPortIndex < 0 ? int.MaxValue : e.FromPortIndex)
                    .ThenBy(e => e.OutputPortY)
                    .ThenBy(e => e.OutputPortX)
                    .ThenBy(e => e.ToGuid, StringComparer.Ordinal)
                    .Select(e => e.ToGuid)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }

            if (isCondition && edgeLookup.TryGetValue(guid, out edgeInfos))
            {
                return edgeInfos
                    .Where(e => group.Contains(e.ToGuid))
                    .OrderBy(e => e.FromPortIndex < 0 ? int.MaxValue : e.FromPortIndex)
                    .ThenBy(e => e.OutputPortY)
                    .ThenBy(e => e.OutputPortX)
                    .ThenBy(e => e.ToGuid, StringComparer.Ordinal)
                    .Select(e => e.ToGuid)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }

            if (s.PreserveManualOrderForNonChoiceBranches)
            {
                return children
                    .Where(group.Contains)
                    .OrderBy(childGuid => GetCurrentPositionY(childGuid, guidToNode))
                    .ThenBy(childGuid => GetCurrentPositionX(childGuid, guidToNode))
                    .ThenBy(childGuid => childGuid, StringComparer.Ordinal)
                    .ToList();
            }

            return children
                .Where(group.Contains)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
        }

        #endregion

        #region ---------------- Layer Assignment ----------------

        private static Dictionary<string, int> AssignLayers(
            string root,
            HashSet<string> group,
            Dictionary<string, List<string>> outMap,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent)
        {
            var layer = new Dictionary<string, int>(StringComparer.Ordinal);

            if (!string.IsNullOrWhiteSpace(root) && group.Contains(root))
                layer[root] = 0;

            // First pass: primary tree depth.
            List<string> orderedByTreeDepth = group
                .OrderBy(g => GetParentDepth(g, parent))
                .ThenBy(g => g, StringComparer.Ordinal)
                .ToList();

            foreach (string guid in orderedByTreeDepth)
            {
                if (layer.ContainsKey(guid))
                    continue;

                int depth = GetParentDepth(guid, parent);
                layer[guid] = Mathf.Max(0, depth);
            }

            RelaxForwardLayers(root, group, outMap, inMap, parent, layer);
            EnforceDeepestIncomingLayers(group, inMap, parent, layer);

            // Run forward relaxation again because merge nodes may have been pushed deeper.
            RelaxForwardLayers(root, group, outMap, inMap, parent, layer);

            return layer;
        }

        private static void RelaxForwardLayers(
            string root,
            HashSet<string> group,
            Dictionary<string, List<string>> outMap,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer)
        {
            int maxIterations = Mathf.Max(1, group.Count * 2);

            List<string> orderedNodes = group
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                bool changed = false;

                foreach (string from in orderedNodes)
                {
                    if (!outMap.TryGetValue(from, out List<string> outs))
                        continue;

                    int fromLayer = layer.TryGetValue(from, out int existingFromLayer)
                        ? existingFromLayer
                        : 0;

                    foreach (string to in outs.Where(group.Contains).OrderBy(x => x, StringComparer.Ordinal))
                    {
                        if (string.Equals(to, root, StringComparison.Ordinal))
                            continue;

                        if (!ShouldEdgeAffectLayer(from, to, group, inMap, parent, layer))
                            continue;

                        int desiredLayer = fromLayer + 1;

                        if (!layer.TryGetValue(to, out int currentLayer) || desiredLayer > currentLayer)
                        {
                            layer[to] = desiredLayer;
                            changed = true;
                        }
                    }
                }

                if (!changed)
                    break;
            }
        }

        private static void EnforceDeepestIncomingLayers(
            HashSet<string> group,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer)
        {
            foreach (string guid in group.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!inMap.TryGetValue(guid, out List<string> incoming))
                    continue;

                int deepestParentLayer = -1;

                foreach (string parentGuid in incoming.Where(group.Contains))
                {
                    if (!ShouldEdgeAffectLayer(parentGuid, guid, group, inMap, parent, layer))
                        continue;

                    int parentLayer = layer.TryGetValue(parentGuid, out int existingParentLayer)
                        ? existingParentLayer
                        : 0;

                    if (parentLayer > deepestParentLayer)
                        deepestParentLayer = parentLayer;
                }

                if (deepestParentLayer < 0)
                    continue;

                int currentLayer = layer.TryGetValue(guid, out int existingLayer)
                    ? existingLayer
                    : 0;

                layer[guid] = Mathf.Max(currentLayer, deepestParentLayer + 1);
            }
        }

        /// <summary>
        /// Returns true if the edge <paramref name="from"/> → <paramref name="to"/>
        /// should actively influence layer assignment (i.e. push the target to a later
        /// layer or participate in centering / relaxation).
        ///
        /// IMPORTANT: as of the layout-constraint-graph refactor, <paramref name="inMap"/>
        /// and the caller-visible outMap contain ONLY acyclic, forward-safe edges.
        /// Back and cycle edges have already been excluded by
        /// <see cref="BuildLayoutConstraintEdges"/>.  The <see cref="IsAncestor"/> and
        /// <see cref="IsSameOrEarlierLayerEdge"/> guards below are retained as
        /// defence-in-depth; they are not the primary cycle filter.
        ///
        /// The function discriminates between:
        ///  • Tree edges (tree parent → child)  → true
        ///  • True merge edges (target has 2+ forward incoming) → true
        ///  • Cross-links (single non-tree forward reference) → false
        /// </summary>
        private static bool ShouldEdgeAffectLayer(
            string from,
            string to,
            HashSet<string> group,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer = null)
        {
            if (string.IsNullOrWhiteSpace(from) ||
                string.IsNullOrWhiteSpace(to) ||
                string.Equals(from, to, StringComparison.Ordinal))
            {
                return false;
            }

            if (group == null || !group.Contains(from) || !group.Contains(to))
                return false;

            // Defence-in-depth: if a back/cycle edge somehow reached this point,
            // reject it here too.  Under the new architecture this should not fire.
            if (IsAncestor(to, from, parent))
                return false;

            if (IsSameOrEarlierLayerEdge(from, to, layer))
                return false;

            // Tree edge: this node is the primary parent in the layout tree.
            if (parent != null &&
                parent.TryGetValue(to, out string primaryParent) &&
                string.Equals(primaryParent, from, StringComparison.Ordinal))
            {
                return true;
            }

            // True merge node: target has 2+ forward-incoming layout edges.
            // Single non-tree forward references are cross-links; they are visual-only
            // and must not push the target to a later layer.
            if (inMap != null && inMap.TryGetValue(to, out List<string> incoming))
            {
                int forwardIncomingCount = incoming
                    .Where(candidate => group.Contains(candidate))
                    .Where(candidate => !string.Equals(candidate, to, StringComparison.Ordinal))
                    .Count(candidate => !IsAncestor(to, candidate, parent) &&
                                        !IsSameOrEarlierLayerEdge(candidate, to, layer));

                return forwardIncomingCount > 1;
            }

            return false;
        }

        private static int ComputeEndLayer(
            HashSet<string> group,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer,
            string endGuid)
        {
            if (string.IsNullOrWhiteSpace(endGuid) || !inMap.TryGetValue(endGuid, out List<string> incoming))
                return layer.Values.Count > 0 ? layer.Values.Max() + 1 : 1;

            int maxParentLayer = 0;

            foreach (string parentGuid in incoming.Where(group.Contains))
            {
                if (!ShouldEdgeAffectLayer(parentGuid, endGuid, group, inMap, parent, layer))
                    continue;

                if (layer.TryGetValue(parentGuid, out int parentLayer))
                    maxParentLayer = Mathf.Max(maxParentLayer, parentLayer);
            }

            // End must be placed after the deepest non-End node in the entire group, not
            // just after its direct layout parents.  Without this, a loop such as
            //   Choice(2) → Condition(3) → Dialog(4) → [cycle back]
            //   Choice(2) → End
            // would place End at layer 3 (same column as Condition) even though Dialog
            // lives at layer 4, making End land in the middle of the graph.
            int deepestNonEndLayer = layer
                .Where(kv => group.Contains(kv.Key) &&
                             !string.Equals(kv.Key, endGuid, StringComparison.Ordinal))
                .Select(kv => kv.Value)
                .DefaultIfEmpty(0)
                .Max();

            return Mathf.Max(maxParentLayer, deepestNonEndLayer) + 1;
        }

        private static int GetParentDepth(string guid, Dictionary<string, string> parent)
        {
            int depth = 0;
            string current = guid;
            var guard = new HashSet<string>(StringComparer.Ordinal);

            while (parent.TryGetValue(current, out string p))
            {
                if (!guard.Add(current))
                    break;

                depth++;
                current = p;
            }

            return depth;
        }

        private static bool IsAncestor(string possibleAncestor, string node, Dictionary<string, string> parent)
        {
            string current = node;
            var guard = new HashSet<string>(StringComparer.Ordinal);

            while (parent.TryGetValue(current, out string p))
            {
                if (!guard.Add(current))
                    return false;

                if (string.Equals(p, possibleAncestor, StringComparison.Ordinal))
                    return true;

                current = p;
            }

            return false;
        }

        private static bool IsSameOrEarlierLayerEdge(string from, string to, Dictionary<string, int> layer)
        {
            if (layer == null ||
                string.IsNullOrWhiteSpace(from) ||
                string.IsNullOrWhiteSpace(to) ||
                !layer.TryGetValue(from, out int fromLayer) ||
                !layer.TryGetValue(to, out int toLayer))
            {
                return false;
            }

            return toLayer <= fromLayer;
        }

        #endregion

        #region ---------------- Y Assignment ----------------

        private static Dictionary<string, float> AssignSubtreeY(
            string root,
            HashSet<string> group,
            Dictionary<string, List<string>> treeChildren,
            Dictionary<string, Node> guidToNode,
            float rootY,
            Settings s)
        {
            var y = new Dictionary<string, float>(StringComparer.Ordinal);
            var roots = new List<string>();

            if (!string.IsNullOrWhiteSpace(root) && group.Contains(root))
                roots.Add(root);

            HashSet<string> treeChildSet = BuildTreeChildSet(treeChildren);

            foreach (string guid in group
                .OrderBy(x => GetCurrentPositionY(x, guidToNode))
                .ThenBy(x => GetCurrentPositionX(x, guidToNode))
                .ThenBy(x => x, StringComparer.Ordinal))
            {
                if (roots.Contains(guid))
                    continue;

                if (!treeChildSet.Contains(guid))
                    roots.Add(guid);
            }

            float nextLane = 0f;

            foreach (string forestRoot in roots)
            {
                AssignNodeYRecursive(
                    forestRoot,
                    treeChildren,
                    y,
                    ref nextLane,
                    s
                );

                nextLane += 1f;
            }

            if (y.Count == 0)
            {
                y[root] = rootY;
                return y;
            }

            float currentRootY = y.TryGetValue(root, out float ry) ? ry : y.Values.First();
            float delta = rootY - currentRootY;

            foreach (string guid in y.Keys.ToList())
                y[guid] = Snap(y[guid] + delta, s.Snap);

            return y;
        }

        private static HashSet<string> BuildTreeChildSet(Dictionary<string, List<string>> treeChildren)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, List<string>> kv in treeChildren)
            {
                for (int i = 0; i < kv.Value.Count; i++)
                    result.Add(kv.Value[i]);
            }

            return result;
        }

        private static float AssignNodeYRecursive(
            string guid,
            Dictionary<string, List<string>> treeChildren,
            Dictionary<string, float> y,
            ref float nextLane,
            Settings s)
        {
            if (!treeChildren.TryGetValue(guid, out List<string> children) || children.Count == 0)
            {
                float leafY = nextLane * s.YStep;
                y[guid] = Snap(leafY, s.Snap);
                nextLane += 1f;
                return y[guid];
            }

            var childYs = new List<float>();

            foreach (string child in children)
            {
                float childY = AssignNodeYRecursive(child, treeChildren, y, ref nextLane, s);
                childYs.Add(childY);
            }

            float nodeY;

            if (s.CenterParentsOverChildren)
                nodeY = (childYs.Min() + childYs.Max()) * 0.5f;
            else
                nodeY = childYs[0];

            y[guid] = Snap(nodeY, s.Snap);
            return y[guid];
        }

        private static void RelaxSharedNodeY(
            HashSet<string> group,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer,
            Dictionary<string, float> y,
            string root,
            float rootY,
            Settings s,
            string endGuid)
        {
            foreach (string guid in group.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (string.Equals(guid, root, StringComparison.Ordinal))
                    continue;

                bool isEnd = !string.IsNullOrWhiteSpace(endGuid) &&
                             string.Equals(guid, endGuid, StringComparison.Ordinal);

                if (!inMap.TryGetValue(guid, out List<string> incoming))
                    continue;

                List<string> validIncoming = incoming
                    .Where(candidate => ShouldEdgeAffectLayer(candidate, guid, group, inMap, parent, layer))
                    .ToList();

                if (!isEnd && validIncoming.Count <= 1)
                    continue;

                float averageY = ComputeAverageIncomingY(validIncoming, y, rootY);
                y[guid] = Snap(averageY, s.Snap);
            }
        }

        private static void AlignSingleChildChains(
            HashSet<string> group,
            Dictionary<string, List<string>> outMap,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer,
            Dictionary<string, float> y,
            Settings s)
        {
            foreach (string start in group.OrderBy(x => x, StringComparer.Ordinal))
            {
                string current = start;
                var guard = new HashSet<string>(StringComparer.Ordinal);

                while (guard.Add(current))
                {
                    if (!outMap.TryGetValue(current, out List<string> outs))
                        break;

                    List<string> validOuts = outs
                        .Where(candidate => ShouldEdgeAffectLayer(current, candidate, group, inMap, parent, layer))
                        .ToList();

                    if (validOuts.Count != 1)
                        break;

                    string child = validOuts[0];

                    if (!inMap.TryGetValue(child, out List<string> incoming))
                        break;

                    List<string> validIncoming = incoming
                        .Where(candidate => ShouldEdgeAffectLayer(candidate, child, group, inMap, parent, layer))
                        .ToList();

                    if (validIncoming.Count != 1)
                        break;

                    if (!y.TryGetValue(current, out float currentY))
                        break;

                    y[child] = Snap(currentY, s.Snap);
                    current = child;
                }
            }
        }

        private static void SpaceBranchRoots(
            HashSet<string> group,
            Dictionary<string, List<string>> treeChildren,
            Dictionary<string, Node> guidToNode,
            Dictionary<string, Vector2> nodeSizes,
            Dictionary<string, float> y,
            Settings s)
        {
            if (group == null || treeChildren == null || y == null)
                return;

            foreach (string branchGuid in group.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!guidToNode.TryGetValue(branchGuid, out Node branchNode) ||
                    !IsSemanticBranchNode(branchNode) ||
                    !treeChildren.TryGetValue(branchGuid, out List<string> children))
                {
                    continue;
                }

                List<string> branchChildren = children
                    .Where(child => group.Contains(child) && y.ContainsKey(child))
                    .ToList();

                if (branchChildren.Count <= 1)
                    continue;

                float minimumGap = ComputeBranchRootGap(branchChildren, nodeSizes, s);
                float previousY = y[branchChildren[0]];

                for (int i = 1; i < branchChildren.Count; i++)
                {
                    string child = branchChildren[i];
                    float desiredY = Snap(previousY + minimumGap, s.Snap);

                    if (y[child] < desiredY)
                        ShiftSubtreeY(child, desiredY - y[child], treeChildren, y);

                    previousY = y[child];
                }

                if (s.CenterParentsOverChildren && y.ContainsKey(branchGuid))
                {
                    float firstY = y[branchChildren[0]];
                    float lastY = y[branchChildren[branchChildren.Count - 1]];
                    y[branchGuid] = Snap((firstY + lastY) * 0.5f, s.Snap);
                }
            }
        }

        private static float ComputeBranchRootGap(
            List<string> branchChildren,
            Dictionary<string, Vector2> nodeSizes,
            Settings s)
        {
            float maxHeight = 0f;

            foreach (string child in branchChildren)
                maxHeight = Mathf.Max(maxHeight, GetNodeSize(child, nodeSizes).y);

            float padding = Mathf.Max(BranchRootVerticalPadding, s?.Snap ?? 0f);
            return Snap(Mathf.Max(s?.YStep ?? 0f, maxHeight + padding), s?.Snap ?? 0f);
        }

        private static void ShiftSubtreeY(
            string root,
            float delta,
            Dictionary<string, List<string>> treeChildren,
            Dictionary<string, float> y)
        {
            if (Mathf.Abs(delta) <= 0.001f)
                return;

            var stack = new Stack<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            stack.Push(root);

            while (stack.Count > 0)
            {
                string current = stack.Pop();
                if (!visited.Add(current))
                    continue;

                if (y.TryGetValue(current, out float currentY))
                    y[current] = currentY + delta;

                if (!treeChildren.TryGetValue(current, out List<string> children))
                    continue;

                for (int i = children.Count - 1; i >= 0; i--)
                    stack.Push(children[i]);
            }
        }

        private static float ComputeEndY(
            string endGuid,
            HashSet<string> group,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer,
            Dictionary<string, float> y,
            float rootY,
            Settings s)
        {
            List<string> forwardIncoming = GetForwardIncoming(endGuid, group, inMap, parent, layer);

            if (forwardIncoming.Count > 1)
                return Snap(ComputeAverageIncomingY(forwardIncoming, y, rootY), s.Snap);

            if (s.KeepEndOnRootLane)
                return Snap(rootY, s.Snap);

            return Snap(ComputeAverageIncomingY(forwardIncoming, y, rootY), s.Snap);
        }

        private static float ComputeAverageIncomingY(
            string guid,
            HashSet<string> group,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, float> y,
            float fallbackY)
        {
            if (!inMap.TryGetValue(guid, out List<string> incoming))
                return fallbackY;

            float sum = 0f;
            int count = 0;

            foreach (string parentGuid in incoming.Where(group.Contains))
            {
                if (!y.TryGetValue(parentGuid, out float parentY))
                    continue;

                sum += parentY;
                count++;
            }

            return count > 0 ? sum / count : fallbackY;
        }

        private static float ComputeAverageIncomingY(
            List<string> incoming,
            Dictionary<string, float> y,
            float fallbackY)
        {
            if (incoming == null || incoming.Count == 0)
                return fallbackY;

            float sum = 0f;
            int count = 0;

            foreach (string parentGuid in incoming)
            {
                if (!y.TryGetValue(parentGuid, out float parentY))
                    continue;

                sum += parentY;
                count++;
            }

            return count > 0 ? sum / count : fallbackY;
        }

        private static List<string> GetForwardIncoming(
            string guid,
            HashSet<string> group,
            Dictionary<string, List<string>> inMap,
            Dictionary<string, string> parent,
            Dictionary<string, int> layer)
        {
            if (string.IsNullOrWhiteSpace(guid) || inMap == null || !inMap.TryGetValue(guid, out List<string> incoming))
                return new List<string>();

            return incoming
                .Where(candidate => ShouldEdgeAffectLayer(candidate, guid, group, inMap, parent, layer))
                .OrderBy(candidate => candidate, StringComparer.Ordinal)
                .ToList();
        }

        private static bool IsSemanticBranchNode(Node node)
        {
            return node is ChoiceNodeView || node is ConditionNodeView;
        }

        #endregion

        #region ---------------- Node Size Resolution ----------------

        private static Dictionary<string, Vector2> BuildNodeSizeMap(
            HashSet<string> group,
            Dictionary<string, Node> guidToNode)
        {
            var nodeSizes = new Dictionary<string, Vector2>(StringComparer.Ordinal);

            if (group == null || guidToNode == null)
                return nodeSizes;

            foreach (string guid in group.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!guidToNode.TryGetValue(guid, out Node node) || node == null)
                    continue;

                nodeSizes[guid] = ResolveNodeSize(node);
            }

            return nodeSizes;
        }

        private static Vector2 GetNodeSize(
            string guid,
            Node node,
            Dictionary<string, Vector2> nodeSizes)
        {
            if (!string.IsNullOrWhiteSpace(guid) &&
                nodeSizes != null &&
                nodeSizes.TryGetValue(guid, out Vector2 size))
            {
                return size;
            }

            return ResolveNodeSize(node);
        }

        private static Vector2 ResolveNodeSize(Node node)
        {
            Vector2 fallback = GetFallbackNodeSize(node);

            if (node == null)
                return fallback;

            Rect rect = node.GetPosition();
            float width = MaxValidDimension(fallback.x, rect.width, node.layout.width, node.resolvedStyle.width);
            float height = MaxValidDimension(fallback.y, rect.height, node.layout.height, node.resolvedStyle.height);

            return new Vector2(width, height);
        }

        private static Vector2 GetFallbackNodeSize(Node node)
        {
            if (node is StartNodeView || node is EndNodeView)
                return new Vector2(FallbackBoundaryWidth, FallbackBoundaryHeight);

            if (node is ActionNodeView)
                return new Vector2(FallbackActionWidth, FallbackActionHeight);

            if (node is ConditionNodeView || node is VariableMutationNodeView)
                return new Vector2(FallbackConditionWidth, FallbackConditionHeight);

            if (node is OutcomeNodeView)
                return new Vector2(320f, 220f);

            return new Vector2(FallbackNodeWidth, FallbackNodeHeight);
        }

        private static float MaxValidDimension(float fallback, params float[] values)
        {
            float result = fallback;

            if (values == null)
                return result;

            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    continue;

                result = Mathf.Max(result, value);
            }

            return result;
        }

        #endregion

        #region ---------------- Collision Resolution ----------------

        private static void ResolveCollisions(
            Dictionary<string, Vector2> pos,
            Dictionary<string, Vector2> nodeSizes,
            Settings s,
            string startGuid,
            string endGuid)
        {
            ResolveSameLayerRectOverlaps(pos, nodeSizes, s, startGuid, endGuid);
            ResolveGlobalRectOverlaps(pos, nodeSizes, s, startGuid, endGuid);
            ResolveSameLayerRectOverlaps(pos, nodeSizes, s, startGuid, endGuid);
        }

        private static void ResolveSameLayerRectOverlaps(
            Dictionary<string, Vector2> pos,
            Dictionary<string, Vector2> nodeSizes,
            Settings s,
            string startGuid,
            string endGuid)
        {
            if (pos == null || pos.Count <= 1)
                return;

            float padding = Mathf.Max(SameLayerVerticalPadding, s?.Snap ?? 0f);

            List<IGrouping<float, KeyValuePair<string, Vector2>>> layerGroups = pos
                .GroupBy(kv => Snap(kv.Value.x, s?.Snap ?? 0f))
                .OrderBy(g => g.Key)
                .ToList();

            foreach (IGrouping<float, KeyValuePair<string, Vector2>> layerGroup in layerGroups)
            {
                List<KeyValuePair<string, Vector2>> items = layerGroup
                    .OrderBy(kv => IsGuid(kv.Key, startGuid) ? 0 : 1)
                    .ThenBy(kv => kv.Value.y)
                    .ThenBy(kv => GetCollisionPriority(kv.Key, startGuid, endGuid))
                    .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .ToList();

                if (items.Count <= 1)
                    continue;

                float nextMinY = float.NegativeInfinity;

                foreach (KeyValuePair<string, Vector2> item in items)
                {
                    Vector2 p = item.Value;
                    float candidateY = Snap(p.y, s.Snap);
                    Vector2 size = GetNodeSize(item.Key, nodeSizes);

                    if (!float.IsNegativeInfinity(nextMinY) && candidateY < nextMinY)
                        candidateY = Snap(nextMinY, s.Snap);

                    pos[item.Key] = new Vector2(Snap(p.x, s.Snap), candidateY);
                    nextMinY = candidateY + size.y + padding;
                }
            }
        }

        private static void ResolveGlobalRectOverlaps(
            Dictionary<string, Vector2> pos,
            Dictionary<string, Vector2> nodeSizes,
            Settings s,
            string startGuid,
            string endGuid)
        {
            if (pos == null || pos.Count <= 1)
                return;

            float padding = Mathf.Max(CrossLayerVerticalPadding, s?.Snap ?? 0f);
            List<string> orderedGuids = pos.Keys
                .OrderBy(guid => pos[guid].x)
                .ThenBy(guid => pos[guid].y)
                .ThenBy(guid => GetCollisionPriority(guid, startGuid, endGuid))
                .ThenBy(guid => guid, StringComparer.Ordinal)
                .ToList();

            for (int pass = 0; pass < MaxGlobalOverlapPasses; pass++)
            {
                bool changed = false;

                for (int i = 0; i < orderedGuids.Count; i++)
                {
                    string a = orderedGuids[i];

                    for (int j = i + 1; j < orderedGuids.Count; j++)
                    {
                        string b = orderedGuids[j];
                        Rect rectA = GetDesiredRect(a, pos, nodeSizes);
                        Rect rectB = GetDesiredRect(b, pos, nodeSizes);

                        if (rectB.xMin >= rectA.xMax + s.XPadding)
                            break;

                        if (!rectA.Overlaps(rectB))
                            continue;

                        string moveGuid = ChooseOverlapMoveTarget(a, b, pos, startGuid, endGuid);
                        if (string.IsNullOrEmpty(moveGuid))
                            continue;

                        string anchorGuid = string.Equals(moveGuid, a, StringComparison.Ordinal) ? b : a;
                        Rect anchorRect = GetDesiredRect(anchorGuid, pos, nodeSizes);
                        Vector2 current = pos[moveGuid];
                        float resolvedY = Snap(Mathf.Max(current.y, anchorRect.yMax + padding), s.Snap);

                        if (Mathf.Abs(resolvedY - current.y) <= 0.001f)
                            continue;

                        pos[moveGuid] = new Vector2(Snap(current.x, s.Snap), resolvedY);
                        changed = true;
                    }
                }

                if (!changed)
                    break;

                orderedGuids = orderedGuids
                    .OrderBy(guid => pos[guid].x)
                    .ThenBy(guid => pos[guid].y)
                    .ThenBy(guid => GetCollisionPriority(guid, startGuid, endGuid))
                    .ThenBy(guid => guid, StringComparer.Ordinal)
                    .ToList();
            }
        }

        private static Rect GetDesiredRect(
            string guid,
            Dictionary<string, Vector2> pos,
            Dictionary<string, Vector2> nodeSizes)
        {
            if (pos == null || !pos.TryGetValue(guid, out Vector2 position))
                return default;

            return new Rect(position, GetNodeSize(guid, nodeSizes));
        }

        private static Vector2 GetNodeSize(
            string guid,
            Dictionary<string, Vector2> nodeSizes)
        {
            if (nodeSizes != null && nodeSizes.TryGetValue(guid, out Vector2 size))
                return size;

            return new Vector2(FallbackNodeWidth, FallbackNodeHeight);
        }

        private static string ChooseOverlapMoveTarget(
            string a,
            string b,
            Dictionary<string, Vector2> pos,
            string startGuid,
            string endGuid)
        {
            if (IsGuid(a, startGuid))
                return IsGuid(b, startGuid) ? null : b;

            if (IsGuid(b, startGuid))
                return a;

            int priorityA = GetCollisionPriority(a, startGuid, endGuid);
            int priorityB = GetCollisionPriority(b, startGuid, endGuid);

            if (priorityA != priorityB)
                return priorityA > priorityB ? a : b;

            Vector2 posA = pos.TryGetValue(a, out Vector2 pa) ? pa : Vector2.zero;
            Vector2 posB = pos.TryGetValue(b, out Vector2 pb) ? pb : Vector2.zero;

            if (!Mathf.Approximately(posA.x, posB.x))
                return posA.x > posB.x ? a : b;

            if (!Mathf.Approximately(posA.y, posB.y))
                return posA.y > posB.y ? a : b;

            return string.CompareOrdinal(a, b) > 0 ? a : b;
        }

        private static bool IsGuid(string guid, string expectedGuid)
        {
            return !string.IsNullOrWhiteSpace(expectedGuid) &&
                   string.Equals(guid, expectedGuid, StringComparison.Ordinal);
        }

        private static int GetCollisionPriority(string guid, string startGuid, string endGuid)
        {
            if (!string.IsNullOrWhiteSpace(startGuid) &&
                string.Equals(guid, startGuid, StringComparison.Ordinal))
                return 0;

            if (!string.IsNullOrWhiteSpace(endGuid) &&
                string.Equals(guid, endGuid, StringComparison.Ordinal))
                return 2;

            return 1;
        }

        #endregion

        #region ---------------- Graph Helpers ----------------

        private static string GetBoundaryGuid<TNode>(IEnumerable<Node> nodes)
            where TNode : Node
        {
            return nodes?
                .OfType<TNode>()
                .Select(GetNodeGuid)
                .FirstOrDefault(guid => !string.IsNullOrWhiteSpace(guid));
        }

        private static bool PlaceDetachedEndNode(
            DialogGraphView view,
            Dictionary<string, Node> guidToNode,
            string startGuid,
            string endGuid,
            Settings s,
            string preservedNodeGuid)
        {
            if (string.IsNullOrWhiteSpace(endGuid) ||
                string.Equals(preservedNodeGuid, endGuid, StringComparison.Ordinal) ||
                !guidToNode.TryGetValue(endGuid, out Node endNode) ||
                endNode is not EndNodeView endView)
            {
                return false;
            }

            if (endView.inputPort?.connections != null && endView.inputPort.connections.Any())
                return false;

            DialogGraph owner = GetGraphOwner(view);
            float anchorY = GetComparableNodePosition(owner, endGuid, endNode).y;
            if (s.KeepEndOnRootLane &&
                !string.IsNullOrWhiteSpace(startGuid) &&
                guidToNode.TryGetValue(startGuid, out Node startNode))
            {
                anchorY = GetComparableNodePosition(owner, startGuid, startNode).y;
            }

            float maxRight = guidToNode
                .Where(kv => !string.Equals(kv.Key, endGuid, StringComparison.Ordinal))
                .Select(kv =>
                {
                    Vector2 position = GetComparableNodePosition(owner, kv.Key, kv.Value);
                    return position.x + GetFallbackSizeForNode(kv.Value).x;
                })
                .DefaultIfEmpty(GetComparableNodePosition(owner, endGuid, endNode).x + GetFallbackSizeForNode(endNode).x)
                .Max();

            Rect rect = endNode.GetPosition();
            Vector2 currentPosition = GetComparableNodePosition(owner, endGuid, endNode);
            Vector2 desiredPosition = new Vector2(
                Mathf.Max(Snap(currentPosition.x, s.Snap), Snap(maxRight + s.XSpacing, s.Snap)),
                Snap(anchorY, s.Snap));

            if (ArePositionsEquivalent(currentPosition, desiredPosition))
                return false;

            RecordFormatUndo(view, new List<string> { endGuid });
            rect = new Rect(desiredPosition, IsUsableRect(rect) ? rect.size : GetFallbackSizeForNode(endNode));
            endNode.SetPosition(rect);
            SetSerializedNodePosition(owner, endGuid, desiredPosition);
            return true;
        }

        private static void AddUnique(Dictionary<string, List<string>> map, string key, string value)
        {
            if (!map.TryGetValue(key, out List<string> list))
            {
                list = new List<string>();
                map.Add(key, list);
            }

            if (!list.Contains(value))
                list.Add(value);
        }

        private static void AddUndirected(Dictionary<string, HashSet<string>> map, string a, string b)
        {
            if (!map.TryGetValue(a, out HashSet<string> setA))
            {
                setA = new HashSet<string>(StringComparer.Ordinal);
                map.Add(a, setA);
            }

            if (!map.TryGetValue(b, out HashSet<string> setB))
            {
                setB = new HashSet<string>(StringComparer.Ordinal);
                map.Add(b, setB);
            }

            setA.Add(b);
            setB.Add(a);
        }

        private static List<HashSet<string>> ConnectedComponents(
            HashSet<string> nodes,
            Dictionary<string, HashSet<string>> undirected)
        {
            var components = new List<HashSet<string>>();
            var unvisited = new HashSet<string>(nodes, StringComparer.Ordinal);

            while (unvisited.Count > 0)
            {
                string seed = unvisited.OrderBy(x => x, StringComparer.Ordinal).First();
                unvisited.Remove(seed);

                var component = new HashSet<string>(StringComparer.Ordinal) { seed };
                var queue = new Queue<string>();
                queue.Enqueue(seed);

                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();

                    if (!undirected.TryGetValue(current, out HashSet<string> neighbours))
                        continue;

                    foreach (string neighbour in neighbours.OrderBy(x => x, StringComparer.Ordinal))
                    {
                        if (!nodes.Contains(neighbour))
                            continue;

                        if (!component.Add(neighbour))
                            continue;

                        unvisited.Remove(neighbour);
                        queue.Enqueue(neighbour);
                    }
                }

                components.Add(component);
            }

            return components;
        }

        private static Vector2 ComputeCentroid(HashSet<string> component, Dictionary<string, Node> guidToNode)
        {
            float sumX = 0f;
            float sumY = 0f;
            int count = 0;

            foreach (string guid in component)
            {
                if (!guidToNode.TryGetValue(guid, out Node node))
                    continue;

                Vector2 position = node.GetPosition().position;

                sumX += position.x;
                sumY += position.y;
                count++;
            }

            if (count <= 0)
                return Vector2.zero;

            return new Vector2(sumX / count, sumY / count);
        }

        private static string FindBestRoot(
            HashSet<string> component,
            Dictionary<string, List<string>> inMap,
            string startGuid)
        {
            if (!string.IsNullOrWhiteSpace(startGuid) && component.Contains(startGuid))
                return startGuid;

            foreach (string guid in component.OrderBy(x => x, StringComparer.Ordinal))
            {
                bool hasIncomingInsideComponent =
                    inMap.TryGetValue(guid, out List<string> incoming) &&
                    incoming.Any(component.Contains);

                if (!hasIncomingInsideComponent)
                    return guid;
            }

            return component.OrderBy(x => x, StringComparer.Ordinal).First();
        }

        private static float GetCurrentPositionX(string guid, Dictionary<string, Node> guidToNode)
        {
            if (!guidToNode.TryGetValue(guid, out Node node))
                return 0f;

            return node.GetPosition().x;
        }

        private static float GetCurrentPositionY(string guid, Dictionary<string, Node> guidToNode)
        {
            if (!guidToNode.TryGetValue(guid, out Node node))
                return 0f;

            return node.GetPosition().y;
        }

        private static float Snap(float value, float snap)
        {
            if (snap <= 0.0001f)
                return value;

            return Mathf.Round(value / snap) * snap;
        }

        private static Vector2 SnapVector(Vector2 value, float snap)
        {
            return new Vector2(Snap(value.x, snap), Snap(value.y, snap));
        }

        private static void NormalizeDesiredPositions(Dictionary<string, Vector2> desiredPos, Settings s)
        {
            if (desiredPos == null || desiredPos.Count == 0)
                return;

            float snap = s?.Snap ?? 0f;
            var keys = desiredPos.Keys.ToList();

            foreach (string guid in keys)
                desiredPos[guid] = SnapVector(desiredPos[guid], snap);
        }

        private static bool ArePositionsEquivalent(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) <= PositionEpsilon &&
                   Mathf.Abs(a.y - b.y) <= PositionEpsilon;
        }

        private static void NormalizeInvalidViewRectsFromSerializedData(
            DialogGraphView view,
            Dictionary<string, Node> guidToNode)
        {
            DialogGraph owner = GetGraphOwner(view);
            if (owner == null || guidToNode == null)
                return;

            foreach (KeyValuePair<string, Node> kv in guidToNode)
            {
                Node node = kv.Value;
                if (node == null)
                    continue;

                Rect rect = node.GetPosition();
                if (IsUsableRect(rect))
                    continue;

                if (!TryGetSerializedNodePosition(owner, kv.Key, out Vector2 position))
                    position = Vector2.zero;

                node.SetPosition(new Rect(position, GetFallbackSizeForNode(node)));
            }
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

        private static Vector2 GetFallbackSizeForNode(Node node)
        {
            if (node is StartNodeView || node is EndNodeView)
                return new Vector2(FallbackBoundaryWidth, FallbackBoundaryHeight);

            if (node is ActionNodeView)
                return new Vector2(FallbackActionWidth, FallbackActionHeight);

            if (node is ConditionNodeView || node is VariableMutationNodeView)
                return new Vector2(FallbackConditionWidth, FallbackConditionHeight);

            if (node is ChoiceNodeView)
                return new Vector2(FallbackNodeWidth, FallbackNodeHeight);

            return new Vector2(FallbackNodeWidth, FallbackNodeHeight);
        }

        private static bool TryGetSerializedNodePosition(DialogGraph owner, string guid, out Vector2 position)
        {
            position = Vector2.zero;

            if (owner == null || string.IsNullOrWhiteSpace(guid))
                return false;

            if (string.Equals(guid, owner.startGuid, StringComparison.Ordinal) && owner.startInitialized)
            {
                position = owner.startPosition;
                return true;
            }

            if (string.Equals(guid, owner.endGuid, StringComparison.Ordinal) && owner.endInitialized)
            {
                position = owner.endPosition;
                return true;
            }

            BaseNode nodeData = owner.nodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.choiceNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.actionNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.conditionNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.variableMutationNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.graphJumpNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.outcomeNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));

            if (nodeData == null)
                return false;

            position = nodeData.GetPosition();
            return true;
        }

        private static Vector2 GetComparableNodePosition(DialogGraph owner, string guid, Node node)
        {
            if (node != null)
            {
                Rect rect = node.GetPosition();
                if (IsUsableRect(rect))
                    return rect.position;
            }

            return TryGetSerializedNodePosition(owner, guid, out Vector2 serializedPosition)
                ? serializedPosition
                : node?.GetPosition().position ?? Vector2.zero;
        }

        private static bool SetSerializedNodePosition(DialogGraph owner, string guid, Vector2 position)
        {
            if (owner == null || string.IsNullOrWhiteSpace(guid))
                return false;

            if (string.Equals(guid, owner.startGuid, StringComparison.Ordinal))
            {
                if (owner.startInitialized && ArePositionsEquivalent(owner.startPosition, position))
                    return false;

                owner.startPosition = position;
                owner.startInitialized = true;
                UnityEditor.EditorUtility.SetDirty(owner);
                return true;
            }

            if (string.Equals(guid, owner.endGuid, StringComparison.Ordinal))
            {
                if (owner.endInitialized && ArePositionsEquivalent(owner.endPosition, position))
                    return false;

                owner.endPosition = position;
                owner.endInitialized = true;
                UnityEditor.EditorUtility.SetDirty(owner);
                return true;
            }

            BaseNode nodeData = owner.nodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.choiceNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.actionNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.conditionNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.variableMutationNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.graphJumpNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));
            nodeData ??= owner.outcomeNodes?.FirstOrDefault(node => node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal));

            if (nodeData == null || ArePositionsEquivalent(nodeData.GetPosition(), position))
                return false;

            nodeData.SetPosition(position);
            return true;
        }

        private static Dictionary<string, Vector2> SnapshotNodePositions(Dictionary<string, Node> guidToNode)
        {
            return guidToNode?
                .Where(kv => kv.Value != null)
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.GetPosition().position,
                    StringComparer.Ordinal) ?? new Dictionary<string, Vector2>(StringComparer.Ordinal);
        }

        private static bool HaveNodePositionsChanged(
            Dictionary<string, Vector2> positionsBefore,
            Dictionary<string, Node> guidToNode)
        {
            if (positionsBefore == null || guidToNode == null)
                return false;

            foreach (KeyValuePair<string, Node> kv in guidToNode)
            {
                if (kv.Value == null)
                    continue;

                if (!positionsBefore.TryGetValue(kv.Key, out Vector2 before))
                    return true;

                if (!ArePositionsEquivalent(before, kv.Value.GetPosition().position))
                    return true;
            }

            return positionsBefore.Keys.Any(guid => !guidToNode.ContainsKey(guid));
        }

        #endregion

        #region ---------------- GUID Extraction ----------------

        private static string GetNodeGuid(GraphElement graphElement)
        {
            if (graphElement == null)
                return string.Empty;

            if (graphElement is DialogNodeView dialogNodeView)
                return dialogNodeView.GUID;

            if (graphElement is ChoiceNodeView choiceNodeView)
                return choiceNodeView.GUID;

            if (graphElement is ActionNodeView actionNodeView)
                return actionNodeView.GUID;

            if (graphElement is StartNodeView startNodeView)
                return startNodeView.GUID;

            if (graphElement is EndNodeView endNodeView)
                return endNodeView.GUID;

            if (graphElement is ConditionNodeView conditionNodeView)
                return conditionNodeView.GUID;

            if (graphElement is VariableMutationNodeView variableMutationNodeView)
                return variableMutationNodeView.GUID;

            if (graphElement is GraphJumpNodeView graphJumpNodeView)
                return graphJumpNodeView.GUID;

            if (graphElement is OutcomeNodeView outcomeNodeView)
                return outcomeNodeView.GUID;

            if (graphElement is Node node)
            {
                if (string.Equals(node.title, "Start", StringComparison.Ordinal))
                    return "Start";

                if (string.Equals(node.title, "End", StringComparison.Ordinal))
                    return "End";

                string userDataGuid = node.userData as string;

                if (!string.IsNullOrWhiteSpace(userDataGuid))
                    return userDataGuid;
            }

            return string.Empty;
        }

        #endregion
    }
}
#endif