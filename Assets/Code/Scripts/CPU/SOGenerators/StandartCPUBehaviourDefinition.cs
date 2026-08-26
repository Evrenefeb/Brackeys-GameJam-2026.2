using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/StandartCPUBehaviourDefinition", fileName = "StandartCPUBehaviourDefinition")]
public class StandartCPUBehaviourDefinition : CPUBehaviourDefinition
{
    [SerializeField] private float m_MaxPressChanceValue;
    [SerializeField] private float m_PressChangeOvertimeChangeValue;

    public float MaxPressChanceValue => m_MaxPressChanceValue;
    public float PressChangeOvertimeChangeValue => m_PressChangeOvertimeChangeValue;

    public override CPURuntimeBehaviour CreateRuntimeBehaviour()
    {
        return new StandartCPURuntimeBehaviour(this);
    }
}
