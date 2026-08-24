using ImprovedTimers;
using Lean.Gui;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

public class TrustSequenceManager : MonoBehaviour
{
    
    [SerializeField] private GameManager2 p_GameManager;
    [SerializeField] private TrustSequenceArgs p_SequenceArgs;

    private LeanButton m_PlayerButton;

    
    [SerializeField] [ReadOnly] private TrustSequenceRound m_CurrentRound;
    [SerializeField] [ReadOnly] private TrustSequenceState m_State = TrustSequenceState.Init;
    [SerializeField] [ReadOnly] private TrustSequenceOverState m_OverState = TrustSequenceOverState.None;
    [SerializeField] private int m_CurrentRoundIndex;
    [SerializeField] [ReadOnly] private int m_MaxRounds;
    
    private CountdownTimer m_RoundTimer;
    private CountdownTimer m_SlackTimer;


    private void OnEnable() {
        StartTrustSequence();
    }

    private void OnDisable() {
        if (m_RoundTimer != null) {
            m_RoundTimer.OnTimerStart -= RoundTimer_OnStart;
            m_RoundTimer.OnTimerStop -= RoundTimer_OnStop;
        }

        if (m_SlackTimer != null) {
            m_SlackTimer.OnTimerStart -= SlackTimer_OnStart;
            m_SlackTimer.OnTimerStop -= SlackTimer_OnStop;
        }

        if (m_PlayerButton != null) {
            m_PlayerButton.OnClick.RemoveListener(PlayerButton_OnClick);
            m_PlayerButton.OnClick.RemoveListener(PlayerButton_OnDown);
        }
        
    }

    public void StartTrustSequence() {
        Debug.Log("Starting Trust Sequence: " + p_SequenceArgs.p_SequenceName);

        // Setup Args
        SetupArgs();

        // Setup Timers
        SetupTimers();

        // Setup AI
        SetupAI();

        // Setup Player Upgrades
        SetupPlayer();

        // Start First Round
        StartFirstRound();
    }

    

    private void SetupArgs() {
        //Debug.Log("Setting up Args");

        m_CurrentRound = new TrustSequenceRound(p_SequenceArgs.p_InitialPot);

        m_CurrentRoundIndex = 0;
        m_MaxRounds = p_SequenceArgs.p_MaxRounds;
    }

    private void SetupTimers() {
        //Debug.Log("Setting up Timers");

        m_RoundTimer = new CountdownTimer(p_SequenceArgs.p_RoundTime);
        m_RoundTimer.OnTimerStart += RoundTimer_OnStart;
        m_RoundTimer.OnTimerStop += RoundTimer_OnStop;

        m_SlackTimer = new CountdownTimer(p_SequenceArgs.p_SlackTime);
        m_SlackTimer.OnTimerStart += SlackTimer_OnStart;
        m_SlackTimer.OnTimerStop += SlackTimer_OnStop;

        
    }

    

    private void SetupAI() {
        //Debug.Log("Setting up AI");
    }

    private void SetupPlayer() {
        //Debug.Log("Setting up Player Upgrades");

        m_PlayerButton = p_GameManager.PlayerButton;
        m_PlayerButton.OnClick.AddListener(PlayerButton_OnClick);
        m_PlayerButton.OnClick.AddListener(PlayerButton_OnDown);
    }

    

    private void PlayerButton_OnClick() {
        

        ForceEndTrustSequence();
    }


    private void ForceEndTrustSequence() {
        Debug.Log("Force End Trust Sequence for Player");
        Debug.Log("Move to Upgrade Sequence");

        // Reevalute Current Quota
        p_GameManager.ChangeCurrentQuota(m_CurrentRound.CurrentPot, QuotaChangeMode.ADD);

        // Switch to Upgrade Sequence
        p_GameManager.ChangeToUpgradeSequence();
    }

    private void PlayerButton_OnDown() {
        Debug.Log("PlayerButton_OnDown");

    }
    private void StartFirstRound() {
        //Debug.Log("Start First Round");

        m_SlackTimer.Start();
    }





    #region Timers

    private void RoundTimer_OnStop() {
        Debug.Log("Round Timer Ended");

        m_RoundTimer.Reset();

        // Check if the sequence is over
        bool sequenceOver = CheckSequenceOverState(out m_OverState);

        if (sequenceOver) {
            Debug.Log("Sequence Over");
            return;
        }

        SetupNextRound();

        m_SlackTimer.Start();
    }

    private void RoundTimer_OnStart() {
        Debug.Log("Round Timer Started");

        m_State = TrustSequenceState.OnGoing;
        m_CurrentRoundIndex++;
    }


    private void SlackTimer_OnStop() {
        Debug.Log("Slack Timer Ended");

        m_SlackTimer.Reset();

        m_RoundTimer.Start();
    }

    private void SlackTimer_OnStart() {
        Debug.Log("Slack Timer Started");

        m_State = TrustSequenceState.OnSlack;
    }

    #endregion


    private void SetupNextRound() {
        m_CurrentRound.SetupNextRound(p_SequenceArgs.p_RoundPotMultiplier);
    }

    private bool CheckSequenceOverState(out TrustSequenceOverState endState) {

        if(m_CurrentRoundIndex >= m_MaxRounds) {
            endState = TrustSequenceOverState.BothWin;
            return true;
        }

        endState = TrustSequenceOverState.None;
        return false;
    }
}
