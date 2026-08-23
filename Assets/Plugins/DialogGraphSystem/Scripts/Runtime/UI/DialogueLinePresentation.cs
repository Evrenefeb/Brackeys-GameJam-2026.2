using UnityEngine;

namespace DialogSystem.Runtime.UI
{
    /// <summary>
    /// Resolved runtime data needed to present a dialogue line.
    /// </summary>
    public readonly struct DialogueLinePresentation
    {
        public DialogueLinePresentation(string speakerName, string text, Sprite portrait)
        {
            SpeakerName = speakerName ?? string.Empty;
            Text = text ?? string.Empty;
            Portrait = portrait;
        }

        public string SpeakerName { get; }
        public string Text { get; }
        public Sprite Portrait { get; }
    }
}
