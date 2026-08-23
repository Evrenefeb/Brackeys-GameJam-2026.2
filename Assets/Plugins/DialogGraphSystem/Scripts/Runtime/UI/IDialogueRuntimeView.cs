using System;
using System.Collections.Generic;
using DialogSystem.Runtime.Settings.Panels;
using TMPro;
using UnityEngine;

namespace DialogSystem.Runtime.UI
{
    /// <summary>
    /// Minimal runtime presentation surface consumed by DialogManager.
    /// </summary>
    public interface IDialogueRuntimeView
    {
        TMP_Text DialogueTextTarget { get; }
        Transform ChoicesRoot { get; }

        void ApplySettings(DialogueRuntimeUISettings settings);
        void SetVisible(bool visible);
        void SetSkipAllVisible(bool visible);
        void SetAutoPlayActive(bool isActive);
        void ShowDialogueLine(DialogueLinePresentation line);
        void ShowChoicePrompt(string promptText);
        void SetSpeaker(string speaker);
        void SetText(string text);
        void SetPortraitSide(bool portraitOnRight);
        void RebuildChoices(IReadOnlyList<DialogueChoicePresentation> choices, DialogChoiceSettings settings, Action<int> onPick);
        void SetChoicesVisible(bool visible);
        void ClearChoices();
        void ResetTransientState(bool autoPlayActive);
    }
}
