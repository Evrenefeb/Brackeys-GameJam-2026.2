using UnityEngine;

[CreateAssetMenu(menuName = "CPU Data", fileName = "CPU/CPU Data")]
public class CPUDataDefinition : ScriptableObject, ICPUDataLike {

    [SerializeField] private float m_InitialPressChance;
    [SerializeField] private float m_InitialRetryInterval;

    public float PressChance => m_InitialPressChance;
    public float RetryInterval => m_InitialRetryInterval;


}
