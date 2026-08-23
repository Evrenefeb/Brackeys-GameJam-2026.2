using DialogSystem.Runtime.Settings.Panels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using DialogSystem.EditorTools.Resources;
using static DialogSystem.EditorTools.Settings.DialogSettingsEditorUtils;

namespace DialogSystem.EditorTools.Settings.Panels
{
    public class ChoicePanel : BasePanel
    {
        [SerializeField] private bool doDebug = false;

        public override void BuildUI(SerializedObject masterSo)
        {
            if (masterSo == null || masterSo.targetObject == null)
            {
                Add(ErrorCard("Settings not loaded",
                    "Master settings object is null. Reopen the window or reimport the package."));
                return;
            }

            SetPageHeader(
                "Choice Selection",
                "Configure how players interact with branching paths — navigation wrap, hold repeat, visual feedback, and hotkey hints.",
                DialogGraphIconId.NodeChoice
            );

            var choiceProp = masterSo.FindProperty("choiceSettings");
            if (choiceProp == null)
            {
                Add(ErrorCard("Missing field",
                    "Field 'choiceSettings' was not found on DialogSystemSettings. Check the asset/script definitions."));
                return;
            }

            if (choiceProp.objectReferenceValue == null)
            {
                if (doDebug) Debug.Log("[ChoicePanel] 'choiceSettings' is null. Creating a new DialogChoiceSettings sub-asset.");

                var master = masterSo.targetObject as DialogSystemSettings;
                if (master == null)
                {
                    Add(ErrorCard("Master cast failed",
                        "Could not cast masterSo.targetObject to DialogSystemSettings."));
                    return;
                }

                var created = ScriptableObject.CreateInstance<DialogChoiceSettings>();
                created.name = "ChoiceSettings";
                AssetDatabase.AddObjectToAsset(created, master);
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(master));

                choiceProp.objectReferenceValue = created;
                masterSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(master);
                EditorUtility.SetDirty(created);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var choiceObj = choiceProp.objectReferenceValue as DialogChoiceSettings;
            if (choiceObj == null)
            {
                Add(ErrorCard("Choice settings invalid",
                    "The 'choiceSettings' reference exists but isn't a DialogChoiceSettings. Re-create it."));
                return;
            }

            var choiceSo = new SerializedObject(choiceObj);

            // ---------------- Navigation ----------------
            var nav = Card("Navigation");
            nav.Add(ToggleRow(choiceSo, "wrapNavigation", "Wrap Navigation"));
            nav.Add(AdvancedSliderWithValue(
                choiceSo,
                "holdRepeatDelay",
                0.05f, 0.5f,
                "Hold Repeat Delay (s)",
                new Vector2(0.10f, 0.25f)
            ));
            nav.Add(ToggleRow(choiceSo, "selectFirstOnEnable", "Select First On Enable"));
            nav.Add(ToggleRow(choiceSo, "mouseHoverMovesSelection", "Mouse Hover Moves Selection"));
            Add(nav);

            // ---------------- Visuals ----------------
            var vis = Card("Visuals");
            vis.Add(new PropertyField(choiceSo.FindProperty("selectedOutlineColor"), "Selected Outline Color"));
            vis.Add(AdvancedSliderWithValue(
                choiceSo,
                "outlineThickness",
                0.5f, 10f,
                "Outline Thickness",
                new Vector2(1f, 3f)
            ));
            vis.Add(ToggleRow(choiceSo, "animateSelected", "Animate Selected"));
            vis.Add(AdvancedSliderWithValue(
                choiceSo,
                "animatePulseScale",
                1.0f, 1.2f,
                "Pulse Scale",
                new Vector2(1.03f, 1.10f)
            ));
            vis.Add(AdvancedSliderWithValue(
                choiceSo,
                "animatePulseSpeed",
                0.25f, 3.0f,
                "Pulse Speed",
                new Vector2(0.75f, 1.50f)
            ));
            Add(vis);

            // ---------------- Hints & Confirm ----------------
            var hints = Card("Key Hints & Confirm");
            hints.Add(ToggleRow(choiceSo, "showKeyHints", "Show Key Hints"));
            hints.Add(ToggleRow(choiceSo, "enableKeyboardConfirmKey", "Enable Keyboard Confirm Key"));
            hints.Add(StringKeyCaptureRow(
                choiceSo,
                "keyboardConfirmLetter",
                "Choice Confirm Key",
                KeyCode.F,
                "Click the binding, then press any key or mouse button."
            ));
            hints.Add(ToggleRow(choiceSo, "alsoAcceptSubmit", "Also Accept Submit (Enter/Space)"));
            hints.Add(ToggleRow(choiceSo, "acceptGamepadConfirm", "Accept Gamepad Confirm"));
            hints.Add(ToggleRow(choiceSo, "acceptXRSelect", "Accept XR Select"));
            Add(hints);

            // ---------------- Footer Save ----------------
            Add(FooterSaveWithDirty(
                isDirty: () => EditorUtility.IsDirty(choiceSo.targetObject),
                onSave: () =>
                {
                    choiceSo.ApplyModifiedProperties();
                    EditorUtility.SetDirty(choiceSo.targetObject);
                    AssetDatabase.SaveAssets();
                }
            ));
        }

        private VisualElement ErrorCard(string title, string message)
        {
            var card = Card(title);
            var lbl = new Label(message) { style = { whiteSpace = WhiteSpace.Normal } };
            card.Add(lbl);
            return card;
        }
    }
}
