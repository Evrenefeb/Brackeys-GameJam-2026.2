using System;
using UnityEngine;

[CreateAssetMenu(fileName = "New Game Data", menuName = "Game/Game Data")]
[Serializable]
public class GameData : ScriptableObject {
    public float p_RequiredQuota = 100000;
    public float p_StartMoney = 10000;
}


