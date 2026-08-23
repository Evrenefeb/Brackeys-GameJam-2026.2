using ImprovedTimers;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

public class GameManager : MonoBehaviour {



    [SerializeField] private GameManagerSettings r_GameManagerSettings;
    [SerializeField] private TrustSequenceSettings r_TrustSequenceSettings;



    [SerializeField] private PlayerButton r_PlayerButton;




    [SerializeField][ReadOnly] private TrustSequence m_CurrentTrustSequence;




    private CountdownTimer m_TrustSequenceTimer;



    public CountdownTimer TrustSequenceTimer => m_TrustSequenceTimer;
    public TrustSequence CurrentTrustSequence => m_CurrentTrustSequence;




    #region Unity Methods

    // Unity Methods
    private void OnEnable() {
        Initialize();
    }

    #endregion

    #region Initialization

    private void Initialize() {
        m_TrustSequenceTimer = new CountdownTimer(r_GameManagerSettings.InitTrustSequenceTimerValue);

        m_TrustSequenceTimer.OnTimerStart += OnGameTrustSequenceTimerStart;
        m_TrustSequenceTimer.OnTimerStop += OnGameTrustSequenceTimerStop;

        
    }

    #endregion

    #region Timer Events

    public void OnGameTrustSequenceTimerStart() {

        m_CurrentTrustSequence ??= new TrustSequence(r_TrustSequenceSettings);

        m_TrustSequenceTimer.Reset();
        m_TrustSequenceTimer.Start();
    }

    public void OnGameTrustSequenceTimerStop() {
        if (m_TrustSequenceTimer.IsRunning) m_TrustSequenceTimer.Stop();

        CurrentTrustSequence.CallNextSequence();
    }

    #endregion

    public void Player_OnDown() {
        Debug.Log("Player_OnDown");
    }

    public void Player_OnClick() {
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
