using Lean.Gui;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager2 : MonoBehaviour {
    public List<TrustSequenceObject> r_TrustSequenceList = new();
    public UpgradeSequenceObject r_UpgradeSequenceObject;
    [SerializeField] private LeanButton r_PlayerButton;


    [SerializeField] private GameData p_GameData;


    private List<SequenceObject> m_AllSequenceObjects;

    public float m_CurrentQuota;
    [SerializeField] [ReadOnly] private float m_RequiredQuota;


    public LeanButton PlayerButton => r_PlayerButton;

    [Space(20)]
    [SerializeField] private CampaignFlow p_CampaignFlow;
    [SerializeField] [ReadOnly] private int m_CampaignFlowIndex;

    private void OnEnable() {

        // Setup Sequence Object List
        SetupSequenceObjects();

        // Setup Campaign Flow
        m_CampaignFlowIndex = 0;
        Flow();

        // Setup Quotas
        m_CurrentQuota = 0;
        m_RequiredQuota = p_GameData.p_RequiredQuota;




    }

    private void SetupSequenceObjects() {
        m_AllSequenceObjects = new List<SequenceObject>();

        foreach (var sequenceObject in r_TrustSequenceList) {
            m_AllSequenceObjects.Add(sequenceObject);
        }

        m_AllSequenceObjects.Add(r_UpgradeSequenceObject);
    }

    public void ChangeTrustSequence(int index){

        if(index >= r_TrustSequenceList.Count || index < 0) return;

        CloseAllSequenceObjects();

        r_TrustSequenceList[index].gameObject.SetActive(true);

    }

    public void ChangeToUpgradeSequence() {
        CloseAllSequenceObjects();

        r_UpgradeSequenceObject.gameObject.SetActive(true);
    }

    private void CloseAllSequenceObjects() {
        foreach (SequenceObject seq in m_AllSequenceObjects) {
            seq.gameObject.SetActive(false);
        }
    }

    public void ChangeCurrentQuota(float amount, QuotaChangeMode changeMode) {
        switch (changeMode) {
            case QuotaChangeMode.ADD:
                m_CurrentQuota += amount;
                break;
            case QuotaChangeMode.SUBTRACT:
                m_CurrentQuota -= amount;
                break;
            case QuotaChangeMode.OVERRRIDE:
                m_CurrentQuota = amount;
                break;
            case QuotaChangeMode.MULTIPLY:
                m_CurrentQuota *= amount;
                break;
        }
    }

    public void Flow() {
        if(m_CampaignFlowIndex >= p_CampaignFlow.SequenceQueue.Count) return;

        CloseAllSequenceObjects();

        p_CampaignFlow.SequenceQueue[m_CampaignFlowIndex].gameObject.SetActive(true);
        m_CampaignFlowIndex++;
    }

    public void SequenceFinished() {
        Flow();
    }
}

[Serializable]
public class CampaignFlow {
    public List<SequenceObject> SequenceQueue = new();
}
