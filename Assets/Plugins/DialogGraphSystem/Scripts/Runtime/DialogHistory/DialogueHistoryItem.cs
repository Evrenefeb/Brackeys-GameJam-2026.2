using DialogSystem.Runtime.UI.Theming;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DialogSystem.Runtime
{
    /// <summary>
    /// One pooled row in the history list.
    /// Subscribes to <see cref="DialogUIManager.OnThemeChanged"/> and applies the
    /// correct row background sprite, icon, colors, and fonts without needing a manager reference.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueHistoryItem : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Portrait or icon image on the left of the row.")]
        public Image icon;

        [Tooltip("Speaker name label.")]
        public TextMeshProUGUI speaker;

        [Tooltip("Dialog line body text.")]
        public TextMeshProUGUI line;

        [Header("Theme Targets")]
        [Tooltip("Background image of this row, swapped between character and choice row sprites.")]
        [SerializeField] private Image _rowBackground;

        [Tooltip("Fallback icon used for player-choice rows when the active theme does not provide one.")]
        [SerializeField] private Sprite _choiceIconFallback;

        [Header("Style")]
        [Tooltip("Italicise choice text to visually distinguish player choices from spoken lines.")]
        public bool italicForChoices = true;

        [Range(0.5f, 1.5f)]
        [Tooltip("Font-size scale applied to choice rows relative to the base size.")]
        public float choiceFontScale = 0.95f;

        [Tooltip("Hide the icon on choice rows.")]
        public bool hideIconForChoices = true;

        private float _baseLineFontSize = -1f;
        private bool _isChoice;
        private Sprite _portraitSprite;

        private static DialogThemeSO _activeTheme;

        private void Awake()
        {
            if (line != null && _baseLineFontSize < 0f)
            {
                _baseLineFontSize = line.fontSize;
            }

            SyncActiveTheme();
        }

        private void OnEnable()
        {
            DialogUIManager.OnThemeChanged += HandleThemeChanged;
            SyncActiveTheme();
            if (_activeTheme != null)
            {
                ApplyThemeToRow(_activeTheme);
            }
        }

        private void OnDisable()
        {
            DialogUIManager.OnThemeChanged -= HandleThemeChanged;
        }

        public void Bind(string speakerName, string text, Sprite portrait, bool isChoice)
        {
            _isChoice = isChoice;
            _portraitSprite = portrait;

            if (speaker != null)
            {
                var display = string.IsNullOrEmpty(speakerName) ? "Your Choice" : speakerName;
                speaker.text = isChoice ? $"({display})" : display;
            }

            if (line != null)
            {
                if (_baseLineFontSize < 0f)
                {
                    _baseLineFontSize = line.fontSize;
                }

                line.text = text ?? string.Empty;
                line.fontStyle = isChoice && italicForChoices ? FontStyles.Italic : FontStyles.Normal;
                line.fontSize = _baseLineFontSize * (isChoice ? choiceFontScale : 1f);
            }

            ApplyIcon();

            SyncActiveTheme();
            if (_activeTheme != null)
            {
                ApplyThemeToRow(_activeTheme);
            }
        }

        private void HandleThemeChanged(DialogThemeSO theme)
        {
            _activeTheme = theme;
            ApplyThemeToRow(theme);
        }

        private void ApplyThemeToRow(DialogThemeSO theme)
        {
            if (theme == null)
            {
                return;
            }

            if (_rowBackground != null)
            {
                var sprite = _isChoice ? theme.historyRowChoice : theme.historyRowChar;
                if (sprite != null)
                {
                    _rowBackground.sprite = sprite;
                }
            }

            if (speaker != null)
            {
                speaker.color = _isChoice ? theme.historyChoiceTextColor : theme.historySpeakerColor;
                SetFont(speaker, theme.historySpeakerFont != null ? theme.historySpeakerFont : theme.defaultFont);
            }

            if (line != null)
            {
                line.color = _isChoice ? theme.historyChoiceTextColor : theme.historyCharTextColor;
                SetFont(line, theme.historyLineFont != null ? theme.historyLineFont : theme.defaultFont);
            }

            ApplyIcon();
        }

        private void ApplyIcon()
        {
            if (icon == null)
            {
                return;
            }

            if (_isChoice && hideIconForChoices)
            {
                icon.enabled = false;
                return;
            }

            var themedChoiceIcon = _activeTheme != null ? _activeTheme.historyChoiceIcon : null;
            icon.sprite = _isChoice
                ? (themedChoiceIcon != null ? themedChoiceIcon : _choiceIconFallback)
                : _portraitSprite;
            icon.enabled = icon.sprite != null;
        }

        private void SyncActiveTheme()
        {
            if (DialogUIManager.ActiveTheme != null)
            {
                _activeTheme = DialogUIManager.ActiveTheme;
            }
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
