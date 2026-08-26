using System;
using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeData", menuName = "Game/UpgradeData", order = 0)]
[Serializable]
public class UpgradeData : ScriptableObject {
    public int Charisma;
    public int GoodLooking;


    public void ResetData() {
        Charisma = 0;
        GoodLooking = 0;
    }
}
