using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DialogSystem.Runtime.Settings.Panels
{
    /// <summary>
    /// Voice/SFX volumes, UI SFX clips, stop/fade behavior, and per-letter typewriter audio.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogAudioSettings", menuName = "Beka Forge/Dialogues/Settings/Audio Settings")]
    public class DialogAudioSettings : ScriptableObject
    {
        #region ---------------- Volumes ----------------
        [Header("Volumes")]
        [Range(0f, 1f)] public float voiceVolume = 0.85f;
        [Range(0f, 1f)] public float sfxVolume = 0.90f;
        #endregion

        #region ---------------- UI SFX ----------------
        [Header("UI SFX")]
        public bool enableUiSfx = true;
        public AudioClip sfxNavigate;
        public AudioClip sfxConfirm;
        public AudioClip sfxSkip;
        #endregion

        #region ---------------- Stop & Fade ----------------
        [Header("Stop & Fade Behaviour")]
        [Tooltip("If the player skips a typing line, stop any playing line audio.")]
        public bool stopOnSkipLine = true;

        [Tooltip("If the player skips the entire conversation, stop audio immediately.")]
        public bool stopOnSkipAll = true;

        [Tooltip("Fade out audio when stopping instead of cutting instantly.")]
        public bool fadeOutOnStop = true;

        [Tooltip("Fade-out time in seconds when stopping audio with fade.")]
        [Range(0f, 1f)] public float fadeOutTime = 0.08f;
        #endregion

        #region ---------------- Typewriter Audio ----------------
        [Header("Typewriter Audio")]
        [Tooltip("Play a sound for each revealed character during typewriter effect.")]
        public bool enableTypewriterAudio = false;

        [Tooltip("Clip played for each revealed character.")]
        public AudioClip typewriterClip;

        [FormerlySerializedAs("typewriterClips")]
        [HideInInspector] public List<AudioClip> legacyTypewriterClips = new List<AudioClip>();

        [Tooltip("Play a sound every N visible characters. 1 = every character.")]
        [Min(1)] public int typewriterPlayEveryNChars = 1;

        [Tooltip("Volume of the per-character typewriter sound.")]
        [Range(0f, 1f)] public float typewriterVolume = 0.5f;

        [Tooltip("Randomly shift pitch by ±this amount each sound. 0 = no randomization.")]
        [Range(0f, 0.5f)] public float typewriterPitchVariance = 0.05f;

        [Tooltip("Minimum seconds that must pass between two typewriter sounds, regardless of speed. Prevents audio spam when CPS is very high.")]
        [Min(0f)] public float typewriterMinInterval = 0.02f;

        [Tooltip("Skip typewriter sound when the revealed character is whitespace (space, tab, newline).")]
        public bool typewriterIgnoreWhitespace = true;

        [Tooltip("Skip typewriter sound when the revealed character is inside a rich-text tag (e.g. <b>, <color=…>).")]
        public bool typewriterIgnoreRichTextTags = true;
        #endregion

        private void OnValidate()
        {
            if (typewriterClip == null && legacyTypewriterClips != null)
            {
                for (var i = 0; i < legacyTypewriterClips.Count; i++)
                {
                    if (legacyTypewriterClips[i] != null)
                    {
                        typewriterClip = legacyTypewriterClips[i];
                        break;
                    }
                }
            }
        }
    }
}
