using UnityEngine;

[CreateAssetMenu(fileName = "TrustSequenceSettings", menuName = "Game/TrustSequenceSettings")]
public class TrustSequenceSettings : ScriptableObject, ITrustSequenceLike {

    [SerializeField] private int m_PotIndex;
    [SerializeField] private float m_CurrentPot;

    public int PotIndex => m_PotIndex;

    public float CurrentPot => m_CurrentPot;
}

