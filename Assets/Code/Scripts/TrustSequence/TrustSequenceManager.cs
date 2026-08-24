using System;
using UnityEngine;

public class TrustSequenceManager : MonoBehaviour
{
    
    [SerializeField] private GameManager2 p_GameManager;
    [SerializeField] private TrustSequenceArgs p_SequenceArgs;


    private void OnEnable() {
        StartTrustSequence();
    }

    public void StartTrustSequence() {
        Debug.Log("Starting Trust Sequence: " + p_SequenceArgs.p_SequenceName);

        // Setup AI
        SetupAI();

        // Setup Player Upgrades
        SetupPlayerUpgrades();

        // Start First Round
        StartFirstRound();
    }

    

    private void SetupAI() {
        Debug.Log("Setting up AI");
    }

    private void SetupPlayerUpgrades() {
        Debug.Log("Setting up Player Upgrades");

    }

    private void StartFirstRound() {
        Debug.Log("Start First Round");
    }
}
