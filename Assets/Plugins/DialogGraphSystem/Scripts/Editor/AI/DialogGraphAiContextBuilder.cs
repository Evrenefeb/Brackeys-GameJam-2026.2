using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.EditorTools.Windows;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DialogSystem.EditorTools.AI
{
    public enum AiContextDepth
    {
        Minimal = 0,
        Local = 1,
        Path = 2,
        FullGraph = 3,
    }

    [Serializable]
    public sealed class DialogGraphAiContext
    {
        public AiContextDepth contextDepth;
        public string graphName;
        public string graphSummary;
        public string sceneContextAssetName;
        public DialogEnvironmentAiContext environment;
        public string sceneGoal;
        public string sceneTone;
        public string extraRules;
        public string selectedNodeType;
        public string selectedNodeText;
        public string fullTranscriptText;
        public DialogGraphAiSidebarContext sidebar;
        public DialogGraphAiNodeContext selectedNode;
        public DialogGraphAiNodeContext anchorNode;
        public List<string> availableCharacters;
        public List<string> availableActions;
        public List<DialogActionAiContext> actionContexts;
        public List<DialogCharacterAiContext> speakerExamples;
        public List<DialogGraphAiConnectionContext> nearbyConnectedNodes;
        public List<DialogGraphAiConnectionContext> localNodeNeighborhood;
        public List<DialogGraphAiNodeContext> orderedPathNodes;
        public List<DialogGraphAiNodeContext> orderedGraphNodes;

        // ── Localization fields (populated by AiLocalizationCommandHandler) ──

        /// <summary>BCP 47 target locale code, e.g. "fr-FR". Used by BatchTranslate.</summary>
        public string targetLocale;

        /// <summary>
        /// Human-readable target language name, e.g. "French".
        /// Populated by Phase 35 translate command handlers.
        /// </summary>
        public string targetLanguage;

        /// <summary>
        /// Scene or quest narrative goal extracted from the user instruction.
        /// Used by GenerateNarrativeScene and ScaffoldQuestDialogue handlers.
        /// </summary>
        public string narrativeGoal;

        /// <summary>
        /// Source strings collected from the graph for translation.
        /// Populated by the handler before calling AiPromptBuilder.Build.
        /// Not serializable as a tuple list — handler injects directly into
        /// the prompt builder rather than storing here.
        /// </summary>
        [NonSerialized]
        public List<(string key, string sourceText)> sourceStringsForTranslation;
    }

    [Serializable]
    public sealed class DialogGraphAiSidebarContext
    {
        public string userInstruction;
        public string tone;
        public string instructionPreset;
    }

    [Serializable]
    public sealed class DialogGraphAiNodeContext
    {
        public string guid;
        public string nodeType;
        public string title;
        public string text;
        public string speakerName;
        public List<string> choices;
        public string actionId;
        public string payloadJson;
        public bool waitForCompletion;
        public float waitSeconds;

        /// <summary>
        /// Stable locale key stamped on this node, if it has been keyed.
        /// Populated when the context is built for a translate command.
        /// </summary>
        public string localeKey;
    }

    [Serializable]
    public sealed class DialogGraphAiConnectionContext
    {
        public string relationship;
        public int fromPortIndex;
        public DialogGraphAiNodeContext node;
    }

    [Serializable]
    public sealed class DialogCharacterAiContext
    {
        public string characterId;
        public string speakerName;
        public string shortDescription;
        public List<string> personalityTraits;
        public string speechStyle;
        public List<string> exampleLines;
    }

    [Serializable]
    public sealed class DialogActionAiContext
    {
        public string actionId;
        public string displayName;
        public string description;
        public string defaultPayloadJson;
        public bool waitForCompletion;
        public float defaultDelay;
    }

    [Serializable]
    public sealed class DialogEnvironmentAiContext
    {
        public string environmentId;
        public string displayName;
        public string description;
        public string atmosphere;
        public string canonRules;
        public string defaultTone;
    }

    public static class DialogGraphAiContextBuilder
    {
        private const int MaxNearbyNodeCount = 6;

        #region ---------------- Public API ----------------

        public static DialogGraphAiContext Build(IDialogGraphOwner owner)
        {
            return Build(owner, AiContextDepth.Local);
        }

        public static DialogGraphAiContext Build(IDialogGraphOwner owner, AiContextDepth depth)
        {
            return Build(owner, depth, null);
        }

        public static DialogGraphAiContext Build(
            IDialogGraphOwner owner,
            AiContextDepth depth,
            string anchorNodeGuid)
        {
            if (owner == null)
            {
                return CreateEmptyContext(depth);
            }

            var graphView = owner.GetGraphView();
            var context = Build(
                graphView,
                owner.GetCurrentGraphName(),
                owner.GetAiSidebarPrompt(),
                owner.GetAiSidebarTone(),
                owner.GetAiSidebarInstructionPreset(),
                owner.CollectSpeakersFromNodes(),
                owner.CollectActionNodes().Select(node => node.actionId),
                depth,
                anchorNodeGuid);

            ApplyGraphSceneContext(context, owner.LoadCurrentGraphAsset());
            return AiContextBudgetService.Apply(context);
        }

        public static DialogGraphAiContext Build(
            DialogGraphView graphView,
            string graphName,
            string userInstruction,
            string tone,
            string instructionPreset,
            IEnumerable<string> availableCharacters,
            IEnumerable<string> availableActions)
        {
            return Build(
                graphView,
                graphName,
                userInstruction,
                tone,
                instructionPreset,
                availableCharacters,
                availableActions,
                AiContextDepth.Local,
                null);
        }

        public static DialogGraphAiContext Build(
            DialogGraphView graphView,
            string graphName,
            string userInstruction,
            string tone,
            string instructionPreset,
            IEnumerable<string> availableCharacters,
            IEnumerable<string> availableActions,
            AiContextDepth depth,
            string anchorNodeGuid = null)
        {
            var normalizedCharacters = NormalizeDistinctStrings(availableCharacters);
            var normalizedActions = NormalizeDistinctStrings(availableActions);
            var allNodes = GetAllNodes(graphView);
            var selectedNode = GetSelectedNode(graphView);
            var anchorNode = ResolveAnchorNode(allNodes, anchorNodeGuid) ?? selectedNode;
            var selectedNodeContext = BuildNodeContext(selectedNode);
            var anchorNodeContext = BuildNodeContext(anchorNode);
            var localNeighborhood = BuildNearbyConnectedNodes(graphView, anchorNode, MaxNearbyNodeCount);
            var orderedGraphNodes = BuildOrderedGraphNodes(graphView, allNodes);
            var orderedGraphNodeContexts = orderedGraphNodes.Select(BuildNodeContext).Where(node => node != null).ToList();
            var orderedPathNodes = BuildPathNodes(graphView, allNodes, anchorNode);
            var speakerExamples = BuildSpeakerExamples(orderedGraphNodes);

            var context = new DialogGraphAiContext
            {
                contextDepth = depth,
                graphName = graphName?.Trim() ?? string.Empty,
                graphSummary = BuildGraphSummary(graphName, orderedGraphNodes, anchorNodeContext ?? selectedNodeContext),
                fullTranscriptText = BuildFullTranscript(orderedGraphNodes),
                sidebar = new DialogGraphAiSidebarContext
                {
                    userInstruction = userInstruction?.Trim() ?? string.Empty,
                    tone = tone?.Trim() ?? string.Empty,
                    instructionPreset = instructionPreset?.Trim() ?? string.Empty,
                },
                selectedNode = selectedNodeContext,
                anchorNode = anchorNodeContext,
                selectedNodeType = anchorNodeContext?.nodeType ?? selectedNodeContext?.nodeType ?? string.Empty,
                selectedNodeText = anchorNodeContext?.text ?? selectedNodeContext?.text ?? string.Empty,
                availableCharacters = normalizedCharacters,
                availableActions = normalizedActions,
                actionContexts = normalizedActions
                    .Select(actionId => new DialogActionAiContext { actionId = actionId })
                    .ToList(),
                speakerExamples = speakerExamples,
                nearbyConnectedNodes = localNeighborhood,
                localNodeNeighborhood = localNeighborhood,
                orderedPathNodes = orderedPathNodes,
                orderedGraphNodes = orderedGraphNodeContexts,
            };

            return context;
        }

        public static string ToJson(DialogGraphAiContext context, bool prettyPrint = true)
        {
            if (context == null)
            {
                return string.Empty;
            }

            return JsonUtility.ToJson(context, prettyPrint);
        }

        #endregion

        #region ---------------- Context Creation ----------------

        private static DialogGraphAiContext CreateEmptyContext(AiContextDepth depth)
        {
            return new DialogGraphAiContext
            {
                contextDepth = depth,
                graphName = string.Empty,
                graphSummary = string.Empty,
                sceneContextAssetName = string.Empty,
                environment = null,
                sceneGoal = string.Empty,
                sceneTone = string.Empty,
                extraRules = string.Empty,
                selectedNodeType = string.Empty,
                selectedNodeText = string.Empty,
                fullTranscriptText = string.Empty,
                sidebar = new DialogGraphAiSidebarContext(),
                selectedNode = null,
                anchorNode = null,
                availableCharacters = new List<string>(),
                availableActions = new List<string>(),
                actionContexts = new List<DialogActionAiContext>(),
                speakerExamples = new List<DialogCharacterAiContext>(),
                nearbyConnectedNodes = new List<DialogGraphAiConnectionContext>(),
                localNodeNeighborhood = new List<DialogGraphAiConnectionContext>(),
                orderedPathNodes = new List<DialogGraphAiNodeContext>(),
                orderedGraphNodes = new List<DialogGraphAiNodeContext>(),
            };
        }

        private static Node GetSelectedNode(DialogGraphView graphView)
        {
            if (graphView?.selection == null)
            {
                return null;
            }

            var selectedNodes = graphView.selection.OfType<Node>().ToList();
            return selectedNodes.Count == 1 ? selectedNodes[0] : null;
        }

        private static List<Node> GetAllNodes(DialogGraphView graphView)
        {
            if (graphView == null)
            {
                return new List<Node>();
            }

            return graphView.nodes.ToList().OfType<Node>().ToList();
        }

        private static Node ResolveAnchorNode(IEnumerable<Node> allNodes, string anchorNodeGuid)
        {
            if (allNodes == null || string.IsNullOrWhiteSpace(anchorNodeGuid))
            {
                return null;
            }

            return allNodes.FirstOrDefault(node =>
            {
                switch (node)
                {
                    case DialogNodeView dialogNode:
                        return string.Equals(dialogNode.GUID, anchorNodeGuid, StringComparison.OrdinalIgnoreCase);
                    case ChoiceNodeView choiceNode:
                        return string.Equals(choiceNode.GUID, anchorNodeGuid, StringComparison.OrdinalIgnoreCase);
                    case ActionNodeView actionNode:
                        return string.Equals(actionNode.GUID, anchorNodeGuid, StringComparison.OrdinalIgnoreCase);
                    case StartNodeView startNode:
                        return string.Equals(startNode.GUID, anchorNodeGuid, StringComparison.OrdinalIgnoreCase);
                    case EndNodeView endNode:
                        return string.Equals(endNode.GUID, anchorNodeGuid, StringComparison.OrdinalIgnoreCase);
                    default:
                        return false;
                }
            });
        }

        private static List<string> NormalizeDistinctStrings(IEnumerable<string> values)
        {
            if (values == null)
            {
                return new List<string>();
            }

            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        #endregion

        #region ---------------- Traversal ----------------

        private static List<DialogGraphAiNodeContext> BuildPathNodes(
            DialogGraphView graphView,
            IReadOnlyCollection<Node> allNodes,
            Node targetNode)
        {
            if (graphView == null || targetNode == null)
            {
                return new List<DialogGraphAiNodeContext>();
            }

            var startNode = allNodes.OfType<StartNodeView>().FirstOrDefault();
            if (startNode == null)
            {
                return new List<DialogGraphAiNodeContext> { BuildNodeContext(targetNode) }
                    .Where(node => node != null)
                    .ToList();
            }

            var parents = new Dictionary<Node, Node>();
            var queue = new Queue<Node>();
            var visited = new HashSet<Node>();

            queue.Enqueue(startNode);
            visited.Add(startNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == targetNode)
                {
                    break;
                }

                foreach (var next in GetOutgoingNodes(graphView, current))
                {
                    if (!visited.Add(next))
                    {
                        continue;
                    }

                    parents[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!visited.Contains(targetNode))
            {
                return new List<DialogGraphAiNodeContext> { BuildNodeContext(targetNode) }
                    .Where(node => node != null)
                    .ToList();
            }

            var path = new List<Node>();
            var cursor = targetNode;
            path.Add(cursor);

            while (parents.TryGetValue(cursor, out var parent))
            {
                cursor = parent;
                path.Add(cursor);
            }

            path.Reverse();
            return path.Select(BuildNodeContext).Where(node => node != null).ToList();
        }

        private static List<Node> BuildOrderedGraphNodes(DialogGraphView graphView, IReadOnlyCollection<Node> allNodes)
        {
            var ordered = new List<Node>();
            if (graphView == null || allNodes == null || allNodes.Count == 0)
            {
                return ordered;
            }

            var visited = new HashSet<Node>();
            var queue = new Queue<Node>();
            var startNode = allNodes.OfType<StartNodeView>().FirstOrDefault();

            if (startNode != null)
            {
                queue.Enqueue(startNode);
                visited.Add(startNode);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    ordered.Add(current);

                    foreach (var next in GetOutgoingNodes(graphView, current))
                    {
                        if (!visited.Add(next))
                        {
                            continue;
                        }

                        queue.Enqueue(next);
                    }
                }
            }

            foreach (var node in allNodes
                         .Where(node => !visited.Contains(node))
                         .OrderBy(GetNodeSortBucket)
                         .ThenBy(node => node.title ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(node);
            }

            return ordered;
        }

        private static IEnumerable<Node> GetOutgoingNodes(DialogGraphView graphView, Node node)
        {
            if (graphView == null || node == null)
            {
                return Enumerable.Empty<Node>();
            }

            return graphView.edges
                .ToList()
                .OfType<Edge>()
                .Where(edge => edge.output?.node == node && edge.input?.node is Node)
                .OrderBy(edge => GetPortIndex(edge.output))
                .ThenBy(edge => (edge.input?.node as Node)?.title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(edge => edge.input.node as Node)
                .Where(next => next != null)
                .ToList();
        }

        private static int GetNodeSortBucket(Node node)
        {
            switch (node)
            {
                case StartNodeView:
                    return 0;
                case DialogNodeView:
                    return 1;
                case ChoiceNodeView:
                    return 2;
                case ActionNodeView:
                    return 3;
                case EndNodeView:
                    return 4;
                default:
                    return 5;
            }
        }

        #endregion

        #region ---------------- Summaries ----------------

        private static string BuildGraphSummary(
            string graphName,
            IReadOnlyCollection<Node> orderedNodes,
            DialogGraphAiNodeContext selectedNode)
        {
            var nodeList = orderedNodes ?? Array.Empty<Node>();
            var dialogCount = nodeList.OfType<DialogNodeView>().Count();
            var choiceCount = nodeList.OfType<ChoiceNodeView>().Count();
            var actionCount = nodeList.OfType<ActionNodeView>().Count();
            var startCount = nodeList.OfType<StartNodeView>().Count();
            var endCount = nodeList.OfType<EndNodeView>().Count();

            var selectedSummary = selectedNode == null
                ? "No node selected."
                : $"Selected: {selectedNode.nodeType} '{selectedNode.title}'.";

            return
                $"Graph '{(string.IsNullOrWhiteSpace(graphName) ? "Unnamed" : graphName.Trim())}' has " +
                $"{nodeList.Count} node(s): {dialogCount} dialog, {choiceCount} choice, {actionCount} action, " +
                $"{startCount} start, {endCount} end. {selectedSummary}";
        }

        private static string BuildFullTranscript(IEnumerable<Node> orderedNodes)
        {
            if (orderedNodes == null)
            {
                return string.Empty;
            }

            var transcriptLines = new List<string>();

            foreach (var node in orderedNodes)
            {
                switch (node)
                {
                    case DialogNodeView dialogNode when !string.IsNullOrWhiteSpace(dialogNode.questionText):
                    {
                        var speaker = string.IsNullOrWhiteSpace(dialogNode.speakerName)
                            ? "Narrator"
                            : dialogNode.speakerName.Trim();
                        transcriptLines.Add($"{speaker}: {dialogNode.questionText.Trim()}");
                        break;
                    }

                    case ChoiceNodeView choiceNode when choiceNode.answers.Count > 0:
                    {
                        var options = choiceNode.answers
                            .Where(answer => !string.IsNullOrWhiteSpace(answer))
                            .Select(answer => answer.Trim());
                        transcriptLines.Add($"Choice: {string.Join(" | ", options)}");
                        break;
                    }

                    case ActionNodeView actionNode when !string.IsNullOrWhiteSpace(actionNode.actionId):
                        transcriptLines.Add($"[Action: {actionNode.actionId.Trim()}]");
                        break;
                }
            }

            return string.Join("\n", transcriptLines);
        }

        private static List<DialogCharacterAiContext> BuildSpeakerExamples(IEnumerable<Node> orderedNodes)
        {
            if (orderedNodes == null)
            {
                return new List<DialogCharacterAiContext>();
            }

            return orderedNodes
                .OfType<DialogNodeView>()
                .Where(node => !string.IsNullOrWhiteSpace(node.speakerName) && !string.IsNullOrWhiteSpace(node.questionText))
                .GroupBy(node => node.speakerName.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new DialogCharacterAiContext
                {
                    speakerName = group.Key,
                    exampleLines = group
                        .Select(node => node.questionText.Trim())
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.Ordinal)
                    .Take(AiContextBudgetService.DefaultSpeakerExampleLineLimit)
                    .ToList(),
                    characterId = string.Empty,
                    shortDescription = string.Empty,
                    personalityTraits = new List<string>(),
                    speechStyle = string.Empty,
                })
                .Where(context => context.exampleLines.Count > 0)
                .ToList();
        }

        public static void ApplyGraphSceneContext(DialogGraphAiContext context, DialogGraph graphAsset)
        {
            if (context == null || graphAsset == null)
            {
                return;
            }

            var sceneContext = graphAsset.sceneContext;
            var environment = graphAsset.environment ?? sceneContext?.Environment;
            var participatingCharacters = MergeCharacterDefinitions(
                graphAsset.participatingCharacters,
                sceneContext?.ParticipatingCharacters);
            var availableActions = MergeActionDefinitions(
                graphAsset.availableActions,
                sceneContext?.AvailableActions);

            context.sceneContextAssetName = sceneContext != null ? sceneContext.name : string.Empty;
            context.environment = BuildEnvironmentContext(environment);
            context.sceneGoal = FirstNonEmpty(graphAsset.sceneGoal, sceneContext?.SceneGoal);
            context.sceneTone = FirstNonEmpty(graphAsset.tone, sceneContext?.Tone, environment?.DefaultTone);
            context.extraRules = FirstNonEmpty(graphAsset.extraRules, sceneContext?.ExtraRules, environment?.CanonRules);

            if (participatingCharacters.Count > 0)
            {
                context.availableCharacters = context.availableCharacters
                    .Concat(participatingCharacters.Select(GetCharacterLabel))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                context.speakerExamples = MergeCharacterContexts(
                    context.speakerExamples,
                    participatingCharacters
                        .Select(BuildCharacterContext)
                        .Where(item => item != null));
            }

            if (availableActions.Count > 0)
            {
                context.availableActions = context.availableActions
                    .Concat(availableActions.Select(action => action.ActionID?.Trim()))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                context.actionContexts = MergeActionContexts(
                    context.actionContexts,
                    availableActions
                        .Select(BuildActionContext)
                        .Where(item => item != null));
            }
        }

        private static DialogEnvironmentAiContext BuildEnvironmentContext(DialogEnvironmentSO environment)
        {
            if (environment == null)
            {
                return null;
            }

            return new DialogEnvironmentAiContext
            {
                environmentId = environment.EnvironmentID?.Trim() ?? string.Empty,
                displayName = environment.DisplayName?.Trim() ?? string.Empty,
                description = environment.Description?.Trim() ?? string.Empty,
                atmosphere = environment.Atmosphere?.Trim() ?? string.Empty,
                canonRules = environment.CanonRules?.Trim() ?? string.Empty,
                defaultTone = environment.DefaultTone?.Trim() ?? string.Empty,
            };
        }

        private static DialogCharacterAiContext BuildCharacterContext(DialogCharacterSO character)
        {
            if (character == null)
            {
                return null;
            }

            return new DialogCharacterAiContext
            {
                characterId = character.CharacterID?.Trim() ?? string.Empty,
                speakerName = GetCharacterLabel(character),
                shortDescription = character.ShortDescription?.Trim() ?? string.Empty,
                personalityTraits = character.PersonalityTraits?
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>(),
                speechStyle = character.SpeechStyle?.Trim() ?? string.Empty,
                exampleLines = character.ExampleLines?
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .Take(AiContextBudgetService.DefaultSpeakerExampleLineLimit)
                    .ToList() ?? new List<string>(),
            };
        }

        private static DialogActionAiContext BuildActionContext(DialogActionSO action)
        {
            if (action == null)
            {
                return null;
            }

            return new DialogActionAiContext
            {
                actionId = action.ActionID?.Trim() ?? string.Empty,
                displayName = action.DisplayName?.Trim() ?? string.Empty,
                description = action.Description?.Trim() ?? string.Empty,
                defaultPayloadJson = action.DefaultPayloadJson ?? "{}",
                waitForCompletion = action.WaitForCompletion,
                defaultDelay = action.DefaultDelay,
            };
        }

        private static List<DialogCharacterSO> MergeCharacterDefinitions(
            IEnumerable<DialogCharacterSO> primary,
            IEnumerable<DialogCharacterSO> secondary)
        {
            return (primary ?? Enumerable.Empty<DialogCharacterSO>())
                .Concat(secondary ?? Enumerable.Empty<DialogCharacterSO>())
                .Where(character => character != null)
                .GroupBy(character => character.CharacterID ?? character.DisplayName ?? character.name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static List<DialogCharacterAiContext> MergeCharacterContexts(
            IEnumerable<DialogCharacterAiContext> primary,
            IEnumerable<DialogCharacterAiContext> secondary)
        {
            return (primary ?? Enumerable.Empty<DialogCharacterAiContext>())
                .Concat(secondary ?? Enumerable.Empty<DialogCharacterAiContext>())
                .Where(context => context != null)
                .GroupBy(GetCharacterContextKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var items = group.ToList();
                    return new DialogCharacterAiContext
                    {
                        characterId = FirstNonEmpty(items.Select(item => item.characterId).ToArray()),
                        speakerName = FirstNonEmpty(items.Select(item => item.speakerName).ToArray()),
                        shortDescription = FirstNonEmpty(items.Select(item => item.shortDescription).ToArray()),
                        personalityTraits = items
                            .SelectMany(item => item.personalityTraits ?? Enumerable.Empty<string>())
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .Select(value => value.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        speechStyle = FirstNonEmpty(items.Select(item => item.speechStyle).ToArray()),
                        exampleLines = items
                            .SelectMany(item => item.exampleLines ?? Enumerable.Empty<string>())
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .Select(value => value.Trim())
                            .Distinct(StringComparer.Ordinal)
                            .Take(AiContextBudgetService.DefaultSpeakerExampleLineLimit)
                            .ToList(),
                    };
                })
                .Where(context => !string.IsNullOrWhiteSpace(context.speakerName) ||
                                  !string.IsNullOrWhiteSpace(context.characterId))
                .OrderBy(context => context.speakerName ?? context.characterId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<DialogActionSO> MergeActionDefinitions(
            IEnumerable<DialogActionSO> primary,
            IEnumerable<DialogActionSO> secondary)
        {
            return (primary ?? Enumerable.Empty<DialogActionSO>())
                .Concat(secondary ?? Enumerable.Empty<DialogActionSO>())
                .Where(action => action != null)
                .GroupBy(action => action.ActionID ?? action.DisplayName ?? action.name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static List<DialogActionAiContext> MergeActionContexts(
            IEnumerable<DialogActionAiContext> primary,
            IEnumerable<DialogActionAiContext> secondary)
        {
            return (primary ?? Enumerable.Empty<DialogActionAiContext>())
                .Concat(secondary ?? Enumerable.Empty<DialogActionAiContext>())
                .Where(action => action != null)
                .GroupBy(action => action.actionId ?? action.displayName, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var items = group.ToList();
                    return new DialogActionAiContext
                    {
                        actionId = FirstNonEmpty(items.Select(item => item.actionId).ToArray()),
                        displayName = FirstNonEmpty(items.Select(item => item.displayName).ToArray()),
                        description = FirstNonEmpty(items.Select(item => item.description).ToArray()),
                        defaultPayloadJson = FirstNonEmpty(items.Select(item => item.defaultPayloadJson).ToArray()),
                        waitForCompletion = items.Last().waitForCompletion,
                        defaultDelay = items.Last().defaultDelay,
                    };
                })
                .Where(action => !string.IsNullOrWhiteSpace(action.actionId))
                .OrderBy(action => action.actionId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetCharacterContextKey(DialogCharacterAiContext context)
        {
            return FirstNonEmpty(context?.characterId, context?.speakerName);
        }

        private static string GetCharacterLabel(DialogCharacterSO character)
        {
            return !string.IsNullOrWhiteSpace(character?.DisplayName)
                ? character.DisplayName.Trim()
                : character?.CharacterID?.Trim() ?? string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        #endregion

        #region ---------------- Node Conversion ----------------

        private static List<DialogGraphAiConnectionContext> BuildNearbyConnectedNodes(
            DialogGraphView graphView,
            Node selectedNode,
            int maxCount)
        {
            var connections = new List<DialogGraphAiConnectionContext>();
            if (graphView == null || selectedNode == null)
            {
                return connections;
            }

            foreach (var edge in graphView.edges.ToList().OfType<Edge>())
            {
                if (connections.Count >= maxCount)
                {
                    break;
                }

                if (edge.output?.node == selectedNode)
                {
                    var oppositeNode = edge.input?.node as Node;
                    if (oppositeNode == null)
                    {
                        continue;
                    }

                    connections.Add(new DialogGraphAiConnectionContext
                    {
                        relationship = "outgoing",
                        fromPortIndex = GetPortIndex(edge.output),
                        node = BuildNodeContext(oppositeNode),
                    });
                }
                else if (edge.input?.node == selectedNode)
                {
                    var oppositeNode = edge.output?.node as Node;
                    if (oppositeNode == null)
                    {
                        continue;
                    }

                    connections.Add(new DialogGraphAiConnectionContext
                    {
                        relationship = "incoming",
                        fromPortIndex = GetPortIndex(edge.output),
                        node = BuildNodeContext(oppositeNode),
                    });
                }
            }

            return connections
                .Where(connection => connection.node != null)
                .OrderBy(connection => connection.relationship, StringComparer.OrdinalIgnoreCase)
                .ThenBy(connection => connection.fromPortIndex)
                .ThenBy(connection => connection.node.title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int GetPortIndex(Port port)
        {
            if (port?.node is ChoiceNodeView choiceNodeView)
            {
                return choiceNodeView.GetPortIndex(port);
            }

            return 0;
        }

        private static DialogGraphAiNodeContext BuildNodeContext(Node node)
        {
            if (node == null)
            {
                return null;
            }

            switch (node)
            {
                case DialogNodeView dialogNode:
                    return new DialogGraphAiNodeContext
                    {
                        guid = dialogNode.GUID ?? string.Empty,
                        nodeType = "Dialog",
                        title = dialogNode.nodeTitle ?? string.Empty,
                        text = dialogNode.questionText ?? string.Empty,
                        speakerName = dialogNode.speakerName ?? string.Empty,
                        choices = new List<string>(),
                        actionId = string.Empty,
                        payloadJson = string.Empty,
                        waitForCompletion = false,
                        waitSeconds = 0f,
                    };

                case ChoiceNodeView choiceNode:
                    return new DialogGraphAiNodeContext
                    {
                        guid = choiceNode.GUID ?? string.Empty,
                        nodeType = "Choice",
                        title = node.title ?? "Choice",
                        text = string.Join(" | ", choiceNode.answers.Where(answer => !string.IsNullOrWhiteSpace(answer)).Select(answer => answer.Trim())),
                        speakerName = string.Empty,
                        choices = choiceNode.answers
                            .Where(answer => !string.IsNullOrWhiteSpace(answer))
                            .Select(answer => answer.Trim())
                            .ToList(),
                        actionId = string.Empty,
                        payloadJson = string.Empty,
                        waitForCompletion = false,
                        waitSeconds = 0f,
                    };

                case ActionNodeView actionNode:
                    return new DialogGraphAiNodeContext
                    {
                        guid = actionNode.GUID ?? string.Empty,
                        nodeType = "Action",
                        title = node.title ?? "Action",
                        text = !string.IsNullOrWhiteSpace(actionNode.payloadJson) ? actionNode.payloadJson : actionNode.actionId,
                        speakerName = string.Empty,
                        choices = new List<string>(),
                        actionId = actionNode.actionId ?? string.Empty,
                        payloadJson = actionNode.payloadJson ?? string.Empty,
                        waitForCompletion = actionNode.waitForCompletion,
                        waitSeconds = actionNode.waitSeconds,
                    };

                case StartNodeView startNode:
                    return new DialogGraphAiNodeContext
                    {
                        guid = startNode.GUID ?? string.Empty,
                        nodeType = "Start",
                        title = node.title ?? "Start",
                        text = string.Empty,
                        speakerName = string.Empty,
                        choices = new List<string>(),
                        actionId = string.Empty,
                        payloadJson = string.Empty,
                        waitForCompletion = false,
                        waitSeconds = 0f,
                    };

                case EndNodeView endNode:
                    return new DialogGraphAiNodeContext
                    {
                        guid = endNode.GUID ?? string.Empty,
                        nodeType = "End",
                        title = node.title ?? "End",
                        text = string.Empty,
                        speakerName = string.Empty,
                        choices = new List<string>(),
                        actionId = string.Empty,
                        payloadJson = string.Empty,
                        waitForCompletion = false,
                        waitSeconds = 0f,
                    };

                default:
                    return new DialogGraphAiNodeContext
                    {
                        guid = string.Empty,
                        nodeType = node.GetType().Name,
                        title = node.title ?? string.Empty,
                        text = string.Empty,
                        speakerName = string.Empty,
                        choices = new List<string>(),
                        actionId = string.Empty,
                        payloadJson = string.Empty,
                        waitForCompletion = false,
                        waitSeconds = 0f,
                    };
            }
        }

        #endregion
    }

    /// <summary>
    /// Applies deterministic size limits to AI context payloads based on requested depth.
    /// </summary>
    public static class AiContextBudgetService
    {
        public const int DefaultSpeakerExampleLineLimit = 3;

        private const int MinimalCharacterLimit = 12;
        private const int MinimalActionLimit = 12;
        private const int LocalConnectionLimit = 6;
        private const int PathNodeLimit = 10;
        private const int FullGraphNodeLimit = 24;
        private const int TranscriptCharacterLimit = 3000;
        private const int GraphSummaryCharacterLimit = 320;

        #region ---------------- Public API ----------------

        public static DialogGraphAiContext Apply(DialogGraphAiContext source)
        {
            if (source == null)
            {
                return null;
            }

            var budgeted = Clone(source);
            budgeted.graphSummary = TrimText(source.graphSummary, GraphSummaryCharacterLimit);
            budgeted.sceneContextAssetName = source.sceneContextAssetName ?? string.Empty;
            budgeted.sceneGoal = TrimText(source.sceneGoal, GraphSummaryCharacterLimit);
            budgeted.sceneTone = TrimText(source.sceneTone, 120);
            budgeted.extraRules = TrimText(source.extraRules, TranscriptCharacterLimit / 2);
            budgeted.environment = Clone(source.environment);
            budgeted.availableCharacters = source.availableCharacters?.Take(MinimalCharacterLimit).ToList() ?? new List<string>();
            budgeted.availableActions = source.availableActions?.Take(MinimalActionLimit).ToList() ?? new List<string>();
            budgeted.actionContexts = source.actionContexts?.Take(MinimalActionLimit).Select(Clone).ToList() ?? new List<DialogActionAiContext>();
            budgeted.speakerExamples = source.speakerExamples?.Select(Clone).ToList() ?? new List<DialogCharacterAiContext>();

            switch (source.contextDepth)
            {
                case AiContextDepth.Minimal:
                    budgeted.selectedNode = null;
                    budgeted.anchorNode = null;
                    budgeted.selectedNodeType = string.Empty;
                    budgeted.selectedNodeText = string.Empty;
                    budgeted.nearbyConnectedNodes = new List<DialogGraphAiConnectionContext>();
                    budgeted.localNodeNeighborhood = new List<DialogGraphAiConnectionContext>();
                    budgeted.orderedPathNodes = new List<DialogGraphAiNodeContext>();
                    budgeted.orderedGraphNodes = new List<DialogGraphAiNodeContext>();
                    budgeted.fullTranscriptText = string.Empty;
                    break;

                case AiContextDepth.Local:
                    budgeted.nearbyConnectedNodes = TrimConnections(source.localNodeNeighborhood, LocalConnectionLimit);
                    budgeted.localNodeNeighborhood = CloneConnections(budgeted.nearbyConnectedNodes);
                    budgeted.orderedPathNodes = new List<DialogGraphAiNodeContext>();
                    budgeted.orderedGraphNodes = new List<DialogGraphAiNodeContext>();
                    budgeted.fullTranscriptText = string.Empty;
                    break;

                case AiContextDepth.Path:
                    budgeted.nearbyConnectedNodes = TrimConnections(source.localNodeNeighborhood, LocalConnectionLimit);
                    budgeted.localNodeNeighborhood = CloneConnections(budgeted.nearbyConnectedNodes);
                    budgeted.orderedPathNodes = TrimNodes(source.orderedPathNodes, PathNodeLimit);
                    budgeted.orderedGraphNodes = new List<DialogGraphAiNodeContext>();
                    budgeted.fullTranscriptText = string.Empty;
                    break;

                case AiContextDepth.FullGraph:
                    budgeted.nearbyConnectedNodes = TrimConnections(source.localNodeNeighborhood, LocalConnectionLimit);
                    budgeted.localNodeNeighborhood = CloneConnections(budgeted.nearbyConnectedNodes);
                    budgeted.orderedPathNodes = TrimNodes(source.orderedPathNodes, PathNodeLimit);
                    budgeted.orderedGraphNodes = TrimNodes(source.orderedGraphNodes, FullGraphNodeLimit);
                    budgeted.fullTranscriptText = TrimText(source.fullTranscriptText, TranscriptCharacterLimit);
                    break;
            }

            return budgeted;
        }

        #endregion

        #region ---------------- Clone Helpers ----------------

        private static DialogGraphAiContext Clone(DialogGraphAiContext source)
        {
            return new DialogGraphAiContext
            {
                contextDepth = source.contextDepth,
                graphName = source.graphName ?? string.Empty,
                graphSummary = source.graphSummary ?? string.Empty,
                sceneContextAssetName = source.sceneContextAssetName ?? string.Empty,
                environment = Clone(source.environment),
                sceneGoal = source.sceneGoal ?? string.Empty,
                sceneTone = source.sceneTone ?? string.Empty,
                extraRules = source.extraRules ?? string.Empty,
                selectedNodeType = source.selectedNodeType ?? string.Empty,
                selectedNodeText = source.selectedNodeText ?? string.Empty,
                fullTranscriptText = source.fullTranscriptText ?? string.Empty,
                sidebar = source.sidebar == null
                    ? new DialogGraphAiSidebarContext()
                    : new DialogGraphAiSidebarContext
                    {
                        userInstruction = source.sidebar.userInstruction ?? string.Empty,
                        tone = source.sidebar.tone ?? string.Empty,
                        instructionPreset = source.sidebar.instructionPreset ?? string.Empty,
                    },
                selectedNode = Clone(source.selectedNode),
                anchorNode = Clone(source.anchorNode),
                availableCharacters = source.availableCharacters?.ToList() ?? new List<string>(),
                availableActions = source.availableActions?.ToList() ?? new List<string>(),
                actionContexts = source.actionContexts?.Select(Clone).ToList() ?? new List<DialogActionAiContext>(),
                speakerExamples = source.speakerExamples?.Select(Clone).ToList() ?? new List<DialogCharacterAiContext>(),
                nearbyConnectedNodes = CloneConnections(source.nearbyConnectedNodes),
                localNodeNeighborhood = CloneConnections(source.localNodeNeighborhood),
                orderedPathNodes = source.orderedPathNodes?.Select(Clone).ToList() ?? new List<DialogGraphAiNodeContext>(),
                orderedGraphNodes = source.orderedGraphNodes?.Select(Clone).ToList() ?? new List<DialogGraphAiNodeContext>(),
            };
        }

        private static DialogGraphAiNodeContext Clone(DialogGraphAiNodeContext source)
        {
            if (source == null)
            {
                return null;
            }

            return new DialogGraphAiNodeContext
            {
                guid = source.guid ?? string.Empty,
                nodeType = source.nodeType ?? string.Empty,
                title = source.title ?? string.Empty,
                text = source.text ?? string.Empty,
                speakerName = source.speakerName ?? string.Empty,
                choices = source.choices?.ToList() ?? new List<string>(),
                actionId = source.actionId ?? string.Empty,
                payloadJson = source.payloadJson ?? string.Empty,
                waitForCompletion = source.waitForCompletion,
                waitSeconds = source.waitSeconds,
            };
        }

        private static DialogGraphAiConnectionContext Clone(DialogGraphAiConnectionContext source)
        {
            if (source == null)
            {
                return null;
            }

            return new DialogGraphAiConnectionContext
            {
                relationship = source.relationship ?? string.Empty,
                fromPortIndex = source.fromPortIndex,
                node = Clone(source.node),
            };
        }

        private static DialogCharacterAiContext Clone(DialogCharacterAiContext source)
        {
            if (source == null)
            {
                return null;
            }

            return new DialogCharacterAiContext
            {
                characterId = source.characterId ?? string.Empty,
                speakerName = source.speakerName ?? string.Empty,
                shortDescription = source.shortDescription ?? string.Empty,
                personalityTraits = source.personalityTraits?.ToList() ?? new List<string>(),
                speechStyle = source.speechStyle ?? string.Empty,
                exampleLines = source.exampleLines?
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Take(DefaultSpeakerExampleLineLimit)
                    .ToList() ?? new List<string>(),
            };
        }

        private static DialogActionAiContext Clone(DialogActionAiContext source)
        {
            if (source == null)
            {
                return null;
            }

            return new DialogActionAiContext
            {
                actionId = source.actionId ?? string.Empty,
                displayName = source.displayName ?? string.Empty,
                description = source.description ?? string.Empty,
                defaultPayloadJson = source.defaultPayloadJson ?? "{}",
                waitForCompletion = source.waitForCompletion,
                defaultDelay = source.defaultDelay,
            };
        }

        private static DialogEnvironmentAiContext Clone(DialogEnvironmentAiContext source)
        {
            if (source == null)
            {
                return null;
            }

            return new DialogEnvironmentAiContext
            {
                environmentId = source.environmentId ?? string.Empty,
                displayName = source.displayName ?? string.Empty,
                description = source.description ?? string.Empty,
                atmosphere = source.atmosphere ?? string.Empty,
                canonRules = source.canonRules ?? string.Empty,
                defaultTone = source.defaultTone ?? string.Empty,
            };
        }

        #endregion

        #region ---------------- Trimming ----------------

        private static List<DialogGraphAiConnectionContext> TrimConnections(
            IEnumerable<DialogGraphAiConnectionContext> source,
            int limit)
        {
            return source?
                .Where(item => item != null)
                .Take(limit)
                .Select(Clone)
                .ToList() ?? new List<DialogGraphAiConnectionContext>();
        }

        private static List<DialogGraphAiConnectionContext> CloneConnections(
            IEnumerable<DialogGraphAiConnectionContext> source)
        {
            return source?
                .Where(item => item != null)
                .Select(Clone)
                .ToList() ?? new List<DialogGraphAiConnectionContext>();
        }

        private static List<DialogGraphAiNodeContext> TrimNodes(
            IEnumerable<DialogGraphAiNodeContext> source,
            int limit)
        {
            return source?
                .Where(item => item != null)
                .Take(limit)
                .Select(Clone)
                .ToList() ?? new List<DialogGraphAiNodeContext>();
        }

        private static string TrimText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength).TrimEnd() + "...";
        }

        #endregion
    }

    /// <summary>
    /// Formats compact, read-only AI prompt context from <see cref="DialogGraphAiContext"/>.
    /// </summary>
    public static class DialogGraphAiPromptContextFormatter
    {
        public static string BuildReadOnlyContextSummary(DialogGraphAiContext context)
        {
            if (context == null)
            {
                return "{}";
            }

            var sections = new List<string>();

            if (!string.IsNullOrWhiteSpace(context.graphSummary))
            {
                sections.Add($"  \"graphSummary\": \"{EscapeJson(context.graphSummary)}\"");
            }

            if (!string.IsNullOrWhiteSpace(context.sceneContextAssetName))
            {
                sections.Add($"  \"sceneContextAsset\": \"{EscapeJson(context.sceneContextAssetName)}\"");
            }

            if (context.environment != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("  \"environment\": {");
                sb.AppendLine($"    \"id\": \"{EscapeJson(context.environment.environmentId)}\",");
                sb.AppendLine($"    \"displayName\": \"{EscapeJson(context.environment.displayName)}\",");
                sb.AppendLine($"    \"description\": \"{EscapeJson(context.environment.description)}\",");
                sb.AppendLine($"    \"atmosphere\": \"{EscapeJson(context.environment.atmosphere)}\",");
                sb.AppendLine($"    \"canonRules\": \"{EscapeJson(context.environment.canonRules)}\",");
                sb.Append("    \"defaultTone\": \"" + EscapeJson(context.environment.defaultTone) + "\"\n  }");
                sections.Add(sb.ToString());
            }

            if (!string.IsNullOrWhiteSpace(context.sceneGoal))
            {
                sections.Add($"  \"sceneGoal\": \"{EscapeJson(context.sceneGoal)}\"");
            }

            if (!string.IsNullOrWhiteSpace(context.sceneTone))
            {
                sections.Add($"  \"tone\": \"{EscapeJson(context.sceneTone)}\"");
            }

            if (!string.IsNullOrWhiteSpace(context.extraRules))
            {
                sections.Add($"  \"extraRules\": \"{EscapeJson(context.extraRules)}\"");
            }

            if (context.selectedNode != null)
            {
                sections.Add(BuildNodeSection("selectedNode", context.selectedNode));
            }

            if (context.anchorNode != null && !IsSameNode(context.selectedNode, context.anchorNode))
            {
                sections.Add(BuildNodeSection("anchorNode", context.anchorNode));
            }

            var relevantSpeakers = BuildRelevantSpeakerProfiles(context);
            if (relevantSpeakers.Count > 0)
            {
                sections.Add(BuildCharacterArray("activeSpeakerProfiles", relevantSpeakers));
            }

            if (context.speakerExamples != null && context.speakerExamples.Count > 0)
            {
                sections.Add(BuildCharacterArray("registeredCharacters", context.speakerExamples));
            }

            if (context.actionContexts != null && context.actionContexts.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("  \"registeredActions\": [");
                for (var i = 0; i < context.actionContexts.Count; i++)
                {
                    var action = context.actionContexts[i];
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"id\": \"{EscapeJson(action.actionId)}\",");
                    sb.AppendLine($"      \"displayName\": \"{EscapeJson(action.displayName)}\",");
                    sb.AppendLine($"      \"description\": \"{EscapeJson(action.description)}\",");
                    sb.AppendLine($"      \"defaultPayloadJson\": \"{EscapeJson(action.defaultPayloadJson)}\",");
                    sb.AppendLine($"      \"waitForCompletion\": {action.waitForCompletion.ToString().ToLowerInvariant()},");
                    sb.AppendLine($"      \"defaultDelay\": {action.defaultDelay.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    sb.AppendLine(i < context.actionContexts.Count - 1 ? "    }," : "    }");
                }
                sb.Append("  ]");
                sections.Add(sb.ToString());
            }

            var localNodes = context.localNodeNeighborhood ?? context.nearbyConnectedNodes;
            if (localNodes != null && localNodes.Count > 0)
            {
                var nearby = localNodes
                    .Where(connection => connection?.node != null)
                    .Take(4)
                    .ToList();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("  \"localNeighborhood\": [");
                for (var i = 0; i < nearby.Count; i++)
                {
                    var connection = nearby[i];
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"relationship\": \"{EscapeJson(connection.relationship)}\",");
                    sb.AppendLine($"      \"type\": \"{EscapeJson(connection.node.nodeType)}\",");
                    sb.AppendLine($"      \"speaker\": \"{EscapeJson(connection.node.speakerName)}\",");
                    sb.AppendLine($"      \"text\": \"{EscapeJson(connection.node.text)}\"");
                    sb.AppendLine(i < nearby.Count - 1 ? "    }," : "    }");
                }
                sb.Append("  ]");
                sections.Add(sb.ToString());
            }

            if (context.orderedPathNodes != null && context.orderedPathNodes.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("  \"pathFromStart\": [");
                for (var i = 0; i < context.orderedPathNodes.Count; i++)
                {
                    var node = context.orderedPathNodes[i];
                    sb.AppendLine("    {");
                    sb.AppendLine($"      \"type\": \"{EscapeJson(node.nodeType)}\",");
                    sb.AppendLine($"      \"title\": \"{EscapeJson(node.title)}\",");
                    sb.AppendLine($"      \"speaker\": \"{EscapeJson(node.speakerName)}\",");
                    sb.AppendLine($"      \"text\": \"{EscapeJson(node.text)}\"");
                    sb.AppendLine(i < context.orderedPathNodes.Count - 1 ? "    }," : "    }");
                }
                sb.Append("  ]");
                sections.Add(sb.ToString());
            }

            if (!string.IsNullOrWhiteSpace(context.fullTranscriptText))
            {
                sections.Add($"  \"fullTranscript\": \"{EscapeJson(context.fullTranscriptText)}\"");
            }

            return sections.Count == 0
                ? "{}"
                : "{\n" + string.Join(",\n", sections) + "\n}";
        }

        public static string BuildSpeakerVoiceGuidance(DialogGraphAiContext context, string speakerName)
        {
            var character = FindSpeakerContext(context, speakerName);
            if (character == null)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Speaker Voice Guidance:");
            sb.AppendLine($"- Speaker: {Safe(character.speakerName, speakerName)}");

            if (!string.IsNullOrWhiteSpace(character.shortDescription))
            {
                sb.AppendLine($"- Role: {character.shortDescription.Trim()}");
            }

            if (character.personalityTraits != null && character.personalityTraits.Count > 0)
            {
                sb.AppendLine($"- Traits: {string.Join(", ", character.personalityTraits.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()))}");
            }

            if (!string.IsNullOrWhiteSpace(character.speechStyle))
            {
                sb.AppendLine($"- Speech Style: {character.speechStyle.Trim()}");
            }

            if (character.exampleLines != null && character.exampleLines.Count > 0)
            {
                sb.AppendLine("- Example Lines:");
                foreach (var line in character.exampleLines.Where(value => !string.IsNullOrWhiteSpace(value)).Take(AiContextBudgetService.DefaultSpeakerExampleLineLimit))
                {
                    sb.AppendLine($"  - {line.Trim()}");
                }
            }

            sb.AppendLine("Use this as authoritative guidance for preserving this speaker's established voice.");
            return sb.ToString().Trim();
        }

        private static string BuildNodeSection(string propertyName, DialogGraphAiNodeContext node)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"  \"{propertyName}\": {{");
            sb.AppendLine($"    \"type\": \"{EscapeJson(node.nodeType)}\",");
            sb.AppendLine($"    \"title\": \"{EscapeJson(node.title)}\",");
            sb.AppendLine($"    \"speaker\": \"{EscapeJson(node.speakerName)}\",");
            sb.Append("    \"text\": \"" + EscapeJson(node.text) + "\"\n  }");
            return sb.ToString();
        }

        private static string BuildCharacterArray(string propertyName, IReadOnlyList<DialogCharacterAiContext> characters)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"  \"{propertyName}\": [");
            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"id\": \"{EscapeJson(character.characterId)}\",");
                sb.AppendLine($"      \"displayName\": \"{EscapeJson(character.speakerName)}\",");
                sb.AppendLine($"      \"shortDescription\": \"{EscapeJson(character.shortDescription)}\",");
                sb.AppendLine($"      \"speechStyle\": \"{EscapeJson(character.speechStyle)}\",");
                sb.AppendLine($"      \"personalityTraits\": [{string.Join(", ", (character.personalityTraits ?? new List<string>()).Select(value => $"\"{EscapeJson(value)}\""))}],");
                sb.AppendLine($"      \"exampleLines\": [{string.Join(", ", (character.exampleLines ?? new List<string>()).Select(value => $"\"{EscapeJson(value)}\""))}]");
                sb.AppendLine(i < characters.Count - 1 ? "    }," : "    }");
            }
            sb.Append("  ]");
            return sb.ToString();
        }

        private static List<DialogCharacterAiContext> BuildRelevantSpeakerProfiles(DialogGraphAiContext context)
        {
            if (context?.speakerExamples == null || context.speakerExamples.Count == 0)
            {
                return new List<DialogCharacterAiContext>();
            }

            var relevantNames = new List<string>();
            AddSpeakerName(relevantNames, context.selectedNode?.speakerName);
            AddSpeakerName(relevantNames, context.anchorNode?.speakerName);

            if (context.orderedPathNodes != null)
            {
                foreach (var node in context.orderedPathNodes)
                {
                    AddSpeakerName(relevantNames, node?.speakerName);
                }
            }

            return relevantNames
                .Select(name => FindSpeakerContext(context, name))
                .Where(character => character != null)
                .GroupBy(character => Safe(character.characterId, character.speakerName), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static DialogCharacterAiContext FindSpeakerContext(DialogGraphAiContext context, string speakerName)
        {
            if (context?.speakerExamples == null || string.IsNullOrWhiteSpace(speakerName))
            {
                return null;
            }

            var normalized = speakerName.Trim();
            return context.speakerExamples.FirstOrDefault(character =>
                string.Equals(character?.speakerName?.Trim(), normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(character?.characterId?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddSpeakerName(ICollection<string> values, string speakerName)
        {
            if (!string.IsNullOrWhiteSpace(speakerName))
            {
                values.Add(speakerName.Trim());
            }
        }

        private static bool IsSameNode(DialogGraphAiNodeContext a, DialogGraphAiNodeContext b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(a.guid) && !string.IsNullOrWhiteSpace(b.guid))
            {
                return string.Equals(a.guid, b.guid, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(a.nodeType, b.nodeType, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.title, b.title, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.text, b.text, StringComparison.Ordinal);
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value.Trim();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
