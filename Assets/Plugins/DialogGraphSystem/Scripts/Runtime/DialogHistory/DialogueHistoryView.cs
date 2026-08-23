using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DialogSystem.Runtime.DialogHistory;
using DialogSystem.Runtime.UI.Theming;

namespace DialogSystem.Runtime
{
    /// <summary>
    /// Pooled, scroll-to-bottom history list renderer for <see cref="HistoryEntry"/>.
    /// Owns all Image references for the history panel and applies theme sprites
    /// through <see cref="ApplyTheme"/>, which is called by <see cref="DialogUIManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueHistoryView : MonoBehaviour
    {
        // ─── UI References ────────────────────────────────────────────────────────

        [Header("UI References")]
        [Tooltip("Root panel GameObject — shown/hidden by DialogueHistory.")]
        public GameObject root;

        [Tooltip("Vertical ScrollRect that contains the history rows.")]
        public ScrollRect scrollRect;

        [Tooltip("Parent transform for pooled row items.")]
        public Transform contentRoot;

        [Tooltip("Row prefab — must have a DialogueHistoryItem component.")]
        public DialogueHistoryItem itemPrefab;

        // ─── Theme Targets ────────────────────────────────────────────────────────

        [Header("Theme Targets")]
        [Tooltip("Background image of the history panel window. Set Image Type to Sliced.")]
        [SerializeField] private Image _panelBackground;

        [Tooltip("Title-bar strip image at the top of the panel.")]
        [SerializeField] private Image _titleBackground;

        [Tooltip("Text label in the history panel title bar. Receives historyTitleTextColor from the theme.")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Tooltip("Close / X button image.")]
        [SerializeField] private Image _closeButtonImage;

        // ─── Pool Settings ────────────────────────────────────────────────────────

        [Header("Pool Settings")]
        [Min(0), Tooltip("Pre-instantiate this many row items on Start to avoid first-open stutter.")]
        public int prewarm = 24;

        [Tooltip("When true, choice rows hide their portrait icon.")]
        public bool hideChoiceIcon = true;

        // ─── Private State ────────────────────────────────────────────────────────

        private readonly List<DialogueHistoryItem> _pool = new();
        private int _activeCount;
        private bool _dirtyScrollToBottom;
        private Button _runtimeCloseButton;
        private UnityAction _runtimeCloseAction;
        private bool _didWarnMissingCloseButton;
        private bool _didWarnMissingRoot;

        public bool IsVisible => GetPrimaryPanelObject().activeInHierarchy;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            AutoResolveViewBindings();
            ValidateViewBindings();
        }

        private void Start() => Prewarm(prewarm);

        private void OnDestroy()
        {
            UnbindCloseAction();
        }

        private void LateUpdate()
        {
            if (!_dirtyScrollToBottom) return;
            _dirtyScrollToBottom = false;
            Canvas.ForceUpdateCanvases();
            if (scrollRect) scrollRect.verticalNormalizedPosition = 0f;
        }

        // ─── Theming ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the given theme to every Image target owned by this panel.
        /// Called by <see cref="DialogUIManager"/> whenever the active theme changes.
        /// </summary>
        public void ApplyTheme(DialogThemeSO theme)
        {
            if (theme == null) return;
            SetSprite(_panelBackground,  theme.historyPanelBg);
            SetSprite(_titleBackground,  theme.historyTitleBg);
            SetSprite(_closeButtonImage, theme.closeButton);
            SetTextColor(_titleText, theme.historyTitleTextColor);
        }

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Wires a runtime close action to the history panel close button.
        /// </summary>
        public void BindCloseAction(Action closeAction)
        {
            UnbindCloseAction();

            if (closeAction == null)
            {
                Debug.LogWarning("[DialogueHistoryView] Close action binding skipped because the provided action is null.", this);
                return;
            }

            _runtimeCloseButton = ResolveCloseButton();
            if (_runtimeCloseButton == null)
            {
                if (!_didWarnMissingCloseButton)
                {
                    Debug.LogWarning("[DialogueHistoryView] Unable to resolve a close button for the history panel. Assign a close button image/button in the panel hierarchy so runtime close wiring can be applied.", this);
                    _didWarnMissingCloseButton = true;
                }

                return;
            }

            _runtimeCloseAction = () => closeAction();
            _runtimeCloseButton.onClick.AddListener(_runtimeCloseAction);
        }

        /// <summary>Shows the history panel root.</summary>
        public void Show()
        {
            var primaryPanel = GetPrimaryPanelObject();
            ActivateHierarchy(primaryPanel.transform);

            if (root != null && root != primaryPanel)
            {
                ActivateHierarchy(root.transform);
            }
        }

        /// <summary>Hides the history panel root.</summary>
        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            var primaryPanel = GetPrimaryPanelObject();
            if (primaryPanel != root)
            {
                primaryPanel.SetActive(false);
            }
        }

        /// <summary>Rebuilds the full list from <paramref name="entries"/> and scrolls to bottom.</summary>
        public void Refresh(IReadOnlyList<HistoryEntry> entries)
        {
            ReturnAll();
            EnsurePool(entries != null ? entries.Count : 0);
            if (entries != null)
                for (int i = 0; i < entries.Count; i++)
                    BindToNext(entries[i]);
            _dirtyScrollToBottom = true;
        }

        /// <summary>Appends one entry to the bottom of the list and scrolls to it.</summary>
        public void AppendItem(HistoryEntry entry)
        {
            EnsurePool(_activeCount + 1);
            BindToNext(entry);
            _dirtyScrollToBottom = true;
        }

        // ─── Pool Helpers ─────────────────────────────────────────────────────────

        private void Prewarm(int count) => EnsurePool(count);

        private void EnsurePool(int needed)
        {
            if (!itemPrefab)  { Debug.LogError("[DialogueHistoryView] itemPrefab not assigned.");  return; }
            if (!contentRoot) { Debug.LogError("[DialogueHistoryView] contentRoot not assigned."); return; }

            while (_pool.Count < needed)
            {
                var item = Instantiate(itemPrefab, contentRoot);
                item.gameObject.SetActive(false);
                item.hideIconForChoices = hideChoiceIcon;
                _pool.Add(item);
            }
        }

        private void ReturnAll()
        {
            for (int i = 0; i < _activeCount; i++)
                if (_pool[i]) _pool[i].gameObject.SetActive(false);
            _activeCount = 0;
        }

        private void BindToNext(HistoryEntry e)
        {
            if (_activeCount >= _pool.Count || e == null) return;
            var item    = _pool[_activeCount++];
            var portrait = (e.kind == HistoryKind.Choice && item.hideIconForChoices) ? null : e.portrait;
            item.gameObject.SetActive(true);
            item.Bind(
                e.kind == HistoryKind.Choice ? "Your Choice" : e.speaker,
                e.text,
                portrait,
                e.kind == HistoryKind.Choice
            );
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private void UnbindCloseAction()
        {
            if (_runtimeCloseButton != null && _runtimeCloseAction != null)
            {
                _runtimeCloseButton.onClick.RemoveListener(_runtimeCloseAction);
            }

            _runtimeCloseButton = null;
            _runtimeCloseAction = null;
        }

        private void ValidateViewBindings()
        {
            if (root == null && !_didWarnMissingRoot)
            {
                Debug.LogWarning("[DialogueHistoryView] Root panel is not assigned. History visibility cannot be controlled until root is assigned.", this);
                _didWarnMissingRoot = true;
            }
        }

        private void AutoResolveViewBindings()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (scrollRect == null)
            {
                scrollRect = GetComponentInChildren<ScrollRect>(true);
            }

            if (contentRoot == null && scrollRect != null)
            {
                contentRoot = scrollRect.content;
            }
        }

        private Button ResolveCloseButton()
        {
            var closeButton = GetButtonFromCloseImage();
            if (closeButton != null)
            {
                return closeButton;
            }

            if (root == null)
            {
                return null;
            }

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var candidate = buttons[i];
                if (candidate == null)
                {
                    continue;
                }

                var lowerName = candidate.name.ToLowerInvariant();
                if (lowerName.Contains("close") || lowerName == "x")
                {
                    return candidate;
                }
            }

            return null;
        }

        private Button GetButtonFromCloseImage()
        {
            if (_closeButtonImage == null)
            {
                return null;
            }

            var button = _closeButtonImage.GetComponent<Button>();
            if (button != null)
            {
                return button;
            }

            return _closeButtonImage.GetComponentInParent<Button>();
        }

        private static void SetSprite(Image img, Sprite s)
        {
            if (img != null && s != null) img.sprite = s;
        }

        private static void SetTextColor(TextMeshProUGUI tmp, Color c)
        {
            if (tmp != null) tmp.color = c;
        }

        private GameObject GetPrimaryPanelObject()
        {
            return gameObject != null ? gameObject : root;
        }

        private static void ActivateHierarchy(Transform start)
        {
            while (start != null)
            {
                start.gameObject.SetActive(true);

                if (start.GetComponent<Canvas>() != null)
                {
                    break;
                }

                start = start.parent;
            }
        }
    }
}
