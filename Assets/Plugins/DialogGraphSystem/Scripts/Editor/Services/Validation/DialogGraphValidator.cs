using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Variables;
using UnityEngine;

namespace DialogSystem.EditorTools.Services.Validation
{
    /// <summary>
    /// Read-only validation boundary for dialog graph assets.
    /// </summary>
    public interface IDialogGraphValidator
    {
        /// <summary>
        /// Validates a graph without optional project registry constraints.
        /// </summary>
        DialogGraphValidationResult Validate(DialogGraph graph);

        /// <summary>
        /// Validates a graph with optional speaker and action constraints.
        /// </summary>
        DialogGraphValidationResult Validate(
            DialogGraph graph,
            IEnumerable<string> knownSpeakerIds,
            IEnumerable<string> knownActionIds);

        /// <summary>
        /// Validates a graph with optional speaker, action, and variable constraints.
        /// </summary>
        DialogGraphValidationResult Validate(
            DialogGraph graph,
            IEnumerable<string> knownSpeakerIds,
            IEnumerable<string> knownActionIds,
            IEnumerable<DialogVariableSO> knownVariables);

        /// <summary>
        /// Validates a graph with all optional constraints plus localization key checks.
        /// </summary>
        DialogGraphValidationResult Validate(
            DialogGraph graph,
            IEnumerable<string> knownSpeakerIds,
            IEnumerable<string> knownActionIds,
            IEnumerable<DialogVariableSO> knownVariables,
            bool hasLocaleSource);
    }

    /// <summary>
    /// Read-only structural validator for a <see cref="DialogGraph"/> asset.
    ///
    /// Usage:
    /// <code>
    ///   var result = DialogGraphValidator.Validate(graph);
    ///   // -- or, with known speaker/action sets --
    ///   var result = DialogGraphValidator.Validate(graph, knownSpeakerIds, knownActionIds);
    ///
    ///   foreach (var issue in result.Issues)
    ///       Debug.Log(issue);
    /// </code>
    ///
    /// Contract:
    /// - Never mutates the graph or any asset.
    /// - Never calls AssetDatabase, Undo, or EditorUtility.SetDirty.
    /// - Safe to call from any editor-only context (preview window, command executor, menu item).
    /// </summary>
    public sealed class DefaultDialogGraphValidator : IDialogGraphValidator
    {
        private const string StartAlias = "Start";
        private const string EndAlias = "End";
        private static readonly Regex VariableTokenRegex = new Regex(
            @"\{\{\s*(?<key>[^{}]+?)\s*\}\}|\{var:\s*(?<key>[^{}]+?)\s*\}|\{\s*(?<key>[A-Za-z_][A-Za-z0-9_.-]*)\s*\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // ── overlap detection threshold (graph-space units) ──────────────────
        private const float OverlapThreshold = 30f;

        // ─────────────────────────────────────────────────────────────────────
        //  Public entry points
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates <paramref name="graph"/> with no known-speaker or known-action
        /// constraints (those checks are skipped if the sets are empty).
        /// </summary>
        public DialogGraphValidationResult Validate(DialogGraph graph)
        {
            return Validate(graph, null, null, null);
        }

        /// <summary>
        /// Validates <paramref name="graph"/> and optionally checks speaker IDs
        /// and action IDs against caller-supplied authoritative sets.
        /// </summary>
        /// <param name="graph">The graph asset to inspect. Must not be null.</param>
        /// <param name="knownSpeakerIds">
    ///   Set of valid speaker IDs (e.g. DialogCharacterSO.CharacterID values).
        ///   Pass null or empty to skip the speaker-ID check.
        /// </param>
        /// <param name="knownActionIds">
        ///   Set of valid action IDs (e.g. DialogActionSO.ActionID values).
        ///   Pass null or empty to skip the action-ID check.
        /// </param>
        public DialogGraphValidationResult Validate(
            DialogGraph graph,
            IEnumerable<string> knownSpeakerIds,
            IEnumerable<string> knownActionIds)
        {
            return Validate(graph, NullIfEmpty(knownSpeakerIds), NullIfEmpty(knownActionIds), null);
        }

        /// <summary>
        /// Validates <paramref name="graph"/> and optionally checks speaker,
        /// action, and variable references against caller-supplied authoritative sets.
        /// </summary>
        public DialogGraphValidationResult Validate(
            DialogGraph graph,
            IEnumerable<string> knownSpeakerIds,
            IEnumerable<string> knownActionIds,
            IEnumerable<DialogVariableSO> knownVariables)
        {
            if (graph == null)
            {
                var nullIssue = new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "NULL_GRAPH",
                    "The supplied DialogGraph is null.");
                return new DialogGraphValidationResult(new[] { nullIssue });
            }

            var issues = new List<DialogGraphValidationIssue>();
            var ctx    = new ValidationContext(graph, knownSpeakerIds, knownActionIds, knownVariables);

            // ── structural / identity ──────────────────────────────────────
            CheckGraphIdentityAndSchema(ctx, issues);
            CheckMissingNodeGuids(ctx, issues);
            CheckDuplicateGuids(ctx, issues);
            CheckChoiceIds(ctx, issues);
            CheckStartEndNodes(ctx, issues);

            // ── link integrity ─────────────────────────────────────────────
            CheckLinkIdentities(ctx, issues);
            CheckUnknownGuidReferences(ctx, issues);
            CheckLinkPortKeys(ctx, issues);
            CheckChoiceLinkConsistency(ctx, issues);
            CheckEndNodeOutgoingLinks(ctx, issues);
            CheckStartNodeIncomingLinks(ctx, issues);
            CheckStartNodeOutgoingLinks(ctx, issues);

            // ── reachability ───────────────────────────────────────────────
            CheckOrphanNodes(ctx, issues);
            CheckUnreachableNodes(ctx, issues);
            CheckDeadEnds(ctx, issues);

            // ── node-level rules ───────────────────────────────────────────
            CheckDialogNodeRules(ctx, issues);
            CheckChoiceNodeRules(ctx, issues);
            CheckActionNodeRules(ctx, issues);
            CheckConditionNodeRules(ctx, issues);
            CheckVariableMutationNodeRules(ctx, issues);
            CheckGraphJumpNodeRules(ctx, issues);
            CheckOutcomeNodeRules(ctx, issues);
            CheckHiddenFlowCycles(ctx, issues);

            // ── optional semantic checks ───────────────────────────────────
            if (ctx.HasKnownSpeakers) CheckInvalidSpeakerIds(ctx, issues);
            if (ctx.HasKnownActions)  CheckInvalidActionIds(ctx, issues);
            if (ctx.HasKnownVariables) CheckInvalidVariableReferences(ctx, issues);

            // ── localization checks ────────────────────────────────────────
            if (ctx.HasLocaleSource) CheckLocaleKeys(ctx, issues);

            // ── layout warnings ────────────────────────────────────────────
            CheckNodeOverlap(ctx, issues);

            return new DialogGraphValidationResult(issues);
        }

        /// <summary>
        /// Validates <paramref name="graph"/> with all optional constraints plus
        /// locale key checks when <paramref name="hasLocaleSource"/> is <c>true</c>.
        /// </summary>
        public DialogGraphValidationResult Validate(
            DialogGraph graph,
            IEnumerable<string> knownSpeakerIds,
            IEnumerable<string> knownActionIds,
            IEnumerable<DialogVariableSO> knownVariables,
            bool hasLocaleSource)
        {
            var result = Validate(graph, knownSpeakerIds, knownActionIds, knownVariables);

            if (!hasLocaleSource || graph == null) return result;

            // Re-run only the locale check with the flag set.
            var extraIssues = new List<DialogGraphValidationIssue>();
            var ctx = new ValidationContext(graph, knownSpeakerIds, knownActionIds, knownVariables)
            {
                HasLocaleSource = true
            };
            CheckLocaleKeys(ctx, extraIssues);

            if (extraIssues.Count == 0) return result;

            var combined = new List<DialogGraphValidationIssue>(result.Issues);
            combined.AddRange(extraIssues);
            return new DialogGraphValidationResult(combined);
        }



        private static IEnumerable<string> NullIfEmpty(IEnumerable<string> values)
        {
            if (values == null)
            {
                return null;
            }

            var list = values.ToList();
            return list.Count == 0 ? null : list;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Context (pre-computed lookup tables)
        // ─────────────────────────────────────────────────────────────────────

        private sealed class ValidationContext
        {
            public readonly DialogGraph Graph;
            public readonly string EffectiveStartGuid;
            public readonly string EffectiveEndGuid;
            public readonly bool HasStartBoundary;
            public readonly bool HasEndBoundary;

            // All GUIDs present in the graph (Start/End stored separately).
            public readonly HashSet<string> AllGuids = new HashSet<string>();

            // Per-type GUID sets (for targeted checks).
            public readonly HashSet<string> DialogGuids  = new HashSet<string>();
            public readonly HashSet<string> ChoiceGuids  = new HashSet<string>();
            public readonly HashSet<string> ActionGuids  = new HashSet<string>();
            public readonly HashSet<string> ConditionGuids = new HashSet<string>();
            public readonly HashSet<string> VariableMutationGuids = new HashSet<string>();
            public readonly HashSet<string> GraphJumpGuids = new HashSet<string>();
            public readonly HashSet<string> OutcomeGuids = new HashSet<string>();

            // Nodes by GUID (fast lookup).
            public readonly Dictionary<string, DialogNode> DialogById  = new Dictionary<string, DialogNode>();
            public readonly Dictionary<string, ChoiceNode> ChoiceById  = new Dictionary<string, ChoiceNode>();
            public readonly Dictionary<string, ActionNode> ActionById  = new Dictionary<string, ActionNode>();
            public readonly Dictionary<string, ConditionNode> ConditionById = new Dictionary<string, ConditionNode>();
            public readonly Dictionary<string, VariableMutationNode> VariableMutationById = new Dictionary<string, VariableMutationNode>();
            public readonly Dictionary<string, GraphJumpNode> GraphJumpById = new Dictionary<string, GraphJumpNode>();
            public readonly Dictionary<string, OutcomeNode> OutcomeById = new Dictionary<string, OutcomeNode>();

            // Link maps derived from graph.links.
            public readonly Dictionary<string, List<GraphLink>> OutgoingByGuid = new Dictionary<string, List<GraphLink>>();
            public readonly HashSet<string>                     GuidsWithIncoming = new HashSet<string>();

            // Optional semantic constraints.
            public readonly HashSet<string> KnownSpeakers;
            public readonly HashSet<string> KnownActions;
            public readonly Dictionary<string, DialogueVariableValueType> KnownVariables;
            public bool HasKnownSpeakers  => KnownSpeakers  != null;
            public bool HasKnownActions   => KnownActions   != null;
            public bool HasKnownVariables => KnownVariables != null;

            /// <summary>
            /// When true, the project has at least one localization table, so locale
            /// keys are expected on nodes and missing keys generate warnings.
            /// </summary>
            public bool HasLocaleSource { get; internal set; }

            public ValidationContext(
                DialogGraph graph,
                IEnumerable<string> knownSpeakers,
                IEnumerable<string> knownActions,
                IEnumerable<DialogVariableSO> knownVariables)
            {
                Graph = graph;

                var hasStartAliasReference = HasBoundaryReference(graph.links, StartAlias) ||
                                             HasBoundaryReference(graph.choiceNodes, StartAlias);
                var hasEndAliasReference = HasBoundaryReference(graph.links, EndAlias) ||
                                           HasBoundaryReference(graph.choiceNodes, EndAlias);

                EffectiveStartGuid = !string.IsNullOrWhiteSpace(graph.startGuid)
                    ? graph.startGuid
                    : hasStartAliasReference || graph.startInitialized
                        ? StartAlias
                        : null;

                EffectiveEndGuid = !string.IsNullOrWhiteSpace(graph.endGuid)
                    ? graph.endGuid
                    : hasEndAliasReference || graph.endInitialized
                        ? EndAlias
                        : null;

                HasStartBoundary = !string.IsNullOrWhiteSpace(EffectiveStartGuid);
                HasEndBoundary = !string.IsNullOrWhiteSpace(EffectiveEndGuid);

                // Speakers / actions
                KnownSpeakers = knownSpeakers != null
                    ? new HashSet<string>(
                        knownSpeakers
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.Trim()),
                        StringComparer.OrdinalIgnoreCase)
                    : null;
                KnownActions = knownActions != null
                    ? new HashSet<string>(
                        knownActions
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.Trim()),
                        StringComparer.OrdinalIgnoreCase)
                    : null;
                KnownVariables = BuildKnownVariableMap(knownVariables);

                // Index nodes
                foreach (var n in graph.nodes ?? Enumerable.Empty<DialogNode>())
                {
                    if (n == null) continue;
                    var g = n.GetGuid();
                    if (!string.IsNullOrEmpty(g))
                    {
                        AllGuids.Add(g);
                        DialogGuids.Add(g);
                        DialogById[g] = n;
                    }
                }

                foreach (var n in graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
                {
                    if (n == null) continue;
                    var g = n.GetGuid();
                    if (!string.IsNullOrEmpty(g))
                    {
                        AllGuids.Add(g);
                        ChoiceGuids.Add(g);
                        ChoiceById[g] = n;
                    }
                }

                foreach (var n in graph.actionNodes ?? Enumerable.Empty<ActionNode>())
                {
                    if (n == null) continue;
                    var g = n.GetGuid();
                    if (!string.IsNullOrEmpty(g))
                    {
                        AllGuids.Add(g);
                        ActionGuids.Add(g);
                        ActionById[g] = n;
                    }
                }

                foreach (var n in graph.conditionNodes ?? Enumerable.Empty<ConditionNode>())
                {
                    if (n == null) continue;
                    var g = n.GetGuid();
                    if (!string.IsNullOrEmpty(g))
                    {
                        AllGuids.Add(g);
                        ConditionGuids.Add(g);
                        ConditionById[g] = n;
                    }
                }

                foreach (var n in graph.variableMutationNodes ?? Enumerable.Empty<VariableMutationNode>())
                {
                    if (n == null) continue;
                    var g = n.GetGuid();
                    if (!string.IsNullOrEmpty(g))
                    {
                        AllGuids.Add(g);
                        VariableMutationGuids.Add(g);
                        VariableMutationById[g] = n;
                    }
                }

                foreach (var n in graph.graphJumpNodes ?? Enumerable.Empty<GraphJumpNode>())
                {
                    if (n == null) continue;
                    var g = n.GetGuid();
                    if (!string.IsNullOrEmpty(g))
                    {
                        AllGuids.Add(g);
                        GraphJumpGuids.Add(g);
                        GraphJumpById[g] = n;
                    }
                }

                foreach (var n in graph.outcomeNodes ?? Enumerable.Empty<OutcomeNode>())
                {
                    if (n == null) continue;
                    var g = n.GetGuid();
                    if (!string.IsNullOrEmpty(g))
                    {
                        AllGuids.Add(g);
                        OutcomeGuids.Add(g);
                        OutcomeById[g] = n;
                    }
                }

                // Boundary aliases may appear in links before the graph is fully normalized on disk.
                RegisterBoundaryGuid(AllGuids, EffectiveStartGuid, StartAlias);
                RegisterBoundaryGuid(AllGuids, EffectiveEndGuid, EndAlias);

                // Index links
                foreach (var link in graph.links ?? Enumerable.Empty<GraphLink>())
                {
                    if (link == null) continue;

                    if (!OutgoingByGuid.TryGetValue(link.fromGuid, out var list))
                    {
                        list = new List<GraphLink>();
                        OutgoingByGuid[link.fromGuid] = list;
                    }
                    list.Add(link);

                    if (!string.IsNullOrEmpty(link.toGuid))
                        GuidsWithIncoming.Add(link.toGuid);
                }
            }

            /// <summary>Returns all GUIDs that appear in content nodes (not Start/End).</summary>
            public IEnumerable<string> AllContentGuids()
            {
                foreach (var g in DialogGuids) yield return g;
                foreach (var g in ChoiceGuids) yield return g;
                foreach (var g in ActionGuids) yield return g;
                foreach (var g in ConditionGuids) yield return g;
                foreach (var g in VariableMutationGuids) yield return g;
                foreach (var g in OutcomeGuids) yield return g;
            }

            public bool IsStartGuid(string guid)
            {
                return MatchesBoundaryGuid(guid, EffectiveStartGuid, StartAlias);
            }

            public bool IsEndGuid(string guid)
            {
                return MatchesBoundaryGuid(guid, EffectiveEndGuid, EndAlias);
            }

            private static bool HasBoundaryReference(IEnumerable<GraphLink> links, string alias)
            {
                return links != null && links.Any(link =>
                    link != null &&
                    (string.Equals(link.fromGuid, alias, System.StringComparison.Ordinal) ||
                     string.Equals(link.toGuid, alias, System.StringComparison.Ordinal)));
            }

            private static bool HasBoundaryReference(IEnumerable<ChoiceNode> choiceNodes, string alias)
            {
                return choiceNodes != null && choiceNodes.Any(node =>
                    node?.choices != null &&
                    node.choices.Any(choice =>
                        choice != null &&
                        string.Equals(choice.nextNodeGUID, alias, System.StringComparison.Ordinal)));
            }

            private static void RegisterBoundaryGuid(HashSet<string> allGuids, string effectiveGuid, string alias)
            {
                if (string.IsNullOrWhiteSpace(effectiveGuid))
                    return;

                allGuids.Add(effectiveGuid);
                allGuids.Add(alias);
            }

            private static bool MatchesBoundaryGuid(string guid, string effectiveGuid, string alias)
            {
                if (string.IsNullOrWhiteSpace(guid))
                    return false;

                return string.Equals(guid, alias, System.StringComparison.Ordinal) ||
                       (!string.IsNullOrWhiteSpace(effectiveGuid) &&
                        string.Equals(guid, effectiveGuid, System.StringComparison.Ordinal));
            }

            private static Dictionary<string, DialogueVariableValueType> BuildKnownVariableMap(IEnumerable<DialogVariableSO> variables)
            {
                if (variables == null)
                {
                    return null;
                }

                var map = new Dictionary<string, DialogueVariableValueType>(StringComparer.OrdinalIgnoreCase);
                foreach (var variable in variables)
                {
                    if (variable == null || string.IsNullOrWhiteSpace(variable.Key))
                    {
                        continue;
                    }

                    var key = variable.Key.Trim();
                    if (!map.ContainsKey(key))
                    {
                        map.Add(key, variable.ValueType);
                    }
                }

                return map;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Check implementations
        // ─────────────────────────────────────────────────────────────────────

        // ── 1. Duplicate GUIDs ────────────────────────────────────────────────
        private static void CheckGraphIdentityAndSchema(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            var graph = ctx.Graph;
            if (!graph.HasGraphGuid)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "MISSING_GRAPH_GUID",
                    "Graph has no stable graphGuid. Run the explicit graph upgrade before release."));
            }

            var schemaVersion = graph.GraphSchemaVersion;
            if (schemaVersion < 0 || schemaVersion > DialogGraph.CurrentSchemaVersion)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "INVALID_SCHEMA_VERSION",
                    $"Graph schema version {schemaVersion} is outside the supported range 0-{DialogGraph.CurrentSchemaVersion}.",
                    graphGuid: graph.GraphGuid));
            }
            else if (schemaVersion < DialogGraph.CurrentSchemaVersion)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "OLD_SCHEMA_VERSION",
                    $"Graph schema version {schemaVersion} is older than current version {DialogGraph.CurrentSchemaVersion}. Run the explicit graph upgrade.",
                    graphGuid: graph.GraphGuid));
            }
        }

        private static void CheckMissingNodeGuids(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            CheckMissingNodeGuids(ctx.Graph.nodes, "Dialog", issues);
            CheckMissingNodeGuids(ctx.Graph.choiceNodes, "Choice", issues);
            CheckMissingNodeGuids(ctx.Graph.actionNodes, "Action", issues);
            CheckMissingNodeGuids(ctx.Graph.conditionNodes, "Condition", issues);
            CheckMissingNodeGuids(ctx.Graph.variableMutationNodes, "Variable mutation", issues);
            CheckMissingNodeGuids(ctx.Graph.graphJumpNodes, "Graph jump", issues);
            CheckMissingNodeGuids(ctx.Graph.outcomeNodes, "Outcome", issues);
        }

        private static void CheckMissingNodeGuids<TNode>(
            IEnumerable<TNode> nodes,
            string nodeLabel,
            List<DialogGraphValidationIssue> issues)
            where TNode : BaseNode
        {
            var index = 0;
            foreach (var node in nodes ?? Enumerable.Empty<TNode>())
            {
                if (node == null)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "NULL_NODE_ENTRY",
                        $"{nodeLabel} node list contains a null entry at index {index}."));
                }
                else if (string.IsNullOrWhiteSpace(node.GetGuid()))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "MISSING_NODE_GUID",
                        $"{nodeLabel} node at index {index} has no stable node GUID."));
                }

                index++;
            }
        }

        private static void CheckChoiceIds(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            var graphWideChoiceIds = new HashSet<string>(StringComparer.Ordinal);
            var duplicateChoiceIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var node in ctx.Graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (node?.choices == null)
                {
                    continue;
                }

                var nodeGuid = node.GetGuid();
                var perNodeChoiceIds = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < node.choices.Count; i++)
                {
                    var choice = node.choices[i];
                    if (choice == null)
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "NULL_CHOICE_ENTRY",
                            $"Choice node '{nodeGuid}' contains a null choice at index {i}.",
                            nodeGuid));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(choice.choiceId))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "MISSING_CHOICE_ID",
                            $"Choice node '{nodeGuid}', choice index {i} has no stable choiceId.",
                            nodeGuid));
                        continue;
                    }

                    if (!perNodeChoiceIds.Add(choice.choiceId) || !graphWideChoiceIds.Add(choice.choiceId))
                    {
                        duplicateChoiceIds.Add(choice.choiceId);
                    }
                }
            }

            foreach (var duplicateChoiceId in duplicateChoiceIds)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "DUPLICATE_CHOICE_ID",
                    $"Choice ID '{duplicateChoiceId}' is assigned to more than one choice.",
                    choiceId: duplicateChoiceId));
            }
        }

        private static void CheckLinkIdentities(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;

            foreach (var link in ctx.Graph.links ?? Enumerable.Empty<GraphLink>())
            {
                if (link == null)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "NULL_LINK_ENTRY",
                        $"Graph link list contains a null entry at index {index}."));
                    index++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(link.LinkGuid))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "MISSING_LINK_GUID",
                        $"Link from '{link.fromGuid}' to '{link.toGuid}' has no stable linkGuid.",
                        link.fromGuid));
                }
                else if (!seen.Add(link.LinkGuid))
                {
                    duplicates.Add(link.LinkGuid);
                }

                index++;
            }

            foreach (var duplicateLinkGuid in duplicates)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "DUPLICATE_LINK_GUID",
                    $"Link GUID '{duplicateLinkGuid}' is assigned to more than one link.",
                    linkGuid: duplicateLinkGuid));
            }
        }

        private static void CheckDuplicateGuids(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            // Collect every raw GUID from every node list (including nulls/empties).
            var allRaw = new List<string>();

            foreach (var n in ctx.Graph.nodes ?? Enumerable.Empty<DialogNode>())
                allRaw.Add(n?.GetGuid());
            foreach (var n in ctx.Graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
                allRaw.Add(n?.GetGuid());
            foreach (var n in ctx.Graph.actionNodes ?? Enumerable.Empty<ActionNode>())
                allRaw.Add(n?.GetGuid());
            foreach (var n in ctx.Graph.conditionNodes ?? Enumerable.Empty<ConditionNode>())
                allRaw.Add(n?.GetGuid());
            foreach (var n in ctx.Graph.variableMutationNodes ?? Enumerable.Empty<VariableMutationNode>())
                allRaw.Add(n?.GetGuid());
            foreach (var n in ctx.Graph.graphJumpNodes ?? Enumerable.Empty<GraphJumpNode>())
                allRaw.Add(n?.GetGuid());
            foreach (var n in ctx.Graph.outcomeNodes ?? Enumerable.Empty<OutcomeNode>())
                allRaw.Add(n?.GetGuid());

            // Also include start/end.
            allRaw.Add(ctx.Graph.startGuid);
            allRaw.Add(ctx.Graph.endGuid);

            var seen   = new HashSet<string>();
            var dupes  = new HashSet<string>();

            foreach (var g in allRaw)
            {
                if (string.IsNullOrEmpty(g)) continue;
                if (!seen.Add(g))            dupes.Add(g);
            }

            foreach (var dup in dupes)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "DUPLICATE_GUID",
                    $"GUID '{dup}' is assigned to more than one node.",
                    dup));
            }
        }

        // ── 2. Start / End presence ───────────────────────────────────────────
        private static void CheckStartEndNodes(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            var g = ctx.Graph;

            if (!ctx.HasStartBoundary)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "MISSING_START",
                    "Graph has no Start node (startGuid is empty)."));
            }
            else if (string.IsNullOrWhiteSpace(g.startGuid))
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "START_GUID_EMPTY",
                    "Start node metadata is not normalized yet (startGuid is empty). Save the graph to persist the boundary GUID."));
            }

            if (!ctx.HasEndBoundary)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "MISSING_END",
                    "Graph has no End node (endGuid is empty)."));
            }
            else if (string.IsNullOrWhiteSpace(g.endGuid))
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "END_GUID_EMPTY",
                    "End node metadata is not normalized yet (endGuid is empty). Save the graph to persist the boundary GUID."));
            }
        }

        // ── 3. Unknown GUID references in links ───────────────────────────────
        private static void CheckUnknownGuidReferences(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var link in ctx.Graph.links ?? Enumerable.Empty<GraphLink>())
            {
                if (link == null) continue;

                if (string.IsNullOrEmpty(link.fromGuid) || !ctx.AllGuids.Contains(link.fromGuid))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "UNKNOWN_FROM_GUID",
                        $"Link references unknown source GUID '{link.fromGuid}'.",
                        link.fromGuid,
                        link.LinkGuid));
                }

                if (string.IsNullOrEmpty(link.toGuid) || !ctx.AllGuids.Contains(link.toGuid))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "UNKNOWN_TO_GUID",
                        $"Link references unknown destination GUID '{link.toGuid}'.",
                        link.toGuid,
                        link.LinkGuid));
                }
            }

            // Also check Choice.nextNodeGUID references.
            foreach (var n in ctx.Graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (n == null) continue;
                foreach (var c in n.choices ?? Enumerable.Empty<Choice>())
                {
                    if (c == null) continue;
                    if (string.IsNullOrEmpty(c.nextNodeGUID)) continue;
                    if (!ctx.AllGuids.Contains(c.nextNodeGUID))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Error,
                            "UNKNOWN_CHOICE_TARGET",
                            $"Choice '{c.answerText}' on ChoiceNode '{n.GetGuid()}' references unknown target GUID '{c.nextNodeGUID}'.",
                            n.GetGuid(),
                            choiceId: c.choiceId));
                    }
                }
            }
        }

        // ── 4. End node must have no outgoing links ───────────────────────────
        private static void CheckLinkPortKeys(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var link in ctx.Graph.links ?? Enumerable.Empty<GraphLink>())
            {
                if (link == null)
                {
                    continue;
                }

                ValidateInputPortKey(link, issues);

                if (string.IsNullOrWhiteSpace(link.fromGuid) || !ctx.AllGuids.Contains(link.fromGuid))
                {
                    continue;
                }

                if (ctx.IsEndGuid(link.fromGuid))
                {
                    continue;
                }

                var result = TryResolveOutputPort(ctx, link, out var resolvedPortKey, out var choiceId);
                if (result == PortResolutionResult.Valid)
                {
                    if (string.IsNullOrWhiteSpace(link.fromPortKey))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "MISSING_FROM_PORT_KEY_LEGACY",
                            $"Link '{FormatLinkId(link)}' has no fromPortKey and is using legacy fromPortIndex {link.fromPortIndex}. Run the explicit graph upgrade to stamp stable port keys.",
                            link.fromGuid,
                            link.LinkGuid,
                            choiceId));
                    }

                    continue;
                }

                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    result == PortResolutionResult.MissingPortIdentity
                        ? "MISSING_FROM_PORT_IDENTITY"
                        : "LINK_FROM_PORT_MISSING",
                    result == PortResolutionResult.MissingPortIdentity
                        ? $"Link '{FormatLinkId(link)}' has no usable output port identity."
                        : $"Link '{FormatLinkId(link)}' references output port '{GetDisplayPortIdentity(link)}' that does not exist on source node '{link.fromGuid}'.",
                    link.fromGuid,
                    link.LinkGuid,
                    choiceId));

                if (!string.IsNullOrWhiteSpace(resolvedPortKey) &&
                    !string.IsNullOrWhiteSpace(link.fromPortKey) &&
                    !string.Equals(link.fromPortKey, resolvedPortKey, StringComparison.Ordinal))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "CHOICE_PORT_KEY_MISMATCH",
                        $"Link '{FormatLinkId(link)}' uses fromPortKey '{link.fromPortKey}' but legacy index {link.fromPortIndex} maps to '{resolvedPortKey}'.",
                        link.fromGuid,
                        link.LinkGuid,
                        choiceId));
                }
            }
        }

        private static void ValidateInputPortKey(GraphLink link, List<DialogGraphValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(link.toPortKey))
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "MISSING_TO_PORT_KEY_LEGACY",
                    $"Link '{FormatLinkId(link)}' has no toPortKey and is using the default input implicitly.",
                    link.toGuid,
                    link.LinkGuid));
                return;
            }

            if (!string.Equals(link.toPortKey, DialogGraphPortKeys.Default, StringComparison.Ordinal))
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "INVALID_TO_PORT_KEY",
                    $"Link '{FormatLinkId(link)}' targets input port '{link.toPortKey}', but current node types only expose '{DialogGraphPortKeys.Default}' input ports.",
                    link.toGuid,
                    link.LinkGuid));
            }
        }

        private static PortResolutionResult TryResolveOutputPort(
            ValidationContext ctx,
            GraphLink link,
            out string expectedPortKey,
            out string choiceId)
        {
            expectedPortKey = string.Empty;
            choiceId = null;

            if (ctx.IsStartGuid(link.fromGuid))
            {
                expectedPortKey = DialogGraphPortKeys.Default;
                return ValidateSingleOutputPort(link, expectedPortKey);
            }

            if (ctx.DialogGuids.Contains(link.fromGuid) ||
                ctx.VariableMutationGuids.Contains(link.fromGuid) ||
                ctx.GraphJumpGuids.Contains(link.fromGuid) ||
                ctx.OutcomeGuids.Contains(link.fromGuid))
            {
                expectedPortKey = DialogGraphPortKeys.Default;
                return ValidateSingleOutputPort(link, expectedPortKey);
            }

            if (ctx.ActionGuids.Contains(link.fromGuid))
            {
                expectedPortKey = DialogGraphPortKeys.ActionSuccess;
                return ValidateSingleOutputPort(link, expectedPortKey);
            }

            if (ctx.ConditionGuids.Contains(link.fromGuid))
            {
                return ValidateConditionOutputPort(link, out expectedPortKey);
            }

            if (ctx.ChoiceById.TryGetValue(link.fromGuid, out var choiceNode))
            {
                return ValidateChoiceOutputPort(choiceNode, link, out expectedPortKey, out choiceId);
            }

            return PortResolutionResult.MissingPort;
        }

        private static PortResolutionResult ValidateSingleOutputPort(GraphLink link, string expectedPortKey)
        {
            if (!string.IsNullOrWhiteSpace(link.fromPortKey))
            {
                return string.Equals(link.fromPortKey, expectedPortKey, StringComparison.Ordinal)
                    ? PortResolutionResult.Valid
                    : PortResolutionResult.MissingPort;
            }

            return link.fromPortIndex == 0
                ? PortResolutionResult.Valid
                : PortResolutionResult.MissingPort;
        }

        private static PortResolutionResult ValidateConditionOutputPort(GraphLink link, out string expectedPortKey)
        {
            expectedPortKey = string.Empty;
            if (!string.IsNullOrWhiteSpace(link.fromPortKey))
            {
                if (string.Equals(link.fromPortKey, DialogGraphPortKeys.True, StringComparison.Ordinal))
                {
                    expectedPortKey = DialogGraphPortKeys.True;
                    return PortResolutionResult.Valid;
                }

                if (string.Equals(link.fromPortKey, DialogGraphPortKeys.False, StringComparison.Ordinal))
                {
                    expectedPortKey = DialogGraphPortKeys.False;
                    return PortResolutionResult.Valid;
                }

                return PortResolutionResult.MissingPort;
            }

            if (link.fromPortIndex == ConditionNode.TruePortIndex)
            {
                expectedPortKey = DialogGraphPortKeys.True;
                return PortResolutionResult.Valid;
            }

            if (link.fromPortIndex == ConditionNode.FalsePortIndex)
            {
                expectedPortKey = DialogGraphPortKeys.False;
                return PortResolutionResult.Valid;
            }

            return PortResolutionResult.MissingPortIdentity;
        }

        private static PortResolutionResult ValidateChoiceOutputPort(
            ChoiceNode choiceNode,
            GraphLink link,
            out string expectedPortKey,
            out string choiceId)
        {
            expectedPortKey = string.Empty;
            choiceId = null;
            var choices = choiceNode.choices;
            if (choices == null || choices.Count == 0)
            {
                return PortResolutionResult.MissingPort;
            }

            Choice indexedChoice = null;
            if (link.fromPortIndex >= 0 && link.fromPortIndex < choices.Count)
            {
                indexedChoice = choices[link.fromPortIndex];
                choiceId = indexedChoice?.choiceId;
                expectedPortKey = DialogGraphPortKeys.ForChoiceId(choiceId);
            }

            if (!string.IsNullOrWhiteSpace(link.fromPortKey))
            {
                if (!DialogGraphPortKeys.TryGetChoiceId(link.fromPortKey, out var portChoiceId))
                {
                    return PortResolutionResult.MissingPort;
                }

                choiceId = portChoiceId;
                var matchingChoice = choices.FirstOrDefault(choice =>
                    choice != null &&
                    string.Equals(choice.choiceId, portChoiceId, StringComparison.Ordinal));
                if (matchingChoice == null)
                {
                    return PortResolutionResult.MissingPort;
                }

                if (indexedChoice != null &&
                    !string.IsNullOrWhiteSpace(indexedChoice.choiceId) &&
                    !string.Equals(indexedChoice.choiceId, portChoiceId, StringComparison.Ordinal))
                {
                    expectedPortKey = DialogGraphPortKeys.ForChoiceId(indexedChoice.choiceId);
                    return PortResolutionResult.MissingPort;
                }

                expectedPortKey = DialogGraphPortKeys.ForChoiceId(portChoiceId);
                return PortResolutionResult.Valid;
            }

            return indexedChoice != null
                ? PortResolutionResult.Valid
                : PortResolutionResult.MissingPortIdentity;
        }

        private enum PortResolutionResult
        {
            Valid,
            MissingPort,
            MissingPortIdentity
        }

        private static void CheckChoiceLinkConsistency(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var choiceNode in ctx.Graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (choiceNode == null)
                {
                    continue;
                }

                var nodeGuid = choiceNode.GetGuid();
                var choices = choiceNode.choices ?? new List<Choice>();
                var outgoingLinks = ctx.OutgoingByGuid.TryGetValue(nodeGuid, out var links)
                    ? links
                    : new List<GraphLink>();

                for (var i = 0; i < choices.Count; i++)
                {
                    var choice = choices[i];
                    if (choice == null || string.IsNullOrWhiteSpace(choice.nextNodeGUID))
                    {
                        continue;
                    }

                    var hasMatchingLink = outgoingLinks.Any(link =>
                        LinkTargetsChoice(link, choice, i) &&
                        string.Equals(link.toGuid, choice.nextNodeGUID, StringComparison.Ordinal));

                    if (!hasMatchingLink)
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "CHOICE_LINK_MISSING",
                            $"Choice '{choice.answerText}' on ChoiceNode '{nodeGuid}' points to '{choice.nextNodeGUID}', but no matching GraphLink exists for that choice output.",
                            nodeGuid,
                            choiceId: choice.choiceId));
                    }
                }

                foreach (var link in outgoingLinks)
                {
                    if (!TryResolveChoiceForLink(choiceNode, link, out var choice, out var choiceIndex))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "CHOICE_LINK_ORPHAN",
                            $"Link '{FormatLinkId(link)}' uses a choice output that no longer exists on ChoiceNode '{nodeGuid}'.",
                            nodeGuid,
                            link?.LinkGuid));
                        continue;
                    }

                    if (choice == null || string.IsNullOrWhiteSpace(choice.nextNodeGUID))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "CHOICE_LINK_ORPHAN",
                            $"Link '{FormatLinkId(link)}' exists for choice index {choiceIndex}, but that choice has no nextNodeGUID.",
                            nodeGuid,
                            link?.LinkGuid,
                            choice?.choiceId));
                        continue;
                    }

                    if (!string.Equals(choice.nextNodeGUID, link.toGuid, StringComparison.Ordinal))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "CHOICE_LINK_ORPHAN",
                            $"Link '{FormatLinkId(link)}' points to '{link.toGuid}', but choice index {choiceIndex} points to '{choice.nextNodeGUID}'.",
                            nodeGuid,
                            link?.LinkGuid,
                            choice.choiceId));
                    }
                }
            }
        }

        private static bool LinkTargetsChoice(GraphLink link, Choice choice, int choiceIndex)
        {
            if (link == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(link.fromPortKey) &&
                !string.IsNullOrWhiteSpace(choice?.choiceId))
            {
                return string.Equals(
                    link.fromPortKey,
                    DialogGraphPortKeys.ForChoiceId(choice.choiceId),
                    StringComparison.Ordinal);
            }

            return link.fromPortIndex == choiceIndex;
        }

        private static bool TryResolveChoiceForLink(
            ChoiceNode choiceNode,
            GraphLink link,
            out Choice choice,
            out int choiceIndex)
        {
            choice = null;
            choiceIndex = -1;

            var choices = choiceNode?.choices;
            if (link == null || choices == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(link.fromPortKey) &&
                DialogGraphPortKeys.TryGetChoiceId(link.fromPortKey, out var choiceId))
            {
                for (var i = 0; i < choices.Count; i++)
                {
                    if (choices[i] != null &&
                        string.Equals(choices[i].choiceId, choiceId, StringComparison.Ordinal))
                    {
                        choice = choices[i];
                        choiceIndex = i;
                        return true;
                    }
                }

                return false;
            }

            if (link.fromPortIndex < 0 || link.fromPortIndex >= choices.Count)
            {
                return false;
            }

            choiceIndex = link.fromPortIndex;
            choice = choices[choiceIndex];
            return choice != null;
        }

        private static void CheckEndNodeOutgoingLinks(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            if (!ctx.HasEndBoundary) return;

            var outgoingCount = CountBoundaryOutgoingLinks(ctx, ctx.IsEndGuid);
            if (outgoingCount > 0)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "END_NODE_HAS_OUTGOING",
                    $"End node (GUID '{ctx.EffectiveEndGuid}') has {outgoingCount} outgoing link(s). End nodes must not connect to anything.",
                    ctx.EffectiveEndGuid));
            }
        }

        // ── 5. Start node must have no incoming links ─────────────────────────
        private static void CheckStartNodeIncomingLinks(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            if (!ctx.HasStartBoundary) return;

            if (ctx.GuidsWithIncoming.Any(ctx.IsStartGuid))
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Error,
                    "START_NODE_HAS_INCOMING",
                    $"Start node (GUID '{ctx.EffectiveStartGuid}') has incoming link(s). Nothing should point back to Start.",
                    ctx.EffectiveStartGuid));
            }
        }

        // ── 6. Orphan nodes (no incoming AND not the Start node) ─────────────
        private static void CheckStartNodeOutgoingLinks(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            if (!ctx.HasStartBoundary) return;

            var outgoingCount = CountBoundaryOutgoingLinks(ctx, ctx.IsStartGuid);
            if (outgoingCount == 0)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "START_NODE_MISSING_OUTGOING",
                    "Start node has no outgoing link. Runtime cannot resolve an entry node until Start is connected.",
                    ctx.EffectiveStartGuid));
            }
        }

        private static void CheckOrphanNodes(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            // The Start node is allowed to have no incoming links — that is expected.
            foreach (var guid in ctx.AllContentGuids())
            {
                if (ctx.IsStartGuid(guid)) continue;
                if (ctx.GuidsWithIncoming.Contains(guid)) continue;

                // Node has no incoming — it can never be reached from Start.
                // If it also has outgoing links it may be a floating sub-graph (Info).
                // If it has neither incoming nor outgoing it is fully isolated (Warning).
                var hasOutgoing = ctx.OutgoingByGuid.TryGetValue(guid, out var ol) && ol.Count > 0;
                var severity = hasOutgoing
                    ? DialogGraphValidationSeverity.Info
                    : DialogGraphValidationSeverity.Warning;

                issues.Add(new DialogGraphValidationIssue(
                    severity,
                    "ORPHAN_NODE",
                    severity == DialogGraphValidationSeverity.Info
                        ? $"Node '{guid}' has no incoming links — it cannot be reached from Start."
                        : $"Node '{guid}' has no incoming or outgoing links and is fully disconnected.",
                    guid));
            }
        }

        // ── 7. Dead ends (content node with no outgoing, excluding End) ───────
        private static void CheckUnreachableNodes(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            if (!ctx.HasStartBoundary)
            {
                return;
            }

            var reachable = BuildReachableGuidSet(ctx);
            foreach (var guid in ctx.AllContentGuids())
            {
                if (ctx.IsStartGuid(guid) || reachable.Contains(guid))
                {
                    continue;
                }

                if (!ctx.GuidsWithIncoming.Contains(guid))
                {
                    continue;
                }

                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "UNREACHABLE_NODE",
                    $"Node '{guid}' has incoming links but is not reachable from Start.",
                    guid));
            }
        }

        private static void CheckDeadEnds(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var guid in ctx.AllContentGuids())
            {
                var hasOutgoing = ctx.OutgoingByGuid.TryGetValue(guid, out var outgoing) && outgoing.Count > 0;
                if (hasOutgoing) continue;

                // Choice nodes may carry targets via nextNodeGUID rather than GraphLink.
                if (ctx.ChoiceById.TryGetValue(guid, out var cn))
                {
                    var hasChoiceTargets = cn.choices != null &&
                        cn.choices.Any(c => c != null && !string.IsNullOrEmpty(c.nextNodeGUID));
                    if (hasChoiceTargets) continue;
                }

                // Only flag as a warning if the node has no incoming links either
                // (fully isolated nodes are more concerning than tail nodes).
                // Tail nodes (have incoming but no outgoing) are downgraded to Info
                // because they may intentionally end a branch or be a work-in-progress.
                var severity = ctx.GuidsWithIncoming.Contains(guid)
                    ? DialogGraphValidationSeverity.Info
                    : DialogGraphValidationSeverity.Warning;

                issues.Add(new DialogGraphValidationIssue(
                    severity,
                    "DEAD_END",
                    severity == DialogGraphValidationSeverity.Info
                        ? $"Node '{guid}' has no outgoing link (branch tail). Connect it to the End node or another node when ready."
                        : $"Node '{guid}' has no incoming or outgoing links.",
                    guid));
            }
        }

        // ── 8. Dialog node rules ──────────────────────────────────────────────
        private static void CheckDialogNodeRules(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var n in ctx.Graph.nodes ?? Enumerable.Empty<DialogNode>())
            {
                if (n == null) continue;
                var guid = n.GetGuid();

                // 8a. Empty dialog text
                if (string.IsNullOrWhiteSpace(n.questionText))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "EMPTY_DIALOG_TEXT",
                        $"Dialog node '{guid}' (speaker: '{n.speakerName}') has no dialog text.",
                        guid));
                }

                // 8b. Missing speaker/portrait authoring guidance
                if (string.IsNullOrWhiteSpace(n.speakerName))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "MISSING_DIALOG_SPEAKER",
                        $"Dialog node '{guid}' has no speaker name. Assign a speaker so runtime UI and history display cleanly.",
                        guid));
                }
                else if (!HasEffectiveSpeakerPortrait(ctx.Graph, n))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Info,
                        "MISSING_DIALOG_PORTRAIT",
                        $"Dialog node '{guid}' has speaker '{n.speakerName}' but no portrait sprite. Assign a portrait or registered character sprite for a cleaner runtime presentation.",
                        guid));
                }

                // 8c. Multiple outgoing links (dialog nodes must have exactly 0 or 1)
                if (ctx.OutgoingByGuid.TryGetValue(guid, out var outgoing) && outgoing.Count > 1)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "DIALOG_MULTIPLE_OUTGOING",
                        $"Dialog node '{guid}' has {outgoing.Count} outgoing links. Dialog nodes support at most 1 outgoing connection.",
                        guid));
                }
            }
        }

        // ── 9. Choice node rules ──────────────────────────────────────────────
        private static bool HasEffectiveSpeakerPortrait(DialogGraph graph, DialogNode node)
        {
            if (node?.speakerPortrait != null)
            {
                return true;
            }

            var character = ResolveSpeakerCharacter(graph, node?.speakerName);
            return character != null && character.Portrait != null;
        }

        private static DialogCharacterSO ResolveSpeakerCharacter(DialogGraph graph, string speakerName)
        {
            if (string.IsNullOrWhiteSpace(speakerName))
            {
                return null;
            }

            var trimmedSpeaker = speakerName.Trim();
            var scopedCharacter =
                FindCharacterBySpeaker(graph?.participatingCharacters, trimmedSpeaker) ??
                FindCharacterBySpeaker(graph?.sceneContext?.ParticipatingCharacters, trimmedSpeaker);

            return scopedCharacter ?? DialogGraphDefinitionResolver.FindCharacterBySpeaker(trimmedSpeaker);
        }

        private static DialogCharacterSO FindCharacterBySpeaker(IEnumerable<DialogCharacterSO> characters, string speakerName)
        {
            return (characters ?? Enumerable.Empty<DialogCharacterSO>())
                .FirstOrDefault(character => CharacterMatches(character, speakerName));
        }

        private static bool CharacterMatches(DialogCharacterSO character, string speakerName)
        {
            return character != null &&
                   (StringMatches(character.CharacterID, speakerName) ||
                    StringMatches(character.DisplayName, speakerName));
        }

        private static bool StringMatches(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                   !string.IsNullOrWhiteSpace(right) &&
                   string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static void CheckChoiceNodeRules(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var n in ctx.Graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (n == null) continue;
                var guid    = n.GetGuid();
                var choices = n.choices ?? new List<Choice>();

                // 9a. No choices at all
                if (choices.Count == 0)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "CHOICE_NO_CHOICES",
                        $"Choice node '{guid}' has no choices defined.",
                        guid));
                    continue;
                }

                // 9b. Output port count should match choice count.
                //     We derive expected port count from the choice list.
                //     Mismatch is flagged as an error because port index
                //     in GraphLink references would be off.
                var outgoingLinks = ctx.OutgoingByGuid.TryGetValue(guid, out var ol) ? ol : new List<GraphLink>();
                var usedPortIndexes = outgoingLinks.Select(l => l.fromPortIndex).ToHashSet();
                var maxPortIndex    = usedPortIndexes.Count > 0 ? usedPortIndexes.Max() : -1;

                if (maxPortIndex >= choices.Count)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "CHOICE_PORT_OUT_OF_RANGE",
                        $"Choice node '{guid}' has a link from port index {maxPortIndex} but only {choices.Count} choice(s) defined (valid range 0–{choices.Count - 1}).",
                        guid));
                }

                // 9c. Choice count / output-link count mismatch warning
                //     (fewer links than choices means some branches are unconnected).
                var connectedPorts = usedPortIndexes.Count;
                if (connectedPorts < choices.Count)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "CHOICE_MISSING_OUTPUT",
                        $"Choice node '{guid}' has {choices.Count} choice(s) but only {connectedPorts} output port(s) connected. {choices.Count - connectedPorts} branch(es) lead nowhere.",
                        guid));
                }

                // 9d. Empty choice answer text
                for (var i = 0; i < choices.Count; i++)
                {
                    var c = choices[i];
                    if (c == null || string.IsNullOrWhiteSpace(c.answerText))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "CHOICE_EMPTY_ANSWER_TEXT",
                            $"Choice node '{guid}', choice index {i} has empty answer text.",
                            guid));
                    }
                }
            }
        }

        // ── 10. Action node rules ─────────────────────────────────────────────
        private static void CheckActionNodeRules(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var n in ctx.Graph.actionNodes ?? Enumerable.Empty<ActionNode>())
            {
                if (n == null) continue;
                var guid = n.GetGuid();

                if (string.IsNullOrWhiteSpace(n.actionId))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "EMPTY_ACTION_ID",
                        $"Action node '{guid}' has no actionId set.",
                        guid));
                }

                // 10a. Multiple outgoing links (same rule as Dialog nodes)
                if (ctx.OutgoingByGuid.TryGetValue(guid, out var outgoing) && outgoing.Count > 1)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "ACTION_MULTIPLE_OUTGOING",
                        $"Action node '{guid}' has {outgoing.Count} outgoing links. Action nodes support at most 1 outgoing connection.",
                        guid));
                }
            }
        }

        // ── 11. Invalid speaker IDs ───────────────────────────────────────────
        private static void CheckConditionNodeRules(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var n in ctx.Graph.conditionNodes ?? Enumerable.Empty<ConditionNode>())
            {
                if (n == null) continue;
                var guid = n.GetGuid();
                var outgoing = ctx.OutgoingByGuid.TryGetValue(guid, out var links)
                    ? links
                    : new List<GraphLink>();

                if (string.IsNullOrWhiteSpace(n.variableName))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "CONDITION_EMPTY_VARIABLE",
                        $"Condition node '{guid}' has no variable name.",
                        guid));
                }

                var invalidPort = outgoing.FirstOrDefault(link => !IsConditionPortLink(link, true) && !IsConditionPortLink(link, false));
                if (invalidPort != null)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "CONDITION_PORT_OUT_OF_RANGE",
                        $"Condition node '{guid}' has a link from invalid output port '{GetDisplayPortIdentity(invalidPort)}'. Valid ports are '{DialogGraphPortKeys.True}' and '{DialogGraphPortKeys.False}'.",
                        guid,
                        invalidPort.LinkGuid));
                }

                var trueLink = outgoing.FirstOrDefault(link => IsConditionPortLink(link, true));
                var falseLink = outgoing.FirstOrDefault(link => IsConditionPortLink(link, false));

                if (trueLink == null)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "CONDITION_MISSING_TRUE_OUTPUT",
                        $"Condition node '{guid}' has no True branch target. If the condition succeeds at runtime, the dialogue will end.",
                        guid));
                }

                if (falseLink == null)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "CONDITION_MISSING_FALSE_OUTPUT",
                        $"Condition node '{guid}' has no False branch target. If the condition fails at runtime, the dialogue will end.",
                        guid));
                }

                if (trueLink != null && string.Equals(trueLink.toGuid, guid, StringComparison.Ordinal))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "CONDITION_TRUE_SELF_LOOP",
                        $"Condition node '{guid}' True branch points back to itself. Runtime cycle guards will end the dialogue if this branch is selected.",
                        guid,
                        trueLink.LinkGuid));
                }

                if (falseLink != null && string.Equals(falseLink.toGuid, guid, StringComparison.Ordinal))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "CONDITION_FALSE_SELF_LOOP",
                        $"Condition node '{guid}' False branch points back to itself. Runtime cycle guards will end the dialogue if this branch is selected.",
                        guid,
                        falseLink.LinkGuid));
                }

                if (trueLink != null &&
                    falseLink != null &&
                    !string.IsNullOrWhiteSpace(trueLink.toGuid) &&
                    string.Equals(trueLink.toGuid, falseLink.toGuid, StringComparison.Ordinal))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Info,
                        "CONDITION_BRANCHES_SHARE_TARGET",
                        $"Condition node '{guid}' True and False branches both target '{trueLink.toGuid}'. This is allowed, but confirm it is intentional.",
                        guid));
                }

                if (!IsOperatorCompatible(n.valueType, n.conditionOperator))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "CONDITION_OPERATOR_TYPE_MISMATCH",
                        $"Condition node '{guid}' uses operator '{n.conditionOperator}' with type '{n.valueType}', which will evaluate false unless the fallback path is used.",
                        guid));
                }

                if (!IsComparisonValueValid(n.valueType, n.conditionOperator, n.comparisonValue))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "CONDITION_VALUE_PARSE_ERROR",
                        $"Condition node '{guid}' comparison value '{n.comparisonValue}' cannot be parsed as {n.valueType}.",
                        guid));
                }
            }
        }

        private static void CheckVariableMutationNodeRules(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var n in ctx.Graph.variableMutationNodes ?? Enumerable.Empty<VariableMutationNode>())
            {
                if (n == null) continue;
                var guid = n.GetGuid();

                if (string.IsNullOrWhiteSpace(n.variableName))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "VARIABLE_EMPTY_NAME",
                        $"Variable mutation node '{guid}' has no variable name.",
                        guid));
                }

                if (ctx.OutgoingByGuid.TryGetValue(guid, out var outgoing) && outgoing.Count > 1)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "VARIABLE_MULTIPLE_OUTGOING",
                        $"Variable mutation node '{guid}' has {outgoing.Count} outgoing links. Variable mutation nodes support at most 1 outgoing connection.",
                        guid));
                }

                if (!IsVariableOperationCompatible(n.valueType, n.operation))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "VARIABLE_OPERATION_TYPE_MISMATCH",
                        $"Variable mutation node '{guid}' uses operation '{n.operation}' with type '{n.valueType}'.",
                        guid));
                }

                if (!IsMutationValueValid(n.valueType, n.operation, n.value))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "VARIABLE_VALUE_PARSE_ERROR",
                        $"Variable mutation node '{guid}' value '{n.value}' cannot be parsed as {n.valueType}.",
                        guid));
                }
            }
        }

        private static void CheckGraphJumpNodeRules(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var n in ctx.Graph.graphJumpNodes ?? Enumerable.Empty<GraphJumpNode>())
            {
                if (n == null) continue;

                var guid = n.GetGuid();
                var reference = n.targetGraph;
                if (reference == null || !reference.HasReference)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "GRAPH_JUMP_TARGET_MISSING",
                        $"Graph Jump node '{guid}' has no target graph assigned. Runtime will end this branch if it is reached.",
                        guid));
                    continue;
                }

                var targetsSelf =
                    (reference.graphAsset != null && ReferenceEquals(reference.graphAsset, ctx.Graph)) ||
                    (!string.IsNullOrWhiteSpace(reference.graphGuid) &&
                     !string.IsNullOrWhiteSpace(ctx.Graph.GraphGuid) &&
                     string.Equals(reference.graphGuid, ctx.Graph.GraphGuid, StringComparison.Ordinal));

                if (targetsSelf)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "GRAPH_JUMP_SELF_TARGET",
                        $"Graph Jump node '{guid}' targets the same graph asset. Runtime cycle guards will block recursive re-entry.",
                        guid));
                }
            }
        }

        private static void CheckHiddenFlowCycles(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            var hiddenGuids = new HashSet<string>(
                ctx.ActionGuids
                    .Concat(ctx.ConditionGuids)
                    .Concat(ctx.VariableMutationGuids)
                    .Concat(ctx.GraphJumpGuids),
                StringComparer.Ordinal);

            var reportedCycles = new HashSet<string>(StringComparer.Ordinal);

            foreach (var startGuid in hiddenGuids)
            {
                var path = new List<string>();
                var pathSet = new HashSet<string>(StringComparer.Ordinal);
                VisitHiddenFlow(startGuid, startGuid);

                void VisitHiddenFlow(string cursor, string origin)
                {
                    if (string.IsNullOrWhiteSpace(cursor) || !hiddenGuids.Contains(cursor))
                    {
                        return;
                    }

                    if (!pathSet.Add(cursor))
                    {
                        if (string.Equals(cursor, origin, StringComparison.Ordinal))
                        {
                            ReportCycle(origin, path);
                        }

                        return;
                    }

                    path.Add(cursor);
                    foreach (var next in GetHiddenFlowTargets(ctx, cursor))
                    {
                        if (string.Equals(next, origin, StringComparison.Ordinal))
                        {
                            ReportCycle(origin, path.Concat(new[] { origin }));
                            continue;
                        }

                        VisitHiddenFlow(next, origin);
                    }

                    path.RemoveAt(path.Count - 1);
                    pathSet.Remove(cursor);
                }
            }

            void ReportCycle(string origin, IEnumerable<string> cyclePath)
            {
                var normalizedPath = cyclePath
                    .Where(guid => !string.IsNullOrWhiteSpace(guid))
                    .ToArray();

                var cycleKey = string.Join(">", normalizedPath.Distinct().OrderBy(guid => guid, StringComparer.Ordinal));
                if (normalizedPath.Length == 0 || !reportedCycles.Add(cycleKey))
                {
                    return;
                }

                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "HIDDEN_FLOW_CYCLE",
                    $"Hidden-flow cycle detected: {FormatHiddenFlowPath(ctx, normalizedPath)}. Runtime will end gracefully if this path is reached.",
                    origin));
            }
        }

        private static IEnumerable<string> GetHiddenFlowTargets(ValidationContext ctx, string guid)
        {
            if (!ctx.OutgoingByGuid.TryGetValue(guid, out var outgoing) || outgoing == null)
            {
                yield break;
            }

            if (ctx.ConditionGuids.Contains(guid))
            {
                foreach (var link in outgoing.Where(link => IsConditionPortLink(link, true) || IsConditionPortLink(link, false)))
                {
                    if (!string.IsNullOrWhiteSpace(link?.toGuid))
                    {
                        yield return link.toGuid;
                    }
                }

                yield break;
            }

            foreach (var link in outgoing)
            {
                if (!string.IsNullOrWhiteSpace(link?.toGuid))
                {
                    yield return link.toGuid;
                }
            }
        }

        private static string FormatHiddenFlowPath(ValidationContext ctx, IEnumerable<string> path)
        {
            return string.Join(" -> ", path.Select(guid => FormatHiddenFlowNode(ctx, guid)));
        }

        private static string FormatHiddenFlowNode(ValidationContext ctx, string guid)
        {
            if (ctx.ConditionById.TryGetValue(guid, out var condition))
            {
                return $"Condition '{GetNodeDisplayName(condition, condition.variableName)}'";
            }

            if (ctx.VariableMutationById.TryGetValue(guid, out var variableMutation))
            {
                return $"Variable Mutation '{GetNodeDisplayName(variableMutation, variableMutation.variableName)}'";
            }

            if (ctx.ActionById.TryGetValue(guid, out var action))
            {
                return $"Action '{GetNodeDisplayName(action, action.actionId)}'";
            }

            if (ctx.GraphJumpById.TryGetValue(guid, out var graphJump))
            {
                return $"Graph Jump '{GetNodeDisplayName(graphJump, guid)}'";
            }

            return guid;
        }

        private static string GetNodeDisplayName(BaseNode node, string fallback)
        {
            if (node != null && !string.IsNullOrWhiteSpace(node.name))
            {
                return node.name;
            }

            return !string.IsNullOrWhiteSpace(fallback)
                ? fallback.Trim()
                : node?.GetGuid() ?? "<unknown>";
        }

        private static void CheckInvalidSpeakerIds(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var n in ctx.Graph.nodes ?? Enumerable.Empty<DialogNode>())
            {
                if (n == null) continue;
                var speaker = n.speakerName;

                // Blank speaker is a warning (covered by CheckDialogNodeRules if needed),
                // not an "invalid ID" error.
                if (string.IsNullOrWhiteSpace(speaker)) continue;

                if (!ctx.KnownSpeakers.Contains(speaker.Trim()))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "UNKNOWN_SPEAKER_ID",
                        $"Dialog node '{n.GetGuid()}' uses speaker '{speaker}' which is not in the known character list.",
                        n.GetGuid()));
                }
            }
        }

        // ── 12. Invalid action IDs ────────────────────────────────────────────
        private static void CheckInvalidActionIds(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var n in ctx.Graph.actionNodes ?? Enumerable.Empty<ActionNode>())
            {
                if (n == null) continue;
                var actionId = n.actionId;

                if (string.IsNullOrWhiteSpace(actionId))
                {
                    continue;
                }

                if (!ctx.KnownActions.Contains(actionId.Trim()))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "UNKNOWN_ACTION_ID",
                        $"Action node '{n.GetGuid()}' uses actionId '{actionId}' which is not in the known action list.",
                        n.GetGuid()));
                }
            }
        }

        // ── 13. Node overlap warnings ─────────────────────────────────────────
        private static void CheckInvalidVariableReferences(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            foreach (var n in ctx.Graph.conditionNodes ?? Enumerable.Empty<ConditionNode>())
            {
                if (n == null || string.IsNullOrWhiteSpace(n.variableName))
                {
                    continue;
                }

                CheckKnownVariableReference(
                    ctx,
                    n.variableName,
                    n.valueType,
                    n.GetGuid(),
                    "Condition node",
                    issues);
            }

            foreach (var n in ctx.Graph.variableMutationNodes ?? Enumerable.Empty<VariableMutationNode>())
            {
                if (n == null || string.IsNullOrWhiteSpace(n.variableName))
                {
                    continue;
                }

                CheckKnownVariableReference(
                    ctx,
                    n.variableName,
                    n.valueType,
                    n.GetGuid(),
                    "Variable mutation node",
                    issues);
            }

            foreach (var n in ctx.Graph.nodes ?? Enumerable.Empty<DialogNode>())
            {
                if (n == null)
                {
                    continue;
                }

                CheckKnownVariableTokens(ctx, n.questionText, n.GetGuid(), null, "Dialog node", issues);
            }

            foreach (var n in ctx.Graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (n?.choices == null)
                {
                    continue;
                }

                foreach (var choice in n.choices)
                {
                    if (choice == null)
                    {
                        continue;
                    }

                    CheckKnownVariableTokens(ctx, choice.answerText, n.GetGuid(), choice.choiceId, "Choice text", issues);
                }
            }
        }

        private static void CheckNodeOverlap(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            // Collect (guid, position) for all nodes with a valid position.
            var positions = new List<(string guid, Vector2 pos)>();

            CollectPositions(ctx.Graph.nodes,       n => n.GetGuid(), n => n.GetPosition(), positions);
            CollectPositions(ctx.Graph.choiceNodes,  n => n.GetGuid(), n => n.GetPosition(), positions);
            CollectPositions(ctx.Graph.actionNodes,  n => n.GetGuid(), n => n.GetPosition(), positions);
            CollectPositions(ctx.Graph.conditionNodes, n => n.GetGuid(), n => n.GetPosition(), positions);
            CollectPositions(ctx.Graph.variableMutationNodes, n => n.GetGuid(), n => n.GetPosition(), positions);
            CollectPositions(ctx.Graph.graphJumpNodes, n => n.GetGuid(), n => n.GetPosition(), positions);
            CollectPositions(ctx.Graph.outcomeNodes, n => n.GetGuid(), n => n.GetPosition(), positions);

            // Start / End GUIDs don't have a BaseNode we can query directly here,
            // so we skip them. Their positions are stored on the DialogGraph itself
            // but belong to the editor layout, not logic — overlaps there are cosmetic.

            var reported = new HashSet<(string, string)>();

            for (var i = 0; i < positions.Count; i++)
            {
                for (var j = i + 1; j < positions.Count; j++)
                {
                    var (guidA, posA) = positions[i];
                    var (guidB, posB) = positions[j];

                    if (Vector2.Distance(posA, posB) < OverlapThreshold)
                    {
                        // Report each unordered pair once.
                        var key = string.CompareOrdinal(guidA, guidB) < 0
                            ? (guidA, guidB)
                            : (guidB, guidA);

                        if (reported.Add(key))
                        {
                            issues.Add(new DialogGraphValidationIssue(
                                DialogGraphValidationSeverity.Info,
                                "NODE_OVERLAP",
                                $"Nodes '{guidA}' and '{guidB}' are visually overlapping (distance {Vector2.Distance(posA, posB):F1} < {OverlapThreshold}). Consider running Auto-Layout.",
                                guidA));
                        }
                    }
                }
            }
        }

        // ── Outcome node rules ──────────────────────────────────────────────
        private static void CheckOutcomeNodeRules(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            if (ctx.Graph.outcomeNodes == null) return;

            var outcomeIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var n in ctx.Graph.outcomeNodes)
            {
                if (n == null) continue;

                var guid = n.GetGuid();

                // Outcome ID must be non-empty
                if (!n.HasOutcomeId)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "OUTCOME_MISSING_ID",
                        $"Outcome node '{guid}' has no outcomeId.",
                        guid));
                }
                else if (!outcomeIds.Add(n.outcomeId))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "OUTCOME_DUPLICATE_ID",
                        $"Outcome node '{guid}' has duplicate outcomeId '{n.outcomeId}'.",
                        guid));
                }

                // Outcome must have exactly one outgoing link
                var outgoing = ctx.Graph.links?
                    .Where(link => link != null && string.Equals(link.fromGuid, guid, StringComparison.Ordinal))
                    .ToList() ?? new List<GraphLink>();

                if (outgoing.Count == 0)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "OUTCOME_NO_OUTPUT",
                        $"Outcome node '{guid}' has no outgoing link. It must be connected to End.",
                        guid));
                }
                else if (outgoing.Count > 1)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Error,
                        "OUTCOME_MULTIPLE_OUTPUTS",
                        $"Outcome node '{guid}' has {outgoing.Count} outgoing links. Only one is allowed.",
                        guid));
                }
                else
                {
                    // Single output — must target End
                    var target = outgoing[0].toGuid;
                    if (!DialogGraphFlowUtility.IsEndGuid(ctx.Graph, target))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Error,
                            "OUTCOME_NOT_END",
                            $"Outcome node '{guid}' links to '{target}' instead of End.",
                            guid));
                    }
                }

                // Validate default output port key
                foreach (var link in outgoing)
                {
                    if (link != null && !string.Equals(link.fromPortKey, DialogGraphPortKeys.Default, StringComparison.Ordinal))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "OUTCOME_INVALID_PORT_KEY",
                            $"Outcome node '{guid}' has non-default port key '{link.fromPortKey}'.",
                            guid));
                    }
                }
            }
        }

        private static void CollectPositions<T>(
            IEnumerable<T> nodes,
            System.Func<T, string>  getGuid,
            System.Func<T, Vector2> getPos,
            List<(string, Vector2)> output)
            where T : class
        {
            foreach (var n in nodes ?? Enumerable.Empty<T>())
            {
                if (n == null) continue;
                var g = getGuid(n);
                if (!string.IsNullOrEmpty(g))
                    output.Add((g, getPos(n)));
            }
        }

        private static int CountBoundaryOutgoingLinks(
            ValidationContext ctx,
            System.Func<string, bool> boundaryMatch)
        {
            var count = 0;

            foreach (var kv in ctx.OutgoingByGuid)
            {
                if (!boundaryMatch(kv.Key))
                    continue;

                count += kv.Value?.Count ?? 0;
            }

            return count;
        }

        private static HashSet<string> BuildReachableGuidSet(ValidationContext ctx)
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>();

            EnqueueGuid(ctx.EffectiveStartGuid);
            EnqueueGuid(StartAlias);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();

                if (ctx.OutgoingByGuid.TryGetValue(current, out var outgoing))
                {
                    foreach (var link in outgoing)
                    {
                        EnqueueGuid(link?.toGuid);
                    }
                }

                if (ctx.ChoiceById.TryGetValue(current, out var choiceNode) && choiceNode?.choices != null)
                {
                    foreach (var choice in choiceNode.choices)
                    {
                        EnqueueGuid(choice?.nextNodeGUID);
                    }
                }
            }

            return reachable;

            void EnqueueGuid(string guid)
            {
                if (string.IsNullOrWhiteSpace(guid) || !reachable.Add(guid))
                {
                    return;
                }

                pending.Enqueue(guid);
            }
        }

        private static void CheckKnownVariableReference(
            ValidationContext ctx,
            string variableName,
            DialogueVariableValueType expectedType,
            string nodeGuid,
            string nodeLabel,
            List<DialogGraphValidationIssue> issues)
        {
            var key = variableName.Trim();
            if (!ctx.KnownVariables.TryGetValue(key, out var registeredType))
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "UNKNOWN_VARIABLE_KEY",
                    $"{nodeLabel} '{nodeGuid}' references variable '{variableName}' which is not in the known variable list.",
                    nodeGuid));
                return;
            }

            if (registeredType != expectedType)
            {
                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "VARIABLE_TYPE_MISMATCH",
                    $"{nodeLabel} '{nodeGuid}' references variable '{variableName}' as {expectedType}, but the registered definition is {registeredType}.",
                    nodeGuid));
            }
        }

        private static void CheckKnownVariableTokens(
            ValidationContext ctx,
            string text,
            string nodeGuid,
            string choiceId,
            string sourceLabel,
            List<DialogGraphValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (Match match in VariableTokenRegex.Matches(text))
            {
                var key = match.Groups["key"].Value?.Trim();
                if (string.IsNullOrWhiteSpace(key) || ContainsKnownVariableToken(ctx, key))
                {
                    continue;
                }

                issues.Add(new DialogGraphValidationIssue(
                    DialogGraphValidationSeverity.Warning,
                    "UNKNOWN_VARIABLE_TOKEN",
                    $"{sourceLabel} on node '{nodeGuid}' references variable token '{key}' which is not in the known variable list.",
                    nodeGuid,
                    choiceId: choiceId));
            }
        }

        private static bool ContainsKnownVariableToken(ValidationContext ctx, string key)
        {
            if (ctx.KnownVariables.ContainsKey(key))
            {
                return true;
            }

            return ctx.KnownVariables.Keys.Any(known =>
                string.Equals(known, key, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsConditionPortLink(GraphLink link, bool truePort)
        {
            if (link == null)
            {
                return false;
            }

            var expectedKey = truePort ? DialogGraphPortKeys.True : DialogGraphPortKeys.False;
            if (!string.IsNullOrWhiteSpace(link.fromPortKey))
            {
                return string.Equals(link.fromPortKey, expectedKey, StringComparison.Ordinal);
            }

            var expectedIndex = truePort ? ConditionNode.TruePortIndex : ConditionNode.FalsePortIndex;
            return link.fromPortIndex == expectedIndex;
        }

        private static string FormatLinkId(GraphLink link)
        {
            if (link == null)
            {
                return "<null>";
            }

            return string.IsNullOrWhiteSpace(link.LinkGuid)
                ? $"{link.fromGuid}->{link.toGuid}"
                : link.LinkGuid;
        }

        private static string GetDisplayPortIdentity(GraphLink link)
        {
            if (link == null)
            {
                return "<null>";
            }

            return !string.IsNullOrWhiteSpace(link.fromPortKey)
                ? link.fromPortKey
                : $"legacy index {link.fromPortIndex}";
        }

        private static bool IsOperatorCompatible(DialogueVariableValueType type, ConditionOperator op)
        {
            return type switch
            {
                DialogueVariableValueType.Boolean => op == ConditionOperator.IsTrue ||
                                                     op == ConditionOperator.IsFalse ||
                                                     op == ConditionOperator.Equals ||
                                                     op == ConditionOperator.NotEquals,
                DialogueVariableValueType.Integer => op == ConditionOperator.Equals ||
                                                     op == ConditionOperator.NotEquals ||
                                                     op == ConditionOperator.Greater ||
                                                     op == ConditionOperator.Less,
                DialogueVariableValueType.Float => op == ConditionOperator.Equals ||
                                                   op == ConditionOperator.NotEquals ||
                                                   op == ConditionOperator.Greater ||
                                                   op == ConditionOperator.Less,
                DialogueVariableValueType.String => op == ConditionOperator.Equals ||
                                                    op == ConditionOperator.NotEquals ||
                                                    op == ConditionOperator.Contains,
                _ => false
            };
        }

        private static bool IsComparisonValueValid(DialogueVariableValueType type, ConditionOperator op, string value)
        {
            if (op == ConditionOperator.IsTrue || op == ConditionOperator.IsFalse)
            {
                return true;
            }

            return TryParseValue(type, value);
        }

        private static bool IsVariableOperationCompatible(DialogueVariableValueType type, VariableMutationOperation operation)
        {
            return operation switch
            {
                VariableMutationOperation.Toggle => type == DialogueVariableValueType.Boolean,
                VariableMutationOperation.ClearString => type == DialogueVariableValueType.String,
                VariableMutationOperation.Add => type == DialogueVariableValueType.Integer || type == DialogueVariableValueType.Float,
                VariableMutationOperation.Subtract => type == DialogueVariableValueType.Integer || type == DialogueVariableValueType.Float,
                VariableMutationOperation.Set => true,
                _ => false
            };
        }

        private static bool IsMutationValueValid(DialogueVariableValueType type, VariableMutationOperation operation, string value)
        {
            if (operation == VariableMutationOperation.Toggle || operation == VariableMutationOperation.ClearString)
            {
                return true;
            }

            if (operation == VariableMutationOperation.Add || operation == VariableMutationOperation.Subtract)
            {
                return type == DialogueVariableValueType.Integer || type == DialogueVariableValueType.Float
                    ? TryParseValue(type, value)
                    : true;
            }

            return TryParseValue(type, value);
        }

        private static bool TryParseValue(DialogueVariableValueType type, string value)
        {
            switch (type)
            {
                case DialogueVariableValueType.Boolean:
                    return TryParseBool(value);
                case DialogueVariableValueType.Integer:
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                case DialogueVariableValueType.Float:
                    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                case DialogueVariableValueType.String:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseBool(string value)
        {
            return bool.TryParse(value, out _) ||
                   string.Equals(value, "1", StringComparison.Ordinal) ||
                   string.Equals(value, "0", StringComparison.Ordinal);
        }

        // ── 12. Locale key rules ──────────────────────────────────────────────
        private static readonly Regex LocaleKeyPattern = new Regex(
            @"^[a-z0-9][a-z0-9_.\-]*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static void CheckLocaleKeys(ValidationContext ctx, List<DialogGraphValidationIssue> issues)
        {
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 12a. DialogNode — questionText locale key
            foreach (var n in ctx.Graph.nodes ?? Enumerable.Empty<DialogNode>())
            {
                if (n == null) continue;
                var guid = n.GetGuid();

                if (!n.HasQuestionLocaleKey)
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "MISSING_LOCALE_KEY",
                        $"Dialog node '{guid}' has no questionTextLocaleKey. Assign a stable key so the text can be localized.",
                        guid));
                }
                else
                {
                    var key = n.questionTextLocaleKey.Trim();
                    if (!LocaleKeyPattern.IsMatch(key))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Error,
                            "INVALID_LOCALE_KEY_FORMAT",
                            $"Dialog node '{guid}' has an invalid questionTextLocaleKey '{key}'. Keys must match [a-z0-9_.-]+.",
                            guid));
                    }
                    else if (!seenKeys.Add(key))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Error,
                            "DUPLICATE_LOCALE_KEY",
                            $"Dialog node '{guid}' uses questionTextLocaleKey '{key}' which is already used by another node in this graph.",
                            guid));
                    }
                }
            }

            // 12b. ChoiceNode — prompt and choice answer locale keys
            foreach (var n in ctx.Graph.choiceNodes ?? Enumerable.Empty<ChoiceNode>())
            {
                if (n == null) continue;
                var guid = n.GetGuid();

                if (string.IsNullOrWhiteSpace(n.textLocaleKey) && !string.IsNullOrWhiteSpace(n.text))
                {
                    issues.Add(new DialogGraphValidationIssue(
                        DialogGraphValidationSeverity.Warning,
                        "MISSING_LOCALE_KEY",
                        $"Choice node '{guid}' has prompt text but no textLocaleKey.",
                        guid));
                }
                else if (!string.IsNullOrWhiteSpace(n.textLocaleKey))
                {
                    var key = n.textLocaleKey.Trim();
                    if (!LocaleKeyPattern.IsMatch(key))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Error,
                            "INVALID_LOCALE_KEY_FORMAT",
                            $"Choice node '{guid}' has an invalid textLocaleKey '{key}'.",
                            guid));
                    }
                    else if (!seenKeys.Add(key))
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Error,
                            "DUPLICATE_LOCALE_KEY",
                            $"Choice node '{guid}' uses textLocaleKey '{key}' which is already used by another node.",
                            guid));
                    }
                }

                if (n.choices == null) continue;
                for (var i = 0; i < n.choices.Count; i++)
                {
                    var c = n.choices[i];
                    if (c == null) continue;

                    if (!c.HasAnswerLocaleKey)
                    {
                        issues.Add(new DialogGraphValidationIssue(
                            DialogGraphValidationSeverity.Warning,
                            "MISSING_LOCALE_KEY",
                            $"Choice node '{guid}', choice index {i} ('{c.answerText}') has no answerTextLocaleKey.",
                            guid,
                            choiceId: c.choiceId));
                    }
                    else
                    {
                        var key = c.answerTextLocaleKey.Trim();
                        if (!LocaleKeyPattern.IsMatch(key))
                        {
                            issues.Add(new DialogGraphValidationIssue(
                                DialogGraphValidationSeverity.Error,
                                "INVALID_LOCALE_KEY_FORMAT",
                                $"Choice node '{guid}', choice index {i} has invalid answerTextLocaleKey '{key}'.",
                                guid,
                                choiceId: c.choiceId));
                        }
                        else if (!seenKeys.Add(key))
                        {
                            issues.Add(new DialogGraphValidationIssue(
                                DialogGraphValidationSeverity.Error,
                                "DUPLICATE_LOCALE_KEY",
                                $"Choice node '{guid}', choice index {i} uses answerTextLocaleKey '{key}' which is already used by another node.",
                                guid,
                                choiceId: c.choiceId));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Backward-compatible static entry point for dialog graph validation.
    /// </summary>
    public static class DialogGraphValidator
    {
        private static readonly IDialogGraphValidator DefaultValidator = new DefaultDialogGraphValidator();

        /// <summary>
        /// Validates <paramref name="graph"/> with no known-speaker or known-action
        /// constraints (those checks are skipped if the sets are empty).
        /// </summary>
        public static DialogGraphValidationResult Validate(DialogGraph graph)
        {
            return DefaultValidator.Validate(graph);
        }

        /// <summary>
        /// Validates <paramref name="graph"/> and optionally checks speaker IDs
        /// and action IDs against caller-supplied authoritative sets.
        /// </summary>
        public static DialogGraphValidationResult Validate(
            DialogGraph graph,
            IEnumerable<string> knownSpeakerIds,
            IEnumerable<string> knownActionIds)
        {
            return DefaultValidator.Validate(graph, knownSpeakerIds, knownActionIds);
        }

        /// <summary>
        /// Validates <paramref name="graph"/> and optionally checks speaker,
        /// action, and variable references against caller-supplied authoritative sets.
        /// </summary>
        public static DialogGraphValidationResult Validate(
            DialogGraph graph,
            IEnumerable<string> knownSpeakerIds,
            IEnumerable<string> knownActionIds,
            IEnumerable<DialogVariableSO> knownVariables)
        {
            return DefaultValidator.Validate(graph, knownSpeakerIds, knownActionIds, knownVariables);
        }

        /// <summary>
        /// Validates <paramref name="graph"/> with all optional constraints and
        /// locale key checks when <paramref name="hasLocaleSource"/> is <c>true</c>.
        /// </summary>
        public static DialogGraphValidationResult Validate(
            DialogGraph graph,
            IEnumerable<string> knownSpeakerIds,
            IEnumerable<string> knownActionIds,
            IEnumerable<DialogVariableSO> knownVariables,
            bool hasLocaleSource)
        {
            return DefaultValidator.Validate(
                graph, knownSpeakerIds, knownActionIds, knownVariables, hasLocaleSource);
        }
    }
}
