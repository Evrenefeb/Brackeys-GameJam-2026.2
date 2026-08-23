using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DialogSystem.Runtime.Definitions
{
    /// <summary>
    /// Reusable environment definition for dialog scene authoring.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Dialog Environment",
        menuName = "Beka Forge/Dialogues/Environment",
        order = 12)]
    public class DialogEnvironmentSO : ScriptableObject
    {
        #region ---------------- Inspector ----------------

        [Header("Identity")]
        [Tooltip("Stable identifier used to reference this environment.")]
        [SerializeField] private string environmentID = "environment_id";

        [Tooltip("Human-readable environment name.")]
        [FormerlySerializedAs("environmentName")]
        [SerializeField] private string displayName = "Environment";

        [Header("Description")]
        [Tooltip("Short description of the location or setup.")]
        [TextArea(2, 5)]
        [SerializeField] private string description;

        [Tooltip("Atmosphere of the scene such as tense, warm, or eerie.")]
        [FormerlySerializedAs("mood")]
        [SerializeField] private string atmosphere = "Neutral";

        [Tooltip("World or canon rules that apply in this environment.")]
        [TextArea(2, 5)]
        [SerializeField] private string canonRules;

        [Tooltip("Default tone to use when no scene-specific tone overrides it.")]
        [SerializeField] private string defaultTone = "Neutral";

        [Header("Legacy Context")]
        [Tooltip("Optional time-of-day detail preserved for existing assets.")]
        [FormerlySerializedAs("timeOfDay")]
        [SerializeField] private string timeOfDay = "Day";

        [Tooltip("Optional environment tags preserved for existing assets.")]
        [FormerlySerializedAs("tags")]
        [SerializeField] private string[] tags = Array.Empty<string>();

        #endregion

        #region ---------------- Properties ----------------

        public string EnvironmentID
        {
            get => environmentID;
            set => environmentID = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        public string Description
        {
            get => description;
            set => description = value;
        }

        public string Atmosphere
        {
            get => atmosphere;
            set => atmosphere = value;
        }

        public string CanonRules
        {
            get => canonRules;
            set => canonRules = value;
        }

        public string DefaultTone
        {
            get => defaultTone;
            set => defaultTone = value;
        }

        public string TimeOfDay
        {
            get => timeOfDay;
            set => timeOfDay = value;
        }

        public string[] Tags
        {
            get => tags;
            set => tags = value ?? Array.Empty<string>();
        }

        public string EnvironmentName
        {
            get => displayName;
            set => displayName = value;
        }

        public string Mood
        {
            get => atmosphere;
            set => atmosphere = value;
        }

        #endregion

        #region ---------------- Validation ----------------

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(environmentID))
            {
                error = $"DialogEnvironmentSO '{name}' has empty EnvironmentID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = $"DialogEnvironmentSO '{name}' has empty DisplayName.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        #endregion
    }
}
