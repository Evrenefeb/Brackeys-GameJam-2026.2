using UnityEngine;

[CreateAssetMenu(menuName = "CPU Data", fileName = "CPU/CPU Data")]
public class CPUDataDefinition : ScriptableObject, ICPUDataLike {

    [SerializeField] private float m_InitialPressChance;
    [SerializeField] private float m_InitialRetryInterval;
    [SerializeField] private AnimationCurve m_InitialPressChanceCurve;


    public float PressChance { get => m_InitialPressChance; set => m_InitialPressChance = value; }
    public float RetryInterval { get => m_InitialRetryInterval; set => m_InitialRetryInterval = value; }
    public AnimationCurve PressChanceCurve { get => m_InitialPressChanceCurve; set => m_InitialPressChanceCurve = value; }
}
