using System;
using System.Collections.Generic;
using DialogSystem.Runtime;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.DialogHistory;
using DialogSystem.Runtime.Settings;
using DialogSystem.Runtime.UI;
using DialogSystem.Runtime.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace DialogSystem.Runtime.UI.Theming
{
    public enum DialogThemeType
    {
        Default = 0,
        Dark = 1,
        Fantasy = 2,
        SciFi = 3,
        Modern = 4,
        Warm = 5,
        Minimal = 6
    }

    [Serializable]
    public sealed class DialogThemeMapping
    {
        public DialogThemeType themeType = DialogThemeType.Default;
        public DialogThemeSO theme;
    }

    /// <summary>
    /// Owns runtime dialog UI creation, theme selection, and theme application.
    /// Dialog flow code should request a ready <see cref="DialogUIController"/> from here
    /// instead of instantiating or theming UI directly.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogUIManager : MonoBehaviour
    {
        public static DialogUIManager Instance { get; private set; }

        public static event Action<DialogThemeSO> OnThemeChanged;

        public static DialogThemeSO ActiveTheme { get; private set; }

        [Header("Theme Selection")]
        [SerializeField] private DialogThemeSO _defaultTheme;
        [SerializeField] private List<DialogThemeMapping> _themeMappings = new();

        [Header("Theme Library (Legacy Order)")]
        [SerializeField] private List<DialogThemeSO> _themes = new();
        [SerializeField] private int _startThemeIndex;

        [Header("UI Prefabs")]
        [SerializeField] private GameObject _dialogCanvasPrefab;
        [SerializeField] private DialogUIController _dialogPanelPrefab;
        [SerializeField] private GameObject _fallbackChoiceButtonPrefab;
        [SerializeField] private bool _dontDestroyOnLoad;

        [Header("Existing Scene References (Optional)")]
        [SerializeField] private DialogUIController _dialogPanel;
        [SerializeField] private DialogueHistoryView _historyPanel;
        [SerializeField] private DialogueHistory _dialogueHistory;
        [SerializeField] private GameObject _settingsPanel;

        [Header("Runtime UI Visibility Overrides")]
        [SerializeField] private bool _overrideRuntimeUiVisibility;
        [SerializeField] private bool _showBackgroundPanel = true;
        [SerializeField] private bool _showSpeakerName = true;
        [SerializeField] private bool _showPortrait = true;
        [SerializeField] private bool _showSkipButton = true;
        [SerializeField] private bool _showAutoButton = true;
        [SerializeField] private bool _showHistoryButton = true;
        [SerializeField] private bool _showSettingsButton = true;
        [SerializeField] private bool _showLanguageButton = true;
        [SerializeField] private bool _showContinueIndicator = true;
        [SerializeField] private bool _showAutoSkipIcon = true;
        [SerializeField] private bool _showChoicePanel = true;
        [SerializeField] private bool _showHistoryPanel = true;
        [SerializeField] private bool _showSettingsPanel = true;
        [SerializeField] private bool _showSettingsLanguageDropdown = true;
        [SerializeField] private bool _showSettingsThemeDropdown = true;
        [SerializeField] private bool _showSettingsTextSpeed = true;
        [SerializeField] private bool _showSettingsAutoAdvance = true;
        [SerializeField] private bool _showSettingsVoiceVolume = true;
        [SerializeField] private bool _showSettingsSfxVolume = true;

        [Header("Runtime State")]
        [SerializeField] private GameObject _runtimeDialogCanvasInstance;

        private DialogThemeSO _currentTheme;
        private DialogThemeType _currentThemeType = DialogThemeType.Default;
        private int _currentIndex = -1;
        private bool _didWarnMissingHistory;
        private bool _didWarnMissingSettingsPanel;
        private DialogManager _owningDialogManager;
        private DialogSystem.Runtime.Settings.Panels.DialogueRuntimeUISettings _runtimeUiSettings;
        private DialogSystem.Runtime.Settings.Panels.DialogueRuntimeUISettings _runtimeUiSettingsOverrideInstance;
        private Button _runtimeSettingsCloseButton;
        private Canvas _runtimeSettingsCanvas;
        private CanvasGroup _runtimeSettingsCanvasGroup;
        private bool _isSettingsPanelOpen;
        private const int SettingsPanelSortingOrder = 100;

        public DialogThemeSO CurrentTheme => _currentTheme;
        public DialogThemeType CurrentThemeType => _currentThemeType;
        public int CurrentIndex => _currentIndex;
        public IReadOnlyList<DialogThemeSO> AvailableThemes => _themes;
        public IReadOnlyList<DialogThemeMapping> ThemeMappings => _themeMappings;
        public bool OverrideRuntimeUiVisibility => _overrideRuntimeUiVisibility;
        public DialogThemeSO DefaultTheme => GetDefaultThemeOrNull();
        public DialogUIController CurrentDialogUI => _dialogPanel;
        public DialogueHistoryView CurrentHistoryView => _historyPanel;
        public DialogueHistory CurrentDialogueHistory => _dialogueHistory;
        public GameObject DialogCanvasPrefab => _dialogCanvasPrefab;
        public DialogUIController DialogUIPrefab => _dialogPanelPrefab;
        public GameObject FallbackChoiceButtonPrefab => _fallbackChoiceButtonPrefab;
        public GameObject RuntimeDialogCanvasInstance => _runtimeDialogCanvasInstance;
        public bool PersistAcrossScenes => _dontDestroyOnLoad;

        public DialogSystem.Runtime.Settings.Panels.DialogueRuntimeUISettings GetEffectiveRuntimeUiSettings(DialogSystem.Runtime.Settings.Panels.DialogueRuntimeUISettings source = null)
        {
            return BuildEffectiveRuntimeUiSettings(source);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (_dontDestroyOnLoad)
{
                DontDestroyOnLoad(gameObject);
            }

            Initialize(null);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveTheme, _currentTheme))
            {
                ActiveTheme = null;
            }
        }

        public DialogUIController GetOrCreateDialogUI(DialogThemeType themeType = DialogThemeType.Default)
        {
            if (!Initialize(null))
            {
                return null;
            }

            var resolvedTheme = ResolveTheme(themeType, logWarnings: true);
            if (resolvedTheme == null)
            {
                return null;
            }

            if (!EnsureDialogUiReferences())
            {
                return null;
            }

            ConfigureDialogPanel(_owningDialogManager != null ? _owningDialogManager : DialogManager.Instance);
            ApplyThemeInternal(resolvedTheme, ResolveAppliedThemeType(themeType, resolvedTheme));
            return _dialogPanel;
        }

        public bool SetTheme(DialogThemeType themeType)
        {
            if (!Initialize(null))
            {
                return false;
            }

            var resolvedTheme = ResolveTheme(themeType, logWarnings: true);
            if (resolvedTheme == null)
            {
                return false;
            }

            ApplyThemeInternal(resolvedTheme, ResolveAppliedThemeType(themeType, resolvedTheme));
            return true;
        }

        public bool ApplyTheme(DialogThemeType themeType)
        {
            return SetTheme(themeType);
        }

        public void ApplyTheme(DialogThemeSO theme)
        {
            if (!Initialize(null))
            {
                return;
            }

            if (theme == null)
            {
                Debug.LogWarning("[DialogUIManager] ApplyTheme called with null.");
                return;
            }

            ApplyThemeInternal(theme, ResolveThemeTypeForTheme(theme));
        }

        public void ApplyThemeByIndex(int index)
        {
            if (_themes == null || _themes.Count == 0)
            {
                Debug.LogWarning("[DialogUIManager] No themes are registered in the legacy theme list.");
                return;
            }

            index = Mathf.Clamp(index, 0, _themes.Count - 1);
            var theme = _themes[index];
            if (theme == null)
            {
                Debug.LogWarning($"[DialogUIManager] Theme at legacy index {index} is null.");
                return;
            }

            ApplyThemeInternal(theme, ResolveThemeTypeForTheme(theme));
        }

        public void NextTheme()
        {
            if (_themes == null || _themes.Count == 0)
            {
                return;
            }

            var nextIndex = _currentIndex >= 0 ? (_currentIndex + 1) % _themes.Count : 0;
            ApplyThemeByIndex(nextIndex);
        }

        public void PreviousTheme()
        {
            if (_themes == null || _themes.Count == 0)
            {
                return;
            }

            var previousIndex = _currentIndex >= 0
                ? (_currentIndex - 1 + _themes.Count) % _themes.Count
                : Mathf.Clamp(_startThemeIndex, 0, _themes.Count - 1);
            ApplyThemeByIndex(previousIndex);
        }

        public bool ApplyThemeByName(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                return false;
            }

            if (TryResolveThemeByName(themeName, out var theme, out var themeType))
            {
                ApplyThemeInternal(theme, themeType);
                return true;
            }

            Debug.LogWarning($"[DialogUIManager] No theme named '{themeName}' is configured.");
            return false;
        }

        public void HideDialogUI()
        {
            CloseSettingsPanel();

            if (_dialogPanel != null)
            {
                _dialogPanel.SetPanelVisible(false);
                _dialogPanel.SetChoicesVisible(false);
            }
        }

        public void ApplyRuntimeUiSettings(DialogSystem.Runtime.Settings.Panels.DialogueRuntimeUISettings settings)
        {
            _runtimeUiSettings = BuildEffectiveRuntimeUiSettings(settings);

            if (_runtimeUiSettings == null)
            {
                return;
            }

            ConfigureHistoryPanel();
            ConfigureHistoryBindings(_owningDialogManager != null ? _owningDialogManager : DialogManager.Instance);
            ConfigureSettingsPanel(_owningDialogManager != null ? _owningDialogManager : DialogManager.Instance);

            if (!DialogOptionalUiFeaturePolicy.IsEnabled(_runtimeUiSettings, DialogOptionalUiFeature.HistoryPanel))
            {
                _historyPanel?.Hide();
            }

            if (!DialogOptionalUiFeaturePolicy.IsEnabled(_runtimeUiSettings, DialogOptionalUiFeature.SettingsPanel))
            {
                CloseSettingsPanel();
            }
        }

        public bool Initialize(DialogManager dialogManager)
        {
            if (dialogManager != null)
            {
                _owningDialogManager = dialogManager;
            }

            var effectiveDialogManager = _owningDialogManager != null ? _owningDialogManager : DialogManager.Instance;

            ResolveEmbeddedReferences();

            if (!EnsureDialogUiReferences())
            {
                return false;
            }

            ConfigureDialogPanel(effectiveDialogManager);
            ConfigureHistoryPanel();
            ConfigureHistoryBindings(effectiveDialogManager);
            RefreshCurrentIndex();

            if (_currentTheme == null)
            {
                var savedThemeName = DialogRuntimeSettingsPersistence.LoadThemeName();
                if (!string.IsNullOrWhiteSpace(savedThemeName) &&
                    TryResolveThemeByName(savedThemeName, out var savedTheme, out var savedThemeType))
                {
                    ApplyThemeInternal(savedTheme, savedThemeType);
                    return true;
                }

                var defaultTheme = GetDefaultThemeOrNull();
                if (defaultTheme != null)
                {
                    ApplyThemeInternal(defaultTheme, DialogThemeType.Default);
                }
            }
            else
            {
                ApplyThemeInternal(_currentTheme, _currentThemeType);
            }

            return true;
        }

        public bool HasConfiguredDefaultTheme()
        {
            return GetDefaultThemeOrNull() != null;
        }

        public bool HasDialogCanvasPrefab()
        {
            return _dialogCanvasPrefab != null || GetEmbeddedCanvas() != null;
        }

        public bool HasResolvedMainDialogUiPrefab()
        {
            if (_dialogPanelPrefab != null)
            {
                return true;
            }

            if (_dialogPanel != null)
            {
                return true;
            }

            if (GetEmbeddedDialogUiController() != null)
            {
                return true;
            }

            return _dialogCanvasPrefab != null &&
                   _dialogCanvasPrefab.GetComponentInChildren<DialogUIController>(true) != null;
        }

        public bool HasChoicePrefabFallback()
        {
            if (_fallbackChoiceButtonPrefab != null)
            {
                return true;
            }

            if (_dialogPanel != null && _dialogPanel.choiceButtonPrefab != null)
            {
                return true;
            }

            if (_dialogPanelPrefab != null && _dialogPanelPrefab.choiceButtonPrefab != null)
            {
                return true;
            }

            var embeddedController = GetEmbeddedDialogUiController();
            if (embeddedController != null && embeddedController.choiceButtonPrefab != null)
            {
                return true;
            }

            var canvasController = _dialogCanvasPrefab != null
                ? _dialogCanvasPrefab.GetComponentInChildren<DialogUIController>(true)
                : null;
            return canvasController != null && canvasController.choiceButtonPrefab != null;
        }

        public Dictionary<DialogThemeType, int> GetDuplicateThemeMappingCounts()
        {
            var counts = new Dictionary<DialogThemeType, int>();
            if (_themeMappings == null)
            {
                return counts;
            }

            for (var i = 0; i < _themeMappings.Count; i++)
            {
                var mapping = _themeMappings[i];
                if (mapping == null)
                {
                    continue;
                }

                counts.TryGetValue(mapping.themeType, out var existingCount);
                counts[mapping.themeType] = existingCount + 1;
            }

            return counts;
        }

        private bool EnsureDialogUiReferences()
        {
            ResolveEmbeddedReferences();

            if (HasLiveDialogUi())
            {
                ConfigureHistoryPanel();
                return true;
            }

            if (_runtimeDialogCanvasInstance != null)
            {
                SyncRuntimeReferencesFromCanvas(_runtimeDialogCanvasInstance);
                if (HasLiveDialogUi())
                {
                    ConfigureHistoryPanel();
                    return true;
                }
            }

            if (_dialogCanvasPrefab == null)
            {
                Debug.LogError("[DialogUIManager] Cannot create dialog UI because no canvas prefab is assigned and no live scene dialog UI was found.");
                return false;
            }

            _runtimeDialogCanvasInstance = Instantiate(_dialogCanvasPrefab, transform);
            _runtimeDialogCanvasInstance.name = "Runtime_DialogUICanvas";
            SyncRuntimeReferencesFromCanvas(_runtimeDialogCanvasInstance);

            if (_dialogPanel == null && _dialogPanelPrefab != null)
            {
                _dialogPanel = Instantiate(_dialogPanelPrefab, _runtimeDialogCanvasInstance.transform);
                _dialogPanel.name = _dialogPanelPrefab.name;
            }

            if (!HasLiveDialogUi())
            {
                Debug.LogError("[DialogUIManager] Dialog UI canvas was created, but no DialogUIController could be resolved. Assign a canvas prefab containing one or provide a main dialog UI prefab.");
                return false;
            }

            ConfigureHistoryPanel();
            return true;
        }

        private bool HasLiveDialogUi()
        {
            return _dialogPanel != null && _dialogPanel.gameObject != null;
        }

        private void SyncRuntimeReferencesFromCanvas(GameObject canvasInstance)
        {
            if (canvasInstance == null)
            {
                return;
            }

            if (_dialogPanel == null)
            {
                _dialogPanel = canvasInstance.GetComponentInChildren<DialogUIController>(true);
            }

            if (_historyPanel == null)
            {
                _historyPanel = canvasInstance.GetComponentInChildren<DialogueHistoryView>(true);
            }

            if (_dialogueHistory == null)
            {
                _dialogueHistory = canvasInstance.GetComponentInChildren<DialogueHistory>(true);
            }
        }

        private void ConfigureDialogPanel(DialogManager dialogManager)
        {
            if (_dialogPanel == null)
            {
                return;
            }

            _dialogPanel.SetChoiceButtonPrefabFallback(_fallbackChoiceButtonPrefab);
            _dialogPanel.SetSkipButtonListener(dialogManager != null ? dialogManager.SkipAll : null);
            _dialogPanel.SetSkipLineButtonListener(dialogManager != null ? dialogManager.SkipLine : null);
            _dialogPanel.SetAutoPlayButtonListener(dialogManager != null ? ToggleAutoPlayFromUi : null);
            var runtimeUiSettings = ResolveRuntimeUiSettings(dialogManager);
            _dialogPanel.SetLanguageButtonListener(
                DialogOptionalUiFeaturePolicy.IsEnabled(runtimeUiSettings, DialogOptionalUiFeature.SettingsPanel)
                    ? ToggleSettingsFromUi
                    : null);
            ConfigureSettingsPanel(dialogManager);
        }

        private void ConfigureHistoryPanel()
        {
            if (_historyPanel == null && _dialogPanel != null)
            {
                _historyPanel = _dialogPanel.GetComponentInChildren<DialogueHistoryView>(true);
            }

            if (_historyPanel == null && _runtimeDialogCanvasInstance != null)
            {
                _historyPanel = _runtimeDialogCanvasInstance.GetComponentInChildren<DialogueHistoryView>(true);
            }

            if (_historyPanel == null)
            {
                _historyPanel = GetComponentInChildren<DialogueHistoryView>(true);
            }

            if (_dialogueHistory == null && _dialogPanel != null)
            {
                _dialogueHistory = _dialogPanel.GetComponentInChildren<DialogueHistory>(true);
            }

            if (_dialogueHistory == null && _runtimeDialogCanvasInstance != null)
            {
                _dialogueHistory = _runtimeDialogCanvasInstance.GetComponentInChildren<DialogueHistory>(true);
            }

            if (_dialogueHistory == null)
            {
                _dialogueHistory = GetComponentInChildren<DialogueHistory>(true);
            }

            if (_dialogueHistory == null && _historyPanel != null)
            {
                _dialogueHistory = gameObject.AddComponent<DialogueHistory>();
            }
        }

        private void ConfigureHistoryBindings(DialogManager dialogManager)
        {
            if (_dialogPanel == null)
            {
                return;
            }

            ConfigureHistoryPanel();

            var runtimeUiSettings = ResolveRuntimeUiSettings(dialogManager);
            var historyButtonEnabled = DialogOptionalUiFeaturePolicy.IsEnabled(runtimeUiSettings, DialogOptionalUiFeature.HistoryButton);
            var historyPanelEnabled = DialogOptionalUiFeaturePolicy.IsEnabled(runtimeUiSettings, DialogOptionalUiFeature.HistoryPanel);
            if (!historyButtonEnabled || !historyPanelEnabled)
            {
                _dialogPanel.SetHistoryBtnListener(null);
                if (!historyPanelEnabled)
                {
                    _historyPanel?.Hide();
                }

                return;
            }

            if (_dialogueHistory == null)
            {
                if (!_didWarnMissingHistory && _dialogPanel.historyPanelButton != null)
                {
                    Debug.LogWarning("[DialogUIManager] History button exists, but no DialogueHistory component was found under DialogUIManager. History toggle will remain unbound.", this);
                    _didWarnMissingHistory = true;
                }

                _dialogPanel.SetHistoryBtnListener(null);
                return;
            }

            _dialogueHistory.Initialize(dialogManager, _historyPanel);
            _dialogPanel.SetHistoryBtnListener(ToggleHistoryFromUi);
        }

        private DialogSystem.Runtime.Settings.Panels.DialogueRuntimeUISettings ResolveRuntimeUiSettings(DialogManager dialogManager)
        {
            if (_runtimeUiSettings != null)
            {
                return BuildEffectiveRuntimeUiSettings(_runtimeUiSettings);
            }

            if (dialogManager != null && dialogManager.RuntimeUiSettings != null)
            {
                return BuildEffectiveRuntimeUiSettings(dialogManager.RuntimeUiSettings);
            }

            if (_owningDialogManager != null && _owningDialogManager.RuntimeUiSettings != null)
            {
                return BuildEffectiveRuntimeUiSettings(_owningDialogManager.RuntimeUiSettings);
            }

            return BuildEffectiveRuntimeUiSettings(DialogSettingsRuntime.UI);
        }

        private void ToggleAutoPlayFromUi()
        {
            ResolveOverlayDialogManager()?.ToggleAutoPlay();
        }

        private void ToggleSettingsFromUi()
        {
            if (_isSettingsPanelOpen)
            {
                CloseSettingsPanel();
                return;
            }

            OpenSettingsPanel();
        }

        private void OpenSettingsPanel()
        {
            if (!DialogOptionalUiFeaturePolicy.IsEnabled(ResolveRuntimeUiSettings(ResolveOverlayDialogManager()), DialogOptionalUiFeature.SettingsPanel))
            {
                return;
            }

            var settingsPanel = ResolveSettingsPanel();
            if (settingsPanel == null)
            {
                if (!_didWarnMissingSettingsPanel)
                {
                    Debug.LogWarning("[DialogUIManager] Settings button was clicked, but no settings panel GameObject is assigned or could be resolved.", this);
                    _didWarnMissingSettingsPanel = true;
                }

                return;
            }

            _didWarnMissingSettingsPanel = false;
            EnsureSettingsOverlayConfigured(settingsPanel);
            var dialogManager = ResolveOverlayDialogManager();
            var runtimeSettingsPanel = settingsPanel.GetComponentInChildren<DialogRuntimeSettingsPanel>(true);
            runtimeSettingsPanel?.Initialize(dialogManager, this);
            runtimeSettingsPanel?.RefreshFromRuntime();
            SetSettingsPanelVisible(true);
            dialogManager?.PauseForOverlay();
            _isSettingsPanelOpen = true;
        }

        private void CloseSettingsPanel()
        {
            var settingsPanel = ResolveSettingsPanel();
            if (settingsPanel != null && (_isSettingsPanelOpen || _runtimeSettingsCanvasGroup != null))
            {
                EnsureSettingsOverlayConfigured(settingsPanel);
                SetSettingsPanelVisible(false);
            }

            if (!_isSettingsPanelOpen)
            {
                return;
            }

            ResolveOverlayDialogManager()?.ResumeAfterOverlay();
            _isSettingsPanelOpen = false;
        }

        private void ConfigureSettingsPanel(DialogManager dialogManager)
        {
            if (!DialogOptionalUiFeaturePolicy.IsEnabled(ResolveRuntimeUiSettings(dialogManager), DialogOptionalUiFeature.SettingsPanel))
            {
                CloseSettingsPanel();
                return;
            }

            var settingsPanel = ResolveSettingsPanel();
            if (settingsPanel == null)
            {
                if (_isSettingsPanelOpen)
                {
                    ResolveOverlayDialogManager()?.ResumeAfterOverlay();
                }

                _runtimeSettingsCloseButton = null;
                _runtimeSettingsCanvas = null;
                _runtimeSettingsCanvasGroup = null;
                _isSettingsPanelOpen = false;
                return;
            }

            var wasOpen = _isSettingsPanelOpen;
            EnsureSettingsOverlayConfigured(settingsPanel);
            SetSettingsPanelVisible(wasOpen);
            _isSettingsPanelOpen = wasOpen;
            _runtimeSettingsCloseButton = ResolveSettingsCloseButton(settingsPanel);
            var effectiveDialogManager = dialogManager != null ? dialogManager : ResolveOverlayDialogManager();
            var runtimeSettingsPanel = settingsPanel.GetComponentInChildren<DialogRuntimeSettingsPanel>(true);
            runtimeSettingsPanel?.Initialize(effectiveDialogManager, this);
            if (_runtimeSettingsCloseButton != null)
            {
                _runtimeSettingsCloseButton.onClick.RemoveListener(CloseSettingsPanel);
                _runtimeSettingsCloseButton.onClick.AddListener(CloseSettingsPanel);
            }
        }

        private DialogManager ResolveOverlayDialogManager()
        {
            if (_owningDialogManager != null)
            {
                return _owningDialogManager;
            }

            _owningDialogManager = DialogManager.Instance != null
                ? DialogManager.Instance
                : DialogRuntimeUnityCompatibility.FindFirst<DialogManager>(includeInactive: true);

            return _owningDialogManager;
        }

        private void PrimeSettingsPanelReferences(GameObject settingsPanel)
        {
            if (settingsPanel == null)
            {
                return;
            }

            _runtimeSettingsCanvas = settingsPanel.GetComponent<Canvas>();
            _runtimeSettingsCanvasGroup = settingsPanel.GetComponent<CanvasGroup>();
        }

        private DialogSystem.Runtime.Settings.Panels.DialogueRuntimeUISettings BuildEffectiveRuntimeUiSettings(DialogSystem.Runtime.Settings.Panels.DialogueRuntimeUISettings source)
        {
            if (!_overrideRuntimeUiVisibility)
            {
                return source;
            }

            if (_runtimeUiSettingsOverrideInstance == null)
            {
                _runtimeUiSettingsOverrideInstance = ScriptableObject.CreateInstance<DialogSystem.Runtime.Settings.Panels.DialogueRuntimeUISettings>();
                _runtimeUiSettingsOverrideInstance.hideFlags = HideFlags.DontSave;
            }

            var target = _runtimeUiSettingsOverrideInstance;
            if (source != null)
            {
                target.settingsTheme = source.settingsTheme;
                target.showBackgroundPanel = source.showBackgroundPanel;
                target.showSpeakerName = source.showSpeakerName;
                target.showPortrait = source.showPortrait;
                target.showSkipButton = source.showSkipButton;
                target.showAutoButton = source.showAutoButton;
                target.showHistoryButton = source.showHistoryButton;
                target.showSettingsButton = source.showSettingsButton;
                target.showLanguageButton = source.showLanguageButton;
                target.showContinueIndicator = source.showContinueIndicator;
                target.showAutoSkipIcon = source.showAutoSkipIcon;
                target.showChoicePanel = source.showChoicePanel;
                target.showHistoryPanel = source.showHistoryPanel;
                target.showSettingsPanel = source.showSettingsPanel;
                target.showSettingsLanguageDropdown = source.showSettingsLanguageDropdown;
                target.showSettingsThemeDropdown = source.showSettingsThemeDropdown;
                target.showSettingsTextSpeed = source.showSettingsTextSpeed;
                target.showSettingsAutoAdvance = source.showSettingsAutoAdvance;
                target.showSettingsVoiceVolume = source.showSettingsVoiceVolume;
                target.showSettingsSfxVolume = source.showSettingsSfxVolume;
            }
            else
            {
                target.settingsTheme = null;
                target.showBackgroundPanel = true;
                target.showSpeakerName = true;
                target.showPortrait = true;
                target.showSkipButton = true;
                target.showAutoButton = true;
                target.showHistoryButton = true;
                target.showSettingsButton = true;
                target.showLanguageButton = true;
                target.showContinueIndicator = true;
                target.showAutoSkipIcon = true;
                target.showChoicePanel = true;
                target.showHistoryPanel = true;
                target.showSettingsPanel = true;
                target.showSettingsLanguageDropdown = true;
                target.showSettingsThemeDropdown = true;
                target.showSettingsTextSpeed = true;
                target.showSettingsAutoAdvance = true;
                target.showSettingsVoiceVolume = true;
                target.showSettingsSfxVolume = true;
            }

            target.showBackgroundPanel = _showBackgroundPanel;
            target.showSpeakerName = _showSpeakerName;
            target.showPortrait = _showPortrait;
            target.showSkipButton = _showSkipButton;
            target.showAutoButton = _showAutoButton;
            target.showHistoryButton = _showHistoryButton;
            target.showSettingsButton = _showSettingsButton;
            target.showLanguageButton = _showLanguageButton;
            target.showContinueIndicator = _showContinueIndicator;
            target.showAutoSkipIcon = _showAutoSkipIcon;
            target.showChoicePanel = _showChoicePanel;
            target.showHistoryPanel = _showHistoryPanel;
            target.showSettingsPanel = _showSettingsPanel;
            target.showSettingsLanguageDropdown = _showSettingsLanguageDropdown;
            target.showSettingsThemeDropdown = _showSettingsThemeDropdown;
            target.showSettingsTextSpeed = _showSettingsTextSpeed;
            target.showSettingsAutoAdvance = _showSettingsAutoAdvance;
            target.showSettingsVoiceVolume = _showSettingsVoiceVolume;
            target.showSettingsSfxVolume = _showSettingsSfxVolume;

            return target;
        }

        private void SwitchToRandomLocaleFromUi()
        {
            var languageController = DialogRuntimeUnityCompatibility.FindFirst<DialogRuntimeLanguageController>(includeInactive: true);
            if (languageController != null)
            {
                if (!languageController.SwitchToRandomOtherLocale())
                {
                    Debug.LogWarning("[DialogUIManager] Could not switch locale because fewer than two runtime locales are configured.", this);
                }

                return;
            }

            if (DialogSystem.Runtime.Localization.DialogLocalizationRuntime.Instance.Settings == null)
            {
                DialogSystem.Runtime.Localization.DialogLocalizationRuntime.Instance.Initialize();
            }

            if (!DialogSystem.Runtime.Localization.DialogLocalizationRuntime.Instance.TrySetRandomOtherLocale())
            {
                Debug.LogWarning("[DialogUIManager] Could not switch locale because fewer than two runtime locales are configured.", this);
            }
        }

        private void ToggleHistoryFromUi()
        {
            ConfigureHistoryPanel();

            if (_dialogueHistory == null)
            {
                Debug.LogWarning("[DialogUIManager] History button was clicked, but no DialogueHistory runtime controller could be resolved.", this);
                return;
            }

            if (!_dialogueHistory.HasResolvedView)
            {
                _dialogueHistory.Initialize(_owningDialogManager != null ? _owningDialogManager : DialogManager.Instance, _historyPanel);
            }

            _dialogueHistory.Toggle();
        }

        private void ResolveEmbeddedReferences()
        {
            if (!IsRuntimeInstance(_dialogPanel))
            {
                _dialogPanel = null;
            }

            if (!IsRuntimeInstance(_historyPanel))
            {
                _historyPanel = null;
            }

            if (!IsRuntimeInstance(_dialogueHistory))
            {
                _dialogueHistory = null;
            }

            if (!IsRuntimeInstance(_runtimeDialogCanvasInstance))
            {
                _runtimeDialogCanvasInstance = null;
            }

            if (_dialogPanel == null)
            {
                _dialogPanel = GetEmbeddedDialogUiController();
            }

            if (_historyPanel == null)
            {
                _historyPanel = GetComponentInChildren<DialogueHistoryView>(true);
            }

            if (_dialogueHistory == null)
            {
                _dialogueHistory = GetComponentInChildren<DialogueHistory>(true);
            }

            if (!IsRuntimeInstance(_settingsPanel))
            {
                _settingsPanel = null;
            }

            if (_settingsPanel == null)
            {
                _settingsPanel = ResolveSettingsPanelByName();
            }

            if (_runtimeDialogCanvasInstance == null)
            {
                var embeddedCanvas = GetEmbeddedCanvas();
                if (embeddedCanvas != null)
                {
                    _runtimeDialogCanvasInstance = embeddedCanvas.gameObject;
                }
            }
        }

        private DialogThemeSO ResolveTheme(DialogThemeType requestedThemeType, bool logWarnings)
        {
            if (requestedThemeType == DialogThemeType.Default)
            {
                var defaultTheme = GetDefaultThemeOrNull();
                if (defaultTheme == null)
                {
                    Debug.LogError("[DialogUIManager] No default theme is configured. Dialog UI cannot be prepared.");
                }

                return defaultTheme;
            }

            if (_themeMappings != null)
            {
                for (var i = 0; i < _themeMappings.Count; i++)
                {
                    var mapping = _themeMappings[i];
                    if (mapping != null && mapping.themeType == requestedThemeType && mapping.theme != null)
                    {
                        return mapping.theme;
                    }
                }
            }

            var fallbackTheme = GetDefaultThemeOrNull();
            if (fallbackTheme == null)
            {
                Debug.LogError($"[DialogUIManager] Requested theme '{requestedThemeType}' is not configured and no default theme is available.");
                return null;
            }

            if (logWarnings)
            {
                Debug.LogWarning($"[DialogUIManager] Requested theme '{requestedThemeType}' is not configured. Falling back to the default theme '{fallbackTheme.themeName}'.");
            }

            return fallbackTheme;
        }

        private DialogThemeSO GetDefaultThemeOrNull()
        {
            if (_defaultTheme != null)
            {
                return _defaultTheme;
            }

            if (_themes != null && _themes.Count > 0)
            {
                var clampedIndex = Mathf.Clamp(_startThemeIndex, 0, _themes.Count - 1);
                return _themes[clampedIndex];
            }

            return null;
        }

        private DialogThemeType ResolveAppliedThemeType(DialogThemeType requestedThemeType, DialogThemeSO resolvedTheme)
        {
            if (resolvedTheme == null)
            {
                return DialogThemeType.Default;
            }

            if (requestedThemeType == DialogThemeType.Default)
            {
                return DialogThemeType.Default;
            }

            return ResolveThemeTypeForTheme(resolvedTheme);
        }

        private DialogThemeType ResolveThemeTypeForTheme(DialogThemeSO theme)
        {
            if (theme == null)
            {
                return DialogThemeType.Default;
            }

            if (_defaultTheme == theme)
            {
                return DialogThemeType.Default;
            }

            if (_themeMappings != null)
            {
                for (var i = 0; i < _themeMappings.Count; i++)
                {
                    var mapping = _themeMappings[i];
                    if (mapping?.theme == theme)
                    {
                        return mapping.themeType;
                    }
                }
            }

            return DialogThemeType.Default;
        }

        private bool TryResolveThemeByName(string themeName, out DialogThemeSO theme, out DialogThemeType themeType)
        {
            theme = null;
            themeType = DialogThemeType.Default;

            if (string.IsNullOrWhiteSpace(themeName))
            {
                return false;
            }

            if (_defaultTheme != null && string.Equals(_defaultTheme.themeName, themeName, StringComparison.OrdinalIgnoreCase))
            {
                theme = _defaultTheme;
                themeType = DialogThemeType.Default;
                return true;
            }

            if (_themes != null)
            {
                for (var i = 0; i < _themes.Count; i++)
                {
                    var candidate = _themes[i];
                    if (candidate != null && string.Equals(candidate.themeName, themeName, StringComparison.OrdinalIgnoreCase))
                    {
                        theme = candidate;
                        themeType = ResolveThemeTypeForTheme(candidate);
                        return true;
                    }
                }
            }

            if (_themeMappings != null)
            {
                for (var i = 0; i < _themeMappings.Count; i++)
                {
                    var mapping = _themeMappings[i];
                    if (mapping?.theme != null &&
                        string.Equals(mapping.theme.themeName, themeName, StringComparison.OrdinalIgnoreCase))
                    {
                        theme = mapping.theme;
                        themeType = mapping.themeType;
                        return true;
                    }
                }
            }

            return false;
        }

        private void ApplyThemeInternal(DialogThemeSO theme, DialogThemeType appliedThemeType)
        {
            if (theme == null)
            {
                return;
            }

            _currentTheme = theme;
            _currentThemeType = appliedThemeType;
            ActiveTheme = theme;
            RefreshCurrentIndex();

            ConfigureDialogPanel(_owningDialogManager != null ? _owningDialogManager : DialogManager.Instance);
            _dialogPanel?.ApplyTheme(theme);
            _historyPanel?.ApplyTheme(theme);
            OnThemeChanged?.Invoke(theme);
        }

        private void RefreshCurrentIndex()
        {
            _currentIndex = _themes != null && _currentTheme != null
                ? _themes.IndexOf(_currentTheme)
                : -1;
        }

        private DialogUIController GetEmbeddedDialogUiController()
        {
            return GetComponentInChildren<DialogUIController>(true);
        }

        private Canvas GetEmbeddedCanvas()
        {
            return GetComponentInChildren<Canvas>(true);
        }

        private GameObject ResolveSettingsPanel()
        {
            if (_settingsPanel != null)
            {
                return _settingsPanel;
            }

            _settingsPanel = ResolveSettingsPanelByName();
            return _settingsPanel;
        }

        private GameObject ResolveSettingsPanelByName()
        {
            Transform[] searchRoots =
            {
                _runtimeDialogCanvasInstance != null ? _runtimeDialogCanvasInstance.transform : null,
                _dialogPanel != null ? _dialogPanel.transform.root : null,
                transform
            };

            for (var i = 0; i < searchRoots.Length; i++)
            {
                var root = searchRoots[i];
                if (root == null)
                {
                    continue;
                }

                var namedPanel = FindChildByName(root, "SettingsPanel");
                if (namedPanel != null)
                {
                    return namedPanel.gameObject;
                }
            }

            return null;
        }

        private static Button ResolveSettingsCloseButton(GameObject settingsPanel)
        {
            if (settingsPanel == null)
            {
                return null;
            }

            var namedClose = FindChildByName(settingsPanel.transform, "CloseButton");
            if (namedClose == null)
            {
                return null;
            }

            return namedClose.GetComponent<Button>() ?? namedClose.GetComponentInChildren<Button>(true);
        }

        private void EnsureSettingsOverlayConfigured(GameObject settingsPanel)
        {
            if (settingsPanel == null)
            {
                return;
            }

            if (Application.isPlaying &&
                _runtimeDialogCanvasInstance != null &&
                settingsPanel.transform.IsChildOf(_runtimeDialogCanvasInstance.transform))
            {
                settingsPanel.transform.SetParent(transform, true);
            }

            if (!settingsPanel.activeSelf)
            {
                settingsPanel.SetActive(true);
            }

            _runtimeSettingsCanvas = settingsPanel.GetComponent<Canvas>();
            if (_runtimeSettingsCanvas == null)
            {
                _runtimeSettingsCanvas = settingsPanel.AddComponent<Canvas>();
            }

            _runtimeSettingsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _runtimeSettingsCanvas.overrideSorting = true;
            _runtimeSettingsCanvas.sortingOrder = SettingsPanelSortingOrder;

            if (settingsPanel.GetComponent<GraphicRaycaster>() == null)
            {
                settingsPanel.AddComponent<GraphicRaycaster>();
            }

            _runtimeSettingsCanvasGroup = settingsPanel.GetComponent<CanvasGroup>();
            if (_runtimeSettingsCanvasGroup == null)
            {
                _runtimeSettingsCanvasGroup = settingsPanel.AddComponent<CanvasGroup>();
            }
        }

        private void SetSettingsPanelVisible(bool visible)
        {
            if (_runtimeSettingsCanvasGroup == null)
            {
                return;
            }

            _runtimeSettingsCanvasGroup.alpha = visible ? 1f : 0f;
            _runtimeSettingsCanvasGroup.interactable = visible;
            _runtimeSettingsCanvasGroup.blocksRaycasts = visible;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child != null && string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsRuntimeInstance(Component component)
        {
            return component != null &&
                   component.gameObject != null &&
                   component.gameObject.scene.IsValid();
        }

        private static bool IsRuntimeInstance(GameObject gameObject)
        {
            return gameObject != null &&
                   gameObject.scene.IsValid();
        }
    }
}