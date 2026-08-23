using UnityEngine;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Models;

namespace DialogSystem.Runtime.Settings.Panels
{
    /// <summary>
    /// Master settings that references all sub-settings (created as sub-assets).
    /// </summary>
    [CreateAssetMenu(fileName = "DialogSystemSettings", menuName = "Beka Forge/Dialogues/Settings/System Settings", order = 0)]
    public class DialogSystemSettings : ScriptableObject
    {
        #region ---------------- Inspector ----------------
        [Header("References")]
        public DialogTextSettings textSettings;
        public DialogChoiceSettings choiceSettings;
        public DialogInputSettings inputSettings;
        public DialogAudioSettings audioSettings;
        public DialogueRuntimeUISettings uiSettings;

        [Header("Meta / Debug")]
        public string version = DialogGraph.CurrentVersionString;

        [Tooltip("Controls runtime console logging. WarningsOnly is the recommended production default.")]
        public DialogRuntimeLogMode runtimeLogMode = DialogRuntimeLogMode.WarningsOnly;

        [Tooltip("Legacy shortcut for verbose runtime diagnostics.")]
        public bool enableDebugLogs = false;
        #endregion
    }
}