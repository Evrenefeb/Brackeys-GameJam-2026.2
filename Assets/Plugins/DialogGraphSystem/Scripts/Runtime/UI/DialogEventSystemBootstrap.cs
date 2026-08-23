using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Utils;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace DialogSystem.Runtime.UI
{
    /// <summary>
    /// Ensures dialogue scenes use the correct EventSystem input module for the
    /// active Unity input handling mode without requiring manual scene fixes.
    /// </summary>
    internal static class DialogEventSystemBootstrap
    {
        private static Type _inputSystemUiModuleType;
        private static bool _checkedInputSystemUiModuleType;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureConfiguredForActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureConfiguredForActiveScene();
        }

        private static void EnsureConfiguredForActiveScene()
        {
            if (!SceneNeedsDialogueEventSystem())
                return;

            var eventSystem = DialogRuntimeUnityCompatibility.FindFirst<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            EnsureInputSystemModule(eventSystem);
#else
            EnsureStandaloneModule(eventSystem);
#endif
        }

        private static bool SceneNeedsDialogueEventSystem()
        {
            return DialogRuntimeUnityCompatibility.FindFirst<DialogManager>() != null
                || DialogRuntimeUnityCompatibility.FindFirst<DialogUIController>() != null
                || DialogRuntimeUnityCompatibility.FindFirst<DialogPlayer>() != null;
        }

        private static void EnsureStandaloneModule(EventSystem eventSystem)
        {
            var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneModule == null)
                standaloneModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();

            standaloneModule.enabled = true;

            var inputSystemModule = GetInputSystemModule(eventSystem);
            if (inputSystemModule != null)
                inputSystemModule.enabled = false;
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnsureInputSystemModule(EventSystem eventSystem)
        {
            var inputSystemModule = GetInputSystemModule(eventSystem);
            if (inputSystemModule == null)
                inputSystemModule = AddInputSystemModule(eventSystem);

            if (inputSystemModule == null)
            {
                Debug.LogWarning("Dialogue Graph System could not find InputSystemUIInputModule. Install or enable the Unity Input System package, or switch Active Input Handling to Both/Old.");
                return;
            }

            inputSystemModule.enabled = true;

            var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneModule != null)
                standaloneModule.enabled = false;
        }
#endif

        private static Behaviour GetInputSystemModule(EventSystem eventSystem)
        {
            var moduleType = GetInputSystemUiModuleType();
            if (moduleType == null)
                return null;

            return eventSystem.GetComponent(moduleType) as Behaviour;
        }

        private static Behaviour AddInputSystemModule(EventSystem eventSystem)
        {
            var moduleType = GetInputSystemUiModuleType();
            if (moduleType == null)
                return null;

            return eventSystem.gameObject.AddComponent(moduleType) as Behaviour;
        }

        private static Type GetInputSystemUiModuleType()
        {
            if (_checkedInputSystemUiModuleType)
                return _inputSystemUiModuleType;

            _checkedInputSystemUiModuleType = true;
            _inputSystemUiModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            return _inputSystemUiModuleType;
        }
    }
}
