using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Settings;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.UI.Theming;

namespace DialogSystem.Runtime.UI
{
    /// <summary>
    /// Clickable, highlightable row used for dialog choices.
    /// Subscribes to <see cref="DialogUIManager.OnThemeChanged"/> and swaps
    /// its background sprite and text color without needing a direct manager reference.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ChoiceButtonView : MonoBehaviour, IPointerEnterHandler
    {
        // ─── Inspector ────────────────────────────────────────────────────────────

        [SerializeField] private bool _doDebug = false;

        [Header("UI Refs (auto-detected if null)")]
        [SerializeField] private Button          _button;
        [SerializeField] private TextMeshProUGUI _choiceText;
        [SerializeField] private Outline         _outline;

        [Header("Hotkey")]
        [SerializeField] private GameObject _hotkeyHolder;
        [SerializeField] private TextMeshProUGUI _hotkeyText;

        [Header("Theme")]
        [Tooltip("Background Image of this button — sprite is swapped by the active theme.")]
        [SerializeField] private Image _background;

        [Tooltip("Small icon displayed on the left of the choice row (e.g. chat-bubble). Sprite is set by the active theme.")]
        [SerializeField] private Image _messageIcon;

        [Tooltip("Background Image of the hotkey hint. Optional, but if set, its color will be swapped by the active theme.")]
        [SerializeField] private Image _hotkeyBackgroundImage;

        // ─── Runtime State ────────────────────────────────────────────────────────

        private DialogManager       _mgr;
        private int                 _index;
        private DialogChoiceSettings _settings;
        private Vector3             _baseScale;
        private bool                _selected;
        private float               _pulseT;
        private string              _currentHotkey = string.Empty;
        private Action              _onClick;

        // Shared across all instances to avoid per-button scene lookups.
        private static DialogThemeSO _activeTheme;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            _doDebug = DialogSettingsRuntime.DebugLogsEnabled && (_doDebug || DialogSettingsRuntime.DoDebug());

            if (!_button)     _button     = GetComponentInChildren<Button>();
            if (!_outline)    _outline    = GetComponentInChildren<Outline>();
            if (!_background) _background = GetComponent<Image>();
            if (!_choiceText)      Debug.LogError($"[ChoiceButtonView] '{name}' has no Label.");
            if (!_hotkeyText) Debug.LogWarning($"[ChoiceButtonView] '{name}' has no HotkeyLabel — creating one.");

            if (_choiceText)      _choiceText.raycastTarget      = false;
            if (_hotkeyText) _hotkeyText.raycastTarget = false;

            _baseScale = transform.localScale;
            SyncActiveTheme();
            ApplySelected(false, string.Empty);
        }

        private void OnEnable()
        {
            DialogUIManager.OnThemeChanged += HandleThemeChanged;
            SyncActiveTheme();
            RefreshThemeVisuals();
        }

        private void OnDisable() => DialogUIManager.OnThemeChanged -= HandleThemeChanged;

        private void HandleThemeChanged(DialogThemeSO theme)
        {
            _activeTheme = theme;
            RefreshThemeVisuals();
        }

        // ─── Public API ───────────────────────────────────────────────────────────

        public void Init(DialogManager manager, int rowIndex, DialogChoiceSettings choiceSettings)
        {
            _mgr      = manager;
            _index    = rowIndex;
            _settings = choiceSettings;

            _hotkeyHolder?.SetActive(_settings != null && _settings.showKeyHints);

            if (_outline && _settings != null)
            {
                _outline.effectColor    = _settings.selectedOutlineColor;
                float t = Mathf.Max(0.5f, _settings.outlineThickness);
                _outline.effectDistance = new Vector2(t, -t);
            }

            // Apply theme immediately if one is already active.
            SyncActiveTheme();
            if (_activeTheme != null) RefreshThemeVisuals();
        }

        public void SetContent(string text, string subText, bool interactable, Action onClick)
        {
            if (_choiceText)    _choiceText.text    = text    ?? string.Empty;

            _onClick = onClick;

            if (_button)
            {
                _button.interactable = interactable;
                _button.onClick.RemoveListener(OnUIButtonClicked);
                if (onClick != null) _button.onClick.AddListener(OnUIButtonClicked);

                if (_doDebug)
                    Debug.Log($"[ChoiceButtonView] '{name}' wired. Interactable={interactable}, Handler={(onClick != null)}");
            }
            else if (_doDebug)
            {
                Debug.LogWarning($"[ChoiceButtonView] '{name}' has no Button component.");
            }
        }

        public void SetHotkey(string hotkey)
        {
            _currentHotkey = hotkey ?? string.Empty;
            if (_hotkeyText) _hotkeyText.text = string.Empty;
        }

        public void ApplySelected(bool isSelected, string hintText)
        {
            _selected = isSelected;

            if (_outline) _outline.enabled  = isSelected;
            if (_choiceText)   _choiceText.fontStyle   = isSelected ? FontStyles.Bold : FontStyles.Normal;

            var shouldShowHotkey = false;
            if (_hotkeyText)
            {
                if (isSelected)
                {
                    var raw = string.IsNullOrEmpty(hintText) ? _currentHotkey : hintText;
                    _hotkeyText.text = string.IsNullOrEmpty(raw) ? string.Empty : $"{raw}";
                    shouldShowHotkey = !string.IsNullOrEmpty(_hotkeyText.text);
                }
                else
                {
                    _hotkeyText.text = string.Empty;
                }
            }

            if (_hotkeyHolder != null)
            {
                var hintsEnabled = _settings != null && _settings.showKeyHints;
                _hotkeyHolder.SetActive(hintsEnabled && shouldShowHotkey);
            }

            if (!isSelected)
            {
                _pulseT = 0f;
                transform.localScale = _baseScale;
            }

            RefreshThemeVisuals();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_mgr == null) return;
            if (_settings != null && _settings.mouseHoverMovesSelection)
            {
                if (_doDebug) Debug.Log($"[ChoiceButtonView] Hover → select index {_index}");
                _mgr.SelectChoiceIndex(_index);
            }
        }

        // ─── Internals ────────────────────────────────────────────────────────────

        private void OnUIButtonClicked()
        {
            if (_doDebug) Debug.Log($"[ChoiceButtonView] Click '{name}'. HandlerNull={_onClick == null}");
            _onClick?.Invoke();
        }

        private void RefreshThemeVisuals()
        {
            if (_activeTheme == null) return;

            if (_background != null)
                _background.sprite = _selected
                    ? _activeTheme.choiceButtonActive
                    : _activeTheme.choiceButtonNormal;

            if (_choiceText != null)
                _choiceText.color = _selected
                    ? _activeTheme.choiceActiveColor
                    : _activeTheme.choiceNormalColor;

            if (_hotkeyText != null)
                _hotkeyText.color = _selected
                    ? _activeTheme.choiceActiveColor
                    : _activeTheme.choiceNormalColor;

            // Message icon — same sprite regardless of selection state.
            if (_messageIcon != null && _activeTheme.choiceMessageIcon != null)
            {
                _messageIcon.sprite  = _activeTheme.choiceMessageIcon;
                _messageIcon.enabled = true;
            }

            // Hotkey background — same sprite regardless of selection state.
            if (_hotkeyBackgroundImage != null && _activeTheme.hotkeyBackgroundSprite != null)
            {
                _hotkeyBackgroundImage.sprite = _activeTheme.hotkeyBackgroundSprite;
                _hotkeyBackgroundImage.enabled = true;
            }
        }

        private void SyncActiveTheme()
        {
            if (DialogUIManager.ActiveTheme != null)
                _activeTheme = DialogUIManager.ActiveTheme;
        }

        private void Update()
        {
            if (!_selected || _settings == null || !_settings.animateSelected) return;
            _pulseT += Time.deltaTime * Mathf.Max(0.01f, _settings.animatePulseSpeed);
            float s = Mathf.Lerp(1f, _settings.animatePulseScale, 0.5f + 0.5f * Mathf.Sin(_pulseT * Mathf.PI * 2f));
            transform.localScale = _baseScale * s;
        }
    }
}
