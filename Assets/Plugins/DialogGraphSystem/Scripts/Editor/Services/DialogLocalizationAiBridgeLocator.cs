using DialogSystem.EditorTools.PublicInformation;
using UnityEditor;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Holds the optional AI bridge for localization manager translation actions.
    /// </summary>
    public static class DialogLocalizationAiBridgeLocator
    {
        public static IDialogLocalizationAiBridge Current { get; set; }

        public static bool IsAvailable => Current?.IsAvailable == true;

        public static void ShowUnavailableMessage()
        {
            var choice = EditorUtility.DisplayDialogComplex(
                "AI Translation Unavailable",
                $"{DialogPublicInformationCatalog.AiComingSoonTitle}. Manual translation stays fully available in Core 3.0.0.",
                "Learn More",
                "View Bundled Copy",
                "Not Now");
            if (choice == 0)
            {
                DialogPublicInformationCatalog.Open("ai-extension");
            }
            else if (choice == 1)
            {
                DialogPublicInformationCatalog.OpenBundled("ai-extension");
            }
        }
    }
}
