using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Utils;
using UnityEditor;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Centralizes editor-only graph asset path rules so the release path can
    /// prefer the primary graph folder while still reading legacy assets.
    /// </summary>
    public static class DialogGraphAssetPaths
    {
        private static List<string> _visibleGraphNamesCache;
        private static List<string> _visibleGraphAssetPathsCache;

        private static readonly HashSet<string> HiddenLegacyGraphNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "DemoActionDialog",
            "DialogDemo",
            "DialogDemo_Copy",
            "NewDialogGraph"
        };

        static DialogGraphAssetPaths()
        {
            EditorApplication.projectChanged += InvalidateVisibleGraphCache;
        }

        public static string GetPrimaryGraphAssetPath(string graphName)
        {
            return CombineAssetPath(TextResources.GRAPHS_FOLDER, $"{graphName}.asset");
        }

        public static string ResolveGraphAssetPath(string graphName)
        {
            var primaryPath = GetPrimaryGraphAssetPath(graphName);
            if (AssetDatabase.LoadAssetAtPath<DialogGraph>(primaryPath) != null)
            {
                return primaryPath;
            }

            var legacyPath = CombineAssetPath(TextResources.CONVERSATION_FOLDER, $"{graphName}.asset");
            if (AssetDatabase.LoadAssetAtPath<DialogGraph>(legacyPath) != null)
            {
                return legacyPath;
            }

            var discoveredPath = GetVisibleGraphAssetPaths()
                .FirstOrDefault(path =>
                    string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        graphName,
                        StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(discoveredPath))
            {
                return discoveredPath;
            }

            return primaryPath;
        }

        public static DialogGraph LoadGraphAsset(string graphName)
        {
            return AssetDatabase.LoadAssetAtPath<DialogGraph>(ResolveGraphAssetPath(graphName));
        }

        public static List<string> GetVisibleGraphNames()
        {
            if (_visibleGraphNamesCache != null)
            {
                return new List<string>(_visibleGraphNamesCache);
            }

            _visibleGraphNamesCache = GetVisibleGraphAssetPaths()
                .Select(Path.GetFileNameWithoutExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new List<string>(_visibleGraphNamesCache);
        }

        public static List<string> GetVisibleGraphAssetPaths()
        {
            if (_visibleGraphAssetPathsCache != null)
            {
                return new List<string>(_visibleGraphAssetPathsCache);
            }

            _visibleGraphAssetPathsCache = AssetDatabase.FindAssets("t:DialogGraph", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => !IsHiddenLegacyGraphPath(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new List<string>(_visibleGraphAssetPathsCache);
        }

        public static List<DialogGraph> LoadAllGraphAssets()
        {
            return GetVisibleGraphAssetPaths()
                .Select(AssetDatabase.LoadAssetAtPath<DialogGraph>)
                .Where(asset => asset != null)
                .ToList();
        }

        public static void InvalidateVisibleGraphCache()
        {
            _visibleGraphNamesCache = null;
            _visibleGraphAssetPathsCache = null;
        }

        public static void EnsurePrimaryGraphFolderExists()
        {
            EnsureFolder(TextResources.GRAPHS_FOLDER);
        }

        /// <summary>
        /// Ensures any arbitrary folder path (relative to the project root, must start with "Assets")
        /// exists in the asset database, creating intermediate folders as needed.
        /// </summary>
        public static void EnsureFolderExists(string assetFolderPath)
        {
            EnsureFolder(assetFolderPath);
        }

        private static bool IsHiddenLegacyGraphPath(string assetPath)
        {
            var normalizedLegacyFolder = NormalizeFolder(TextResources.CONVERSATION_FOLDER);
            if (!assetPath.StartsWith(normalizedLegacyFolder + "/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(assetPath, normalizedLegacyFolder, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var graphName = Path.GetFileNameWithoutExtension(assetPath);
            return HiddenLegacyGraphNames.Contains(graphName);
        }

        private static string CombineAssetPath(string folder, string fileWithExt)
        {
            return $"{NormalizeFolder(folder)}/{fileWithExt.TrimStart('/')}";
        }

        private static string NormalizeFolder(string folder)
        {
            return NormalizePath(folder).TrimEnd('/');
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static void EnsureFolder(string folder)
        {
            var normalized = NormalizeFolder(folder);
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var parts = normalized.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Graph folder must live under Assets. Received '{folder}'.");
            }

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    InvalidateVisibleGraphCache();
                }

                current = next;
            }
        }
    }
}
