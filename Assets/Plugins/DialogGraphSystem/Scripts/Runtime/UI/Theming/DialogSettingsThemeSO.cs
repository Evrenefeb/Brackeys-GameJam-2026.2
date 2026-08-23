using TMPro;
using UnityEngine;

namespace DialogSystem.Runtime.UI.Theming
{
    /// <summary>
    /// Visual theme for the runtime settings overlay. This is intentionally separate
    /// from <see cref="DialogThemeSO"/> so games can keep one settings style while
    /// dialogue themes change per scene, location, or speaker context.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DialogSettingsTheme_New",
        menuName = "Beka Forge/Dialogues/Settings UI Theme",
        order = 11)]
    public class DialogSettingsThemeSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in editors and debug logs.")]
        public string themeName = "Settings Theme";

        [TextArea(1, 2)]
        [Tooltip("Short description of the settings overlay style.")]
        public string themeDescription;

        [Header("Sprites")]
        [Tooltip("Optional full-screen overlay or panel background sprite for the runtime settings panel.")]
        public Sprite panelBackground;

        [Tooltip("Optional header/background sprite for the runtime settings panel title area.")]
        public Sprite headerBackground;

        [Tooltip("Optional row background sprite for individual settings rows.")]
        public Sprite rowBackground;

        [Tooltip("Optional background sprite for dropdowns, sliders, and value chips.")]
        public Sprite controlBackground;

        [Tooltip("Optional close button sprite for the runtime settings panel.")]
        public Sprite closeButton;

        [Header("Colors")]
        [Tooltip("Optional tint for the runtime settings panel background. Leave alpha at 0 to keep the prefab color.")]
        public Color panelTint = Color.clear;

        [Tooltip("Optional tint for the runtime settings panel header. Leave alpha at 0 to keep the prefab color.")]
        public Color headerTint = Color.clear;

        [Tooltip("Optional tint for runtime settings rows. Leave alpha at 0 to keep the prefab color.")]
        public Color rowTint = Color.clear;

        [Tooltip("Optional tint for settings controls such as dropdowns, sliders, handles, and value chips. Leave alpha at 0 to keep the prefab color.")]
        public Color controlTint = Color.clear;

        [Tooltip("Optional accent tint for slider fills and active control parts. Leave alpha at 0 to keep the prefab color.")]
        public Color accentColor = Color.clear;

        [Tooltip("Optional primary text color for labels in the runtime settings panel. Leave alpha at 0 to keep the prefab color.")]
        public Color textColor = Color.clear;

        [Tooltip("Optional value text color for runtime settings value labels. Leave alpha at 0 to keep the prefab color.")]
        public Color valueTextColor = Color.clear;

        [Header("Typography")]
        [Tooltip("Optional font override for all runtime settings panel text.")]
        public TMP_FontAsset font;
    }
}
