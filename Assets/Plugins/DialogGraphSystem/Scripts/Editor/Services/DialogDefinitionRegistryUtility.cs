using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    internal static class DialogDefinitionRegistryUtility
    {
        private static readonly Dictionary<Type, object> AssetCache = new();

        static DialogDefinitionRegistryUtility()
        {
            EditorApplication.projectChanged += InvalidateAllCaches;
        }

        public static IReadOnlyList<TAsset> LoadAllAssets<TAsset>() where TAsset : ScriptableObject
        {
            if (AssetCache.TryGetValue(typeof(TAsset), out var cached) &&
                cached is IReadOnlyList<TAsset> typedCache)
            {
                return typedCache;
            }

            var guids = AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}");
            var assets = new List<TAsset>(guids.Length);
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path) || !seenPaths.Add(path))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            var orderedAssets = assets
                .OrderBy(asset => asset.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AssetCache[typeof(TAsset)] = orderedAssets;
            return orderedAssets;
        }

        public static TAsset FindById<TAsset>(
            Func<TAsset, string> idSelector,
            string id) where TAsset : ScriptableObject
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return LoadAllAssets<TAsset>()
                .FirstOrDefault(asset => string.Equals(
                    idSelector(asset)?.Trim(),
                    id.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        public static IReadOnlyList<string> FindDuplicateIds<TAsset>(
            IEnumerable<TAsset> assets,
            Func<TAsset, string> idSelector) where TAsset : ScriptableObject
        {
            if (assets == null)
            {
                return Array.Empty<string>();
            }

            return assets
                .Select(asset => idSelector(asset)?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static TAsset CreateAsset<TAsset>(
            string defaultFolder,
            string defaultFileName) where TAsset : ScriptableObject
        {
            EnsureFolderHierarchy(defaultFolder);

            var asset = ScriptableObject.CreateInstance<TAsset>();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{defaultFolder}/{defaultFileName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            InvalidateCache<TAsset>();
            return AssetDatabase.LoadAssetAtPath<TAsset>(path);
        }

        public static void InvalidateCache<TAsset>() where TAsset : ScriptableObject
        {
            AssetCache.Remove(typeof(TAsset));
        }

        private static void InvalidateAllCaches()
        {
            AssetCache.Clear();
        }

        public static void EnsureFolderHierarchy(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("Folder path cannot be empty.", nameof(folderPath));
            }

            var normalized = folderPath.Replace('\\', '/');
            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                throw new ArgumentException("Folder path is invalid.", nameof(folderPath));
            }

            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
