using TMPro;
using UnityEngine;

public class TrustSequenceUIManager : MonoBehaviour {

    [SerializeField] private TrustSequenceManager r_TrustSequenceManager;
    [SerializeField] private TMP_Text txt_GameTimer;

    private void Update() {
        txt_GameTimer.text = r_TrustSequenceManager.RoundTimer.CurrentTime.ToString("F2");
    }

}
