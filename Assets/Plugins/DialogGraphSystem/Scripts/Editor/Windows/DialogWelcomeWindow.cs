using System;
using DialogSystem.EditorTools.Onboarding;
using DialogSystem.EditorTools.PublicInformation;
using DialogSystem.EditorTools.Settings;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Windows
{
    /// <summary>
    /// Professional first-run onboarding window for the Dialogue Graph System asset.
    /// </summary>
    internal sealed class DialogWelcomeWindow : EditorWindow
    {
        #region ---------------- State ----------------

        private DialogWelcomeContent _branding;
        private bool _openedFromStartup;
        private bool _suppressFuturePopups;
        private bool _closeStatePersisted;

        #endregion

        #region ---------------- Open ----------------

        [MenuItem(TextResources.MENU_DIALOGUE_WELCOME, priority = 20)]
        private static void OpenFromMenu()
        {
            Open(null, false);
        }

        public static void Open(DialogWelcomeContent branding, bool openedFromStartup)
        {
            var config = branding ?? DialogWelcomeBrandingEditorService.LoadContent();
            if (config == null)
            {
                Debug.LogWarning("[DialogWelcomeWindow] No welcome content could be loaded.");
                return;
            }

            var window = GetWindow<DialogWelcomeWindow>(utility: true, title: "Welcome", focus: true);
            var fixedSize = new Vector2(780f, 490f);
            window.minSize = fixedSize;
            window.maxSize = fixedSize;
            window.Initialize(config, openedFromStartup);
            window.Show();
            window.Focus();
        }

        private void Initialize(DialogWelcomeContent branding, bool openedFromStartup)
        {
            _branding = branding;
            _openedFromStartup = openedFromStartup;
            _suppressFuturePopups = false;
            _closeStatePersisted = false;
            titleContent = new GUIContent(string.IsNullOrWhiteSpace(_branding.ProductName) ? "Welcome" : _branding.ProductName);
            BuildUi();
        }

        #endregion

        #region ---------------- Unity ----------------

        private void OnEnable()
        {
            if (_branding != null)
            {
                BuildUi();
            }
        }

        private void OnDisable()
        {
            PersistCloseStateIfNeeded(_openedFromStartup);
        }

        #endregion

        #region ---------------- UI ----------------

        private void BuildUi()
        {
            if (_branding == null)
            {
                return;
            }

            rootVisualElement.Clear();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.WELCOME_STYLE_PATH);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            rootVisualElement.AddToClassList("dgs-welcome-root");

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("dgs-welcome-scroll");
            rootVisualElement.Add(scrollView);

            var bannerTexture = LoadConfiguredTexture(_branding.BannerImageAssetPath);
            if (bannerTexture != null)
            {
                var banner = new Image
                {
                    image = bannerTexture,
                    scaleMode = ScaleMode.ScaleAndCrop
                };
                banner.AddToClassList("dgs-welcome-banner");
                scrollView.Add(banner);
            }

            scrollView.Add(BuildHeroCard());
            var releaseNotes = BuildReleaseNotesCard();
            if (releaseNotes != null)
            {
                scrollView.Add(releaseNotes);
            }

            scrollView.Add(BuildToggleRow());
        }

        private VisualElement BuildHeroCard()
        {
            var card = CreateCard();
            card.AddToClassList("dgs-welcome-hero-card");

            var row = new VisualElement();
            row.AddToClassList("dgs-welcome-header-row");

            var iconTexture = LoadConfiguredTexture(_branding.BrandIconAssetPath);
            if (iconTexture != null)
            {
                var icon = new Image
                {
                    image = iconTexture,
                    scaleMode = ScaleMode.ScaleToFit
                };
                icon.AddToClassList("dgs-welcome-icon");
                row.Add(icon);
            }

            var content = new VisualElement();
            content.AddToClassList("dgs-welcome-headline");

            var eyebrow = new Label("WELCOME");
            eyebrow.AddToClassList("dgs-welcome-eyebrow");
            content.Add(eyebrow);

            var title = new Label(string.IsNullOrWhiteSpace(_branding.ProductName) ? "Dialogue Graph System" : _branding.ProductName.Trim());
            title.AddToClassList("dgs-welcome-title");
            content.Add(title);

            var summaryLine = BuildSummaryLine();
            if (!string.IsNullOrWhiteSpace(summaryLine))
            {
                var meta = new Label(summaryLine);
                meta.AddToClassList("dgs-welcome-meta");
                content.Add(meta);
            }

            if (!string.IsNullOrWhiteSpace(_branding.ShortDescription))
            {
                var description = new Label(_branding.ShortDescription.Trim());
                description.AddToClassList("dgs-welcome-description");
                content.Add(description);
            }

            row.Add(content);
            card.Add(row);
            card.Add(BuildCompactLinksRow());

            return card;
        }

        private VisualElement BuildCompactLinksRow()
        {
            var row = new VisualElement();
            row.AddToClassList("dgs-welcome-links-row");
            row.style.flexWrap = Wrap.Wrap;

            AddPublicInformationButtons(row, "documentation", "Documentation", true);
            AddPublicInformationButtons(row, "changelog", "Changelog", false);
            AddPublicInformationButtons(row, "license", "License", false);
            AddPublicInformationButtons(row, "support", "Support", false);
            AddLinkButton(row, "Discord", _branding.DiscordUrl, false);

            return row;
        }

        private VisualElement BuildReleaseNotesCard()
        {
            if (string.IsNullOrWhiteSpace(_branding.ReleaseNotes))
            {
                return null;
            }

            var card = CreateCard();
            card.AddToClassList("dgs-welcome-release-card");

            var title = new Label("What's New");
            title.AddToClassList("dgs-welcome-section-title");
            card.Add(title);

            var notes = new Label(_branding.ReleaseNotes.Trim());
            notes.AddToClassList("dgs-welcome-release-notes");
            card.Add(notes);

            return card;
        }

        private VisualElement BuildToggleRow()
        {
            var row = new VisualElement();
            row.AddToClassList("dgs-welcome-toggle-row");

            var toggle = new Toggle(string.IsNullOrWhiteSpace(_branding.DontShowAgainLabel) ? "Don't show again" : _branding.DontShowAgainLabel.Trim())
            {
                value = false
            };
            toggle.AddToClassList("dgs-welcome-toggle");
            toggle.RegisterValueChangedCallback(evt => _suppressFuturePopups = evt.newValue);
            row.Add(toggle);

            return row;
        }

        private void AddLinkButton(VisualElement parent, string label, string rawLink, bool primary)
        {
            if (!DialogWelcomeBrandingEditorService.TryGetOpenableLink(rawLink, out _))
            {
                return;
            }

            var button = new Button(() => DialogWelcomeBrandingEditorService.OpenLink(rawLink))
            {
                text = label
            };
            button.AddToClassList(primary ? "dgs-welcome-primary-button" : "dgs-welcome-secondary-button");
            parent.Add(button);
        }

        private static void AddPublicInformationButtons(VisualElement parent, string topicId, string label, bool primary)
        {
            var entry = DialogPublicInformationCatalog.Get(topicId);
            if (entry == null)
            {
                return;
            }

            var onlineButton = new Button(() => DialogPublicInformationCatalog.Open(topicId))
            {
                text = label,
                tooltip = entry.OnlineUrl
            };
            onlineButton.AddToClassList(primary ? "dgs-welcome-primary-button" : "dgs-welcome-secondary-button");
            parent.Add(onlineButton);

            var bundledButton = new Button(() => DialogPublicInformationCatalog.OpenBundled(topicId))
            {
                text = entry.BundledActionLabel,
                tooltip = $"{label}: bundled offline copy"
            };
            bundledButton.AddToClassList("dgs-welcome-secondary-button");
            parent.Add(bundledButton);
        }

        private static VisualElement CreateCard()
        {
            var card = new VisualElement();
            card.AddToClassList("dgs-welcome-card");
            return card;
        }


        private static VisualElement CreateStatusRow(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("dgs-welcome-status-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("dgs-welcome-status-label");
            row.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.AddToClassList("dgs-welcome-status-value");
            row.Add(valueElement);

            return row;
        }

        private string BuildSummaryLine()
        {
            var version = DialogWelcomeBrandingEditorService.GetEffectiveVersionLabel(_branding);
            var developer = _branding.DeveloperName?.Trim();

            if (!string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(developer))
            {
                return $"v{version}  -  {developer}";
            }

            if (!string.IsNullOrWhiteSpace(version))
            {
                return $"v{version}";
            }

            return developer ?? string.Empty;
        }

        private static Texture2D LoadConfiguredTexture(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var trimmedPath = assetPath.Trim();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(trimmedPath);
            if (sprite != null)
            {
                return sprite.texture;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(trimmedPath);
        }

        #endregion

        #region ---------------- Close ----------------

        private void CloseWindow(bool recordVersionDismissal)
        {
            PersistCloseStateIfNeeded(recordVersionDismissal);
            Close();
        }

        private void PersistCloseStateIfNeeded(bool recordVersionDismissal)
        {
            if (_closeStatePersisted || _branding == null)
            {
                return;
            }

            DialogWelcomeBrandingEditorService.RecordWelcomeClosed(_branding, _suppressFuturePopups, recordVersionDismissal);
            _closeStatePersisted = true;
        }

        #endregion
    }
}
