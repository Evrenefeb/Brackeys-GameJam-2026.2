using DialogSystem.Runtime.Models.Nodes;
using System;
using System.Collections;
using UnityEngine;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Owns autoplay state and delayed dialog progression.
    /// </summary>
    internal sealed class DialogAutoplayController
    {
        private readonly MonoBehaviour coroutineHost;
        private readonly Func<float> resolveDefaultDelay;
        private readonly Func<bool> canProgress;
        private readonly Func<string> resolveCurrentGuid;
        private readonly Action<string> advanceToGuid;
        private readonly Action endDialog;

        private Coroutine autoAdvanceCoroutine;

        public DialogAutoplayController(
            MonoBehaviour coroutineHost,
            Func<float> resolveDefaultDelay,
            Func<bool> canProgress,
            Func<string> resolveCurrentGuid,
            Action<string> advanceToGuid,
            Action endDialog)
        {
            this.coroutineHost = coroutineHost;
            this.resolveDefaultDelay = resolveDefaultDelay;
            this.canProgress = canProgress;
            this.resolveCurrentGuid = resolveCurrentGuid;
            this.advanceToGuid = advanceToGuid;
            this.endDialog = endDialog;
        }

        public bool IsEnabled { get; private set; }

        public void SetInitialState(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        public bool Toggle()
        {
            SetEnabled(!IsEnabled);
            return IsEnabled;
        }

        public void SetEnabled(bool isEnabled)
        {
            IsEnabled = isEnabled;

            if (!IsEnabled)
                Cancel();
        }

        public void Cancel()
        {
            if (autoAdvanceCoroutine == null)
                return;

            coroutineHost.StopCoroutine(autoAdvanceCoroutine);
            autoAdvanceCoroutine = null;
        }

        public void ScheduleAdvance(string nextGuid, DialogNode nodeForTiming)
        {
            if (string.IsNullOrEmpty(nextGuid))
                return;

            Cancel();
            autoAdvanceCoroutine = coroutineHost.StartCoroutine(AutoAdvanceAfterDelay(nextGuid, nodeForTiming));
        }

        public void ScheduleEnd(DialogNode nodeForTiming)
        {
            Cancel();
            autoAdvanceCoroutine = coroutineHost.StartCoroutine(AutoEndAfterDelay(nodeForTiming));
        }

        private IEnumerator AutoAdvanceAfterDelay(string nextGuid, DialogNode nodeForTiming)
        {
            var expectedGuid = ResolveExpectedGuid(nodeForTiming);
            yield return new WaitForSeconds(ResolveDelay(nodeForTiming));
            autoAdvanceCoroutine = null;

            if (!CanProgress(expectedGuid))
                yield break;

            if (!string.IsNullOrEmpty(nextGuid))
                advanceToGuid?.Invoke(nextGuid);
        }

        private IEnumerator AutoEndAfterDelay(DialogNode nodeForTiming)
        {
            var expectedGuid = ResolveExpectedGuid(nodeForTiming);
            yield return new WaitForSeconds(ResolveDelay(nodeForTiming));
            autoAdvanceCoroutine = null;

            if (!CanProgress(expectedGuid))
                yield break;

            endDialog?.Invoke();
        }

        private string ResolveExpectedGuid(DialogNode nodeForTiming)
        {
            return nodeForTiming != null ? nodeForTiming.GetGuid() : resolveCurrentGuid?.Invoke();
        }

        private float ResolveDelay(DialogNode nodeForTiming)
        {
            return nodeForTiming == null || nodeForTiming.displayTime < 0.01f
                ? resolveDefaultDelay?.Invoke() ?? 1.0f
                : nodeForTiming.displayTime;
        }

        private bool CanProgress(string expectedGuid)
        {
            if (!IsEnabled || canProgress?.Invoke() != true)
                return false;

            return string.IsNullOrEmpty(expectedGuid) || resolveCurrentGuid?.Invoke() == expectedGuid;
        }
    }
}
