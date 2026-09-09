using UnityEngine;

[CreateAssetMenu(menuName = "CPU Data", fileName = "CPU/CPU Data")]
public class CPUDataDefinition : ScriptableObject, ICPUDataLike {

    [SerializeField] private int m_CPUID;
    [SerializeField] private string m_DisplayName;
    [SerializeField] private float m_InitialPressChance;
    [SerializeField] private float m_InitialRetryInterval;


    public int CPUID => m_CPUID;
    public string DisplayName => m_DisplayName;

    public float PressChance { get => m_InitialPressChance; set => m_InitialPressChance = value; }
    public float RetryInterval { get => m_InitialRetryInterval; set => m_InitialRetryInterval = value; }


}


