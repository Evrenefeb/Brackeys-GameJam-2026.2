// Copyright (c) 2025 PinePie. All rights reserved.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace PinePie.PieTabs
{
    public static class ReflectionsMethods
    {
        public static List<WinInstanceInfo> BrowserWindowsInfo;
        public static EditorWindow hierarchyWindow;
        private static Type browserWinType;

        private static MethodInfo showFolderContents;


        public static WinInstanceInfo GetFocusedWindow()
        {
            return BrowserWindowsInfo.FirstOrDefault(w => w.editorWindow == EditorWindow.focusedWindow);
        }

        // internal values fetcher
        public static float GetSideRectWidth(EditorWindow win)
        {
            if (win.GetType().ToString() == "UnityEditor.ProjectBrowser")
            {
                var type = win.GetType();
                FieldInfo Field = type.GetField("m_DirectoriesAreaWidth", BindingFlags.NonPublic | BindingFlags.Instance);

                if (Field != null)
                {
                    var rect = Field.GetValue(win);
                    return (float)rect;
                }
                else
                    return 0;
            }
            else
                return 0;
        }

        public static bool IsTwoColumnMode(EditorWindow win)
        {
            if (win.GetType().ToString() == "UnityEditor.ProjectBrowser")
            {
                var viewModeField = win.GetType().GetField("m_ViewMode", BindingFlags.Instance | BindingFlags.NonPublic);
                if (viewModeField != null)
                {
                    object viewModeValue = viewModeField.GetValue(win);
                    return viewModeValue.ToString() == "TwoColumns";
                }
            }

            return false;
        }

        public static List<VisualElement> GetProjectBrowserUIs()
        {
            List<VisualElement> result = new();

            var infos = GetWindowsInfo();

            foreach (var info in infos)
            {
                result.Add(info.ProjUI);
            }

            return result;
        }

        public static List<WinInstanceInfo> GetWindowsInfo()
        {
            List<WinInstanceInfo> result = new();

            var projectBrowsers = Resources.FindObjectsOfTypeAll(typeof(EditorWindow));
            foreach (EditorWindow window in projectBrowsers.Cast<EditorWindow>())
            {
                if (window.GetType().ToString() == "UnityEditor.ProjectBrowser")
                {
                    result.Add(new()
                    {
                        editorWindow = window,
                        ProjUI = window.rootVisualElement
                    });
                }
            }

            BrowserWindowsInfo = result;
            return result;
        }

        public static EditorWindow GetHierarchyWindow()
        {
            var hierarchyType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");

            var windows = Resources.FindObjectsOfTypeAll(hierarchyType).Cast<EditorWindow>();

            hierarchyWindow = windows.FirstOrDefault();
            return hierarchyWindow;
        }

        public static string GetActiveFolderPath(EditorWindow win)
        {
            MethodInfo method = win.GetType().GetMethod("GetActiveFolderPath", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
            {
                string result = method.Invoke(win, null) as string;
                if (!string.IsNullOrEmpty(result))
                    return result;
            }

            return "Assets/";
        }


        public static MethodInfo GetInternalMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static void OpenFolder(string folderPath, WinInstanceInfo info)
        {
            if (!info.isTwoColumn)
            {
                FocusAssetByPath(folderPath, info.editorWindow);
                return;
            }

            if (browserWinType == null)
                browserWinType = info.editorWindow.GetType();

            Object obj = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
            if (obj == null)
                return;

            if (showFolderContents == null)
                showFolderContents = GetInternalMethod(browserWinType, "ShowFolderContents");

            if (showFolderContents != null)
            {
                var paramType = showFolderContents.GetParameters()[0].ParameterType;

#if UNITY_6000_0_OR_NEWER
                if (paramType.Name.Contains("EntityId"))
                {
                    showFolderContents.Invoke(info.editorWindow, new object[] { obj.GetEntityId(), true });
                    return;
                }
#endif

                if (paramType == typeof(int))
                {
#pragma warning disable CS0618

                    showFolderContents.Invoke(info.editorWindow, new object[] { obj.GetEntityId(), true });
#pragma warning restore CS0618
                    return;
                }
            }

            AssetDatabase.OpenAsset(obj);
        }

        public static void FocusAssetByObj(Object asset, EditorWindow win)
        {
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorUtility.FocusProjectWindow();
                win.Focus();
            }
        }

        public static void FocusAssetByPath(string path, EditorWindow win)
        {
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);

            FocusAssetByObj(obj, win);
        }

    }


}
#endif
