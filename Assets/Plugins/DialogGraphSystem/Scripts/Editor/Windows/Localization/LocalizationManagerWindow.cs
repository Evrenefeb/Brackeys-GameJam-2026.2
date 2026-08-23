using DialogSystem.Runtime.Utils;
using DialogSystem.EditorTools.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Legacy standalone host for the embedded localization manager.
    /// </summary>
    public class LocalizationManagerWindow : EditorWindow
    {
        [System.Obsolete("Use DialogSystemMainWindow Localization tab instead.")]
        public static void Open()
        {
            DialogSystemMainWindow.OpenLocalizationTab();
        }

        internal static void OpenLegacyStandalone()
        {
            var win = GetWindow<LocalizationManagerWindow>("Localization Manager");
            win.minSize = new Vector2(900, 560);
            win.Show();
        }

        private void OnEnable()
        {
            var mainSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (mainSs != null && !rootVisualElement.styleSheets.Contains(mainSs))
            {
                rootVisualElement.styleSheets.Add(mainSs);
            }

            var locSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.LOCALIZATION_MANAGER_STYLE_PATH);
            if (locSs != null && !rootVisualElement.styleSheets.Contains(locSs))
            {
                rootVisualElement.styleSheets.Add(locSs);
            }

            rootVisualElement.Clear();
            var embedded = new LocalizationManagerEmbedded();
            embedded.style.flexGrow = 1;
            rootVisualElement.Add(embedded);
        }
    }
}
