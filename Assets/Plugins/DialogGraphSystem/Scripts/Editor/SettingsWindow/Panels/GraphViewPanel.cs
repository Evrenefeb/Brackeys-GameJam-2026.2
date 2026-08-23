using DialogSystem.EditorTools.Settings;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings.Panels
{
    /// <summary>
    /// Settings panel that exposes graph canvas zoom limits and minimap visibility.
    /// Values are persisted per-machine via EditorPrefs (not a ScriptableObject),
    /// so the masterSo parameter in BuildUI is intentionally unused.
    /// </summary>
    public class GraphViewPanel : BasePanel
    {
        private bool _dirty;

        public override void BuildUI(SerializedObject masterSo)
        {
            // masterSo is unused — all settings here live in EditorPrefs, not a ScriptableObject.
            SetPageHeader(
                "Graph View",
                "Controls the graph editor canvas — minimap visibility and zoom limits. Changes take effect immediately."
            );

            // ---- Minimap card ----
            var minimapCard = Card("Minimap");

            var minimapToggle = new Toggle("Show Minimap") { value = DialogGraphEditorSettings.MinimapVisible };
            minimapToggle.AddToClassList("dgs-row");
            minimapToggle.RegisterValueChangedCallback(evt =>
            {
                DialogGraphEditorSettings.MinimapVisible = evt.newValue;
                _dirty = true;
            });
            minimapCard.Add(minimapToggle);

            Add(minimapCard);

            var zoomCard = Card("Zoom Limits");

            // ---- Min Zoom row ----
            zoomCard.Add(BuildZoomSliderRow(
                label: "Min Zoom",
                sliderMin: DialogGraphEditorSettings.AbsoluteMinZoom,
                sliderMax: 1.0f,
                getter: () => DialogGraphEditorSettings.MinZoom,
                setter: v => { DialogGraphEditorSettings.MinZoom = v; _dirty = true; }
            ));

            // ---- Max Zoom row ----
            zoomCard.Add(BuildZoomSliderRow(
                label: "Max Zoom",
                sliderMin: 1.0f,
                sliderMax: DialogGraphEditorSettings.AbsoluteMaxZoom,
                getter: () => DialogGraphEditorSettings.MaxZoom,
                setter: v => { DialogGraphEditorSettings.MaxZoom = v; _dirty = true; }
            ));

            // ---- Hint ----
            var hint = new Label(
                $"Min: {DialogGraphEditorSettings.AbsoluteMinZoom:0.#}–1.0  ·  " +
                $"Max: 1.0–{DialogGraphEditorSettings.AbsoluteMaxZoom:0.#}  ·  " +
                $"Defaults: {DialogGraphEditorSettings.DefaultMinZoom:0.##} / {DialogGraphEditorSettings.DefaultMaxZoom:0.##}"
            );
            hint.AddToClassList("dgs-adv-hint");
            zoomCard.Add(hint);

            Add(zoomCard);

            // ---- Footer ----
            Add(FooterSaveWithDirty(
                isDirty: () => _dirty,
                onSave: () =>
                {
                    // All values are already written to EditorPrefs on every change.
                    // NotifySettingsChanged is called by each setter, so open graph views
                    // react immediately. Just clear the dirty flag here.
                    _dirty = false;
                }
            ));
        }

        /// <summary>
        /// Builds a labelled slider + float pill row that reads/writes to EditorPrefs
        /// via <paramref name="getter"/> / <paramref name="setter"/>.
        /// Matches the visual style of <see cref="DialogSettingsEditorUtils.SliderWithPill"/>.
        /// </summary>
        private static VisualElement BuildZoomSliderRow(
            string label,
            float sliderMin,
            float sliderMax,
            System.Func<float> getter,
            System.Action<float> setter)
        {
            var row = new VisualElement();
            row.AddToClassList("dgs-row");

            var labelEl = new Label(label);
            labelEl.AddToClassList("dgs-muted");
            row.Add(labelEl);

            float initial = getter();

            var slider = new Slider(sliderMin, sliderMax) { value = initial, showInputField = false };
            slider.AddToClassList("dgs-slider");

            var pill = new FloatField { value = initial };
            pill.AddToClassList("dgs-pill");
            pill.style.width = 72;
            pill.style.minWidth = 72;
            pill.style.maxWidth = 72;
            pill.style.flexGrow = 0;
            pill.style.flexShrink = 0;

            var pillInput = pill.Q(className: "unity-text-input");
            if (pillInput != null)
            {
                pillInput.style.width = 72;
                pillInput.style.minWidth = 72;
                pillInput.style.maxWidth = 72;
                pillInput.style.flexGrow = 0;
                pillInput.style.flexShrink = 0;
            }

            slider.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                // Re-read to display the cross-clamped value
                float corrected = getter();
                pill.SetValueWithoutNotify(corrected);
                slider.SetValueWithoutNotify(corrected);
            });

            pill.RegisterValueChangedCallback(evt =>
            {
                float clamped = Mathf.Clamp(evt.newValue, sliderMin, sliderMax);
                setter(clamped);
                float corrected = getter();
                slider.SetValueWithoutNotify(corrected);
                pill.SetValueWithoutNotify(corrected);
            });

            // Keep display in sync when external changes occur (e.g. the cross-clamp from the other slider)
            row.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                row.schedule.Execute(() =>
                {
                    float v = getter();
                    slider.SetValueWithoutNotify(v);
                    pill.SetValueWithoutNotify(v);
                }).Every(200);
            });

            row.Add(slider);
            row.Add(pill);
            return row;
        }
    }
}
