using DialogSystem.Runtime.Core;
using TMPro;
using UnityEngine;

namespace DialogSystem.Runtime.Variables
{
    /// <summary>
    /// Small demo component for testing dialogue variable reads, writes, and change notifications.
    /// Keeps gameplay-to-dialogue sync and dialogue-to-game reactions separate to avoid feedback loops.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dialogue Graph System/Demos/Variable Showcase")]
    public sealed class DialogDemoVariableShowcase : MonoBehaviour
    {
        [Header("Optional Store Reference")]
        [SerializeField] private DialogueVariableStore variableStore;

        [Header("Variable Keys")]
        [SerializeField] private string playerNameKey = "playerName";
        [SerializeField] private string goldKey = "gold";
        [SerializeField] private string hasKeyKey = "hasKey";
        [SerializeField] private string reputationKey = "reputation";
        [SerializeField] private string trustLevelKey = "trustLevel";
        [SerializeField] private string temperatureKey = "temperature";
        [SerializeField] private string questStateKey = "questState";

        [Header("Demo State")]
        [SerializeField] private string playerName = "Alex";
        [SerializeField] private int gold = 20;
        [SerializeField] private bool hasKey;
        [SerializeField] private int reputation = 1;
        [SerializeField] private bool trustLevel;
        [SerializeField] private float temperature = 96f;
        [SerializeField] private string questState = "standby";
        [SerializeField] private bool pushValuesOnStart = true;

        [Header("Demo Controls")]
        [SerializeField] private int bonusGoldAmount = 10;
        [SerializeField] private int keyPrice = 10;
        [SerializeField] private int reputationStep = 1;

        [Header("Optional Debug UI")]
        [SerializeField] private TextMeshProUGUI statusLabel;

        private void Awake()
        {
            ResolveStore();
        }

        private void OnEnable()
        {
            if (ResolveStore())
            {
                variableStore.VariableChanged += OnVariableChanged;
            }
        }

        private void Start()
        {
            if (!ResolveStore())
            {
                return;
            }

            if (pushValuesOnStart)
            {
                PushAllToStore();
            }
            else
            {
                PullAllFromStore();
                RefreshStatusLabel();
            }
        }

        private void OnDisable()
        {
            if (variableStore != null)
            {
                variableStore.VariableChanged -= OnVariableChanged;
            }
        }

        /// <summary>
        /// Pushes the demo player name into the dialogue store.
        /// </summary>
        public void PushPlayerNameToStore()
        {
            if (!ResolveStore() || string.IsNullOrWhiteSpace(playerNameKey))
            {
                return;
            }

            variableStore.SetString(playerNameKey, playerName);
            RefreshStatusLabel();
        }

        /// <summary>
        /// Pushes the demo gold value into the dialogue store.
        /// Call this when your gameplay gold changes.
        /// </summary>
        public void PushGoldToStore()
        {
            if (!ResolveStore() || string.IsNullOrWhiteSpace(goldKey))
            {
                return;
            }

            variableStore.SetInt(goldKey, gold);
            RefreshStatusLabel();
        }

        /// <summary>
        /// Pushes the demo key state into the dialogue store.
        /// Call this when your gameplay key state changes.
        /// </summary>
        public void PushHasKeyToStore()
        {
            if (!ResolveStore() || string.IsNullOrWhiteSpace(hasKeyKey))
            {
                return;
            }

            variableStore.SetBool(hasKeyKey, hasKey);
            RefreshStatusLabel();
        }

        /// <summary>
        /// Pushes the demo reputation value into the dialogue store.
        /// </summary>
        public void PushReputationToStore()
        {
            if (!ResolveStore() || string.IsNullOrWhiteSpace(reputationKey))
            {
                return;
            }

            variableStore.SetInt(reputationKey, reputation);
            RefreshStatusLabel();
        }

        /// <summary>Pushes the demo trust-clearance state into the dialogue store.</summary>
        public void PushTrustLevelToStore()
        {
            if (!ResolveStore() || string.IsNullOrWhiteSpace(trustLevelKey))
            {
                return;
            }

            variableStore.SetBool(trustLevelKey, trustLevel);
            RefreshStatusLabel();
        }

        /// <summary>Pushes the current reactor temperature into the dialogue store.</summary>
        public void PushTemperatureToStore()
        {
            if (!ResolveStore() || string.IsNullOrWhiteSpace(temperatureKey))
            {
                return;
            }

            variableStore.SetFloat(temperatureKey, temperature);
            RefreshStatusLabel();
        }

        /// <summary>Pushes the current showcase route state into the dialogue store.</summary>
        public void PushQuestStateToStore()
        {
            if (!ResolveStore() || string.IsNullOrWhiteSpace(questStateKey))
            {
                return;
            }

            variableStore.SetString(questStateKey, questState);
            RefreshStatusLabel();
        }

        /// <summary>
        /// Pushes all demo values into the dialogue store.
        /// </summary>
        public void PushAllToStore()
        {
            PushPlayerNameToStore();
            PushGoldToStore();
            PushHasKeyToStore();
            PushReputationToStore();
            PushTrustLevelToStore();
            PushTemperatureToStore();
            PushQuestStateToStore();
        }

        /// <summary>
        /// Pulls all known values from the dialogue store into the local demo fields.
        /// </summary>
        public void PullAllFromStore()
        {
            if (!ResolveStore())
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(playerNameKey))
            {
                playerName = variableStore.GetString(playerNameKey, playerName);
            }

            if (!string.IsNullOrWhiteSpace(goldKey))
            {
                gold = variableStore.GetInt(goldKey, gold);
            }

            if (!string.IsNullOrWhiteSpace(hasKeyKey))
            {
                hasKey = variableStore.GetBool(hasKeyKey, hasKey);
            }

            if (!string.IsNullOrWhiteSpace(reputationKey))
            {
                reputation = variableStore.GetInt(reputationKey, reputation);
            }

            if (!string.IsNullOrWhiteSpace(trustLevelKey))
            {
                trustLevel = variableStore.GetBool(trustLevelKey, trustLevel);
            }

            if (!string.IsNullOrWhiteSpace(temperatureKey))
            {
                temperature = variableStore.GetFloat(temperatureKey, temperature);
            }

            if (!string.IsNullOrWhiteSpace(questStateKey))
            {
                questState = variableStore.GetString(questStateKey, questState);
            }

            RefreshStatusLabel();
        }

        /// <summary>
        /// Resets the showcase values and writes them to the dialogue store.
        /// Useful for replaying the variables/conditions demo from a clean state.
        /// </summary>
        public void ResetDemoVariables()
        {
            playerName = "Alex";
            gold = 20;
            hasKey = false;
            reputation = 1;
            trustLevel = false;
            temperature = 96f;
            questState = "standby";
            PushAllToStore();
        }

        /// <summary>Sets whether the Shop Gate key route is available.</summary>
        public void SetHasKey(bool value)
        {
            hasKey = value;
            PushHasKeyToStore();
        }

        /// <summary>Sets whether the Shop Gate trust route is available.</summary>
        public void SetTrustLevel(bool value)
        {
            trustLevel = value;
            PushTrustLevelToStore();
        }

        /// <summary>
        /// Adds bonus gold and writes the new value to the dialogue store.
        /// </summary>
        public void GrantBonusGold()
        {
            gold += Mathf.Max(0, bonusGoldAmount);
            PushGoldToStore();
        }

        /// <summary>
        /// Spends the configured key price if enough gold is available.
        /// </summary>
        public void BuyKeyIfAffordable()
        {
            if (gold < keyPrice)
            {
                RefreshStatusLabel();
                return;
            }

            gold -= Mathf.Max(0, keyPrice);
            hasKey = true;
            PushGoldToStore();
            PushHasKeyToStore();
        }

        /// <summary>
        /// Toggles the local key state and writes it to the dialogue store.
        /// </summary>
        public void ToggleHasKey()
        {
            hasKey = !hasKey;
            PushHasKeyToStore();
        }

        /// <summary>
        /// Raises reputation and writes it to the dialogue store.
        /// </summary>
        public void IncreaseReputation()
        {
            reputation += Mathf.Max(1, reputationStep);
            PushReputationToStore();
        }

        /// <summary>
        /// Lowers reputation and writes it to the dialogue store.
        /// </summary>
        public void DecreaseReputation()
        {
            reputation = Mathf.Max(0, reputation - Mathf.Max(1, reputationStep));
            PushReputationToStore();
        }

        private void OnVariableChanged(string key, DialogueVariableValueType type)
        {
            if (string.Equals(key, playerNameKey, System.StringComparison.Ordinal))
            {
                playerName = variableStore.GetString(playerNameKey, playerName);
            }
            else if (string.Equals(key, goldKey, System.StringComparison.Ordinal))
            {
                gold = variableStore.GetInt(goldKey, gold);
            }
            else if (string.Equals(key, hasKeyKey, System.StringComparison.Ordinal))
            {
                hasKey = variableStore.GetBool(hasKeyKey, hasKey);
            }
            else if (string.Equals(key, reputationKey, System.StringComparison.Ordinal))
            {
                reputation = variableStore.GetInt(reputationKey, reputation);
            }
            else if (string.Equals(key, trustLevelKey, System.StringComparison.Ordinal))
            {
                trustLevel = variableStore.GetBool(trustLevelKey, trustLevel);
            }
            else if (string.Equals(key, temperatureKey, System.StringComparison.Ordinal))
            {
                temperature = variableStore.GetFloat(temperatureKey, temperature);
            }
            else if (string.Equals(key, questStateKey, System.StringComparison.Ordinal))
            {
                questState = variableStore.GetString(questStateKey, questState);
            }

            RefreshStatusLabel();
        }

        private bool ResolveStore()
        {
            if (variableStore != null)
            {
                return true;
            }

            variableStore = GetComponent<DialogueVariableStore>();
            if (variableStore != null)
            {
                return true;
            }

            return DialogManager.Instance != null && DialogManager.Instance.TryGetVariableStore(out variableStore);
        }

        private void RefreshStatusLabel()
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text =
                $"Player: {playerName}\n" +
                $"Gold: {gold}\n" +
                $"Has Key: {hasKey}\n" +
                $"Reputation: {reputation}\n" +
                $"Trust: {trustLevel}\n" +
                $"Temperature: {temperature:0.#}\n" +
                $"State: {questState}";
        }
    }
}
