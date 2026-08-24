using System;
using UnityEngine;

public class TrustSequenceManager : MonoBehaviour
{
    //[SerializeField] private TrustSequenceType p_SequenceType = TrustSequenceType.CAMPAIGN;
    [SerializeField] private TrustSequenceArgs p_SequenceArgs;


    private void OnEnable() {
        StartTrustSequence();
    }

    public void StartTrustSequence() {
        Debug.Log("Starting Trust Sequence: " + p_SequenceArgs.p_SequenceName);
    }
}
