using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Settings;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.UI.Theming;
using UnityEngine.Serialization;

namespace DialogSystem.Runtime.UI
{
    /// <summary>
    /// Bridge between <see cref="DialogManager"/> and the concrete dialog UI.
    /// Owns all Image/Text references for the dialog panel and applies theme
    /// sprites + colors through <see cref="ApplyTheme"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogUIController : MonoBehaviour, IDialogueRuntimeView
    {
        // ─── Core UI ─────────────────────────────────────────────────────────────

        [Header("Core UI")]
        [Tooltip("Root panel — shown/hidden when dialog starts/ends.")]
        public GameObject panelRoot;

        [Tooltip("Text element that displays the speaker's name.")]
        public TextMeshProUGUI speakerName;

        [Tooltip("Text element that displays the dialog body.")]
        public TextMeshProUGUI dialogText;

        [Tooltip("Image that shows the speaker portrait sprite.")]
        public Image portraitImage;

        [Header("Speaker Layout")]
        [Tooltip("When enabled, the dialog manager can alternate portrait/content sides between speakers.")]
        [SerializeField] private bool _allowPortraitSideSwapping = true;

        [Tooltip("Optional layout root that contains the portrait and frame. Assign this for prefabs with more than two children in the dialog row.")]
        [SerializeField] private RectTransform _portraitLayoutRoot;

        [Tooltip("Optional layout root that contains the speaker name and dialog text. Assign this for prefabs with more than two children in the dialog row.")]
        [SerializeField] private RectTransform _dialogContentLayoutRoot;

        // ─── Theme Targets ───────────────────────────────────────────────────────

        [Header("Theme Targets")]
        [Tooltip("Background image of the dialog box frame. Set Image Type to Sliced.")]
        [SerializeField] private Image _dialogBoxImage;

        [Tooltip("Background image behind the speaker name plate.")]
        [SerializeField] private Image _nameplateImage;

        [Tooltip("Border / frame image drawn around the portrait.")]
        [SerializeField] private Image _avatarFrameImage;

        [Tooltip("Small arrow or chevron shown to prompt the player to skip.")]
        [FormerlySerializedAs("skipButtonIcon")]
        [SerializeField] private Image _skipButtonIconImage;

        [Tooltip("Background image for the skip button.")]
        [FormerlySerializedAs("skipButtonBackground")]
        [SerializeField] private Image _skipButtonBackgroundImage;

        [Tooltip("Small arrow or chevron shown to prompt the player to skip Line.")]
        [SerializeField] private Image _skipLineButtonIconImage;

        [Tooltip("Background image for the skip line button.")]
        [SerializeField] private Image _skipLineButtonBackgroundImage;

        [Tooltip("Background image on the history panel button.")]
        [FormerlySerializedAs("_historyButtonImage")]
        [SerializeField] private Image _historyButtonBackgroundImage;

        [Tooltip("Icon image on the history panel button.")]
        [SerializeField] private Image _historyButtonIconImage;

        [Tooltip("Background image on the settings panel button.")]
        [SerializeField] private Image _settingsButtonBackgroundImage;

        [Tooltip("Icon image on the Settings button.")]
        [SerializeField] private Image _settingsButtonIconImage;

        [Tooltip("Background image on the autoplay button.")]
        [SerializeField] private Image _autoPlayButtonBackgroundImage;

        [Tooltip("Icon image on the autoplay button — sprite swapped between idle and active icons by the theme.")]
        [FormerlySerializedAs("_autoPlayButtonImage")]
        [SerializeField] private Image _autoPlayButtonIconImage;

        [Tooltip("Optional text label on the skip / skip-all button. Receives skipAllTextColor from the theme.")]
        [SerializeField] private TextMeshProUGUI _skipAllTextLabel;

        // ─── Choices ─────────────────────────────────────────────────────────────

        [Header("Choices")]
        [Tooltip("Log verbose choice build messages.")]
        [SerializeField] private bool _doDebug = false;

        [Tooltip("Parent transform for choice buttons. Needs VerticalLayoutGroup + ContentSizeFitter.")]
        public Transform choicesContainer;

        [Tooltip("Prefab with a Button + ChoiceButtonView on the root.")]
        public GameObject choiceButtonPrefab;

        // ─── Buttons ─────────────────────────────────────────────────────────────

        [Header("Buttons")]
        [Tooltip("Skip-conversation button root object.")]
        public GameObject skipButton;

        [Tooltip("Skip-line button root object.")]
        public GameObject skipLineButton;

        [Tooltip("Button that opens the dialog history panel.")]
        [FormerlySerializedAs("dialogPanelButton")]
        public Button historyPanelButton;

        [Tooltip("Button that opens the settings panel.")]
        public Button settingsPanelButton;

        [Tooltip("AutoPlay toggle button.")]
        public Button autoPlayButton;

        [Tooltip("Optional button that opens an in-game language selector/settings panel.")]
        public Button languageButton;



        // ─── Theming ─────────────────────────────────────────────────────────────

        // Cached so autoplay updates and future choice rebuilds can resolve theme data
        // without an extra scene lookup.
        private DialogThemeSO _currentTheme;
        private GameObject _choiceButtonPrefabFallback;
        private bool _isAutoPlayActive;
        private Button _runtimeSkipButton;
        private UnityAction _runtimeSkipAction;
        private Button _runtimeSkipLineButton;
        private UnityAction _runtimeSkipLineAction;
        private Button _runtimeHistoryButton;
        private UnityAction _runtimeHistoryAction;
        private Button _runtimeSettingsButton;
        private UnityAction _runtimeSettingsAction;
        private Button _runtimeAutoPlayButton;
        private UnityAction _runtimeAutoPlayAction;
        private Button _runtimeLanguageButton;
        private UnityAction _runtimeLanguageAction;
        private DialogueRuntimeUISettings _appliedSettings;
        private readonly System.Collections.Generic.HashSet<string> _missingBindingWarnings = new System.Collections.Generic.HashSet<string>();

        #region ---------------- Runtime View ----------------

        public TMP_Text DialogueTextTarget => dialogText;
        public Transform ChoicesRoot => choicesContainer;

        public void SetVisible(bool visible)
        {
            SetPanelVisible(visible);
        }

        public void SetSkipAllVisible(bool visible)
        {
            SetSkipVisible(visible);
        }

        public void SetAutoPlayActive(bool isActive)
        {
            UpdateAutoPlayIcon(isActive);
        }

        public void ShowDialogueLine(DialogueLinePresentation line)
        {
            ShowDialogLine(line.SpeakerName, line.Portrait);
        }

        public void ShowChoicePrompt(string promptText)
        {
            ShowChoicePrompt(!string.IsNullOrWhiteSpace(promptText), promptText);
        }

        public void RebuildChoices(IReadOnlyList<DialogueChoicePresentation> choices, DialogChoiceSettings settings, Action<int> onPick)
        {
            if (!IsFeatureEnabled(DialogOptionalUiFeature.ChoicePanel))
            {
                ClearChoices();
                return;
            }

            if (!choicesContainer)
            {
                WarnMissingBinding(DialogOptionalUiFeature.ChoicePanel, nameof(choicesContainer));
                return;
            }

            if (!choicesContainer.gameObject.scene.IsValid())
            {
                Debug.LogError("[DialogUIController] Choices container points to a prefab asset instead of a live scene/runtime instance. Check DialogUIManager bootstrap and scene assignments.");
                return;
            }

            var choicePrefab = ResolveChoiceButtonPrefab();
            if (choicePrefab == null)
            {
                return;
            }

            for (int i = choicesContainer.childCount - 1; i >= 0; i--)
                DestroyChoiceObject(choicesContainer.GetChild(i).gameObject);

            var count = choices?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var choice = choices[i];
                var go = Instantiate(choicePrefab, choicesContainer);
                var view = go.GetComponent<ChoiceButtonView>();
                if (view == null)
                {
                    Debug.LogError($"[DialogUIController] Choice prefab '{choicePrefab.name}' instantiated without a {nameof(ChoiceButtonView)} component. Skipping choice index {choice.Index}.");
                    DestroyChoiceObject(go);
                    continue;
                }

                view.Init(DialogManager.Instance, choice.Index, settings);
                view.SetHotkey(string.Empty);
                view.SetContent(choice.Text, choice.SubLabel, choice.Interactable, () => onPick?.Invoke(choice.Index));
            }

            if (ShouldLogDebug())
            {
                Debug.Log($"[DialogUIController] Built {count} choice button(s).", this);
            }

            SetChoicesVisible(true);
        }

        public void ResetTransientState(bool autoPlayActive)
        {
            ClearChoices();
            SetPanelVisible(false);
            SetSkipVisible(false);
            SetText(string.Empty);
            SetSpeaker(string.Empty);
            if (portraitImage != null) portraitImage.sprite = null;
            UpdateAutoPlayIcon(autoPlayActive);
        }

        #endregion

        /// <summary>
        /// Applies the given theme to every Image and Text target owned by this panel.
        /// Called by <see cref="DialogUIManager"/> whenever the active theme changes.
        /// </summary>
        public void ApplyTheme(DialogThemeSO theme)
        {
            if (theme == null) return;
            _currentTheme = theme;

            // Dialog panel
            SetSprite(_dialogBoxImage, theme.dialogBox);
            SetSprite(_nameplateImage, theme.nameplate);
            SetSprite(_avatarFrameImage, theme.avatarFrame);
            SetSprite(_skipButtonBackgroundImage, theme.skipButtonBackground);
            SetSprite(_skipButtonIconImage, theme.skipButtonIcon);
            SetSprite(_skipLineButtonBackgroundImage, theme.skipLineButtonBackground);
            SetSprite(_skipLineButtonIconImage, theme.skipLineButtonIcon);

            // Buttons
            SetSprite(_historyButtonBackgroundImage, theme.historyButton);
            SetSprite(_historyButtonIconImage, theme.historyButtonIcon);
            SetSprite(_settingsButtonBackgroundImage, theme.settingsButton);
            SetSprite(_settingsButtonIconImage, theme.settingsButtonIcon);
            SetSprite(_autoPlayButtonBackgroundImage, theme.autoPlayButtonBackground);

            // AutoPlay — apply the sprite that matches the current play state.
            ApplyAutoPlayThemeSprite(theme);

            // Text colors
            SetColor(speakerName, theme.speakerNameColor);
            SetColor(dialogText, theme.dialogTextColor);
            SetColor(_skipAllTextLabel, theme.skipAllTextColor);

            // Fonts
            SetFont(speakerName, theme.speakerNameFont != null ? theme.speakerNameFont : theme.defaultFont);
            SetFont(dialogText, theme.dialogTextFont != null ? theme.dialogTextFont : theme.defaultFont);
        }

        /// <summary>
        /// Applies visibility settings to the UI components.
        /// </summary>
        public void ApplySettings(DialogueRuntimeUISettings settings)
        {
            _appliedSettings = settings;
            if (settings == null) return;

            // Global panels
            SetOptionalGameObjectActive(DialogOptionalUiFeature.BackgroundPanel, panelRoot, settings.showBackgroundPanel, nameof(panelRoot));
            SetChoicesVisible(settings.showChoicePanel);

            // Metadata
            SetSpeakerVisible(settings.showSpeakerName);
            SetPortraitVisible(settings.showPortrait);

            // Controls
            SetSkipVisible(settings.showSkipButton);
            SetOptionalGameObjectActive(DialogOptionalUiFeature.AutoButton, ResolveAutoPlayButtonObject(), settings.showAutoButton, "autoplay button");
            SetOptionalGameObjectActive(DialogOptionalUiFeature.HistoryButton, ResolveHistoryButtonObject(), settings.showHistoryButton, "history button");
            SetOptionalGameObjectActive(DialogOptionalUiFeature.SettingsButton, ResolveSettingsButtonObject(), settings.showSettingsButton, "settings button");
            var languageButtonObject = ResolveLanguageButtonObject();
            if (languageButtonObject != null)
            {
                SetOptionalGameObjectActive(
                    DialogOptionalUiFeature.LanguageButton,
                    languageButtonObject,
                    settings.showLanguageButton && settings.showSettingsPanel,
                    "language button");
            }

            // Indicators
            SetOptionalGraphicEnabled(DialogOptionalUiFeature.ContinueIndicator, _skipButtonIconImage, settings.showContinueIndicator, nameof(_skipButtonIconImage));
            SetOptionalGraphicEnabled(DialogOptionalUiFeature.AutoSkipIcon, _autoPlayButtonIconImage, settings.showAutoSkipIcon, nameof(_autoPlayButtonIconImage));
        }

        // ─── Public API ───────────────────────────────────────────────────────────

        public void UpdateAutoPlayIcon(bool isAutoPlay)
        {
            _isAutoPlayActive = isAutoPlay;

            // Swap themed sprite on the button Image.
            ApplyAutoPlayThemeSprite(_currentTheme);
        }

        public void ToggleAutoPlayIcon()
        {
            var mgr = DialogManager.Instance;
            if (mgr) UpdateAutoPlayIcon(mgr.ToggleAutoPlay());
        }

        public void SetChoiceButtonPrefabFallback(GameObject fallbackPrefab)
        {
            _choiceButtonPrefabFallback = fallbackPrefab;
        }

        public void SetHistoryBtnListener(UnityAction action)
        {
            BindRuntimeButton("History", ref _runtimeHistoryButton, ref _runtimeHistoryAction, ResolveHistoryButton(), action);
        }

        [Obsolete("Use historyPanelButton instead.")]
        public Button dialogPanelButton
        {
            get => historyPanelButton;
            set => historyPanelButton = value;
        }

        [Obsolete("Use SetHistoryBtnListener instead.")]
        public void SetDialogPanelBtnListener(UnityAction action)
        {
            SetHistoryBtnListener(action);
        }

        public void SetSettingsBtnListener(UnityAction action)
        {
            BindRuntimeButton("Settings", ref _runtimeSettingsButton, ref _runtimeSettingsAction, ResolveSettingsButton(), action);
        }

        public void SetSkipButtonListener(UnityAction action)
        {
            BindRuntimeButton("Skip", ref _runtimeSkipButton, ref _runtimeSkipAction, ResolveSkipButton(), action);
        }

        public void SetSkipLineButtonListener(UnityAction action)
        {
            BindRuntimeButton("SkipLine", ref _runtimeSkipLineButton, ref _runtimeSkipLineAction, ResolveSkipLineButton(), action);
        }

        public void SetAutoPlayButtonListener(UnityAction action)
        {
            BindRuntimeButton("AutoPlay", ref _runtimeAutoPlayButton, ref _runtimeAutoPlayAction, ResolveAutoPlayButton(), action);
        }

        public void SetLanguageButtonListener(UnityAction action)
        {
            BindRuntimeButton("Language", ref _runtimeLanguageButton, ref _runtimeLanguageAction, ResolveLanguageButton(), action);
        }

        public void SetPanelVisible(bool v) => SetOptionalGameObjectActive(DialogOptionalUiFeature.BackgroundPanel, panelRoot, v, nameof(panelRoot));
        public void SetSkipVisible(bool v) => SetOptionalGameObjectActive(DialogOptionalUiFeature.SkipButton, ResolveSkipButtonObject(), v, "skip button");
        public void SetChoicesVisible(bool v) => SetOptionalGameObjectActive(DialogOptionalUiFeature.ChoicePanel, choicesContainer != null ? choicesContainer.gameObject : null, v, nameof(choicesContainer));

        public void SetSpeaker(string value) { if (speakerName) speakerName.text = value ?? string.Empty; }
        public void SetText(string value) { if (dialogText) dialogText.text = value ?? string.Empty; }

        /// <summary>
        /// Sets the speaker name, resolving through <see cref="DialogLocalizationRuntime"/>
        /// when the node carries a stable <see cref="DialogNode.speakerNameLocaleKey"/>.
        /// Falls back to <paramref name="rawValue"/> when no table is active or key is missing.
        /// </summary>
        public void SetSpeaker(string rawValue, DialogNode node)
        {
            var resolved = node != null && node.HasSpeakerLocaleKey
                ? DialogLocalizationRuntime.Instance.Resolve(node.speakerNameLocaleKey, rawValue)
                : rawValue;
            SetSpeaker(resolved);
        }

        /// <summary>
        /// Sets the dialog body text, resolving through <see cref="DialogLocalizationRuntime"/>
        /// when the node carries a stable <see cref="DialogNode.questionTextLocaleKey"/>.
        /// Falls back to <paramref name="rawValue"/> when no table is active or key is missing.
        /// </summary>
        public void SetText(string rawValue, DialogNode node)
        {
            var resolved = node != null && node.HasQuestionLocaleKey
                ? DialogLocalizationRuntime.Instance.Resolve(node.questionTextLocaleKey, rawValue)
                : rawValue;
            SetText(resolved);
        }
        // TODO: FIX this later — always keep enabled, just swap the sprite
        public void SetPortrait(Sprite s)
        {
            if (portraitImage == null)
            {
                if (s != null)
                {
                    WarnMissingBinding(DialogOptionalUiFeature.Portrait, nameof(portraitImage));
                }

                return;
            }

            portraitImage.sprite = s;
            portraitImage.enabled = s != null && IsFeatureEnabled(DialogOptionalUiFeature.Portrait);
        }

        public void ShowDialogLine(string speaker, Sprite portrait)
        {
            SetTextVisible(true);
            SetSpeakerVisible(!string.IsNullOrWhiteSpace(speaker));
            SetPortraitVisible(portrait != null);
            SetSpeaker(speaker);
            SetPortrait(portrait);
        }

        public void ShowChoicePrompt(bool hasPrompt, string promptText)
        {
            if (!hasPrompt)
            {
                SetText(string.Empty);
            }

            SetTextVisible(hasPrompt);
        }

        // ─── Choices (simple rebuild, no pooling) ─────────────────────────────────

        public void SetPortraitSide(bool portraitOnRight)
        {
            if (!_allowPortraitSideSwapping)
            {
                return;
            }

            var portraitRoot = ResolvePortraitLayoutRoot();
            var contentRoot = ResolveContentLayoutRoot();
            if (portraitRoot == null || contentRoot == null || portraitRoot.parent != contentRoot.parent)
            {
                return;
            }

            var hasExplicitRoots = _portraitLayoutRoot != null && _dialogContentLayoutRoot != null;
            if (!hasExplicitRoots && portraitRoot.parent.childCount != 2)
            {
                return;
            }

            if (portraitOnRight)
            {
                contentRoot.SetAsFirstSibling();
                portraitRoot.SetAsLastSibling();
            }
            else
            {
                portraitRoot.SetAsFirstSibling();
                contentRoot.SetAsLastSibling();
            }
        }

        /// <summary>Destroys existing choice buttons and rebuilds one per choice in the node.</summary>
        public void BuildChoices(ChoiceNode node, DialogChoiceSettings settings, Action<int> onPick, Func<string, string> textFormatter = null)
        {
            if (node == null)
            {
                return;
            }

            var presentations = new List<DialogueChoicePresentation>(node.choices.Count);
            for (int i = 0; i < node.choices.Count; i++)
            {
                var ch = node.choices[i];
                var answerText = ch.HasAnswerLocaleKey
                    ? DialogLocalizationRuntime.Instance.Resolve(ch.answerTextLocaleKey, ch.answerText)
                    : ch.answerText;
                answerText = textFormatter != null ? textFormatter(answerText) : answerText;
                var subLabel = textFormatter != null ? textFormatter(ch.tooltipOrSubLabel) : ch.tooltipOrSubLabel;
                presentations.Add(new DialogueChoicePresentation(i, answerText, subLabel));
            }

            RebuildChoices(presentations, settings, onPick);
        }

        /// <summary>Destroys all choice buttons and hides the choices container.</summary>
        public void ClearChoices()
        {
            if (!choicesContainer) return;
            for (int i = choicesContainer.childCount - 1; i >= 0; i--)
                DestroyChoiceObject(choicesContainer.GetChild(i).gameObject);
            SetChoicesVisible(false);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static void SafeActive(GameObject go, bool v) { if (go) go.SetActive(v); }

        private static void DestroyChoiceObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }

        private DialogueRuntimeUISettings ResolveRuntimeUiSettings()
        {
            if (_appliedSettings != null)
            {
                return _appliedSettings;
            }

            var manager = DialogManager.Instance;
            if (manager != null && manager.RuntimeUiSettings != null)
            {
                return manager.RuntimeUiSettings;
            }

            return DialogSettingsRuntime.UI;
        }

        private bool IsFeatureEnabled(DialogOptionalUiFeature feature)
        {
            return DialogOptionalUiFeaturePolicy.IsEnabled(ResolveRuntimeUiSettings(), feature);
        }

        private bool ShouldLogDebug()
        {
            return DialogSettingsRuntime.DebugLogsEnabled && (_doDebug || DialogSettingsRuntime.DoDebug());
        }

        private void WarnMissingBinding(DialogOptionalUiFeature feature, string bindingName)
        {
            if (!IsFeatureEnabled(feature))
            {
                return;
            }

            var warningKey = feature + ":" + bindingName;
            if (_missingBindingWarnings.Add(warningKey))
            {
                Debug.LogWarning($"[DialogUIController] The {DialogOptionalUiFeaturePolicy.GetDisplayName(feature)} feature is enabled, but '{bindingName}' is not assigned.", this);
            }
        }

        private void SetOptionalGameObjectActive(DialogOptionalUiFeature feature, GameObject target, bool visible, string bindingName)
        {
            if (target == null)
            {
                if (visible)
                {
                    WarnMissingBinding(feature, bindingName);
                }

                return;
            }

            target.SetActive(visible && IsFeatureEnabled(feature));
        }

        private void SetOptionalGraphicEnabled(DialogOptionalUiFeature feature, Graphic graphic, bool visible, string bindingName)
        {
            if (graphic == null)
            {
                if (visible)
                {
                    WarnMissingBinding(feature, bindingName);
                }

                return;
            }

            graphic.enabled = visible && IsFeatureEnabled(feature);
        }

        private GameObject ResolveSkipButtonObject()
        {
            if (skipButton != null)
            {
                return skipButton;
            }

            var button = ResolveSkipButton();
            return button != null ? button.gameObject : null;
        }

        private GameObject ResolveHistoryButtonObject()
        {
            var button = ResolveHistoryButton();
            return button != null ? button.gameObject : null;
        }

        private GameObject ResolveSettingsButtonObject()
        {
            var button = ResolveSettingsButton();
            return button != null ? button.gameObject : null;
        }

        private GameObject ResolveAutoPlayButtonObject()
        {
            var button = ResolveAutoPlayButton();
            return button != null ? button.gameObject : null;
        }

        private GameObject ResolveLanguageButtonObject()
        {
            var button = ResolveLanguageButton();
            return button != null ? button.gameObject : null;
        }

        private RectTransform ResolvePortraitLayoutRoot()
        {
            if (_portraitLayoutRoot != null)
            {
                return _portraitLayoutRoot;
            }

            return portraitImage != null ? portraitImage.rectTransform : null;
        }

        private RectTransform ResolveContentLayoutRoot()
        {
            if (_dialogContentLayoutRoot != null)
            {
                return _dialogContentLayoutRoot;
            }

            return dialogText != null ? dialogText.rectTransform : null;
        }

        private void SetTextVisible(bool visible)
        {
            if (dialogText != null)
            {
                dialogText.gameObject.SetActive(visible);
            }
        }

        // TODO: FIX this later — always show for now, don't toggle on/off
        private void SetSpeakerVisible(bool visible)
        {
            SetOptionalGameObjectActive(DialogOptionalUiFeature.SpeakerName, speakerName != null ? speakerName.gameObject : null, visible, nameof(speakerName));

            if (_nameplateImage != null)
            {
                _nameplateImage.gameObject.SetActive(visible && IsFeatureEnabled(DialogOptionalUiFeature.SpeakerName));
            }
        }

        // TODO: FIX this later — always show for now, don't toggle on/off
        private void SetPortraitVisible(bool visible)
        {
            SetOptionalGraphicEnabled(DialogOptionalUiFeature.Portrait, portraitImage, visible, nameof(portraitImage));

            if (_avatarFrameImage != null)
            {
                _avatarFrameImage.enabled = visible && IsFeatureEnabled(DialogOptionalUiFeature.Portrait);
            }
        }

        private void ApplyAutoPlayThemeSprite(DialogThemeSO theme)
        {
            if (theme == null)
            {
                return;
            }

            var sprite = _isAutoPlayActive ? theme.autoPlayActiveIcon : theme.autoPlayNormalIcon;
            if (_autoPlayButtonIconImage != null && sprite != null)
            {
                _autoPlayButtonIconImage.sprite = sprite;
            }
        }

        private GameObject ResolveChoiceButtonPrefab()
        {
            var activeTheme = _currentTheme != null ? _currentTheme : DialogUIManager.ActiveTheme;
            if (activeTheme != null)
            {
                _currentTheme = activeTheme;
            }

            var themedPrefab = activeTheme != null ? activeTheme.choiceButtonPrefabOverride : null;
            if (ValidateChoicePrefab(themedPrefab, "theme override"))
            {
                return themedPrefab;
            }

            if (themedPrefab != null)
            {
                Debug.LogWarning($"[DialogUIController] Theme '{activeTheme.themeName}' choice prefab override is invalid. Falling back to the controller choiceButtonPrefab.");
            }

            var fallbackPrefab = _choiceButtonPrefabFallback != null ? _choiceButtonPrefabFallback : choiceButtonPrefab;
            if (ValidateChoicePrefab(fallbackPrefab, "controller fallback"))
            {
                return fallbackPrefab;
            }

            if (fallbackPrefab == null)
            {
                Debug.LogError("[DialogUIController] No usable choice prefab found. Assign a fallback choice prefab in DialogUIManager or provide a valid theme choice prefab override.");
            }
            else
            {
                Debug.LogError($"[DialogUIController] Controller fallback choice prefab '{fallbackPrefab.name}' is invalid. It must include a {nameof(ChoiceButtonView)} component and a Button component.");
            }

            return null;
        }

        private bool ValidateChoicePrefab(GameObject prefab, string sourceLabel)
        {
            if (prefab == null)
            {
                return false;
            }

            if (prefab.GetComponent<ChoiceButtonView>() == null)
            {
                Debug.LogWarning($"[DialogUIController] The {sourceLabel} choice prefab '{prefab.name}' is missing a {nameof(ChoiceButtonView)} component.");
                return false;
            }

            if (prefab.GetComponent<Button>() == null && prefab.GetComponentInChildren<Button>(true) == null)
            {
                Debug.LogWarning($"[DialogUIController] The {sourceLabel} choice prefab '{prefab.name}' is missing a Button component.");
                return false;
            }

            return true;
        }

        private Button ResolveSkipButton()
        {
            if (skipButton != null)
            {
                var button = skipButton.GetComponent<Button>();
                if (button != null)
                {
                    return button;
                }

                button = skipButton.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    return button;
                }
            }

            var resolved = ResolveButtonFromImage(_skipButtonBackgroundImage)
                           ?? ResolveButtonFromImage(_skipButtonIconImage)
                           ?? ResolveNamedButton("skip");
            if (resolved != null && skipButton == null)
            {
                skipButton = resolved.gameObject;
            }

            if (resolved == null)
            {
                WarnMissingBinding(DialogOptionalUiFeature.SkipButton, "skip button");
            }

            return resolved;
        }

        private Button ResolveSkipLineButton()
        {
            if (skipLineButton != null)
            {
                var button = skipLineButton.GetComponent<Button>();
                if (button != null)
                {
                    return button;
                }

                button = skipLineButton.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    return button;
                }
            }

            var resolved = ResolveButtonFromImage(_skipLineButtonBackgroundImage)
                           ?? ResolveButtonFromImage(_skipLineButtonIconImage)
                           ?? ResolveNamedButton("skipLine");
            if (resolved != null && skipLineButton == null)
            {
                skipLineButton = resolved.gameObject;
            }

            if (resolved == null)
            {
                WarnMissingBinding(DialogOptionalUiFeature.SkipButton, "skip line button");
            }

            return resolved;
        }



        private Button ResolveHistoryButton()
        {
            if (historyPanelButton != null)
            {
                return historyPanelButton;
            }

            historyPanelButton = ResolveButtonFromImage(_historyButtonBackgroundImage)
                                ?? ResolveButtonFromImage(_historyButtonIconImage)
                                ?? ResolveNamedButton("history");
            if (historyPanelButton == null)
            {
                WarnMissingBinding(DialogOptionalUiFeature.HistoryButton, "history button");
            }

            return historyPanelButton;
        }

        private Button ResolveSettingsButton()
        {
            if (settingsPanelButton != null)
            {
                return settingsPanelButton;
            }

            settingsPanelButton = ResolveButtonFromImage(_settingsButtonBackgroundImage)
                                ?? ResolveButtonFromImage(_settingsButtonIconImage)
                                ?? ResolveNamedButton("settings");
            if (settingsPanelButton == null)
            {
                WarnMissingBinding(DialogOptionalUiFeature.SettingsButton, "settings button");
            }

            return settingsPanelButton;
        }

        private Button ResolveAutoPlayButton()
        {
            if (autoPlayButton != null)
            {
                return autoPlayButton;
            }

            autoPlayButton = ResolveButtonFromImage(_autoPlayButtonBackgroundImage)
                             ?? ResolveButtonFromImage(_autoPlayButtonIconImage)
                             ?? ResolveNamedButton("autoplay")
                             ?? ResolveNamedButton("auto");
            if (autoPlayButton == null)
            {
                WarnMissingBinding(DialogOptionalUiFeature.AutoButton, "autoplay button");
            }

            return autoPlayButton;
        }

        private Button ResolveLanguageButton()
        {
            if (languageButton != null)
            {
                return languageButton;
            }

            languageButton = ResolveNamedButton("language")
                             ?? ResolveNamedButton("settings")
                             ?? ResolveNamedButton("locale");
            return languageButton;
        }

        private Button ResolveButtonFromImage(Image image)
        {
            if (image == null)
            {
                return null;
            }

            var button = image.GetComponent<Button>();
            if (button != null)
            {
                return button;
            }

            return image.GetComponentInParent<Button>();
        }

        private Button ResolveNamedButton(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var searchRoot = panelRoot != null ? panelRoot.transform : transform;
            var buttons = searchRoot.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                var candidate = buttons[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void BindRuntimeButton(string label, ref Button cachedButton, ref UnityAction cachedAction, Button targetButton, UnityAction newAction)
        {
            if (cachedButton != null && cachedAction != null)
            {
                cachedButton.onClick.RemoveListener(cachedAction);
            }

            cachedButton = targetButton;
            cachedAction = null;

            if (targetButton == null || newAction == null)
            {
                return;
            }

            cachedAction = () => newAction.Invoke();
            targetButton.onClick.AddListener(cachedAction);
        }

        private static void SetSprite(Image img, Sprite s)
        {
            if (img != null && s != null) img.sprite = s;
        }

        private static void SetColor(TextMeshProUGUI tmp, Color c)
        {
            if (tmp != null) tmp.color = c;
        }

        private static void SetFont(TextMeshProUGUI tmp, TMP_FontAsset font)
        {
            if (tmp != null && font != null)
            {
                tmp.font = font;
            }
        }
    }
}
