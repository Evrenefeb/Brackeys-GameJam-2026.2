using UnityEditor;
using UnityEngine.UIElements;
using DialogSystem.Runtime.Utils;

namespace DialogSystem.EditorTools.View
{
    /// <summary>
    /// Centralizes stylesheet loading for all node views.
    /// Loads the authoritative editor node stylesheets from
    /// Assets/DialogGraphSystem/Scripts/Editor/USS via AssetDatabase.
    /// </summary>
    internal static class NodeUssLoader
    {
        private static StyleSheet _base;
        private static StyleSheet _types;

        internal static void ApplyTo(VisualElement element)
        {
            if (_base == null)
                _base = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.NODE_BASE_STYLE_PATH);
            if (_types == null)
                _types = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.NODE_TYPES_STYLE_PATH);

            if (_base  != null && !element.styleSheets.Contains(_base))
                element.styleSheets.Add(_base);
            if (_types != null && !element.styleSheets.Contains(_types))
                element.styleSheets.Add(_types);
        }
    }
}
