using DialogSystem.EditorTools.Localization;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows.Tabs
{
    /// <summary>
    /// Localization tab in <see cref="DialogSystemMainWindow"/>.
    /// Embeds the Localization Manager UI directly in the tab content area.
    /// </summary>
    public class LocalizationTabController
    {
        #region ---------------- State ----------------

        private VisualElement _root;

        // Embedded localization manager instance
        private LocalizationManagerEmbedded _embedded;
        private DialogGraph _pendingGraph;

        #endregion

        #region ---------------- Public API ----------------

        /// <summary>Builds the Localization tab content into the given root.</summary>
        public void BuildUI(VisualElement root)
        {
            _root = root;
            _root.Clear();

            var mainSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (mainSs != null && !_root.styleSheets.Contains(mainSs))
                _root.styleSheets.Add(mainSs);

            var locSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.LOCALIZATION_MANAGER_STYLE_PATH);
            if (locSs != null && !_root.styleSheets.Contains(locSs))
                _root.styleSheets.Add(locSs);

            // Use the embedded version of the localization manager
            _embedded = new LocalizationManagerEmbedded();
            _embedded.style.flexGrow = 1;
            _root.Add(_embedded);

            if (_pendingGraph != null)
            {
                _embedded.SelectGraph(_pendingGraph);
            }
        }

        public void SelectGraph(DialogGraph graph)
        {
            _pendingGraph = graph;
            _embedded?.SelectGraph(graph);
        }

        #endregion
    }
}
