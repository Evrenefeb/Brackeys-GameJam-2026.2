using System;
using System.IO;
using System.Linq;
using DialogSystem.Runtime.Actions;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Demo;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Variables;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DemoShowcaseSceneTests
    {
        private const string ScenePath = "Assets/DialogGraphSystem/DemoScenes/DialogueDemo.unity";
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Scene_ContainsOneControllerWithThreeEntryGraphsAndRuntimeResetDependencies()
        {
            var controller = FindSingle<DialogDemoShowcaseController>();
            var serialized = new SerializedObject(controller);

            Assert.That(AssetPath(serialized, "productTourGraph"), Is.EqualTo(DemoShowcaseGraphSpecs.ProductPath));
            Assert.That(AssetPath(serialized, "shopGateGraph"), Is.EqualTo(DemoShowcaseGraphSpecs.ShopPath));
            Assert.That(AssetPath(serialized, "reactorControlRoomGraph"), Is.EqualTo(DemoShowcaseGraphSpecs.ReactorPath));
            AssertReference<DialogManager>(serialized, "dialogManager");
            AssertReference<DialogDemoActionShowcase>(serialized, "actionShowcase");
            AssertReference<DialogDemoVariableShowcase>(serialized, "variableShowcase");
            AssertReference<Toggle>(serialized, "startWithKeyToggle");
            AssertReference<Toggle>(serialized, "startTrustedToggle");
        }

        [Test]
        public void Scene_MenuPresentsExactlyThreePolishedDemoCards()
        {
            var menu = FindGameObject("ShowcaseMenu");
            var cards = menu.transform.Find("Content/Cards");
            Assert.That(cards, Is.Not.Null);
            Assert.That(cards.childCount, Is.EqualTo(3));
            Assert.That(cards.Cast<Transform>().Select(child => child.name), Is.EqualTo(new[]
            {
                "01 Product Tour Card",
                "02 Shop Gate Card",
                "03 Reactor Control Room Card"
            }));

            AssertCard(cards.GetChild(0), "Product Tour", "branching", "localization");
            AssertCard(cards.GetChild(1), "Shop Gate", "variables", "conditions");
            AssertCard(cards.GetChild(2), "Reactor Control Room", "actions", "Graph Jump");

            Assert.That(AllObjects().Any(go => go.name is "Dialogue_1" or "Dialogue_2" or "Dialogue_3"), Is.False);
        }

        [Test]
        public void Scene_ProvidesPersistentBackResetAndSettingsControls()
        {
            var controls = FindGameObject("Persistent Controls");
            Assert.That(controls.GetComponentsInChildren<Button>(true).Select(button => button.name),
                Is.EquivalentTo(new[] { "Back to Demo Menu", "Reset Demo", "Settings" }));

            var controlHint = controls.transform.Find("Control Hint")?.GetComponent<RectTransform>();
            Assert.That(controlHint, Is.Not.Null);
            Assert.That(controlHint.pivot.x, Is.EqualTo(0f),
                "The left-anchored footer label must pivot from the left edge so its copy is not clipped off-screen.");

            var controller = FindSingle<DialogDemoShowcaseController>();
            var serialized = new SerializedObject(controller);
            AssertReference<Button>(serialized, "backToMenuButton");
            AssertReference<Button>(serialized, "resetDemoButton");
            AssertReference<Button>(serialized, "showcaseSettingsButton");
            AssertReference<Button>(serialized, "runtimeSettingsButton");
        }

        [Test]
        public void DialogManager_RegistersThreeEntriesAndTheNonSelectableSupportGraph()
        {
            var manager = FindSingle<DialogManager>();
            Assert.That(manager.dialogGraphs, Has.Count.EqualTo(4));
            Assert.That(manager.dialogGraphs.Select(entry => entry.dialogID), Is.EqualTo(new[]
            {
                "Demo_ProductTour",
                "Demo_ShopGate",
                "Demo_ControlRoomActions",
                "Demo_ReactorAftermath"
            }));
            Assert.That(manager.dialogGraphs.Select(entry => AssetDatabase.GetAssetPath(entry.dialogGraph)), Is.EqualTo(new[]
            {
                DemoShowcaseGraphSpecs.ProductPath,
                DemoShowcaseGraphSpecs.ShopPath,
                DemoShowcaseGraphSpecs.ReactorPath,
                DemoShowcaseGraphSpecs.AftermathPath
            }));
        }

        [Test]
        public void ActionRunner_RoutesEveryShowcaseActionThroughTheLiveGlobalHandler()
        {
            var runner = FindSingle<DialogActionRunner>();
            var showcase = FindSingle<DialogDemoActionShowcase>();

            Assert.That(runner.global, Is.Not.Null);
            Assert.That(runner.global.bindings, Is.Empty,
                "The showcase should use one runtime handler rather than stale per-dialogue UnityEvent overrides.");
            Assert.That(runner.global.handlers, Is.EqualTo(new MonoBehaviour[] { showcase }));
            Assert.That(runner.dialogueSets, Is.Empty);

            foreach (var actionId in new[] { "TurnOnTV", "OpenGate", "PlayAlarm", "StartCountdown", "FadeLights" })
            {
                Assert.That(showcase.CanHandle(actionId), Is.True, actionId);
            }
        }

        [Test]
        public void Scene_HasNoAiExtensionOrWorkflowKitDependency()
        {
            var yaml = File.ReadAllText(ScenePath);
            Assert.That(yaml, Does.Not.Contain("AI Extension").IgnoreCase);
            Assert.That(yaml, Does.Not.Contain("WorkflowKit").IgnoreCase);
        }

        private static void AssertCard(Transform card, string title, params string[] concepts)
        {
            var copy = string.Join("\n", card.GetComponentsInChildren<TextMeshProUGUI>(true).Select(label => label.text));
            Assert.That(copy, Does.Contain(title));
            Assert.That(copy, Does.Contain("Demonstrates:"));
            foreach (var concept in concepts)
            {
                Assert.That(copy, Does.Contain(concept).IgnoreCase, card.name);
            }

            Assert.That(card.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(1));
        }

        private static string AssetPath(SerializedObject serialized, string fieldName)
        {
            return AssetDatabase.GetAssetPath(serialized.FindProperty(fieldName).objectReferenceValue);
        }

        private static void AssertReference<T>(SerializedObject serialized, string fieldName) where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            Assert.That(property.objectReferenceValue, Is.InstanceOf<T>(), fieldName);
        }

        private T FindSingle<T>() where T : UnityEngine.Object
        {
            var matches = _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), typeof(T).Name);
            return matches[0];
        }

        private GameObject FindGameObject(string name)
        {
            var matches = AllObjects().Where(go => string.Equals(go.name, name, StringComparison.Ordinal)).ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), name);
            return matches[0];
        }

        private GameObject[] AllObjects()
        {
            return _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();
        }
    }
}
