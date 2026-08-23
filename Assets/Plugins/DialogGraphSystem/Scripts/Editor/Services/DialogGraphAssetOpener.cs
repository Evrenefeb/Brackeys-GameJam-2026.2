using System;
using System.IO;
using System.Linq;
using DialogSystem.EditorTools.Windows;
using DialogSystem.Runtime.Models;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Handles double-click opens on <see cref="DialogGraph"/> assets in the Project window.
    /// Delegates to the unified main editor window and its Graph tab.
    /// </summary>
    internal static class DialogGraphAssetOpener
    {
        /// <summary>
        /// Called by Unity whenever the user double-clicks any asset.
        /// Returns <c>true</c> to consume the event (suppress default Inspector open),
        /// or <c>false</c> to let Unity handle non-DialogGraph assets normally.
        /// </summary>
        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            var asset = ResolveOpenedObject(instanceId) as DialogGraph;
            if (asset == null)
                return false;

            if (string.IsNullOrWhiteSpace(asset.name))
            {
                Debug.LogWarning("[DialogGraphAssetOpener] Cannot open a DialogGraph asset with an empty asset name.", asset);
                return false;
            }

            var selectedPath = AssetDatabase.GetAssetPath(asset);
            var resolvedPath = DialogGraphAssetPaths.ResolveGraphAssetPath(asset.name);
            var duplicateNameCount = DialogGraphAssetPaths.GetVisibleGraphAssetPaths()
                .Count(path => string.Equals(Path.GetFileNameWithoutExtension(path), asset.name, StringComparison.OrdinalIgnoreCase));

            if (duplicateNameCount > 1 && !string.Equals(selectedPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Duplicate Graph Name",
                    $"Multiple DialogGraph assets named '{asset.name}' exist. The graph editor opens graphs by name, so this asset could be confused with another graph.\n\nRename one of the duplicate assets, or open the currently resolved asset:\n{resolvedPath}",
                    "OK");
                Debug.LogWarning(
                    $"[DialogGraphAssetOpener] Refused to open duplicate-named graph '{asset.name}' at '{selectedPath}' because the editor would resolve '{resolvedPath}' instead.",
                    asset);
                return false;
            }

            DialogSystemMainWindow.OpenWithGraph(asset.name);
            return true;
        }

        private static UnityEngine.Object ResolveOpenedObject(int instanceId)
        {
            var entityIdToObject = typeof(EditorUtility).GetMethod(
                "EntityIdToObject",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new[] { typeof(int) },
                null);

            if (entityIdToObject != null)
            {
                return entityIdToObject.Invoke(null, new object[] { instanceId }) as UnityEngine.Object;
            }

#pragma warning disable CS0618
            return EditorUtility.EntityIdToObject(instanceId);
#pragma warning restore CS0618
        }
    }
}
