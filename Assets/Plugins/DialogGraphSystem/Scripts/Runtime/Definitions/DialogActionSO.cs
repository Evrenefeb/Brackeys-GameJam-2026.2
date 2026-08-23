using UnityEngine;

namespace DialogSystem.Runtime.Definitions
{
    /// <summary>
    /// Reusable action definition that can be referenced by dialog nodes or scene context.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Dialogue Action",
        menuName = "Beka Forge/Dialogues/Action",
        order = 11)]
    public class DialogActionSO : ScriptableObject
    {
        #region ---------------- Inspector ----------------

        [Header("Identity")]
        [Tooltip("Stable identifier used to reference this action.")]
        [SerializeField] private string actionID = "ActionID";

        [Tooltip("Human-readable label for editor UIs.")]
        [SerializeField] private string displayName = "Action";

        [Header("Description")]
        [Tooltip("Short explanation of what this action does.")]
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Header("Defaults")]
        [Tooltip("Default JSON payload when this action is inserted.")]
        [TextArea(2, 5)]
        [SerializeField] private string defaultPayloadJson = "{}";

        [Tooltip("Whether the runtime should wait for this action to complete.")]
        [SerializeField] private bool waitForCompletion = true;

        [Tooltip("Default delay before this action runs.")]
        [SerializeField] private float defaultDelay;

        #endregion

        #region ---------------- Properties ----------------

        public string ActionID
        {
            get => actionID;
            set => actionID = value;
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

        public string DefaultPayloadJson
        {
            get => defaultPayloadJson;
            set => defaultPayloadJson = string.IsNullOrWhiteSpace(value) ? "{}" : value;
        }

        public bool WaitForCompletion
        {
            get => waitForCompletion;
            set => waitForCompletion = value;
        }

        public float DefaultDelay
        {
            get => defaultDelay;
            set => defaultDelay = value;
        }

        #endregion

        #region ---------------- Validation ----------------

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(actionID))
            {
                error = $"DialogActionSO '{name}' has empty ActionID.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        #endregion
    }
}
