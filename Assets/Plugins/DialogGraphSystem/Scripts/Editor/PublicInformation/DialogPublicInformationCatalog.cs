using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.PublicInformation
{
    internal sealed class DialogPublicInformationEntry
    {
        public DialogPublicInformationEntry(string id, string title, string onlineUrl, string bundledAssetPath)
        {
            Id = id;
            Title = title;
            OnlineUrl = onlineUrl;
            BundledAssetPath = bundledAssetPath;
        }

        public string Id { get; }
        public string Title { get; }
        public string OnlineUrl { get; }
        public string BundledAssetPath { get; }
        public string BundledActionLabel => "View Bundled Copy";
    }

    internal sealed class DialogPublicInformationContent
    {
        public DialogPublicInformationContent(string content, bool isBundled, string statusMessage)
        {
            Content = content ?? string.Empty;
            IsBundled = isBundled;
            StatusMessage = statusMessage ?? string.Empty;
        }

        public string Content { get; }
        public bool IsBundled { get; }
        public string StatusMessage { get; }
    }

    internal static class DialogPublicInformationCatalog
    {
        internal const string AiComingSoonTitle = "AI Extension 2.0 — Coming Soon";
        internal const string AiLearnMoreUrl = "https://bekaforge.com/dgs-docs/ai-extension.html";

        private const string BundledRoot = "Assets/DialogGraphSystem/Documentation/Bundled";

        private static readonly DialogPublicInformationEntry[] Entries =
        {
            new("documentation", "Dialogue Graph System Documentation", "https://bekaforge.com/dgs-docs/", $"{BundledRoot}/DOCUMENTATION.txt"),
            new("changelog", "Dialogue Graph System Changelog", "https://bekaforge.com/dgs-docs/changelog.html", $"{BundledRoot}/CHANGELOG.txt"),
            new("license", "Dialogue Graph System License", "https://bekaforge.com/dgs-docs/license.html", $"{BundledRoot}/LICENSE.txt"),
            new("ai-extension", AiComingSoonTitle, AiLearnMoreUrl, $"{BundledRoot}/AI_EXTENSION.txt"),
            new("support", "Dialogue Graph System Support", "https://bekaforge.com/dgs-docs/support.html", $"{BundledRoot}/SUPPORT.txt"),
            new("upgrade", "Dialogue Graph System Upgrade Guide", "https://bekaforge.com/dgs-docs/upgrade.html", $"{BundledRoot}/UPGRADE.txt")
        };

        public static IReadOnlyList<DialogPublicInformationEntry> All => Entries;

        public static DialogPublicInformationEntry Get(string topicId)
        {
            if (string.IsNullOrWhiteSpace(topicId))
            {
                return null;
            }

            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Id, topicId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        public static DialogPublicInformationContent Resolve(
            string topicId,
            bool onlineSucceeded,
            string onlineContent,
            string failureReason)
        {
            var entry = Get(topicId);
            if (entry == null)
            {
                return new DialogPublicInformationContent(
                    "No public information is configured for this topic.",
                    true,
                    "Bundled copy unavailable");
            }

            if (onlineSucceeded && !string.IsNullOrWhiteSpace(onlineContent))
            {
                return new DialogPublicInformationContent(onlineContent.Trim(), false, "Online copy");
            }

            var bundledAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(entry.BundledAssetPath);
            var bundledText = bundledAsset != null && !string.IsNullOrWhiteSpace(bundledAsset.text)
                ? bundledAsset.text.Trim()
                : $"{entry.Title}\n\nThe bundled copy is missing from the installed package.";
            var reason = string.IsNullOrWhiteSpace(failureReason) ? "Online copy unavailable" : failureReason.Trim();

            return new DialogPublicInformationContent(
                bundledText,
                true,
                $"Bundled copy — {reason}");
        }

        public static void Open(string topicId)
        {
            DialogPublicInformationWindow.Open(topicId, bundledOnly: false);
        }

        public static void OpenBundled(string topicId)
        {
            DialogPublicInformationWindow.Open(topicId, bundledOnly: true);
        }

        public static void OpenExternal(string topicId)
        {
            var entry = Get(topicId);
            if (entry != null)
            {
                Application.OpenURL(entry.OnlineUrl);
            }
        }
    }
}
