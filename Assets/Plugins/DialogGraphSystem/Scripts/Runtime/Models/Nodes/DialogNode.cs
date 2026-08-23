using UnityEngine;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Standard dialog line with optional speaker, portrait, audio, and display time.
    /// </summary>
    public class DialogNode : BaseNode
    {
        #region -------- Speaker --------
        [Header("Speaker")]
        [Tooltip("Name shown as the speaker of this line.")]
        [SerializeField] public string speakerName;

        [Tooltip("Stable locale key for speakerName. Only needed when speaker names are inlined " +
                 "raw strings rather than resolved through a DialogCharacterSO. Leave blank otherwise.")]
        [SerializeField] public string speakerNameLocaleKey;

        [Tooltip("Portrait/avatar shown for the speaker.")]
        [SerializeField] public Sprite speakerPortrait;
        #endregion

        #region -------- Content --------
        [Header("Content")]
        [TextArea(2, 5)]
        [Tooltip("The main text shown to the player.")]
        [SerializeField] public string questionText;

        [Tooltip("Stable locale key for questionText, e.g. 'npc_001.line_003.text'. " +
                 "Used by DialogLocalizationRuntime to resolve the active language at runtime. " +
                 "Leave blank to display questionText directly (no localization).")]
        [SerializeField] public string questionTextLocaleKey;
        #endregion

        #region -------- Audio --------
        [Header("Audio")]
        [Tooltip("Optional voice-over or SFX for this node.")]
        [SerializeField] public AudioClip dialogAudio;
        #endregion

        #region -------- Flow --------
        [Tooltip("Seconds to show this node before auto-advancing. Use 0 to wait for input.")]
        [Min(0f)] public float displayTime = 0f;

        [Tooltip("If a dialogAudio clip is assigned, wait for it to finish playing before allowing the player to advance to the next node.")]
        public bool waitForAudioFinish = false;
        #endregion

        public DialogNode()
        {
            nodeKind = NodeKind.Dialog;
        }

        /// <summary>Returns true when a stable locale key is set for the main dialog text.</summary>
        public bool HasQuestionLocaleKey => !string.IsNullOrWhiteSpace(questionTextLocaleKey);

        /// <summary>Returns true when a stable locale key is set for the speaker name.</summary>
        public bool HasSpeakerLocaleKey => !string.IsNullOrWhiteSpace(speakerNameLocaleKey);
    }
}
