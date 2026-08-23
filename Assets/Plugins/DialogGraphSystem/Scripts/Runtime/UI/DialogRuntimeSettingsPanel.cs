using System;
using System.Collections.Generic;
using Christina.UI;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Settings;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.UI.Theming;
using DialogSystem.Runtime.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DialogSystem.Runtime.UI
{
    /// <summary>
    /// Runtime binder for the settings overlay.
    /// Owns language/theme dropdowns and the common text/audio toggles and sliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogRuntimeSettingsPanel : MonoBehaviour
    {
        private const float TextSpeedSliderMin = 1f;
        private const float TextSpeedSliderMax = 120f;

        private DialogManager _manager;
        private DialogUIManager _uiManager;

        [Header("Localization")]
        [SerializeField] private DialogLocalizationRuntimeSettings _localizationSettings;
        [SerializeField] private TMP_Dropdown _languageDropdown;

        [Header("Theme")]
        [SerializeField] private TMP_Dropdown _themeDropdown;

        [Header("Text")]
        [SerializeField] private Slider _textSpeedSlider;
        [SerializeField] private TMP_Text _textSpeedValueLabel;
        [SerializeField] private ToggleSwitch _autoAdvanceToggle;

        [Header("Audio")]
        [SerializeField] private Slider _voiceVolumeSlider;
        [SerializeField] private TMP_Text _voiceVolumeValueLabel;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private TMP_Text _sfxVolumeValueLabel;

        [Header("Visibility Targets (Optional)")]
        [SerializeField] private GameObject _languageDropdownRow;
        [SerializeField] private GameObject _themeDropdownRow;
        [SerializeField] private GameObject _textSpeedRow;
        [SerializeField] private GameObject _autoAdvanceRow;
        [SerializeField] private GameObject _voiceVolumeRow;
        [SerializeField] private GameObject _sfxVolumeRow;

        [Header("Theme Targets (Optional)")]
        [SerializeField] private Image _panelBackgroundImage;
        [SerializeField] private Image _headerBackgroundImage;
        [SerializeField] private Image _closeButtonImage;
        [SerializeField] private Image[] _rowBackgroundImages;
        [SerializeField] private Image[] _controlBackgroundImages;
        [SerializeField] private Image[] _accentImages;
        [SerializeField] private TMP_Text[] _labelTextTargets;
        [SerializeField] private TMP_Text[] _valueTextTargets;

        private readonly List<string> _localeCodes = new();
        private readonly List<string> _themeNames = new();
        private bool _isRefreshing;

        private void OnEnable()
        {
            Bind(true);
            DisableLegacyLanguageController();
            RefreshFromRuntime();
            DialogLocalizationRuntime.Instance.OnLocaleChanged += RefreshLanguageSelection;
        }

        private void OnDisable()
        {
            Bind(false);
            DialogLocalizationRuntime.Instance.OnLocaleChanged -= RefreshLanguageSelection;
        }

        public void Initialize(DialogManager dialogManager, DialogUIManager uiManager)
        {
            if (dialogManager != null)
            {
                _manager = dialogManager;
            }

            if (uiManager != null)
            {
                _uiManager = uiManager;
            }

            DisableLegacyLanguageController();
            InitializeLocalizationRuntime();
            ConfigureControlRanges();
            RefreshFromRuntime();
        }

        public void RefreshFromRuntime()
        {
            var manager = ResolveManager();
            var uiManager = ResolveUiManager();
            if (manager == null)
            {
                return;
            }

            InitializeLocalizationRuntime();
            ConfigureControlRanges();
            _isRefreshing = true;

            RefreshLanguageDropdown();
            RefreshThemeDropdown(uiManager);

            var runtimeUiSettings = ResolveRuntimeUiSettings(manager, uiManager);
            ApplyVisibility(runtimeUiSettings);
            ApplyTheme(runtimeUiSettings?.settingsTheme);

            if (_textSpeedSlider != null && manager.RuntimeTextSettings != null)
            {
                _textSpeedSlider.SetValueWithoutNotify(manager.RuntimeTextSettings.charsPerSecond);
                SetText(_textSpeedValueLabel, $"{Mathf.RoundToInt(manager.RuntimeTextSettings.charsPerSecond)} cps");
            }

            if (_autoAdvanceToggle != null && manager.RuntimeTextSettings != null)
            {
                _autoAdvanceToggle.SetIsOnWithoutNotify(manager.RuntimeTextSettings.autoAdvance);
            }

            if (_voiceVolumeSlider != null && manager.RuntimeAudioSettings != null)
            {
                _voiceVolumeSlider.SetValueWithoutNotify(manager.RuntimeAudioSettings.voiceVolume);
                SetText(_voiceVolumeValueLabel, ToPercent(manager.RuntimeAudioSettings.voiceVolume));
            }

            if (_sfxVolumeSlider != null && manager.RuntimeAudioSettings != null)
            {
                _sfxVolumeSlider.SetValueWithoutNotify(manager.RuntimeAudioSettings.sfxVolume);
                SetText(_sfxVolumeValueLabel, ToPercent(manager.RuntimeAudioSettings.sfxVolume));
            }

            _isRefreshing = false;
        }

        public void ApplyTheme(DialogSettingsThemeSO theme)
        {
            if (theme == null)
            {
                return;
            }

            SetThemedImage(
                _panelBackgroundImage != null ? _panelBackgroundImage : GetComponent<Image>(),
                theme.panelBackground,
                theme.panelTint);

            SetThemedImage(
                _headerBackgroundImage != null ? _headerBackgroundImage : FindImageByName("Header"),
                theme.headerBackground,
                theme.headerTint);

            SetThemedImage(
                _closeButtonImage != null ? _closeButtonImage : FindImageByName("CloseButton"),
                theme.closeButton,
                theme.controlTint);

            foreach (var image in ResolveImages(_rowBackgroundImages, IsSettingsRowImage))
            {
                SetThemedImage(image, theme.rowBackground, theme.rowTint);
            }

            foreach (var image in ResolveImages(_controlBackgroundImages, IsSettingsControlImage))
            {
                SetThemedImage(image, theme.controlBackground, theme.controlTint);
            }

            foreach (var image in ResolveImages(_accentImages, IsSettingsAccentImage))
            {
                SetImageColor(image, theme.accentColor);
            }

            foreach (var text in ResolveTexts(_labelTextTargets, IsSettingsLabelText))
            {
                SetTextColor(text, theme.textColor);
                SetTextFont(text, theme.font);
            }

            foreach (var text in ResolveTexts(_valueTextTargets, IsSettingsValueText))
            {
                SetTextColor(text, theme.valueTextColor);
                SetTextFont(text, theme.font);
            }
        }

        private void OnLanguageDropdownChanged(int index)
        {
            if (_isRefreshing || index < 0 || index >= _localeCodes.Count)
            {
                return;
            }

            if (DialogLocalizationRuntime.Instance.SetActiveLocale(_localeCodes[index]))
            {
                DialogRuntimeSettingsPersistence.SaveLocale(_localeCodes[index]);
            }
        }

        private void OnThemeDropdownChanged(int index)
        {
            if (_isRefreshing || index < 0 || index >= _themeNames.Count)
            {
                return;
            }

            var uiManager = ResolveUiManager();
            if (uiManager == null)
            {
                return;
            }

            var themeName = _themeNames[index];
            if (uiManager.ApplyThemeByName(themeName))
            {
                DialogRuntimeSettingsPersistence.SaveTheme(themeName);
            }
        }

        private void OnTextSpeedChanged(float value)
        {
            if (_isRefreshing)
            {
                return;
            }

            ResolveManager()?.SetRuntimeTextSpeed(value);
            SetText(_textSpeedValueLabel, $"{Mathf.RoundToInt(value)} cps");
        }

        private void OnAutoAdvanceChanged(bool value)
        {
            if (_isRefreshing)
            {
                return;
            }

            ResolveManager()?.SetRuntimeAutoAdvance(value);
        }

        private void OnVoiceVolumeChanged(float value)
        {
            if (_isRefreshing)
            {
                return;
            }

            ResolveManager()?.SetRuntimeVoiceVolume(value);
            SetText(_voiceVolumeValueLabel, ToPercent(value));
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (_isRefreshing)
            {
                return;
            }

            ResolveManager()?.SetRuntimeSfxVolume(value);
            SetText(_sfxVolumeValueLabel, ToPercent(value));
        }

        private void RefreshLanguageDropdown()
        {
            if (_languageDropdown == null)
            {
                return;
            }

            _localeCodes.Clear();
            _languageDropdown.ClearOptions();

            var labels = new List<string>();
            foreach (var localeCode in DialogLocalizationRuntime.Instance.AvailableLocales)
            {
                if (string.IsNullOrWhiteSpace(localeCode))
                {
                    continue;
                }

                _localeCodes.Add(localeCode);
                labels.Add(DialogLocalizationRuntime.Instance.GetDisplayName(localeCode));
            }

            _languageDropdown.AddOptions(labels);
            _languageDropdown.interactable = _localeCodes.Count > 1;
            RefreshLanguageSelection();
        }

        private void RefreshLanguageSelection()
        {
            if (_languageDropdown == null)
            {
                return;
            }

            _isRefreshing = true;

            if (_localeCodes.Count == 0)
            {
                _languageDropdown.SetValueWithoutNotify(0);
                _languageDropdown.RefreshShownValue();
                _isRefreshing = false;
                return;
            }

            var activeLocaleCode = DialogLocalizationRuntime.Instance.ActiveLocaleCode;
            var index = _localeCodes.FindIndex(code => string.Equals(code, activeLocaleCode, StringComparison.OrdinalIgnoreCase));
            _languageDropdown.SetValueWithoutNotify(index < 0 ? 0 : index);
            _languageDropdown.RefreshShownValue();

            _isRefreshing = false;
        }

        private void RefreshThemeDropdown(DialogUIManager uiManager)
        {
            if (_themeDropdown == null || uiManager == null)
            {
                return;
            }

            _themeNames.Clear();
            _themeDropdown.ClearOptions();

            var labels = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddTheme(DialogThemeSO theme)
            {
                if (theme == null || string.IsNullOrWhiteSpace(theme.themeName) || !seen.Add(theme.themeName))
                {
                    return;
                }

                _themeNames.Add(theme.themeName);
                labels.Add(theme.themeName);
            }

            AddTheme(uiManager.DefaultTheme);

            if (uiManager.AvailableThemes != null)
            {
                for (var i = 0; i < uiManager.AvailableThemes.Count; i++)
                {
                    AddTheme(uiManager.AvailableThemes[i]);
                }
            }

            if (uiManager.ThemeMappings != null)
            {
                for (var i = 0; i < uiManager.ThemeMappings.Count; i++)
                {
                    AddTheme(uiManager.ThemeMappings[i]?.theme);
                }
            }

            _themeDropdown.AddOptions(labels);
            _themeDropdown.interactable = _themeNames.Count > 1;

            var currentThemeName = uiManager.CurrentTheme != null ? uiManager.CurrentTheme.themeName : uiManager.DefaultTheme != null ? uiManager.DefaultTheme.themeName : string.Empty;
            var selectedIndex = _themeNames.FindIndex(name => string.Equals(name, currentThemeName, StringComparison.OrdinalIgnoreCase));
            _themeDropdown.SetValueWithoutNotify(selectedIndex < 0 ? 0 : selectedIndex);
            _themeDropdown.RefreshShownValue();
        }

        private void InitializeLocalizationRuntime()
        {
            if (_localizationSettings != null)
            {
                DialogLocalizationRuntime.Instance.Initialize(_localizationSettings);
            }
            else if (DialogLocalizationRuntime.Instance.Settings == null)
            {
                DialogLocalizationRuntime.Instance.Initialize();
            }
        }

        private void ConfigureControlRanges()
        {
            if (_textSpeedSlider != null)
            {
                _textSpeedSlider.minValue = TextSpeedSliderMin;
                _textSpeedSlider.maxValue = TextSpeedSliderMax;
                _textSpeedSlider.wholeNumbers = true;
            }

            if (_voiceVolumeSlider != null)
            {
                _voiceVolumeSlider.minValue = 0f;
                _voiceVolumeSlider.maxValue = 1f;
            }

            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.minValue = 0f;
                _sfxVolumeSlider.maxValue = 1f;
            }
        }

        private void DisableLegacyLanguageController()
        {
            var legacyController = GetComponentInChildren<DialogRuntimeLanguageController>(true);
            if (legacyController != null)
            {
                legacyController.enabled = false;
            }
        }

        private DialogManager ResolveManager()
        {
            if (_manager == null)
            {
                _manager = DialogManager.Instance != null
                    ? DialogManager.Instance
                    : DialogRuntimeUnityCompatibility.FindFirst<DialogManager>(includeInactive: true);
            }

            return _manager;
        }

        private DialogUIManager ResolveUiManager()
        {
            if (_uiManager == null)
            {
                _uiManager = DialogUIManager.Instance != null
                    ? DialogUIManager.Instance
                    : DialogRuntimeUnityCompatibility.FindFirst<DialogUIManager>(includeInactive: true);
            }

            return _uiManager;
        }

        private DialogueRuntimeUISettings ResolveRuntimeUiSettings(DialogManager manager, DialogUIManager uiManager)
        {
            var source = manager != null && manager.RuntimeUiSettings != null
                ? manager.RuntimeUiSettings
                : DialogSettingsRuntime.UI;

            return uiManager != null ? uiManager.GetEffectiveRuntimeUiSettings(source) : source;
        }

        private void ApplyVisibility(DialogueRuntimeUISettings settings)
        {
            if (settings == null)
            {
                return;
            }

            SetSettingsRowVisible(
                DialogOptionalUiFeature.SettingsLanguageDropdown,
                _languageDropdownRow,
                _languageDropdown,
                settings.showSettingsPanel && settings.showSettingsLanguageDropdown,
                "LanguageDropdownContainer");

            SetSettingsRowVisible(
                DialogOptionalUiFeature.SettingsThemeDropdown,
                _themeDropdownRow,
                _themeDropdown,
                settings.showSettingsPanel && settings.showSettingsThemeDropdown,
                "ThemeDropdownContainer");

            SetSettingsRowVisible(
                DialogOptionalUiFeature.SettingsTextSpeed,
                _textSpeedRow,
                _textSpeedSlider,
                settings.showSettingsPanel && settings.showSettingsTextSpeed,
                "Text Speed Slider",
                "TextSpeedSlider");

            SetSettingsRowVisible(
                DialogOptionalUiFeature.SettingsAutoAdvance,
                _autoAdvanceRow,
                _autoAdvanceToggle,
                settings.showSettingsPanel && settings.showSettingsAutoAdvance,
                "AutoplayToggle",
                "AutoAdvanceToggle");

            SetSettingsRowVisible(
                DialogOptionalUiFeature.SettingsVoiceVolume,
                _voiceVolumeRow,
                _voiceVolumeSlider,
                settings.showSettingsPanel && settings.showSettingsVoiceVolume,
                "Audio Volume Slider",
                "Audio_VolumeSlider");

            SetSettingsRowVisible(
                DialogOptionalUiFeature.SettingsSfxVolume,
                _sfxVolumeRow,
                _sfxVolumeSlider,
                settings.showSettingsPanel && settings.showSettingsSfxVolume,
                "SFX Volume Slider",
                "SFC Volume Slider",
                "SFX_VolumeSlider");
        }

        private void SetSettingsRowVisible(DialogOptionalUiFeature feature, GameObject explicitRow, Component anchor, bool visible, params string[] markers)
        {
            var target = explicitRow != null ? explicitRow : ResolveSettingsRow(anchor, markers);
            if (target == null)
            {
                if (visible)
                {
                    Debug.LogWarning($"[DialogRuntimeSettingsPanel] The {DialogOptionalUiFeaturePolicy.GetDisplayName(feature)} is enabled, but no row target could be resolved.", this);
                }

                return;
            }

            target.SetActive(visible && DialogOptionalUiFeaturePolicy.IsEnabled(ResolveRuntimeUiSettings(ResolveManager(), ResolveUiManager()), feature));
        }

        private GameObject ResolveSettingsRow(Component anchor, params string[] markers)
        {
            GameObject named = null;
            if (markers != null)
            {
                for (var i = 0; i < markers.Length && named == null; i++)
                {
                    named = FindDescendantGameObject(markers[i]);
                }
            }

            if (named != null)
            {
                return FindHighestMarkedAncestor(named.transform, markers) ?? named;
            }

            if (anchor == null)
            {
                return null;
            }

            return FindHighestMarkedAncestor(anchor.transform, markers) ?? anchor.gameObject;
        }

        private GameObject FindHighestMarkedAncestor(Transform start, params string[] markers)
        {
            if (start == null)
            {
                return null;
            }

            GameObject match = null;
            var current = start;
            while (current != null && current != transform)
            {
                if (MatchesAnyMarker(current.name, markers))
                {
                    match = current.gameObject;
                }

                current = current.parent;
            }

            return match;
        }

        private GameObject FindDescendantGameObject(string marker)
        {
            if (string.IsNullOrWhiteSpace(marker))
            {
                return null;
            }

            var children = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child != null && NameContains(child.name, marker))
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private Image FindImageByName(string marker)
        {
            var target = FindDescendantGameObject(marker);
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<Image>() ?? target.GetComponentInChildren<Image>(true);
        }

        private static bool MatchesAnyMarker(string name, params string[] markers)
        {
            if (markers == null || markers.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < markers.Length; i++)
            {
                if (NameContains(name, markers[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NameContains(string name, string marker)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(marker))
            {
                return false;
            }

            return NormalizeName(name).Contains(NormalizeName(marker));
        }

        private static string NormalizeName(string value)
        {
            return value
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private IEnumerable<Image> ResolveImages(Image[] explicitImages, Predicate<Image> fallbackPredicate)
        {
            if (explicitImages != null && explicitImages.Length > 0)
            {
                for (var i = 0; i < explicitImages.Length; i++)
                {
                    if (explicitImages[i] != null)
                    {
                        yield return explicitImages[i];
                    }
                }

                yield break;
            }

            var images = GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image != null && fallbackPredicate(image))
                {
                    yield return image;
                }
            }
        }

        private IEnumerable<TMP_Text> ResolveTexts(TMP_Text[] explicitTexts, Predicate<TMP_Text> fallbackPredicate)
        {
            if (explicitTexts != null && explicitTexts.Length > 0)
            {
                for (var i = 0; i < explicitTexts.Length; i++)
                {
                    if (explicitTexts[i] != null)
                    {
                        yield return explicitTexts[i];
                    }
                }

                yield break;
            }

            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text != null && fallbackPredicate(text))
                {
                    yield return text;
                }
            }
        }

        private bool IsSettingsRowImage(Image image)
        {
            var targetName = image.gameObject.name;
            var parentName = image.transform.parent != null ? image.transform.parent.name : string.Empty;
            return NameContains(targetName, "Item Background") ||
                   NameContains(parentName, "Item") ||
                   NameContains(targetName, "LableHolder") ||
                   NameContains(targetName, "LabelHolder");
        }

        private bool IsSettingsControlImage(Image image)
        {
            var targetName = image.gameObject.name;
            return NameContains(targetName, "Dropdown") ||
                   NameContains(targetName, "Background") ||
                   NameContains(targetName, "SliderValueHolder") ||
                   NameContains(targetName, "Toggle") ||
                   NameContains(targetName, "Handle");
        }

        private bool IsSettingsAccentImage(Image image)
        {
            var targetName = image.gameObject.name;
            return NameContains(targetName, "Fill") || NameContains(targetName, "Handle");
        }

        private bool IsSettingsValueText(TMP_Text text)
        {
            return text == _textSpeedValueLabel ||
                   text == _voiceVolumeValueLabel ||
                   text == _sfxVolumeValueLabel ||
                   NameContains(text.gameObject.name, "SliderValue");
        }

        private bool IsSettingsLabelText(TMP_Text text)
        {
            return !IsSettingsValueText(text);
        }

        private static void SetThemedImage(Image image, Sprite sprite, Color color)
        {
            if (image == null)
            {
                return;
            }

            if (sprite != null)
            {
                image.sprite = sprite;
            }

            SetImageColor(image, color);
        }

        private static void SetImageColor(Image image, Color color)
        {
            if (image != null && HasColorOverride(color))
            {
                image.color = color;
            }
        }

        private static void SetTextColor(TMP_Text text, Color color)
        {
            if (text != null && HasColorOverride(color))
            {
                text.color = color;
            }
        }

        private static void SetTextFont(TMP_Text text, TMP_FontAsset font)
        {
            if (text != null && font != null)
            {
                text.font = font;
            }
        }

        private static bool HasColorOverride(Color color)
        {
            return color.a > 0.001f;
        }

        private void Bind(bool subscribe)
        {
            BindDropdown(_languageDropdown, OnLanguageDropdownChanged, subscribe);
            BindDropdown(_themeDropdown, OnThemeDropdownChanged, subscribe);
            BindSlider(_textSpeedSlider, OnTextSpeedChanged, subscribe);
            BindSlider(_voiceVolumeSlider, OnVoiceVolumeChanged, subscribe);
            BindSlider(_sfxVolumeSlider, OnSfxVolumeChanged, subscribe);
            BindToggle(_autoAdvanceToggle, OnAutoAdvanceChanged, subscribe);
        }

        private static void BindDropdown(TMP_Dropdown dropdown, UnityEngine.Events.UnityAction<int> action, bool subscribe)
        {
            if (dropdown == null || action == null)
            {
                return;
            }

            dropdown.onValueChanged.RemoveListener(action);
            if (subscribe)
            {
                dropdown.onValueChanged.AddListener(action);
            }
        }

        private static void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> action, bool subscribe)
        {
            if (slider == null || action == null)
            {
                return;
            }

            slider.onValueChanged.RemoveListener(action);
            if (subscribe)
            {
                slider.onValueChanged.AddListener(action);
            }
        }

        private static void BindToggle(ToggleSwitch toggle, Action<bool> action, bool subscribe)
        {
            if (toggle == null || action == null)
            {
                return;
            }

            toggle.ValueChanged -= action;
            if (subscribe)
            {
                toggle.ValueChanged += action;
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private static string ToPercent(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }
}
