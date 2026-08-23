using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Models;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    public static class DialogGraphCategoryCatalogService
    {
        private const string CatalogFolder = "Assets/DialogGraphSystem/Definitions/GraphCategories";
        private const string CatalogAssetPath = CatalogFolder + "/DialogGraphCategoryCatalog.asset";

        private static readonly Color[] DefaultPalette =
        {
            new(0.24f, 0.51f, 0.92f, 1f),
            new(0.16f, 0.69f, 0.49f, 1f),
            new(0.92f, 0.53f, 0.18f, 1f),
            new(0.77f, 0.31f, 0.84f, 1f),
            new(0.84f, 0.23f, 0.33f, 1f),
            new(0.86f, 0.71f, 0.16f, 1f)
        };

        public static DialogGraphCategoryCatalog GetOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DialogGraphCategoryCatalog>(CatalogAssetPath);
            if (catalog != null)
            {
                EnsureCatalogIntegrity(catalog);
                return catalog;
            }

            DialogGraphAssetPaths.EnsureFolderExists(CatalogFolder);
            catalog = ScriptableObject.CreateInstance<DialogGraphCategoryCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        public static IReadOnlyList<DialogGraphCategoryDefinition> GetOrderedCategories()
        {
            var catalog = GetOrCreateCatalog();
            EnsureCatalogIntegrity(catalog);
            return catalog.categories
                .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.path))
                .OrderBy(definition => definition.path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static DialogGraphCategoryDefinition FindByPath(string categoryPath)
        {
            var normalized = DialogGraphBrowserUtility.NormalizeCategoryPath(categoryPath);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            return GetOrderedCategories().FirstOrDefault(definition =>
                string.Equals(definition.path, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static DialogGraphCategoryDefinition AddCategory(string leafName, string parentPath, Color color)
        {
            var normalizedLeaf = DialogGraphBrowserUtility.GetCategoryLeafName(leafName);
            if (string.IsNullOrWhiteSpace(normalizedLeaf))
            {
                return null;
            }

            var fullPath = CombineCategoryPath(parentPath, normalizedLeaf);
            var catalog = GetOrCreateCatalog();
            EnsureCatalogIntegrity(catalog);

            var existing = catalog.categories.FirstOrDefault(definition =>
                definition != null &&
                string.Equals(definition.path, fullPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            Undo.RecordObject(catalog, "Add Graph Category");
            var created = new DialogGraphCategoryDefinition
            {
                path = fullPath,
                color = color
            };
            catalog.categories.Add(created);
            SaveCatalog(catalog);
            return created;
        }

        public static bool RemoveCategory(string categoryPath)
        {
            var normalized = DialogGraphBrowserUtility.NormalizeCategoryPath(categoryPath);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            var catalog = GetOrCreateCatalog();
            EnsureCatalogIntegrity(catalog);

            var removed = false;
            Undo.RecordObject(catalog, "Remove Graph Category");
            for (var index = catalog.categories.Count - 1; index >= 0; index--)
            {
                var definition = catalog.categories[index];
                if (definition == null)
                {
                    catalog.categories.RemoveAt(index);
                    removed = true;
                    continue;
                }

                if (string.Equals(definition.path, normalized, StringComparison.OrdinalIgnoreCase) ||
                    definition.path.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase))
                {
                    catalog.categories.RemoveAt(index);
                    removed = true;
                }
            }

            if (removed)
            {
                SaveCatalog(catalog);
            }

            return removed;
        }

        public static void UpdateCategoryColor(string categoryPath, Color color)
        {
            var definition = FindByPath(categoryPath);
            if (definition == null)
            {
                return;
            }

            var catalog = GetOrCreateCatalog();
            Undo.RecordObject(catalog, "Update Graph Category Color");
            definition.color = color;
            SaveCatalog(catalog);
        }

        public static Color GetSuggestedColor(int index)
        {
            if (index < 0)
            {
                index = 0;
            }

            return DefaultPalette[index % DefaultPalette.Length];
        }

        public static string CombineCategoryPath(string parentPath, string leafName)
        {
            var normalizedLeaf = DialogGraphBrowserUtility.GetCategoryLeafName(leafName);
            var normalizedParent = DialogGraphBrowserUtility.NormalizeCategoryPath(parentPath);
            if (string.IsNullOrWhiteSpace(normalizedLeaf))
            {
                return normalizedParent;
            }

            return string.IsNullOrEmpty(normalizedParent)
                ? normalizedLeaf
                : normalizedParent + "/" + normalizedLeaf;
        }

        private static void EnsureCatalogIntegrity(DialogGraphCategoryCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            catalog.categories ??= new List<DialogGraphCategoryDefinition>();
            var changed = false;

            for (var index = catalog.categories.Count - 1; index >= 0; index--)
            {
                var definition = catalog.categories[index];
                if (definition == null)
                {
                    catalog.categories.RemoveAt(index);
                    changed = true;
                    continue;
                }

                var normalizedPath = DialogGraphBrowserUtility.NormalizeCategoryPath(definition.path);
                if (string.IsNullOrEmpty(normalizedPath))
                {
                    catalog.categories.RemoveAt(index);
                    changed = true;
                    continue;
                }

                if (!string.Equals(definition.path, normalizedPath, StringComparison.Ordinal))
                {
                    definition.path = normalizedPath;
                    changed = true;
                }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = catalog.categories.Count - 1; index >= 0; index--)
            {
                var definition = catalog.categories[index];
                if (!seen.Add(definition.path))
                {
                    catalog.categories.RemoveAt(index);
                    changed = true;
                }
            }

            if (changed)
            {
                SaveCatalog(catalog);
            }
        }

        private static void SaveCatalog(DialogGraphCategoryCatalog catalog)
        {
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }
    }
}
