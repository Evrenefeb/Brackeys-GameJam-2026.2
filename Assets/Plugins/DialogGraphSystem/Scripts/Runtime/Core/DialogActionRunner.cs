using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Interfaces;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Runs dialogue-scoped actions (and optional global fallbacks).
    /// Supports:
    /// - Sync UnityEvent bindings per actionId.
    /// - Async handlers via <see cref="IActionHandler"/>, either fire-and-forget or awaited.
    /// - Global-only and per-dialogue convenience invocations.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogActionRunner : MonoBehaviour
    {
        #region -------- Inspector --------
        [Header("Global (shared across dialogue graphs)")]
        [Tooltip("Bindings and handlers applied to every dialogue when global fallback is enabled.")]
        public ConversationActionSet global = new ConversationActionSet { dialogueKey = "<global>" };

        [FormerlySerializedAs("conversations")]
        [Header("Per-dialogue sets (key = dialogID)")]
        [Tooltip("Bindings and handlers for a specific dialogID from DialogManager.")]
        public List<ConversationActionSet> dialogueSets = new List<ConversationActionSet>();

        [Tooltip("If true, use Global when a dialogue set doesn't contain the requested binding or handler.")]
        public bool useGlobalFallback = true;
        #endregion

        private readonly HashSet<string> warnedActionKeys = new HashSet<string>();

        #region -------- Backward-Compatible API --------
        [System.Obsolete("Use dialogueSets instead.")]
        public List<ConversationActionSet> conversations
        {
            get => dialogueSets;
            set => dialogueSets = value ?? new List<ConversationActionSet>();
        }
        #endregion

        #region -------- ActionNode entrypoint (used by DialogManager) --------
        /// <summary>
        /// Executes an <see cref="ActionNode"/>:
        /// - Optional pre-delay (<see cref="ActionNode.waitSeconds"/>).
        /// - Invokes UnityEvent binding(s) matching <see cref="ActionNode.actionId"/>.
        /// - Runs an async handler when available. If <see cref="ActionNode.waitForCompletion"/> is true, it waits.
        /// </summary>
        public IEnumerator RunAction(ActionNode node, string dialogueId = null)
        {
            if (node == null) yield break;

            if (string.IsNullOrWhiteSpace(node.actionId))
            {
                WarnMissingActionId(node.GetGuid(), dialogueId);
                yield break;
            }

            var payload = node.payloadJson ?? string.Empty;

            if (node.waitSeconds > 0f)
                yield return new WaitForSeconds(Mathf.Max(0f, node.waitSeconds));

            var dialogueSet = FindSet(dialogueId);

            // 1) Fire-and-forget UnityEvent binding(s)
            var invoked = false;
            if (dialogueSet != null) invoked |= TryInvokeBinding(dialogueSet, node.actionId, payload);
            if (!invoked && useGlobalFallback && global != null)
                invoked |= TryInvokeBinding(global, node.actionId, payload);

            // 2) Async handler, optionally awaited
            var handled = false;
            if (dialogueSet != null && TryHandleAsync(dialogueSet, node.actionId, payload, out var dialogueRoutine) && dialogueRoutine != null)
            {
                handled = true;
                if (node.waitForCompletion)
                {
                    yield return dialogueRoutine;
                    yield break;
                }

                StartCoroutine(dialogueRoutine);
            }
            else if (useGlobalFallback && global != null && TryHandleAsync(global, node.actionId, payload, out var globalRoutine) && globalRoutine != null)
            {
                handled = true;
                if (node.waitForCompletion)
                {
                    yield return globalRoutine;
                    yield break;
                }

                StartCoroutine(globalRoutine);
            }

            if (!invoked && !handled)
            {
                WarnMissingAction(node.actionId, dialogueId);
            }
        }
        #endregion

        #region -------- Convenience API --------
        /// <summary>
        /// Invokes an action only against the Global set (optionally waiting and/or delaying).
        /// </summary>
        public IEnumerator RunActionGlobal(string actionId, string payloadJson = "", bool waitForCompletion = false, float waitSeconds = 0f)
            => RunActionInternal(
                dialogId: null,
                actionId: actionId,
                payloadJson: payloadJson,
                waitForCompletion: waitForCompletion,
                waitSeconds: waitSeconds,
                forceGlobalOnly: true
            );

        /// <summary>
        /// Invokes an action for a specific dialogue (by dialogId). If not found and
        /// <see cref="useGlobalFallback"/> is true, it will try Global.
        /// </summary>
        public IEnumerator RunActionForDialogue(string dialogId, string actionId, string payloadJson = "", bool waitForCompletion = false, float waitSeconds = 0f)
            => RunActionInternal(
                dialogId: dialogId,
                actionId: actionId,
                payloadJson: payloadJson,
                waitForCompletion: waitForCompletion,
                waitSeconds: waitSeconds,
                forceGlobalOnly: false
            );

        [System.Obsolete("Use RunActionForDialogue instead.")]
        public IEnumerator RunActionForConversation(string dialogId, string actionId, string payloadJson = "", bool waitForCompletion = false, float waitSeconds = 0f)
            => RunActionForDialogue(dialogId, actionId, payloadJson, waitForCompletion, waitSeconds);
        #endregion

        #region -------- Internals --------
        private IEnumerator RunActionInternal(string dialogId, string actionId, string payloadJson, bool waitForCompletion, float waitSeconds, bool forceGlobalOnly)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                WarnMissingActionId("<direct-call>", forceGlobalOnly ? "<global>" : dialogId);
                yield break;
            }

            if (waitSeconds > 0f)
                yield return new WaitForSeconds(Mathf.Max(0f, waitSeconds));

            var payload = payloadJson ?? string.Empty;
            var set = forceGlobalOnly ? null : FindSet(dialogId);

            // 1) Sync invoke
            var invoked = false;
            if (!forceGlobalOnly && set != null)
                invoked = TryInvokeBinding(set, actionId, payload);

            if (!invoked && (forceGlobalOnly || useGlobalFallback) && global != null)
                invoked = TryInvokeBinding(global, actionId, payload);

            // 2) Async handler, optionally awaited
            var handled = false;
            if (!forceGlobalOnly && set != null && TryHandleAsync(set, actionId, payload, out var dialogueRoutine) && dialogueRoutine != null)
            {
                handled = true;
                if (waitForCompletion)
                {
                    yield return dialogueRoutine;
                    yield break;
                }

                StartCoroutine(dialogueRoutine);
            }
            else if ((forceGlobalOnly || useGlobalFallback) && global != null && TryHandleAsync(global, actionId, payload, out var globalRoutine) && globalRoutine != null)
            {
                handled = true;
                if (waitForCompletion)
                {
                    yield return globalRoutine;
                    yield break;
                }

                StartCoroutine(globalRoutine);
            }

            if (!invoked && !handled)
            {
                WarnMissingAction(actionId, forceGlobalOnly ? "<global>" : dialogId);
            }
        }

        private ConversationActionSet FindSet(string key)
        {
            if (string.IsNullOrEmpty(key) || dialogueSets == null) return null;
            for (int i = 0; i < dialogueSets.Count; i++)
            {
                var s = dialogueSets[i];
                if (s != null && string.Equals(s.dialogueKey, key, StringComparison.Ordinal))
                    return s;
            }
            return null;
        }

        private static bool TryInvokeBinding(ConversationActionSet set, string actionId, string payload)
        {
            if (set == null || set.bindings == null) return false;

            for (int i = 0; i < set.bindings.Count; i++)
            {
                var b = set.bindings[i];
                if (b != null && string.Equals(b.actionId, actionId, StringComparison.Ordinal))
                {
                    try
                    {
                        b.onInvoke?.Invoke(payload);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[DialogActionRunner] Exception in binding '{actionId}': {ex.Message}");
                    }
                    return true;
                }
            }
            return false;
        }

        private static bool TryHandleAsync(ConversationActionSet set, string actionId, string payload, out IEnumerator routine)
        {
            routine = null;
            if (set == null || set.handlers == null) return false;

            for (int i = 0; i < set.handlers.Count; i++)
            {
                var mb = set.handlers[i];
                if (mb != null && mb is IActionHandler handler && handler.CanHandle(actionId))
                {
                    routine = handler.Handle(actionId, payload);
                    return true;
                }
            }
            return false;
        }

        private void WarnMissingAction(string actionId, string dialogueId)
        {
            var key = $"missing:{dialogueId ?? string.Empty}:{actionId ?? string.Empty}";
            if (!warnedActionKeys.Add(key))
            {
                return;
            }

            Debug.LogWarning(
                $"[DialogActionRunner] No binding or handler matched action '{actionId}' for dialogue '{dialogueId ?? string.Empty}'.");
        }

        private void WarnMissingActionId(string nodeGuid, string dialogueId)
        {
            var key = $"empty:{dialogueId ?? string.Empty}:{nodeGuid ?? string.Empty}";
            if (!warnedActionKeys.Add(key))
            {
                return;
            }

            Debug.LogWarning($"[DialogActionRunner] Action node '{nodeGuid ?? "<unknown>"}' has an empty action ID and was skipped.");
        }
        #endregion
    }

    /// <summary>
    /// Group of bindings and handlers for a specific dialogue (key = dialogID).
    /// </summary>
    [Serializable]
    public class ConversationActionSet
    {
        [FormerlySerializedAs("conversationKey")]
        [Tooltip("Use the dialogID from DialogManager here.")]
        public string dialogueKey;

        [System.Obsolete("Use dialogueKey instead.")]
        public string conversationKey
        {
            get => dialogueKey;
            set => dialogueKey = value;
        }

        [Tooltip("UnityEvent bindings keyed by actionId (synchronous).")]
        public List<ActionBinding> bindings = new List<ActionBinding>();

        [Tooltip("MonoBehaviours implementing IActionHandler for async action execution.")]
        public List<MonoBehaviour> handlers = new List<MonoBehaviour>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (bindings == null) bindings = new List<ActionBinding>();
            if (handlers == null) handlers = new List<MonoBehaviour>();
        }
#endif
    }

    /// <summary>
    /// UnityEvent binding for a single action id.
    /// </summary>
    [Serializable]
    public class ActionBinding
    {
        [Header("Action")]
        [Tooltip("Must match ActionNode.actionId (or the id passed to the runner).")]
        public string actionId;

        [Header("UnityEvent Callback")]
        [Tooltip("Tip: To pass a constant string (e.g., JSON), select the NON-dynamic overload (shows “(String)”). Dynamic mode forwards the runtime payload.")]
        [InspectorName("On Invoke (string payload)")]
        public UnityEvent<string> onInvoke;
    }
}