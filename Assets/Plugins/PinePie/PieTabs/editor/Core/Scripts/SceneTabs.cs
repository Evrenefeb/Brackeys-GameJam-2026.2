// Copyright (c) 2025 PinePie. All rights reserved.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PinePie.PieTabs
{
    public static partial class SceneTabs
    {
        public static void Setup()
        {
            ReflectionsMethods.GetHierarchyWindow();
            SceneTabsUI.RefreshSceneKey();

            VisualElement sceneWinUI = ReflectionsMethods.hierarchyWindow.rootVisualElement;

            if (sceneWinUI.panel == null)
                return;

            SceneTabsUI.mainUI = GlobalUtils.LoadUXML("heirarchyTabsMain.uxml").Instantiate().Q<VisualElement>("sceneTabsMain");
            SceneTabsUI.mainUI.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>($"{PathUtility.GetPieTabsPath()}/PinePie/PieTabs/editor/Core/UI/PieDeskStyling.uss"));

            // popup closing callback
            SceneTabsUI.mainUI.RegisterCallback<MouseDownEvent>((evt) => CloseAllPopups());

            SceneTabsUI.sceneTabsAsset = GlobalUtils.LoadUXML("sceneTabAsset.uxml");
            SceneTabsUI.sceneTabsArea = SceneTabsUI.mainUI.Q<ScrollView>("tabArea");

            SceneTabsUI.placeholderNeedle = GlobalUtils.LoadUXML("placeholderLine.uxml").Instantiate().Q<VisualElement>("line");

            // show all - btn
            Button mainSceneBtn = SceneTabsUI.mainUI.Q<Button>("showAllBtn");
            MainSceneTabDropSetup(mainSceneBtn);
            mainSceneBtn.clicked -= OnShowAllTabSelected;
            mainSceneBtn.clicked += OnShowAllTabSelected;

            SceneTabsUI.iconPopup = SceneTabsUI.mainUI.Q<VisualElement>("colorPopup");


            // loading scene tabs
            CallbacksForColorPopup();
            SetupDropAreaForSceneTabArea();

            LoadSceneTabs();

            FillSceneButtons();
            ShowActiveSceneTabGameobjects(SceneTabsUI.ActiveTab);

            sceneWinUI.Query<VisualElement>("sceneTabsMain").ForEach(t => t.RemoveFromHierarchy()); // remove if found
            sceneWinUI.Add(SceneTabsUI.mainUI);
        }


        public static void OnSceneTabClicked(VisualElement UIbutton, SceneTab buttonProp)
        {
            // callbacks
            UIbutton.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    UIbutton.Q<VisualElement>("shade").style.backgroundColor = new Color(255, 255, 255, 0.15f);

                    SceneTabsUI.dragState.dragStartPos = evt.position;
                    SceneTabsUI.dragState.isMouseDown = true;
                    SceneTabsUI.dragState.isDragging = false;

                    evt.StopPropagation();
                }
                else if (evt.button == 1)
                {
                    evt.StopPropagation();
                }
            });

            UIbutton.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!SceneTabsUI.dragState.isMouseDown) return;

                if (!SceneTabsUI.dragState.isDragging && Vector2.Distance(evt.position, SceneTabsUI.dragState.dragStartPos) > 3f)
                {
                    SceneTabsUI.dragState.isDragging = true;
                    UIbutton.CaptureMouse();
                }

                if (UIbutton.HasMouseCapture() && SceneTabsUI.dragState.isDragging)
                    OnDrag(evt.position.x);
            });

            UIbutton.RegisterCallback<PointerUpEvent>(evt =>
            {
                // single click
                if (evt.button == 0) SceneTabsUI.dragState.isMouseDown = false;

                if (SceneTabsUI.dragState.isDragging)
                {
                    SceneTabsUI.dragState.isDragging = false;
                    UIbutton.ReleaseMouse();

                    EndDrag(UIbutton);
                    FillSceneButtons();

                    evt.StopPropagation();
                    return;
                }

                // single click
                if (evt.button == 0 && evt.modifiers == EventModifiers.None)
                {
                    SceneTabsUI.ActiveSceneTabID = buttonProp.id;
                    ShowActiveSceneTabGameobjects(SceneTabsUI.ActiveTab);

                    evt.StopPropagation();
                }
                // color setting
                else if (PieTabsPrefs.KeyMatches(evt, PieTabsPrefs.colorShortcut))
                {
                    if (SceneTabsUI.ActiveSceneTabID != buttonProp.id)
                    {
                        SceneTabsUI.ActiveSceneTabID = buttonProp.id;
                        ShowActiveSceneTabGameobjects(SceneTabsUI.ActiveTab);
                    }

                    ShowBoxAtPos(SceneTabsUI.iconPopup, evt.position.x - 100, SceneTabsUI.mainUI);

                    SceneTabsUI.popupTarget.activeTabProp = buttonProp;
                    SceneTabsUI.popupTarget.activeVisualItem = UIbutton;

                    evt.StopPropagation();
                }
                else if (PieTabsPrefs.KeyMatches(evt, PieTabsPrefs.deleteShortcut))
                {
                    bool shouldDelete = true;

                    if (PieTabsPrefs.AskBeforeDelete)
                    {
                        shouldDelete = EditorUtility.DisplayDialog(
                            "Delete Tab",
                            "Are you sure you want to delete this scene Tab?",
                            "Delete",
                            "Cancel"
                        );
                    }

                    if (shouldDelete)
                    {
                        RemoveButton(buttonProp);
                        SaveSceneTabs();
                    }

                    evt.StopPropagation();
                }

            });

        }

        public static void FillSceneButtons()
        {
            SceneTabsUI.sceneTabsArea.Clear();

            foreach (var button in SceneTabsUI.sceneTabs)
            {
                SceneTabsUI.sceneTabsArea.Add(button.UIButton);

                if (button.id == SceneTabsUI.ActiveSceneTabID)
                    button.UIButton.AddToClassList("activeSceneTab");
                else
                    button.UIButton.RemoveFromClassList("activeSceneTab");
            }
        }

        public static void RemoveButton(SceneTab tab)
        {
            SceneTabsUI.sceneTabs.Remove(tab);

            if (SceneTabsUI.sceneTabs.Count < 1)
                SceneTabsUI.ActiveSceneTabID = -1;
            else
                SceneTabsUI.ActiveSceneTabID = SceneTabsUI.sceneTabs.Last().id;

            FillSceneButtons();
            ShowActiveSceneTabGameobjects(SceneTabsUI.ActiveTab);
        }


        // drag n drop
        public static void SetupDropAreaForSceneTabArea()
        {
            SceneTabsUI.sceneTabsArea.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                PlaceholderNeedleAtPos(evt.localMousePosition.x);

                evt.StopPropagation();
            });

            SceneTabsUI.sceneTabsArea.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();

                GameObject[] objs = DragAndDrop.objectReferences.OfType<GameObject>().ToArray();
                if (objs.Length == 0)
                    return;

                SceneTab targetTab;
                if (SceneTabsUI.isMerging)
                {
                    targetTab = SceneTabsUI.sceneTabsArea[SceneTabsUI.placeHolderIndex - 1].userData as SceneTab;

                    foreach (var obj in objs)
                    {
                        if (!targetTab.refs.Contains(obj))
                            targetTab.refs.Add(obj);
                    }
                }
                else
                {
                    targetTab = new SceneTab(GetNextTabID(GetSceneTabsData()));
                    targetTab.refs.AddRange(objs);
                    targetTab.UIButton = CreateButtonElement(targetTab);

                    SceneTabsUI.sceneTabs.Add(targetTab);
                }


                var activeTab = SceneTabsUI.ActiveTab;

                if (activeTab != null)
                {
                    if (targetTab != activeTab)
                    {
                        foreach (var obj in objs)
                            activeTab.refs.Remove(obj);
                    }

                    if (activeTab.refs.Count == 0)
                    {
                        SceneTabsUI.sceneTabs.Remove(activeTab);

                        SceneTabsUI.ActiveSceneTabID = targetTab.id;
                    }
                }
                else
                    SceneTabsUI.ActiveSceneTabID = targetTab.id;



                SceneTabsUI.placeholderNeedle.RemoveFromHierarchy();
                SceneTabsUI.placeHolderIndex = -1;

                SaveSceneTabs();
                FillSceneButtons();
                ShowActiveSceneTabGameobjects(SceneTabsUI.ActiveTab);

                evt.StopPropagation();
            });

            SceneTabsUI.sceneTabsArea.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (DragAndDrop.objectReferences.Length > 0)
                {
                    SceneTabsUI.placeholderNeedle.RemoveFromHierarchy();
                    SceneTabsUI.placeHolderIndex = -1;
                }
            });
        }

        public static void MainSceneTabDropSetup(Button btn)
        {
            btn.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                evt.StopPropagation();
            });

            btn.RegisterCallback<DragPerformEvent>(evt =>
            {
                DragAndDrop.AcceptDrag();

                foreach (GameObject obj in DragAndDrop.objectReferences.Cast<GameObject>())
                {
                    var activeTab = SceneTabsUI.ActiveTab;

                    if (activeTab == null)
                        return;

                    activeTab.refs.Remove(obj);

                    // getting all ancesters
                    for (Transform t = obj.transform.parent; t != null; t = t.parent)
                        activeTab.refs.Remove(t.gameObject);

                    // getting childs too
                    if (obj.transform.childCount > 0)
                        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
                        {
                            if (child == obj.transform)
                                continue;

                            activeTab.refs.Remove(child.gameObject);
                        }

                    if (activeTab.refs.Count == 0)
                    {
                        SceneTabsUI.sceneTabs.Remove(activeTab);

                        // SceneTabsUI.activeTab = null;
                        SceneTabsUI.ActiveSceneTabID = -1;

                    }
                }

                SaveSceneTabs();
                FillSceneButtons();
                ShowActiveSceneTabGameobjects(SceneTabsUI.ActiveTab);

                evt.StopPropagation();
            });

        }

    }

    public static class SceneTabsUI
    {
        public static VisualElement mainUI;

        public static List<SceneTab> sceneTabs = new();
        public static SceneTab ActiveTab => sceneTabs.Find(t => t.id == ActiveSceneTabID);

        public static void RefreshSceneKey()
        {
            string path = SceneManager.GetActiveScene().path;
            string guid = AssetDatabase.AssetPathToGUID(path);
            _activeSceneKey = $"PieTabs_ActiveTab_{guid}";
        }

        static string _activeSceneKey;
        public static int ActiveSceneTabID
        {
            get => EditorPrefs.GetInt(_activeSceneKey, -1);
            set => EditorPrefs.SetInt(_activeSceneKey, value);
        }


        public static VisualTreeAsset sceneTabsAsset;
        public static VisualElement sceneTabsArea;

        public static IconPopupTarget popupTarget = new();
        public static DragState dragState = new();

        public static VisualElement iconPopup;

        public static VisualElement placeholderNeedle;
        public static int placeHolderIndex;
        public static bool isMerging;
    }

    public class IconPopupTarget
    {
        public SceneTab activeTabProp;
        public VisualElement activeVisualItem;
    }

}
#endif