namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Holds the currently registered AI bridge, if the paid extension is installed.
    /// </summary>
    public static class DialogGraphAiBridgeLocator
    {
        public static IDialogGraphAiBridge Current { get; set; }
    }
}
