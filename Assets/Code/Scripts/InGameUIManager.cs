using System;
using TMPro;
using UnityEngine;

public class InGameUIManager : MonoBehaviour {


    [SerializeField] private GameManager r_GameManager;

    [SerializeField] private TMP_Text txt_TimerTime;

    #region Unity Methods

    private void Update() {
        UpdateTrustSequenceTimer();
    }

    #endregion

    private void UpdateTrustSequenceTimer() {
        txt_TimerTime.text = r_GameManager.TrustSequenceTimer.CurrentTime.ToString("F2");
    }


}