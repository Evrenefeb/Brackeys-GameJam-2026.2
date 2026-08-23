// Copyright (c) 2025 PinePie. All rights reserved.

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PinePie.PieTabs
{
    public static class GlobalUtils
    {
        // loader
        public static VisualTreeAsset LoadUXML(string relativePath) =>
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{PathUtility.GetPieTabsPath()}/PinePie/PieTabs/editor/Core/UI/{relativePath}");

        public static Texture2D LoadTex(string relativePath) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{PathUtility.GetPieTabsPath()}/PinePie/PieTabs/editor/Core/UI/{relativePath}");

        // color setup helpers
        public static bool IsColorDark(Color color)
        {
            float brightness = (color.r * 0.299f) + (color.g * 0.587f) + (color.b * 0.114f);
            return brightness < 0.5f;
        }

        public static string ColorToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGBA(color);
        }

        public static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return HexToColor("#3E3E3E");

            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            hex = hex.ToUpperInvariant();

            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;

            return HexToColor("#3E3E3E");
        }

        public static PieAssetType GetAssetType(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return PieAssetType.Folder;

            Type t = AssetDatabase.GetMainAssetTypeAtPath(path);

            if (t == typeof(SceneAsset))
                return PieAssetType.Scene;

            if (t == typeof(GameObject))
                return PieAssetType.Prefab;

            if (t == typeof(MonoScript))
                return PieAssetType.Script;

            if (t.FullName == "UnityEditor.ShaderGraph.GraphData" ||
                t.FullName?.Contains("ShaderGraph") == true)
                return PieAssetType.ShaderGraph;

            if (t.FullName?.Contains("VisualScripting") == true)
                return PieAssetType.VisualScriptingGraph;

            return PieAssetType.Other;
        }

    }

    public enum PieAssetType
    {
        Folder,
        Scene,
        Prefab,
        Script,
        ShaderGraph,
        VisualScriptingGraph,
        Other
    }
}

#endif