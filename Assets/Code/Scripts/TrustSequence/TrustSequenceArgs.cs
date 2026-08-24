using System;
using UnityEngine;

[CreateAssetMenu(fileName = "TrustSequenceArgs", menuName = "TrustSequence/Args", order = 0)]
[Serializable]
public class TrustSequenceArgs : ScriptableObject {
    public string p_SequenceName;
    public TrustSequenceType p_SequenceType = TrustSequenceType.CAMPAIGN;

    public float p_RoundTime = 10;
    public float p_SlackTime = 5;
    public float p_RoundPotMultiplier = 2;
    public float p_RoundQuota = 100000;

}
