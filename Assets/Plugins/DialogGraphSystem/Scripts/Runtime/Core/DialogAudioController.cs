using DialogSystem.Runtime.Settings.Panels;
using System.Collections;
using UnityEngine;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Handles runtime dialog line audio playback, stopping, and fade-out behavior.
    /// </summary>
    internal sealed class DialogAudioController
    {
        private readonly MonoBehaviour coroutineHost;
        private AudioSource audioSource;
        private DialogAudioSettings localAudioSettings;
        private DialogAudioSettings audioSettings;
        private Coroutine audioFadeCoroutine;

        public DialogAudioController(MonoBehaviour coroutineHost)
        {
            this.coroutineHost = coroutineHost;
        }

        public void Bind(AudioSource source, DialogAudioSettings localSettings, DialogAudioSettings globalSettings)
        {
            audioSource = source;
            localAudioSettings = localSettings;
            audioSettings = globalSettings;
        }

        public void ApplyDefaultVolume()
        {
            var settings = EffectiveSettings;
            if (audioSource != null && settings != null)
            {
                audioSource.volume = settings.voiceVolume;
            }
        }

        public void PlayLineAudio(AudioClip clip, bool doDebug, string nodeName)
        {
            if (audioSource == null) return;

            if (doDebug)
                Debug.Log($"[DialogManager] Play audio: {(clip ? clip.name : "null")} for node {(nodeName ?? "n/a")}");

            StopAudioImmediate();
            if (clip == null) return;

            var settings = EffectiveSettings;
            if (settings != null)
            {
                audioSource.volume = settings.voiceVolume;
            }

            audioSource.clip = clip;
            audioSource.time = 0f;
            audioSource.Play();
        }

        public void StopAudio(bool withFade)
        {
            if (audioSource == null || !audioSource.isPlaying) return;

            float fadeTime = FadeOutTime();
            if (!withFade || fadeTime <= 0f)
            {
                StopAudioImmediate();
                return;
            }

            CancelFade();
            audioFadeCoroutine = coroutineHost.StartCoroutine(FadeOutAudio(fadeTime));
        }

        public void StopAudioImmediate()
        {
            CancelFade();

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
                var settings = EffectiveSettings;
                if (settings != null) audioSource.volume = settings.voiceVolume;
            }
        }

        public void CancelFade()
        {
            if (audioFadeCoroutine != null)
            {
                coroutineHost.StopCoroutine(audioFadeCoroutine);
                audioFadeCoroutine = null;
            }
        }

        public bool ShouldStopOnSkipLine()
        {
            return localAudioSettings != null
                ? localAudioSettings.stopOnSkipLine
                : audioSettings?.stopOnSkipLine ?? true;
        }

        public bool ShouldStopOnSkipAll()
        {
            return localAudioSettings != null
                ? localAudioSettings.stopOnSkipAll
                : audioSettings?.stopOnSkipAll ?? true;
        }

        public bool ShouldFadeOutOnStop()
        {
            return localAudioSettings != null
                ? localAudioSettings.fadeOutOnStop
                : audioSettings?.fadeOutOnStop ?? true;
        }

        public float FadeOutTime()
        {
            return localAudioSettings != null
                ? localAudioSettings.fadeOutTime
                : audioSettings?.fadeOutTime ?? 0.08f;
        }

        private DialogAudioSettings EffectiveSettings => localAudioSettings != null ? localAudioSettings : audioSettings;

        private IEnumerator FadeOutAudio(float duration)
        {
            if (audioSource == null || !audioSource.isPlaying)
            {
                StopAudioImmediate();
                yield break;
            }

            float startVol = audioSource.volume;
            float t = 0f;

            while (t < duration && audioSource != null && audioSource.isPlaying)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                audioSource.volume = startVol * k;
                yield return null;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
                audioSource.volume = startVol;
            }

            audioFadeCoroutine = null;
        }
    }
}
