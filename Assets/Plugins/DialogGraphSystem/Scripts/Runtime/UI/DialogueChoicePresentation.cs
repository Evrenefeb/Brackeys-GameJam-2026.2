namespace DialogSystem.Runtime.UI
{
    /// <summary>
    /// Resolved runtime data needed to render one selectable choice.
    /// </summary>
    public readonly struct DialogueChoicePresentation
    {
        public DialogueChoicePresentation(int index, string text, string subLabel = null, bool interactable = true)
        {
            Index = index;
            Text = text ?? string.Empty;
            SubLabel = subLabel ?? string.Empty;
            Interactable = interactable;
        }

        public int Index { get; }
        public string Text { get; }
        public string SubLabel { get; }
        public bool Interactable { get; }
    }
}
