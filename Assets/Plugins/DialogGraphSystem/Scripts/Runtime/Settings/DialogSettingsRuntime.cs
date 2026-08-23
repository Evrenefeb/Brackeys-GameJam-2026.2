using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.Core;
using UnityEngine;

namespace DialogSystem.Runtime.Settings
{
    public static class DialogSettingsRuntime
    {
        private const string PrimaryResourcesPath = "DialogSettingsSO/DialogSystemSettings";
        private static DialogSystemSettings master;

        public static DialogSystemSettings Master
        {
            get
            {
                if (master == null)
                {
                    master = Resources.Load<DialogSystemSettings>(PrimaryResourcesPath);
#if UNITY_EDITOR
                    if (master == null)
                        Debug.LogWarning(
                            "[DialogSettingsRuntime] Could not load DialogSystemSettings at Resources path '" +
                            PrimaryResourcesPath + "'. Check the asset filename and location.");
#endif
                }
                return master;
            }
        }
        public static DialogRuntimeLogMode RuntimeLogMode => Master
            ? (Master.enableDebugLogs ? DialogRuntimeLogMode.Verbose : Master.runtimeLogMode)
            : DialogRuntimeLogMode.WarningsOnly;

        public static bool DebugLogsEnabled => Master == null || Master.enableDebugLogs;

        public static bool DoDebug() => RuntimeLogMode == DialogRuntimeLogMode.Verbose;
        public static DialogTextSettings Text => Master ? Master.textSettings : null;
        public static DialogChoiceSettings Choice => Master ? Master.choiceSettings : null;
        public static DialogInputSettings Input => Master ? Master.inputSettings : null;
        public static DialogAudioSettings Audio => Master ? Master.audioSettings : null;
        public static DialogueRuntimeUISettings UI => Master ? Master.uiSettings : null;

    }
}
