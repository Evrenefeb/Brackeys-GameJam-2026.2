using ImprovedTimers;
using Lean.Gui;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

public class GameManager : MonoBehaviour {



    [SerializeField] private GameManagerSettings r_GameManagerSettings;
    [SerializeField] private TrustSequenceSettings r_TrustSequenceSettings;
    //[SerializeField] private UpgradeData r_UpgradeData;



    [SerializeField] private PlayerButton r_PlayerButton;




    [SerializeField][ReadOnly] private TrustSequence m_CurrentTrustSequence;
    [SerializeField][ReadOnly] private bool m_SequenceRunning;




    private CountdownTimer m_TrustSequenceTimer;
    private LeanButton m_LeanButton_Player;


    public CountdownTimer TrustSequenceTimer => m_TrustSequenceTimer;
    public TrustSequence CurrentTrustSequence => m_CurrentTrustSequence;




    #region Unity Methods

    // Unity Methods
    private void OnEnable() {
        SelfInitialization();
    }

    

    #endregion

    #region Initialization

    private void SelfInitialization() {

        // Setup Trust Sequence Timer
        m_TrustSequenceTimer = new CountdownTimer(r_GameManagerSettings.InitTrustSequenceTimerValue);

        m_TrustSequenceTimer.OnTimerStart += TrustSequenceTimer_OnStart;
        m_TrustSequenceTimer.OnTimerStop += TrustSequenceTimer_OnStop;

        // Setup Player Button
        m_LeanButton_Player = r_PlayerButton.gameObject.GetComponent<LeanButton>();
        m_LeanButton_Player.OnClick.AddListener(Player_OnClick);
        m_LeanButton_Player.OnDown.AddListener(Player_OnDown);
    }

    #endregion

    #region Timer Events

    public void TrustSequenceTimer_OnStart() {

        m_CurrentTrustSequence ??= new TrustSequence(r_TrustSequenceSettings);

        m_SequenceRunning = true;

        m_TrustSequenceTimer.Reset();
        m_TrustSequenceTimer.Start();
    }

    public void TrustSequenceTimer_OnStop() {
        if (m_TrustSequenceTimer.IsRunning) m_TrustSequenceTimer.Stop();

        m_SequenceRunning = false;

        CurrentTrustSequence.CallNextSequence();
    }

    #endregion

    public void Player_OnDown() {
        if(!m_SequenceRunning) return;

        Debug.Log("Player_OnDown");
    }

    public void Player_OnClick() {
        if (!m_SequenceRunning) return;

        Debug.Log("Player_OnPress");
    }




}

[Serializable]
public class TrustSequence : ITrustSequenceLike {

    private int m_PotIndex;
    private float m_CurrentPot;

    public int PotIndex => m_PotIndex;
    public float CurrentPot => m_CurrentPot;

    public TrustSequence(TrustSequenceSettings sequenceSettings) {
        CallFirstSequence(sequenceSettings.CurrentPot);
    }

    public event Action OnNextSequenceCalled;


    public void CallNextSequence() {

        m_PotIndex += 1;
        m_CurrentPot *= 2;

        OnNextSequenceCalled?.Invoke();
    }

    private void CallFirstSequence(float currentPot) {
        m_PotIndex = 0;
        m_CurrentPot = currentPot;

        OnNextSequenceCalled?.Invoke();
    }
}

public interface ITrustSequenceLike {
    public int PotIndex { get; }
    public float CurrentPot { get; }
}
