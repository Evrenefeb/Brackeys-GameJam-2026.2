using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/ScaredCPUBehaviourDefinition", fileName = "ScaredCPUBehaviourDefinition")]
public class ScaredCPUBehaviourDefinition : CPUBehaviourDefinition {
    [SerializeField] private float m_MaxPressChanceValue;
    [SerializeField] private float m_PressChangeOvertimeChangeValue;

    public float MaxPressChanceValue => m_MaxPressChanceValue;
    public float PressChangeOvertimeChangeValue => m_PressChangeOvertimeChangeValue;

    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new ScaredCPURuntimeBehaviour(this);
    }
}
