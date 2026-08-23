using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using NUnit.Framework;
using UnityEditor;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DemoShowcaseRouteSemanticsTests
    {
        [Test]
        public void ProductTour_HasTwoDistinctChoiceRoutesWithDistinctActionsAndOutcomes()
        {
            var graph = Load(DemoShowcaseGraphSpecs.ProductPath);
            var choice = graph.choiceNodes.Single();

            Assert.That(choice.choices, Has.Count.EqualTo(2));
            Assert.That(choice.choices.Select(option => option.answerText), Is.Unique);
            Assert.That(choice.choices.Select(option => option.nextNodeGUID), Is.Unique);

            AssertRoute(
                graph,
                new Dictionary<string, string> { [choice.GetGuid()] = choice.choices[0].PortKey },
                actions: new[] { "TurnOnTV" },
                mutations: Array.Empty<string>(),
                outcome: "product_tour_branching");
            AssertRoute(
                graph,
                new Dictionary<string, string> { [choice.GetGuid()] = choice.choices[1].PortKey },
                actions: new[] { "FadeLights" },
                mutations: Array.Empty<string>(),
                outcome: "product_tour_actions");
        }

        [Test]
        public void ShopGate_ModelsThreeApproachesAndSixDeterministicResults()
        {
            var graph = Load(DemoShowcaseGraphSpecs.ShopPath);
            var choice = graph.choiceNodes.Single();
            var conditions = graph.conditionNodes.ToDictionary(node => node.variableName, StringComparer.Ordinal);

            Assert.That(choice.choices, Has.Count.EqualTo(3));
            Assert.That(conditions.Keys, Is.EquivalentTo(new[] { "gold", "hasKey", "trustLevel" }));
            Assert.That(graph.actionNodes.Select(node => node.actionId),
                Is.EqualTo(new[] { "OpenGate", "OpenGate", "OpenGate" }));

            AssertShopRoute(graph, choice, 0, conditions["gold"], true, "gold", "shop_gate_paid", opensGate: true);
            AssertShopRoute(graph, choice, 0, conditions["gold"], false, null, "shop_gate_denied_funds", opensGate: false);
            AssertShopRoute(graph, choice, 1, conditions["hasKey"], true, null, "shop_gate_key", opensGate: true);
            AssertShopRoute(graph, choice, 1, conditions["hasKey"], false, null, "shop_gate_denied_key", opensGate: false);
            AssertShopRoute(graph, choice, 2, conditions["trustLevel"], true, null, "shop_gate_trusted", opensGate: true);
            AssertShopRoute(graph, choice, 2, conditions["trustLevel"], false, "reputation", "shop_gate_denied_trust", opensGate: false);
        }

        [Test]
        public void ReactorControlRoom_UsesRequiredActionSequencesJumpAndFinalThreshold()
        {
            var reactor = Load(DemoShowcaseGraphSpecs.ReactorPath);
            var aftermath = Load(DemoShowcaseGraphSpecs.AftermathPath);
            var choice = reactor.choiceNodes.Single();
            var jump = reactor.graphJumpNodes.Single();
            var finalCondition = reactor.conditionNodes.Single();
            var aftermathCondition = aftermath.conditionNodes.Single();

            Assert.That(finalCondition.variableName, Is.EqualTo("temperature"));
            Assert.That(finalCondition.conditionOperator, Is.EqualTo(ConditionOperator.Less));
            Assert.That(finalCondition.comparisonValue, Is.EqualTo("80"));
            Assert.That(jump.targetGraph.graphAsset, Is.SameAs(aftermath));

            var calmPrefix = Trace(
                reactor,
                reactor.startGuid,
                jump.GetGuid(),
                new Dictionary<string, string>
                {
                    [choice.GetGuid()] = choice.choices[0].PortKey
                });
            AssertSequenceActions(reactor, calmPrefix, "TurnOnTV", "FadeLights");
            AssertSequenceMutations(reactor, calmPrefix, "questState");

            var calmSupport = Trace(
                aftermath,
                aftermath.startGuid,
                aftermath.endGuid,
                new Dictionary<string, string>
                {
                    [aftermathCondition.GetGuid()] = DialogGraphPortKeys.False
                });
            AssertSequenceActions(aftermath, calmSupport);
            AssertSequenceMutations(aftermath, calmSupport, "temperature");

            var emergencyPrefix = Trace(
                reactor,
                reactor.startGuid,
                jump.GetGuid(),
                new Dictionary<string, string>
                {
                    [choice.GetGuid()] = choice.choices[1].PortKey
                });
            AssertSequenceActions(reactor, emergencyPrefix, "TurnOnTV", "PlayAlarm", "StartCountdown");
            AssertSequenceMutations(reactor, emergencyPrefix, "questState");

            var emergencySupport = Trace(
                aftermath,
                aftermath.startGuid,
                aftermath.endGuid,
                new Dictionary<string, string>
                {
                    [aftermathCondition.GetGuid()] = DialogGraphPortKeys.True
                });
            AssertSequenceActions(aftermath, emergencySupport, "OpenGate");
            AssertSequenceMutations(aftermath, emergencySupport, "temperature");

            var success = Trace(
                reactor,
                jump.GetGuid(),
                reactor.endGuid,
                new Dictionary<string, string> { [finalCondition.GetGuid()] = DialogGraphPortKeys.True });
            Assert.That(Outcomes(reactor, success), Is.EqualTo(new[] { "reactor_contained" }));

            var failure = Trace(
                reactor,
                jump.GetGuid(),
                reactor.endGuid,
                new Dictionary<string, string> { [finalCondition.GetGuid()] = DialogGraphPortKeys.False });
            Assert.That(Outcomes(reactor, failure), Is.EqualTo(new[] { "reactor_unstable" }));
        }

        [Test]
        public void ShowcaseAssets_HaveUniqueStableIdentifiers()
        {
            foreach (var graph in DemoShowcaseGraphSpecs.CreateAll().Keys.Select(Load))
            {
                var nodeIds = graph.EnumerateAllNodeGuids().ToList();
                var linkIds = graph.links.Select(link => link.LinkGuid).ToList();
                var choiceIds = graph.choiceNodes.SelectMany(node => node.choices).Select(choice => choice.choiceId).ToList();

                Assert.That(nodeIds, Is.Unique, graph.name + " node ids");
                Assert.That(linkIds, Is.Unique, graph.name + " link ids");
                Assert.That(choiceIds, Is.Unique, graph.name + " choice ids");
                Assert.That(nodeIds, Does.Not.Contain(string.Empty), graph.name);
                Assert.That(linkIds, Does.Not.Contain(string.Empty), graph.name);
            }
        }

        [Test]
        public void LocalizationTables_ContainExactlyTheVisibleShowcaseKeys()
        {
            var expected = DemoShowcaseGraphSpecs.CreateAll().Keys
                .Select(Load)
                .SelectMany(VisibleLocalizationKeys)
                .ToHashSet(StringComparer.Ordinal);
            var source = AssetDatabase.LoadAssetAtPath<DialogLocalizationTable>(
                "Assets/DialogGraphSystem/Definitions/Localization/DialogLocalizationTable.asset");
            var german = AssetDatabase.LoadAssetAtPath<DialogLocalizationTable>(
                "Assets/DialogGraphSystem/Definitions/Localization/DialogLocalizationTable_de_DE.asset");

            Assert.That(source.AllEntries.Keys, Is.EquivalentTo(expected));
            Assert.That(german.AllEntries.Keys, Is.EquivalentTo(expected));
        }

        private static void AssertShopRoute(
            DialogGraph graph,
            ChoiceNode choice,
            int choiceIndex,
            ConditionNode condition,
            bool conditionResult,
            string mutation,
            string outcome,
            bool opensGate)
        {
            var route = Trace(
                graph,
                graph.startGuid,
                graph.endGuid,
                new Dictionary<string, string>
                {
                    [choice.GetGuid()] = choice.choices[choiceIndex].PortKey,
                    [condition.GetGuid()] = conditionResult ? DialogGraphPortKeys.True : DialogGraphPortKeys.False
                });

            Assert.That(Actions(graph, route),
                Is.EqualTo(opensGate ? new[] { "OpenGate" } : Array.Empty<string>()));
            Assert.That(Mutations(graph, route),
                Is.EqualTo(mutation == null ? Array.Empty<string>() : new[] { mutation }));
            Assert.That(Outcomes(graph, route), Is.EqualTo(new[] { outcome }));
        }

        private static void AssertRoute(
            DialogGraph graph,
            IReadOnlyDictionary<string, string> decisions,
            string[] actions,
            string[] mutations,
            string outcome)
        {
            var route = Trace(graph, graph.startGuid, graph.endGuid, decisions);
            Assert.That(Actions(graph, route), Is.EqualTo(actions));
            Assert.That(Mutations(graph, route), Is.EqualTo(mutations));
            Assert.That(Outcomes(graph, route), Is.EqualTo(new[] { outcome }));
        }

        private static List<string> Trace(
            DialogGraph graph,
            string start,
            string end,
            IReadOnlyDictionary<string, string> decisions)
        {
            var route = new List<string> { start };
            var current = start;
            for (var guard = 0; guard < 128 && !string.Equals(current, end, StringComparison.Ordinal); guard++)
            {
                var outgoing = graph.links.Where(link => string.Equals(link.fromGuid, current, StringComparison.Ordinal)).ToList();
                GraphLink selected;
                if (decisions.TryGetValue(current, out var portKey))
                {
                    selected = outgoing.Single(link => string.Equals(link.fromPortKey, portKey, StringComparison.Ordinal));
                }
                else
                {
                    selected = outgoing.Single();
                }

                current = selected.toGuid;
                route.Add(current);
            }

            Assert.That(current, Is.EqualTo(end), graph.name + " route did not reach expected destination");
            return route;
        }

        private static string[] Actions(DialogGraph graph, IReadOnlyCollection<string> route)
        {
            return graph.actionNodes.Where(node => route.Contains(node.GetGuid())).Select(node => node.actionId).ToArray();
        }

        private static string[] Mutations(DialogGraph graph, IReadOnlyCollection<string> route)
        {
            return graph.variableMutationNodes.Where(node => route.Contains(node.GetGuid())).Select(node => node.variableName).ToArray();
        }

        private static string[] Outcomes(DialogGraph graph, IReadOnlyCollection<string> route)
        {
            return graph.outcomeNodes.Where(node => route.Contains(node.GetGuid())).Select(node => node.outcomeId).ToArray();
        }

        private static void AssertSequenceActions(DialogGraph graph, IReadOnlyCollection<string> route, params string[] expected)
        {
            Assert.That(Actions(graph, route), Is.EqualTo(expected));
        }

        private static void AssertSequenceMutations(DialogGraph graph, IReadOnlyCollection<string> route, params string[] expected)
        {
            Assert.That(Mutations(graph, route), Is.EqualTo(expected));
        }

        private static IEnumerable<string> VisibleLocalizationKeys(DialogGraph graph)
        {
            foreach (var node in graph.nodes)
            {
                yield return node.speakerNameLocaleKey;
                yield return node.questionTextLocaleKey;
            }

            foreach (var node in graph.choiceNodes)
            {
                yield return node.textLocaleKey;
                foreach (var choice in node.choices)
                {
                    yield return choice.answerTextLocaleKey;
                }
            }
        }

        private static DialogGraph Load(string path)
        {
            var graph = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
            Assert.That(graph, Is.Not.Null, path);
            return graph;
        }
    }
}
