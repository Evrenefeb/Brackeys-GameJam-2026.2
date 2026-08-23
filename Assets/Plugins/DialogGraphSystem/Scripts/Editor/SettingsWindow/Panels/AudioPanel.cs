using DialogSystem.Runtime.Settings.Panels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.Resources;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings.Panels
{
    public class AudioPanel : BasePanel
    {
        public override void BuildUI(SerializedObject masterSo)
        {
            SetPageHeader(
                "Audio",
                "Volume levels, UI sound effects, and per-character typewriter audio.",
                DialogGraphIconId.ToolbarSettings
            );

            var audioProp = masterSo.FindProperty("audioSettings");
            var audioObj = (DialogAudioSettings)audioProp.objectReferenceValue;
            var audioSo = new SerializedObject(audioObj);

            // ---- Volumes ----
            var vol = Card("Volumes");
            vol.Add(AdvancedSliderWithValue(audioSo, "voiceVolume", 0f, 1f, "Voice Volume", new Vector2(0.5f, 0.8f)));
            vol.Add(AdvancedSliderWithValue(audioSo, "sfxVolume", 0f, 1f, "SFX Volume", new Vector2(0.5f, 0.8f)));
            vol.Bind(audioSo);
            Add(vol);

            // ---- UI SFX ----
            var sfx = Card("UI SFX");
            sfx.Add(ToggleRow(audioSo, "enableUiSfx", "Enable UI SFX"));
            sfx.Add(new PropertyField(audioSo.FindProperty("sfxNavigate"), "Navigate SFX"));
            sfx.Add(new PropertyField(audioSo.FindProperty("sfxConfirm"), "Confirm SFX"));
            sfx.Add(new PropertyField(audioSo.FindProperty("sfxSkip"), "Skip SFX"));
            sfx.Bind(audioSo);
            Add(sfx);

            // ---- Typewriter Audio ----
            var tw = Card("Typewriter Audio");
            tw.Add(ToggleRow(audioSo, "enableTypewriterAudio", "Enable Typewriter Audio"));
            tw.Add(SettingsPropertyField(audioSo, "typewriterClip", "Clip"));

            tw.Add(AdvancedSliderWithValue(audioSo, "typewriterVolume", 0f, 1f, "Volume", new Vector2(0.3f, 0.6f)));
            tw.Add(AdvancedSliderWithValue(audioSo, "typewriterPitchVariance", 0f, 0.5f, "Pitch Variance", new Vector2(0.02f, 0.1f)));
            tw.Add(SettingsPropertyField(audioSo, "typewriterPlayEveryNChars", "Play Every N Chars"));
            tw.Add(SettingsPropertyField(audioSo, "typewriterMinInterval", "Min Interval (s)"));

            tw.Add(ToggleRow(audioSo, "typewriterIgnoreWhitespace", "Ignore Whitespace"));
            tw.Add(ToggleRow(audioSo, "typewriterIgnoreRichTextTags", "Ignore Rich Text Tags"));
            tw.Bind(audioSo);
            Add(tw);

            // ---- Footer ----
            Add(FooterSaveWithDirty(
                isDirty: () => EditorUtility.IsDirty(audioSo.targetObject),
                onSave: () =>
                {
                    audioSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(audioSo.targetObject);
                    AssetDatabase.SaveAssets();
                }
            ));
        }
    }
}