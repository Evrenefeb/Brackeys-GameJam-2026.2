using System;
using System.Collections.Generic;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Models;

namespace DialogSystem.EditorTools.Services
{
    public sealed class DialogLocalizationAiRequestEntry
    {
        public string Key;
        public string SourceText;
        public string Context;
    }

    public sealed class DialogLocalizationAiRequest
    {
        public DialogGraph Graph;
        public DialogLocalizationTable TargetTable;
        public string TargetLocaleCode;
        public string ScopeLabel;
        public IReadOnlyList<DialogLocalizationAiRequestEntry> Entries;
        public Action<string> OnCompleted;
        public Action OnCancelled;
    }

    /// <summary>
    /// Optional extension-owned bridge for AI-assisted localization from the core
    /// localization manager UI.
    /// </summary>
    public interface IDialogLocalizationAiBridge
    {
        bool IsAvailable { get; }

        bool TryTranslate(DialogLocalizationAiRequest request, out string error);

        void OpenSettings();
    }
}
