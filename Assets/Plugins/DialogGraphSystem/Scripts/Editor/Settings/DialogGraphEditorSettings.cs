using UnityEditor;
using UnityEngine;
using System;

namespace DialogSystem.EditorTools.Settings
{
    public enum EdgeLabelVisibility
    {
        Hidden,
        OnHover,
        OnSelection,
        Always
    }

    public static class DialogGraphEditorSettings
    {
        #region ---------------- Keys & Defaults ----------------

        private const string EdgeLabelVisibilityKey = "DialogGraphSystem_EdgeLabelVisibility";

        private const string MinimapVisibleKey = "DialogGraphSystem_MinimapVisible";

        private const string MinZoomKey = "DialogGraphSystem_MinZoom";
        private const string MaxZoomKey = "DialogGraphSystem_MaxZoom";

        /// <summary>Default minimum zoom level (matches the original hard-coded value).</summary>
        public const float DefaultMinZoom = 0.25f;

        /// <summary>Default maximum zoom level (matches the original hard-coded value).</summary>
        public const float DefaultMaxZoom = 1.0f;

        /// <summary>Absolute lower bound a user may set for MinZoom.</summary>
        public const float AbsoluteMinZoom = 0.1f;

        /// <summary>Absolute upper bound a user may set for MaxZoom.</summary>
        public const float AbsoluteMaxZoom = 3.0f;

        #endregion

        #region ---------------- Properties ----------------

        public static EdgeLabelVisibility EdgeLabelVisibility
        {
            get => (EdgeLabelVisibility)EditorPrefs.GetInt(EdgeLabelVisibilityKey, (int)EdgeLabelVisibility.OnHover);
            set => EditorPrefs.SetInt(EdgeLabelVisibilityKey, (int)value);
        }

        /// <summary>
        /// Whether the minimap overlay is shown on the graph canvas.
        /// Persists per-machine via EditorPrefs. Default is <c>true</c>.
        /// </summary>
        public static bool MinimapVisible
        {
            get => EditorPrefs.GetBool(MinimapVisibleKey, true);
            set
            {
                EditorPrefs.SetBool(MinimapVisibleKey, value);
                NotifySettingsChanged();
            }
        }

        /// <summary>
        /// Minimum zoom level for the graph canvas.
        /// Clamped to [0.1, 1.0] and guaranteed not to exceed <see cref="MaxZoom"/>.
        /// Default is 0.25.
        /// </summary>
        public static float MinZoom
        {
            get => EditorPrefs.GetFloat(MinZoomKey, DefaultMinZoom);
            set
            {
                float clamped = Mathf.Clamp(value, AbsoluteMinZoom, 1.0f);
                // Ensure Min never exceeds Max
                clamped = Mathf.Min(clamped, MaxZoom);
                EditorPrefs.SetFloat(MinZoomKey, clamped);
                NotifySettingsChanged();
            }
        }

        /// <summary>
        /// Maximum zoom level for the graph canvas.
        /// Clamped to [1.0, 3.0] and guaranteed not to fall below <see cref="MinZoom"/>.
        /// Default is 1.0.
        /// </summary>
        public static float MaxZoom
        {
            get => EditorPrefs.GetFloat(MaxZoomKey, DefaultMaxZoom);
            set
            {
                float clamped = Mathf.Clamp(value, 1.0f, AbsoluteMaxZoom);
                // Ensure Max never falls below Min
                clamped = Mathf.Max(clamped, MinZoom);
                EditorPrefs.SetFloat(MaxZoomKey, clamped);
                NotifySettingsChanged();
            }
        }

        #endregion

        #region ---------------- Events ----------------

        public static event Action OnSettingsChanged;

        /// <summary>Broadcasts a settings-changed notification to all subscribers.</summary>
        public static void NotifySettingsChanged()
        {
            OnSettingsChanged?.Invoke();
        }

        #endregion
    }
}
