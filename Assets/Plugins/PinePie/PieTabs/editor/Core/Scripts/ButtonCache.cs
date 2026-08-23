// Copyright (c) 2025 PinePie. All rights reserved.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PinePie.PieTabs
{
    public static class ButtonCache<T> where T : IButtonData
    {
        static string DirPath => Path.Combine(Application.dataPath, "..", "ProjectSettings", "PieTabs");
        private static readonly string saveFilePath = $"{DirPath}/Data_{typeof(T).Name}.json";

        public static void Save(List<T> buttons)
        {
            string json = JsonUtility.ToJson(new Wrapper { items = buttons });

            string dir = Path.GetDirectoryName(saveFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(saveFilePath, json);
        }

        public static List<T> Load()
        {
            if (File.Exists(saveFilePath))
                return JsonUtility.FromJson<Wrapper>(File.ReadAllText(saveFilePath)).items;

            return new List<T>();
        }

        [System.Serializable]
        private class Wrapper
        {
            public List<T> items;
        }
    }
}


#endif