using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerInventoryData", menuName = "Game/PlayerInventoryData", order = 0)]
[Serializable]
public class PlayerInventoryData : ScriptableObject {

    public List<int> OwnedItemIDList = new List<int>();

    public void Reset() {
        OwnedItemIDList.Clear();
    }
}

