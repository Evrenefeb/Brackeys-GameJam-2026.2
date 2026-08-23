using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime.Models
{
    /// <summary>
    /// Project-backed category catalog used by the editor asset browser.
    /// Categories are independent from Unity folders and graph tags.
    /// </summary>
    public sealed class DialogGraphCategoryCatalog : ScriptableObject
    {
        [Tooltip("Ordered category definitions used by the graph browser.")]
        public List<DialogGraphCategoryDefinition> categories = new();
    }

    [Serializable]
    public sealed class DialogGraphCategoryDefinition
    {
        [Tooltip("Slash-delimited category path, for example Story/MainQuest or Vendors/Blacksmith.")]
        public string path = string.Empty;

        [Tooltip("Color accent shown in the graph browser.")]
        public Color color = new(0.24f, 0.51f, 0.92f, 1f);
    }
}
