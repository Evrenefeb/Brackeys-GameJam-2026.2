using System;
using TMPro;
using UnityEngine;

public class InGameUIManager : MonoBehaviour {


    [SerializeField] private GameManager r_GameManager;

    [SerializeField] private TMP_Text txt_TimerTime;
    [SerializeField] private TMP_Text txt_PotIndex;
    [SerializeField] private TMP_Text txt_CurrentPot;

    #region Unity Methods

    private void Start() {
        Initialize();
    }



    private void Update() {
        UpdateTrustSequenceTimer();
    }

    #endregion

    private void Initialize() {
        r_GameManager.CurrentTrustSequence.OnNextSequenceCalled += UpdatePotIndex;
        r_GameManager.CurrentTrustSequence.OnNextSequenceCalled += UpdateCurrentPot;
    }



    #region UI Update Methods

    private void UpdatePotIndex() => txt_PotIndex.text = r_GameManager.CurrentTrustSequence.PotIndex.ToString();
    private void UpdateCurrentPot() => txt_CurrentPot.text = r_GameManager.CurrentTrustSequence.CurrentPot.ToString("F2");
    private void UpdateTrustSequenceTimer() => txt_TimerTime.text = (r_GameManager.TrustSequenceTimer.CurrentTime < 0) ? "00.000" : r_GameManager.TrustSequenceTimer.CurrentTime.ToString("F3");

    #endregion




}