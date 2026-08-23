using System.Collections;
using UnityEngine;
using TMPro;
using DialogSystem.Runtime.Interfaces;
using DialogSystem.Runtime.Utils;

namespace DialogSystem.Runtime.Actions
{
    /// <summary>
    /// Simple action handler that blocks dialog flow while showing a countdown.
    /// Accepts payload either as a raw integer (e.g., "5") or JSON {"seconds":5}.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dialogue Graph System/Actions/Demo Countdown Handler")]
    public class DemoHandler_Countdown : MonoBehaviour, IActionHandler
    {
        #region -------- Inspector: Config & UI --------
        [Header("Action Binding")]
        [Tooltip("Wire this ID into your ActionNode's actionId.")]
        public string actionId = "demo.countdown.sync";

        [Header("Optional UI")]
        [Tooltip("Optional TMP label to display the countdown.")]
        public TextMeshProUGUI counterLabel;

        [Tooltip("Default seconds if payload is missing/invalid.")]
        [Min(0)] public int defaultSeconds = 3;
        #endregion

        #region -------- IActionHandler --------
        /// <summary>Returns true when this component handles the provided action id.</summary>
        public bool CanHandle(string id) => id == actionId;

        /// <summary>
        /// Runs a blocking countdown. Payload supports:
        /// - raw integer string: "5"
        /// - JSON with key "seconds": {"seconds":5}
        /// </summary>
        public IEnumerator Handle(string id, string payloadJson)
        {
            int seconds = ResolveSeconds(payloadJson, defaultSeconds);
            seconds = Mathf.Max(0, seconds);

            for (int s = seconds; s >= 0; s--)
            {
                if (counterLabel) counterLabel.text = s.ToString();
                yield return new WaitForSeconds(1f);
            }

            if (counterLabel) counterLabel.text = string.Empty;
        }
        #endregion

        #region -------- Helpers --------
        private static int ResolveSeconds(string payload, int fallback)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return fallback;

            // Try JSON form: {"seconds":5}
            if (PayloadHelper.TryGetInt(payload, "seconds", out var jsonVal))
                return jsonVal;

            // Try raw integer (optionally quoted)
            var raw = payload.Trim().Trim('"');
            if (int.TryParse(raw, out var rawVal))
                return rawVal;

            return fallback;
        }
        #endregion
    }
}

