// Copyright (c) 2025 PinePie. All rights reserved.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PinePie.PieTabs
{
    public static partial class BrowserTabs
    {
        // public static List<Color> palette = new();
        private const string SearchBarOpenKey = "PieTabs_SearchBarOpen";

        private static bool IsSearchBarOpen
        {
            get => EditorPrefs.GetBool(SearchBarOpenKey, false);
            set => EditorPrefs.SetBool(SearchBarOpenKey, value);
        }


        public static void Setup()
        {
            ReflectionsMethods.GetProjectBrowserUIs();
            foreach (var info in ReflectionsMethods.BrowserWindowsInfo)
            {
                if (info.ProjUI.panel == null)
                    continue;

                SetupForWindow(info);

                info.ProjUI.RegisterCallback<DetachFromPanelEvent>(evt =>
                {
                    Selection.selectionChanged -= PieTabsLoader.EnsurePieDeskOverlay;
                    Selection.selectionChanged += PieTabsLoader.EnsurePieDeskOverlay;

                    // EditorApplication.delayCall += () =>
                    // {
                    //     if (info.ProjUI.panel == null)
                    // };
                });
            }
        }

        private static void SetupForWindow(WinInstanceInfo info)
        {
            info.mainUI = GlobalUtils.LoadUXML("PieDeskMainUI.uxml").Instantiate().Q<VisualElement>("PieDeskUI");
            info.mainUI.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                $"{PathUtility.GetPieTabsPath()}/PinePie/PieTabs/editor/Core/UI/PieDeskStyling.uss"));

            BrowserTabsUI ui = info.uiState;

            ui.shortcutButtonAsset = GlobalUtils.LoadUXML("shortcutButton.uxml");
            ui.placeholderNeedle = GlobalUtils.LoadUXML("placeholderLine.uxml").Instantiate().Q<VisualElement>("line");

            ui.spacer = info.mainUI.Q<VisualElement>("spacer");
            ui.copiedText = info.mainUI.Q<VisualElement>("copiedText");
            ui.colorPopup = info.mainUI.Q<VisualElement>("colorPopup");
            CallbacksForColorPopup(info);

            info.ProjUI.Q<VisualElement>("PieDeskUI")?.RemoveFromHierarchy(); // remove if found

            info.isTwoColumn = ReflectionsMethods.IsTwoColumnMode(info.editorWindow);
            PieTabsPrefs.LoadKeyComb();

            ui.shortcutButtonArea = info.mainUI.Q<ScrollView>("shorcutsDragArea");
            ui.creatorButtonArea = info.mainUI.Q<VisualElement>("creatorTabs");

            // ui.creatorButtonArea = info.mainUI.Q<ScrollView>("CreationMenuDragArea");
            if (!info.isTwoColumn)
            {
                ui.creatorButtonArea.style.display = DisplayStyle.None;
                ui.spacer.style.display = DisplayStyle.None;

                VisualElement bottomBar = info.mainUI.Q<VisualElement>("bottomAddressBar");
                bottomBar.style.marginRight = 0;
                bottomBar.style.marginLeft = 0;
            }
            else // two coloumn mode 
            {
                ui.creatorButtonArea.style.display = DisplayStyle.Flex;
                ui.spacer.style.display = DisplayStyle.Flex;

                for (int i = 0; i < ui.creatorButtonArea.childCount; i++)
                {
                    var e = ui.creatorButtonArea[i] as Button;

                    switch (i)
                    {
                        case 0:
                            e.Q<VisualElement>("icon").style.backgroundImage = IconsManager.Folder;
                            e.clicked += () => EditorApplication.ExecuteMenuItem("Assets/Create/Folder");
                            break;

                        case 1:
                            e.Q<VisualElement>("icon").style.backgroundImage = IconsManager.Script;
#if UNITY_6000_0_OR_NEWER
                            e.clicked += () => EditorApplication.ExecuteMenuItem("Assets/Create/MonoBehaviour Script");
#else
                            e.clicked += () => EditorApplication.ExecuteMenuItem("Assets/Create/C# Script");
#endif
                            break;

                        case 2:
                            e.Q<VisualElement>("icon").style.backgroundImage = IconsManager.Scene;
#if UNITY_6000_0_OR_NEWER
                            e.clicked += () => EditorApplication.ExecuteMenuItem("Assets/Create/Scene/Scene");
#else
                            e.clicked += () => EditorApplication.ExecuteMenuItem("Assets/Create/Scene");
#endif
                            break;

                        case 3:
                            e.Q<VisualElement>("icon").style.backgroundImage = IconsManager.Material;
                            e.clicked += () => EditorApplication.ExecuteMenuItem("Assets/Create/Material");
                            break;

                        case 4:
                            e.Q<VisualElement>("icon").style.backgroundImage = IconsManager.Shader;
#if UNITY_6000_0_OR_NEWER
                            e.clicked += () => EditorApplication.ExecuteMenuItem("Assets/Create/Shader/Standard Surface Shader");
#else
                            e.clicked += () => EditorApplication.ExecuteMenuItem("Assets/Create/Shader");
#endif
                            break;
                    }
                }


                SetupBottomBarMargin(info);
            }

            CallbacksForPopupBoxes(info);
            RegisterAddressCopyCallbacks(info);
            SetupDragAreaStyling(info);
            SearchBarAndCreatorTabBtnCallbacks(info);

            // shortcut buttons
            ui.navButtons.LoadFromJson(ui.shortcutButtonAsset, info);
            SetupDragNDropForShortcutArea(info);
            FillShortcutButtons(info);


            info.ProjUI.Add(info.mainUI);
        }


        // click callbacks
        public static void OnShortcutButtonClicked(
            VisualElement UIbutton,
            ShortcutButton buttonProp,
            WinInstanceInfo info)
        {
            // callbacks
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(buttonProp.Path);
            UIbutton.AddManipulator(new ShortcutDragManipulator(obj));


            UIbutton.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    UIbutton.Q<VisualElement>("shade").style.backgroundColor = new Color(255, 255, 255, 0.15f);

                    evt.StopPropagation();
                }
                else if (evt.button == 1)
                {
                    evt.StopPropagation();
                }
            });

            UIbutton.RegisterCallback<PointerUpEvent>(evt =>
            {
                // single click
                if (obj == null) return;

                if (PieTabsPrefs.KeyMatches(evt, PieTabsPrefs.directOpenShortcut))
                {
                    if (AssetDatabase.IsValidFolder(buttonProp.Path))
                        ReflectionsMethods.OpenFolder(buttonProp.Path, info);
                    else
                        AssetDatabase.OpenAsset(obj);

                    evt.StopPropagation();
                }
                else if (PieTabsPrefs.KeyMatches(evt, PieTabsPrefs.minimalShortcut))
                {
                    buttonProp.isMinimal = !buttonProp.isMinimal;

                    UIbutton.Q<Label>("buttonLabel").text = buttonProp.isMinimal ? "" : buttonProp.Label;
                    UIbutton.tooltip = buttonProp.isMinimal ? buttonProp.Label : null;

                    info.uiState.navButtons.SaveToJson();

                    evt.StopPropagation();
                }
                else if (PieTabsPrefs.KeyMatches(evt, PieTabsPrefs.colorShortcut))
                {
                    ShowBoxAtPos(info.uiState.colorPopup, evt.position.x - 100, info?.mainUI);

                    // info.uiState.popupTarget.isForCreator = false;
                    info.uiState.popupTarget.popupIsOpen = true;

                    info.uiState.popupTarget.activeNavButton = buttonProp;
                    info.uiState.popupTarget.activeVisualItem = UIbutton;

                    evt.StopPropagation();
                }

                else if (PieTabsPrefs.KeyMatches(evt, PieTabsPrefs.deleteShortcut))
                {
                    if (PieTabsPrefs.AskBeforeDelete)
                    {
                        bool confirm = EditorUtility.DisplayDialog(
                            "Delete Tab",
                            $"Are you sure you want to delete \"{buttonProp.Label}\" Tab?",
                            "Delete", "Cancel"
                        );

                        if (confirm) RemoveButton(buttonProp, info);
                    }
                    else RemoveButton(buttonProp, info);

                    evt.StopPropagation();
                }

                else if (evt.button == 0)
                {
                    string path = buttonProp.Path;
                    PieAssetType type = GlobalUtils.GetAssetType(path);

                    if (type == PieAssetType.Folder && PieTabsPrefs.FastFolderOpen)
                        ReflectionsMethods.OpenFolder(path, info);
                    else if ((type == PieAssetType.Scene && PieTabsPrefs.FastSceneOpen)
                             || (type == PieAssetType.Script && PieTabsPrefs.FastScriptOpen)
                             || (type == PieAssetType.Prefab && PieTabsPrefs.FastPrefabOpen)
                             || (type == PieAssetType.ShaderGraph && PieTabsPrefs.FastShaderGraphOpen)
                             || (type == PieAssetType.VisualScriptingGraph && PieTabsPrefs.FastVisScrGraphOpen))
                        AssetDatabase.OpenAsset(obj);
                    else
                        ReflectionsMethods.FocusAssetByObj(obj, info?.editorWindow);


                    evt.StopPropagation();
                }

                UIbutton.Q<VisualElement>("shade").style.backgroundColor = new Color(255, 255, 255, 0.12f);
            });
        }

        public static void FillShortcutButtons(WinInstanceInfo info)
        {
            info.uiState.shortcutButtonArea.Clear();

            foreach (var button in info.uiState.navButtons.buttons)
            {
                info.uiState.shortcutButtonArea.Add(button.UIbutton);
            }
        }

        public static void RemoveButton(ShortcutButton toRemove, WinInstanceInfo info)
        {
            info.uiState.navButtons.RemoveButton(toRemove);

            FillShortcutButtons(info);
        }


        // shortcut bar dragging
        public static void SetupDragNDropForShortcutArea(WinInstanceInfo info)
        {
            info.uiState.shortcutButtonArea.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                PlaceholderNeedleAtPos(info, evt.localMousePosition.x);

                evt.StopPropagation();
            });

            info.uiState.shortcutButtonArea.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));

                    Texture2D iconTexture = EditorGUIUtility.ObjectContent(obj, obj.GetType()).image as Texture2D;

                    var foundButton = info.uiState.navButtons.buttons.FirstOrDefault(b => b.buttonProp.guid == guid);
                    ShortcutButton buttonAtSamePath = foundButton?.buttonProp;

                    var button = new ShortcutButton(obj.name, guid);
                    if (buttonAtSamePath != null)
                        button = new ShortcutButton(obj.name, guid, buttonAtSamePath.isMinimal, buttonAtSamePath.color);

                    if (info.uiState.placeHolderIndex != -1) info.uiState.navButtons.InsertAt(button, info);

                    if (buttonAtSamePath != null) info.uiState.navButtons.RemoveButton(buttonAtSamePath);

                    info.uiState.placeholderNeedle.RemoveFromHierarchy();
                    info.uiState.placeHolderIndex = -1;
                }

                FillShortcutButtons(info);

                evt.StopPropagation();
            });

            info.uiState.shortcutButtonArea.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (DragAndDrop.objectReferences.Length > 0)
                {
                    info.uiState.placeholderNeedle.RemoveFromHierarchy();

                    info.uiState.placeHolderIndex = -1;
                }
            });
        }
    }

    public class BrowserTabsUI
    {
        public ShortcutButtonBundle navButtons = new();

        public VisualElement placeholderNeedle;
        public VisualElement copiedText;

        public VisualElement colorPopup;
        public VisualElement spacer;

        public VisualTreeAsset shortcutButtonAsset;

        public VisualElement shortcutButtonArea;
        public VisualElement creatorButtonArea;

        public int placeHolderIndex;

        public DragState dragState = new();
        public ColorPopupTarget popupTarget = new();
    }

    public class ColorPopupTarget
    {
        public bool popupIsOpen = false;

        public ShortcutButton activeNavButton;
        public VisualElement activeVisualItem;
    }

    public class DragState
    {
        public Vector2 dragStartPos;
        public bool isDragging = false;
        public bool isMouseDown = false;
    }


    public class WinInstanceInfo
    {
        public bool isTwoColumn;
        public EditorWindow editorWindow;
        public VisualElement ProjUI;
        public VisualElement mainUI;

        public BrowserTabsUI uiState = new();
    }
}
#endif