using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DialogSystem.Runtime.Actions
{
    /// <summary>
    /// Fades a full-screen dark overlay in and out to simulate scene lighting changes.
    ///
    /// Why not use the Directional Light?
    ///   Unity's default sprite shader (Sprites/Default) is unlit — it ignores all
    ///   scene lights by design. The reliable cross-pipeline solution is a screen overlay.
    ///
    /// Setup:
    ///   1. Create a Canvas (Screen Space – Overlay, sort order above your scene canvas).
    ///   2. Add a child Image that fills the canvas (Anchor: stretch/stretch, all offsets 0).
    ///   3. Set the Image color to black (or any tint), alpha = 0.
    ///   4. Set the Image's Raycast Target to OFF so it doesn't block input.
    ///   5. Drag that Image into the Overlay Image field on this component.
    ///
    /// Public API (call from UnityEvents, DialogDemoActionShowcase, or any script):
    ///   DimToBlack()      — fades to fully dark
    ///   Restore()         — fades back to clear
    ///   DimTo(float t)    — fades to an arbitrary darkness (0 = clear, 1 = full black)
    ///   ToggleDim()       — flips between dark and clear
    ///   StopFade()        — immediately stops any in-progress fade
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dialogue Graph System/Demos/Scene Dimmer")]
    public class SceneDimmer : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────────────

        [Header("Overlay")]
        [Tooltip("Full-screen Image used as the dark overlay. Set its alpha to 0 in the editor.")]
        [SerializeField] private Image overlayImage;

        [Tooltip("Color of the overlay (default black). Alpha is controlled at runtime.")]
        [SerializeField] private Color overlayColor = Color.black;

        [Header("Durations (seconds)")]
        [SerializeField] private float dimDuration     = 1.2f;
        [SerializeField] private float restoreDuration = 1.0f;

        [Header("Timing")]
        [SerializeField] private bool useUnscaledTime;

        // ── State ────────────────────────────────────────────────────────────────────

        private Coroutine _fadeRoutine;
        private bool      _isDimmed;

        // ── Unity ────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (overlayImage != null)
            {
                overlayImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);
                overlayImage.raycastTarget = false;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────────

        /// <summary>Fades the overlay to full black over <see cref="dimDuration"/> seconds.</summary>
        public void DimToBlack() => FadeTo(1f, dimDuration);

        /// <summary>Fades the overlay back to transparent over <see cref="restoreDuration"/> seconds.</summary>
        public void Restore() => FadeTo(0f, restoreDuration);

        /// <summary>Fades to a specific darkness level (0 = clear, 1 = full black).</summary>
        public void DimToHalf() => FadeTo(0.5f, dimDuration);

        /// <summary>
        /// Fades to a custom target alpha over a custom duration.
        /// Safe to call from code — not directly from a UnityEvent (use the named overloads there).
        /// </summary>
        public void FadeTo(float targetAlpha, float duration)
        {
            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            _fadeRoutine = StartCoroutine(CoFade(targetAlpha, duration));
        }

        /// <summary>Toggles: dims if currently clear, restores if currently dimmed.</summary>
        public void ToggleDim()
        {
            if (_isDimmed) Restore();
            else DimToBlack();
        }

        /// <summary>Immediately snaps the overlay to fully dark — no fade.</summary>
        public void SnapToDark()
        {
            StopFade();
            SetAlpha(1f);
            _isDimmed = true;
        }

        /// <summary>Immediately snaps the overlay to transparent — no fade.</summary>
        public void SnapToClear()
        {
            StopFade();
            SetAlpha(0f);
            _isDimmed = false;
        }

        /// <summary>Stops any in-progress fade, leaving the overlay at its current alpha.</summary>
        public void StopFade()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }
        }

        // ── Coroutine ────────────────────────────────────────────────────────────────

        private IEnumerator CoFade(float target, float duration)
        {
            target   = Mathf.Clamp01(target);
            duration = Mathf.Max(0.02f, duration);

            float start   = GetCurrentAlpha();
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += GetDelta();
                float t  = Mathf.Clamp01(elapsed / duration);
                SetAlpha(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }

            SetAlpha(target);
            _isDimmed    = target > 0.01f;
            _fadeRoutine = null;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private void SetAlpha(float a)
        {
            if (overlayImage == null) return;
            var c = overlayColor;
            c.a = a;
            overlayImage.color = c;
        }

        private float GetCurrentAlpha() =>
            overlayImage != null ? overlayImage.color.a : 0f;

        private float GetDelta() =>
            useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
