// Copyright (c) 2025 PinePie. All rights reserved.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace PinePie.PieTabs
{
    public static partial class BrowserTabs
    {
        public static void SetupDragAreaStyling(WinInstanceInfo info)
        {
            // foreach (var view in new VisualElement[] { info.uiState.shortcutButtonArea})
            // {
            // }
            info.uiState.shortcutButtonArea.contentContainer.style.flexDirection = FlexDirection.RowReverse;
            info.uiState.shortcutButtonArea.contentContainer.style.justifyContent = Justify.FlexStart;
        }

        // public static void SetupSplitter(WinInstanceInfo info)
        // {
        //     VisualElement splitter = info.mainUI.Q<VisualElement>("splitter");

        //     bool isDragging = false;
        //     int pointerId = -1;
        //     float distFromMouse = 0;

        //     info.uiState.shortcutButtonArea.style.flexGrow = 0;
        //     info.uiState.shortcutButtonArea.style.width = LastCreatorWidth;

        //     splitter.RegisterCallback<PointerDownEvent>(evt =>
        //     {
        //         isDragging = true;
        //         pointerId = evt.pointerId;

        //         distFromMouse = evt.position.x - splitter.worldBound.x - 5f;

        //         splitter.CapturePointer(pointerId);

        //         evt.StopPropagation();
        //     });

        //     splitter.RegisterCallback<PointerMoveEvent>(evt =>
        //     {
        //         if (!isDragging || evt.pointerId != pointerId) return;

        //         info.uiState.shortcutButtonArea.style.width = evt.position.x - 107f - distFromMouse;
        //         info.uiState.creatorButtonArea.style.flexGrow = 1;

        //         evt.StopPropagation();
        //     });

        //     splitter.RegisterCallback<PointerUpEvent>(evt =>
        //     {
        //         if (evt.pointerId != pointerId) return;

        //         LastCreatorWidth = info.uiState.shortcutButtonArea.resolvedStyle.width;
        //         isDragging = false;

        //         splitter.ReleasePointer(pointerId);
        //         evt.StopPropagation();
        //     });
        // }

        public static void SearchBarAndCreatorTabBtnCallbacks(WinInstanceInfo info)
        {
            // VisualElement splitter = info.mainUI.Q<VisualElement>("splitter");
            // splitter.style.backgroundImage = LoadTex("grip.png");

            Button searchBtn = info.mainUI.Q<Button>("searchToggle");
            searchBtn.Q<VisualElement>("icon").style.backgroundImage = GlobalUtils.LoadTex("magnifying-glass (1).png");

            // Button creatorTabAddingBtn = info.mainUI.Q<Button>("addCreatorBtn");
            // creatorTabAddingBtn.style.backgroundImage = LoadTex("plus 1.png");

            // ScrollView AssetCreatorButtonArea = info.mainUI.Q<ScrollView>("CreationMenuDragArea");
            ScrollView shortcutTabsArea = info.mainUI.Q<ScrollView>("shorcutsDragArea");
            VisualElement toolbarStrip = info.mainUI.Q<VisualElement>("topToolbar");

            Button sceneMenu = info.mainUI.Q<Button>("sceneSel");

            if (IsSearchBarOpen)
                OpenSearchBar(info, sceneMenu, searchBtn, toolbarStrip);
            else
                CloseSearchBar(info, sceneMenu, searchBtn, shortcutTabsArea,toolbarStrip);

            searchBtn.clicked += () =>
                {
                    if (IsSearchBarOpen)
                        CloseSearchBar(info, sceneMenu, searchBtn, shortcutTabsArea,toolbarStrip);
                    else
                        OpenSearchBar(info, sceneMenu, searchBtn, toolbarStrip);
                };


            sceneMenu.clicked += () =>
            {
                var menu = new GenericMenu();

                string[] guids = AssetDatabase.FindAssets("t:Scene");
                string activeScenePath = EditorSceneManager.GetActiveScene().path;

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.StartsWith("Assets/"))
                        continue;

                    string name = Path.GetFileNameWithoutExtension(path);

                    string capturedPath = path;
                    menu.AddItem(new GUIContent(name), capturedPath == activeScenePath, () =>
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            EditorSceneManager.OpenScene(capturedPath);
                    });
                }

                menu.ShowAsContext();
            };

        }

        private static void CloseSearchBar(WinInstanceInfo info, Button sceneBtn, Button openSearchButton, ScrollView shortcutTabsArea, VisualElement toolbar)
        {
            if (info.isTwoColumn)
            {
                // splitter.style.display = DisplayStyle.Flex;
                // creatorBtn.style.display = DisplayStyle.Flex;
                sceneBtn.style.display = DisplayStyle.Flex;
                shortcutTabsArea.scrollOffset = Vector2.zero;
            }
            else
                sceneBtn.style.display = DisplayStyle.None;

            openSearchButton.style.width = 30f;

            // AssetCreatorButtonArea.style.width = LastCreatorWidth;
            toolbar.style.marginRight = 40f;

            IsSearchBarOpen = false;
        }

        private static void OpenSearchBar(WinInstanceInfo info, Button sceneBtn, Button openSearchButton, VisualElement toolbar)
        {
            if (!info.isTwoColumn)
            {
                openSearchButton.style.width = 20f;
                sceneBtn.style.display = DisplayStyle.None;
            }

            // splitter.style.display = DisplayStyle.None;
            // creatorBtn.style.display = DisplayStyle.None;

            // AssetCreatorButtonArea.style.width = 0f;
            toolbar.style.marginRight = 460f;

            IsSearchBarOpen = true;
        }


        // address copy from bottom bar
        public static void SetupBottomBarMargin(WinInstanceInfo winInfo)
        {
            VisualElement bottomAddressBar = winInfo.mainUI.Q<VisualElement>("bottomAddressBar");

            bottomAddressBar.style.marginLeft = ReflectionsMethods.GetSideRectWidth(winInfo.editorWindow);
        }

        public static void RegisterAddressCopyCallbacks(WinInstanceInfo info)
        {
            VisualElement bottomAddressBar = info.mainUI.Q<VisualElement>("bottomAddressBar");
            bottomAddressBar.RegisterCallback<MouseDownEvent>((evt) =>
            {
                if (info.isTwoColumn) bottomAddressBar.style.marginLeft = ReflectionsMethods.GetSideRectWidth(info.editorWindow);

                string copyingStr = "";

                if (evt.button == 0)
                {
                    copyingStr = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject))
                        ? AssetDatabase.GetAssetPath(Selection.activeObject)
                        : ReflectionsMethods.GetActiveFolderPath(info.editorWindow);
                }
                else if (evt.button == 1)
                {
                    string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                    copyingStr = !string.IsNullOrEmpty(assetPath)
                        ? Path.GetFileName(assetPath)
                        : "";
                }

                EditorGUIUtility.systemCopyBuffer = copyingStr;

                ShowCopiedNotification(evt.mousePosition, info);
            });

            if (info.isTwoColumn)
                Selection.selectionChanged += () =>
                {
                    SetupBottomBarMargin(info);
                };
        }

        public static void ShowCopiedNotification(Vector2 position, WinInstanceInfo info)
        {
            info.uiState.copiedText.style.left = position.x - 20;

            info.uiState.copiedText.style.display = DisplayStyle.Flex;

            info.mainUI.schedule.Execute(() =>
            {
                info.uiState.copiedText.style.display = DisplayStyle.None;
            }).ExecuteLater(1000);
        }


        // icon popup
        public static void ShowBoxAtPos(VisualElement box, float posX, VisualElement win)
        {
            win.pickingMode = PickingMode.Position;

            float rightOffset = win.resolvedStyle.width - 200;

            box.style.left = Mathf.Clamp(posX, 0, rightOffset);

            box.style.display = DisplayStyle.Flex;
        }

        public static void CallbacksForPopupBoxes(WinInstanceInfo info)
        {
            info.mainUI.RegisterCallback<MouseDownEvent>((evt) =>
            {
                CloseAllPopups(info);
            });
        }

        private static void CloseAllPopups(WinInstanceInfo info)
        {
            info.uiState.colorPopup.style.display = DisplayStyle.None;

            info.uiState.popupTarget.popupIsOpen = false;

            info.mainUI.pickingMode = PickingMode.Ignore;
        }

        public static void CallbacksForColorPopup(WinInstanceInfo info)
        {
            var colorButtons = info.uiState.colorPopup.Query<Button>("icon").ToList();
            var removeColorBtn = info.uiState.colorPopup.Q<Button>("removeColorBtn");

            // palette.Clear();
            foreach (Button clrBtn in colorButtons)
            {
                var buttonColor = clrBtn.resolvedStyle.backgroundColor;
                // palette.Add(buttonColor);

                clrBtn.clicked += () =>
                {
                    if (!info.uiState.popupTarget.popupIsOpen) return;

                    ApplyColorToTargetElement(GlobalUtils.ColorToHex(buttonColor), info);
                };
            }

            removeColorBtn.style.backgroundImage = GlobalUtils.LoadTex("cross icon.png");
            removeColorBtn.clicked += () =>
            {
                if (!info.uiState.popupTarget.popupIsOpen) return;

                ApplyColorToTargetElement("#3E3E3E", info);
            };

            info.uiState.colorPopup.RegisterCallback<MouseDownEvent>((evt) => evt.StopPropagation());
        }

        public static void ApplyColorToTargetElement(string hex, WinInstanceInfo info)
        {
            ColorPopupTarget target = info.uiState.popupTarget;

            // if (target.isForCreator)
            // {
            //     target.activeCreatorButton.color = hex;
            //     info.uiState.creatorButtons.SaveToJson();
            // }
            // else
            // {
            // }
            target.activeNavButton.color = hex;
            info.uiState.navButtons.SaveToJson();

            Color clr = GlobalUtils.HexToColor(hex);

            target.activeVisualItem.style.backgroundColor = clr;
            target.activeVisualItem.Q<Label>("buttonLabel").style.color = GlobalUtils.IsColorDark(clr)
                ? GlobalUtils.HexToColor("#f7f7f7")
                : GlobalUtils.HexToColor("#2e2e2e");

            CloseAllPopups(info);
        }

    }
}

#endif