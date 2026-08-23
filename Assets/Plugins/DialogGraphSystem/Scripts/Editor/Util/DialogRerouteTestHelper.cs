using UnityEditor;
using UnityEngine;
using System.Linq;
using DialogSystem.EditorTools.Windows;
using DialogSystem.EditorTools.View;
using DialogSystem.EditorTools.View.Elements;
using UnityEditor.Experimental.GraphView;

namespace DialogSystem.EditorTools.Util
{
#if UNITY_EDITOR && DGS_INCLUDE_DEV_TOOLS
    /// <summary>
    /// Development-only menu helpers for testing edge reroute metadata.
    /// Define DGS_INCLUDE_DEV_TOOLS to expose these menu items.
    /// </summary>
    public static class DialogRerouteTestHelper
    {
        [MenuItem("Dialogue Graph/Debug/Add Reroute Point to Selected Edge")]
        public static void AddReroutePoint()
        {
            var window = UnityEngine.Resources.FindObjectsOfTypeAll<DialogGraphEditorWindow>().FirstOrDefault();
            if (window == null)
            {
                Debug.LogWarning("[DialogRerouteTestHelper] No DialogGraphEditorWindow found.");
                return;
            }

            var graphView = window.GetGraphView();
            if (graphView == null)
            {
                Debug.LogWarning("[DialogRerouteTestHelper] No GraphView found in window.");
                return;
            }

            var selectedEdge = graphView.selection.OfType<DialogGraphEdge>().FirstOrDefault();
            if (selectedEdge == null)
            {
                Debug.LogWarning("[DialogRerouteTestHelper] No DialogGraphEdge selected.");
                return;
            }

            // Calculate a point in the middle of the ports
            Vector2 start = selectedEdge.output != null
                ? selectedEdge.output.ChangeCoordinatesTo(graphView.contentViewContainer, selectedEdge.output.layout.center)
                : Vector2.zero;
            Vector2 end = selectedEdge.input != null
                ? selectedEdge.input.ChangeCoordinatesTo(graphView.contentViewContainer, selectedEdge.input.layout.center)
                : Vector2.zero;
            Vector2 mid = (start + end) / 2f + new Vector2(0, 50); // Offset a bit so it's not a straight line

            selectedEdge.DEBUG_AddTestReroutePoint(mid);
            Debug.Log($"[DialogRerouteTestHelper] Added test reroute point at {mid} to edge {selectedEdge.linkGuid}");
        }
        
        [MenuItem("Dialogue Graph/Debug/Clear Reroute Points for Selected Edge")]
        public static void ClearReroutePoints()
        {
            var window = UnityEngine.Resources.FindObjectsOfTypeAll<DialogGraphEditorWindow>().FirstOrDefault();
            if (window == null) return;

            var graphView = window.GetGraphView();
            if (graphView == null) return;

            var selectedEdge = graphView.selection.OfType<DialogGraphEdge>().FirstOrDefault();
            if (selectedEdge == null) return;

            if (selectedEdge.Owner != null && !string.IsNullOrEmpty(selectedEdge.linkGuid))
            {
                var layout = selectedEdge.Owner.GetEdgeLayout(selectedEdge.linkGuid);
                if (layout != null)
                {
                    layout.reroutePoints.Clear();
                    EditorUtility.SetDirty(selectedEdge.Owner);
                    selectedEdge.UpdateEdgeControl();
                    Debug.Log("[DialogRerouteTestHelper] Cleared reroute points.");
                }
            }
        }
    }
#endif
}