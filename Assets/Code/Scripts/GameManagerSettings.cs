using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/GameManagerSettings")]
[Serializable]
public class GameManagerSettings : ScriptableObject {

    [SerializeField] private float p_InitCountdownTimerValue = 10.0f;


    public float InitTrustSequenceTimerValue => p_InitCountdownTimerValue;



}
