using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.DialogHistory;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.Variables;
using DialogSystem.Runtime.UI;
using DialogSystem.Runtime.UI.Theming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using UnityEngine.UI;

namespace DialogSystem.Tests.EditMode
{
    public class DialogManagerIntegrationTests
    {
        private DialogManager _manager;
        private GameObject _managerGo;

        private sealed class TestableDialogManager : DialogManager
        {
            public bool ForceDialogAudioPlaying { get; set; }

            protected override bool IsDialogAudioPlaybackActive()
            {
                return ForceDialogAudioPlaying || base.IsDialogAudioPlaybackActive();
            }
        }

        [SetUp]
        public void SetUp()
        {
            _managerGo = new GameObject("DialogManagerTest");
            _manager = _managerGo.AddComponent<TestableDialogManager>();

            // Setup DialogUIManager and a dummy UI controller
            var uiMgr = _managerGo.AddComponent<DialogUIManager>();
            var uiGo = new GameObject("DummyUI");
            uiGo.transform.SetParent(_managerGo.transform);
            var controller = uiGo.AddComponent<DialogUIController>();
            _manager.dialogUIManager = uiMgr;
            _manager.dialogUIController = controller;
            
            // Assign essential fields to avoid NullReferenceException during flow
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(uiGo.transform);
            controller.dialogText = textGo.AddComponent<TextMeshProUGUI>();
            
            var speakerGo = new GameObject("Speaker");
            speakerGo.transform.SetParent(uiGo.transform);
            controller.speakerName = speakerGo.AddComponent<TextMeshProUGUI>();

            var choicesGo = new GameObject("Choices");
            choicesGo.transform.SetParent(uiGo.transform);
            controller.choicesContainer = choicesGo.transform;

            controller.choiceButtonPrefab = CreateChoiceButtonPrefab();
            controller.choiceButtonPrefab.transform.SetParent(_managerGo.transform);
            
            // Link them via reflection
            var field = typeof(DialogUIManager).GetField("_dialogPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(uiMgr, controller);

            var theme = ScriptableObject.CreateInstance<DialogThemeSO>();
            var themeField = typeof(DialogUIManager).GetField("_defaultTheme", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            themeField.SetValue(uiMgr, theme);

            // Runtime lookup fallback is not needed here because the manager is assigned explicitly.
        }

        [TearDown]
        public void TearDown()
        {
            ResetLocalizationRuntime();
            Object.DestroyImmediate(_managerGo);
        }

        [UnityTest]
        public IEnumerator DialogManager_BasicFlow_TraversesStartToEnd()
        {
            var graph = CreateSimpleGraph();
            var lineShownCount = 0;
            var conversationEnded = false;

            _manager.OnLineShown += (speaker, text, id) => lineShownCount++;
            _manager.onDialogExit += () => conversationEnded = true;

            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;

            _manager.StartDialog(graph);
            yield return null; // Let StartDialog initialize

            Assert.That(lineShownCount, Is.EqualTo(1));
            // conversationActive is private, we can infer status from other indicators if needed
            // or just rely on the exit callback

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(conversationEnded, Is.True);
        }

        [UnityTest]
        public IEnumerator DialogManager_BindsResolvedControllerAsRuntimeView()
        {
            var graph = CreateSimpleGraph();

            _manager.StartDialog(graph);
            yield return null;

            Assert.That(_manager.dialogUIController, Is.Not.Null);
            Assert.That(_manager.RuntimeView, Is.SameAs(_manager.dialogUIController));
        }

        [UnityTest]
        public IEnumerator DialogManager_MissingRuntimeViewLogsOneWarning()
        {
            var graph = CreateSimpleGraph();

            Object.DestroyImmediate(_manager.dialogUIManager);
            _manager.dialogUIManager = null;
            _manager.dialogUIController = null;

            LogAssert.Expect(LogType.Warning, new Regex(@"\[DialogManager\].+"));
            _manager.StartDialog(graph);
            yield return null;

            _manager.StartDialog(graph);
            yield return null;

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DialogManager_SkipTypingLocalizedLine_RevealsLocalizedText()
        {
            var graph = CreateSimpleGraph();
            var table = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var settings = ScriptableObject.CreateInstance<DialogLocalizationRuntimeSettings>();

            try
            {
                graph.nodes[0].questionText = "Hello world";
                graph.nodes[0].questionTextLocaleKey = "line.hello";
                table.ConfigureMetadata("fr-FR", true);
                table.SetEntry("line.hello", "Bonjour monde");
                settings.defaultLocaleCode = "fr-FR";
                settings.rememberPlayerChoice = false;
                settings.localizationTables.Add(table);

                ResetLocalizationRuntime();
                DialogLocalizationRuntime.Instance.Initialize(settings);

                _manager.StartDialog(graph);
                _manager.OnDialogAreaClick();
                yield return null;

                Assert.That(_manager.dialogUIController.dialogText.text, Is.EqualTo("Bonjour monde"));
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(table);
            }
        }

        [UnityTest]
        public IEnumerator DialogManager_ActiveLocalizedTypewriter_RefreshesWhenLocaleChanges()
        {
            var graph = CreateSimpleGraph();
            var source = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var target = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var settings = ScriptableObject.CreateInstance<DialogLocalizationRuntimeSettings>();

            try
            {
                graph.nodes[0].questionText = "Hello world";
                graph.nodes[0].questionTextLocaleKey = "line.hello";
                graph.nodes[0].speakerName = "Narrator";
                graph.nodes[0].speakerNameLocaleKey = "speaker.narrator";

                source.ConfigureMetadata("en-US", true);
                source.SetEntry("line.hello", "Hello world");
                source.SetEntry("speaker.narrator", "Narrator");
                target.ConfigureMetadata("fr-FR", false);
                target.SetEntry("line.hello", "Bonjour monde");
                target.SetEntry("speaker.narrator", "Narrateur");

                settings.defaultLocaleCode = "en-US";
                settings.rememberPlayerChoice = false;
                settings.localizationTables.Add(source);
                settings.localizationTables.Add(target);

                _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.Typing;
                _manager.RuntimeTextSettings.charsPerSecond = 1f;

                DialogLocalizationRuntime.Instance.Initialize(settings);
                _manager.StartDialog(graph);

                Assert.That(_manager.dialogUIController.speakerName.text, Is.EqualTo("Narrator"));

                Assert.That(DialogLocalizationRuntime.Instance.SetActiveLocale("fr-FR"), Is.True);
                yield return null;
                yield return null;

                _manager.OnDialogAreaClick();

                Assert.That(_manager.dialogUIController.speakerName.text, Is.EqualTo("Narrateur"));
                Assert.That(_manager.dialogUIController.dialogText.text, Is.EqualTo("Bonjour monde"));
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
            }
        }

        [UnityTest]
        public IEnumerator DialogueHistory_LocalizedLine_ReplacesGenericEntryAndReResolves()
        {
            var graph = CreateSimpleGraph();
            var source = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var target = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var settings = ScriptableObject.CreateInstance<DialogLocalizationRuntimeSettings>();

            try
            {
                graph.nodes[0].questionText = "Hello world";
                graph.nodes[0].questionTextLocaleKey = "line.hello";
                graph.nodes[0].speakerName = "Narrator";
                graph.nodes[0].speakerNameLocaleKey = "speaker.narrator";

                source.ConfigureMetadata("en-US", true);
                source.SetEntry("line.hello", "Hello world");
                source.SetEntry("speaker.narrator", "Narrator");
                target.ConfigureMetadata("fr-FR", false);
                target.SetEntry("line.hello", "Bonjour monde");
                target.SetEntry("speaker.narrator", "Narrateur");

                settings.defaultLocaleCode = "en-US";
                settings.rememberPlayerChoice = false;
                settings.localizationTables.Add(source);
                settings.localizationTables.Add(target);

                var history = _managerGo.AddComponent<DialogueHistory>();
                history.Initialize(_manager, null);

                DialogLocalizationRuntime.Instance.Initialize(settings);
                _manager.StartDialog(graph);
                yield return null;
                yield return null;

                Assert.That(history.Entries.Count, Is.EqualTo(1));
                Assert.That(history.Entries[0].speaker, Is.EqualTo("Narrator"));
                Assert.That(history.Entries[0].text, Is.EqualTo("Hello world"));

                Assert.That(DialogLocalizationRuntime.Instance.SetActiveLocale("fr-FR"), Is.True);
                yield return null;
                yield return null;

                Assert.That(history.Entries.Count, Is.EqualTo(1));
                Assert.That(history.Entries[0].speaker, Is.EqualTo("Narrateur"));
                Assert.That(history.Entries[0].text, Is.EqualTo("Bonjour monde"));
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(source);
            }
        }

        [UnityTest]
        public IEnumerator DialogManager_ChoicePickedLocalized_ReportsResolvedAnswer()
        {
            var graph = CreateChoiceGraph();
            var table = ScriptableObject.CreateInstance<DialogLocalizationTable>();
            var settings = ScriptableObject.CreateInstance<DialogLocalizationRuntimeSettings>();
            string rawChoice = null;
            string localizedChoice = null;
            string localizedKey = null;
            string localizedRaw = null;

            try
            {
                graph.choiceNodes[0].choices[0].answerTextLocaleKey = "choice.a";
                table.ConfigureMetadata("fr-FR", true);
                table.SetEntry("choice.a", "Choix A");
                settings.defaultLocaleCode = "fr-FR";
                settings.rememberPlayerChoice = false;
                settings.localizationTables.Add(table);

                DialogLocalizationRuntime.Instance.Initialize(settings);
                _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;

                _manager.OnChoicePicked += (_, text) => rawChoice = text;
                _manager.OnChoicePickedLocalized += (_, text, key, rawText) =>
                {
                    localizedChoice = text;
                    localizedKey = key;
                    localizedRaw = rawText;
                };

                _manager.StartDialog(graph);
                _manager.OnChoiceSelected(0);
                yield return null;

                Assert.That(rawChoice, Is.EqualTo("A"));
                Assert.That(localizedChoice, Is.EqualTo("Choix A"));
                Assert.That(localizedKey, Is.EqualTo("choice.a"));
                Assert.That(localizedRaw, Is.EqualTo("A"));
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(table);
            }
        }

        [UnityTest]
        public IEnumerator DialogManager_ChoiceSelection_NavigatesExpectedBranch()
        {
            var graph = CreateChoiceGraph();
            var lastLine = "";
            _manager.OnLineShown += (_, _, text) => lastLine = text;
            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;

            _manager.StartDialog(graph);
            yield return null;

            Assert.That(lastLine, Is.EqualTo("Pick one:"));

            _manager.OnChoiceSelected(0); // choice-a
            yield return null;

            Assert.That(lastLine, Is.EqualTo("You picked A."));
        }

        [UnityTest]
        public IEnumerator DialogManager_ChoiceSelection_UsesLegacyNextNodeGuidFallback()
        {
            var graph = CreateChoiceGraph();
            var lastLine = "";

            graph.links.RemoveAll(link => link != null && link.fromGuid == "c1");
            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;
            _manager.OnLineShown += (_, _, text) => lastLine = text;

            _manager.StartDialog(graph);
            yield return null;

            _manager.OnChoiceSelected(0);
            yield return null;

            Assert.That(lastLine, Is.EqualTo("You picked A."));
        }

        [UnityTest]
        public IEnumerator DialogManager_ChoiceSelection_MissingTargetWarnsAndEndsGracefully()
        {
            var graph = CreateChoiceGraph();
            var exitCount = 0;

            graph.links.RemoveAll(link => link != null && link.fromGuid == "c1");
            graph.choiceNodes[0].choices[0].nextNodeGUID = string.Empty;
            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;
            _manager.onDialogExit += () => exitCount++;

            _manager.StartDialog(graph);
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex(@".*has no valid target.*"));
            _manager.OnChoiceSelected(0);
            yield return null;

            Assert.That(exitCount, Is.EqualTo(1));
            Assert.That(_manager.RuntimeState, Is.EqualTo(DialogueRuntimeState.Idle));
        }

        [UnityTest]
        public IEnumerator DialogManager_ConditionTrueAndFalseBranches_UseCorrectPortTargets()
        {
            var graph = CreateConditionBranchGraph();
            var store = _managerGo.AddComponent<DialogueVariableStore>();
            var shownLines = new List<string>();

            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;
            _manager.OnLineShown += (_, _, text) => shownLines.Add(text);

            store.SetBool("flag", true);
            _manager.StartDialog(graph);
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "True branch" }));

            _manager.StopImmediately();
            yield return null;
            shownLines.Clear();

            store.SetBool("flag", false);
            _manager.StartDialog(graph);
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "False branch" }));
        }

        [UnityTest]
        public IEnumerator DialogManager_ConditionMissingSelectedBranch_WarnsOnceAndEndsGracefully()
        {
            var graph = CreateConditionBranchGraph(includeFalseBranch: false);
            var store = _managerGo.AddComponent<DialogueVariableStore>();
            var exitCount = 0;

            store.SetBool("flag", false);
            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;
            _manager.onDialogExit += () => exitCount++;

            LogAssert.Expect(LogType.Warning, new Regex(@".*selected the False branch, but that branch has no target.*"));
            _manager.StartDialog(graph);
            yield return null;

            Assert.That(exitCount, Is.EqualTo(1));
            Assert.That(_manager.RuntimeState, Is.EqualTo(DialogueRuntimeState.Idle));
        }

        [UnityTest]
        public IEnumerator DialogManager_HiddenFlowCycle_WarnsAndEndsGracefully()
        {
            var graph = CreateHiddenFlowCycleGraph();
            var store = _managerGo.AddComponent<DialogueVariableStore>();
            var exitCount = 0;

            store.SetBool("flag", false);
            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;
            _manager.onDialogExit += () => exitCount++;

            LogAssert.Expect(LogType.Warning, new Regex(@".*Hidden-flow cycle detected.*"));
            _manager.StartDialog(graph);
            yield return null;

            Assert.That(exitCount, Is.EqualTo(1));
            Assert.That(_manager.RuntimeState, Is.EqualTo(DialogueRuntimeState.Idle));
        }

        [UnityTest]
        public IEnumerator DialogManager_RuntimeState_ReflectsWaitingForContinueAfterLineReveal()
        {
            var graph = CreateSimpleGraph();
            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;

            _manager.StartDialog(graph);
            yield return null;

            Assert.That(_manager.RuntimeState, Is.EqualTo(DialogueRuntimeState.WaitingForContinue));
        }

        [UnityTest]
        public IEnumerator DialogManager_OnDialogAreaClick_InterruptsBlockingVoiceAudioAndAdvances()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.startGuid = "Start";
            graph.endGuid = "End";
            graph.startInitialized = true;
            graph.endInitialized = true;

            var first = ScriptableObject.CreateInstance<DialogNode>();
            first.SetGuid("d1");
            first.questionText = "First line";
            first.dialogAudio = AudioClip.Create("voice", 128, 1, 44100, false);
            graph.nodes.Add(first);

            var second = ScriptableObject.CreateInstance<DialogNode>();
            second.SetGuid("d2");
            second.questionText = "Second line";
            graph.nodes.Add(second);

            graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "d1", fromPortIndex = 0 });
            graph.links.Add(new GraphLink { fromGuid = "d1", toGuid = "d2", fromPortIndex = 0 });
            graph.links.Add(new GraphLink { fromGuid = "d2", toGuid = "End", fromPortIndex = 0 });

            var shownLines = new List<string>();
            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;
            _manager.OnLineShown += (_, _, text) => shownLines.Add(text);

            _manager.StartDialog(graph);
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "First line" }));

            ((TestableDialogManager)_manager).ForceDialogAudioPlaying = true;
            _manager.OnDialogAreaClick();
            ((TestableDialogManager)_manager).ForceDialogAudioPlaying = false;
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "First line", "Second line" }));
            Assert.That(_manager.GetCurrentGuid(), Is.EqualTo("d2"));
        }

        [UnityTest]
        public IEnumerator DialogManager_VariableMutation_UpdatesStore()
        {
            var graph = CreateVariableMutationGraph();
            var varStore = _managerGo.AddComponent<DialogueVariableStore>();
            
            // Register variable
            var varDef = ScriptableObject.CreateInstance<DialogVariableSO>();
            varDef.SetKey("testVar");
            varDef.SetDefaultValue(0);
            varStore.SetInt("testVar", 0);

            _manager.StartDialog(graph);
            yield return null; // Set Variable node
            yield return null; // End node

            Assert.That(varStore.GetInt("testVar"), Is.EqualTo(10));
            
            Object.DestroyImmediate(varDef);
        }

        [UnityTest]
        public IEnumerator DialogManager_GraphJump_ReturnsToCaller_AndEndsOnce()
        {
            var graphs = CreateGraphJumpScenario();
            var shownLines = new List<string>();
            var exitCount = 0;
            var completionCount = 0;

            _manager.OnLineShown += (_, speaker, text) => shownLines.Add(text);
            _manager.onDialogExit += () => exitCount++;

            _manager.StartDialog(graphs.caller, () => completionCount++);
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1" }));

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1", "Callee line" }));
            Assert.That(exitCount, Is.EqualTo(0));
            Assert.That(completionCount, Is.EqualTo(0));

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1", "Callee line", "Caller line 2" }));
            Assert.That(exitCount, Is.EqualTo(0));
            Assert.That(completionCount, Is.EqualTo(0));

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(exitCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.EqualTo(1));

            Object.DestroyImmediate(graphs.caller);
            Object.DestroyImmediate(graphs.callee);
            Object.DestroyImmediate(graphs.callerIntro);
            Object.DestroyImmediate(graphs.callerOutro);
            Object.DestroyImmediate(graphs.jumpNode);
            Object.DestroyImmediate(graphs.calleeLine);
        }

        [UnityTest]
        public IEnumerator DialogManager_GraphJump_AutoRegistersDirectAssetTarget_AndPlaysIt()
        {
            var graphs = CreateGraphJumpScenario();
            var shownLines = new List<string>();

            _manager.OnLineShown += (_, speaker, text) => shownLines.Add(text);

            Assert.That(_manager.dialogGraphs.Count, Is.EqualTo(0));

            _manager.StartDialog(graphs.caller);
            yield return null;

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(_manager.dialogGraphs.Any(entry => entry != null && entry.dialogGraph == graphs.callee), Is.True);
            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1", "Callee line" }));

            Object.DestroyImmediate(graphs.caller);
            Object.DestroyImmediate(graphs.callee);
            Object.DestroyImmediate(graphs.callerIntro);
            Object.DestroyImmediate(graphs.callerOutro);
            Object.DestroyImmediate(graphs.jumpNode);
            Object.DestroyImmediate(graphs.calleeLine);
        }

        [UnityTest]
        public IEnumerator DialogManager_GraphJump_ResolvesTargetByRuntimeDialogId()
        {
            var graphs = CreateGraphJumpScenario();
            var shownLines = new List<string>();

            graphs.jumpNode.targetGraph.graphAsset = null;
            graphs.jumpNode.targetGraph.graphGuid = string.Empty;
            graphs.jumpNode.targetGraph.graphName = string.Empty;
            graphs.jumpNode.targetGraph.assetPath = string.Empty;
            graphs.jumpNode.targetGraph.runtimeDialogId = "callee-runtime-id";

            _manager.dialogGraphs.Add(new DialogManager.DialogGraphModel
            {
                dialogGraph = graphs.callee,
                dialogID = "callee-runtime-id"
            });

            _manager.OnLineShown += (_, _, text) => shownLines.Add(text);

            _manager.StartDialog(graphs.caller);
            yield return null;

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1", "Callee line" }));

            Object.DestroyImmediate(graphs.caller);
            Object.DestroyImmediate(graphs.callee);
            Object.DestroyImmediate(graphs.callerIntro);
            Object.DestroyImmediate(graphs.callerOutro);
            Object.DestroyImmediate(graphs.jumpNode);
            Object.DestroyImmediate(graphs.calleeLine);
        }

        [UnityTest]
        public IEnumerator DialogManager_GraphJump_ResolvesTargetByGraphGuid()
        {
            var graphs = CreateGraphJumpScenario();
            var shownLines = new List<string>();

            graphs.jumpNode.targetGraph.graphAsset = null;
            graphs.jumpNode.targetGraph.runtimeDialogId = string.Empty;
            graphs.jumpNode.targetGraph.graphName = string.Empty;
            graphs.jumpNode.targetGraph.assetPath = string.Empty;
            graphs.jumpNode.targetGraph.graphGuid = graphs.callee.GraphGuid;

            _manager.dialogGraphs.Add(new DialogManager.DialogGraphModel
            {
                dialogGraph = graphs.callee,
                dialogID = "callee-registry-id"
            });

            _manager.OnLineShown += (_, _, text) => shownLines.Add(text);

            _manager.StartDialog(graphs.caller);
            yield return null;

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1", "Callee line" }));

            Object.DestroyImmediate(graphs.caller);
            Object.DestroyImmediate(graphs.callee);
            Object.DestroyImmediate(graphs.callerIntro);
            Object.DestroyImmediate(graphs.callerOutro);
            Object.DestroyImmediate(graphs.jumpNode);
            Object.DestroyImmediate(graphs.calleeLine);
        }

        [UnityTest]
        public IEnumerator DialogManager_GraphJump_CycleGuard_BlocksRecursiveReentry()
        {
            var graphs = CreateRecursiveGraphJumpScenario();
            var shownLines = new List<string>();

            _manager.OnLineShown += (_, _, text) => shownLines.Add(text);

            _manager.StartDialog(graphs.caller);
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1" }));

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1", "Callee line" }));

            LogAssert.Expect(LogType.Warning, new Regex(@".*would re-enter graph.*"));
            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1", "Callee line", "Caller line 2" }));

            Object.DestroyImmediate(graphs.caller);
            Object.DestroyImmediate(graphs.callee);
            Object.DestroyImmediate(graphs.callerIntro);
            Object.DestroyImmediate(graphs.callerOutro);
            Object.DestroyImmediate(graphs.jumpNode);
            Object.DestroyImmediate(graphs.calleeLine);
            Object.DestroyImmediate(graphs.calleeJumpBack);
        }

        [UnityTest]
        public IEnumerator DialogManager_GraphJump_MaxDepthGuard_BlocksExcessiveNesting()
        {
            var graphs = CreateNestedGraphJumpScenario();
            var shownLines = new List<string>();

            _manager.MaxGraphJumpDepth = 1;
            _manager.OnLineShown += (_, _, text) => shownLines.Add(text);

            _manager.StartDialog(graphs.caller);
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1" }));

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1", "Mid line" }));

            LogAssert.Expect(LogType.Warning, new Regex(@".*exceeded the maximum nested jump depth.*"));
            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(shownLines, Is.EqualTo(new[] { "Caller line 1", "Mid line", "Caller line 2" }));

            Object.DestroyImmediate(graphs.caller);
            Object.DestroyImmediate(graphs.mid);
            Object.DestroyImmediate(graphs.deep);
            Object.DestroyImmediate(graphs.callerIntro);
            Object.DestroyImmediate(graphs.callerOutro);
            Object.DestroyImmediate(graphs.jumpToMid);
            Object.DestroyImmediate(graphs.midLine);
            Object.DestroyImmediate(graphs.jumpToDeep);
            Object.DestroyImmediate(graphs.deepLine);
        }

        [UnityTest]
        public IEnumerator DialogManager_OutcomeNode_PublishesEndResult()
        {
            var graph = CreateOutcomeGraph();
            DialogueEndResult receivedResult = default;
            var receivedCount = 0;
            var completionCount = 0;

            _manager.OnDialogEndedWithResult += result =>
            {
                receivedResult = result;
                receivedCount++;
                Assert.That(_manager.LastEndResult.HasOutcome, Is.True);
                Assert.That(_manager.LastEndResult.OutcomeId, Is.EqualTo("good_ending"));
            };

            _manager.RuntimeTextSettings.typewriterEffect = TypewriterEffect.None;

            _manager.StartDialog(graph, () => completionCount++);
            yield return null;

            Assert.That(_manager.RuntimeState, Is.EqualTo(DialogueRuntimeState.WaitingForContinue));

            _manager.OnDialogAreaClick();
            yield return null;

            Assert.That(receivedCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(receivedResult.HasOutcome, Is.True);
            Assert.That(receivedResult.ConversationId, Is.Null.Or.Empty);
            Assert.That(receivedResult.OutcomeId, Is.EqualTo("good_ending"));
            Assert.That(receivedResult.OutcomeDisplayName, Is.EqualTo("Good Ending"));
            Assert.That(receivedResult.OutcomeDescription, Is.EqualTo("Reached the best path."));
            Assert.That(_manager.LastEndResult.HasOutcome, Is.False);
            Assert.That(_manager.GetLastOutcomeDescription(), Is.EqualTo("Reached the best path."));
            Assert.That(_manager.TryGetLastCompletedOutcomeNode(out var outcomeNode), Is.True);
            Assert.That(outcomeNode, Is.Not.Null);
            Assert.That(outcomeNode.outcomeId, Is.EqualTo("good_ending"));
            Assert.That(outcomeNode.description, Is.EqualTo("Reached the best path."));
            Assert.That(_manager.TryGetOutcomeNode("good_ending", out var resolvedOutcomeNode), Is.True);
            Assert.That(resolvedOutcomeNode, Is.SameAs(outcomeNode));

            Object.DestroyImmediate(graph);
        }

        private DialogGraph CreateActionGraph()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.startGuid = "Start";
            graph.endGuid = "End";
            graph.startInitialized = true;
            graph.endInitialized = true;

            var actionNode = ScriptableObject.CreateInstance<ActionNode>();
            actionNode.SetGuid("a1");
            actionNode.actionId = "testAction";
            actionNode.payloadJson = "hello-payload";
            graph.actionNodes.Add(actionNode);

            graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "a1", fromPortIndex = 0 });
            graph.links.Add(new GraphLink { fromGuid = "a1", toGuid = "End", fromPortIndex = 0 });

            return graph;
        }

        private DialogGraph CreateSimpleGraph()
{
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.startGuid = "Start";
            graph.endGuid = "End";
            graph.startInitialized = true;
            graph.endInitialized = true;

            var node = ScriptableObject.CreateInstance<DialogNode>();
            node.SetGuid("d1");
            node.questionText = "Hello world";
            graph.nodes.Add(node);

            graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "d1", fromPortIndex = 0 });
            graph.links.Add(new GraphLink { fromGuid = "d1", toGuid = "End", fromPortIndex = 0 });

            return graph;
        }

        private DialogGraph CreateOutcomeGraph()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.startGuid = "Start";
            graph.endGuid = "End";
            graph.startInitialized = true;
            graph.endInitialized = true;

            var node = ScriptableObject.CreateInstance<DialogNode>();
            node.SetGuid("intro");
            node.questionText = "The story ends here.";
            graph.nodes.Add(node);

            var outcome = ScriptableObject.CreateInstance<OutcomeNode>();
            outcome.SetGuid("outcome");
            outcome.outcomeId = "good_ending";
            outcome.displayName = "Good Ending";
            outcome.description = "Reached the best path.";
            graph.outcomeNodes.Add(outcome);

            graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "intro", fromPortIndex = 0 });
            graph.links.Add(new GraphLink { fromGuid = "intro", toGuid = "outcome", fromPortIndex = 0 });
            graph.links.Add(new GraphLink { fromGuid = "outcome", toGuid = "End", fromPortIndex = 0, fromPortKey = DialogGraphPortKeys.Default, toPortKey = DialogGraphPortKeys.Default });

            return graph;
        }

        private static void ResetLocalizationRuntime()
        {
            typeof(DialogLocalizationRuntime)
                .GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, null);
        }

        private static GameObject CreateChoiceButtonPrefab()
        {
            var prefab = new GameObject("ChoiceButtonPrefab");
            prefab.SetActive(false);
            prefab.AddComponent<RectTransform>();
            var button = prefab.AddComponent<Button>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(prefab.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();

            var hotkeyGo = new GameObject("Hotkey");
            hotkeyGo.transform.SetParent(prefab.transform);
            var hotkeyLabel = hotkeyGo.AddComponent<TextMeshProUGUI>();

            var view = prefab.AddComponent<ChoiceButtonView>();
            SetPrivateField(view, "_button", button);
            SetPrivateField(view, "_choiceText", label);
            SetPrivateField(view, "_hotkeyHolder", hotkeyGo);
            SetPrivateField(view, "_hotkeyText", hotkeyLabel);
            prefab.SetActive(true);
            return prefab;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(target, value);
        }

        private DialogGraph CreateChoiceGraph()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.startGuid = "Start";
            graph.endGuid = "End";
            graph.startInitialized = true;
            graph.endInitialized = true;

            var choiceNode = ScriptableObject.CreateInstance<ChoiceNode>();
            choiceNode.SetGuid("c1");
            choiceNode.text = "Pick one:";
            choiceNode.choices.Add(new Choice { choiceId = "choice-a", answerText = "A", nextNodeGUID = "d_a" });
            choiceNode.choices.Add(new Choice { choiceId = "choice-b", answerText = "B", nextNodeGUID = "d_b" });
            graph.choiceNodes.Add(choiceNode);

            var nodeA = ScriptableObject.CreateInstance<DialogNode>();
            nodeA.SetGuid("d_a");
            nodeA.questionText = "You picked A.";
            graph.nodes.Add(nodeA);

            var nodeB = ScriptableObject.CreateInstance<DialogNode>();
            nodeB.SetGuid("d_b");
            nodeB.questionText = "You picked B.";
            graph.nodes.Add(nodeB);

            graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "c1", fromPortIndex = 0 });
            
            var linkA = new GraphLink { fromGuid = "c1", toGuid = "d_a", fromPortIndex = 0 };
            linkA.fromPortKey = DialogGraphPortKeys.ForChoiceId("choice-a");
            graph.links.Add(linkA);

            var linkB = new GraphLink { fromGuid = "c1", toGuid = "d_b", fromPortIndex = 1 };
            linkB.fromPortKey = DialogGraphPortKeys.ForChoiceId("choice-b");
            graph.links.Add(linkB);

            return graph;
        }

        private DialogGraph CreateConditionBranchGraph(bool includeFalseBranch = true)
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.startGuid = "Start";
            graph.endGuid = "End";
            graph.startInitialized = true;
            graph.endInitialized = true;

            var condition = ScriptableObject.CreateInstance<ConditionNode>();
            condition.SetGuid("condition");
            condition.variableName = "flag";
            condition.valueType = DialogueVariableValueType.Boolean;
            condition.conditionOperator = ConditionOperator.IsTrue;
            graph.conditionNodes.Add(condition);

            var trueNode = ScriptableObject.CreateInstance<DialogNode>();
            trueNode.SetGuid("true-line");
            trueNode.questionText = "True branch";
            graph.nodes.Add(trueNode);

            var falseNode = ScriptableObject.CreateInstance<DialogNode>();
            falseNode.SetGuid("false-line");
            falseNode.questionText = "False branch";
            graph.nodes.Add(falseNode);

            graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "condition", fromPortIndex = 0 });
            graph.links.Add(new GraphLink
            {
                fromGuid = "condition",
                toGuid = "true-line",
                fromPortKey = DialogGraphPortKeys.True,
                fromPortIndex = ConditionNode.TruePortIndex
            });

            if (includeFalseBranch)
            {
                graph.links.Add(new GraphLink
                {
                    fromGuid = "condition",
                    toGuid = "false-line",
                    fromPortKey = DialogGraphPortKeys.False,
                    fromPortIndex = ConditionNode.FalsePortIndex
                });
            }

            graph.links.Add(new GraphLink { fromGuid = "true-line", toGuid = "End", fromPortIndex = 0 });
            graph.links.Add(new GraphLink { fromGuid = "false-line", toGuid = "End", fromPortIndex = 0 });

            return graph;
        }

        private DialogGraph CreateHiddenFlowCycleGraph()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.startGuid = "Start";
            graph.endGuid = "End";
            graph.startInitialized = true;
            graph.endInitialized = true;

            var condition = ScriptableObject.CreateInstance<ConditionNode>();
            condition.SetGuid("condition");
            condition.variableName = "flag";
            condition.valueType = DialogueVariableValueType.Boolean;
            condition.conditionOperator = ConditionOperator.IsTrue;
            graph.conditionNodes.Add(condition);

            var mutation = ScriptableObject.CreateInstance<VariableMutationNode>();
            mutation.SetGuid("mutation");
            mutation.variableName = "flag";
            mutation.valueType = DialogueVariableValueType.Boolean;
            mutation.operation = VariableMutationOperation.Set;
            mutation.value = "false";
            graph.variableMutationNodes.Add(mutation);

            graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "condition", fromPortIndex = 0 });
            graph.links.Add(new GraphLink
            {
                fromGuid = "condition",
                toGuid = "End",
                fromPortKey = DialogGraphPortKeys.True,
                fromPortIndex = ConditionNode.TruePortIndex
            });
            graph.links.Add(new GraphLink
            {
                fromGuid = "condition",
                toGuid = "mutation",
                fromPortKey = DialogGraphPortKeys.False,
                fromPortIndex = ConditionNode.FalsePortIndex
            });
            graph.links.Add(new GraphLink { fromGuid = "mutation", toGuid = "condition", fromPortIndex = 0 });

            return graph;
        }

        private DialogGraph CreateVariableMutationGraph()
        {
            var graph = ScriptableObject.CreateInstance<DialogGraph>();
            graph.startGuid = "Start";
            graph.endGuid = "End";

            var mutateNode = ScriptableObject.CreateInstance<VariableMutationNode>();
            mutateNode.SetGuid("m1");
            mutateNode.variableName = "testVar";
            mutateNode.operation = VariableMutationOperation.Set;
            mutateNode.valueType = DialogueVariableValueType.Integer;
            mutateNode.value = "10";
            graph.variableMutationNodes.Add(mutateNode);

            graph.links.Add(new GraphLink { fromGuid = "Start", toGuid = "m1", fromPortIndex = 0 });
            graph.links.Add(new GraphLink { fromGuid = "m1", toGuid = "End", fromPortIndex = 0 });

            return graph;
        }

        private (DialogGraph caller, DialogGraph callee, DialogNode callerIntro, DialogNode callerOutro, GraphJumpNode jumpNode, DialogNode calleeLine) CreateGraphJumpScenario()
        {
            var caller = ScriptableObject.CreateInstance<DialogGraph>();
            caller.startGuid = "caller-start";
            caller.endGuid = "caller-end";
            caller.startInitialized = true;
            caller.endInitialized = true;
            caller.SetGraphGuidForMigration("graph-caller");

            var callee = ScriptableObject.CreateInstance<DialogGraph>();
            callee.startGuid = "callee-start";
            callee.endGuid = "callee-end";
            callee.startInitialized = true;
            callee.endInitialized = true;
            callee.SetGraphGuidForMigration("graph-callee");

            var callerIntro = ScriptableObject.CreateInstance<DialogNode>();
            callerIntro.SetGuid("caller-line-1");
            callerIntro.questionText = "Caller line 1";
            caller.nodes.Add(callerIntro);

            var callerOutro = ScriptableObject.CreateInstance<DialogNode>();
            callerOutro.SetGuid("caller-line-2");
            callerOutro.questionText = "Caller line 2";
            caller.nodes.Add(callerOutro);

            var jumpNode = ScriptableObject.CreateInstance<GraphJumpNode>();
            jumpNode.SetGuid("jump-node");
            jumpNode.targetGraph.graphAsset = callee;
            jumpNode.targetGraph.graphGuid = callee.GraphGuid;
            jumpNode.targetGraph.graphName = callee.name;
            caller.graphJumpNodes.Add(jumpNode);

            var calleeLine = ScriptableObject.CreateInstance<DialogNode>();
            calleeLine.SetGuid("callee-line");
            calleeLine.questionText = "Callee line";
            callee.nodes.Add(calleeLine);

            caller.links.Add(new GraphLink { fromGuid = caller.startGuid, toGuid = callerIntro.GetGuid(), fromPortIndex = 0 });
            caller.links.Add(new GraphLink { fromGuid = callerIntro.GetGuid(), toGuid = jumpNode.GetGuid(), fromPortIndex = 0 });
            caller.links.Add(new GraphLink { fromGuid = jumpNode.GetGuid(), toGuid = callerOutro.GetGuid(), fromPortIndex = 0 });
            caller.links.Add(new GraphLink { fromGuid = callerOutro.GetGuid(), toGuid = caller.endGuid, fromPortIndex = 0 });

            callee.links.Add(new GraphLink { fromGuid = callee.startGuid, toGuid = calleeLine.GetGuid(), fromPortIndex = 0 });
            callee.links.Add(new GraphLink { fromGuid = calleeLine.GetGuid(), toGuid = callee.endGuid, fromPortIndex = 0 });

            return (caller, callee, callerIntro, callerOutro, jumpNode, calleeLine);
        }

        private (DialogGraph caller, DialogGraph callee, DialogNode callerIntro, DialogNode callerOutro, GraphJumpNode jumpNode, DialogNode calleeLine, GraphJumpNode calleeJumpBack) CreateRecursiveGraphJumpScenario()
        {
            var graphs = CreateGraphJumpScenario();

            var calleeJumpBack = ScriptableObject.CreateInstance<GraphJumpNode>();
            calleeJumpBack.SetGuid("callee-jump-back");
            calleeJumpBack.targetGraph.graphAsset = graphs.caller;
            calleeJumpBack.targetGraph.graphGuid = graphs.caller.GraphGuid;
            calleeJumpBack.targetGraph.graphName = graphs.caller.name;
            graphs.callee.graphJumpNodes.Add(calleeJumpBack);

            graphs.callee.links.Clear();
            graphs.callee.links.Add(new GraphLink { fromGuid = graphs.callee.startGuid, toGuid = graphs.calleeLine.GetGuid(), fromPortIndex = 0 });
            graphs.callee.links.Add(new GraphLink { fromGuid = graphs.calleeLine.GetGuid(), toGuid = calleeJumpBack.GetGuid(), fromPortIndex = 0 });
            graphs.callee.links.Add(new GraphLink { fromGuid = calleeJumpBack.GetGuid(), toGuid = graphs.callee.endGuid, fromPortIndex = 0 });

            return (graphs.caller, graphs.callee, graphs.callerIntro, graphs.callerOutro, graphs.jumpNode, graphs.calleeLine, calleeJumpBack);
        }

        private (DialogGraph caller, DialogGraph mid, DialogGraph deep, DialogNode callerIntro, DialogNode callerOutro, GraphJumpNode jumpToMid, DialogNode midLine, GraphJumpNode jumpToDeep, DialogNode deepLine) CreateNestedGraphJumpScenario()
        {
            var caller = ScriptableObject.CreateInstance<DialogGraph>();
            caller.startGuid = "caller-start";
            caller.endGuid = "caller-end";
            caller.startInitialized = true;
            caller.endInitialized = true;
            caller.SetGraphGuidForMigration("graph-caller");

            var mid = ScriptableObject.CreateInstance<DialogGraph>();
            mid.startGuid = "mid-start";
            mid.endGuid = "mid-end";
            mid.startInitialized = true;
            mid.endInitialized = true;
            mid.SetGraphGuidForMigration("graph-mid");

            var deep = ScriptableObject.CreateInstance<DialogGraph>();
            deep.startGuid = "deep-start";
            deep.endGuid = "deep-end";
            deep.startInitialized = true;
            deep.endInitialized = true;
            deep.SetGraphGuidForMigration("graph-deep");

            var callerIntro = ScriptableObject.CreateInstance<DialogNode>();
            callerIntro.SetGuid("caller-line-1");
            callerIntro.questionText = "Caller line 1";
            caller.nodes.Add(callerIntro);

            var callerOutro = ScriptableObject.CreateInstance<DialogNode>();
            callerOutro.SetGuid("caller-line-2");
            callerOutro.questionText = "Caller line 2";
            caller.nodes.Add(callerOutro);

            var jumpToMid = ScriptableObject.CreateInstance<GraphJumpNode>();
            jumpToMid.SetGuid("jump-to-mid");
            jumpToMid.targetGraph.graphAsset = mid;
            jumpToMid.targetGraph.graphGuid = mid.GraphGuid;
            jumpToMid.targetGraph.graphName = mid.name;
            caller.graphJumpNodes.Add(jumpToMid);

            var midLine = ScriptableObject.CreateInstance<DialogNode>();
            midLine.SetGuid("mid-line");
            midLine.questionText = "Mid line";
            mid.nodes.Add(midLine);

            var jumpToDeep = ScriptableObject.CreateInstance<GraphJumpNode>();
            jumpToDeep.SetGuid("jump-to-deep");
            jumpToDeep.targetGraph.graphAsset = deep;
            jumpToDeep.targetGraph.graphGuid = deep.GraphGuid;
            jumpToDeep.targetGraph.graphName = deep.name;
            mid.graphJumpNodes.Add(jumpToDeep);

            var deepLine = ScriptableObject.CreateInstance<DialogNode>();
            deepLine.SetGuid("deep-line");
            deepLine.questionText = "Deep line";
            deep.nodes.Add(deepLine);

            caller.links.Add(new GraphLink { fromGuid = caller.startGuid, toGuid = callerIntro.GetGuid(), fromPortIndex = 0 });
            caller.links.Add(new GraphLink { fromGuid = callerIntro.GetGuid(), toGuid = jumpToMid.GetGuid(), fromPortIndex = 0 });
            caller.links.Add(new GraphLink { fromGuid = jumpToMid.GetGuid(), toGuid = callerOutro.GetGuid(), fromPortIndex = 0 });
            caller.links.Add(new GraphLink { fromGuid = callerOutro.GetGuid(), toGuid = caller.endGuid, fromPortIndex = 0 });

            mid.links.Add(new GraphLink { fromGuid = mid.startGuid, toGuid = midLine.GetGuid(), fromPortIndex = 0 });
            mid.links.Add(new GraphLink { fromGuid = midLine.GetGuid(), toGuid = jumpToDeep.GetGuid(), fromPortIndex = 0 });
            mid.links.Add(new GraphLink { fromGuid = jumpToDeep.GetGuid(), toGuid = mid.endGuid, fromPortIndex = 0 });

            deep.links.Add(new GraphLink { fromGuid = deep.startGuid, toGuid = deepLine.GetGuid(), fromPortIndex = 0 });
            deep.links.Add(new GraphLink { fromGuid = deepLine.GetGuid(), toGuid = deep.endGuid, fromPortIndex = 0 });

            return (caller, mid, deep, callerIntro, callerOutro, jumpToMid, midLine, jumpToDeep, deepLine);
        }
    }
}
