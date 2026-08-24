using System.Collections.Generic;
using UnityEngine;

public class GameManager2 : MonoBehaviour {
    public List<TrustSequenceObject> TrustSequenceList = new();
    public UpgradeSequenceObject UpgradeSequenceObject;

    private List<SequenceObject> AllSequenceObjects;

    private void OnEnable() {

        AllSequenceObjects = new List<SequenceObject>();

        foreach (var sequenceObject in TrustSequenceList) {
            AllSequenceObjects.Add(sequenceObject);
        }

        AllSequenceObjects.Add(UpgradeSequenceObject);
    }

    public void ChangeTrustSequence(int index){

        if(index >= TrustSequenceList.Count || index < 0) return;

        CloseAllSequenceObjects();

        TrustSequenceList[index].gameObject.SetActive(true);

    }

    public void ChangeToUpgradeSequence() {
        CloseAllSequenceObjects();

        UpgradeSequenceObject.gameObject.SetActive(true);
    }

    private void CloseAllSequenceObjects() {
        foreach (SequenceObject seq in AllSequenceObjects) {
            seq.gameObject.SetActive(false);
        }
    }
}


