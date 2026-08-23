using DialogSystem.EditorTools.AI;
using DialogSystem.Runtime.Definitions;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.Windows;

namespace DialogSystem.EditorTools.Services
{
    public enum DialogGraphAiQuickAction
    {
        ContinueBranch,
        SuggestChoices,
        RewriteDialog,
        GrammarFix,
        ValidateGraph,
    }

    /// <summary>
    /// Extension-owned bridge that injects paid AI UI into the free/core editor sidebar.
    /// </summary>
    public interface IDialogGraphAiBridge
    {
        bool IsAvailable { get; }

        VisualElement CreateAiSidebarContent(IDialogGraphOwner owner);

        bool TryRunQuickAction(
            IDialogGraphOwner owner,
            DialogGraphAiQuickAction action,
            out string error);

        bool CanRewriteText(out string reason);

        bool TryRewriteText(
            string nodeTitle,
            string speakerName,
            string originalText,
            string customPrompt,
            string desiredTone,
            string instructionPreset,
            DialogGraphAiContext context,
            out string rewrittenText,
            out string error);

        bool TryGeneratePayloadJson(
            string actionId,
            string currentPayload,
            DialogActionSO registeredAction,
            string payloadInstruction,
            out string payloadJson,
            out string error);

        void OpenSettings();
    }
}
