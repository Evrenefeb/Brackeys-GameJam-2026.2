// Copyright (c) 2025 PinePie. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UIElements;

namespace PinePie.PieTabs
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    public class SceneTabsData : MonoBehaviour
    {
        public List<SceneTab> tabs = new();

#if UNITY_EDITOR
        private void OnEnable()
        {
            gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
        }
#endif
    }

    // scene tab
    [Serializable]
    public class SceneTab
    {
        public int id;

        public int iconIndex;
        public int colorIndex;

        public List<GameObject> refs = new();

        public SceneTab(int id)
        {
            this.id = id;
        }

#if UNITY_EDITOR
        public VisualElement UIButton;
#endif
    }

}