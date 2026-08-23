using DialogSystem.Runtime.Core.Effects;
using DialogSystem.Runtime.Interfaces;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.Utils;
using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Handles dialog text reveal effect creation and coroutine lifecycle.
    /// </summary>
    internal sealed class DialogRevealController
    {
        private readonly MonoBehaviour coroutineHost;
        private readonly Func<TMP_Text> resolveTarget;
        private readonly Func<DialogTextSettings> resolveLocalSettings;
        private readonly Func<DialogTextSettings> resolveEffectiveSettings;
        private readonly Func<DialogInputSettings> resolveInputSettings;
        private readonly Func<bool> resolveDebug;
        private readonly Action onRevealComplete;
        private readonly Action<char> onCharRevealed;

        private Coroutine revealCoroutine;
        private ITextRevealEffect activeEffect;
        private int revealEpoch;

        public DialogRevealController(
            MonoBehaviour coroutineHost,
            Func<TMP_Text> resolveTarget,
            Func<DialogTextSettings> resolveLocalSettings,
            Func<DialogTextSettings> resolveEffectiveSettings,
            Func<DialogInputSettings> resolveInputSettings,
            Func<bool> resolveDebug,
            Action onRevealComplete,
            Action<char> onCharRevealed = null)
        {
            this.coroutineHost = coroutineHost;
            this.resolveTarget = resolveTarget;
            this.resolveLocalSettings = resolveLocalSettings;
            this.resolveEffectiveSettings = resolveEffectiveSettings;
            this.resolveInputSettings = resolveInputSettings;
            this.resolveDebug = resolveDebug;
            this.onRevealComplete = onRevealComplete;
            this.onCharRevealed = onCharRevealed;
        }

        public bool IsRevealing { get; private set; }

        public bool CanReveal => resolveTarget?.Invoke() != null;

        public void StartReveal(string line)
        {
            var target = resolveTarget?.Invoke();
            if (target == null)
                return;

            StopAndComplete();

            var effect = CreateRevealEffect(line, target);

            if (effect == null)
            {
                target.text = line ?? string.Empty;
                IsRevealing = false;
                onRevealComplete?.Invoke();
                return;
            }

            activeEffect = effect;
            IsRevealing = true;

            revealEpoch++;
            int epoch = revealEpoch;
            revealCoroutine = coroutineHost.StartCoroutine(RunEffect(effect, epoch));
        }

        public void StopAndComplete()
        {
            if (revealCoroutine != null)
            {
                coroutineHost.StopCoroutine(revealCoroutine);
                revealCoroutine = null;
            }

            if (activeEffect != null)
            {
                activeEffect.Cancel();
                activeEffect.CompleteImmediately();
                activeEffect = null;
            }

            IsRevealing = false;
        }

        public float GetCurrentCps(bool isHoldingFastForward)
        {
            var src = ResolveTextSettings();
            if (src == null) return 40f;
            if (src.typewriterEffect == TypewriterEffect.None) return float.MaxValue;

            float cps = Mathf.Max(1f, src.charsPerSecond);
            if (isHoldingFastForward && src.allowFastForwardHold)
                cps *= Mathf.Max(1f, src.fastForwardMultiplier);
            return cps;
        }

        private IEnumerator RunEffect(ITextRevealEffect effect, int epoch)
        {
            yield return effect.Play();

            if (effect.IsCancelled || epoch != revealEpoch)
                yield break;

            IsRevealing = false;
            activeEffect = null;
            revealCoroutine = null;

            onRevealComplete?.Invoke();
        }

        private ITextRevealEffect CreateRevealEffect(string line, TMP_Text target)
        {
            var type = ResolveTextSettings()?.typewriterEffect ?? TypewriterEffect.Typing;

            switch (type)
            {
                case TypewriterEffect.Typing:
                    return new TypingRevealEffect(
                        line,
                        target,
                        () => GetCurrentCps(InputHelper.IsFastForwardHeld(resolveInputSettings?.Invoke())),
                        PauseFor,
                        resolveDebug?.Invoke() == true,
                        onCharRevealed);

                case TypewriterEffect.WordByWord:
                    return new WordRevealEffect(
                        line,
                        target,
                        () => Mathf.Max(1f, ((resolveEffectiveSettings?.Invoke()?.charsPerSecond ?? 5f) / 5f)));

                case TypewriterEffect.FadeIn:
                    return new FadeInRevealEffect(
                        line,
                        target,
                        () => 1.5f);

                default:
                    return null;
            }
        }

        private float PauseFor(char c)
        {
            var src = ResolveTextSettings();
            if (src == null) return 0f;

            return c switch
            {
                ',' => src.commaPause,
                '.' => src.periodPause,
                '?' => src.questionPause,
                '!' => src.exclamationPause,
                _ => 0f
            };
        }

        private DialogTextSettings ResolveTextSettings()
        {
            return resolveLocalSettings?.Invoke() ?? resolveEffectiveSettings?.Invoke();
        }
    }
}
