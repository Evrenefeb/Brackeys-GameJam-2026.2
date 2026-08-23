using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

namespace DialogSystem.Runtime.UI.Theming
{
    /// <summary>
    /// All sprite and color data for one visual theme of the dialog UI.
    /// Assign sprites from a DialogThemes/ resource folder, then reference
    /// this asset in <see cref="DialogUIManager"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DialogTheme_New",
        menuName  = "Beka Forge/Dialogues/UI Theme",
        order     = 10)]
    public class DialogThemeSO : ScriptableObject
    {
        // ─── Identity ───────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Display name shown in editors and debug logs.")]
        public string themeName = "Unnamed Theme";

        [TextArea(1, 2)]
        [Tooltip("Short description of the theme's visual style.")]
        public string themeDescription;

        // ─── Dialog Panel ────────────────────────────────────────────────────────

        [Header("Dialog Panel")]
        [Tooltip("Background frame of the main dialog box. Use 9-slice, border ~20 px.")]
        public Sprite dialogBox;

        [Tooltip("Background of the speaker name plate.")]
        public Sprite nameplate;

        [Tooltip("Border/frame around the character portrait.")]
        public Sprite avatarFrame;

        [FormerlySerializedAs("continueArrow")]
        [Tooltip("Icon sprite displayed on the skip button.")]
        public Sprite skipButtonIcon;

        [Tooltip("Background sprite displayed on the skip button.")]
        public Sprite skipButtonBackground;

        [Tooltip("Icon sprite displayed on the skip line button.")]
        public Sprite skipLineButtonIcon;

        [Tooltip("Background sprite displayed on the skip line button.")]
        public Sprite skipLineButtonBackground;

        // ─── Choice Buttons ──────────────────────────────────────────────────────

        [Header("Choice Buttons")]
        [Tooltip("Background sprite for an unselected / idle choice button.")]
        public Sprite choiceButtonNormal;

        [Tooltip("Background sprite for the currently selected / highlighted choice.")]
        public Sprite choiceButtonActive;

        [Tooltip("Small icon displayed on the left of every choice row (e.g. a chat-bubble glyph).")]
        public Sprite choiceMessageIcon;

        [Tooltip("Optional override prefab for choice rows. Leave empty to use the DialogUIManager/DialogUIController fallback prefab.")]
        public GameObject choiceButtonPrefabOverride;

        [Tooltip("Background sprite displayed behind the choice hotkey hint.")]
        public Sprite hotkeyBackgroundSprite;

        // ─── History Panel ───────────────────────────────────────────────────────

        [Header("History Panel")]
        [Tooltip("Background of the full history panel window. Use 9-slice, border ~20 px.")]
        public Sprite historyPanelBg;

        [Tooltip("Title bar strip at the top of the history panel.")]
        public Sprite historyTitleBg;

        [Tooltip("Row background for a character's spoken dialog line.")]
        public Sprite historyRowChar;

        [Tooltip("Row background for a player choice entry.")]
        public Sprite historyRowChoice;

        [Tooltip("Icon shown on history rows for player choices.")]
        public Sprite historyChoiceIcon;

        // ─── Buttons ─────────────────────────────────────────────────────────────

        [Header("Buttons")]
        [Tooltip("Background sprite for the history button.")]
        public Sprite historyButton;

        [Tooltip("Icon displayed on the history button.")]
        public Sprite historyButtonIcon;

        [Tooltip("The close / X button for the history panel.")]
        public Sprite closeButton;

        [Tooltip("Background sprite for the autoplay button.")]
        public Sprite autoPlayButtonBackground;

        [FormerlySerializedAs("autoPlayNormal")]
        [Tooltip("Autoplay button icon when playback is paused (play / idle state).")]
        public Sprite autoPlayNormalIcon;

        [FormerlySerializedAs("autoPlayActive")]
        [Tooltip("Autoplay button icon while autoplay is running (pause / active state).")]
        public Sprite autoPlayActiveIcon;

        [Tooltip("Background sprite for the Settings button.")]
        public Sprite settingsButton;

        [Tooltip("Icon displayed on the Settings button.")]
        public Sprite settingsButtonIcon;

        // ─── Text Colors ─────────────────────────────────────────────────────────

        [Header("Text Colors")]
        [Tooltip("Color of the main dialog text body.")]
        public Color dialogTextColor = Color.white;

        [Tooltip("Color of the speaker name label.")]
        public Color speakerNameColor = Color.white;

        [Tooltip("Color of choice button text in normal/idle state.")]
        public Color choiceNormalColor = Color.white;

        [Tooltip("Color of choice button text when selected/highlighted.")]
        public Color choiceActiveColor = Color.white;

        [Tooltip("Color of speaker name text in history rows.")]
        public Color historySpeakerColor = Color.white;

        [Tooltip("Color of dialog line text in history rows (character lines).")]
        public Color historyCharTextColor = Color.white;

        [Tooltip("Color of choice text in history rows.")]
        public Color historyChoiceTextColor = new Color(0.78f, 0.78f, 0.78f, 1f);

        [Tooltip("Color of the label text on the skip / skip-all button.")]
        public Color skipAllTextColor = Color.white;

        [Tooltip("Color of the title label inside the history panel header bar.")]
        public Color historyTitleTextColor = Color.white;

        [Header("Fonts")]
        [Tooltip("Fallback font used for all dialog UI text when no role-specific font is assigned.")]
        public TMP_FontAsset defaultFont;

        [Tooltip("Optional font override for the speaker name label.")]
        public TMP_FontAsset speakerNameFont;

        [Tooltip("Optional font override for the main dialog body text.")]
        public TMP_FontAsset dialogTextFont;

        [Tooltip("Optional font override for choice row text.")]
        public TMP_FontAsset choiceTextFont;

        [Tooltip("Optional font override for choice hotkey hint text.")]
        public TMP_FontAsset choiceHotkeyFont;

        [Tooltip("Optional font override for history speaker labels.")]
        public TMP_FontAsset historySpeakerFont;

        [Tooltip("Optional font override for history line text.")]
        public TMP_FontAsset historyLineFont;
    }
}