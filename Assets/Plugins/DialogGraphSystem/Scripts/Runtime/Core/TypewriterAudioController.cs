using DialogSystem.Runtime.Settings.Panels;
using UnityEngine;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Plays per-character typewriter audio during text reveal.
    /// All throttle, whitespace, rich-text, and pitch randomization logic lives here
    /// so <see cref="DialogAudioController"/> and the reveal effects stay focused.
    /// </summary>
    internal sealed class TypewriterAudioController
    {
        #region ---------------- Fields ----------------

        private readonly MonoBehaviour _coroutineHost;

        // Dedicated AudioSource so typewriter sounds never interrupt the line voice-over.
        private AudioSource _source;

        private DialogAudioSettings _settings;

        // Counts visible characters revealed since the last sound was played.
        private int _charsSinceLastSound;

        // Time stamp of the last sound played (real time, unaffected by Time.timeScale pausing).
        private float _lastSoundTime;

        // Tracks whether we are currently inside a rich-text tag during sequential reveal.
        private bool _insideTag;

        #endregion

        #region ---------------- Ctor ----------------

        public TypewriterAudioController(MonoBehaviour coroutineHost)
        {
            _coroutineHost = coroutineHost;
        }

        #endregion

        #region ---------------- Bind ----------------

        /// <summary>
        /// Binds or rebinds the dedicated <see cref="AudioSource"/> and settings asset.
        /// Call this once from <see cref="DialogManager.Awake"/> or whenever settings change.
        /// </summary>
        public void Bind(AudioSource dedicatedSource, DialogAudioSettings settings)
        {
            _source = dedicatedSource;
            _settings = settings;
        }

        #endregion

        #region ---------------- Lifecycle ----------------

        /// <summary>
        /// Resets per-line counters. Call at the start of every new text reveal.
        /// </summary>
        public void OnRevealStart()
        {
            _charsSinceLastSound = 0;
            _lastSoundTime = -1f;
            _insideTag = false;
        }

        /// <summary>
        /// Stops any playing typewriter sound immediately. Call on skip or line end.
        /// </summary>
        public void Stop()
        {
            if (_source != null && _source.isPlaying)
                _source.Stop();
        }

        #endregion

        #region ---------------- Per-character callback ----------------

        /// <summary>
        /// Invoked by the reveal effect each time a character is about to become visible.
        /// Decides whether to fire an audio cue based on settings constraints.
        /// </summary>
        /// <param name="c">The character that was just revealed.</param>
        public void OnCharRevealed(char c)
        {
            if (_settings == null || !_settings.enableTypewriterAudio)
                return;

            if (_source == null)
                return;

            if (!HasTypewriterClip())
                return;

            // Track rich-text tag boundaries
            if (c == '<') { _insideTag = true; }
            if (_settings.typewriterIgnoreRichTextTags && _insideTag)
            {
                if (c == '>') _insideTag = false;
                return;
            }
            if (c == '>') { _insideTag = false; }

            // Ignore whitespace if requested
            if (_settings.typewriterIgnoreWhitespace && char.IsWhiteSpace(c))
                return;

            _charsSinceLastSound++;

            // Only play every N visible characters
            if (_charsSinceLastSound < _settings.typewriterPlayEveryNChars)
                return;

            // Enforce minimum time between sounds (prevents spam at high CPS)
            float now = Time.unscaledTime;
            if (_lastSoundTime >= 0f && (now - _lastSoundTime) < _settings.typewriterMinInterval)
                return;

            PlayTypewriterSound();

            _charsSinceLastSound = 0;
            _lastSoundTime = now;
        }

        #endregion

        #region ---------------- Internals ----------------

        private void PlayTypewriterSound()
        {
            var volume = Mathf.Clamp01(_settings.sfxVolume) * Mathf.Clamp01(_settings.typewriterVolume);
            if (volume <= 0f)
            {
                return;
            }

            var clip = ResolveTypewriterClip();
            if (clip == null)
                return;

            _source.pitch = 1f + Random.Range(-_settings.typewriterPitchVariance, _settings.typewriterPitchVariance);
            _source.PlayOneShot(clip, volume);
        }

        private bool HasTypewriterClip()
        {
            return ResolveTypewriterClip() != null;
        }

        private AudioClip ResolveTypewriterClip()
        {
            if (_settings.typewriterClip != null)
            {
                return _settings.typewriterClip;
            }

            var legacyClips = _settings.legacyTypewriterClips;
            if (legacyClips == null)
            {
                return null;
            }

            for (var i = 0; i < legacyClips.Count; i++)
            {
                if (legacyClips[i] != null)
                {
                    return legacyClips[i];
                }
            }

            return null;
        }

        #endregion
    }
}
