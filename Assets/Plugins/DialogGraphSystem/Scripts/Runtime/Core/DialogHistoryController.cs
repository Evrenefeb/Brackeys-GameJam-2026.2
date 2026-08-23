using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Transcript;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Owns runtime history pause state, branch-path tracking, and transcript reconstruction.
    /// </summary>
    internal sealed class DialogHistoryController
    {
        private readonly Func<bool> isTyping;
        private readonly Func<bool> isConversationActive;
        private readonly Action stopTyping;
        private readonly Action cancelAutoAdvance;
        private readonly Action revealCurrentLineText;
        private readonly Action handleAfterTyping;
        private readonly Action tryResumeAutoPlay;
        private readonly Func<string, DialogGraph> resolveGraphById;

        private readonly List<int> currentBranchPath = new();
        private readonly Dictionary<string, List<int>> lastPlayedBranchByDialogId = new();
        private bool pendingPostTypingResolution;
        private int pauseDepth;

        public DialogHistoryController(
            Func<bool> isTyping,
            Func<bool> isConversationActive,
            Action stopTyping,
            Action cancelAutoAdvance,
            Action revealCurrentLineText,
            Action handleAfterTyping,
            Action tryResumeAutoPlay,
            Func<string, DialogGraph> resolveGraphById)
        {
            this.isTyping = isTyping;
            this.isConversationActive = isConversationActive;
            this.stopTyping = stopTyping;
            this.cancelAutoAdvance = cancelAutoAdvance;
            this.revealCurrentLineText = revealCurrentLineText;
            this.handleAfterTyping = handleAfterTyping;
            this.tryResumeAutoPlay = tryResumeAutoPlay;
            this.resolveGraphById = resolveGraphById;
        }

        public bool IsPaused => pauseDepth > 0;

        public void Pause()
        {
            pauseDepth++;
            if (pauseDepth > 1)
            {
                return;
            }

            if (isTyping?.Invoke() == true)
            {
                stopTyping?.Invoke();
                pendingPostTypingResolution = true;
                revealCurrentLineText?.Invoke();
            }

            cancelAutoAdvance?.Invoke();
        }

        public void Resume()
        {
            if (pauseDepth <= 0)
            {
                pauseDepth = 0;
                return;
            }

            pauseDepth--;
            if (pauseDepth > 0)
            {
                return;
            }

            if (isConversationActive?.Invoke() != true)
                return;

            if (pendingPostTypingResolution)
            {
                handleAfterTyping?.Invoke();
                return;
            }

            tryResumeAutoPlay?.Invoke();
        }

        public void ResetPauseState()
        {
            pauseDepth = 0;
            pendingPostTypingResolution = false;
        }

        public void ClearPendingPostTypingResolution()
        {
            pendingPostTypingResolution = false;
        }

        public void RecordChoiceIndex(int index)
        {
            currentBranchPath.Add(index);
        }

        public void ResetRuntimeState(bool preserveBranchHistory, string dialogId)
        {
            if (preserveBranchHistory && !string.IsNullOrWhiteSpace(dialogId))
                lastPlayedBranchByDialogId[dialogId] = new List<int>(currentBranchPath);

            ResetPauseState();
            currentBranchPath.Clear();
        }

        public bool TryGetLastPlayedBranchPath(string dialogId, out List<int> branchPath)
        {
            branchPath = null;

            if (string.IsNullOrWhiteSpace(dialogId))
                return false;

            if (!lastPlayedBranchByDialogId.TryGetValue(dialogId, out var storedPath) || storedPath == null)
                return false;

            branchPath = new List<int>(storedPath);
            return true;
        }

        public string GetLastPlayedDialogTranscript(string dialogId, bool doDebug)
        {
            if (!TryGetLastPlayedBranchPath(dialogId, out var branchPath))
                return string.Empty;

            if (doDebug)
                Debug.Log($"[DialogManager] Building transcript for dialog id '{dialogId}' with branch path: {string.Join(",", branchPath)}");

            var graph = resolveGraphById?.Invoke(dialogId);
            if (graph == null)
            {
                if (doDebug) Debug.LogWarning($"[DialogManager] Cannot build transcript. No graph found for dialog id '{dialogId}'.");
                return string.Empty;
            }

            if (DialogGraphTranscriptBuilder.TryBuildTranscript(
                    graph,
                    DialogGraphTranscriptBuildMode.SpecificBranchPath,
                    branchPath,
                    out var transcript,
                    out var error))
            {
                if (doDebug)
                    Debug.Log($"[DialogManager] Transcript for dialog id '{dialogId}':\n{transcript}");
                return transcript;
            }

            if (doDebug && !string.IsNullOrWhiteSpace(error))
                Debug.LogWarning($"[DialogManager] Failed to build transcript for dialog id '{dialogId}': {error}");

            return string.Empty;
        }
    }
}
