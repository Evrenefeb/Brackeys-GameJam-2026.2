using System;
using System.Collections.Generic;
using DialogSystem.Runtime.Localization;
using TMPro;
using UnityEngine;

namespace DialogSystem.Runtime.UI
{
    /// <summary>
    /// Small runtime controller for locale switching.
    /// Attach it next to a TMP_Dropdown to populate options and apply changes.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogRuntimeLanguageController : MonoBehaviour
    {
        [SerializeField] private DialogLocalizationRuntimeSettings _settings;
        [SerializeField] private TMP_Dropdown _dropdown;
        [SerializeField] private bool _detachFromLegacyOverlay = false;

        private readonly List<string> _localeCodes = new();
        private bool _isRefreshing;

        public TMP_Dropdown Dropdown => _dropdown;
        public IReadOnlyList<string> LocaleCodes => _localeCodes;

        private void Awake()
        {
            DetachFromLegacyOverlayIfNeeded();
        }

        private void OnEnable()
        {
            DetachFromLegacyOverlayIfNeeded();
            InitializeRuntime();
            BindDropdown(true);
            DialogLocalizationRuntime.Instance.OnLocaleChanged += RefreshSelection;
            RebuildDropdown();
        }

        private void OnDisable()
        {
            BindDropdown(false);
            DialogLocalizationRuntime.Instance.OnLocaleChanged -= RefreshSelection;
        }

        public void InitializeRuntime()
        {
            if (_settings != null)
            {
                DialogLocalizationRuntime.Instance.Initialize(_settings);
            }
            else if (DialogLocalizationRuntime.Instance.Settings == null)
            {
                DialogLocalizationRuntime.Instance.Initialize();
            }
        }

        public void SetDropdown(TMP_Dropdown dropdown)
        {
            if (_dropdown == dropdown)
            {
                return;
            }

            BindDropdown(false);
            _dropdown = dropdown;
            BindDropdown(isActiveAndEnabled);

            if (isActiveAndEnabled)
            {
                RebuildDropdown();
            }
        }

        public void RebuildDropdown()
        {
            if (_dropdown == null)
            {
                return;
            }

            _isRefreshing = true;
            _localeCodes.Clear();
            _dropdown.ClearOptions();

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

            _dropdown.AddOptions(labels);
            _dropdown.interactable = _localeCodes.Count > 1;
            _isRefreshing = false;
            RefreshSelection();
        }

        public bool SetLocale(string localeCode)
        {
            InitializeRuntime();
            return DialogLocalizationRuntime.Instance.SetActiveLocale(localeCode);
        }

        public bool SwitchToRandomOtherLocale()
        {
            InitializeRuntime();
            return DialogLocalizationRuntime.Instance.TrySetRandomOtherLocale();
        }

        private void OnDropdownValueChanged(int index)
        {
            if (_isRefreshing || index < 0 || index >= _localeCodes.Count)
            {
                return;
            }

            DialogLocalizationRuntime.Instance.SetActiveLocale(_localeCodes[index]);
        }

        private void RefreshSelection()
        {
            if (_dropdown == null)
            {
                return;
            }

            _isRefreshing = true;

            if (_localeCodes.Count == 0)
            {
                _dropdown.SetValueWithoutNotify(0);
                _dropdown.RefreshShownValue();
                _isRefreshing = false;
                return;
            }

            var activeLocaleCode = DialogLocalizationRuntime.Instance.ActiveLocaleCode;
            var index = _localeCodes.FindIndex(code => string.Equals(code, activeLocaleCode, StringComparison.OrdinalIgnoreCase));
            _dropdown.SetValueWithoutNotify(index < 0 ? 0 : index);
            _dropdown.RefreshShownValue();

            _isRefreshing = false;
        }

        private void BindDropdown(bool subscribe)
        {
            if (_dropdown == null)
            {
                return;
            }

            if (subscribe)
            {
                _dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
                _dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
                return;
            }

            _dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        }

        private void DetachFromLegacyOverlayIfNeeded()
        {
            if (!_detachFromLegacyOverlay)
            {
                return;
            }

            var rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
            {
                return;
            }

            var legacyOverlay = FindLegacyOverlayRoot();
            if (legacyOverlay == null)
            {
                return;
            }

            var rectTransform = transform as RectTransform;
            var canvasTransform = rootCanvas.transform as RectTransform;
            if (rectTransform == null || canvasTransform == null || rectTransform.parent == canvasTransform)
            {
                return;
            }

            rectTransform.SetParent(canvasTransform, true);
            rectTransform.SetAsLastSibling();

            if (legacyOverlay.gameObject.activeSelf)
            {
                legacyOverlay.gameObject.SetActive(false);
            }
        }

        private RectTransform FindLegacyOverlayRoot()
        {
            var current = transform.parent as RectTransform;
            while (current != null)
            {
                var currentName = current.name;
                if (string.Equals(currentName, "DisabledOverlayPanel", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentName, "DialogueSettingsPanel", StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }

                current = current.parent as RectTransform;
            }

            return null;
        }
    }
}
