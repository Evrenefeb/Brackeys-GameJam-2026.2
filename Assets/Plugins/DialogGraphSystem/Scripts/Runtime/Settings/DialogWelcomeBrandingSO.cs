using UnityEngine;

namespace DialogSystem.Runtime.Settings
{
    /// <summary>
    /// Configurable branding and first-run behavior for the editor welcome window.
    /// </summary>
    public enum DialogWelcomeRepeatMode
    {
        OnceEver = 0,
        PerVersion = 1,
        PerMajorMinorVersion = 2
    }

    /// <summary>
    /// Internal content asset for the Dialogue Graph System onboarding window.
    /// </summary>
    public sealed class DialogWelcomeBrandingSO : ScriptableObject
    {
        #region ---------------- Identity ----------------

        [Header("Identity")]
        [Tooltip("Main product name shown in the welcome window.")]
        [SerializeField] private string productName = "Dialogue Graph System";

        [Tooltip("Short summary shown under the product name.")]
        [TextArea(2, 4)]
        [SerializeField] private string shortDescription = "Visual dialogue graphs for Unity with a production-ready runtime and editor workflow.";

        [Tooltip("Optional version label override. Leave empty to use the main Dialogue Graph System settings version when available.")]
        [SerializeField] private string versionOverride = string.Empty;

        [Tooltip("Optional developer or studio name shown in the welcome header.")]
        [SerializeField] private string developerName = string.Empty;

        #endregion

        #region ---------------- Visuals ----------------

        [Header("Visuals")]
        [Tooltip("Optional square logo or brand mark.")]
        [SerializeField] private Sprite brandIcon;

        [Tooltip("Optional wide banner image displayed at the top of the window.")]
        [SerializeField] private Sprite bannerImage;

        #endregion

        #region ---------------- Links ----------------

        [Header("Links")]
        [Tooltip("Documentation or quick-start URL.")]
        [SerializeField] private string documentationUrl = string.Empty;

        [Tooltip("Discord invite URL.")]
        [SerializeField] private string discordUrl = string.Empty;

        [Tooltip("Primary website or landing page URL.")]
        [SerializeField] private string websiteUrl = string.Empty;

        [Tooltip("Support email address, mailto link, or support page URL.")]
        [SerializeField] private string supportContact = string.Empty;

        [Tooltip("Changelog URL.")]
        [SerializeField] private string changelogUrl = string.Empty;

        [Tooltip("Asset Store page or review URL.")]
        [SerializeField] private string reviewUrl = string.Empty;

        #endregion

        #region ---------------- Window Copy ----------------

        [Header("Window Copy")]
        [Tooltip("Label used for the primary call-to-action button. Usually documentation.")]
        [SerializeField] private string primaryButtonLabel = "Open Documentation";

        [Tooltip("Label used for the close button.")]
        [SerializeField] private string secondaryButtonLabel = "Close";

        [Tooltip("Label shown beside the persistent dismiss toggle.")]
        [SerializeField] private string dontShowAgainLabel = "Don't show again";

        [Tooltip("Optional footer text shown at the bottom of the window.")]
        [TextArea(1, 3)]
        [SerializeField] private string footerText = string.Empty;

        [Tooltip("Optional short release notes or bullet list text.")]
        [TextArea(3, 8)]
        [SerializeField] private string releaseNotes = "- Open the graph editor from the Tools menu.\n- Review the sample graph assets.\n- Configure settings before shipping.";

        #endregion

        #region ---------------- First Import Behavior ----------------

        [Header("First Import Behavior")]
        [Tooltip("If disabled, the welcome window will never auto-open on editor startup.")]
        [SerializeField] private bool showOnEditorStartup = true;

        [Tooltip("Controls when the welcome window may appear again after it has already been dismissed.")]
        [SerializeField] private DialogWelcomeRepeatMode repeatMode = DialogWelcomeRepeatMode.PerMajorMinorVersion;

        #endregion

        #region ---------------- Properties ----------------

        public string ProductName => productName;
        public string ShortDescription => shortDescription;
        public string VersionOverride => versionOverride;
        public string DeveloperName => developerName;
        public Sprite BrandIcon => brandIcon;
        public Sprite BannerImage => bannerImage;
        public string DocumentationUrl => documentationUrl;
        public string DiscordUrl => discordUrl;
        public string WebsiteUrl => websiteUrl;
        public string SupportContact => supportContact;
        public string ChangelogUrl => changelogUrl;
        public string ReviewUrl => reviewUrl;
        public string PrimaryButtonLabel => primaryButtonLabel;
        public string SecondaryButtonLabel => secondaryButtonLabel;
        public string DontShowAgainLabel => dontShowAgainLabel;
        public string FooterText => footerText;
        public string ReleaseNotes => releaseNotes;
        public bool ShowOnEditorStartup => showOnEditorStartup;
        public DialogWelcomeRepeatMode RepeatMode => repeatMode;

        #endregion
    }
}
