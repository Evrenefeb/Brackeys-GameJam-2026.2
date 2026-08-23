using System;
using System.Collections.Generic;
using DialogSystem.EditorTools.Services;
using NUnit.Framework;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogGraphBrowserUtilityTests
    {
        [Test]
        public void NormalizeCategoryPath_CollapsesSeparatorsAndWhitespace()
        {
            var normalized = DialogGraphBrowserUtility.NormalizeCategoryPath(@"  Story\\Main Quest//Intro  ");

            Assert.That(normalized, Is.EqualTo("Story/Main Quest/Intro"));
        }

        [Test]
        public void CollectAssignedCategories_DeduplicatesPrimaryAndAdditionalValues()
        {
            var combined = DialogGraphBrowserUtility.CollectAssignedCategories(
                "Story/MainQuest",
                new[] { "Story/MainQuest", " Characters / Allies ", "Characters/Allies" });

            Assert.That(combined, Is.EqualTo(new[]
            {
                "Characters/Allies",
                "Story/MainQuest"
            }));
        }

        [Test]
        public void MatchesFilters_RespectsFavoritesRecentScopeFolderAndSearch()
        {
            var record = new DialogGraphBrowserRecord
            {
                GraphName = "CafeMorning",
                DisplayTitle = "Cafe Morning",
                AssetPath = "Assets/DialogGraphSystem/Graphs/Story/CafeMorning.asset",
                FolderPath = "Assets/DialogGraphSystem/Graphs/Story",
                Description = "Morning branch with vendor greeting.",
                Author = "Arjan",
                PrimaryCategory = "Story/MainQuest",
                Categories = new List<string> { "Locations/Cafe", "Characters/Vendor" },
                IsFavorite = true,
                IsRecent = true,
                LastModifiedUtc = new DateTime(2026, 05, 24, 8, 0, 0, DateTimeKind.Utc)
            };

            Assert.That(DialogGraphBrowserUtility.MatchesFilters(record, new DialogGraphBrowserFilters
            {
                FavoritesOnly = true,
                RecentOnly = true,
                CategoryPath = "Story",
                FolderPath = "Assets/DialogGraphSystem/Graphs",
                SearchQuery = "vendor"
            }), Is.True);

            Assert.That(DialogGraphBrowserUtility.MatchesFilters(record, new DialogGraphBrowserFilters
            {
                FavoritesOnly = true,
                CategoryPath = "Puzzles"
            }), Is.False);

            Assert.That(DialogGraphBrowserUtility.MatchesFilters(record, new DialogGraphBrowserFilters
            {
                FolderPath = "Assets/DialogGraphSystem/Graphs/Combat"
            }), Is.False);
        }

        [Test]
        public void MatchesFilters_UncategorizedOnlyRejectsAssignedCategories()
        {
            var uncategorizedRecord = new DialogGraphBrowserRecord
            {
                GraphName = "LooseGraph",
                DisplayTitle = "Loose Graph",
                AssetPath = "Assets/DialogGraphSystem/Graphs/LooseGraph.asset",
                FolderPath = "Assets/DialogGraphSystem/Graphs",
                Categories = new List<string>()
            };

            var categorizedRecord = new DialogGraphBrowserRecord
            {
                GraphName = "TaggedGraph",
                DisplayTitle = "Tagged Graph",
                AssetPath = "Assets/DialogGraphSystem/Graphs/TaggedGraph.asset",
                FolderPath = "Assets/DialogGraphSystem/Graphs",
                PrimaryCategory = "Story/SideQuest"
            };

            var filters = new DialogGraphBrowserFilters { UncategorizedOnly = true };

            Assert.That(DialogGraphBrowserUtility.MatchesFilters(uncategorizedRecord, filters), Is.True);
            Assert.That(DialogGraphBrowserUtility.MatchesFilters(categorizedRecord, filters), Is.False);
        }
    }
}
