using System;
using System.Collections.Generic;
using UnityEngine;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime;
using DialogSystem.Runtime.Localization;
using DialogSystem.Runtime.Utils;
using DialogSystem.Runtime.UI.Theming;

namespace DialogSystem.Runtime.DialogHistory
{
    /// <summary>Type of entry stored in the dialog history.</summary>
    public enum HistoryKind { Line, Choice }

    /// <summary>One line of history: a spoken line or a chosen option.</summary>
    [Serializable]
    public class HistoryEntry
    {
        #region -------- Data --------
        public HistoryKind kind;
        public string speaker;
        public Sprite portrait;    // optional UI icon
        [TextArea] public string text;
        public string nodeGuid;
        public DateTime time = DateTime.Now;

        /// <summary>
        /// Locale key for the speaker name. Populated when <see cref="DialogManager"/> fires
        /// <see cref="DialogManager.OnLineShownLocalized"/>. Empty when no localization key is available.
        /// </summary>
        public string speakerLocaleKey;

        /// <summary>
        /// Locale key for the line text. Populated when <see cref="DialogManager"/> fires
        /// <see cref="DialogManager.OnLineShownLocalized"/>. Empty when no localization key is available.
        /// </summary>
        public string textLocaleKey;

        /// <summary>
        /// The raw (source-language) speaker name before locale resolution.
        /// Used as fallback when <see cref="speakerLocaleKey"/> is present but cannot be resolved.
        /// </summary>
        public string rawSpeaker;

        /// <summary>
        /// The raw (source-language) line text before locale resolution.
        /// Used as fallback when <see cref="textLocaleKey"/> is present but cannot be resolved.
        /// </summary>
        public string rawText;

        /// <summary>
        /// Returns true when this entry carries locale keys that can be re-resolved
        /// on a locale change.
        /// </summary>
        public bool HasLocaleKeys =>
            !string.IsNullOrEmpty(speakerLocaleKey) || !string.IsNullOrEmpty(textLocaleKey);

        /// <summary>
        /// Re-resolves <see cref="speaker"/> and <see cref="text"/> against the currently
        /// active <see cref="DialogLocalizationRuntime"/> table.
        /// Falls back to <see cref="rawSpeaker"/> / <see cref="rawText"/> when a key is
        /// not found in the new table. No-op when no locale keys are stored.
        /// </summary>
        public void ResolveLocale()
        {
            if (!HasLocaleKeys) return;

            var loc = DialogLocalizationRuntime.Instance;
            if (!string.IsNullOrEmpty(speakerLocaleKey))
                speaker = loc.Resolve(speakerLocaleKey, rawSpeaker ?? speaker);
            if (!string.IsNullOrEmpty(textLocaleKey))
                text = loc.Resolve(textLocaleKey, rawText ?? text);
        }
        #endregion

        #region -------- Ctors --------
        public HistoryEntry(HistoryKind k, string spk, string txt, string guid)
        {
            kind = k; speaker = spk; text = txt; nodeGuid = guid;
        }

        public HistoryEntry(HistoryKind k, string spk, string txt, string guid, Sprite p)
        {
            kind = k; speaker = spk; text = txt; nodeGuid = guid; portrait = p;
        }
        #endregion
    }

    /// <summary>
    /// Collects and displays dialog history. Subscribes to <see cref="DialogManager"/> events,
    /// buffers entries up to <see cref="maxEntries"/>, and drives a <see cref="DialogueHistoryView"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueHistory : MonoBehaviour
    {
        #region -------- Refs --------
        [Header("Refs")]
        [Tooltip("DialogManager to listen to. Defaults to DialogManager.Instance at runtime.")]
        [SerializeField]
        private DialogManager manager;

        [Tooltip("View that renders the history list.")]
        [SerializeField]
        private DialogueHistoryView view;
        #endregion

        #region -------- Settings --------
        [Header("Settings")]
        [Range(50, 2000)]
        [Tooltip("Maximum number of entries to keep in memory.")]
        public int maxEntries = 500;

        [Tooltip("If enabled and autoplay was ON when opening, autoplay will be toggled back ON when closing.")]
        public bool resumeAutoplayOnClose = false;
        #endregion

        #region -------- State --------
        private readonly List<HistoryEntry> _entries = new();
        private bool _isOpen;
        private bool _lastAutoPlayState;
        private bool _isSubscribed;
        private bool _didWarnMissingView;

        /// <summary>Current in-memory history buffer.</summary>
        public IReadOnlyList<HistoryEntry> Entries => _entries;
        public bool IsOpen => _isOpen;
        public bool HasResolvedView => view != null;
        #endregion

        #region -------- Unity Lifecycle --------
        private void Awake()
        {
            ResolveReferences();
            InitializeView();
            ForceClosedState();
        }

        private void OnEnable()
        {
            InitializeView();
            SubscribeToManager();
            DialogLocalizationRuntime.Instance.OnLocaleChanged += HandleLocaleChanged;
        }

        private void OnDisable()
        {
            if (_isOpen)
            {
                Close();
            }

            UnsubscribeFromManager();
            ForceClosedState();
            DialogLocalizationRuntime.Instance.OnLocaleChanged -= HandleLocaleChanged;
        }

        private void OnDestroy()
        {
            ForceClosedState();
        }

        private void Update()
        {
            if (_isSubscribed)
                return;

            SubscribeToManager();
        }
        #endregion

        #region -------- Event Handlers --------
        private void HandleLine(string guid, string speaker, string text)
        {
            var safeSpeaker = string.IsNullOrEmpty(speaker) ? "???" : speaker;
            var portrait = GetCurrentPortraitSafe();
            Add(new HistoryEntry(HistoryKind.Line, safeSpeaker, text, guid, portrait));
        }

        /// <summary>
        /// Handles the locale-aware line event fired by <see cref="DialogManager.OnLineShownLocalized"/>.
        /// Stores locale keys alongside the resolved text so the entry can be re-resolved when
        /// the player changes language mid-session.
        /// </summary>
        private void HandleLineLocalized(string guid, string speaker, string text, string speakerLocaleKey, string textLocaleKey, string rawSpeaker, string rawText)
        {
            var safeSpeaker = string.IsNullOrEmpty(speaker) ? "???" : speaker;
            var portrait = GetCurrentPortraitSafe();
            var entry = new HistoryEntry(HistoryKind.Line, safeSpeaker, text, guid, portrait)
            {
                speakerLocaleKey = speakerLocaleKey,
                textLocaleKey = textLocaleKey,
                rawSpeaker = rawSpeaker,
                rawText = rawText
            };

            if (ReplaceLastGenericEntry(entry))
            {
                return;
            }

            Add(entry);
        }

        private void HandleChoice(string guid, string text)
        {
            Add(new HistoryEntry(HistoryKind.Choice, "Your Choice", text, guid, null));
        }

        private void HandleChoiceLocalized(string guid, string text, string textLocaleKey, string rawText)
        {
            var entry = new HistoryEntry(HistoryKind.Choice, "Your Choice", text, guid, null)
            {
                textLocaleKey = textLocaleKey,
                rawText = rawText
            };

            if (ReplaceLastGenericEntry(entry))
            {
                return;
            }

            Add(entry);
        }

        private void HandleConversationStarted()
        {
            ClearAll();
        }

        /// <summary>
        /// Re-resolves all stored entries against the new active locale and refreshes
        /// the view if it is currently open.
        /// </summary>
        private void HandleLocaleChanged()
        {
            foreach (var entry in _entries)
            {
                entry.ResolveLocale();
            }

            if (_isOpen && view != null)
            {
                view.Refresh(_entries);
            }
        }
        #endregion

        #region -------- Core List Ops --------
        /// <summary>Adds a history entry, trimming the buffer if needed, and updates the view if open.</summary>
        private void Add(HistoryEntry e)
        {
            _entries.Add(e);
            if (_entries.Count > maxEntries) _entries.RemoveAt(0);
            if (_isOpen && view != null) view.AppendItem(e);
        }

        private bool ReplaceLastGenericEntry(HistoryEntry entry)
        {
            if (entry == null || _entries.Count == 0)
            {
                return false;
            }

            var lastIndex = _entries.Count - 1;
            var last = _entries[lastIndex];
            if (last == null ||
                last.kind != entry.kind ||
                last.HasLocaleKeys ||
                !string.Equals(last.nodeGuid, entry.nodeGuid, StringComparison.Ordinal))
            {
                return false;
            }

            _entries[lastIndex] = entry;
            if (_isOpen && view != null)
            {
                view.Refresh(_entries);
            }

            return true;
        }

        /// <summary>Clears all history and refreshes the view if open.</summary>
        public void ClearAll()
        {
            _entries.Clear();
            if (_isOpen && view != null) view.Refresh(_entries);
        }
        #endregion

        #region -------- Panel Open/Close --------
        public void Initialize(DialogManager dialogManager, DialogueHistoryView historyView)
        {
            // Ensure locale-change handler is wired even when OnEnable has not
            // fired yet (e.g. EditMode tests that call AddComponent + Initialize
            // back-to-back).  SubscribeToManager / UnsubscribeToManager now manage
            // this subscription together with the other manager events.
            DialogLocalizationRuntime.Instance.OnLocaleChanged -= HandleLocaleChanged;
            DialogLocalizationRuntime.Instance.OnLocaleChanged += HandleLocaleChanged;

            if (dialogManager != null)
                manager = dialogManager;

            if (historyView != null)
                view = historyView;

            InitializeView();
            UnsubscribeFromManager();
            SubscribeToManager();
        }

        /// <summary>Toggles the history panel open/closed.</summary>
        public void Toggle()
        {
            SyncOpenStateWithView();

            if (_isOpen) Close(); else Open();
        }

        /// <summary>Opens the history panel and pauses dialog playback.</summary>
        public void Open()
        {
            SyncOpenStateWithView();
            if (_isOpen) return;
            _isOpen = true;

            ResolveReferences();

            if (manager != null)
            {
                _lastAutoPlayState = manager.GetAutoPlayState();
                manager.PauseForHistory();
            }

            if (view != null)
            {
                view.Show();
                view.Refresh(_entries);
            }
        }

        /// <summary>Closes the history panel and resumes dialog (optionally restoring autoplay).</summary>
        public void Close()
        {
            SyncOpenStateWithView();
            if (!_isOpen) return;
            _isOpen = false;

            if (view != null) view.Hide();

            if (manager != null)
            {
                manager.ResumeAfterHistory();

                // Resume autoplay only if opted in and it was on before opening.
                if (resumeAutoplayOnClose && _lastAutoPlayState && !manager.GetAutoPlayState())
                    manager.ToggleAutoPlay();
            }
        }
        #endregion

        #region -------- Helpers --------
        private void ResolveReferences()
        {
            if (manager == null)
                manager = DialogManager.Instance;

            if (view == null)
                view = manager != null ? manager.dialogUIManager?.CurrentHistoryView : null;

            if (view == null && manager?.dialogUIController != null)
                view = manager.dialogUIController.GetComponentInChildren<DialogueHistoryView>(true);

            if (view == null)
                view = DialogRuntimeUnityCompatibility.FindFirst<DialogueHistoryView>(includeInactive: true);
        }

        private void InitializeView()
        {
            ResolveReferences();

            if (view == null)
            {
                if (!_didWarnMissingView)
                {
                    Debug.LogWarning("[DialogueHistory] No DialogueHistoryView assigned. History panel open/close UI will be unavailable.", this);
                    _didWarnMissingView = true;
                }

                return;
            }

            view.BindCloseAction(Close);
            view.Hide();
        }

        private void SubscribeToManager()
        {
            ResolveReferences();

            if (manager == null || _isSubscribed)
                return;

            manager.OnLineShown += HandleLine;
            manager.OnLineShownLocalized += HandleLineLocalized;
            manager.OnChoicePicked += HandleChoice;
            manager.OnChoicePickedLocalized += HandleChoiceLocalized;
            manager.onDialogEnter += HandleConversationStarted;
            _isSubscribed = true;
        }

        private void UnsubscribeFromManager()
        {
            if (!_isSubscribed || manager == null)
            {
                _isSubscribed = false;
                return;
            }

            manager.OnLineShown -= HandleLine;
            manager.OnLineShownLocalized -= HandleLineLocalized;
            manager.OnChoicePicked -= HandleChoice;
            manager.OnChoicePickedLocalized -= HandleChoiceLocalized;
            manager.onDialogEnter -= HandleConversationStarted;
            _isSubscribed = false;
        }

        private Sprite GetCurrentPortraitSafe()
        {
            var ui = manager != null ? manager.dialogUIController : null;
            return (ui != null && ui.portraitImage != null) ? ui.portraitImage.sprite : null;
        }

        private void ForceClosedState()
        {
            _isOpen = false;

            if (view != null)
            {
                view.Hide();
            }
        }

        private void SyncOpenStateWithView()
        {
            if (view == null)
            {
                return;
            }

            _isOpen = view.IsVisible;
        }
        #endregion
    }
}