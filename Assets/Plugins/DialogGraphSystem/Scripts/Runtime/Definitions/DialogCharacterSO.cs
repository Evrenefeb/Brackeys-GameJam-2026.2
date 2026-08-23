using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DialogSystem.Runtime.Definitions
{
    /// <summary>
    /// Reusable character definition for dialog graphs and scene context assets.
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Dialogue Character",
        menuName = "Beka Forge/Dialogues/Character",
        order = 10)]
    public class DialogCharacterSO : ScriptableObject
    {
        #region ---------------- Inspector ----------------

        [Header("Identity")]
        [Tooltip("Stable identifier used to reference this character.")]
        [SerializeField] private string characterID = "character_id";

        [Tooltip("Display name shown in editor and runtime UIs.")]
        [SerializeField] private string displayName = "Character";

        [Header("Visuals")]
        [Tooltip("Optional portrait sprite for runtime or editor previews.")]
        [SerializeField] private Sprite portrait;

        [Header("Writing Context")]
        [Tooltip("Short summary of who this character is.")]
        [FormerlySerializedAs("personality")]
        [TextArea(2, 5)]
        [SerializeField] private string shortDescription;

        [Tooltip("Optional personality descriptors such as sarcastic, brave, or careful.")]
        [FormerlySerializedAs("traits")]
        [SerializeField] private string[] personalityTraits = Array.Empty<string>();

        [Tooltip("Speech pattern guidance such as formal, clipped, or slang-heavy.")]
        [FormerlySerializedAs("speakingStyle")]
        [SerializeField] private string speechStyle;

        [Tooltip("Optional sample lines that capture this character's voice.")]
        [SerializeField] private List<string> exampleLines = new List<string>();

        #endregion

        #region ---------------- Properties ----------------

        public string CharacterID
        {
            get => characterID;
            set => characterID = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        public Sprite Portrait
        {
            get => portrait;
            set => portrait = value;
        }

        public string ShortDescription
        {
            get => shortDescription;
            set => shortDescription = value;
        }

        public string[] PersonalityTraits
        {
            get => personalityTraits;
            set => personalityTraits = value ?? Array.Empty<string>();
        }

        public string SpeechStyle
        {
            get => speechStyle;
            set => speechStyle = value;
        }

        public List<string> ExampleLines => exampleLines;

        public string Personality
        {
            get => shortDescription;
            set => shortDescription = value;
        }

        public string[] Traits
        {
            get => personalityTraits;
            set => personalityTraits = value ?? Array.Empty<string>();
        }

        public string SpeakingStyle
        {
            get => speechStyle;
            set => speechStyle = value;
        }

        #endregion

        #region ---------------- Validation ----------------

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(characterID))
            {
                error = $"DialogCharacterSO '{name}' has empty CharacterID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = $"DialogCharacterSO '{name}' has empty DisplayName.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        #endregion
    }
}
