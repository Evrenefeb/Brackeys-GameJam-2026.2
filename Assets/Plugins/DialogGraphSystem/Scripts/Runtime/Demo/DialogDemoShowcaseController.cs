using DialogSystem.Runtime.Actions;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Variables;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DialogSystem.Runtime.Demo
{
    /// <summary>
    /// Coordinates the three shipped demo entry points and guarantees clean replay state.
    /// Supporting graphs remain registered with <see cref="DialogManager"/> but are never exposed as menu entries.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dialogue Graph System/Demos/Showcase Controller")]
    public sealed class DialogDemoShowcaseController : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private DialogManager dialogManager;
        [SerializeField] private DialogDemoActionShowcase actionShowcase;
        [SerializeField] private DialogDemoVariableShowcase variableShowcase;

        [Header("Entry Graphs")]
        [SerializeField] private DialogGraph productTourGraph;
        [SerializeField] private DialogGraph shopGateGraph;
        [SerializeField] private DialogGraph reactorControlRoomGraph;

        [Header("Menu")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private GameObject activeDemoRoot;
        [SerializeField] private TextMeshProUGUI activeDemoLabel;
        [SerializeField] private Button productTourButton;
        [SerializeField] private Button shopGateButton;
        [SerializeField] private Button reactorControlRoomButton;

        [Header("Persistent Controls")]
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private Button resetDemoButton;
        [Tooltip("The settings button already owned by the runtime dialogue UI. Its click event is reused rather than duplicated.")]
        [SerializeField] private Button runtimeSettingsButton;
        [SerializeField] private Button showcaseSettingsButton;

        [Header("Shop Gate Route Setup")]
        [SerializeField] private Toggle startWithKeyToggle;
        [SerializeField] private Toggle startTrustedToggle;

        private DialogGraph _activeGraph;
        private string _activeTitle;
        private bool _returningToMenu;

        private void Awake()
        {
            BindButtons();
            ShowMenu();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        /// <summary>Starts the guided onboarding and branching demo.</summary>
        public void StartProductTour()
        {
            StartDemo(productTourGraph, "Product Tour", applyShopSetup: false);
        }

        /// <summary>Starts the variables, conditions, mutations, and outcomes demo.</summary>
        public void StartShopGate()
        {
            StartDemo(shopGateGraph, "Shop Gate", applyShopSetup: true);
        }

        /// <summary>Starts the visible action and Graph Jump demo.</summary>
        public void StartReactorControlRoom()
        {
            StartDemo(reactorControlRoomGraph, "Reactor Control Room", applyShopSetup: false);
        }

        /// <summary>Stops the active conversation and returns to the three-demo menu.</summary>
        public void BackToMenu()
        {
            if (_returningToMenu)
            {
                return;
            }

            _returningToMenu = true;
            if (dialogManager != null)
            {
                dialogManager.StopImmediately();
            }

            ShowMenu();
            _returningToMenu = false;
        }

        /// <summary>Restarts the current demo from the exact same clean starting state.</summary>
        public void ResetCurrentDemo()
        {
            if (_activeGraph == null)
            {
                ResetShowcaseState();
                ShowMenu();
                return;
            }

            var graph = _activeGraph;
            var title = _activeTitle;
            var applyShopSetup = graph == shopGateGraph;
            if (dialogManager != null)
            {
                dialogManager.StopImmediately();
            }

            StartDemo(graph, title, applyShopSetup);
        }

        /// <summary>Opens the existing runtime settings panel through its configured button.</summary>
        public void OpenRuntimeSettings()
        {
            if (runtimeSettingsButton != null)
            {
                runtimeSettingsButton.onClick.Invoke();
            }
        }

        private void StartDemo(DialogGraph graph, string title, bool applyShopSetup)
        {
            if (graph == null || dialogManager == null)
            {
                Debug.LogError("[DialogDemoShowcaseController] Cannot start a demo because its graph or DialogManager is missing.", this);
                ShowMenu();
                return;
            }

            var startWithKey = applyShopSetup && startWithKeyToggle != null && startWithKeyToggle.isOn;
            var startTrusted = applyShopSetup && startTrustedToggle != null && startTrustedToggle.isOn;

            ResetShowcaseState();
            if (applyShopSetup && variableShowcase != null)
            {
                variableShowcase.SetHasKey(startWithKey);
                variableShowcase.SetTrustLevel(startTrusted);
            }

            _activeGraph = graph;
            _activeTitle = title;
            SetActive(menuRoot, false);
            SetActive(activeDemoRoot, true);
            SetText(activeDemoLabel, title);
            SetNavigationInteractable(true);
            dialogManager.StartDialog(graph, ShowMenu);
        }

        private void ResetShowcaseState()
        {
            actionShowcase?.ResetShowcase();
            variableShowcase?.ResetDemoVariables();
        }

        private void ShowMenu()
        {
            _activeGraph = null;
            _activeTitle = string.Empty;
            SetActive(menuRoot, true);
            SetActive(activeDemoRoot, false);
            SetText(activeDemoLabel, "Choose a demo");
            SetNavigationInteractable(false);
        }

        private void SetNavigationInteractable(bool activeDemo)
        {
            if (backToMenuButton != null)
                backToMenuButton.interactable = activeDemo;
            if (resetDemoButton != null)
                resetDemoButton.interactable = activeDemo;
            if (showcaseSettingsButton != null)
                showcaseSettingsButton.interactable = runtimeSettingsButton != null;
        }

        private void BindButtons()
        {
            productTourButton?.onClick.AddListener(StartProductTour);
            shopGateButton?.onClick.AddListener(StartShopGate);
            reactorControlRoomButton?.onClick.AddListener(StartReactorControlRoom);
            backToMenuButton?.onClick.AddListener(BackToMenu);
            resetDemoButton?.onClick.AddListener(ResetCurrentDemo);
            showcaseSettingsButton?.onClick.AddListener(OpenRuntimeSettings);
        }

        private void UnbindButtons()
        {
            productTourButton?.onClick.RemoveListener(StartProductTour);
            shopGateButton?.onClick.RemoveListener(StartShopGate);
            reactorControlRoomButton?.onClick.RemoveListener(StartReactorControlRoom);
            backToMenuButton?.onClick.RemoveListener(BackToMenu);
            resetDemoButton?.onClick.RemoveListener(ResetCurrentDemo);
            showcaseSettingsButton?.onClick.RemoveListener(OpenRuntimeSettings);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }
    }
}
