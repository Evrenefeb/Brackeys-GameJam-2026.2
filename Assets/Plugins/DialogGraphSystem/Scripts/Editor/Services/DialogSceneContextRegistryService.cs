using System.Collections.Generic;
using DialogSystem.Runtime.Definitions;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Editor registry service for reusable scene context definitions.
    /// </summary>
    public sealed class DialogSceneContextRegistryService
    {
        private const string DefaultFolder = "Assets/DialogGraphSystem/Definitions/SceneContexts";

        public IReadOnlyList<DialogSceneContextSO> GetAllRegisteredDefinitions()
        {
            return DialogDefinitionRegistryUtility.LoadAllAssets<DialogSceneContextSO>();
        }

        public DialogSceneContextSO CreateNewAsset(string defaultFileName = "DialogSceneContext")
        {
            return DialogDefinitionRegistryUtility.CreateAsset<DialogSceneContextSO>(DefaultFolder, defaultFileName);
        }
    }
}
