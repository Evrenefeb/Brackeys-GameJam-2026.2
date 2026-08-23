// Copyright (c) 2025 PinePie. All rights reserved.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PinePie.PieTabs
{
    public static class IconsManager
    {
        public static Texture2D Star => Get("Favorite");

        public static Texture2D GameObject => Get("GameObject Icon");
        public static Texture2D Prefab => Get("Prefab Icon");

        public static Texture2D Folder => Get("d_Folder Icon");
        public static Texture2D Scene => Get("SceneAsset Icon");
        public static Texture2D Material => Get("Material Icon");
        public static Texture2D Shader => Get("Shader Icon");
        public static Texture2D Script => Get("cs Script Icon");

        public static Texture2D Get(string name)
        {
            return EditorGUIUtility.IconContent(name).image as Texture2D;
        }
    }
}

#endif