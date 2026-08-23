using System.Collections.Generic;
using UnityEngine;

namespace DialogSystem.Runtime.Definitions
{
    /// <summary>
    /// Reusable scene context asset for authoring environment, characters, and action availability.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Dialog Scene Context",
        menuName = "Beka Forge/Dialogues/Scene Context",
        order = 13)]
    public class DialogSceneContextSO : ScriptableObject
    {
        #region ---------------- Inspector ----------------

        [Header("References")]
        [Tooltip("Environment definition for this scene.")]
        [SerializeField] private DialogEnvironmentSO environment;

        [Tooltip("Characters that can participate in this scene.")]
        [SerializeField] private List<DialogCharacterSO> participatingCharacters = new List<DialogCharacterSO>();

        [Tooltip("Actions that are available in this scene.")]
        [SerializeField] private List<DialogActionSO> availableActions = new List<DialogActionSO>();

        [Header("Scene Intent")]
        [Tooltip("What the scene should accomplish narratively.")]
        [TextArea(2, 5)]
        [SerializeField] private string sceneGoal;

        [Tooltip("Scene-level tone override.")]
        [SerializeField] private string tone = "Neutral";

        [Tooltip("Additional authoring rules or constraints for this scene.")]
        [TextArea(2, 5)]
        [SerializeField] private string extraRules;

        #endregion

        #region ---------------- Properties ----------------

        public DialogEnvironmentSO Environment
        {
            get => environment;
            set => environment = value;
        }

        public List<DialogCharacterSO> ParticipatingCharacters => participatingCharacters;

        public List<DialogActionSO> AvailableActions => availableActions;

        public string SceneGoal
        {
            get => sceneGoal;
            set => sceneGoal = value;
        }

        public string Tone
        {
            get => tone;
            set => tone = value;
        }

        public string ExtraRules
        {
            get => extraRules;
            set => extraRules = value;
        }

        #endregion

        #region ---------------- Validation ----------------

        public bool IsValid(out string error)
        {
            if (environment == null && (participatingCharacters == null || participatingCharacters.Count == 0))
            {
                error = $"DialogSceneContextSO '{name}' should reference an environment or at least one character.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        #endregion
    }
}
