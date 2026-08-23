using System;
using System.Collections;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Interfaces;
using DialogSystem.Runtime.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DialogSystem.Runtime.Actions
{
    /// <summary>
    /// Turnkey sample action handler for the shipped demo action IDs.
    /// Wire one component into <see cref="Core.DialogActionRunner"/> to showcase
    /// TV/display toggles, door movement, alarms, countdowns, and scene dimming.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dialogue Graph System/Demos/Action Showcase")]
    public class DialogDemoActionShowcase : MonoBehaviour, IActionHandler
    {
        #region ---------------- Payload Models ----------------
        [Serializable]
        private sealed class TurnOnTvPayload
        {
            public bool on = true;
        }

        [Serializable]
        private sealed class OpenDoorPayload
        {
            public string doorId = "main_door";
            public string speed = "normal";
            public bool open = true;
        }

        [Serializable]
        private sealed class PlayAlarmPayload
        {
            public string alarmId = "facility_warning";
            public bool loop = false;
            public float duration = 3.5f;
        }

        [Serializable]
        private sealed class StartCountdownPayload
        {
            public string countdownId = "evac";
            public int seconds = 5;
            public bool showOnHud = true;
        }

        [Serializable]
        private sealed class FadeLightsPayload
        {
            public string group = "main";
            public float intensity = 35f;
            public float duration = 1.25f;
            public string color = "warm";
        }
        #endregion

        #region ---------------- Inspector: Action IDs ----------------
        [Header("Action IDs")]
        [SerializeField] private string turnOnTvActionId = "TurnOnTV";
        [SerializeField] private string openDoorActionId = "OpenGate";
        [SerializeField] private string playAlarmActionId = "PlayAlarm";
        [SerializeField] private string startCountdownActionId = "StartCountdown";
        [SerializeField] private string fadeLightsActionId = "FadeLights";
        #endregion

        #region ---------------- Inspector: TV Sprite Demo ----------------
        [Header("TV Sprite Demo")]
        [Tooltip("The UI Image used to render the TV sprite.")]
        [SerializeField] private Image tvImage;

        [Tooltip("Sprite shown when the TV is off.")]
        [SerializeField] private Sprite tvOffSprite;

        [Tooltip("Sprite shown when the TV is on.")]
        [SerializeField] private Sprite tvOnSprite;
        #endregion

        #region ---------------- Inspector: Door Sprite Demo ----------------
        [Header("Door Sprite Demo")]
        [Tooltip("Transform of the sliding door sprite.")]
        [SerializeField] private Transform doorTransform;

        [Tooltip("How far the door slides on the local X axis to open.")]
        [SerializeField] private float doorOpenDistance = -3f;

        [Tooltip("Seconds to complete a full open or close slide.")]
        [SerializeField] private float doorSlowDuration = 1.8f;
        [SerializeField] private float doorNormalDuration = 1.0f;
        [SerializeField] private float doorFastDuration = 0.5f;
        #endregion

        #region ---------------- Inspector: Alarm Overlay ----------------
        [Header("Alarm Overlay")]
        [Tooltip("Overlay image animated by PlayAlarm. Keep it active in the scene and set the starting color to the idle color.")]
        [SerializeField] private Image alarmOverlay;
        [SerializeField] private AudioSource alarmAudioSource;
        [SerializeField] private TextMeshProUGUI alarmStatusLabel;
        [SerializeField] private Color alarmIdleColor = new Color(1f, 0.35f, 0.35f, 0f);
        [SerializeField] private Color alarmAlertColor = new Color(1f, 0.2f, 0.2f, 0.45f);
        [SerializeField] private float alarmPulseDuration = 0.35f;
        #endregion

        #region ---------------- Inspector: Countdown Demo ----------------
        [Header("Countdown Demo")]
        [SerializeField] private GameObject countdownRoot;
        [SerializeField] private TextMeshProUGUI countdownLabel;
        [SerializeField] private TextMeshProUGUI countdownStatusLabel;
        #endregion

        #region ---------------- Inspector: Scene Dimmer ----------------
        [Header("Scene Dimmer")]
        [Tooltip("Overlay image animated by FadeLights. Keep it active in the scene and start with alpha 0.")]
        [SerializeField] private Image sceneDimOverlay;
        [SerializeField] private TextMeshProUGUI lightsStatusLabel;
        #endregion

        #region ---------------- Inspector: Timing ----------------
        [Header("Timing")]
        [SerializeField] private bool useUnscaledTime;
        #endregion

        #region ---------------- State ----------------
        private Vector3 _doorClosedLocalPosition;
        private bool _doorIsOpen;
        private bool _tvIsOn;

        private int _tvToken;
        private int _doorToken;
        private int _alarmToken;
        private int _countdownToken;
        private int _lightsToken;
        #endregion

        #region ---------------- Unity ----------------
        private void Awake()
        {
            if (doorTransform != null)
                _doorClosedLocalPosition = doorTransform.localPosition;

            if (sceneDimOverlay != null)
                sceneDimOverlay.raycastTarget = false;

            if (alarmOverlay != null)
            {
                alarmOverlay.raycastTarget = false;
            }

            ResetShowcase();
        }

        #endregion

        #region ---------------- Public: Showcase State ----------------
        /// <summary>
        /// Cancels in-flight sample actions and restores every visible demo effect to its idle state.
        /// Call this before starting or replaying any showcase graph.
        /// </summary>
        public void ResetShowcase()
        {
            _tvToken++;
            _doorToken++;
            _alarmToken++;
            _countdownToken++;
            _lightsToken++;
            StopAllCoroutines();

            SetTvSprite(on: false);

            _doorIsOpen = false;
            if (doorTransform != null)
                doorTransform.localPosition = _doorClosedLocalPosition;

            if (alarmAudioSource != null)
            {
                alarmAudioSource.Stop();
                alarmAudioSource.loop = false;
            }

            SetAlarmOverlayColor(alarmIdleColor);
            SetText(alarmStatusLabel, "Ready");

            SetText(countdownLabel, string.Empty);
            SetText(countdownStatusLabel, "Ready");
            SetActive(countdownRoot, false);

            if (sceneDimOverlay != null)
            {
                sceneDimOverlay.gameObject.SetActive(true);
                var color = sceneDimOverlay.color;
                color.a = 0f;
                sceneDimOverlay.color = color;
            }

            SetText(lightsStatusLabel, "Ready");
        }
        #endregion

        #region ---------------- IActionHandler ----------------
        public bool CanHandle(string id)
        {
            return string.Equals(id, turnOnTvActionId, StringComparison.Ordinal)
                || string.Equals(id, openDoorActionId, StringComparison.Ordinal)
                || string.Equals(id, playAlarmActionId, StringComparison.Ordinal)
                || string.Equals(id, startCountdownActionId, StringComparison.Ordinal)
                || string.Equals(id, fadeLightsActionId, StringComparison.Ordinal);
        }

        public IEnumerator Handle(string id, string payloadJson)
        {
            if (string.Equals(id, turnOnTvActionId, StringComparison.Ordinal))
            {
                yield return RunTurnOnTv(payloadJson);
                yield break;
            }

            if (string.Equals(id, openDoorActionId, StringComparison.Ordinal))
            {
                yield return RunOpenDoor(payloadJson);
                yield break;
            }

            if (string.Equals(id, playAlarmActionId, StringComparison.Ordinal))
            {
                yield return RunPlayAlarm(payloadJson);
                yield break;
            }

            if (string.Equals(id, startCountdownActionId, StringComparison.Ordinal))
            {
                yield return RunCountdown(payloadJson);
                yield break;
            }

            if (string.Equals(id, fadeLightsActionId, StringComparison.Ordinal))
            {
                yield return RunFadeLights(payloadJson);
                yield break;
            }
        }
        #endregion

        #region ---------------- Public: Door ----------------
        /// <summary>Slides the door open.</summary>
        public void OpenDoor() => StartCoroutine(CoSlideDoor(open: true, ResolveDoorDuration("normal")));

        /// <summary>Slides the door closed.</summary>
        public void CloseDoor() => StartCoroutine(CoSlideDoor(open: false, ResolveDoorDuration("normal")));

        /// <summary>Toggles the door state.</summary>
        public void ToggleDoor() => StartCoroutine(CoSlideDoor(!_doorIsOpen, ResolveDoorDuration("normal")));

        /// <summary>Slides the door open slowly.</summary>
        public void OpenDoorSlow() => StartCoroutine(CoSlideDoor(open: true, ResolveDoorDuration("slow")));

        /// <summary>Slides the door open quickly.</summary>
        public void OpenDoorFast() => StartCoroutine(CoSlideDoor(open: true, ResolveDoorDuration("fast")));

        /// <summary>Slides the door closed slowly.</summary>
        public void CloseDoorSlow() => StartCoroutine(CoSlideDoor(open: false, ResolveDoorDuration("slow")));

        /// <summary>Slides the door closed quickly.</summary>
        public void CloseDoorFast() => StartCoroutine(CoSlideDoor(open: false, ResolveDoorDuration("fast")));
        #endregion

        #region ---------------- Public: Lights ----------------
        /// <summary>Fades the dimmer overlay to full opacity.</summary>
        public void DimLights() => StartCoroutine(CoFadeDimmer(1f, 0.6f));

        /// <summary>Fades the dimmer overlay to 50 percent opacity.</summary>
        public void DimLightsHalf() => StartCoroutine(CoFadeDimmer(0.5f, 0.6f));
        #endregion

        #region ---------------- Public: TV ----------------
        /// <summary>Swaps to the TV-on sprite.</summary>
        public void TurnTvOn() => SetTvSprite(on: true);

        /// <summary>Swaps to the TV-off sprite.</summary>
        public void TurnTvOff() => SetTvSprite(on: false);

        /// <summary>Toggles the TV between on and off.</summary>
        public void ToggleTv() => SetTvSprite(!_tvIsOn);
        #endregion

        #region ---------------- Public: Alarm ----------------
        /// <summary>Triggers the default alarm pulse sequence.</summary>
        public void PlayAlarm() => StartCoroutine(RunPlayAlarm(string.Empty));

        /// <summary>Stops the current alarm loop/overlay immediately.</summary>
        public void StopAlarm()
        {
            _alarmToken++;
            SetAlarmOverlayColor(alarmIdleColor);
            if (alarmAudioSource != null)
            {
                alarmAudioSource.Stop();
            }

            SetText(alarmStatusLabel, "Alarm stopped");
        }
        #endregion

        #region ---------------- Public: Countdown ----------------
        /// <summary>Starts the default countdown demo.</summary>
        public void StartCountdown() => StartCoroutine(RunCountdown(string.Empty));

        /// <summary>Stops the active countdown and hides the HUD root.</summary>
        public void StopCountdown()
        {
            _countdownToken++;
            SetText(countdownLabel, string.Empty);
            SetText(countdownStatusLabel, "Countdown stopped");
            SetActive(countdownRoot, false);
        }
        #endregion

        #region ---------------- Action Routines ----------------
        private IEnumerator RunTurnOnTv(string payloadJson)
        {
            var payload = PayloadHelper.Parse(payloadJson, new TurnOnTvPayload());
            var token = ++_tvToken;

            SetTvSprite(payload.on);
            yield return WaitForSecondsSafe(0.1f);

            if (token != _tvToken)
                yield break;
        }

        private IEnumerator RunOpenDoor(string payloadJson)
        {
            var payload = PayloadHelper.Parse(payloadJson, new OpenDoorPayload());
            var duration = ResolveDoorDuration(payload.speed);
            yield return CoSlideDoor(payload.open, duration);
        }

        private IEnumerator RunPlayAlarm(string payloadJson)
        {
            var payload = PayloadHelper.Parse(payloadJson, new PlayAlarmPayload());
            var token = ++_alarmToken;
            var duration = Mathf.Max(0.25f, payload.duration);
            var pulseDuration = Mathf.Max(0.05f, alarmPulseDuration);

            SetText(alarmStatusLabel, $"Alarm: {payload.alarmId}");

            if (alarmAudioSource != null)
            {
                alarmAudioSource.loop = payload.loop;
                if (!alarmAudioSource.isPlaying)
                    alarmAudioSource.Play();
            }

            for (float elapsed = 0f; elapsed < duration; elapsed += GetDeltaTime())
            {
                if (token != _alarmToken)
                    yield break;

                ApplyAlarmOverlay(elapsed, pulseDuration);
                yield return null;
            }

            SetAlarmOverlayColor(alarmIdleColor);

            if (!payload.loop && alarmAudioSource != null)
                alarmAudioSource.Stop();

            SetText(alarmStatusLabel, payload.loop ? $"Alarm: {payload.alarmId} (looping)" : $"Alarm: {payload.alarmId} complete");
        }

        private IEnumerator RunCountdown(string payloadJson)
        {
            var payload = PayloadHelper.Parse(payloadJson, new StartCountdownPayload());
            var token = ++_countdownToken;
            var seconds = Mathf.Max(0, payload.seconds);

            SetActive(countdownRoot, payload.showOnHud);
            SetText(countdownStatusLabel, $"{payload.countdownId} countdown");

            for (int remaining = seconds; remaining >= 0; remaining--)
            {
                if (token != _countdownToken)
                    yield break;

                SetText(countdownLabel, remaining.ToString());
                yield return WaitForSecondsSafe(1f);
            }

            SetText(countdownStatusLabel, $"{payload.countdownId} complete");
            yield return WaitForSecondsSafe(0.5f);

            if (token == _countdownToken && payload.showOnHud)
                SetActive(countdownRoot, false);
        }

        private IEnumerator RunFadeLights(string payloadJson)
        {
            var payload = PayloadHelper.Parse(payloadJson, new FadeLightsPayload());
            var duration = Mathf.Max(0.05f, payload.duration);
            var targetAlpha = ResolveDimAlpha(payload.intensity);

            SetText(lightsStatusLabel, $"{payload.group} dim -> {Mathf.RoundToInt(targetAlpha * 100f)}%");
            yield return CoFadeDimmer(targetAlpha, duration);
        }
        #endregion

        #region ---------------- Helpers ----------------
        private void SetTvSprite(bool on)
        {
            _tvIsOn = on;

            if (tvImage == null)
                return;

            var target = on ? tvOnSprite : tvOffSprite;
            if (target != null)
                tvImage.sprite = target;
        }

        private IEnumerator CoSlideDoor(bool open, float duration)
        {
            var token = ++_doorToken;

            if (doorTransform == null)
            {
                _doorIsOpen = open;
                yield return WaitForSecondsSafe(duration);
                yield break;
            }

            var closedPos = _doorClosedLocalPosition;
            var openPos = closedPos + new Vector3(doorOpenDistance, 0f, 0f);
            var startPos = doorTransform.localPosition;
            var endPos = open ? openPos : closedPos;
            duration = Mathf.Max(0.05f, duration);

            for (float t = 0f; t < 1f; t += GetDeltaTime() / duration)
            {
                if (token != _doorToken)
                    yield break;

                doorTransform.localPosition = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            if (token != _doorToken)
                yield break;

            doorTransform.localPosition = endPos;
            _doorIsOpen = open;
        }

        private IEnumerator CoFadeDimmer(float targetAlpha, float duration)
        {
            var token = ++_lightsToken;
            duration = Mathf.Max(0.05f, duration);
            targetAlpha = Mathf.Clamp01(targetAlpha);

            if (sceneDimOverlay == null)
            {
                yield return WaitForSecondsSafe(duration);
                yield break;
            }

            sceneDimOverlay.gameObject.SetActive(true);

            var color = sceneDimOverlay.color;
            var startingAlpha = color.a;

            for (float t = 0f; t < 1f; t += GetDeltaTime() / duration)
            {
                if (token != _lightsToken)
                    yield break;

                color.a = Mathf.Lerp(startingAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, t));
                sceneDimOverlay.color = color;
                yield return null;
            }

            if (token != _lightsToken)
                yield break;

            color.a = targetAlpha;
            sceneDimOverlay.color = color;
        }

        private void ApplyAlarmOverlay(float elapsed, float pulseDuration)
        {
            if (alarmOverlay == null)
                return;

            var cycle = pulseDuration * 2f;
            var normalized = Mathf.Repeat(elapsed, cycle) / pulseDuration;
            if (normalized > 1f)
                normalized = 2f - normalized;

            SetAlarmOverlayColor(Color.Lerp(alarmIdleColor, alarmAlertColor, Mathf.Clamp01(normalized)));
        }

        private void SetAlarmOverlayColor(Color color)
        {
            if (alarmOverlay == null)
                return;

            alarmOverlay.gameObject.SetActive(true);
            alarmOverlay.color = color;
        }

        private float ResolveDoorDuration(string speed)
        {
            if (string.Equals(speed, "slow", StringComparison.OrdinalIgnoreCase))
                return doorSlowDuration;

            if (string.Equals(speed, "fast", StringComparison.OrdinalIgnoreCase))
                return doorFastDuration;

            return doorNormalDuration;
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        private IEnumerator WaitForSecondsSafe(float seconds)
        {
            if (seconds <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += GetDeltaTime();
                yield return null;
            }
        }

        private static float ResolveDimAlpha(float intensity)
        {
            if (intensity <= 1f)
                return Mathf.Clamp01(intensity);

            if (intensity <= 100f)
                return Mathf.Clamp01(intensity / 100f);

            return Mathf.Clamp01(intensity / 255f);
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null)
                target.SetActive(value);
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }

        #endregion
    }
}
