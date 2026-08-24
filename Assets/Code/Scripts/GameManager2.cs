using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager2 : MonoBehaviour {
    public List<TrustSequenceObject> r_TrustSequenceList = new();
    public UpgradeSequenceObject r_UpgradeSequenceObject;
    [SerializeField] private GameData p_GameData;


    private List<SequenceObject> m_AllSequenceObjects;

    [SerializeField] private float m_CurrentQuota;
    [SerializeField] [ReadOnly] private float m_RequiredQuota;


    private void OnEnable() {

        // Setup Sequence Objects
        SetupSequenceObjects();

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


}


