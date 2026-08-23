using DialogSystem.EditorTools.Windows;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Onboarding
{
    /// <summary>
    /// Shows the welcome window once per configured startup cycle after the editor loads.
    /// </summary>
    [InitializeOnLoad]
    internal static class DialogWelcomeStartup
    {
        #region ---------------- Constants ----------------

        private const string StartupAttemptedSessionKey = "DialogSystem.Welcome.StartupAttempted";
        // Testing switches: flip these while iterating on the welcome flow.
        private static readonly bool DisableWelcomePopupDuringStartup = false;
        private static readonly bool ForceShowWelcomePopupDuringStartup = false;

        #endregion

        #region ---------------- Init ----------------

        static DialogWelcomeStartup()
        {
            EditorApplication.delayCall += TryShowWelcome;
        }

        #endregion

        #region ---------------- Flow ----------------

        private static void TryShowWelcome()
        {
            if (SessionState.GetBool(StartupAttemptedSessionKey, false))
            {
                return;
            }

            if (DisableWelcomePopupDuringStartup)
            {
                SessionState.SetBool(StartupAttemptedSessionKey, true);
                return;
            }

            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(StartupAttemptedSessionKey, true);
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryShowWelcome;
                return;
            }

            SessionState.SetBool(StartupAttemptedSessionKey, true);

            var branding = DialogWelcomeBrandingEditorService.LoadContent();
            if (!ForceShowWelcomePopupDuringStartup &&
                !DialogWelcomeBrandingEditorService.ShouldAutoShow(branding))
            {
                return;
            }

            DialogWelcomeWindow.Open(branding, true);
        }

        #endregion
    }
}
