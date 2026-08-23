using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Utils;

namespace DialogSystem.Runtime.Actions
{
    /// <summary>
    /// Simple UnityEvent-friendly demo actions:
    /// - Count-up label until the active dialog ends.
    /// - Toggle a target GameObject via JSON payload.
    /// - Apply quick demo colors (red/green/blue) to a UI Image.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dialogue Graph System/Demos/UnityEvent Actions")]
    public class DemoUnityEventActions : MonoBehaviour
    {
        #region -------- Inspector --------
        [Header("Optional UI Outputs")]
        [Tooltip("Label that displays the running count.")]
        public TextMeshProUGUI counterLabel;

        [Tooltip("Any UI/panel you want to show/hide via Toggle_Target.")]
        public GameObject targetToShow;

        [Header("Color Target (pick one)")]
        [Tooltip("UI Image to tint when calling PickRed/Green/Blue.")]
        public Image uiImage;
        #endregion

        #region -------- State --------
        private Coroutine _running;
        private bool _countUpActive;
        private DialogManager _manager;
        #endregion

        #region -------- Unity --------
        private void Awake()
        {
            _manager = DialogManager.Instance;
        }

        private void OnEnable()
        {
            if (_manager != null)
                _manager.onDialogExit += StopCountUp;
        }

        private void OnDisable()
        {
            if (_manager != null)
                _manager.onDialogExit -= StopCountUp;

            StopAllCoroutines();
            _running = null;
            _countUpActive = false;
        }
        #endregion

        #region -------- UnityEvent bindings --------
        /// <summary>
        /// Starts an async COUNT-UP from 0, updating <see cref="counterLabel"/> every tick,
        /// and auto-stops when the dialog ends.
        /// Payload: <c>{"seconds":1}</c> (optional), otherwise defaults to 1 second.
        /// </summary>
        public void StartCountUpUntilDialogEnds(string payloadJson)
        {
            float tick = 1f;
            if (!string.IsNullOrWhiteSpace(payloadJson))
                PayloadHelper.TryGetFloat(payloadJson, "seconds", out tick);

            if (tick <= 0f) tick = 1f;

            StopCountUp();
            _countUpActive = true;
            _running = StartCoroutine(CoCountUp(tick));
        }

        /// <summary>
        /// Shows/hides <see cref="targetToShow"/>.
        /// Payload: <c>{"status":true}</c> sets explicit state.
        /// If payload is empty or missing "status", this toggles the current state.
        /// </summary>
        public void Toggle_Target(string json)
        {
            if (!targetToShow) return;

            if (PayloadHelper.TryGetBool(json, "status", out bool explicitState))
            {
                targetToShow.SetActive(explicitState);
            }
            else
            {
                targetToShow.SetActive(!targetToShow.activeSelf);
            }
        }

        /// <summary>Tints the target image red.</summary>
        public void PickRed() => ApplyColor(Color.red);

        /// <summary>Tints the target image green.</summary>
        public void PickGreen() => ApplyColor(Color.green);

        /// <summary>Tints the target image blue.</summary>
        public void PickBlue() => ApplyColor(Color.blue);
        #endregion

        #region -------- Routines --------
        private IEnumerator CoCountUp(float tickSeconds)
        {
            float t = 0f;
            while (_countUpActive)
            {
                if (counterLabel) counterLabel.text = Mathf.FloorToInt(t).ToString();
                yield return new WaitForSeconds(Mathf.Max(0.01f, tickSeconds));
                t += tickSeconds;
            }
        }
        #endregion

        #region -------- Helpers --------
        private void StopCountUp()
        {
            _countUpActive = false;
            if (_running != null) StopCoroutine(_running);
            _running = null;
        }

        private void ApplyColor(Color c)
        {
            if (uiImage) uiImage.color = c;
        }
        #endregion
    }
}