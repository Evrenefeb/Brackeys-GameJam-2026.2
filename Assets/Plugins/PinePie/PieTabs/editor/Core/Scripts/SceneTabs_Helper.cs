// Copyright (c) 2025 PinePie. All rights reserved.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PinePie.PieTabs
{
    public static partial class SceneTabs
    {
        public static List<Color> paletteColor = new();
        public static List<Texture2D> paletteIcons = new();

        public static VisualElement CreateButtonElement(SceneTab btnProp)
        {
            var button = SceneTabsUI.sceneTabsAsset.Instantiate().Q<VisualElement>("tab");
            if (button == null) return null;

            int i = btnProp.colorIndex < paletteColor.Count
                ? btnProp.colorIndex
                : 0;
            button.style.backgroundColor = paletteColor[i];

            int j = btnProp.iconIndex < paletteIcons.Count
                ? btnProp.iconIndex
                : 0;
            button.Q<VisualElement>("icon").style.backgroundImage = paletteIcons[j];

            // shade
            var shade = button.Q<VisualElement>("shade");
            button.RegisterCallback<MouseEnterEvent>(_ => { shade.style.backgroundColor = new Color(1, 1, 1, 0.1f); });
            button.RegisterCallback<MouseLeaveEvent>(_ => { shade.style.backgroundColor = new Color(1, 1, 1, 0f); });

            OnSceneTabClicked(button, btnProp);
            button.userData = btnProp;

            return button;
        }

        public static int PlaceholderNeedleAtPos(float mouseX)
        {
            VisualElement area = SceneTabsUI.sceneTabsArea;

            bool containsPlaceholder = area.Contains(SceneTabsUI.placeholderNeedle);

            // getting drop index
            int dropIndex = 0;
            SceneTabsUI.isMerging = false;
            for (int i = 0; i < area.childCount; i++)
            {
                var child = area[i];

                if (containsPlaceholder && child == SceneTabsUI.placeholderNeedle) continue;

                var rect = child.layout;

                float leftPart = rect.xMin + rect.width * 0.2f;
                float rightPart = rect.xMin + rect.width * 0.8f;

                if (mouseX > leftPart && mouseX < rightPart)
                    SceneTabsUI.isMerging = true;

                if (mouseX > leftPart)
                    dropIndex++;
                else
                    break;
            }

            if (dropIndex != SceneTabsUI.placeHolderIndex || !containsPlaceholder)
            {
                SceneTabsUI.placeHolderIndex = dropIndex;

                if (containsPlaceholder)
                    SceneTabsUI.placeholderNeedle.RemoveFromHierarchy();
                if (!SceneTabsUI.isMerging)
                    area.Insert(dropIndex, SceneTabsUI.placeholderNeedle);
            }

            if (containsPlaceholder && SceneTabsUI.isMerging)
                SceneTabsUI.placeholderNeedle.RemoveFromHierarchy();

            return dropIndex;
        }

        public static void ShowBoxAtPos(VisualElement box, float posX, VisualElement win)
        {
            win.pickingMode = PickingMode.Position;

            float rightOffset = win.resolvedStyle.width - 75;

            box.style.left = Mathf.Clamp(posX, 0, rightOffset);
            box.style.display = DisplayStyle.Flex;
        }

        public static void CallbacksForColorPopup()
        {
            var iconButtons = SceneTabsUI.iconPopup.Q<VisualElement>("TabIcons").Query<Button>("icon").ToList();
            var removeColorBtn = SceneTabsUI.iconPopup.Q<Button>("removeColorBtn");

            var colorButtons = SceneTabsUI.iconPopup.Q<VisualElement>("colors").Query<Button>("icon").ToList();

            int i = 0;
            paletteColor.Clear();
            foreach (Button clrBtn in colorButtons)
            {
                var buttonColor = clrBtn.resolvedStyle.backgroundColor;
                paletteColor.Add(buttonColor);

                int index = i;
                clrBtn.clicked += () =>
                {
                    // Undo.RecordObject(GetSceneTabsData(), "Change Scene Tab Icon");

                    ApplyColorToTargetElement(index, buttonColor);
                };
                i++;
            }

            i = 0;
            paletteIcons.Clear();
            foreach (Button iconBtn in iconButtons)
            {
                var icon = iconBtn.resolvedStyle.backgroundImage.texture;
                paletteIcons.Add(icon);

                int index = i;
                iconBtn.clicked += () =>
                {
                    // Undo.RecordObject(GetSceneTabsData(), "Change Scene Tab Color");

                    ApplyIconToTargetElement(index, icon);
                };
                i++;
            }

            removeColorBtn.style.backgroundImage = GlobalUtils.LoadTex("cross icon.png");
            removeColorBtn.clicked += () =>
            {
                // Undo.RecordObject(GetSceneTabsData(), "Change Scene Tab Color");

                ApplyColorToTargetElement(0, new(0.21f, 0.21f, 0.21f));
            };

            SceneTabsUI.iconPopup.RegisterCallback<MouseDownEvent>((evt) => evt.StopPropagation());
        }

        public static void CreateColorsAndIconsList()
        {
            var iconButtons = SceneTabsUI.iconPopup.Q<VisualElement>("TabIcons").Query<Button>("icon").ToList();
            var colorButtons = SceneTabsUI.iconPopup.Q<VisualElement>("colors").Query<Button>("icon").ToList();

            paletteColor.Clear();
            foreach (Button clrBtn in colorButtons)
            {
                var buttonColor = clrBtn.resolvedStyle.backgroundColor;
                paletteColor.Add(buttonColor);
            }

            paletteIcons.Clear();
            foreach (Button iconBtn in iconButtons)
            {
                var icon = iconBtn.resolvedStyle.backgroundImage.texture;
                paletteIcons.Add(icon);
            }
        }

        public static void ApplyColorToTargetElement(int index, Color color)
        {
            SceneTabsUI.popupTarget.activeTabProp.colorIndex = index;
            SceneTabsUI.popupTarget.activeVisualItem.style.backgroundColor = color;

            SaveSceneTabs();
            CloseAllPopups();
        }

        public static void ApplyIconToTargetElement(int index, Texture2D icon)
        {
            IconPopupTarget target = SceneTabsUI.popupTarget;

            target.activeTabProp.iconIndex = index;
            target.activeVisualItem.Q<VisualElement>("icon").style.backgroundImage = icon;

            SaveSceneTabs();
            CloseAllPopups();
        }

        private static void CloseAllPopups()
        {
            SceneTabsUI.iconPopup.style.display = DisplayStyle.None;
            SceneTabsUI.mainUI.pickingMode = PickingMode.Ignore;
        }


        // UI Setup

        public static void OnDrag(float mouseX)
        {
            VisualElement area = SceneTabsUI.sceneTabsArea;

            bool containsPlaceholder = area.Contains(SceneTabsUI.placeholderNeedle);

            int newIndex = 0;
            for (int i = 0; i < area.childCount; i++)
            {
                var child = area[i];
                if (containsPlaceholder && child == SceneTabsUI.placeholderNeedle) continue;

                Rect rect = child.worldBound;
                float midX = rect.x + rect.width / 2;

                if (mouseX > midX) newIndex++;
            }

            if (newIndex != SceneTabsUI.placeHolderIndex)
            {
                SceneTabsUI.placeHolderIndex = newIndex;

                if (containsPlaceholder)
                    SceneTabsUI.placeholderNeedle.RemoveFromHierarchy();
                area.Insert(newIndex, SceneTabsUI.placeholderNeedle);
            }

            // if (containsPlaceholder)
            //     SceneTabsUI.placeholderNeedle.RemoveFromHierarchy();
        }

        public static void EndDrag(VisualElement btn)
        {
            VisualElement area = SceneTabsUI.sceneTabsArea;

            if (SceneTabsUI.placeHolderIndex < 0)
                return;

            SceneTabsUI.placeholderNeedle.RemoveFromHierarchy();

            int oldIndex = area.IndexOf(btn);

            MoveItem(SceneTabsUI.sceneTabs, oldIndex, SceneTabsUI.placeHolderIndex);
            SaveSceneTabs();

            SceneTabsUI.placeHolderIndex = -1;
        }

        public static void MoveItem<T>(List<T> list, int fromIndex, int toIndex)
        {
            toIndex = Mathf.Clamp(toIndex, 0, list.Count);

            if (fromIndex == toIndex || (fromIndex == list.Count - 1 && toIndex == list.Count))
                return;

            T item = list[fromIndex];
            list.RemoveAt(fromIndex);

            if (toIndex > fromIndex) toIndex--;

            if (toIndex >= list.Count) list.Add(item);
            else list.Insert(toIndex, item);
        }


        // data loading
        public static SceneTabsData GetSceneTabsData()
        {
            SceneTabsData data = Object.FindAnyObjectByType<SceneTabsData>();

            if (data != null)
                return data;

            GameObject go = new("[PieTabs_Data]");
            data = go.AddComponent<SceneTabsData>();

            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;

            Undo.RegisterCreatedObjectUndo(go, "Create PieTabs Data");

            return data;
        }

        public static int GetNextTabID(SceneTabsData data)
        {
            int nextID = 1;

            foreach (var tab in data.tabs)
                nextID = Mathf.Max(nextID, tab.id + 1);

            return nextID;
        }

        public static void SaveSceneTabs()
        {
            var data = GetSceneTabsData();

            // Undo.RecordObject(data, "Save Scene Tabs");

            data.tabs = SceneTabsUI.sceneTabs.ToList();

            EditorUtility.SetDirty(data);
        }

        public static void LoadSceneTabs()
        {
            var data = GetSceneTabsData();

            SceneTabsUI.sceneTabs.Clear();
            foreach (var tab in data.tabs)
            {
                tab.UIButton = CreateButtonElement(tab);
                SceneTabsUI.sceneTabs.Add(tab);
            }
        }

        static readonly HashSet<GameObject> visible = new();
        static readonly HashSet<GameObject> deletedGo = new();

        // btn action
        public static void ShowActiveSceneTabGameobjects(SceneTab activeTab)
        {
            foreach (var button in SceneTabsUI.sceneTabs)
            {
                if (button.id == SceneTabsUI.ActiveSceneTabID)
                    button.UIButton.AddToClassList("activeSceneTab");
                else
                    button.UIButton.RemoveFromClassList("activeSceneTab");
            }

            if (activeTab == null)
            {
                ShowAllGameobjects();
                return;
            }

            SceneTabsData data = GetSceneTabsData();

            visible.Clear();
            deletedGo.Clear();

            // gather visible gameobjects -- childs and parents
            if (activeTab != null)
            {
                foreach (GameObject refGo in activeTab.refs)
                {
                    if (refGo == null)
                    {
                        deletedGo.Add(refGo);
                        continue;
                    }

                    visible.Add(refGo);

                    for (Transform t = refGo.transform.parent; t != null; t = t.parent)
                        visible.Add(t.gameObject);

                    if (refGo.transform.childCount > 0)
                        foreach (Transform child in refGo.GetComponentsInChildren<Transform>(true))
                        {
                            if (child == refGo.transform)
                                continue;

                            visible.Add(child.gameObject);
                        }
                }

                foreach (var delGO in deletedGo)
                    activeTab.refs.Remove(delGO);
            }

            // show
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                // keeping data file hidden
                if (go == data.gameObject)
                {
                    go.hideFlags |= HideFlags.HideInHierarchy | HideFlags.NotEditable;
                    continue;
                }

                // show targeted - hide others
                if (SceneTabsUI.ActiveSceneTabID == -1 || visible.Contains(go))
                {
                    go.hideFlags &= ~HideFlags.HideInHierarchy;
                }
                else
                    go.hideFlags |= HideFlags.HideInHierarchy;
            }
        }

        public static void ShowAllGameobjects()
        {
            SceneTabsData data = GetSceneTabsData();

            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                // keeping data file hidden
                if (data != null && go == data.gameObject)
                {
                    go.hideFlags |= HideFlags.HideInHierarchy | HideFlags.NotEditable;
                    continue;
                }

                go.hideFlags &= ~HideFlags.HideInHierarchy;
            }
        }

        public static void OnShowAllTabSelected()
        {
            // select - show all gameobjects
            SceneTabsUI.ActiveSceneTabID = -1;
            ShowAllGameobjects();
        }
    }
}
#endif