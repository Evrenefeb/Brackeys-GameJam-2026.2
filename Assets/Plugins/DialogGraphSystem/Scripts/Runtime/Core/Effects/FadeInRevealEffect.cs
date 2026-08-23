using System.Collections;
using System;
using TMPro;
using UnityEngine;
using DialogSystem.Runtime.Interfaces;

namespace DialogSystem.Runtime.Core.Effects
{
    /// <summary>Fades the whole line from alpha 0 → 1 over a duration.</summary>
    public sealed class FadeInRevealEffect : ITextRevealEffect
    {
        private readonly string _line;
        private readonly TMP_Text _target;
        private readonly Func<float> _getDuration;
        public bool IsCancelled { get; private set; }

        public FadeInRevealEffect(string line, TMP_Text target, Func<float> getDuration)
        {
            _line = line ?? string.Empty;
            _target = target;
            _getDuration = getDuration ?? (() => 1.5f);
        }

        public void Cancel() => IsCancelled = true;

        public void CompleteImmediately()
        {
            if (_target == null) return;
            _target.text = _line;
            var c = _target.color; c.a = 1f; _target.color = c;
        }

        public IEnumerator Play()
        {
            if (_target == null) yield break;

            _target.text = _line;
            var dur = Mathf.Max(0.01f, _getDuration());
            float t = 0f;

            var c = _target.color; c.a = 0f; _target.color = c;

            while (!IsCancelled && t < dur)
            {
                t += Time.deltaTime;
                c.a = Mathf.Clamp01(t / dur);
                _target.color = c;
                yield return null;
            }

            if (IsCancelled) { CompleteImmediately(); yield break; }
            c.a = 1f; _target.color = c;
            // Manager decides what happens next.
        }
    }
}
