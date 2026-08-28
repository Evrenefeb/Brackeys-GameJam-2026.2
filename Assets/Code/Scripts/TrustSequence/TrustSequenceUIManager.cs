using TMPro;
using UnityEngine;

public class TrustSequenceUIManager : MonoBehaviour {

    [SerializeField] private TrustSequenceManager r_TrustSequenceManager;
    [SerializeField] private TMP_Text txt_GameTimer;
    [SerializeField] private TMP_Text txt_CurrentPot;

    private void Update() {
        txt_GameTimer.text = r_TrustSequenceManager.RoundTimer.CurrentTime.ToString("F2");

        txt_CurrentPot.text = r_TrustSequenceManager.CurrentRound.CurrentPot.ToString() + " TL";
    }

}
