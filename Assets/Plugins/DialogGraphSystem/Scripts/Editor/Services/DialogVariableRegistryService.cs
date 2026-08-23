using System.Collections.Generic;
using DialogSystem.Runtime.Variables;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Editor registry service for reusable dialogue variable definitions.
    /// </summary>
    public sealed class DialogVariableRegistryService
    {
        private const string DefaultFolder = "Assets/DialogGraphSystem/Definitions/Variables";

        /// <summary>
        /// Loads all registered dialogue variable definitions in the project.
        /// </summary>
        public IReadOnlyList<DialogVariableSO> GetAllRegisteredDefinitions()
        {
            return DialogDefinitionRegistryUtility.LoadAllAssets<DialogVariableSO>();
        }

        /// <summary>
        /// Finds a dialogue variable definition by runtime key.
        /// </summary>
        public DialogVariableSO FindByKey(string variableKey)
        {
            return DialogDefinitionRegistryUtility.FindById<DialogVariableSO>(
                asset => asset.Key,
                variableKey);
        }

        /// <summary>
        /// Creates a new dialogue variable definition asset in the default registry folder.
        /// </summary>
        public DialogVariableSO CreateNewAsset(string defaultFileName = "DialogVariable")
        {
            return DialogDefinitionRegistryUtility.CreateAsset<DialogVariableSO>(DefaultFolder, defaultFileName);
        }
    }
}
