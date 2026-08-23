using DialogSystem.EditorTools.ExportImport;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows.Tabs
{
    /// <summary>
    /// Import/Export tab in <see cref="DialogSystemMainWindow"/>.
    /// Embeds the JSON Import/Export UI directly in the tab content area.
    /// Opens <see cref="DialogJsonIOWindow"/> in a docked panel for the full experience.
    /// </summary>
    public class ImportExportTabController
    {
        #region ---------------- State ----------------

        private VisualElement _root;

        #endregion

        #region ---------------- Public API ----------------

        /// <summary>Builds the Import/Export tab content into the given root.</summary>
        public void BuildUI(VisualElement root)
        {
            _root = root;
            _root.Clear();

            // Load all relevant stylesheets to ensure brand tokens and card styles are available
            var settingsSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.SETTINGS_STYLE_PATH);
            if (settingsSs != null && !_root.styleSheets.Contains(settingsSs))
                _root.styleSheets.Add(settingsSs);

            var mainSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.MAIN_WINDOW_STYLE_PATH);
            if (mainSs != null && !_root.styleSheets.Contains(mainSs))
                _root.styleSheets.Add(mainSs);

            var graphSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (graphSs != null && !_root.styleSheets.Contains(graphSs))
                _root.styleSheets.Add(graphSs);

            var embedded = new ImportExportEmbedded();
            embedded.style.flexGrow = 1;
            _root.Add(embedded);
        }

        #endregion
    }
}
