using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.UI;
using DialogSystem.Runtime.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime.Core
{
    /// <summary>
    /// Tracks runtime choice overlay selection, highlighting, and keyboard/gamepad input.
    /// </summary>
    internal sealed class DialogChoiceNavigationController
    {
        private readonly List<ChoiceButtonView> choiceViews = new();
        private DialogChoiceSettings choiceSettings;
        private int selectedChoiceIndex = -1;

        public int ChoiceCount => choiceViews.Count;

        public void Bind(DialogChoiceSettings settings)
        {
            choiceSettings = settings;
        }

        public void Reset()
        {
            choiceViews.Clear();
            selectedChoiceIndex = -1;
        }

        public void CacheViewsFrom(Transform choicesContainer)
        {
            choiceViews.Clear();

            if (choicesContainer == null)
                return;

            for (int i = 0; i < choicesContainer.childCount; i++)
            {
                var view = choicesContainer.GetChild(i).GetComponent<ChoiceButtonView>();
                if (view != null)
                    choiceViews.Add(view);
            }

            selectedChoiceIndex = ShouldSelectFirstChoiceOnEnable() && choiceViews.Count > 0 ? 0 : -1;
            ApplyHighlight();
        }

        public bool IsOverlayActive(Transform choicesContainer)
        {
            if (choicesContainer == null) return false;
            return choicesContainer.gameObject.activeInHierarchy && choiceViews.Count > 0;
        }

        public void SelectChoiceIndex(int index)
        {
            if (index < 0 || index >= choiceViews.Count) return;
            selectedChoiceIndex = index;
            ApplyHighlight();
        }

        public bool HandleNavigation(Action<int> onChoiceSelected)
        {
            if (InputHelper.WasMoveDownPressedThisFrame())
            {
                MoveChoice(+1);
                ApplyHighlight();
                return true;
            }

            if (InputHelper.WasMoveUpPressedThisFrame())
            {
                MoveChoice(-1);
                ApplyHighlight();
                return true;
            }

            for (int n = 1; n <= 9; n++)
            {
                if (InputHelper.WasNumberKeyPressedThisFrame(n))
                {
                    onChoiceSelected?.Invoke(n - 1);
                    return true;
                }
            }

            if (!IsChoiceConfirmPressedThisFrame())
                return false;

            int pick = selectedChoiceIndex;
            if (pick < 0 && choiceViews.Count > 0) pick = 0;
            if (pick >= 0 && pick < choiceViews.Count)
            {
                onChoiceSelected?.Invoke(pick);
                return true;
            }

            return false;
        }

        private void MoveChoice(int delta)
        {
            int count = choiceViews.Count;
            if (count == 0) return;

            if (selectedChoiceIndex < 0)
            {
                selectedChoiceIndex = 0;
                return;
            }

            int next = selectedChoiceIndex + delta;
            if (ShouldWrapChoiceNavigation())
            {
                if (next < 0) next = count - 1;
                if (next >= count) next = 0;
            }
            else
            {
                next = Mathf.Clamp(next, 0, count - 1);
            }

            selectedChoiceIndex = next;
        }

        private void ApplyHighlight()
        {
            string hint = GetChoiceConfirmHint();

            for (int i = 0; i < choiceViews.Count; i++)
            {
                if (choiceViews[i] != null)
                    choiceViews[i].ApplySelected(i == selectedChoiceIndex, hint);
            }
        }

        private bool ShouldSelectFirstChoiceOnEnable()
        {
            return choiceSettings == null || choiceSettings.selectFirstOnEnable;
        }

        private bool ShouldWrapChoiceNavigation()
        {
            return choiceSettings == null || choiceSettings.wrapNavigation;
        }

        private string GetChoiceConfirmHint()
        {
            if (choiceSettings == null || !choiceSettings.showKeyHints || !choiceSettings.enableKeyboardConfirmKey)
                return string.Empty;

            return TryGetChoiceConfirmKey(out var confirmKey) ? DialogInputSettings.FormatKeyCode(confirmKey) : string.Empty;
        }

        private bool IsChoiceConfirmPressedThisFrame()
        {
            if (choiceSettings == null)
                return InputHelper.WasSubmitPressedThisFrame();

            if (choiceSettings.alsoAcceptSubmit && InputHelper.WasSubmitPressedThisFrame())
                return true;

            if (choiceSettings.acceptGamepadConfirm && InputHelper.WasGamepadConfirmPressedThisFrame())
                return true;

            if (choiceSettings.acceptXRSelect && InputHelper.WasXRConfirmPressedThisFrame())
                return true;

            return choiceSettings.enableKeyboardConfirmKey &&
                   TryGetChoiceConfirmKey(out var confirmKey) &&
                   InputHelper.WasKeyPressedThisFrame(confirmKey);
        }

        private bool TryGetChoiceConfirmKey(out KeyCode confirmKey)
        {
            confirmKey = KeyCode.F;

            if (choiceSettings == null)
                return true;

            return TryNormalizeChoiceConfirmKey(choiceSettings.keyboardConfirmLetter, out confirmKey) ||
                   TryNormalizeChoiceConfirmKey(choiceSettings.keyboardConfirm, out confirmKey) ||
                   TryNormalizeChoiceConfirmKey(choiceSettings.altConfirm, out confirmKey);
        }

        private static bool TryNormalizeChoiceConfirmKey(string rawValue, out KeyCode confirmKey)
        {
            confirmKey = KeyCode.None;

            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            var value = rawValue.Trim();
            if (Enum.TryParse(value, true, out confirmKey) && confirmKey != KeyCode.None)
            {
                return true;
            }

            if (value.Length != 1)
            {
                confirmKey = value.ToUpperInvariant() switch
                {
                    "ENTER" => KeyCode.Return,
                    "ESC" => KeyCode.Escape,
                    "CTRL" => KeyCode.LeftControl,
                    "CONTROL" => KeyCode.LeftControl,
                    "SHIFT" => KeyCode.LeftShift,
                    "ALT" => KeyCode.LeftAlt,
                    _ => KeyCode.None
                };

                return confirmKey != KeyCode.None;
            }

            char normalized = char.ToUpperInvariant(value[0]);
            if (normalized >= 'A' && normalized <= 'Z')
            {
                confirmKey = (KeyCode)((int)KeyCode.A + (normalized - 'A'));
                return true;
            }

            if (normalized >= '0' && normalized <= '9')
            {
                confirmKey = (KeyCode)((int)KeyCode.Alpha0 + (normalized - '0'));
                return true;
            }

            return false;
        }
    }
}
