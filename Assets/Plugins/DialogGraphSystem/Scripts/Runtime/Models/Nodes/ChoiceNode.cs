using System;
using System.Collections.Generic;
using DialogSystem.Runtime.Models;
using UnityEngine;
using UnityEngine.Events;

namespace DialogSystem.Runtime.Models.Nodes
{
    /// <summary>
    /// Presents a line of text and a list of selectable answers (choices).
    /// </summary>
    public class ChoiceNode : BaseNode
    {
        #region -------- Data --------
        [TextArea(2, 5)]
        public string text;

        [Tooltip("Stable locale key for the choice prompt text. " +
                 "Used by DialogLocalizationRuntime to resolve the active language at runtime.")]
        public string textLocaleKey;

        [Header("Choices")]
        public List<Choice> choices = new();
        #endregion

        /// <summary>
        /// Assigns stable IDs to choices that are missing them. Intended for explicit creation
        /// or migration paths, not passive graph loading.
        /// </summary>
        public int AssignMissingChoiceIds()
        {
            if (choices == null)
            {
                return 0;
            }

            var assignedCount = 0;
            foreach (var choice in choices)
            {
                if (choice == null || choice.HasChoiceId)
                {
                    continue;
                }

                choice.AssignChoiceIdIfMissing(Choice.CreateChoiceId());
                assignedCount++;
            }

            return assignedCount;
        }
    }

    [System.Serializable]
    public class Choice
    {
        [Tooltip("Stable identity for this choice output. Legacy choices may leave this blank until migrated or edited.")]
        public string choiceId;

        [Tooltip("Text shown for this answer.")]
        public string answerText;

        [Tooltip("Stable locale key for answerText. " +
                 "Used by DialogLocalizationRuntime to resolve the active language at runtime.")]
        public string answerTextLocaleKey;

        [Tooltip("GUID of the next node when this answer is picked.")]
        public string nextNodeGUID;

        [Tooltip("Optional UnityEvent fired when this choice is selected.")]
        public UnityEvent onSelected = new UnityEvent();
        public string tooltipOrSubLabel;

        public bool HasChoiceId => !string.IsNullOrWhiteSpace(choiceId);

        /// <summary>Returns true when a stable locale key is set for this choice's answer text.</summary>
        public bool HasAnswerLocaleKey => !string.IsNullOrWhiteSpace(answerTextLocaleKey);

        public string PortKey => DialogGraphPortKeys.ForChoiceId(choiceId);

        public void AssignChoiceIdIfMissing(string id)
        {
            if (HasChoiceId || string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            choiceId = id;
        }

        public static string CreateChoiceId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static Choice Create(string answerText = "", string nextNodeGuid = null, string tooltipOrSubLabel = null)
        {
            return new Choice
            {
                choiceId = CreateChoiceId(),
                answerText = answerText ?? string.Empty,
                nextNodeGUID = nextNodeGuid,
                tooltipOrSubLabel = tooltipOrSubLabel
            };
        }
    }
}
