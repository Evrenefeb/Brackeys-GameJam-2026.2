using System;
using UnityEngine;

namespace DialogSystem.Runtime.Models
{
    /// <summary>
    /// Stable serialized reference to another dialog graph.
    /// Asset references are optional so imports and older graphs remain loadable.
    /// </summary>
    [Serializable]
    public sealed class GraphReference
    {
        [Tooltip("Optional direct asset reference to the target graph.")]
        public DialogGraph graphAsset;

        [Tooltip("Stable graph GUID for the target graph, when known.")]
        public string graphGuid;

        [Tooltip("Optional runtime dialog ID used by DialogManager's dialog registry.")]
        public string runtimeDialogId;

        [Tooltip("Cached graph asset name for editor tooling and import/export.")]
        public string graphName;

        [Tooltip("Optional asset path cache for editor tooling and import/export.")]
        public string assetPath;

        [Tooltip("Optional target entry GUID inside the referenced graph. Leave blank to use that graph's Start boundary.")]
        public string entryGuid;

        public bool HasReference =>
            graphAsset != null ||
            !string.IsNullOrWhiteSpace(runtimeDialogId) ||
            !string.IsNullOrWhiteSpace(graphGuid) ||
            !string.IsNullOrWhiteSpace(graphName) ||
            !string.IsNullOrWhiteSpace(assetPath);

        public void CopyIdentityFrom(DialogGraph graph)
        {
            graphAsset = graph;
            graphGuid = graph != null ? graph.GraphGuid : string.Empty;
            graphName = graph != null ? graph.name : string.Empty;

            if (graph != null && string.IsNullOrWhiteSpace(entryGuid))
            {
                entryGuid = graph.startGuid;
            }
        }
    }
}
