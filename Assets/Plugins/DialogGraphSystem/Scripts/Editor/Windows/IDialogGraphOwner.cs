using System;
using System.Collections.Generic;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements.Nodes;
using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Variables;
using UnityEngine;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Contract that both <see cref="DialogGraphEditorWindow"/> and
    /// <see cref="DialogSystem.EditorTools.Windows.Tabs.GraphTabController"/> satisfy
    /// so that <see cref="EditorSidebarWindow"/> can work with either host.
    /// </summary>
    public interface IDialogGraphOwner
    {
        /// <summary>Returns the live graph view.</summary>
        DialogGraphView GetGraphView();

        /// <summary>Returns the graph name currently loaded, or empty string.</summary>
        string GetCurrentGraphName();

        /// <summary>Loads the graph asset for the current graph ID.</summary>
        DialogGraph LoadCurrentGraphAsset(bool createIfMissing = false);

        /// <summary>Returns the currently selected dialog node, or null.</summary>
        DialogNodeView GetSelectedDialogNode();

        /// <summary>Collapses or expands the sidebar.</summary>
        void ToggleSidebar();

        /// <summary>Applies character name/sprite edits back to matching dialog nodes.</summary>
        void ApplySpritesToNodes(List<DialogGraphEditorWindow.CharacterBinding> bindings);

        /// <summary>Applies action-ID and payload edits back to matching action nodes.</summary>
        void ApplyActionsToNodes(List<DialogGraphEditorWindow.ActionBinding> bindings);

        /// <summary>Collects unique speaker names from the current graph view.</summary>
        IEnumerable<string> CollectSpeakersFromNodes();

        /// <summary>Returns the first portrait sprite found for a speaker name.</summary>
        Sprite FindFirstSpriteForSpeaker(string speaker);

        /// <summary>Returns all action nodes in the current graph view.</summary>
        IEnumerable<ActionNodeView> CollectActionNodes();

        /// <summary>Returns all action IDs used in the current graph view.</summary>
        IEnumerable<string> CollectActionIdsFromNodes();

        /// <summary>Modifies the current graph's context asset.</summary>
        void UpdateGraphContext(string undoLabel, Action<DialogGraph> applyChanges);

        /// <summary>Tries to assign a registered character to the selected dialog node.</summary>
        bool TryAssignRegisteredCharacterToSelectedDialogNode(DialogCharacterSO character, out string error);

        /// <summary>Tries to insert a registered action node after the selected node.</summary>
        bool TryInsertRegisteredActionAfterSelectedNode(DialogActionSO action, out string error);

        /// <summary>Tries to rewrite the currently selected dialog node through the AI flow.</summary>
        bool TryRewriteSelectedDialogNode(
            string customPrompt,
            string desiredTone,
            string instructionPreset,
            out string error);

        /// <summary>Refreshes the graph view and related editor panels.</summary>
        void RefreshGraphEditorState();

        /// <summary>Runs the graph layout formatter.</summary>
        void FormatLayout(string preserveNodeGuid = null);

        /// <summary>Scans graph action nodes and creates any missing action SO assets.</summary>
        int RegisterMissingActionAssetsFromCurrentGraph(out List<string> createdActionIds);

        /// <summary>Assigns registered action assets to the graph's Available Actions list.</summary>
        int AutofillAvailableActionsFromCurrentGraph(out List<string> missingActionIds, out List<string> assignedActionIds);

        /// <summary>Runs graph validation and surfaces the result panel.</summary>
        void ValidateGraph();

        /// <summary>Selects and frames the node with the given GUID in the graph view.</summary>
        bool FocusNodeByGuid(string guid);

        /// <summary>Creates a registered character asset.</summary>
        DialogCharacterSO CreateCharacterAsset(string displayName, Sprite portrait = null);

        /// <summary>Creates a registered action asset.</summary>
        DialogActionSO CreateActionAsset(
            string actionId,
            string payloadJson = "{}",
            bool waitForCompletion = true,
            float delay = 0f,
            bool pingAsset = true);

        /// <summary>Creates a registered variable asset.</summary>
        DialogVariableSO CreateVariableAsset(
            string variableKey,
            DialogueVariableValueType valueType = DialogueVariableValueType.Boolean,
            string defaultValue = "",
            bool pingAsset = true);

        /// <summary>Creates a registered environment asset.</summary>
        DialogEnvironmentSO CreateEnvironmentAsset(string displayName, bool pingAsset = true);

        /// <summary>Creates a registered scene context asset.</summary>
        DialogSceneContextSO CreateSceneContextAsset(
            string displayName,
            DialogEnvironmentSO environment = null,
            bool pingAsset = true);

        /// <summary>Returns the current AI instruction text for context building.</summary>
        string GetAiSidebarPrompt();

        /// <summary>Returns the current AI tone override for context building.</summary>
        string GetAiSidebarTone();

        /// <summary>Returns the current AI instruction preset for context building.</summary>
        string GetAiSidebarInstructionPreset();
    }
}
