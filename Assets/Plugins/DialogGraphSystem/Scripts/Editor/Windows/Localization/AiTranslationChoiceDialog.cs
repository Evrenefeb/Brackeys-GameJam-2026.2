using System;
using DialogSystem.EditorTools.PublicInformation;
using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Localization
{
    /// <summary>
    /// Custom dialog for choosing AI translation mode.
    /// Matches the project design and handles missing AI extension gracefully.
    /// </summary>
    public class AiTranslationChoiceDialog : EditorWindow
    {
        private Action<bool> _onConfirmed; // true = overwrite, false = missing only
        private bool _isAvailable;

        public static void Show(Action<bool> onConfirmed)
        {
            var win = CreateInstance<AiTranslationChoiceDialog>();
            win.titleContent = new GUIContent("AI Translation");
            win._onConfirmed = onConfirmed;
            win.minSize = new Vector2(520, 320);
            win.maxSize = new Vector2(520, 320);
            win.ShowModal();
        }

        private void OnEnable()
        {
            _isAvailable = DialogLocalizationAiBridgeLocator.IsAvailable;

            var root = rootVisualElement;
            root.Clear();

            var mainSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.STYLE_PATH);
            if (mainSs != null) root.styleSheets.Add(mainSs);

            var locSs = AssetDatabase.LoadAssetAtPath<StyleSheet>(TextResources.LOCALIZATION_MANAGER_STYLE_PATH);
            if (locSs != null) root.styleSheets.Add(locSs);

            var container = new VisualElement();
            container.AddToClassList("ai-dialog-root");
            root.Add(container);

            // Header
            var header = new VisualElement();
            header.AddToClassList("ai-dialog-header");
            
            var title = new Label("AI Localization");
            title.AddToClassList("ai-dialog-title");
            header.Add(title);

            var subtitle = new Label("Generate high-quality translations for your dialogue graph using AI.");
            subtitle.AddToClassList("ai-dialog-subtitle");
            header.Add(subtitle);
            
            container.Add(header);

            // Ad Banner (if missing)
            if (!_isAvailable)
            {
                var ad = new VisualElement();
                ad.AddToClassList("ai-ad-banner");

                var adText = new Label($"{DialogPublicInformationCatalog.AiComingSoonTitle}. Core manual localization remains fully available.");
                adText.AddToClassList("ai-ad-text");
                ad.Add(adText);

                var learnMoreButton = new Button(() => DialogPublicInformationCatalog.Open("ai-extension")) { text = "Learn More" };
                learnMoreButton.AddToClassList("loc-btn");
                learnMoreButton.AddToClassList("loc-btn--ai");
                ad.Add(learnMoreButton);

                var bundledButton = new Button(() => DialogPublicInformationCatalog.OpenBundled("ai-extension")) { text = "View Bundled Copy" };
                bundledButton.AddToClassList("loc-btn");
                ad.Add(bundledButton);

                container.Add(ad);
            }

            // Choice Cards
            var choiceRow = new VisualElement();
            choiceRow.AddToClassList("ai-dialog-choices");
            if (!_isAvailable) choiceRow.AddToClassList("ai-locked");

            choiceRow.Add(CreateChoiceCard(
                "Only Missing", 
                "Scan all rows and only translate the ones that are currently empty.",
                () => Confirm(false)));

            choiceRow.Add(CreateChoiceCard(
                "Retranslate All", 
                "Ignore existing text and regenerate every row. Useful for fixing bad translations.",
                () => Confirm(true)));

            container.Add(choiceRow);

            // Footer
            var footer = new VisualElement();
            footer.AddToClassList("ai-dialog-footer");

            var cancelBtn = new Button(Close) { text = "Cancel" };
            cancelBtn.AddToClassList("loc-btn");
            footer.Add(cancelBtn);

            container.Add(footer);
        }

        private VisualElement CreateChoiceCard(string title, string desc, Action onClick)
        {
            var card = new Button(onClick);
            card.AddToClassList("ai-choice-card");
            card.SetEnabled(_isAvailable);

            var t = new Label(title);
            t.AddToClassList("ai-choice-title");
            card.Add(t);

            var d = new Label(desc);
            d.AddToClassList("ai-choice-desc");
            card.Add(d);

            return card;
        }

        private void Confirm(bool overwrite)
        {
            if (!_isAvailable) return;
            _onConfirmed?.Invoke(overwrite);
            Close();
        }
    }
}
