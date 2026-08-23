using UnityEngine;

namespace DialogSystem.Runtime.Utils
{
    /// <summary>
    /// Centralizes Unity API version differences used by runtime code.
    /// </summary>
    public static class DialogRuntimeUnityCompatibility
    {
        public static T FindFirst<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        public static T FindFirst<T>(bool includeInactive) where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindFirstObjectByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
            return Object.FindObjectOfType<T>(includeInactive);
#endif
        }
    }
}
