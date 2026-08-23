using System.Collections;
using System;
using System.Text;
using TMPro;
using UnityEngine;
using DialogSystem.Runtime.Interfaces;

namespace DialogSystem.Runtime.Core.Effects
{
    /// <summary>Reveals text word-by-word. (Basic split; rich-text tags should be pre-baked into _line.)</summary>
    public sealed class WordRevealEffect : ITextRevealEffect
    {
        private readonly string _line;
        private readonly TMP_Text _target;
        private readonly Func<float> _getWordsPerSecond;
        public bool IsCancelled { get; private set; }

        public WordRevealEffect(string line, TMP_Text target, Func<float> getWordsPerSecond)
        {
            _line = line ?? string.Empty;
            _target = target;
            _getWordsPerSecond = getWordsPerSecond ?? (() => 5f);
        }

        public void Cancel() => IsCancelled = true;

        public void CompleteImmediately()
        {
            if (_target) _target.text = _line;
        }

        public IEnumerator Play()
        {
            if (_target == null) yield break;

            var words = _line.Split(' ');
            var sb = new StringBuilder(words.Length * 6);
            _target.text = string.Empty;

            int shown = 0;
            float acc = 0f;

            while (!IsCancelled && shown < words.Length)
            {
                float wps = Mathf.Max(0.1f, _getWordsPerSecond());
                acc += Time.deltaTime * wps;

                while (!IsCancelled && acc >= 1f && shown < words.Length)
                {
                    acc -= 1f;
                    if (shown == 0) sb.Append(words[shown]);
                    else { sb.Append(' '); sb.Append(words[shown]); }
                    _target.text = sb.ToString();
                    shown++;
                }
                yield return null;
            }

            if (IsCancelled) { CompleteImmediately(); yield break; }
            // Manager decides what happens next.
        }
    }
}
