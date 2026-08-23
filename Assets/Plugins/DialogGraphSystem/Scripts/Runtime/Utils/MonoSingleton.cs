using UnityEngine;

namespace DialogSystem.Runtime.Utils
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null) _instance = DialogRuntimeUnityCompatibility.FindFirst<T>();
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
    }
}