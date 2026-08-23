using DialogSystem.Runtime.Core.Effects;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Interfaces;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Settings;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.UI;
using DialogSystem.Runtime.UI.Theming;
using DialogSystem.Runtime.Utils;
using DialogSystem.Runtime.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DialogSystem.Runtime.Core
{
    [DisallowMultipleComponent]
    public class DialogManager : MonoSingleton<DialogManager>
    {
        #region ---------------- Inspector: Debug & Scene References ----------------
        [Header("Debug")]
        [SerializeField] private bool doDebug = false;

        [Header("Graph Jump Safety")]
        [SerializeField, Min(1)]
        [Tooltip("Maximum allowed nested Graph Jump depth before traversal is stopped to avoid runaway recursion.")]
        private int maxGraphJumpDepth = 16;

        [Serializable]
        public class DialogGraphModel
        {
            [Tooltip("Graph asset to use for this dialogID.")]
            public DialogGraph dialogGraph;
            [Tooltip("String ID used to play this dialogue via PlayDialogByID().")]
            public string dialogID;
        }

        public enum DialogRegistryValidationSeverity
        {
            Warning,
            Error
        }

        public sealed class DialogRegistryValidationIssue
        {
            public DialogRegistryValidationIssue(DialogRegistryValidationSeverity severity, int entryIndex, string dialogID, string message)
            {
                Severity = severity;
                EntryIndex = entryIndex;
                DialogID = dialogID;
                Message = message;
            }

            public DialogRegistryValidationSeverity Severity { get; }
            public int EntryIndex { get; }
            public string DialogID { get; }
            public string Message { get; }
        }

        public sealed class DialogRegistryValidationResult
        {
            public DialogRegistryValidationResult(List<DialogRegistryValidationIssue> issues)
            {
                Issues = issues ?? new List<DialogRegistryValidationIssue>();
            }

            public IReadOnlyList<DialogRegistryValidationIssue> Issues { get; }
            public bool IsValid => !Issues.Any(issue => issue.Severity == DialogRegistryValidationSeverity.Error);
        }

        [Header("Dialog Graphs List")]
        public List<DialogGraphModel> dialogGraphs = new List<DialogGraphModel>();

        [Header("UI References")]
        [SerializeField] private DialogUIManager dialogUIManagerPrefab;
        public DialogUIManager dialogUIManager;
        [NonSerialized] public DialogUIController dialogUIController;

        [Header("Audio (Scene Reference)")]
        public AudioSource audioSource;

        [Tooltip("Dedicated AudioSource for per-character typewriter sounds. If left empty, one is created automatically.")]
        public AudioSource typewriterAudioSource;
        #endregion

        #region ---------------- Inspector: Local Overrides (Optional) ----------------
        [Header("Overrides (Optional)")]
        [SerializeField] private DialogTextSettings localTextSettings;
        [SerializeField] private DialogAudioSettings localAudioSettings;
        [SerializeField] private DialogueRuntimeUISettings localUiSettings;
        #endregion

        #region ---------------- Events ----------------
        public Action onDialogEnter;
        public Action onDialogExit;
        public event Action<string, string, string> OnLineShown;
        public event Action<string, string> OnChoicePicked;
        public event Action OnConversationReset;
        public event Action<DialogueEndResult> OnDialogEndedWithResult;

        /// <summary>
        /// Fired alongside <see cref="OnLineShown"/> for dialog lines (not choice prompts).
        /// Carries locale keys and raw fallback strings so subscribers can re-resolve
        /// the displayed text when the active locale changes mid-session.
        /// Parameters: (nodeGuid, resolvedSpeaker, resolvedText, speakerLocaleKey, textLocaleKey, rawSpeaker, rawText)
        /// </summary>
        public event Action<string, string, string, string, string, string, string> OnLineShownLocalized;

        /// <summary>
        /// Fired alongside <see cref="OnChoicePicked"/> with locale metadata for history/UI subscribers.
        /// Parameters: (choiceNodeGuid, resolvedText, answerLocaleKey, rawText)
        /// </summary>
        public event Action<string, string, string, string> OnChoicePickedLocalized;
        #endregion

        #region ---------------- Optional Actions ----------------
        [Header("Optional Actions Runner (leave null to disable)")]
        [SerializeField] private DialogActionRunner actionRunner;

        /// <summary>
        /// Optional action runner used to execute graph action nodes and external action calls.
        /// </summary>
        public DialogActionRunner ActionRunner
        {
            get => ResolveActionRunner();
            set => actionRunner = value;
        }
        #endregion

        #region ---------------- Optional Variables ----------------
        [Header("Optional Variables Store")]
        [SerializeField] private DialogueVariableStore variableStore;

        /// <summary>
        /// Runtime variable store used by condition nodes.
        /// </summary>
        public DialogueVariableStore VariableStore
        {
            get => ResolveVariableStore();
            set => variableStore = value;
        }

        public DialogTextSettings RuntimeTextSettings
        {
            get
            {
                if (dialogTextSettings == null)
                    dialogTextSettings = ScriptableObject.CreateInstance<DialogTextSettings>();
                return dialogTextSettings;
            }
        }
        public DialogAudioSettings RuntimeAudioSettings
        {
            get
            {
                if (dialogAudioSettings == null)
                    dialogAudioSettings = ScriptableObject.CreateInstance<DialogAudioSettings>();
                return dialogAudioSettings;
            }
        }
        public DialogueRuntimeUISettings RuntimeUiSettings
        {
            get
            {
                if (dialogueUiSettings == null)
                    dialogueUiSettings = ScriptableObject.CreateInstance<DialogueRuntimeUISettings>();
                return dialogueUiSettings;
            }
        }
        public DialogInputSettings RuntimeInputSettings
        {
            get
            {
                if (dialogInputSettings == null)
                    dialogInputSettings = ScriptableObject.CreateInstance<DialogInputSettings>();
                return dialogInputSettings;
            }
        }
        public DialogueRuntimeState RuntimeState => runtimeStateMachine.State;
        public IDialogueRuntimeView RuntimeView => runtimeView;
        public DialogueEndResult LastEndResult => _lastEndResult;
        public int MaxGraphJumpDepth
        {
            get => Mathf.Max(1, maxGraphJumpDepth);
            set => maxGraphJumpDepth = Mathf.Max(1, value);
        }
        #endregion

        #region ---------------- State ----------------
        // Graph/node state
        private DialogGraph currentGraph;
        private string currentDialogID = null;
        private string currentGuid;
        private DialogNode currentDialog;
        private ChoiceNode currentChoice;

        private ChoiceNode pendingChoiceFromDialog;
        private string pendingNextGuidAfterDialog;

        // Flags
        private bool isTyping = false;
        private bool conversationActive = false;
        private bool _hasPortraitSideState = false;
        private bool _portraitOnRight = false;
        private string _lastPortraitSideSpeaker = string.Empty;

        // Prevent user advance while action chain runs (you already added this earlier).
        private bool _isWaitingForActions = false;
        private float _genericCheckTimer = 0;
        private float _suppressAdvanceUntilTime = 0f;
        private const float AdvanceInputSuppressionSeconds = 0.1f;

        // Effective settings
        private DialogTextSettings dialogTextSettings;
        private DialogAudioSettings dialogAudioSettings;
        private DialogueRuntimeUISettings dialogueUiSettings;
        private DialogChoiceSettings choiceSettings;
        private DialogInputSettings dialogInputSettings;
        private DialogAudioController dialogAudioController;
        private TypewriterAudioController typewriterAudioController;
        private DialogHistoryController dialogHistoryController;
        private DialogRevealController dialogRevealController;
        private DialogChoiceNavigationController dialogChoiceNavigationController;
        private DialogAutoplayController dialogAutoplayController;
        private readonly DialogGraphTraversalController dialogTraversalController = new DialogGraphTraversalController();
        private readonly DialogueRuntimeStateMachine runtimeStateMachine = new DialogueRuntimeStateMachine();
        private readonly DialogRuntimeLogger runtimeLogger = new DialogRuntimeLogger();
        private DialogChoiceResolver choiceResolver;
        private IDialogueRuntimeView runtimeView;
        private readonly Stack<GraphJumpReturnFrame> graphJumpReturnStack = new Stack<GraphJumpReturnFrame>();
        private DialogueEndResult _lastEndResult;
        private OutcomeNode _lastCompletedOutcomeNode;

        // Set to true after a successful graph-jump return.  Used to allow auto-advance
        // through the final end boundary when the conversation is finishing after a jump.
        private bool _hasReturnedFromGraphJump;

        private bool completionCallbackInvoked;

        private Action OnDialogEndedCallback;

        private readonly struct GraphJumpReturnFrame
        {
            public GraphJumpReturnFrame(DialogGraph graph, string dialogId, string returnGuid)
            {
                Graph = graph;
                DialogId = dialogId;
                ReturnGuid = returnGuid;
            }

            public DialogGraph Graph { get; }
            public string DialogId { get; }
            public string ReturnGuid { get; }
        }
        #endregion

        private DialogRevealController RevealController
        {
            get
            {
                if (dialogRevealController == null)
                    dialogRevealController = CreateRevealController();

                return dialogRevealController;
            }
        }

        private DialogHistoryController HistoryController
        {
            get
            {
                if (dialogHistoryController == null)
                    dialogHistoryController = CreateHistoryController();

                return dialogHistoryController;
            }
        }

        private DialogChoiceNavigationController ChoiceNavigation
        {
            get
            {
                if (dialogChoiceNavigationController == null)
                {
                    dialogChoiceNavigationController = new DialogChoiceNavigationController();
                    dialogChoiceNavigationController.Bind(choiceSettings);
                }

                return dialogChoiceNavigationController;
            }
        }

        private DialogChoiceResolver ChoiceResolver
        {
            get
            {
                choiceResolver ??= new DialogChoiceResolver(dialogTraversalController);
                return choiceResolver;
            }
        }

        private DialogAutoplayController AutoPlayController
        {
            get
            {
                if (dialogAutoplayController == null)
                    dialogAutoplayController = CreateAutoplayController();

                return dialogAutoplayController;
            }
        }

        #region ---------------- Unity ----------------
        protected override void Awake()
        {
            ResolveEffectiveSettings();
            DialogLocalizationRuntime.Instance.Initialize();
            EnsureVoiceAudioSource();
            dialogAudioController = new DialogAudioController(this);
            dialogAudioController.Bind(audioSource, localAudioSettings, dialogAudioSettings);

            // Typewriter audio — create a dedicated AudioSource if none is assigned
            if (typewriterAudioSource == null)
                typewriterAudioSource = gameObject.AddComponent<AudioSource>();
            typewriterAudioController = new TypewriterAudioController(this);
            typewriterAudioController.Bind(typewriterAudioSource, dialogAudioSettings);

            dialogAutoplayController = CreateAutoplayController();
            dialogAutoplayController.SetInitialState(dialogTextSettings.autoAdvance);
            dialogRevealController = CreateRevealController();
            dialogHistoryController = CreateHistoryController();
            dialogChoiceNavigationController = new DialogChoiceNavigationController();
            dialogChoiceNavigationController.Bind(choiceSettings);
            choiceResolver = new DialogChoiceResolver(dialogTraversalController);
            var configuredLogMode = DialogSettingsRuntime.RuntimeLogMode;
            if (doDebug && DialogSettingsRuntime.DebugLogsEnabled && configuredLogMode < DialogRuntimeLogMode.Verbose)
            {
                configuredLogMode = DialogRuntimeLogMode.Verbose;
            }

            runtimeLogger.SetMode(configuredLogMode);
            doDebug = DialogSettingsRuntime.DebugLogsEnabled && runtimeLogger.IsVerbose;
            ResolveVariableStore();
            ResolveActionRunner();

            EnsureDialogUiManager();

            if (runtimeView != null)
            {
                runtimeView.SetVisible(false);
                runtimeView.SetSkipAllVisible(false);
                runtimeView.SetAutoPlayActive(GetAutoPlayState());
            }

            dialogAudioController.ApplyDefaultVolume();

            runtimeLogger.Verbose("[DialogManager] Awake: effective settings resolved.");
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            BindLocalizationRuntimeEvents();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            UnbindLocalizationRuntimeEvents();
            SafeStopAllRuntimeActivity();
        }

        private void OnDestroy()
        {
            SafeStopAllRuntimeActivity();
        }

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            runtimeLogger.Important($"[DialogManager] Scene changed: {prev.name} -> {next.name}. Stopping runtime activity.");
            SafeStopAllRuntimeActivity();
        }

        private void Update()
        {
            if (!conversationActive || HistoryController.IsPaused) return;

            // block any input while actions are resolving
            if (_isWaitingForActions) return;

            if (IsAdvanceInputSuppressed()) return;

            // Choice overlay navigation takes priority
            if (IsChoiceOverlayActive())
            {
                HandleChoiceNavigation();
                return;
            }

            // Generic advance
            _genericCheckTimer += Time.deltaTime;
            if (_genericCheckTimer >= 0.5 && InputHelper.CheckGenericAdvanceInput(dialogInputSettings))
            {
                _genericCheckTimer = 0f;
                OnDialogAreaClick();
            }
        }
        #endregion

        #region ---------------- Public API ----------------
        public void PlayDialogByID(string targetDialogID, Action onDialogEnded = null)
        {
            PlayDialogByID(targetDialogID, DialogThemeType.Default, onDialogEnded);
        }

        public void PlayDialogByID(string targetDialogID, DialogThemeType themeType, Action onDialogEnded = null)
        {
            currentDialog = null;

            if (TryResolveDialogGraphByID(targetDialogID, out var graph, out var errorMessage))
            {
                StartDialogInternal(graph, NormalizeDialogId(targetDialogID), themeType, onDialogEnded);
            }
            else
            {
                runtimeLogger.ErrorOnce($"play-id:{targetDialogID}", errorMessage);
            }
        }

        public void StartDialog(DialogGraph graph, Action onDialogEnded = null)
        {
            StartDialog(graph, DialogThemeType.Default, onDialogEnded);
        }

        public void StartDialog(DialogGraph graph, DialogThemeType themeType, Action onDialogEnded = null)
        {
            StartDialogInternal(graph, null, themeType, onDialogEnded);
        }

        public void PlayDialog(DialogGraph graph, Action onDialogEnded = null)
        {
            StartDialog(graph, DialogThemeType.Default, onDialogEnded);
        }

        public void PlayDialog(DialogGraph graph, DialogThemeType themeType, Action onDialogEnded = null)
        {
            StartDialog(graph, themeType, onDialogEnded);
        }

        /// <summary>
        /// Validates the DialogManager ID registry without starting dialogue playback.
        /// </summary>
        public static DialogRegistryValidationResult ValidateDialogRegistry(IList<DialogGraphModel> registryEntries)
        {
            var issues = new List<DialogRegistryValidationIssue>();

            if (registryEntries == null || registryEntries.Count == 0)
                return new DialogRegistryValidationResult(issues);

            var entriesById = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            for (int i = 0; i < registryEntries.Count; i++)
            {
                var entry = registryEntries[i];
                if (entry == null)
                {
                    issues.Add(new DialogRegistryValidationIssue(
                        DialogRegistryValidationSeverity.Error,
                        i,
                        string.Empty,
                        $"Dialog registry entry {i + 1} is empty."));
                    continue;
                }

                var normalizedId = NormalizeDialogId(entry.dialogID);
                if (string.IsNullOrWhiteSpace(normalizedId))
                {
                    issues.Add(new DialogRegistryValidationIssue(
                        DialogRegistryValidationSeverity.Error,
                        i,
                        entry.dialogID,
                        $"Dialog registry entry {i + 1} has an empty Dialog ID."));
                }
                else
                {
                    if (!entriesById.TryGetValue(normalizedId, out var indexes))
                    {
                        indexes = new List<int>();
                        entriesById.Add(normalizedId, indexes);
                    }

                    indexes.Add(i);
                }

                if (entry.dialogGraph == null)
                {
                    issues.Add(new DialogRegistryValidationIssue(
                        DialogRegistryValidationSeverity.Error,
                        i,
                        normalizedId,
                        string.IsNullOrWhiteSpace(normalizedId)
                            ? $"Dialog registry entry {i + 1} has no graph assigned."
                            : $"Dialog ID '{normalizedId}' has no graph assigned."));
                }
            }

            foreach (var pair in entriesById)
            {
                if (pair.Value.Count <= 1)
                    continue;

                var entryList = string.Join(", ", pair.Value.Select(index => (index + 1).ToString()));
                foreach (var index in pair.Value)
                {
                    issues.Add(new DialogRegistryValidationIssue(
                        DialogRegistryValidationSeverity.Error,
                        index,
                        pair.Key,
                        $"Dialog ID '{pair.Key}' is duplicated in registry entries {entryList}."));
                }
            }

            return new DialogRegistryValidationResult(issues);
        }

        private void StartDialogInternal(DialogGraph graph, string dialogId, DialogThemeType themeType, Action onDialogEnded = null)
        {
            BindLocalizationRuntimeEvents();

            if (!TryResolveDialogUi(themeType))
            {
                TransitionToState(DialogueRuntimeState.Failed);
                return;
            }

            ResetRuntimeState(preserveBranchHistory: conversationActive, clearDialogSession: true);
            runtimeLogger.ResetSession();
            TransitionToState(DialogueRuntimeState.Starting);

            if (graph == null)
            {
                runtimeLogger.WarnOnce("start-null-graph", "[DialogManager] StartDialog called with a null graph.");
                TransitionToState(DialogueRuntimeState.Failed);
                ResetRuntimeState(preserveBranchHistory: false, clearDialogSession: true);
                return;
            }

            BindCurrentGraph(graph, dialogId);
            currentGuid = ResolveEntryGuid(graph);

            if (string.IsNullOrEmpty(currentGuid))
            {
                runtimeLogger.WarnOnce(
                    $"missing-entry:{graph.GetEntityId()}",
                    $"[DialogManager] Could not resolve a valid entry node for graph '{graph.name}'. Connect Start to the first runtime node.");
                TransitionToState(DialogueRuntimeState.Failed);
                ResetRuntimeState(preserveBranchHistory: false, clearDialogSession: true);
                return;
            }

            var effectiveUiSettings = dialogUIManager != null
                ? dialogUIManager.GetEffectiveRuntimeUiSettings(dialogueUiSettings)
                : dialogueUiSettings;

            runtimeView.ApplySettings(effectiveUiSettings);
            dialogUIManager?.ApplyRuntimeUiSettings(dialogueUiSettings);
            runtimeView.SetVisible(true);
            runtimeView.SetSkipAllVisible(CanSkipAll() && (effectiveUiSettings == null || effectiveUiSettings.showSkipButton));
            runtimeView.SetAutoPlayActive(GetAutoPlayState());

            conversationActive = true;
            HistoryController.ResetPauseState();
            _isWaitingForActions = false;
            OnDialogEndedCallback = onDialogEnded;
            completionCallbackInvoked = false;
            _lastEndResult = default;
            _lastCompletedOutcomeNode = null;

            runtimeLogger.Important($"[DialogManager] Starting dialog: {graph.name} entry={currentGuid}");

            OnConversationReset?.Invoke();
            onDialogEnter?.Invoke();

            GoTo(currentGuid);
        }

        public bool ToggleAutoPlay()
        {
            SetAutoPlayState(!GetAutoPlayState());
            return GetAutoPlayState();
        }

        public void SetAutoPlayEnabled(bool isEnabled)
        {
            SetAutoPlayState(isEnabled);
        }

        public bool TryGetLastPlayedBranchPath(string dialogId, out List<int> branchPath)
        {
            return HistoryController.TryGetLastPlayedBranchPath(dialogId, out branchPath);
        }

        public string GetLastPlayedDialogTranscript(string dialogId)
        {
            return HistoryController.GetLastPlayedDialogTranscript(dialogId, doDebug);
        }

        public void SkipAll()
        {
            if (!conversationActive || !CanSkipAll()) return;

            if (ShouldStopOnSkipAll()) StopAudio(ShouldFadeOutOnStop());
            StopImmediately();
        }

        public void SkipLine()
        {
            if (!conversationActive) return;

            if (isTyping)
            {
                if (!CanSkipLine()) return;

                OnDialogAreaClick();
                return;
            }

            OnDialogAreaClick();
        }

        public Coroutine InvokeGlobalAction(string actionId, string payloadJson = "", bool waitForCompletion = false, float preDelaySeconds = 0f)
        {
            var runner = ResolveActionRunner();
            if (runner == null) { WarnOnceNoRunner(); return null; }
            return StartCoroutine(runner.RunActionGlobal(actionId, payloadJson, waitForCompletion, preDelaySeconds));
        }

        public Coroutine InvokeDialogueAction(string dialogId, string actionId, string payloadJson = "", bool waitForCompletion = false, float preDelaySeconds = 0f)
        {
            var runner = ResolveActionRunner();
            if (runner == null) { WarnOnceNoRunner(); return null; }
            return StartCoroutine(runner.RunActionForDialogue(dialogId, actionId, payloadJson, waitForCompletion, preDelaySeconds));
        }

        [System.Obsolete("Use InvokeDialogueAction instead.")]
        public Coroutine InvokeConversationAction(string dialogId, string actionId, string payloadJson = "", bool waitForCompletion = false, float preDelaySeconds = 0f)
            => InvokeDialogueAction(dialogId, actionId, payloadJson, waitForCompletion, preDelaySeconds);
        #endregion

        #region ---------------- Core Flow ----------------
        private void GoTo(string guid)
        {
            SafeStopTyping();
            CancelAutoAdvance();
            StopAudioImmediate();

            pendingChoiceFromDialog = null;
            pendingNextGuidAfterDialog = null;

            if (string.IsNullOrEmpty(guid) || dialogTraversalController.IsEndGuid(guid))
            {
                runtimeLogger.Verbose($"[DialogManager] GoTo('{guid ?? "<null>"}') resolved to conversation completion.");
                CompleteConversationFlow();
                return;
            }

            // Hidden flow chain: actions, variable mutations, and conditions resolve before a UI-facing node is shown.
            if (dialogTraversalController.IsHiddenFlowNode(guid))
            {
                runtimeLogger.Verbose($"[DialogManager] GoTo('{guid}') entering hidden flow resolution in graph '{currentGraph?.name ?? "<null>"}'.");

                // Lock user advance while we process flow nodes (which may include action waits).
                _isWaitingForActions = true;
                TransitionToState(DialogueRuntimeState.Waiting);

                StartCoroutine(ResolveNextAfterFlowNodes(guid, resolved =>
                {
                    // Unlock once we've resolved to a UI-facing destination or the end.
                    _isWaitingForActions = false;

                    runtimeLogger.Verbose($"[DialogManager] Hidden flow from '{guid}' resolved to '{resolved ?? "<null>"}' in graph '{currentGraph?.name ?? "<null>"}'.");

                    if (string.IsNullOrEmpty(resolved)) { CompleteConversationFlow(); return; }

                    if (TrySetCurrentVisibleNode(resolved))
                    {
                        ShowCurrentNode();
                        return;
                    }

                    runtimeLogger.InvalidRuntimeTarget(currentGraph, resolved);
                    CompleteConversationFlow();
                }));
                return;
            }

            // Dialog or Choice
            if (!TrySetCurrentVisibleNode(guid))
            {
                runtimeLogger.InvalidRuntimeTarget(currentGraph, guid);
                CompleteConversationFlow();
                return;
            }
            ShowCurrentNode();
        }

        private bool TrySetCurrentVisibleNode(string guid)
        {
            if (!dialogTraversalController.TryResolveVisibleNode(guid, out var dialogNode, out var choiceNode))
            {
                return false;
            }

            currentGuid = guid;
            currentDialog = dialogNode;
            currentChoice = choiceNode;
            return true;
        }

        private void ShowCurrentNode()
        {
            if (currentDialog == null && currentChoice == null)
            {
                TransitionToState(DialogueRuntimeState.Failed);
                EndDialog();
                return;
            }

            TransitionToState(currentChoice != null && string.IsNullOrWhiteSpace(currentChoice.text)
                ? DialogueRuntimeState.PresentingChoices
                : DialogueRuntimeState.PresentingLine);

            if (runtimeView != null)
            {
                if (currentDialog != null)
                {
                    var localizedSpeaker = ResolveLocalizedText(currentDialog.speakerNameLocaleKey, currentDialog.speakerName);
                    var lineText = ResolveVariablesInText(ResolveLocalizedText(currentDialog.questionTextLocaleKey, currentDialog.questionText));
                    ApplyPortraitSideForSpeaker(localizedSpeaker);
                    runtimeView.ShowDialogueLine(new DialogueLinePresentation(localizedSpeaker, lineText, ResolveDialogPortrait(currentDialog)));
                }
                else
                {
                    var promptText = ResolveVariablesInText(ResolveLocalizedText(currentChoice.textLocaleKey, currentChoice.text));
                    var hasPrompt = !string.IsNullOrWhiteSpace(promptText);
                    runtimeView.ShowChoicePrompt(promptText);

                    OnLineShown?.Invoke(currentGuid, string.Empty, promptText);

                    if (!hasPrompt)
                    {
                        SafeStopTyping();
                        ShowChoices(currentChoice);
                        return;
                    }

                    ShowChoices(currentChoice, transitionState: false);
                    SafeStopTyping();
                    StartTyping(promptText);
                    return;
                }
            }

            var rawText = currentDialog != null
                ? ResolveLocalizedText(currentDialog.questionTextLocaleKey, currentDialog.questionText)
                : ResolveLocalizedText(currentChoice.textLocaleKey, currentChoice.text);
            var shownText = ResolveVariablesInText(rawText);
            var speaker = currentDialog != null
                ? ResolveLocalizedText(currentDialog.speakerNameLocaleKey, currentDialog.speakerName)
                : string.Empty;
            OnLineShown?.Invoke(currentGuid, speaker, shownText);

            // Fire locale-aware event so history can re-resolve text when language changes mid-session.
            if (currentDialog != null)
            {
                OnLineShownLocalized?.Invoke(
                    currentGuid,
                    speaker,
                    shownText,
                    currentDialog.speakerNameLocaleKey ?? string.Empty,
                    currentDialog.questionTextLocaleKey ?? string.Empty,
                    currentDialog.speakerName ?? string.Empty,
                    currentDialog.questionText ?? string.Empty);
            }

            if (currentDialog != null) PlayLineAudio(currentDialog.dialogAudio);

            SafeStopTyping();
            StartTyping(shownText);
        }

        private void ApplyPortraitSideForSpeaker(string speakerName)
        {
            if (runtimeView == null)
            {
                return;
            }

            var normalizedSpeaker = string.IsNullOrWhiteSpace(speakerName) ? string.Empty : speakerName.Trim();
            if (!_hasPortraitSideState)
            {
                _portraitOnRight = false;
                _hasPortraitSideState = true;
            }
            else if (!string.Equals(_lastPortraitSideSpeaker, normalizedSpeaker, StringComparison.OrdinalIgnoreCase))
            {
                _portraitOnRight = !_portraitOnRight;
            }

            _lastPortraitSideSpeaker = normalizedSpeaker;
            runtimeView.SetPortraitSide(_portraitOnRight);
        }

        private Sprite ResolveDialogPortrait(DialogNode dialogNode)
        {
            if (dialogNode == null)
            {
                return null;
            }

            if (dialogNode.speakerPortrait != null)
            {
                return dialogNode.speakerPortrait;
            }

            var character = ResolveSpeakerCharacter(currentGraph, dialogNode.speakerName);
            return character != null ? character.Portrait : null;
        }

        private static DialogCharacterSO ResolveSpeakerCharacter(DialogGraph graph, string speakerName)
        {
            if (graph == null || string.IsNullOrWhiteSpace(speakerName))
            {
                return null;
            }

            var trimmedSpeaker = speakerName.Trim();
            return FindCharacterBySpeaker(graph.participatingCharacters, trimmedSpeaker)
                   ?? FindCharacterBySpeaker(graph.sceneContext?.ParticipatingCharacters, trimmedSpeaker);
        }

        private static DialogCharacterSO FindCharacterBySpeaker(IEnumerable<DialogCharacterSO> characters, string speakerName)
        {
            return (characters ?? Enumerable.Empty<DialogCharacterSO>())
                .FirstOrDefault(character =>
                    character != null &&
                    (StringMatches(character.CharacterID, speakerName) ||
                     StringMatches(character.DisplayName, speakerName)));
        }

        private static bool StringMatches(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                   !string.IsNullOrWhiteSpace(right) &&
                   string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private void StartTyping(string line)
        {
            if (!RevealController.CanReveal)
            {
                TransitionToState(DialogueRuntimeState.Failed);
                FailConversationSafely("[DialogManager] Cannot continue conversation because the runtime view dialogue text target is not assigned.");
                return;
            }

            typewriterAudioController?.OnRevealStart();
            TransitionToState(DialogueRuntimeState.Typing);
            RevealController.StartReveal(line);
            isTyping = RevealController.IsRevealing;
        }

        private void HandleAfterTyping()
        {
            HistoryController.ClearPendingPostTypingResolution();

            if (HistoryController.IsPaused) return;

            if (currentChoice != null) 
            {
                if (!IsChoiceOverlayActive())
                {
                    ShowChoices(currentChoice);
                }
                else
                {
                    TransitionToState(DialogueRuntimeState.PresentingChoices);
                }

                return; 
            }

            if (currentDialog != null)
            {
                var dialogNodeAfterTyping = currentDialog;
                var graphAfterTyping = currentGraph;
                var nextDirect = GetNextFromDialog(dialogNodeAfterTyping.GetGuid());
                runtimeLogger.Verbose($"[DialogManager] HandleAfterTyping on '{dialogNodeAfterTyping.GetGuid()}' nextDirect='{nextDirect ?? "<null>"}' graph='{currentGraph?.name ?? "<null>"}'.");
                _isWaitingForActions = true;
                TransitionToState(DialogueRuntimeState.Waiting);

                StartCoroutine(ResolveNextAfterFlowNodes(nextDirect, resolvedNext =>
                {
                    _isWaitingForActions = false;

                    runtimeLogger.Verbose($"[DialogManager] Post-typing flow from '{dialogNodeAfterTyping.GetGuid()}' resolvedNext='{resolvedNext ?? "<null>"}' activeGraph='{currentGraph?.name ?? "<null>"}'.");

                    if (string.IsNullOrEmpty(resolvedNext))
                    {
                        TransitionToState(DialogueRuntimeState.WaitingForContinue);
                        if (ShouldWaitForCurrentDialogAudio())
                        {
                            StartCoroutine(WaitForAudioThenEnd(dialogNodeAfterTyping));
                        }
                        else if (GetAutoPlayState())
                        {
                            ScheduleAutoEnd(dialogNodeAfterTyping);
                        }

                        return;
                    }

                    if (!ReferenceEquals(currentGraph, graphAfterTyping) || currentDialog == null || !ReferenceEquals(currentDialog, dialogNodeAfterTyping))
                    {
                        runtimeLogger.Verbose(
                            $"[DialogManager] Runtime context changed during post-typing flow " +
                            $"(fromGraph='{graphAfterTyping?.name ?? "<null>"}', activeGraph='{currentGraph?.name ?? "<null>"}'). " +
                            $"Entering '{resolvedNext}' immediately.");

                        GoTo(resolvedNext);
                        return;
                    }

                    var choice = FindChoiceByGuid(resolvedNext);
                    if (choice != null)
                    {
                        // If the node has a voice-over still playing, defer showing choices
                        if (ShouldWaitForCurrentDialogAudio())
                        {
                            StartCoroutine(WaitForAudioThenEnterChoiceNode(choice, dialogNodeAfterTyping));
                            return;
                        }

                        EnterChoiceNode(choice);
                        return;
                    }

                    // When the current graph reaches its end boundary and we have a
                    // graph-jump return pending, advance immediately so the caller
                    // resumes without requiring an extra user click.
                    if (dialogTraversalController.IsEndGuid(resolvedNext)
                        && (graphJumpReturnStack.Count > 0 || _hasReturnedFromGraphJump))
                    {
                        runtimeLogger.Verbose($"[DialogManager] Auto-advancing past end boundary in graph '{currentGraph?.name ?? "<null>"}' because graph-jump return is pending.");
                        GoTo(resolvedNext);
                        return;
                    }

                    pendingNextGuidAfterDialog = resolvedNext;
                    TransitionToState(DialogueRuntimeState.WaitingForContinue);

                    // If the node has a voice-over still playing, defer auto-advance
                    if (ShouldWaitForCurrentDialogAudio())
                    {
                        StartCoroutine(WaitForAudioThenAdvance(resolvedNext, dialogNodeAfterTyping));
                        return;
                    }

                    if (GetAutoPlayState())
                        ScheduleAutoAdvance(pendingNextGuidAfterDialog, dialogNodeAfterTyping);
                }));
            }
        }

        private System.Collections.IEnumerator WaitForAudioThenAdvance(string nextGuid, DialogNode nodeForTiming)
        {
            while (audioSource != null && audioSource.isPlaying)
                yield return null;

            if (!conversationActive) yield break;
            if (!IsStillPresentingDialogNode(nodeForTiming)) yield break;

            if (GetAutoPlayState())
                ScheduleAutoAdvance(nextGuid, nodeForTiming);
        }

        private System.Collections.IEnumerator WaitForAudioThenEnterChoiceNode(ChoiceNode choice, DialogNode expectedDialogNode)
        {
            while (audioSource != null && audioSource.isPlaying)
                yield return null;

            if (!conversationActive) yield break;
            if (!IsStillPresentingDialogNode(expectedDialogNode)) yield break;

            EnterChoiceNode(choice);
        }

        private System.Collections.IEnumerator WaitForAudioThenEnd(DialogNode nodeForTiming)
        {
            while (audioSource != null && audioSource.isPlaying)
                yield return null;

            if (!conversationActive) yield break;
            if (!IsStillPresentingDialogNode(nodeForTiming)) yield break;

            if (GetAutoPlayState())
                ScheduleAutoEnd(nodeForTiming);
        }

        private void EnterChoiceNode(ChoiceNode choice)
        {
            if (choice == null)
            {
                return;
            }

            pendingChoiceFromDialog = null;
            pendingNextGuidAfterDialog = null;
            currentGuid = choice.GetGuid();
            currentDialog = null;
            currentChoice = choice;
            ShowCurrentNode();
        }

        private void SafeStopTyping()
        {
            typewriterAudioController?.Stop();
            RevealController.StopAndComplete();
            isTyping = false;
        }


        #endregion

        #region ---------------- Choices ----------------

        private void ShowChoices(ChoiceNode cnode, bool transitionState = true)
        {
            if (cnode == null)
                return;

            if (runtimeView == null || runtimeView.ChoicesRoot == null)
            {
                TransitionToState(DialogueRuntimeState.Failed);
                FailConversationSafely("[DialogManager] Cannot continue conversation because choice UI dependencies are missing (UI panel or choices container).");
                return;
            }

            CancelAutoAdvance();
            pendingNextGuidAfterDialog = null;
            if (transitionState)
            {
                TransitionToState(DialogueRuntimeState.PresentingChoices);
            }

            // Clear local references & UI
            ChoiceNavigation.Reset();
            runtimeView.ClearChoices();

            var promptText = ResolveVariablesInText(ResolveLocalizedText(cnode.textLocaleKey, cnode.text));
            runtimeView.ShowChoicePrompt(promptText);

            // Rebuild via runtime view with graph/localization data already resolved.
            runtimeView.RebuildChoices(BuildChoicePresentations(cnode), choiceSettings, OnChoiceSelected);

            ChoiceNavigation.CacheViewsFrom(runtimeView.ChoicesRoot);
        }

        private List<DialogueChoicePresentation> BuildChoicePresentations(ChoiceNode choiceNode)
        {
            var presentations = new List<DialogueChoicePresentation>();
            if (choiceNode?.choices == null)
            {
                return presentations;
            }

            for (var i = 0; i < choiceNode.choices.Count; i++)
            {
                var choice = choiceNode.choices[i];
                if (choice == null)
                {
                    presentations.Add(new DialogueChoicePresentation(i, string.Empty));
                    continue;
                }

                var answerText = ResolveVariablesInText(ResolveLocalizedText(choice.answerTextLocaleKey, choice.answerText));
                var subLabel = ResolveVariablesInText(choice.tooltipOrSubLabel);
                presentations.Add(new DialogueChoicePresentation(i, answerText, subLabel));
            }

            return presentations;
        }

        // Navigation (called from Update when IsChoiceOverlayActive())
        private void HandleChoiceNavigation()
        {
            ChoiceNavigation.HandleNavigation(OnChoiceSelected);
        }

        public void SelectChoiceIndex(int index)
        {
            ChoiceNavigation.SelectChoiceIndex(index);
        }

        private bool IsChoiceOverlayActive()
        {
            return ChoiceNavigation.IsOverlayActive(runtimeView?.ChoicesRoot);
        }

        public void OnChoiceSelected(int index)
        {
            if (isTyping && currentChoice != null)
            {
                if (!CanSkipCurrentLine()) return;

                SafeStopTyping();
                runtimeView?.SetText(GetCurrentLineText());
                CancelAutoAdvance();
                HandleAfterTyping();
                return;
            }

            var choiceNode = pendingChoiceFromDialog != null ? pendingChoiceFromDialog : currentChoice;
            if (choiceNode == null) return;
            if (!ChoiceResolver.TryResolveChoice(choiceNode, index, out var picked)) return;

            HistoryController.RecordChoiceIndex(index);

            OnChoicePicked?.Invoke(choiceNode.GetGuid(), picked.answerText);
            var pickedText = ResolveVariablesInText(ResolveLocalizedText(picked.answerTextLocaleKey, picked.answerText));
            OnChoicePickedLocalized?.Invoke(
                choiceNode.GetGuid(),
                pickedText,
                picked.answerTextLocaleKey ?? string.Empty,
                picked.answerText ?? string.Empty);
            picked.onSelected?.Invoke();

            var nextGUID = ChoiceResolver.ResolveTargetGuid(choiceNode, index);
            var currentGraphForChoice = currentGraph;

            CancelAutoAdvance();
            StopAudioImmediate();

            // Hide choices but DO NOT clear dialog text here.
            runtimeView?.SetChoicesVisible(false);
            runtimeView?.ClearChoices();
            ChoiceNavigation.Reset();
            pendingChoiceFromDialog = null;

            if (string.IsNullOrEmpty(nextGUID))
            {
                runtimeLogger.MissingChoiceTarget(currentGraphForChoice, choiceNode.GetGuid(), index);
                CompleteConversationFlow();
                return;
            }

            if (!ChoiceResolver.HasValidTarget(currentGraphForChoice, nextGUID))
            {
                runtimeLogger.InvalidRuntimeTarget(currentGraphForChoice, nextGUID);
                CompleteConversationFlow();
                return;
            }

            SuppressAdvanceInput();
            GoTo(nextGUID);
        }


        #endregion

        #region ---------------- Click Handling ----------------
        public void OnDialogAreaClick()
        {
            if (!conversationActive || HistoryController.IsPaused) return;

            // block any input while actions are resolving
            if (_isWaitingForActions) return;

            if (IsAdvanceInputSuppressed()) return;

            if (isTyping)
            {
                if (!CanSkipCurrentLine()) return;

                SafeStopTyping(); // snaps to full text via CompleteImmediately()
                var full = GetCurrentLineText();
                runtimeView?.SetText(full);

                if (ShouldStopOnSkipLine() && currentDialog != null) StopAudio(ShouldFadeOutOnStop());

                CancelAutoAdvance();
                HandleAfterTyping();
                // If the skipped line was in a graph-jump callee and the resolved
                // next node is the callee's end boundary, process the return
                // immediately so the caller resumes without an extra user click.
                if (!string.IsNullOrEmpty(pendingNextGuidAfterDialog)
                    && dialogTraversalController.IsEndGuid(pendingNextGuidAfterDialog)
                    && (graphJumpReturnStack.Count > 0 || _hasReturnedFromGraphJump))
                {
                    var next = pendingNextGuidAfterDialog;
                    pendingNextGuidAfterDialog = null;
                    SuppressAdvanceInput();
                    GoTo(next);
                }

                return;
            }

            // When choices visible, rely on buttons/navigation (avoid accidental skip)
            if (IsChoiceOverlayActive()) return;

            var interruptedBlockingAudio = false;
            if (currentDialog != null && ShouldWaitForCurrentDialogAudio())
            {
                StopAudioImmediate();
                interruptedBlockingAudio = true;
            }

            if (currentDialog != null)
            {
                if (!interruptedBlockingAudio && ShouldWaitForCurrentDialogAudio())
                    return;

                if (!string.IsNullOrEmpty(pendingNextGuidAfterDialog))
                {
                    var next = pendingNextGuidAfterDialog;
                    pendingNextGuidAfterDialog = null;
                    SuppressAdvanceInput();
                    GoTo(next);
                    return;
                }

                var nextGuid = GetNextFromDialog(currentDialog.GetGuid());
                if (!string.IsNullOrEmpty(nextGuid))
                {
                    SuppressAdvanceInput();
                    GoTo(nextGuid);
                    return;
                }
            }

            CompleteConversationFlow();
        }
        #endregion

        #region ---------------- Auto-Advance ----------------
        private void ScheduleAutoAdvance(string nextGuid, DialogNode nodeForTiming)
        {
            AutoPlayController.ScheduleAdvance(nextGuid, nodeForTiming);
        }

        private void ScheduleAutoEnd(DialogNode nodeForTiming)
        {
            AutoPlayController.ScheduleEnd(nodeForTiming);
        }
        #endregion

        #region ---------------- Stop / End ----------------
        public void StopImmediately()
        {
            EndDialog();
        }

        private void CompleteConversationFlow()
        {
            runtimeLogger.Verbose($"[DialogManager] CompleteConversationFlow graph='{currentGraph?.name ?? "<null>"}' returnDepth={graphJumpReturnStack.Count} currentGuid='{currentGuid ?? "<null>"}'.");

            if (TryResumeGraphJumpReturn())
            {
                return;
            }

            TransitionToState(DialogueRuntimeState.Completed);
            EndDialog();
        }

        private void EndDialog()
        {
            if (!conversationActive && OnDialogEndedCallback == null)
            {
                return;
            }

            var wasActive = conversationActive;
            var endedCallback = OnDialogEndedCallback;
            var endResult = _lastEndResult;
            var shouldInvokeCompletion = !completionCallbackInvoked;
            completionCallbackInvoked = true;

            TransitionToState(DialogueRuntimeState.Completed);
            ResetRuntimeState(preserveBranchHistory: true, clearDialogSession: true, allowAudioFadeOut: true, clearEndResult: false);

            if (wasActive)
            {
                OnConversationReset?.Invoke();
                onDialogExit?.Invoke();
            }

            if (endResult.HasOutcome)
            {
                OnDialogEndedWithResult?.Invoke(endResult);
            }

            if (shouldInvokeCompletion)
            {
                endedCallback?.Invoke();
            }

            _lastEndResult = default;
        }
        #endregion

        #region ---------------- Graph Helpers ----------------
        private DialogNode FindDialogByGuid(string guid)
        {
            return dialogTraversalController.FindDialogByGuid(guid);
        }

        private ChoiceNode FindChoiceByGuid(string guid)
        {
            return dialogTraversalController.FindChoiceByGuid(guid);
        }

        private ActionNode FindActionByGuid(string guid)
        {
            return dialogTraversalController.FindActionByGuid(guid);
        }

        private ConditionNode FindConditionByGuid(string guid)
        {
            return dialogTraversalController.FindConditionByGuid(guid);
        }

        private VariableMutationNode FindVariableMutationByGuid(string guid)
        {
            return dialogTraversalController.FindVariableMutationByGuid(guid);
        }

        private GraphJumpNode FindGraphJumpByGuid(string guid)
        {
            return dialogTraversalController.FindGraphJumpByGuid(guid);
        }

        private string ResolveEntryGuid(DialogGraph graph)
        {
            if (graph == null) return null;

            var next = DialogGraphFlowUtility.ResolveEntryGuid(graph);
            if (!string.IsNullOrEmpty(next))
            {
                return next;
            }

            runtimeLogger.WarnOnce(
                $"missing-entry-detail:{graph.GetEntityId()}",
                string.IsNullOrEmpty(graph.startGuid)
                    ? "[DialogManager] startGuid is empty. Set it in the graph (Start node)."
                    : "[DialogManager] Start node has no outgoing link. Connect Start to the first playable node.");

            return null;
        }

        private string GetNextFromDialog(string guid)
        {
            return dialogTraversalController.GetNextFromDialog(guid);
        }

        private string GetNextFromChoice(string guid, int choiceIndex)
        {
            return dialogTraversalController.GetNextFromChoice(guid, choiceIndex);
        }

        private string GetNextFromAction(string guid)
        {
            return dialogTraversalController.GetNextFromAction(guid);
        }

        private string GetNextFromVariableMutation(string guid)
        {
            return dialogTraversalController.GetNextFromVariableMutation(guid);
        }

        private string GetNextFromCondition(string guid, bool result)
        {
            return dialogTraversalController.GetNextFromCondition(guid, result);
        }

        private string GetNextFromGraphJump(string guid)
        {
            return dialogTraversalController.GetNextFromGraphJump(guid);
        }

        private string GetNextFromOutcome(string guid)
        {
            return dialogTraversalController.GetNextFromOutcome(guid);
        }

        private DialogGraph GetDialogGraphById(string dialogId)
        {
            return TryResolveDialogGraphByID(dialogId, out var graph, out _)
                ? graph
                : null;
        }
        #endregion

        #region ---------------- Helpers ----------------
        private bool TryResolveDialogGraphByID(string dialogId, out DialogGraph graph, out string errorMessage)
        {
            graph = null;
            errorMessage = string.Empty;

            var normalizedId = NormalizeDialogId(dialogId);
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                errorMessage = "[DialogManager] PlayDialogByID failed because the dialog ID is empty.";
                return false;
            }

            var matches = dialogGraphs
                .Where(entry => entry != null && NormalizeDialogId(entry.dialogID) == normalizedId)
                .ToList();

            if (matches.Count == 0)
            {
                errorMessage = $"[DialogManager] PlayDialogByID failed. No dialog registry entry exists for id '{normalizedId}'.";
                return false;
            }

            if (matches.Count > 1)
            {
                errorMessage = $"[DialogManager] PlayDialogByID failed. Dialog id '{normalizedId}' is assigned to {matches.Count} registry entries.";
                return false;
            }

            graph = matches[0].dialogGraph;
            if (graph == null)
            {
                errorMessage = $"[DialogManager] PlayDialogByID failed. Dialog id '{normalizedId}' has no graph assigned.";
                return false;
            }

            return true;
        }

        private static string NormalizeDialogId(string dialogId)
        {
            return string.IsNullOrWhiteSpace(dialogId) ? string.Empty : dialogId.Trim();
        }

        private void CancelAutoAdvance()
        {
            AutoPlayController.Cancel();
        }

        private bool IsAdvanceInputSuppressed()
        {
            return Time.unscaledTime < _suppressAdvanceUntilTime;
        }

        private void SuppressAdvanceInput(float durationSeconds = AdvanceInputSuppressionSeconds)
        {
            _suppressAdvanceUntilTime = Mathf.Max(
                _suppressAdvanceUntilTime,
                Time.unscaledTime + Mathf.Max(0.01f, durationSeconds));
            _genericCheckTimer = 0f;
        }

        private void BindCurrentGraph(DialogGraph graph, string dialogId)
        {
            currentGraph = graph;
            currentDialogID = dialogId;
            dialogTraversalController.Bind(graph);
            currentGuid = null;
            currentDialog = null;
            currentChoice = null;
        }

        private void SafeStopAllRuntimeActivity()
        {
            ResetRuntimeState(preserveBranchHistory: false, clearDialogSession: true);
        }
        #endregion

        #region ---------------- Hidden Flow Traversal ----------------
        private IEnumerator ResolveNextAfterFlowNodes(string nextDirect, Action<string> onResolved)
        {
            string cursor = nextDirect;
            int traversedFlowCount = 0;
            int maxTraversalSteps = dialogTraversalController.GetHiddenFlowTraversalLimit();
            var visitedFlowGuids = new HashSet<string>();

            while (!string.IsNullOrEmpty(cursor))
            {
                maxTraversalSteps = Math.Max(maxTraversalSteps, dialogTraversalController.GetHiddenFlowTraversalLimit() + graphJumpReturnStack.Count);

                if (dialogTraversalController.IsEndGuid(cursor))
                {
                    break;
                }

                if (!dialogTraversalController.TryResolveHiddenFlowNode(
                        cursor,
                        out var act,
                        out var variableMutation,
                        out var condition,
                        out var graphJump,
                        out var outcome))
                {
                    break;
                }

                var traversalKey = GetHiddenFlowTraversalKey(currentGraph, cursor);
                if (!visitedFlowGuids.Add(traversalKey))
                {
                    runtimeLogger.HiddenFlowCycle(currentGraph, cursor);
                    cursor = null;
                    break;
                }

                traversedFlowCount++;
                if (traversedFlowCount > maxTraversalSteps)
                {
                    runtimeLogger.HiddenFlowLimitExceeded(currentGraph, maxTraversalSteps);
                    cursor = null;
                    break;
                }

                if (act != null)
                {
                    TransitionToState(DialogueRuntimeState.ExecutingAction);
                    runtimeLogger.Verbose($"[DialogManager] Action '{act.actionId}' wait={act.waitForCompletion} delay={act.waitSeconds}");

                    if (string.IsNullOrWhiteSpace(act.actionId))
                    {
                        runtimeLogger.WarnOnce(
                            $"empty-action:{GetHiddenFlowTraversalKey(currentGraph, act.GetGuid())}",
                            $"[DialogManager] Action node '{act.GetGuid()}' has an empty action ID. The action will be skipped.");
                    }
                    else
                    {
                        var runner = ResolveActionRunner();
                        if (runner != null)
                            yield return StartCoroutine(runner.RunAction(act, currentDialogID));
                    }

                    cursor = GetNextFromAction(act.GetGuid());
                    continue;
                }

                if (variableMutation != null)
                {
                    TransitionToState(DialogueRuntimeState.Waiting);
                    ApplyVariableMutation(variableMutation);
                    cursor = GetNextFromVariableMutation(variableMutation.GetGuid());
                    continue;
                }

                if (condition != null)
                {
                    TransitionToState(DialogueRuntimeState.EvaluatingCondition);
                    var result = EvaluateCondition(condition);
                    runtimeLogger.Verbose($"[DialogManager] Condition '{condition.variableName}' evaluated to {result}.");

                    cursor = GetNextFromCondition(condition.GetGuid(), result);
                    if (string.IsNullOrWhiteSpace(cursor))
                    {
                        runtimeLogger.MissingBranchTarget(currentGraph, condition.GetGuid(), result ? "True" : "False");
                    }
                    continue;
                }

                if (outcome != null)
                {
                    TransitionToState(DialogueRuntimeState.Waiting);
                    RecordOutcomeResult(outcome);
                    cursor = GetNextFromOutcome(outcome.GetGuid());
                    continue;
                }

                TransitionToState(DialogueRuntimeState.Waiting);
                if (!TryEnterGraphJump(graphJump, out cursor))
                {
                    // Guard blocked the jump (cycle, depth, or resolution failure).
                    // Continue past this jump node in the current graph.
                    cursor = GetNextFromGraphJump(graphJump.GetGuid());
                    continue;
                }

                continue;
            }

            onResolved?.Invoke(cursor);
        }

        private bool TryEnterGraphJump(GraphJumpNode graphJump, out string targetGuid)
        {
            targetGuid = null;
            if (graphJump == null)
            {
                return false;
            }

            if (!TryResolveGraphJumpTarget(graphJump, out var targetGraph, out var targetDialogId, out var error))
            {
                runtimeLogger.WarnOnce($"graph-jump-target:{GetHiddenFlowTraversalKey(currentGraph, graphJump.GetGuid())}", error);
                return false;
            }

            targetGuid = ResolveGraphJumpEntryGuid(targetGraph, graphJump.targetGraph);
            if (string.IsNullOrWhiteSpace(targetGuid))
            {
                runtimeLogger.WarnOnce(
                    $"graph-jump-entry:{GetHiddenFlowTraversalKey(currentGraph, graphJump.GetGuid())}",
                    $"[DialogManager] Graph Jump node '{graphJump.GetGuid()}' could not resolve an entry point for target graph '{targetGraph.name}'.");
                return false;
            }

            if (graphJumpReturnStack.Count >= MaxGraphJumpDepth)
            {
                runtimeLogger.WarnOnce(
                    $"graph-jump-depth:{GetHiddenFlowTraversalKey(currentGraph, graphJump.GetGuid())}",
                    $"[DialogManager] Graph Jump node '{graphJump.GetGuid()}' exceeded the maximum nested jump depth ({MaxGraphJumpDepth}). " +
                    "Traversal will stop to avoid runaway recursion.");
                return false;
            }

            if (WouldCreateGraphJumpCycle(targetGraph))
            {
                runtimeLogger.WarnOnce(
                    $"graph-jump-cycle:{GetHiddenFlowTraversalKey(currentGraph, graphJump.GetGuid())}",
                    $"[DialogManager] Graph Jump node '{graphJump.GetGuid()}' would re-enter graph '{targetGraph.name}' while it is already active on the jump stack. " +
                    "Traversal will stop to avoid a recursive graph cycle.");
                return false;
            }

            var returnGuid = GetNextFromGraphJump(graphJump.GetGuid());
            graphJumpReturnStack.Push(new GraphJumpReturnFrame(currentGraph, currentDialogID, returnGuid));
            BindCurrentGraph(targetGraph, targetDialogId);

            runtimeLogger.Important($"[DialogManager] Graph jump '{graphJump.GetGuid()}' entered graph '{targetGraph.name}' entry={targetGuid} return={returnGuid ?? "<end>"} dialogId='{targetDialogId ?? string.Empty}'.");

            return true;
        }

        private bool TryResolveGraphJumpTarget(GraphJumpNode graphJump, out DialogGraph targetGraph, out string targetDialogId, out string error)
        {
            targetGraph = null;
            targetDialogId = currentDialogID;
            error = string.Empty;

            var reference = graphJump?.targetGraph;
            runtimeLogger.Verbose(
                $"[DialogManager] Resolving graph jump target node='{graphJump?.GetGuid() ?? "<null>"}' " +
                $"asset='{reference?.graphAsset?.name ?? "<null>"}' runtimeDialogId='{reference?.runtimeDialogId ?? string.Empty}' graphGuid='{reference?.graphGuid ?? string.Empty}' " +
                $"graphName='{reference?.graphName ?? string.Empty}' entryGuid='{reference?.entryGuid ?? string.Empty}'.");

            if (reference == null || !reference.HasReference)
            {
                error = $"[DialogManager] Graph Jump node '{graphJump?.GetGuid() ?? "<missing>"}' has no target graph reference.";
                return false;
            }

            targetGraph = ResolveGraphReference(reference, out var resolutionError);
            if (targetGraph == null)
            {
                error = string.IsNullOrWhiteSpace(resolutionError)
                    ? $"[DialogManager] Graph Jump node '{graphJump.GetGuid()}' could not resolve target graph '{GetGraphReferenceLabel(reference)}'. Assign the graph asset directly or register the target graph on DialogManager."
                    : resolutionError;
                return false;
            }

            EnsureRuntimeGraphRegistration(targetGraph, reference.graphAsset != null);
            targetDialogId = ResolveDialogIdForGraph(targetGraph, currentDialogID);
            return true;
        }

        private DialogGraph ResolveGraphReference(GraphReference reference, out string error)
        {
            error = string.Empty;
            if (reference == null)
            {
                return null;
            }

            if (reference.graphAsset != null)
            {
                runtimeLogger.Verbose($"[DialogManager] Graph reference resolved directly via asset '{reference.graphAsset.name}'.");
                return reference.graphAsset;
            }

            if (!string.IsNullOrWhiteSpace(reference.runtimeDialogId))
            {
                var normalizedRuntimeDialogId = NormalizeDialogId(reference.runtimeDialogId);
                var runtimeMatches = dialogGraphs?
                    .Where(entry => entry != null && NormalizeDialogId(entry.dialogID) == normalizedRuntimeDialogId)
                    .ToList();

                if (runtimeMatches != null && runtimeMatches.Count > 1)
                {
                    error = $"[DialogManager] Graph Jump runtime dialog ID '{normalizedRuntimeDialogId}' is assigned to multiple registry entries. Resolve the duplicate ID before using this Graph Jump node.";
                    return null;
                }

                if (runtimeMatches != null && runtimeMatches.Count == 1)
                {
                    var byRuntimeId = runtimeMatches[0].dialogGraph;
                    if (byRuntimeId == null)
                    {
                        error = $"[DialogManager] Graph Jump runtime dialog ID '{normalizedRuntimeDialogId}' resolves to an empty registry entry.";
                        return null;
                    }

                    runtimeLogger.Verbose($"[DialogManager] Graph reference resolved via runtime dialog ID '{normalizedRuntimeDialogId}' -> '{byRuntimeId.name}'.");
                    return byRuntimeId;
                }
            }

            if (!string.IsNullOrWhiteSpace(reference.graphGuid))
            {
                var byGuid = dialogGraphs?
                    .Select(entry => entry?.dialogGraph)
                    .FirstOrDefault(graph => graph != null && string.Equals(graph.GraphGuid, reference.graphGuid, StringComparison.Ordinal));
                if (byGuid != null)
                {
                    runtimeLogger.Verbose($"[DialogManager] Graph reference resolved via graphGuid '{reference.graphGuid}' -> '{byGuid.name}'.");
                    return byGuid;
                }
            }

            if (!string.IsNullOrWhiteSpace(reference.graphName))
            {
                var byName = dialogGraphs?
                    .Select(entry => entry?.dialogGraph)
                    .Where(graph => graph != null && string.Equals(graph.name, reference.graphName, StringComparison.Ordinal))
                    .Distinct()
                    .ToList();
                if (byName != null && byName.Count == 1)
                {
                    runtimeLogger.Verbose($"[DialogManager] Graph reference resolved via graphName '{reference.graphName}' -> '{byName[0].name}'.");
                    return byName[0];
                }
            }

            runtimeLogger.Verbose($"[DialogManager] Graph reference could not be resolved. runtimeDialogId='{reference.runtimeDialogId ?? string.Empty}' graphGuid='{reference.graphGuid ?? string.Empty}' graphName='{reference.graphName ?? string.Empty}'.");

            return null;
        }

        private bool WouldCreateGraphJumpCycle(DialogGraph targetGraph)
        {
            if (targetGraph == null)
            {
                return false;
            }

            if (GraphIdentityEquals(currentGraph, targetGraph))
            {
                return true;
            }

            foreach (var frame in graphJumpReturnStack)
            {
                if (GraphIdentityEquals(frame.Graph, targetGraph))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool GraphIdentityEquals(DialogGraph first, DialogGraph second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            if (first == null || second == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(first.GraphGuid) &&
                   string.Equals(first.GraphGuid, second.GraphGuid, StringComparison.Ordinal);
        }

        private static string GetGraphReferenceLabel(GraphReference reference)
        {
            if (reference == null)
            {
                return "<unknown>";
            }

            if (!string.IsNullOrWhiteSpace(reference.runtimeDialogId))
            {
                return reference.runtimeDialogId;
            }

            if (!string.IsNullOrWhiteSpace(reference.graphGuid))
            {
                return reference.graphGuid;
            }

            if (!string.IsNullOrWhiteSpace(reference.graphName))
            {
                return reference.graphName;
            }

            return "<unknown>";
        }

        private string ResolveDialogIdForGraph(DialogGraph graph, string fallbackDialogId)
        {
            if (graph == null || dialogGraphs == null)
            {
                return fallbackDialogId;
            }

            var matches = dialogGraphs
                .Where(entry => entry?.dialogGraph != null)
                .Where(entry =>
                    ReferenceEquals(entry.dialogGraph, graph) ||
                    (!string.IsNullOrWhiteSpace(graph.GraphGuid) &&
                     string.Equals(entry.dialogGraph.GraphGuid, graph.GraphGuid, StringComparison.Ordinal)))
                .Select(entry => NormalizeDialogId(entry.dialogID))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (matches.Count == 0)
            {
                return fallbackDialogId;
            }

            if (matches.Count > 1)
            {
                runtimeLogger.VerboseWarning(
                    $"graph-multiple-dialog-ids:{graph.GetEntityId()}",
                    $"[DialogManager] Graph '{graph.name}' is mapped to multiple dialog IDs ({string.Join(", ", matches)}). Using '{matches[0]}' for action routing during graph jump traversal.");
            }

            return matches[0];
        }

        private void EnsureRuntimeGraphRegistration(DialogGraph graph, bool allowAutoRegister)
        {
            if (graph == null)
            {
                return;
            }

            dialogGraphs ??= new List<DialogGraphModel>();

            var alreadyRegistered = dialogGraphs.Any(entry =>
                entry?.dialogGraph != null &&
                (ReferenceEquals(entry.dialogGraph, graph) ||
                 (!string.IsNullOrWhiteSpace(graph.GraphGuid) &&
                  string.Equals(entry.dialogGraph.GraphGuid, graph.GraphGuid, StringComparison.Ordinal))));

            if (alreadyRegistered || !allowAutoRegister)
            {
                return;
            }

            var generatedDialogId = !string.IsNullOrWhiteSpace(graph.GraphGuid)
                ? graph.GraphGuid
                : NormalizeDialogId(graph.name);

            dialogGraphs.Add(new DialogGraphModel
            {
                dialogGraph = graph,
                dialogID = generatedDialogId
            });

            runtimeLogger.Verbose($"[DialogManager] Auto-registered graph jump target '{graph.name}' with dialog id '{generatedDialogId}'.");
        }

        private string ResolveGraphJumpEntryGuid(DialogGraph graph, GraphReference reference)
        {
            if (graph == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(reference?.entryGuid) && DialogGraphFlowUtility.ContainsRuntimeNode(graph, reference.entryGuid))
            {
                runtimeLogger.Verbose($"[DialogManager] Graph jump entry resolved from explicit runtime node '{reference.entryGuid}' in graph '{graph.name}'.");
                return reference.entryGuid;
            }

            if (!string.IsNullOrWhiteSpace(reference?.entryGuid))
            {
                var isBoundary = DialogGraphFlowUtility.IsStartGuid(graph, reference.entryGuid) || DialogGraphFlowUtility.IsEndGuid(graph, reference.entryGuid);
                runtimeLogger.Verbose($"[DialogManager] Graph jump explicit entry '{reference.entryGuid}' is not a runtime node in graph '{graph.name}' (boundary={isBoundary}). Falling back to graph entry resolution.");
            }

            var resolvedEntry = ResolveEntryGuid(graph);
            runtimeLogger.Verbose($"[DialogManager] Graph jump fallback entry for graph '{graph.name}' resolved to '{resolvedEntry ?? "<null>"}'.");
            return resolvedEntry;
        }

        private bool TryResumeGraphJumpReturn()
        {
            while (graphJumpReturnStack.Count > 0)
            {
                var frame = graphJumpReturnStack.Pop();
                if (frame.Graph == null)
                {
                    continue;
                }

                BindCurrentGraph(frame.Graph, frame.DialogId);

                if (string.IsNullOrWhiteSpace(frame.ReturnGuid) || dialogTraversalController.IsEndGuid(frame.ReturnGuid))
                {
                    continue;
                }

                runtimeLogger.Verbose($"[DialogManager] Returning from graph jump to graph '{frame.Graph.name}' guid={frame.ReturnGuid} dialogId='{frame.DialogId ?? string.Empty}'.");

                GoTo(frame.ReturnGuid);
                _hasReturnedFromGraphJump = true;
                return true;
            }

            return false;
        }

        private static string GetHiddenFlowTraversalKey(DialogGraph graph, string guid)
        {
            var graphKey = graph != null && !string.IsNullOrWhiteSpace(graph.GraphGuid)
                ? graph.GraphGuid
                : graph != null
                    ? graph.GetEntityId().ToString()
                    : "<null-graph>";
            return $"{graphKey}:{guid ?? string.Empty}";
        }

        private bool EvaluateCondition(ConditionNode condition)
        {
            if (condition == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(condition.variableName))
            {
                runtimeLogger.WarnOnce(
                    $"condition-empty-variable:{GetHiddenFlowTraversalKey(currentGraph, condition.GetGuid())}",
                    $"[DialogManager] Condition node '{condition.GetGuid()}' has an empty variable name. Using fallback result {condition.missingVariableResult}.");
                return condition.missingVariableResult;
            }

            ResolveVariableStore();
            if (variableStore == null)
            {
                runtimeLogger.WarnOnce(
                    $"condition-missing-store:{GetHiddenFlowTraversalKey(currentGraph, condition.GetGuid())}",
                    $"[DialogManager] No DialogueVariableStore assigned. Condition node '{condition.GetGuid()}' will use fallback result {condition.missingVariableResult}.");
                return condition.missingVariableResult;
            }

            if (!variableStore.Contains(condition.variableName))
            {
                runtimeLogger.WarnOnce(
                    $"condition-missing-variable:{GetHiddenFlowTraversalKey(currentGraph, condition.GetGuid())}:{condition.variableName}",
                    $"[DialogManager] Condition node '{condition.GetGuid()}' references missing variable '{condition.variableName}'. Using fallback result {condition.missingVariableResult}.");
                return condition.missingVariableResult;
            }

            return condition.Evaluate(variableStore);
        }

        private void ApplyVariableMutation(VariableMutationNode variableMutation)
        {
            if (variableMutation == null)
            {
                return;
            }

            ResolveVariableStore();
            if (variableStore == null)
            {
                runtimeLogger.WarnOnce(
                    $"variable-missing-store:{GetHiddenFlowTraversalKey(currentGraph, variableMutation.GetGuid())}",
                    $"[DialogManager] No DialogueVariableStore assigned. Variable mutation node '{variableMutation.GetGuid()}' was skipped.");
                return;
            }

            if (!variableMutation.Apply(variableStore, out var error))
            {
                runtimeLogger.WarnOnce(
                    $"variable-mutation-failed:{GetHiddenFlowTraversalKey(currentGraph, variableMutation.GetGuid())}",
                    $"[DialogManager] Variable mutation failed on node '{variableMutation.GetGuid()}': {error}");
            }
        }

        private string ResolveVariablesInText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            ResolveVariableStore();
            return DialogueVariableTextFormatter.Format(text, variableStore);
        }

        /// <summary>
        /// Tries to resolve the active dialogue variable store from the inspector reference, this GameObject, or the scene.
        /// </summary>
        public bool TryGetVariableStore(out DialogueVariableStore store)
        {
            store = ResolveVariableStore();
            return store != null;
        }

        /// <summary>
        /// Tries to resolve the active dialog action runner from the inspector reference, this GameObject, or the scene.
        /// </summary>
        public bool TryGetActionRunner(out DialogActionRunner runner)
        {
            runner = ResolveActionRunner();
            return runner != null;
        }

        private DialogueVariableStore ResolveVariableStore()
        {
            if (variableStore == null)
            {
                variableStore = GetComponent<DialogueVariableStore>();
            }

            if (variableStore == null)
            {
                variableStore = DialogRuntimeUnityCompatibility.FindFirst<DialogueVariableStore>();
            }

            return variableStore;
        }

        private DialogActionRunner ResolveActionRunner()
        {
            if (actionRunner == null)
            {
                actionRunner = GetComponent<DialogActionRunner>();
            }

            if (actionRunner == null)
            {
                actionRunner = DialogRuntimeUnityCompatibility.FindFirst<DialogActionRunner>();
            }

            return actionRunner;
        }
        #endregion

        #region ---------------- Audio ----------------
        private DialogAudioController AudioController
        {
            get
            {
                dialogAudioController ??= new DialogAudioController(this);
                dialogAudioController.Bind(audioSource, localAudioSettings, dialogAudioSettings);
                return dialogAudioController;
            }
        }

        private void PlayLineAudio(AudioClip clip)
        {
            AudioController.PlayLineAudio(clip, doDebug, currentDialog?.name);
        }

        private void StopAudio(bool withFade)
        {
            AudioController.StopAudio(withFade);
        }

        private void StopAudioImmediate()
        {
            AudioController.StopAudioImmediate();
        }

        private void CancelAudioFade()
        {
            AudioController.CancelFade();
        }

        private bool ShouldWaitForCurrentDialogAudio()
        {
            return currentDialog != null &&
                   currentDialog.dialogAudio != null &&
                   IsDialogAudioPlaybackActive();
        }

        protected virtual bool IsDialogAudioPlaybackActive()
        {
            return audioSource != null && audioSource.isPlaying;
        }

        private bool IsStillPresentingDialogNode(DialogNode expectedDialogNode)
        {
            return expectedDialogNode != null &&
                   conversationActive &&
                   currentDialog == expectedDialogNode &&
                   string.Equals(currentGuid, expectedDialogNode.GetGuid(), StringComparison.Ordinal);
        }
        #endregion

        #region ---------------- History ----------------
        public void PauseForHistory()
        {
            HistoryController.Pause();
        }

        public void PauseForOverlay()
        {
            HistoryController.Pause();
        }

        public void SetRuntimeVoiceVolume(float value)
        {
            if (dialogAudioSettings == null)
            {
                return;
            }

            dialogAudioSettings.voiceVolume = Mathf.Clamp01(value);
            dialogAudioController?.ApplyDefaultVolume();
            DialogRuntimeSettingsPersistence.SaveAudio(dialogAudioSettings);
        }

        public void SetRuntimeSfxVolume(float value)
        {
            if (dialogAudioSettings == null)
            {
                return;
            }

            dialogAudioSettings.sfxVolume = Mathf.Clamp01(value);
            DialogRuntimeSettingsPersistence.SaveAudio(dialogAudioSettings);
        }

        public void SetRuntimeTextSpeed(float charsPerSecond)
        {
            if (dialogTextSettings == null)
            {
                return;
            }

            dialogTextSettings.charsPerSecond = Mathf.Clamp(charsPerSecond, 1f, 120f);
            DialogRuntimeSettingsPersistence.SaveText(dialogTextSettings);
        }

        public void SetRuntimeAutoAdvance(bool value)
        {
            if (dialogTextSettings == null)
            {
                return;
            }

            dialogTextSettings.autoAdvance = value;
            SetAutoPlayState(value);
            DialogRuntimeSettingsPersistence.SaveText(dialogTextSettings);
        }

        public void SetRuntimeTypewriterVolume(float value)
        {
            if (dialogAudioSettings == null)
            {
                return;
            }

            dialogAudioSettings.typewriterVolume = Mathf.Clamp01(value);
            DialogRuntimeSettingsPersistence.SaveAudio(dialogAudioSettings);
        }

        public void ResumeAfterHistory()
        {
            HistoryController.Resume();
        }

        public void ResumeAfterOverlay()
        {
            HistoryController.Resume();
        }

        public string GetCurrentLineText()
        {
            if (currentDialog != null) return ResolveVariablesInText(ResolveLocalizedText(currentDialog.questionTextLocaleKey, currentDialog.questionText));
            if (currentChoice != null) return ResolveVariablesInText(ResolveLocalizedText(currentChoice.textLocaleKey, currentChoice.text));
            return string.Empty;
        }

        /// <summary>
        /// Returns the outcome node currently bound to the active runtime cursor, if any.
        /// This is only non-null while the runtime is resolving a hidden flow node.
        /// </summary>
        public OutcomeNode GetCurrentOutcomeNode()
        {
            return string.IsNullOrWhiteSpace(currentGuid)
                ? null
                : dialogTraversalController.FindOutcomeByGuid(currentGuid);
        }

        /// <summary>
        /// Returns the most recently completed outcome node from the last dialogue
        /// that ended through an Outcome node. Cleared when a new dialogue starts.
        /// </summary>
        public OutcomeNode LastCompletedOutcomeNode => _lastCompletedOutcomeNode;

        /// <summary>
        /// Attempts to resolve the most recently completed outcome node.
        /// </summary>
        public bool TryGetLastCompletedOutcomeNode(out OutcomeNode outcomeNode)
        {
            outcomeNode = _lastCompletedOutcomeNode;
            return outcomeNode != null;
        }

        /// <summary>
        /// Returns the description of the most recently completed outcome, or an empty string.
        /// </summary>
        public string GetLastOutcomeDescription()
        {
            if (_lastCompletedOutcomeNode != null)
            {
                return _lastCompletedOutcomeNode.description ?? string.Empty;
            }

            return _lastEndResult.OutcomeDescription ?? string.Empty;
        }

        /// <summary>
        /// Resolves an outcome node by outcome ID from the active graph or, when the
        /// dialogue has already ended, from the last completed outcome.
        /// </summary>
        public bool TryGetOutcomeNode(string outcomeId, out OutcomeNode outcomeNode)
        {
            outcomeNode = null;
            if (string.IsNullOrWhiteSpace(outcomeId))
            {
                return false;
            }

            outcomeNode = dialogTraversalController.FindOutcomeById(outcomeId);
            if (outcomeNode != null)
            {
                return true;
            }

            if (_lastCompletedOutcomeNode != null &&
                string.Equals(_lastCompletedOutcomeNode.outcomeId, outcomeId, StringComparison.Ordinal))
            {
                outcomeNode = _lastCompletedOutcomeNode;
                return true;
            }

            return false;
        }

        private string ResolveLocalizedText(string localeKey, string fallback)
        {
            return DialogLocalizationRuntime.Instance.Resolve(localeKey, fallback);
        }

        private void RefreshVisibleLocalizedText()
        {
            if (!conversationActive || runtimeView == null)
            {
                return;
            }

            if (pendingChoiceFromDialog != null && IsChoiceOverlayActive())
            {
                RefreshChoiceNodeLocalizedText(pendingChoiceFromDialog, restartRevealIfTyping: false);
                return;
            }

            if (currentDialog != null)
            {
                var speaker = ResolveLocalizedText(currentDialog.speakerNameLocaleKey, currentDialog.speakerName);
                var lineText = ResolveVariablesInText(ResolveLocalizedText(currentDialog.questionTextLocaleKey, currentDialog.questionText));
                ApplyPortraitSideForSpeaker(speaker);
                runtimeView.ShowDialogueLine(new DialogueLinePresentation(speaker, lineText, ResolveDialogPortrait(currentDialog)));

                if (isTyping)
                {
                    SafeStopTyping();
                    StartTyping(lineText);
                }
                else
                {
                    runtimeView.SetText(lineText);
                }

                return;
            }

            if (currentChoice != null)
            {
                RefreshChoiceNodeLocalizedText(currentChoice, restartRevealIfTyping: true);
            }
        }

        private void RefreshChoiceNodeLocalizedText(ChoiceNode choiceNode, bool restartRevealIfTyping)
        {
            if (choiceNode == null || runtimeView == null)
            {
                return;
            }

            var promptText = ResolveVariablesInText(ResolveLocalizedText(choiceNode.textLocaleKey, choiceNode.text));
            runtimeView.ShowChoicePrompt(promptText);

            if (restartRevealIfTyping && isTyping)
            {
                SafeStopTyping();
                StartTyping(promptText);
            }
            else if (IsChoiceOverlayActive())
            {
                ShowChoices(choiceNode);
            }
        }

        public string GetCurrentGuid() => currentGuid;
        #endregion

        private void BindLocalizationRuntimeEvents()
        {
            var localizationRuntime = DialogLocalizationRuntime.Instance;
            localizationRuntime.OnLocaleChanged -= RefreshVisibleLocalizedText;
            localizationRuntime.OnLocaleChanged += RefreshVisibleLocalizedText;
        }

        private void UnbindLocalizationRuntimeEvents()
        {
            DialogLocalizationRuntime.Instance.OnLocaleChanged -= RefreshVisibleLocalizedText;
        }

        private DialogHistoryController CreateHistoryController()
        {
            return new DialogHistoryController(
                () => isTyping,
                () => conversationActive,
                SafeStopTyping,
                CancelAutoAdvance,
                RevealCurrentLineTextForHistory,
                HandleAfterTyping,
                TryResumeAutoPlayIfSafe,
                GetDialogGraphById);
        }

        private DialogRevealController CreateRevealController()
        {
            return new DialogRevealController(
                this,
                () => runtimeView?.DialogueTextTarget,
                () => localTextSettings,
                () => dialogTextSettings,
                () => dialogInputSettings,
                () => doDebug,
                HandleRevealComplete,
                c => typewriterAudioController?.OnCharRevealed(c));
        }

        private DialogAutoplayController CreateAutoplayController()
        {
            return new DialogAutoplayController(
                this,
                AutoAdvanceDelay,
                CanAutoPlayProgress,
                () => currentGuid,
                GoTo,
                CompleteConversationFlow);
        }

        private void HandleRevealComplete()
        {
            isTyping = false;
            HandleAfterTyping();
        }

        private void RevealCurrentLineTextForHistory()
        {
            runtimeView?.SetText(GetCurrentLineText());
        }

        #region ---------------- Effective Settings ----------------
        private void ResolveEffectiveSettings()
        {
            dialogTextSettings = localTextSettings != null ? localTextSettings : DialogSettingsRuntime.Text;
            if (dialogTextSettings == null)
            {
                dialogTextSettings = ScriptableObject.CreateInstance<DialogTextSettings>();
                runtimeLogger.VerboseWarning("settings-text-missing", "[DialogManager] No TextSettings found. Created temporary default instance.");
            }

            dialogAudioSettings = localAudioSettings != null ? localAudioSettings : DialogSettingsRuntime.Audio;
            if (dialogAudioSettings == null)
            {
                dialogAudioSettings = ScriptableObject.CreateInstance<DialogAudioSettings>();
                runtimeLogger.VerboseWarning("settings-audio-missing", "[DialogManager] No AudioSettings found. Created temporary default instance.");
            }

            dialogueUiSettings = localUiSettings != null ? localUiSettings : DialogSettingsRuntime.UI;
            if (dialogueUiSettings == null)
            {
                dialogueUiSettings = ScriptableObject.CreateInstance<DialogueRuntimeUISettings>();
                runtimeLogger.VerboseWarning("settings-ui-missing", "[DialogManager] No UISettings found. Created temporary default instance.");
            }

            DialogRuntimeSettingsPersistence.ApplyAudioOverrides(dialogAudioSettings);
            DialogRuntimeSettingsPersistence.ApplyTextOverrides(dialogTextSettings);

            // Choice settings from master
            choiceSettings = DialogSettingsRuntime.Master != null
                ? DialogSettingsRuntime.Master.choiceSettings
                : null;

            if (choiceSettings == null)
            {
                choiceSettings = ScriptableObject.CreateInstance<DialogChoiceSettings>();
                runtimeLogger.VerboseWarning("settings-choice-missing", "[DialogManager] No ChoiceSettings found. Created temporary default instance.");
            }

            dialogInputSettings = DialogSettingsRuntime.Input;
            if (dialogInputSettings == null)
            {
                dialogInputSettings = ScriptableObject.CreateInstance<DialogInputSettings>();
                runtimeLogger.VerboseWarning("settings-input-missing", "[DialogManager] No InputSettings found. Created temporary default instance.");
            }

        }

        private void EnsureVoiceAudioSource()
        {
            if (audioSource != null)
            {
                return;
            }

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
        }

        private bool TryResolveDialogUi(DialogThemeType themeType)
        {
            // When both direct references are missing, warn before the fallback
            // search (EnsureDialogUiManager → FindFirst) picks up a stale
            // component leaked from another test or scene.
            if (EnsureDialogUiManager())
            {
                dialogUIController = dialogUIManager.GetOrCreateDialogUI(themeType);
                runtimeView = dialogUIController;
                if (dialogUIController == null)
                {
                    runtimeView = null;
                    runtimeLogger.WarnOnce(
                        $"ui-manager-no-controller:{themeType}",
                        $"[DialogManager] DialogUIManager could not provide a dialog UI for theme '{themeType}'.");
                    return false;
                }

                return true;
            }

            if (themeType != DialogThemeType.Default)
            {
                runtimeLogger.WarnOnce(
                    $"ui-manager-theme-missing:{themeType}",
                    $"[DialogManager] Theme '{themeType}' was requested, but no DialogUIManager is available. Dialog startup requires a valid DialogUIManager instance.");
            }

            if (dialogUIController == null)
            {
                runtimeView = null;
                runtimeLogger.WarnOnce("ui-manager-missing", "[DialogManager] No DialogUIManager is assigned or instantiated, and no dialog UI controller is available.");
                return false;
            }

            runtimeView = dialogUIController;
            return true;
        }

        private bool EnsureDialogUiManager()
        {
            if (dialogUIManager == null)
            {
                dialogUIManager = DialogRuntimeUnityCompatibility.FindFirst<DialogUIManager>(includeInactive: true);
                // Discard stale/destroyed references that FindFirst may return
                // after DestroyImmediate in tests.
                if ((object)dialogUIManager != null && dialogUIManager == null)
                    dialogUIManager = null;
            }

            if (dialogUIManager != null && !IsRuntimeInstance(dialogUIManager))
            {
                var managerAsset = dialogUIManager;
                dialogUIManager = Instantiate(managerAsset);
                dialogUIManager.name = managerAsset.name;
            }

            if (dialogUIManager == null && dialogUIManagerPrefab != null)
            {
                dialogUIManager = Instantiate(dialogUIManagerPrefab);
                dialogUIManager.name = dialogUIManagerPrefab.name;
            }

            if (dialogUIManager == null)
            {
                dialogUIController = null;
                runtimeView = null;
                return false;
            }

            if (!dialogUIManager.Initialize(this))
            {
                dialogUIController = null;
                runtimeView = null;
                return false;
            }

            dialogUIController = dialogUIManager.CurrentDialogUI;
            runtimeView = dialogUIController;
            return dialogUIController != null;
        }

        private static bool IsRuntimeInstance(Component component)
        {
            return component != null &&
                   component.gameObject != null &&
                   component.gameObject.scene.IsValid();
        }

        private bool CanSkipCurrentLine()
        {
            return localTextSettings != null
                ? localTextSettings.allowSkipCurrentLine
                : dialogTextSettings?.allowSkipCurrentLine ?? true;
        }

        private bool CanSkipAll()
        {
            // block any input while actions are resolving
            if (_isWaitingForActions) return false;

            return localTextSettings != null
                ? localTextSettings.allowSkipAll
                : dialogTextSettings?.allowSkipAll ?? true;
        }

        private bool CanSkipLine()
        {
            return CanSkipCurrentLine();
        }

        private float AutoAdvanceDelay()
        {
            return localTextSettings != null
                ? localTextSettings.autoAdvanceDelay
                : dialogTextSettings?.autoAdvanceDelay ?? 1.0f;
        }

        public float GetCurrentCps(bool isHoldingFastForward)
        {
            return RevealController.GetCurrentCps(isHoldingFastForward);
        }

        public bool GetAutoPlayState() => AutoPlayController.IsEnabled;

        private void SetAutoPlayState(bool isEnabled)
        {
            AutoPlayController.SetEnabled(isEnabled);

            runtimeView?.SetAutoPlayActive(GetAutoPlayState());

            if (GetAutoPlayState())
                TryResumeAutoPlayIfSafe();
        }

        private bool ShouldStopOnSkipLine()
        {
            return AudioController.ShouldStopOnSkipLine();
        }

        private bool ShouldStopOnSkipAll()
        {
            return AudioController.ShouldStopOnSkipAll();
        }

        private bool ShouldFadeOutOnStop()
        {
            return AudioController.ShouldFadeOutOnStop();
        }
        #endregion

        #region ---------------- Internal ----------------
        private bool warnedNoRunner = false;
        private void TransitionToState(DialogueRuntimeState nextState)
        {
            if (runtimeStateMachine.TryTransition(nextState, out var error))
            {
                return;
            }

            runtimeLogger.InvalidStateTransition(error);
        }

        private void WarnOnceNoRunner()
        {
            if (warnedNoRunner) return;
            warnedNoRunner = true;
            runtimeLogger.WarnOnce("action-runner-missing", "[DialogManager] No DialogActionRunner assigned. Action calls will be ignored.");
        }

        private void FailConversationSafely(string message)
        {
            runtimeLogger.WarnOnce($"conversation-failed:{message}", message);
            TransitionToState(DialogueRuntimeState.Failed);

            if (conversationActive)
            {
                EndDialog();
            }
            else
            {
                ResetRuntimeState(preserveBranchHistory: false, clearDialogSession: true);
            }
        }

        private void RecordOutcomeResult(OutcomeNode outcome)
        {
            if (outcome == null)
            {
                return;
            }

            _lastCompletedOutcomeNode = outcome;
            _lastEndResult = new DialogueEndResult
            {
                ConversationId = currentDialogID,
                OutcomeId = outcome.outcomeId ?? string.Empty,
                OutcomeDisplayName = outcome.ResolvedDisplayName,
                OutcomeDescription = outcome.description ?? string.Empty
            };
        }

        private void ResetRuntimeState(bool preserveBranchHistory, bool clearDialogSession, bool allowAudioFadeOut = false, bool clearEndResult = true)
        {
            SafeStopTyping();
            CancelAutoAdvance();

            CancelAudioFade();

            try { StopAllCoroutines(); } catch { }

            if (allowAudioFadeOut && ShouldFadeOutOnStop())
                StopAudio(true);
            else
                StopAudioImmediate();

            conversationActive = false;
            runtimeStateMachine.Reset();
            HistoryController.ResetRuntimeState(preserveBranchHistory, currentDialogID);
            _isWaitingForActions = false;
            _genericCheckTimer = 0f;
            _hasPortraitSideState = false;
            _portraitOnRight = false;
            _lastPortraitSideSpeaker = string.Empty;

            currentGraph = null;
            dialogTraversalController.Clear();
            currentGuid = null;
            currentDialog = null;
            currentChoice = null;
            pendingChoiceFromDialog = null;
            pendingNextGuidAfterDialog = null;
            graphJumpReturnStack.Clear();
            _hasReturnedFromGraphJump = false;

            ChoiceNavigation.Reset();

            if (dialogUIController != null)
            {
                runtimeView?.ResetTransientState(GetAutoPlayState());
            }

            if (clearDialogSession)
            {
                currentDialogID = null;
                OnDialogEndedCallback = null;
            }

            if (clearEndResult)
            {
                _lastEndResult = default;
            }
        }

        private void TryResumeAutoPlayIfSafe()
        {
            if (!GetAutoPlayState() || !CanAutoPlayProgress() || _isWaitingForActions || isTyping)
                return;

            if (currentDialog == null || currentChoice != null || pendingChoiceFromDialog != null || IsChoiceOverlayActive())
                return;

            if (!string.IsNullOrEmpty(pendingNextGuidAfterDialog))
            {
                ScheduleAutoAdvance(pendingNextGuidAfterDialog, currentDialog);
                return;
            }

            if (string.IsNullOrEmpty(GetNextFromDialog(currentDialog.GetGuid())))
                ScheduleAutoEnd(currentDialog);
        }

        private bool CanAutoPlayProgress()
        {
            return conversationActive && !HistoryController.IsPaused;
        }

        #endregion
    }
}
