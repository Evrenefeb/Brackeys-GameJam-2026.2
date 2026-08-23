using System.Collections.Generic;
using DialogSystem.Runtime.Definitions;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Editor registry service for reusable environment definitions.
    /// </summary>
    public sealed class DialogEnvironmentRegistryService
    {
        private const string DefaultFolder = "Assets/DialogGraphSystem/Definitions/Environments";

        public IReadOnlyList<DialogEnvironmentSO> GetAllRegisteredDefinitions()
        {
            return DialogDefinitionRegistryUtility.LoadAllAssets<DialogEnvironmentSO>();
        }

        public DialogEnvironmentSO FindById(string environmentId)
        {
            return DialogDefinitionRegistryUtility.FindById<DialogEnvironmentSO>(
                asset => asset.EnvironmentID,
                environmentId);
        }

        public IReadOnlyList<string> GetDuplicateIds()
        {
            return DialogDefinitionRegistryUtility.FindDuplicateIds(
                GetAllRegisteredDefinitions(),
                asset => asset.EnvironmentID);
        }

        public DialogEnvironmentSO CreateNewAsset(string defaultFileName = "DialogEnvironment")
        {
            return DialogDefinitionRegistryUtility.CreateAsset<DialogEnvironmentSO>(DefaultFolder, defaultFileName);
        }
    }
}
