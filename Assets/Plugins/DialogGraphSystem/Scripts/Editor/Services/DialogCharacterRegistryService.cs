using System.Collections.Generic;
using DialogSystem.Runtime.Definitions;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Editor registry service for reusable character definitions.
    /// </summary>
    public sealed class DialogCharacterRegistryService
    {
        private const string DefaultFolder = "Assets/DialogGraphSystem/Definitions/Characters";

        public IReadOnlyList<DialogCharacterSO> GetAllRegisteredDefinitions()
        {
            return DialogDefinitionRegistryUtility.LoadAllAssets<DialogCharacterSO>();
        }

        public DialogCharacterSO FindById(string characterId)
        {
            return DialogDefinitionRegistryUtility.FindById<DialogCharacterSO>(
                asset => asset.CharacterID,
                characterId);
        }

        public IReadOnlyList<string> GetDuplicateIds()
        {
            return DialogDefinitionRegistryUtility.FindDuplicateIds(
                GetAllRegisteredDefinitions(),
                asset => asset.CharacterID);
        }

        public DialogCharacterSO CreateNewAsset(string defaultFileName = "DialogCharacter")
        {
            return DialogDefinitionRegistryUtility.CreateAsset<DialogCharacterSO>(DefaultFolder, defaultFileName);
        }
    }
}
