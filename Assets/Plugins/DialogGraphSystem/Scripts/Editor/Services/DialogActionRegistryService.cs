using System.Collections.Generic;
using DialogSystem.Runtime.Definitions;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Editor registry service for reusable action definitions.
    /// </summary>
    public sealed class DialogActionRegistryService
    {
        private const string DefaultFolder = "Assets/DialogGraphSystem/Definitions/Actions";

        public IReadOnlyList<DialogActionSO> GetAllRegisteredDefinitions()
        {
            return DialogDefinitionRegistryUtility.LoadAllAssets<DialogActionSO>();
        }

        public DialogActionSO FindById(string actionId)
        {
            return DialogDefinitionRegistryUtility.FindById<DialogActionSO>(
                asset => asset.ActionID,
                actionId);
        }

        public IReadOnlyList<string> GetDuplicateIds()
        {
            return DialogDefinitionRegistryUtility.FindDuplicateIds(
                GetAllRegisteredDefinitions(),
                asset => asset.ActionID);
        }

        public DialogActionSO CreateNewAsset(string defaultFileName = "DialogAction")
        {
            return DialogDefinitionRegistryUtility.CreateAsset<DialogActionSO>(DefaultFolder, defaultFileName);
        }
    }
}
