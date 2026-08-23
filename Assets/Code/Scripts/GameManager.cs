using ImprovedTimers;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{



    [SerializeField] private GameManagerSettings r_Settings;
    [SerializeField] [ReadOnly] private TrustSequence m_CurrentTrustSequence;

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
        m_TrustSequenceTimer = new CountdownTimer(r_Settings.InitTrustSequenceTimerValue);

        m_TrustSequenceTimer.OnTimerStart += OnGameTrustSequenceTimerStart;
        m_TrustSequenceTimer.OnTimerStop += OnGameTrustSequenceTimerStop;

        // Start New TrustSequence
        m_CurrentTrustSequence = new TrustSequence();
    }

    #endregion

    public void OnGameTrustSequenceTimerStart() {

        m_TrustSequenceTimer.Reset();
        m_TrustSequenceTimer.Start();
    }

    public void OnGameTrustSequenceTimerStop() {
        if(m_TrustSequenceTimer.IsRunning) m_TrustSequenceTimer.Stop();        

        CurrentTrustSequence.CallNextSequnce();
    }


    


}

[Serializable]
public class TrustSequence {

    // TODO: Change props from hard coded values

    public int PotIndex;
    public float CurrentPot;

    public event Action OnNextSequenceCalled;

    public TrustSequence() {
        PotIndex = 0;
        CurrentPot = 10000;
    }

    public void CallNextSequnce() {
        CurrentPot *= 2;
        PotIndex += 1;

        OnNextSequenceCalled?.Invoke();
    }
}
