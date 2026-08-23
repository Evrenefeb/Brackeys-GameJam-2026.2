using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.Services.Validation;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Variables;
using NUnit.Framework;
using UnityEditor;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DemoContentConsistencyTests
    {
        private const string GraphsRoot = "Assets/DialogGraphSystem/Graphs";
        private const string DefinitionsRoot = "Assets/DialogGraphSystem/Definitions";
        private const string SourceTablePath = DefinitionsRoot + "/Localization/DialogLocalizationTable.asset";
        private const string GermanTablePath = DefinitionsRoot + "/Localization/DialogLocalizationTable_de_DE.asset";

        private static readonly string[] ExpectedEntryGraphPaths =
        {
            GraphsRoot + "/Demo_ProductTour.asset",
            GraphsRoot + "/Demo_ShopGate.asset",
            GraphsRoot + "/Demo_ControlRoomActions.asset"
        };

        private const string ExpectedSupportGraphPath = GraphsRoot + "/Demo_ReactorAftermath.asset";

        [Test]
        public void ShowcaseCatalog_ContainsExactlyThreeEntryGraphsAndOneSupportGraph()
        {
            var graphs = LoadAllGraphs();

            Assert.That(graphs.Select(AssetDatabase.GetAssetPath),
                Is.EquivalentTo(ExpectedEntryGraphPaths.Append(ExpectedSupportGraphPath)));
            Assert.That(graphs.Count(graph => graph.tags.Contains("demo-entry")), Is.EqualTo(3));
            Assert.That(graphs.Count(graph => graph.tags.Contains("demo-support")), Is.EqualTo(1));
        }

        [Test]
        public void ShowcaseGraphs_HaveProfessionalMetadataAndNoTemporaryNames()
        {
            foreach (var graph in LoadAllGraphs())
            {
                var path = AssetDatabase.GetAssetPath(graph);
                Assert.That(graph.GraphSchemaVersion, Is.EqualTo(DialogGraph.CurrentSchemaVersion), path);
                Assert.That(graph.GraphGuid, Is.Not.Empty, path);
                Assert.That(graph.graphTitle, Is.Not.Empty, path);
                Assert.That(graph.description, Is.Not.Empty, path);
                Assert.That(graph.author, Is.EqualTo("Beka Forge"), path);
                Assert.That(graph.primaryCategory, Is.Not.Empty, path);
                Assert.That(graph.categories, Is.Not.Empty, path);
                Assert.That(graph.tags, Is.Not.Empty, path);
                Assert.That(graph.sceneGoal, Is.Not.Empty, path);
                Assert.That(graph.tone, Is.Not.Empty, path);
                Assert.That(graph.environment, Is.Not.Null, path);

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path).Where(asset => asset != null))
                {
                    Assert.That(asset.name, Does.Not.Contain("Test").IgnoreCase, path);
                    Assert.That(asset.name, Does.Not.Contain("Untitled").IgnoreCase, path);
                    Assert.That(asset.name, Does.Not.Contain("New ").IgnoreCase, path);
                }
            }
        }

        [Test]
        public void DefinitionFiles_UseCanonicalVariableKeyCasing()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<DialogVariableSO>(
                DefinitionsRoot + "/Variables/gold.asset"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<DialogVariableSO>(
                DefinitionsRoot + "/Variables/hasKey.asset"), Is.Not.Null);

            var actualFileNames = Directory.GetFiles(DefinitionsRoot + "/Variables", "*.asset")
                .Select(Path.GetFileName)
                .ToList();
            Assert.That(actualFileNames, Does.Contain("gold.asset"));
            Assert.That(actualFileNames, Does.Contain("hasKey.asset"));
            Assert.That(actualFileNames, Does.Not.Contain("Gold.asset"));
            Assert.That(actualFileNames, Does.Not.Contain("haskey.asset"));

            foreach (var path in FindAssetPaths<DialogVariableSO>(DefinitionsRoot + "/Variables"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<DialogVariableSO>(path);
                Assert.That(Path.GetFileNameWithoutExtension(path), Is.EqualTo(definition.Key), path);
                Assert.That(definition.name, Is.EqualTo(definition.Key), path);
            }
        }

        [Test]
        public void SceneContexts_UseOnlyPackagedCharacterNames()
        {
            var packagedNames = FindAssets<DialogCharacterSO>(DefinitionsRoot + "/Characters")
                .Select(character => character.DisplayName)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var context in FindAssets<DialogSceneContextSO>(DefinitionsRoot + "/SceneContexts"))
            {
                var copy = string.Join(" ", context.SceneGoal, context.Tone, context.ExtraRules);
                Assert.That(copy, Does.Not.Contain("Arjan"), context.name);
                Assert.That(copy, Does.Not.Contain("Kira"), context.name);
                Assert.That(context.ParticipatingCharacters, Is.Not.Empty, context.name);
                Assert.That(context.ParticipatingCharacters.All(character =>
                    character != null && packagedNames.Contains(character.DisplayName)), Is.True, context.name);
                Assert.That(context.IsValid(out var error), Is.True, error);
            }
        }

        [Test]
        public void ShowcaseGraphReferences_ResolveToPackagedDefinitions()
        {
            var knownSpeakers = FindAssets<DialogCharacterSO>(DefinitionsRoot + "/Characters")
                .Select(character => character.DisplayName)
                .ToHashSet(StringComparer.Ordinal);
            var knownActionIds = FindAssets<DialogActionSO>(DefinitionsRoot + "/Actions")
                .Select(action => action.ActionID)
                .ToHashSet(StringComparer.Ordinal);
            var knownVariables = FindAssets<DialogVariableSO>(DefinitionsRoot + "/Variables");
            var knownVariableKeys = knownVariables.Select(variable => variable.Key)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var graph in LoadAllGraphs())
            {
                var path = AssetDatabase.GetAssetPath(graph);
                Assert.That(graph.participatingCharacters, Is.Not.Empty, path);
                Assert.That(graph.availableVariables, Is.Not.Null, path);
                Assert.That(graph.nodes.All(node => knownSpeakers.Contains(node.speakerName)), Is.True, path);
                Assert.That(graph.actionNodes.All(node => knownActionIds.Contains(node.actionId)), Is.True, path);
                Assert.That(graph.conditionNodes.All(node => knownVariableKeys.Contains(node.variableName)), Is.True, path);
                Assert.That(graph.variableMutationNodes.All(node => knownVariableKeys.Contains(node.variableName)), Is.True, path);

                var validation = DialogGraphValidator.Validate(
                    graph, knownSpeakers, knownActionIds, knownVariables, hasLocaleSource: true);
                Assert.That(validation.ErrorCount, Is.Zero,
                    path + Environment.NewLine + string.Join(Environment.NewLine, validation.Errors));
            }
        }

        [Test]
        public void EveryVisibleShowcaseString_HasEnglishAndGermanLocalization()
        {
            var source = AssetDatabase.LoadAssetAtPath<DialogLocalizationTable>(SourceTablePath);
            var german = AssetDatabase.LoadAssetAtPath<DialogLocalizationTable>(GermanTablePath);
            Assert.That(source, Is.Not.Null);
            Assert.That(german, Is.Not.Null);

            foreach (var graph in LoadAllGraphs())
            {
                foreach (var node in graph.nodes)
                {
                    AssertLocalized(source, german, node.speakerNameLocaleKey, graph.name + "/" + node.name + " speaker");
                    AssertLocalized(source, german, node.questionTextLocaleKey, graph.name + "/" + node.name + " text");
                }

                foreach (var node in graph.choiceNodes)
                {
                    AssertLocalized(source, german, node.textLocaleKey, graph.name + "/" + node.name + " prompt");
                    foreach (var choice in node.choices)
                    {
                        AssertLocalized(source, german, choice.answerTextLocaleKey,
                            graph.name + "/" + node.name + "/" + choice.choiceId);
                    }
                }
            }
        }

        [Test]
        public void EveryAuthoredShowcaseNode_IsReachableAndCanReachEnd()
        {
            foreach (var graph in LoadAllGraphs())
            {
                var allNodeIds = graph.EnumerateAllNodeGuids().ToHashSet(StringComparer.Ordinal);
                var forward = BuildAdjacency(graph.links, reverse: false);
                var reverse = BuildAdjacency(graph.links, reverse: true);
                var fromStart = Traverse(graph.startGuid, forward);
                var toEnd = Traverse(graph.endGuid, reverse);

                Assert.That(fromStart, Is.SupersetOf(allNodeIds),
                    $"{graph.name} has unreachable nodes: {string.Join(", ", allNodeIds.Except(fromStart))}");
                Assert.That(toEnd, Is.SupersetOf(allNodeIds),
                    $"{graph.name} has nodes with no path to End: {string.Join(", ", allNodeIds.Except(toEnd))}");
            }
        }

        [Test]
        public void ReactorGraphJump_UsesDirectStableReferenceWithoutCycle()
        {
            var reactor = AssetDatabase.LoadAssetAtPath<DialogGraph>(ExpectedEntryGraphPaths[2]);
            var support = AssetDatabase.LoadAssetAtPath<DialogGraph>(ExpectedSupportGraphPath);

            Assert.That(reactor, Is.Not.Null);
            Assert.That(support, Is.Not.Null);
            Assert.That(reactor.graphJumpNodes, Has.Count.EqualTo(1));

            var reference = reactor.graphJumpNodes.Single().targetGraph;
            Assert.That(reference.graphAsset, Is.SameAs(support));
            Assert.That(reference.graphGuid, Is.EqualTo(support.GraphGuid));
            Assert.That(reference.graphName, Is.EqualTo(support.name));
            Assert.That(reference.assetPath, Is.EqualTo(ExpectedSupportGraphPath));
            Assert.That(support.graphJumpNodes, Is.Empty);
        }

        private static List<DialogGraph> LoadAllGraphs()
        {
            return FindAssets<DialogGraph>(GraphsRoot);
        }

        private static List<T> FindAssets<T>(string root) where T : UnityEngine.Object
        {
            return FindAssetPaths<T>(root)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList();
        }

        private static IEnumerable<string> FindAssetPaths<T>(string root) where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal);
        }

        private static void AssertLocalized(
            DialogLocalizationTable source,
            DialogLocalizationTable german,
            string key,
            string context)
        {
            Assert.That(key, Is.Not.Null.And.Not.Empty, context);
            Assert.That(source.TryResolve(key), Is.Not.Null.And.Not.Empty, context + " en-US");
            Assert.That(german.TryResolve(key), Is.Not.Null.And.Not.Empty, context + " de-DE");
        }

        private static Dictionary<string, List<string>> BuildAdjacency(
            IEnumerable<GraphLink> links,
            bool reverse)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var link in links.Where(link => link != null))
            {
                var from = reverse ? link.toGuid : link.fromGuid;
                var to = reverse ? link.fromGuid : link.toGuid;
                if (!result.TryGetValue(from, out var targets))
                {
                    targets = new List<string>();
                    result[from] = targets;
                }

                targets.Add(to);
            }

            return result;
        }

        private static HashSet<string> Traverse(
            string start,
            IReadOnlyDictionary<string, List<string>> adjacency)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            pending.Enqueue(start);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!visited.Add(current) || !adjacency.TryGetValue(current, out var targets))
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    pending.Enqueue(target);
                }
            }

            return visited;
        }
    }
}
