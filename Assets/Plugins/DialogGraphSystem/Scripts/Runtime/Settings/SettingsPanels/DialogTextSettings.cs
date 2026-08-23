using UnityEngine;

namespace DialogSystem.Runtime.Settings.Panels
{
    /// <summary>
    /// Text, typewriter effect, flow, and pacing.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogTextSettings", menuName = "Beka Forge/Dialogues/Settings/Text Settings")]
    public class DialogTextSettings : ScriptableObject
    {
        #region ---------------- Inspector ----------------
        [Header("Typewriter")]
        [Tooltip("Visual effect used to reveal dialog text.")]
        public TypewriterEffect typewriterEffect = TypewriterEffect.Typing;

        [Tooltip("Characters per second when using a progressive effect (e.g. Typing).")]
        [Range(1f, 120f)] public float charsPerSecond = 35f;

        [Header("Skip")]
        [Tooltip("Allow skipping and ending the entire conversation.")]
        public bool allowSkipAll = true;

        [Tooltip("Allow user input to reveal the current line instantly.")]
        public bool allowSkipCurrentLine = true;

        [Header("Fast-Forward")]
        [Tooltip("Holding the fast-forward key increases the typewriter speed.")]
        public bool allowFastForwardHold = true;

        [Tooltip("Multiplier applied to chars-per-second while fast-forward is held.")]
        [Range(1f, 8f)] public float fastForwardMultiplier = 3f;

        [Header("Auto Advance")]
        [Tooltip("If enabled, nodes advance automatically after display time.")]
        public bool autoAdvance = false;

        [Tooltip("Delay after a line finishes before auto-advancing when node has no displayTime.")]
        public float autoAdvanceDelay = 0.75f;

        [Header("Punctuation Pauses (seconds)")]
        public float commaPause = 0.08f;
        public float periodPause = 0.16f;
        public float questionPause = 0.18f;
        public float exclamationPause = 0.18f;
        #endregion

        public override string ToString()
        {
            return $"[DialogTextSettings: typewriterEffect={typewriterEffect}," +
                $" charsPerSecond={charsPerSecond}, allowSkipAll={allowSkipAll}," +
                $" allowSkipCurrentLine={allowSkipCurrentLine}, allowFastForwardHold={allowFastForwardHold}," +
                $" fastForwardMultiplier={fastForwardMultiplier}, autoAdvance={autoAdvance}, autoAdvanceDelay={autoAdvanceDelay}," +
                $" commaPause={commaPause}, periodPause={periodPause}, questionPause={questionPause}," +
                $" exclamationPause={exclamationPause}]";
        }
    }

    [System.Serializable]
    public enum TypewriterEffect
    {
        None,       // No animation (renders instantly)
        Typing,     // Classic per-character reveal
        WordByWord,       // (Future) Smooth fade per character
        FadeIn     // (Future) Scale bounce effect
    }
}
