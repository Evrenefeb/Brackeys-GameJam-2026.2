using System;
using System.Text.RegularExpressions;
using DialogSystem.Runtime.Settings;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Onboarding
{
    /// <summary>
    /// Internal editor-only content model for the welcome window.
    /// </summary>
    [Serializable]
    internal sealed class DialogWelcomeContent
    {
        public string productName = "Dialogue Graph System";
        [TextArea(2, 4)] public string shortDescription = "Visual dialogue graphs for Unity with a production-ready runtime and editor workflow.";
        public string versionOverride = string.Empty;
        public string developerName = string.Empty;
        public string brandIconAssetPath = string.Empty;
        public string bannerImageAssetPath = string.Empty;
        public string documentationUrl = string.Empty;
        public string discordUrl = string.Empty;
        public string websiteUrl = string.Empty;
        public string supportContact = string.Empty;
        public string changelogUrl = string.Empty;
        public string reviewUrl = string.Empty;
        public string primaryButtonLabel = "Open Documentation";
        public string secondaryButtonLabel = "Close";
        public string dontShowAgainLabel = "Don't show again";
        [TextArea(1, 3)] public string footerText = string.Empty;
        [TextArea(3, 8)] public string releaseNotes = "- Open the graph editor from the Tools menu.\n- Review the sample graph assets.\n- Configure settings before shipping.";
        public bool showOnEditorStartup = true;
        public DialogWelcomeRepeatMode repeatMode = DialogWelcomeRepeatMode.PerMajorMinorVersion;

        public string ProductName => productName ?? string.Empty;
        public string ShortDescription => shortDescription ?? string.Empty;
        public string VersionOverride => versionOverride ?? string.Empty;
        public string DeveloperName => developerName ?? string.Empty;
        public string BrandIconAssetPath => brandIconAssetPath ?? string.Empty;
        public string BannerImageAssetPath => bannerImageAssetPath ?? string.Empty;
        public string DocumentationUrl => documentationUrl ?? string.Empty;
        public string DiscordUrl => discordUrl ?? string.Empty;
        public string WebsiteUrl => websiteUrl ?? string.Empty;
        public string SupportContact => supportContact ?? string.Empty;
        public string ChangelogUrl => changelogUrl ?? string.Empty;
        public string ReviewUrl => reviewUrl ?? string.Empty;
        public string PrimaryButtonLabel => primaryButtonLabel ?? string.Empty;
        public string SecondaryButtonLabel => secondaryButtonLabel ?? string.Empty;
        public string DontShowAgainLabel => dontShowAgainLabel ?? string.Empty;
        public string FooterText => footerText ?? string.Empty;
        public string ReleaseNotes => releaseNotes ?? string.Empty;
        public bool ShowOnEditorStartup => showOnEditorStartup;
        public DialogWelcomeRepeatMode RepeatMode => repeatMode;
    }

    /// <summary>
    /// Central editor-only service for welcome config loading, validation, and dismissal state.
    /// </summary>
    internal static class DialogWelcomeBrandingEditorService
    {
        #region ---------------- Content ----------------

        private const string SettingsAssetPath = "Assets/DialogGraphSystem/Resources/DialogSettingsSO/DialogSystemSettings.asset";
        private const string DefaultWelcomeContentJson = @"{
  ""productName"": ""Dialogue Graph System"",
  ""shortDescription"": ""Build branching dialogue visually with a polished editor, runtime UI, validation, variables, themes, actions, localization, and cross-graph traversal."",
  ""versionOverride"": ""3.0.0"",
  ""developerName"": ""Beka Forge"",
  ""brandIconAssetPath"": ""Assets/DialogGraphSystem/Resources/Brand/icone 1254x1254.png"",
  ""bannerImageAssetPath"": ""Assets/DialogGraphSystem/Resources/Brand/dialogue_graph_system_1731x909.png"",
  ""documentationUrl"": ""https://bekaforge.com/dgs-docs/"",
  ""discordUrl"": ""https://discord.gg/hD8e6WFFWT"",
  ""websiteUrl"": """",
  ""supportContact"": ""bekaforge@gmail.com"",
  ""changelogUrl"": ""https://bekaforge.com/dgs-docs/changelog.html"",
  ""reviewUrl"": """",
  ""primaryButtonLabel"": ""Open Documentation"",
  ""secondaryButtonLabel"": ""Close"",
  ""dontShowAgainLabel"": ""Don't show again"",
  ""footerText"": ""Dialogue Graph System v3.0.0 - Beka Forge"",
  ""releaseNotes"": ""- Open the workspace from {DGS_LAUNCHER_MENU}.\n- Explore the updated sample graphs, localization workflow, and demo scene.\n- Build multi-graph dialogue with dialog, choice, action, condition, set-variable, and graph-jump nodes.\n- Use validation, runtime settings, and bundled offline information before shipping."",
  ""showOnEditorStartup"": true,
  ""repeatMode"": 2
}";

        private static DialogWelcomeContent _cachedContent;

        #endregion

        #region ---------------- Editor Prefs ----------------

        private const string DismissedTokenPrefKey = "DialogSystem.Welcome.LastDismissedToken";
        private const string SuppressAllPrefKey = "DialogSystem.Welcome.SuppressAll";

        #endregion

        #region ---------------- Load ----------------

        public static DialogWelcomeContent LoadContent()
        {
            if (_cachedContent != null)
            {
                return _cachedContent;
            }

            var content = JsonUtility.FromJson<DialogWelcomeContent>(DefaultWelcomeContentJson);
            if (content == null)
            {
                content = new DialogWelcomeContent();
            }

            content.releaseNotes = NormalizeMenuPathHints(content.releaseNotes);
            _cachedContent = content;
            return _cachedContent;
        }

        private static string NormalizeMenuPathHints(string releaseNotes)
        {
            return string.IsNullOrEmpty(releaseNotes)
                ? string.Empty
                : releaseNotes.Replace("{DGS_LAUNCHER_MENU}", TextResources.MENU_DIALOGUE_GRAPH_SYSTEM_LAUNCHER_DISPLAY);
        }

        #endregion

        #region ---------------- Version ----------------

        public static string GetEffectiveVersionLabel(DialogWelcomeContent branding)
        {
            if (branding != null && !string.IsNullOrWhiteSpace(branding.VersionOverride))
            {
                return branding.VersionOverride.Trim();
            }

            var settings = AssetDatabase.LoadAssetAtPath<DialogSystemSettings>(SettingsAssetPath);
            if (settings != null && !string.IsNullOrWhiteSpace(settings.version))
            {
                return settings.version.Trim();
            }

            return "1.0.0";
        }

        public static string GetDismissalToken(DialogWelcomeContent branding)
        {
            var version = GetEffectiveVersionLabel(branding);

            if (branding == null)
            {
                return $"version:{version}";
            }

            return branding.RepeatMode switch
            {
                DialogWelcomeRepeatMode.OnceEver => "once",
                DialogWelcomeRepeatMode.PerMajorMinorVersion => $"majorMinor:{ToMajorMinorVersion(version)}",
                _ => $"version:{version}"
            };
        }

        private static string ToMajorMinorVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return "unversioned";
            }

            var matches = Regex.Matches(version, @"\d+");
            if (matches.Count >= 2)
            {
                return $"{matches[0].Value}.{matches[1].Value}";
            }

            return version.Trim();
        }

        #endregion

        #region ---------------- Startup State ----------------

        public static bool ShouldAutoShow(DialogWelcomeContent branding)
        {
            if (branding == null || !branding.ShowOnEditorStartup)
            {
                return false;
            }

            if (EditorPrefs.GetBool(SuppressAllPrefKey, false))
            {
                return false;
            }

            var dismissedToken = EditorPrefs.GetString(DismissedTokenPrefKey, string.Empty);
            return !string.Equals(dismissedToken, GetDismissalToken(branding), StringComparison.Ordinal);
        }

        public static void RecordWelcomeClosed(DialogWelcomeContent branding, bool suppressAll, bool recordVersionDismissal)
        {
            if (branding == null)
            {
                return;
            }

            if (recordVersionDismissal)
            {
                EditorPrefs.SetString(DismissedTokenPrefKey, GetDismissalToken(branding));
            }

            if (suppressAll)
            {
                EditorPrefs.SetBool(SuppressAllPrefKey, true);
            }
        }

        public static void ResetDismissalState()
        {
            EditorPrefs.DeleteKey(DismissedTokenPrefKey);
            EditorPrefs.DeleteKey(SuppressAllPrefKey);
            _cachedContent = null;
        }

        #endregion

        #region ---------------- Links ----------------

        public static bool TryGetOpenableLink(string rawValue, out string resolvedLink)
        {
            resolvedLink = string.Empty;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var trimmed = rawValue.Trim();
            if (trimmed.Contains("@") && !trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                resolvedLink = $"mailto:{trimmed}";
                return true;
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var scheme = uri.Scheme;
                if (scheme == Uri.UriSchemeHttp || scheme == Uri.UriSchemeHttps || scheme == Uri.UriSchemeMailto)
                {
                    resolvedLink = uri.AbsoluteUri;
                    return true;
                }
            }

            return false;
        }

        public static void OpenLink(string rawValue)
        {
            if (TryGetOpenableLink(rawValue, out var resolvedLink))
            {
                Application.OpenURL(resolvedLink);
            }
        }

        #endregion
    }
}
