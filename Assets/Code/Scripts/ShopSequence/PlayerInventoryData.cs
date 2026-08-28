using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PlayerInventoryData", menuName = "Game/PlayerInventoryData", order = 0)]
[Serializable]
public class PlayerInventoryData : ScriptableObject {

    public List<int> OwnedItemIDList = new List<int>();

    [Space(10)]
    [Header("Debugging")]
    public bool ResetEnabled = true;

    public void Reset() {
        if (ResetEnabled)
            OwnedItemIDList.Clear();
    }
}

