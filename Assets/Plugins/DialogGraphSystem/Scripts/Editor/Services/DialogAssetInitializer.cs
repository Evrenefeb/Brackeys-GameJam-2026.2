using DialogSystem.Runtime.Definitions;
using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Shared asset initialization logic used by manual create window,
    /// tab create buttons, and AI creation flow.
    /// Ensures consistent default metadata across all creation paths.
    /// </summary>
    public static class DialogAssetInitializer
    {
        #region ---------------- Character ----------------

        /// <summary>
        /// Initializes a <see cref="DialogCharacterSO"/> with the given parameters.
        /// Generates a stable <see cref="DialogCharacterSO.CharacterID"/> from the display name.
        /// </summary>
        public static void InitializeCharacter(
            DialogCharacterSO asset,
            string displayName,
            string description = null,
            string speechStyle = null,
            string[] personalityTraits = null)
        {
            if (asset == null) return;

            var name = string.IsNullOrWhiteSpace(displayName) ? "DialogCharacter" : displayName.Trim();

            asset.DisplayName = name;
            asset.CharacterID = DialogGraphDefinitionResolver.CreateSuggestedCharacterId(name);

            // Writing context defaults — match DialogCreateAssetWindow baseline
            asset.ShortDescription = !string.IsNullOrWhiteSpace(description)
                ? description.Trim()
                : $"A character named {name}.";

            asset.SpeechStyle = !string.IsNullOrWhiteSpace(speechStyle)
                ? speechStyle.Trim()
                : "Natural";

            asset.PersonalityTraits = personalityTraits != null && personalityTraits.Length > 0
                ? personalityTraits
                : System.Array.Empty<string>();
        }

        #endregion

        #region ---------------- Action ----------------

        /// <summary>
        /// Initializes a <see cref="DialogActionSO"/> with the given parameters.
        /// </summary>
        public static void InitializeAction(
            DialogActionSO asset,
            string actionLabel,
            string description = null)
        {
            if (asset == null) return;

            var label = string.IsNullOrWhiteSpace(actionLabel) ? "DialogAction" : actionLabel.Trim();

            asset.ActionID = DialogGraphDefinitionResolver.CreateSuggestedActionId(label);
            asset.DisplayName = label;
            asset.Description = !string.IsNullOrWhiteSpace(description) ? description.Trim() : string.Empty;
            asset.DefaultPayloadJson = "{}";
            asset.WaitForCompletion = true;
            asset.DefaultDelay = 0f;
        }

        #endregion

        #region ---------------- Environment ----------------

        /// <summary>
        /// Initializes a <see cref="DialogEnvironmentSO"/> with the given parameters.
        /// Generates a stable <see cref="DialogEnvironmentSO.EnvironmentID"/> from the display name.
        /// </summary>
        public static void InitializeEnvironment(
            DialogEnvironmentSO asset,
            string displayName,
            string description = null,
            string atmosphere = null,
            string tone = null)
        {
            if (asset == null) return;

            var name = string.IsNullOrWhiteSpace(displayName) ? "DialogEnvironment" : displayName.Trim();

            asset.DisplayName = name;
            asset.EnvironmentID = DialogGraphDefinitionResolver.CreateSuggestedEnvironmentId(name);

            asset.Description = !string.IsNullOrWhiteSpace(description)
                ? description.Trim()
                : $"An environment named {name}.";

            asset.Atmosphere = !string.IsNullOrWhiteSpace(atmosphere)
                ? atmosphere.Trim()
                : "Neutral";

            asset.DefaultTone = !string.IsNullOrWhiteSpace(tone)
                ? tone.Trim()
                : "Neutral";
        }

        #endregion

        #region ---------------- Scene Context ----------------

        /// <summary>
        /// Initializes a <see cref="DialogSceneContextSO"/> with the given parameters.
        /// </summary>
        public static void InitializeSceneContext(
            DialogSceneContextSO asset,
            string assetName,
            DialogEnvironmentSO environment = null,
            string sceneGoal = null,
            string tone = null,
            string extraRules = null)
        {
            if (asset == null) return;

            var name = string.IsNullOrWhiteSpace(assetName) ? "DialogSceneContext" : assetName.Trim();

            asset.name = name;
            asset.Environment = environment;

            asset.SceneGoal = !string.IsNullOrWhiteSpace(sceneGoal)
                ? sceneGoal.Trim()
                : $"A scene named {name}.";

            asset.Tone = !string.IsNullOrWhiteSpace(tone)
                ? tone.Trim()
                : "Neutral";

            asset.ExtraRules = !string.IsNullOrWhiteSpace(extraRules)
                ? extraRules.Trim()
                : string.Empty;
        }

        #endregion

        #region ---------------- Variable ----------------

        /// <summary>
        /// Initializes a <see cref="DialogVariableSO"/> with the given parameters.
        /// Generates a stable key from the variable label.
        /// </summary>
        public static void InitializeVariable(
            DialogVariableSO asset,
            string variableLabel,
            DialogueVariableValueType valueType = DialogueVariableValueType.Boolean,
            string defaultValue = null)
        {
            if (asset == null) return;

            var label = string.IsNullOrWhiteSpace(variableLabel) ? "DialogVariable" : variableLabel.Trim();

            asset.name = label;
            asset.SetKey(DialogGraphDefinitionResolver.CreateSuggestedVariableKey(label));

            switch (valueType)
            {
                case DialogueVariableValueType.Boolean:
                    asset.SetDefaultValue(bool.TryParse(defaultValue, out var b) && b);
                    break;
                case DialogueVariableValueType.Integer:
                    asset.SetDefaultValue(int.TryParse(defaultValue, out var i) ? i : 0);
                    break;
                case DialogueVariableValueType.Float:
                    asset.SetDefaultValue(float.TryParse(defaultValue,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f);
                    break;
                case DialogueVariableValueType.String:
                    asset.SetDefaultValue(defaultValue ?? string.Empty);
                    break;
            }
        }

        #endregion

        #region ---------------- Utility ----------------

        /// <summary>Registers undo, marks dirty, saves assets, and pings.</summary>
        public static void FinalizeAsset(ScriptableObject asset, string undoLabel, bool pingAsset = true)
        {
            if (asset == null) return;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            if (pingAsset)
            {
                DialogGraphDefinitionResolver.PingAndSelect(asset);
            }
        }

        #endregion
    }
}
