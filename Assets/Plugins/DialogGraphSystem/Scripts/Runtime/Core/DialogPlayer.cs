using DialogSystem.Runtime.UI.Theming;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DialogSystem.Runtime.Models;

namespace DialogSystem.Runtime.Core
{
    public class DialogPlayer : MonoBehaviour
    {
        [Serializable]
        public sealed class DialogLaunchOption
        {
            [Tooltip("Friendly label for scene buttons or dropdowns.")]
            public string label = "Demo Dialog";

            [Tooltip("DialogManager dialogID to play. Leave empty if you want to play a graph asset directly.")]
            public string dialogID;

            [Tooltip("Optional graph asset override for sample scenes that do not use DialogManager.dialogID mapping yet.")]
            public DialogGraph dialogGraph;

            [Tooltip("Theme applied when this option starts.")]
            public DialogThemeType theme = DialogThemeType.Default;

            [Tooltip("Optional object to hide while this dialog plays. Falls back to Main Menu when empty.")]
            public GameObject hideWhilePlaying;

            [Tooltip("Optional object to show again when this dialog ends. Falls back to Main Menu when empty.")]
            public GameObject showWhenFinished;
        }

        [Serializable]
        public sealed class DialogIdEvent : UnityEvent<string> { }

        [Serializable]
        public sealed class DialogIndexEvent : UnityEvent<int> { }

        [Serializable]
        private sealed class QuitGamePayload
        {
            public string message = string.Empty;
            public string reason = string.Empty;
        }

        [Header("Default Launch")]
        public GameObject mainMenu;
        public DialogThemeType theme;

        [Header("Launch Behavior")]
        [SerializeField] private bool hideMainMenuWhilePlaying = true;
        [SerializeField] private bool showMainMenuOnEnd = true;
        [SerializeField] private bool stopActiveDialogBeforeStarting = true;

        [Header("Events")]
        public UnityEvent onDialogStarted;
        public UnityEvent onDialogFinished;
        [SerializeField] private DialogIdEvent onDialogStartedById = new();
        [SerializeField] private DialogIdEvent onDialogFinishedById = new();
        [SerializeField] private DialogIndexEvent onSelectionChanged = new();

        private string _lastDialogID;
        private DialogGraph _lastDialogGraph;
        private DialogThemeType _lastTheme;
        private GameObject _menuToShowOnEnd;
        private bool _dialogActive;

        #region ---------------- Launch API ----------------

        public void StartDialog(string dialogID)
        {
            if (string.IsNullOrWhiteSpace(dialogID))
            {
                Debug.LogWarning("[DialogPlayer] Cannot start dialog with an empty dialog id.");
                return;
            }

            LaunchDialog(dialogID, null, theme, mainMenu, mainMenu);
        }

        public void StartDialogGraph(DialogGraph graph)
        {
            if (graph == null)
            {
                Debug.LogWarning("[DialogPlayer] Cannot start a dialog graph because the graph reference is null.");
                return;
            }

            LaunchDialog(null, graph, theme, mainMenu, mainMenu);
        }

        public void StopDialog()
        {
            if (!_dialogActive)
                return;

            var manager = DialogManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[DialogPlayer] Cannot stop dialog because no DialogManager instance was found.");
                return;
            }

            manager.SkipAll();
        }
        #endregion

        #region ---------------- Lifecycle ----------------
        public void OnDialogEnded()
        {
            _dialogActive = false;
            SetMenuVisibility(_menuToShowOnEnd, showMainMenuOnEnd);
            onDialogFinished?.Invoke();
            onDialogFinishedById?.Invoke(_lastDialogID ?? string.Empty);
        }

        public void QuitGame(string payloadJson)
        {
            var payload = TryParseQuitPayload(payloadJson);
            var summary = payload != null
                ? payload.message ?? payload.reason ?? payloadJson
                : payloadJson;

            Debug.Log($"[DialogPlayer] Quit Game requested. Payload={summary ?? string.Empty}");
        }
        #endregion

        #region ---------------- Internals ----------------
        private void LaunchDialog(string dialogID, DialogGraph graph, DialogThemeType themeType, GameObject hideTarget, GameObject showTarget)
        {
            var manager = DialogManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[DialogPlayer] Cannot start dialog because no DialogManager instance was found.");
                return;
            }

            if (graph == null && string.IsNullOrWhiteSpace(dialogID))
            {
                Debug.LogWarning("[DialogPlayer] Cannot start dialog because both dialogID and graph are empty.");
                return;
            }

            if (stopActiveDialogBeforeStarting && _dialogActive)
                manager.SkipAll();

            _menuToShowOnEnd = showTarget != null ? showTarget : mainMenu;
            SetMenuVisibility(hideTarget != null ? hideTarget : mainMenu, !hideMainMenuWhilePlaying);

            _lastDialogID = dialogID;
            _lastDialogGraph = graph;
            _lastTheme = themeType;
            _dialogActive = true;

            onDialogStarted?.Invoke();
            onDialogStartedById?.Invoke(dialogID ?? string.Empty);

            if (graph != null)
            {
                manager.StartDialog(graph, themeType, OnDialogEnded);
                return;
            }

            manager.PlayDialogByID(dialogID, themeType, OnDialogEnded);
        }


        private static void SetMenuVisibility(GameObject target, bool visible)
        {
            if (target != null)
                target.SetActive(visible);
        }

        private static QuitGamePayload TryParseQuitPayload(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
                return null;

            try
            {
                return JsonUtility.FromJson<QuitGamePayload>(payloadJson);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DialogPlayer] Failed to parse quit payload JSON: {ex.Message}");
                return null;
            }
        }
        #endregion
    }
}