using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.EditorTools.Resources;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings.Panels
{
    /// <summary>
    /// Editor panel that exposes all fields of <see cref="DialogueRuntimeUISettings"/>.
    /// Appears as the "Runtime UI" entry in the settings side-navigation.
    /// Changes are saved to the sub-asset (packaged defaults); per-player overrides
    /// live in PlayerPrefs and are managed by <c>DialogRuntimeSettingsPersistence</c>.
    /// </summary>
    public class RuntimeUIPanel : BasePanel
    {
        /// <inheritdoc/>
        public override void BuildUI(SerializedObject masterSo)
        {
            SetPageHeader(
                "Runtime UI",
                "Default visibility for dialogue UI elements. Players can override audio and flow settings at runtime.",
                DialogGraphIconId.ToolbarSettings
            );

            var uiProp = masterSo.FindProperty("uiSettings");
            if (uiProp == null || uiProp.objectReferenceValue == null)
            {
                var missing = new Label("RuntimeUISettings sub-asset is missing. Re-open the settings window to regenerate it.");
                missing.AddToClassList("dgs-muted");
                Add(missing);
                return;
            }

            var uiSo = new SerializedObject((DialogueRuntimeUISettings)uiProp.objectReferenceValue);

            // ---- Global Visibility ----
            var globalCard = Card("Global Visibility");
            globalCard.Add(ToggleRow(uiSo, "showBackgroundPanel", "Show Background Panel"));
            globalCard.Bind(uiSo);
            Add(globalCard);

            // ---- Header / Meta ----
            var headerCard = Card("Header / Meta");
            headerCard.Add(ToggleRow(uiSo, "showSpeakerName", "Show Speaker Name"));
            headerCard.Add(ToggleRow(uiSo, "showPortrait",    "Show Portrait"));
            headerCard.Bind(uiSo);
            Add(headerCard);

            // ---- Controls ----
            var controlsCard = Card("Controls");
            controlsCard.Add(ToggleRow(uiSo, "showSkipButton",     "Show Skip Button"));
            controlsCard.Add(ToggleRow(uiSo, "showAutoButton",     "Show Auto Button"));
            controlsCard.Add(ToggleRow(uiSo, "showHistoryButton",  "Show History Button"));
            controlsCard.Add(ToggleRow(uiSo, "showLanguageButton", "Show Language Button"));
            controlsCard.Bind(uiSo);
            Add(controlsCard);

            // ---- Indicators ----
            var indicatorsCard = Card("Indicators");
            indicatorsCard.Add(ToggleRow(uiSo, "showContinueIndicator", "Show Continue Indicator"));
            indicatorsCard.Add(ToggleRow(uiSo, "showAutoSkipIcon",      "Show Auto/Skip Icon"));
            indicatorsCard.Bind(uiSo);
            Add(indicatorsCard);

            // ---- Panels ----
            var panelsCard = Card("Panels");
            panelsCard.Add(ToggleRow(uiSo, "showChoicePanel",  "Show Choice Panel"));
            panelsCard.Add(ToggleRow(uiSo, "showHistoryPanel", "Show History Panel"));
            panelsCard.Bind(uiSo);
            Add(panelsCard);

            // ---- Runtime Settings Panel ----
            var settingsPanelCard = Card("Runtime Settings Panel");
            settingsPanelCard.Add(SettingsPropertyField(uiSo, "settingsTheme", "Settings Theme"));
            settingsPanelCard.Add(ToggleRow(uiSo, "showSettingsPanel", "Show Settings Panel"));
            settingsPanelCard.Add(ToggleRow(uiSo, "showSettingsLanguageDropdown", "Show Language Selector"));
            settingsPanelCard.Add(ToggleRow(uiSo, "showSettingsThemeDropdown", "Show Theme Selector"));
            settingsPanelCard.Add(ToggleRow(uiSo, "showSettingsTextSpeed", "Show Text Speed"));
            settingsPanelCard.Add(ToggleRow(uiSo, "showSettingsAutoAdvance", "Show Auto-Advance"));
            settingsPanelCard.Add(ToggleRow(uiSo, "showSettingsVoiceVolume", "Show Voice Volume"));
            settingsPanelCard.Add(ToggleRow(uiSo, "showSettingsSfxVolume", "Show SFX Volume"));
            settingsPanelCard.Bind(uiSo);
            Add(settingsPanelCard);

            // ---- Footer ----
            Add(FooterSaveWithDirty(
                isDirty: () => EditorUtility.IsDirty(uiSo.targetObject),
                onSave: () =>
                {
                    uiSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(uiSo.targetObject);
                    AssetDatabase.SaveAssets();
                }
            ));
        }
    }
}
