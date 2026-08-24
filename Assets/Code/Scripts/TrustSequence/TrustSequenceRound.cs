using System;
using UnityEngine;

[Serializable]
public class TrustSequenceRound {
    [SerializeField] private int m_RoundIndex;
    [SerializeField] private float m_CurrentPot;

    public int RoundIndex => m_RoundIndex;
    public float CurrentPot => m_CurrentPot;

    public TrustSequenceRound(float intialPot) {
        m_RoundIndex = 0;
        m_CurrentPot = intialPot;
    }

    public void SetupNextRound(float potMultiplier) {
        m_RoundIndex++;
        m_CurrentPot *= potMultiplier;
    }
    
}