using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Models;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogManagerRegistryValidationTests
    {
        private static readonly MethodInfo TryResolveDialogGraphByIdMethod =
            typeof(DialogManager).GetMethod("TryResolveDialogGraphByID", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }

            _objects.Clear();
        }

        [Test]
        public void ValidateDialogRegistryReportsEmptyIdsDuplicateIdsAndNullGraphs()
        {
            var graph = CreateGraph("IntroGraph");
            var entries = new List<DialogManager.DialogGraphModel>
            {
                new() { dialogID = string.Empty, dialogGraph = graph },
                new() { dialogID = "intro", dialogGraph = graph },
                new() { dialogID = " intro ", dialogGraph = graph },
                new() { dialogID = "missing_graph", dialogGraph = null }
            };

            var result = DialogManager.ValidateDialogRegistry(entries);
            var messages = result.Issues.Select(issue => issue.Message).ToArray();

            Assert.That(result.IsValid, Is.False);
            Assert.That(messages, Has.Some.Contains("empty Dialog ID"));
            Assert.That(messages, Has.Some.Contains("Dialog ID 'intro' is duplicated"));
            Assert.That(messages, Has.Some.Contains("Dialog ID 'missing_graph' has no graph assigned"));
        }

        [Test]
        public void ValidateDialogRegistryAcceptsUniqueIdsWithGraphs()
        {
            var intro = CreateGraph("IntroGraph");
            var outro = CreateGraph("OutroGraph");
            var entries = new List<DialogManager.DialogGraphModel>
            {
                new() { dialogID = "intro", dialogGraph = intro },
                new() { dialogID = "outro", dialogGraph = outro }
            };

            var result = DialogManager.ValidateDialogRegistry(entries);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Issues, Is.Empty);
        }

        [Test]
        public void PlayDialogByIDFailsClearlyForMissingId()
        {
            var manager = CreateManager();
            AssertResolveByIdFailure(
                manager,
                "missing",
                "[DialogManager] PlayDialogByID failed. No dialog registry entry exists for id 'missing'.");
        }

        [Test]
        public void PlayDialogByIDFailsClearlyForDuplicateId()
        {
            var graph = CreateGraph("IntroGraph");
            var manager = CreateManager();
            manager.dialogGraphs.Add(new DialogManager.DialogGraphModel { dialogID = "intro", dialogGraph = graph });
            manager.dialogGraphs.Add(new DialogManager.DialogGraphModel { dialogID = "intro", dialogGraph = graph });
            AssertResolveByIdFailure(
                manager,
                "intro",
                "[DialogManager] PlayDialogByID failed. Dialog id 'intro' is assigned to 2 registry entries.");
        }

        [Test]
        public void PlayDialogByIDFailsClearlyForNullGraph()
        {
            var manager = CreateManager();
            manager.dialogGraphs.Add(new DialogManager.DialogGraphModel { dialogID = "intro", dialogGraph = null });
            AssertResolveByIdFailure(
                manager,
                "intro",
                "[DialogManager] PlayDialogByID failed. Dialog id 'intro' has no graph assigned.");
        }

        private DialogGraph CreateGraph(string name)
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.name = name;
            _objects.Add(graph);
            return graph;
        }

        private DialogManager CreateManager()
        {
            var gameObject = new GameObject("DialogManagerRegistryValidationTests");
            _objects.Add(gameObject);
            return gameObject.AddComponent<DialogManager>();
        }

        private static void AssertResolveByIdFailure(DialogManager manager, string dialogId, string expectedErrorMessage)
        {
            Assert.That(TryResolveDialogGraphByIdMethod, Is.Not.Null, "Failed to locate DialogManager.TryResolveDialogGraphByID for test validation.");

            var arguments = new object[] { dialogId, null, null };
            var resolved = (bool)TryResolveDialogGraphByIdMethod.Invoke(manager, arguments);

            Assert.That(resolved, Is.False);
            Assert.That(arguments[1], Is.Null);
            Assert.That(arguments[2], Is.EqualTo(expectedErrorMessage));
        }
    }
}
