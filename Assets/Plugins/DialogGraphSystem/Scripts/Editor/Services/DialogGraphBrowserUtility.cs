using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogSystem.Runtime.Models;

namespace DialogSystem.EditorTools.Services
{
    public enum DialogGraphBrowserScopeKind
    {
        All,
        Uncategorized,
        Category,
        Folder
    }

    public sealed class DialogGraphBrowserFilters
    {
        public string SearchQuery = string.Empty;
        public string CategoryPath = string.Empty;
        public string FolderPath = string.Empty;
        public bool FavoritesOnly;
        public bool RecentOnly;
        public bool UncategorizedOnly;
    }

    public sealed class DialogGraphBrowserRecord
    {
        public string GraphName = string.Empty;
        public string DisplayTitle = string.Empty;
        public string AssetPath = string.Empty;
        public string FolderPath = string.Empty;
        public string Description = string.Empty;
        public string Author = string.Empty;
        public string PrimaryCategory = string.Empty;
        public List<string> Categories = new();
        public bool IsFavorite;
        public bool IsRecent;
        public DateTime LastModifiedUtc;
    }

    public static class DialogGraphBrowserUtility
    {
        public static string GetDisplayTitle(DialogGraph graph, string fallbackName = null)
        {
            if (graph == null)
            {
                return fallbackName ?? string.Empty;
            }

            var title = graph.graphTitle?.Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return string.IsNullOrWhiteSpace(fallbackName) ? graph.name : fallbackName;
        }

        public static string NormalizeCategoryPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace('\\', '/').Trim();
            while (normalized.IndexOf("//", StringComparison.Ordinal) >= 0)
            {
                normalized = normalized.Replace("//", "/");
            }

            var segments = normalized
                .Trim('/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim())
                .Where(segment => !string.IsNullOrWhiteSpace(segment));

            return string.Join("/", segments);
        }

        public static string GetCategoryLeafName(string value)
        {
            var normalized = NormalizeCategoryPath(value);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash >= 0 ? normalized.Substring(lastSlash + 1) : normalized;
        }

        public static int GetCategoryDepth(string value)
        {
            var normalized = NormalizeCategoryPath(value);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
            }

            return normalized.Count(character => character == '/');
        }

        public static string NormalizeFolderPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            var normalized = assetPath.Replace('\\', '/').Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(Path.GetExtension(normalized)))
            {
                return normalized;
            }

            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash <= 0 ? normalized : normalized.Substring(0, lastSlash);
        }

        public static int GetFolderDepth(string folderPath)
        {
            var normalized = NormalizeFolderPath(folderPath);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
            }

            return Math.Max(0, normalized.Count(character => character == '/') - 1);
        }

        public static IReadOnlyList<string> CollectAssignedCategories(string primaryCategory, IEnumerable<string> categories)
        {
            var combined = new List<string>();
            var normalizedPrimary = NormalizeCategoryPath(primaryCategory);
            if (!string.IsNullOrEmpty(normalizedPrimary))
            {
                combined.Add(normalizedPrimary);
            }

            if (categories != null)
            {
                combined.AddRange(categories.Select(NormalizeCategoryPath));
            }

            return combined
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<string> NormalizeCategoryList(IEnumerable<string> categories)
        {
            return CollectAssignedCategories(string.Empty, categories).ToList();
        }

        public static bool MatchesFilters(DialogGraphBrowserRecord item, DialogGraphBrowserFilters filters)
        {
            if (item == null)
            {
                return false;
            }

            filters ??= new DialogGraphBrowserFilters();
            var assignedCategories = CollectAssignedCategories(item.PrimaryCategory, item.Categories);

            if (filters.FavoritesOnly && !item.IsFavorite)
            {
                return false;
            }

            if (filters.RecentOnly && !item.IsRecent)
            {
                return false;
            }

            if (filters.UncategorizedOnly && assignedCategories.Count > 0)
            {
                return false;
            }

            var normalizedCategory = NormalizeCategoryPath(filters.CategoryPath);
            if (!string.IsNullOrEmpty(normalizedCategory) &&
                !assignedCategories.Any(category => MatchesHierarchicalValue(category, normalizedCategory)))
            {
                return false;
            }

            var normalizedFolder = NormalizeFolderPath(filters.FolderPath);
            if (!string.IsNullOrEmpty(normalizedFolder) &&
                !MatchesHierarchicalValue(NormalizeFolderPath(item.FolderPath), normalizedFolder))
            {
                return false;
            }

            var query = filters.SearchQuery?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            var haystack = string.Join("\n", new[]
            {
                item.GraphName,
                item.DisplayTitle,
                item.AssetPath,
                item.FolderPath,
                item.Description,
                item.Author,
                item.PrimaryCategory,
                string.Join(" ", item.Categories ?? new List<string>())
            });

            return haystack.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesHierarchicalValue(string value, string selectedValue)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(selectedValue))
            {
                return false;
            }

            if (string.Equals(value, selectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return value.StartsWith(selectedValue + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
