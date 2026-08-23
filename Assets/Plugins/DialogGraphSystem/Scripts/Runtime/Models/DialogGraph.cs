using UnityEngine;
using System;
using System.Collections.Generic;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Variables;

namespace DialogSystem.Runtime.Models
{
    /// <summary>
    /// ScriptableObject that holds the full conversation graph:
    /// node collections (dialog/choice/action) and directed links between them.
    /// Start/End data is used by the editor for layout and by runtime traversal
    /// to resolve deterministic graph entry and terminal completion.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogGraph", menuName = "Beka Forge/Dialogues/Dialogue Graph", order = 0)]
    public class DialogGraph : ScriptableObject
    {
        #region -------- Schema --------
        public const string CurrentVersionString = "3.0.0";
        public const int CurrentSchemaVersion = 2;

        [SerializeField, HideInInspector]
        private int graphSchemaVersion;

        public int GraphSchemaVersion => graphSchemaVersion;

        public bool IsCurrentSchemaVersion => graphSchemaVersion == CurrentSchemaVersion;

        [NonSerialized]
        private bool newerSchemaWarningLogged;

        private void OnEnable()
        {
            if (newerSchemaWarningLogged || graphSchemaVersion <= CurrentSchemaVersion)
            {
                return;
            }

            newerSchemaWarningLogged = true;
            Debug.LogWarning($"Graph '{name}' was created with schema version {graphSchemaVersion}, but this Dialogue Graph System version supports schema {CurrentSchemaVersion}. Some features may not be available. The graph was not downgraded or modified.");
        }

        /// <summary>
        /// Sets the serialized schema version during an explicit migration or graph creation flow.
        /// This intentionally does not run any migration logic.
        /// </summary>
        public void SetGraphSchemaVersionForMigration(int schemaVersion)
        {
            graphSchemaVersion = Mathf.Max(0, schemaVersion);
        }

        /// <summary>
        /// Marks this graph as using the current schema during explicit migration.
        /// </summary>
        public void MarkSchemaCurrentForMigration()
        {
            SetGraphSchemaVersionForMigration(CurrentSchemaVersion);
        }

        /// <summary>
        /// Ensures Start and End marker GUIDs exist. This mutates only blank boundary IDs
        /// and is intended for explicit editor authoring or migration paths.
        /// </summary>
        public bool EnsureStartEndGuidsForEditor()
        {
            var changed = false;
            if (string.IsNullOrWhiteSpace(startGuid))
            {
                startGuid = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(endGuid) || string.Equals(endGuid, startGuid, StringComparison.Ordinal))
            {
                endGuid = Guid.NewGuid().ToString("N");
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Enumerates all known node GUIDs, including Start/End boundaries by default.
        /// Blank values are skipped.
        /// </summary>
        public IEnumerable<string> EnumerateAllNodeGuids(bool includeBoundaries = true)
        {
            if (includeBoundaries)
            {
                if (!string.IsNullOrWhiteSpace(startGuid)) yield return startGuid;
                if (!string.IsNullOrWhiteSpace(endGuid)) yield return endGuid;
            }

            foreach (var guid in EnumerateNodeListGuids(nodes)) yield return guid;
            foreach (var guid in EnumerateNodeListGuids(choiceNodes)) yield return guid;
            foreach (var guid in EnumerateNodeListGuids(actionNodes)) yield return guid;
            foreach (var guid in EnumerateNodeListGuids(conditionNodes)) yield return guid;
            foreach (var guid in EnumerateNodeListGuids(variableMutationNodes)) yield return guid;
            foreach (var guid in EnumerateNodeListGuids(graphJumpNodes)) yield return guid;
            foreach (var guid in EnumerateNodeListGuids(outcomeNodes)) yield return guid;
        }

        /// <summary>
        /// Finds an outcome node by its node GUID.
        /// Returns null when the graph has no matching outcome node.
        /// </summary>
        public OutcomeNode FindOutcomeNodeByGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid) || outcomeNodes == null)
            {
                return null;
            }

            for (var i = 0; i < outcomeNodes.Count; i++)
            {
                var node = outcomeNodes[i];
                if (node != null && string.Equals(node.GetGuid(), guid, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds an outcome node by its stable outcome ID.
        /// Returns null when the graph has no matching outcome node.
        /// </summary>
        public OutcomeNode FindOutcomeNodeById(string outcomeId)
        {
            if (string.IsNullOrWhiteSpace(outcomeId) || outcomeNodes == null)
            {
                return null;
            }

            for (var i = 0; i < outcomeNodes.Count; i++)
            {
                var node = outcomeNodes[i];
                if (node != null && string.Equals(node.outcomeId, outcomeId, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to resolve an outcome node by its stable outcome ID.
        /// </summary>
        public bool TryGetOutcomeNodeById(string outcomeId, out OutcomeNode outcomeNode)
        {
            outcomeNode = FindOutcomeNodeById(outcomeId);
            return outcomeNode != null;
        }

        private static IEnumerable<string> EnumerateNodeListGuids<TNode>(IEnumerable<TNode> nodeList)
            where TNode : BaseNode
        {
            if (nodeList == null)
            {
                yield break;
            }

            foreach (var node in nodeList)
            {
                var guid = node?.GetGuid();
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    yield return guid;
                }
            }
        }
        #endregion

        #region -------- Identity --------
        [SerializeField, HideInInspector]
        private string graphGuid;

        /// <summary>
        /// Stable serialized identity for this graph asset. Existing legacy graphs may have this blank
        /// until an explicit migration or creation path assigns it.
        /// </summary>
        public string GraphGuid => graphGuid;

        public bool HasGraphGuid => !string.IsNullOrWhiteSpace(graphGuid);

        /// <summary>
        /// Assigns a stable graph identity only when this graph does not already have one.
        /// </summary>
        public void AssignGraphGuidIfMissing(string guid)
        {
            if (HasGraphGuid || string.IsNullOrWhiteSpace(guid))
            {
                return;
            }

            graphGuid = guid;
        }

        /// <summary>
        /// Sets the serialized graph identity during explicit migration/import flows.
        /// </summary>
        public void SetGraphGuidForMigration(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return;
            }

            graphGuid = guid;
        }
        #endregion

        #region -------- Metadata --------
        [Header("Graph Metadata")]
        [Tooltip("Optional human-readable graph title.")]
        public string graphTitle;

        [TextArea(2, 5)]
        [Tooltip("Optional graph description for authoring and documentation.")]
        public string description;

        [Tooltip("Optional author name for this graph.")]
        public string author;

        [Tooltip("Optional tags used by editor tooling.")]
        public List<string> tags = new();

        [Tooltip("Optional primary category used by editor tooling.")]
        public string primaryCategory;

        [Tooltip("Optional category labels used by editor tooling.")]
        public List<string> categories = new();

        [Tooltip("Optional ISO-8601 UTC timestamp for the last editor-side metadata update.")]
        public string lastModifiedUtc;

        [Tooltip("Optional Unity editor version that last updated graph metadata.")]
        public string editorVersion;
        #endregion

        #region -------- Graph Data --------
        [Tooltip("Directed links connecting nodes by GUID.")]
        public List<GraphLink> links = new();

        [Header("Nodes")]
        [Tooltip("All Dialog nodes in this graph.")]
        public List<DialogNode> nodes = new();

        [Tooltip("All Choice nodes in this graph.")]
        public List<ChoiceNode> choiceNodes = new();

        [Tooltip("All Action nodes in this graph.")]
        public List<ActionNode> actionNodes = new();

        [Tooltip("All Condition nodes in this graph.")]
        public List<ConditionNode> conditionNodes = new();

        [Tooltip("All Variable Mutation nodes in this graph.")]
        public List<VariableMutationNode> variableMutationNodes = new();

        [Tooltip("All Graph Jump nodes in this graph.")]
        public List<GraphJumpNode> graphJumpNodes = new();

        [Tooltip("All Outcome nodes in this graph. Hidden runtime flow nodes that record a named ending result.")]
        public List<OutcomeNode> outcomeNodes = new();
        #endregion

        #region -------- Scene Context --------
        [Header("Scene Context")]
        [Tooltip("Optional reusable scene context asset assigned to this graph.")]
        public DialogSceneContextSO sceneContext;

        [Tooltip("Environment used by this graph.")]
        public DialogEnvironmentSO environment;

        [Tooltip("Characters participating in this graph.")]
        public List<DialogCharacterSO> participatingCharacters = new();

        [Tooltip("Actions available to this graph.")]
        public List<DialogActionSO> availableActions = new();

        [Tooltip("Variables available to this graph.")]
        public List<DialogVariableSO> availableVariables = new();

        [TextArea(2, 5)]
        [Tooltip("Narrative goal for this scene.")]
        public string sceneGoal;

        [Tooltip("Tone guidance for this scene.")]
        public string tone;

        [TextArea(2, 5)]
        [Tooltip("Extra authoring rules for this scene.")]
        public string extraRules;
        #endregion

        #region -------- Start/End (Editor) --------
        [Header("Start (Editor)")]
        /// <summary>
        /// GUID of the explicit Start node. Runtime begins by following this node's outgoing link.
        /// </summary>
        [Tooltip("GUID of the Start marker; runtime follows its outgoing link to the first playable node.")]
        public string startGuid;

        /// <summary>
        /// Editor layout position of the explicit Start node.
        /// </summary>
        [Tooltip("Editor layout position of the Start marker.")]
        public Vector2 startPosition;

        /// <summary>
        /// True after the Start node has been placed by the editor or migration.
        /// </summary>
        [Tooltip("Editor bookkeeping flag for Start placement.")]
        public bool startInitialized = false;

        [Header("End (Editor)")]
        /// <summary>
        /// GUID of the explicit End node. Runtime treats this GUID as a terminal destination.
        /// </summary>
        [Tooltip("GUID of the End marker.")]
        public string endGuid;

        /// <summary>
        /// Editor layout position of the explicit End node.
        /// </summary>
        [Tooltip("Editor layout position of the End marker.")]
        public Vector2 endPosition;

        /// <summary>
        /// True after the End node has been placed by the editor or migration.
        /// </summary>
        [Tooltip("Editor bookkeeping flag for End placement.")]
        public bool endInitialized = false;
        #endregion

        #region -------- Edge Layout (Editor) --------
        /// <summary>
        /// Optional per-edge layout records keyed by <see cref="GraphLink.LinkGuid"/>.
        /// Populated only when the user explicitly edits edge routing in the graph editor.
        /// Never created automatically on graph open or during normal save.
        /// Missing entries are treated as empty/default. Not used by runtime traversal.
        /// </summary>
        [SerializeField, HideInInspector]
        private List<EdgeLayoutRecord> edgeLayouts = new();

        // -------- Read helpers (runtime-safe, never create data) --------

        /// <summary>
        /// Returns the edge layout record whose <c>linkGuid</c> matches <paramref name="linkGuid"/>,
        /// or <c>null</c> if no record exists. Does not create a record.
        /// </summary>
        public EdgeLayoutRecord GetEdgeLayout(string linkGuid)
        {
            if (string.IsNullOrWhiteSpace(linkGuid) || edgeLayouts == null)
            {
                return null;
            }

            for (var i = 0; i < edgeLayouts.Count; i++)
            {
                var record = edgeLayouts[i];
                if (record != null && string.Equals(record.linkGuid, linkGuid, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns <c>true</c> if an edge layout record exists for <paramref name="linkGuid"/>.
        /// Does not create a record.
        /// </summary>
        public bool HasEdgeLayout(string linkGuid)
        {
            return GetEdgeLayout(linkGuid) != null;
        }

        // -------- Editor-only mutating helpers (call only from explicit layout-editing code) --------

        /// <summary>
        /// Returns the existing edge layout record for <paramref name="linkGuid"/>,
        /// or creates and registers a new one if none exists.
        /// <para>
        /// <b>Must only be called from explicit editor layout-editing operations</b> —
        /// never during <c>LoadGraph</c>, automatic save, or graph open.
        /// </para>
        /// The caller is responsible for marking the graph asset dirty after all edits
        /// are complete.
        /// </summary>
        /// <param name="linkGuid">Primary identity. Must match a <see cref="GraphLink.LinkGuid"/>.</param>
        /// <param name="fromGuid">Source node GUID stored as a debug/fallback field (optional).</param>
        /// <param name="toGuid">Destination node GUID stored as a debug/fallback field (optional).</param>
        /// <param name="fromPortIndex">Source port index stored as a debug/fallback field (optional).</param>
        public EdgeLayoutRecord GetOrCreateEdgeLayoutForEditor(
            string linkGuid,
            string fromGuid = null,
            string toGuid = null,
            int fromPortIndex = 0)
        {
            if (string.IsNullOrWhiteSpace(linkGuid))
            {
                throw new ArgumentException("linkGuid must not be null or whitespace.", nameof(linkGuid));
            }

            var existing = GetEdgeLayout(linkGuid);
            if (existing != null)
            {
                return existing;
            }

            edgeLayouts ??= new List<EdgeLayoutRecord>();

            var record = new EdgeLayoutRecord
            {
                linkGuid      = linkGuid,
                fromGuid      = fromGuid ?? string.Empty,
                toGuid        = toGuid   ?? string.Empty,
                fromPortIndex = fromPortIndex,
                reroutePoints = new List<Vector2>()
            };

            edgeLayouts.Add(record);
            return record;
        }

        /// <summary>
        /// Removes the edge layout record whose <c>linkGuid</c> matches <paramref name="linkGuid"/>.
        /// If <paramref name="linkGuid"/> is null or missing from metadata, attempts a fallback 
        /// match using <paramref name="fromGuid"/>, <paramref name="toGuid"/>, and <paramref name="fromPortIndex"/>.
        /// Returns <c>true</c> if a record was removed.
        /// The caller is responsible for marking the graph asset dirty when this returns <c>true</c>.
        /// </summary>
        public bool RemoveEdgeLayout(string linkGuid, string fromGuid = null, string toGuid = null, int fromPortIndex = -1)
        {
            if (edgeLayouts == null || edgeLayouts.Count == 0)
            {
                return false;
            }

            var countBefore = edgeLayouts.Count;
            edgeLayouts.RemoveAll(record =>
            {
                if (record == null) return true;

                // Primary match by linkGuid
                if (!string.IsNullOrWhiteSpace(linkGuid) &&
                    string.Equals(record.linkGuid, linkGuid, StringComparison.Ordinal))
                {
                    return true;
                }

                // Fallback match by connection details (Requirement 3)
                if (string.IsNullOrWhiteSpace(linkGuid) &&
                    !string.IsNullOrWhiteSpace(fromGuid) &&
                    !string.IsNullOrWhiteSpace(toGuid) &&
                    fromPortIndex >= 0)
                {
                    return string.Equals(record.fromGuid, fromGuid, StringComparison.Ordinal) &&
                           string.Equals(record.toGuid, toGuid, StringComparison.Ordinal) &&
                           record.fromPortIndex == fromPortIndex;
                }

                return false;
            });

            return edgeLayouts.Count < countBefore;
        }

        /// <summary>
        /// Removes all edge layout records associated with the given node GUID.
        /// Returns <c>true</c> if any records were removed.
        /// </summary>
        public bool RemoveEdgeLayoutsForNode(string nodeGuid)
        {
            if (string.IsNullOrWhiteSpace(nodeGuid) || edgeLayouts == null || edgeLayouts.Count == 0)
            {
                return false;
            }

            var countBefore = edgeLayouts.Count;
            edgeLayouts.RemoveAll(record =>
                record != null &&
                (string.Equals(record.fromGuid, nodeGuid, StringComparison.Ordinal) ||
                 string.Equals(record.toGuid, nodeGuid, StringComparison.Ordinal)));

            return edgeLayouts.Count < countBefore;
        }

        /// <summary>
        /// Handles cleanup and index shifting for edge layouts when a choice option is deleted.
        /// Records matching the deleted index are removed; records with higher indices from the 
        /// same node are decremented.
        /// </summary>
        public bool RemoveOrShiftEdgeLayoutsForChoice(string nodeGuid, int removedIndex)
        {
            if (string.IsNullOrWhiteSpace(nodeGuid) || edgeLayouts == null || edgeLayouts.Count == 0)
            {
                return false;
            }

            var changed = false;
            for (var i = edgeLayouts.Count - 1; i >= 0; i--)
            {
                var record = edgeLayouts[i];
                if (record == null)
                {
                    edgeLayouts.RemoveAt(i);
                    changed = true;
                    continue;
                }

                if (string.Equals(record.fromGuid, nodeGuid, StringComparison.Ordinal))
                {
                    if (record.fromPortIndex == removedIndex)
                    {
                        edgeLayouts.RemoveAt(i);
                        changed = true;
                    }
                    else if (record.fromPortIndex > removedIndex)
                    {
                        record.fromPortIndex--;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        /// <summary>
        /// Purges all edge layout metadata from the graph.
        /// </summary>
        public void ClearAllEdgeLayouts()
        {
            if (edgeLayouts == null || edgeLayouts.Count == 0)
            {
                return;
            }

            edgeLayouts.Clear();
        }

        /// <summary>
        /// Removes edge layout records whose <c>linkGuid</c> does not correspond to any link
        /// currently present in <see cref="links"/>.
        /// Returns the number of stale records removed.
        /// The caller is responsible for marking the graph asset dirty when the return value is
        /// greater than zero.
        /// </summary>
        public int RemoveEdgeLayoutsForMissingLinks()
        {
            if (edgeLayouts == null || edgeLayouts.Count == 0)
            {
                return 0;
            }

            var activeLinkGuids = new HashSet<string>(StringComparer.Ordinal);
            if (links != null)
            {
                for (var i = 0; i < links.Count; i++)
                {
                    var link = links[i];
                    if (link != null && !string.IsNullOrWhiteSpace(link.LinkGuid))
                    {
                        activeLinkGuids.Add(link.LinkGuid);
                    }
                }
            }

            var countBefore = edgeLayouts.Count;
            edgeLayouts.RemoveAll(record =>
                record == null ||
                string.IsNullOrWhiteSpace(record.linkGuid) ||
                !activeLinkGuids.Contains(record.linkGuid));

            return countBefore - edgeLayouts.Count;
        }
        #endregion

        #region -------- Group Layout (Editor) --------
        /// <summary>
        /// Optional editor-only group layout records keyed by a stable group identity.
        /// Missing entries are treated as empty/default and are ignored by runtime traversal.
        /// </summary>
        [SerializeField, HideInInspector]
        private List<GroupLayoutRecord> groupLayouts = new();

        public IEnumerable<GroupLayoutRecord> EnumerateGroupLayouts()
        {
            if (groupLayouts == null)
            {
                yield break;
            }

            foreach (var record in groupLayouts)
            {
                if (record != null)
                {
                    yield return record;
                }
            }
        }

        public bool EnsureGroupLayoutDefaultsForEditor()
        {
            var changed = false;
            if (groupLayouts == null)
            {
                groupLayouts = new List<GroupLayoutRecord>();
                changed = true;
            }

            for (var i = groupLayouts.Count - 1; i >= 0; i--)
            {
                var record = groupLayouts[i];
                if (record == null)
                {
                    groupLayouts.RemoveAt(i);
                    changed = true;
                    continue;
                }

                if (record.nodeGuids == null)
                {
                    record.nodeGuids = new List<string>();
                    changed = true;
                }
            }

            return changed;
        }

        public GroupLayoutRecord GetGroupLayout(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId) || groupLayouts == null)
            {
                return null;
            }

            for (var i = 0; i < groupLayouts.Count; i++)
            {
                var record = groupLayouts[i];
                if (record != null && string.Equals(record.groupId, groupId, StringComparison.Ordinal))
                {
                    return record;
                }
            }

            return null;
        }

        public GroupLayoutRecord GetOrCreateGroupLayoutForEditor(string groupId, string title = null)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                throw new ArgumentException("groupId must not be null or whitespace.", nameof(groupId));
            }

            var existing = GetGroupLayout(groupId);
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(existing.title))
                {
                    existing.title = title;
                }

                return existing;
            }

            groupLayouts ??= new List<GroupLayoutRecord>();

            var record = new GroupLayoutRecord
            {
                groupId = groupId,
                title = title ?? string.Empty,
                nodeGuids = new List<string>()
            };

            groupLayouts.Add(record);
            return record;
        }

        public bool RemoveGroupLayout(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId) || groupLayouts == null || groupLayouts.Count == 0)
            {
                return false;
            }

            var countBefore = groupLayouts.Count;
            groupLayouts.RemoveAll(record =>
                record == null ||
                string.Equals(record.groupId, groupId, StringComparison.Ordinal));

            return groupLayouts.Count < countBefore;
        }

        public bool RemoveGroupLayoutsForNode(string nodeGuid)
        {
            if (string.IsNullOrWhiteSpace(nodeGuid) || groupLayouts == null || groupLayouts.Count == 0)
            {
                return false;
            }

            var changed = false;
            foreach (var record in groupLayouts)
            {
                if (record?.nodeGuids == null)
                {
                    continue;
                }

                changed |= record.nodeGuids.RemoveAll(guid => string.Equals(guid, nodeGuid, StringComparison.Ordinal)) > 0;
            }

            return changed;
        }

        public void ClearAllGroupLayouts()
        {
            if (groupLayouts == null || groupLayouts.Count == 0)
            {
                return;
            }

            groupLayouts.Clear();
        }
        #endregion
    }

    [Serializable]
    public sealed class GroupLayoutRecord
    {
        [SerializeField, HideInInspector]
        public string groupId;

        [SerializeField, HideInInspector]
        public string title;

        [SerializeField, HideInInspector]
        public string category;

        [SerializeField, HideInInspector]
        public Rect bounds;

        [SerializeField, HideInInspector]
        public Color colorTint = new Color(0.26f, 0.49f, 0.93f, 0.16f);

        [SerializeField, HideInInspector]
        public List<string> nodeGuids = new();
    }

    /// <summary>
    /// Serialized editor layout record for one directed edge.
    /// Keyed by <see cref="GraphLink.LinkGuid"/>. Absent entries are treated as empty/default.
    /// Not created automatically; populated only when the user explicitly edits edge routing.
    /// Not used by runtime traversal.
    /// </summary>
    [System.Serializable]
    public sealed class EdgeLayoutRecord
    {
        /// <summary>
        /// Primary identity — must match the <see cref="GraphLink.LinkGuid"/> of the associated edge.
        /// </summary>
        [SerializeField, HideInInspector]
        public string linkGuid;

        /// <summary>
        /// Ordered reroute control points along this edge.
        /// An empty list means the edge draws as a direct curve with no intermediate points.
        /// </summary>
        [SerializeField, HideInInspector]
        public List<Vector2> reroutePoints = new();

        // ---- Optional debug / fallback fields ----
        // Stored to aid authoring tools if a linkGuid lookup ever fails (e.g. partial upgrade).
        // Never used for runtime traversal or link matching.

        /// <summary>Source node GUID (debug/fallback only — not used for traversal).</summary>
        [SerializeField, HideInInspector]
        public string fromGuid;

        /// <summary>Destination node GUID (debug/fallback only — not used for traversal).</summary>
        [SerializeField, HideInInspector]
        public string toGuid;

        /// <summary>Source port index (debug/fallback only — not used for traversal).</summary>
        [SerializeField, HideInInspector]
        public int fromPortIndex;
    }

    /// <summary>
    /// Directed edge between two nodes in the graph.
    /// </summary>
    [System.Serializable]
    public class GraphLink
    {
        [SerializeField, HideInInspector]
        private string linkGuid;

        /// <summary>
        /// Stable serialized identity for editor tooling. Runtime traversal does not use this value.
        /// </summary>
        public string LinkGuid => linkGuid;

        /// <summary>
        /// True when this link already has an editor identity assigned.
        /// </summary>
        public bool HasLinkGuid => !string.IsNullOrWhiteSpace(linkGuid);

        /// <summary>
        /// Assigns an editor link identity only when this legacy link does not already have one.
        /// </summary>
        public void AssignLinkGuidIfMissing(string guid)
        {
            if (HasLinkGuid || string.IsNullOrWhiteSpace(guid))
            {
                return;
            }

            linkGuid = guid;
        }

        /// <summary>
        /// Sets the serialized link identity during explicit migration/import flows.
        /// </summary>
        public void SetLinkGuidForMigration(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return;
            }

            linkGuid = guid;
        }

        [Tooltip("Source node GUID.")]
        public string fromGuid;

        [Tooltip("Destination node GUID.")]
        public string toGuid;

        [Tooltip("Stable output port key on the source node. Legacy links may leave this blank and use fromPortIndex.")]
        public string fromPortKey;

        [Tooltip("Stable input port key on the destination node. Usually \"default\" for current node types.")]
        public string toPortKey;

        [Tooltip("Output port index on the source node (0 for dialog/auto, choice index for choices).")]
        public int fromPortIndex;

        public bool HasFromPortKey => !string.IsNullOrWhiteSpace(fromPortKey);

        public bool HasToPortKey => !string.IsNullOrWhiteSpace(toPortKey);
    }

    /// <summary>
    /// Stable serialized keys for graph node ports. Indexes remain as legacy fallback data.
    /// </summary>
    public static class DialogGraphPortKeys
    {
        public const string Default = "default";
        public const string True = "true";
        public const string False = "false";
        public const string ActionSuccess = "action:success";
        public const string ActionFail = "action:fail";

        private const string ChoicePrefix = "choice:";

        public static string ForChoiceId(string choiceId)
        {
            return string.IsNullOrWhiteSpace(choiceId) ? string.Empty : ChoicePrefix + choiceId;
        }

        public static bool TryGetChoiceId(string portKey, out string choiceId)
        {
            choiceId = string.Empty;
            if (string.IsNullOrWhiteSpace(portKey) ||
                !portKey.StartsWith(ChoicePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            choiceId = portKey.Substring(ChoicePrefix.Length);
            return !string.IsNullOrWhiteSpace(choiceId);
        }
    }
}
