// Copyright (c) 2025 PinePie. All rights reserved.

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace PinePie.PieTabs
{
    public static partial class BrowserTabs
    {
        // placeholder helper
        public static int PlaceholderNeedleAtPos(WinInstanceInfo info, float mouseX)
        {
            VisualElement area = info.uiState.shortcutButtonArea;

            bool containsPlaceholder = area.Contains(info.uiState.placeholderNeedle);

            // getting drop index
            int dropIndex = area.childCount;
            for (int i = area.childCount - 1; i >= 0; i--)
            {
                var child = area[i];

                if (containsPlaceholder && child == info.uiState.placeholderNeedle) continue;

                if (true)
                {
                    if (mouseX > child.layout.center.x) dropIndex--;
                    else break;
                }
            }
            if (containsPlaceholder) dropIndex--;

            if (dropIndex != info.uiState.placeHolderIndex)
            {
                info.uiState.placeHolderIndex = dropIndex;

                info.uiState.placeholderNeedle.RemoveFromHierarchy();
                area.Insert(dropIndex, info.uiState.placeholderNeedle);
            }

            return dropIndex;
        }

        // button setup 
        public static void SetupButtonProperties(VisualElement button, string Label, string color, bool isMinimal = true)
        {
            // color and tooltip
            Color col = GlobalUtils.HexToColor(color);

            button.style.backgroundColor = col;
            button.tooltip = isMinimal ? Label : null;

            // label props
            var labelElement = button.Q<Label>("buttonLabel");
            if (labelElement != null)
            {
                labelElement.text = isMinimal ? "" : Label;

                labelElement.style.color = GlobalUtils.IsColorDark(col)
                ? GlobalUtils.HexToColor("#f7f7f7")
                : GlobalUtils.HexToColor("#2e2e2e");
            }
        }

    }
}

#endif