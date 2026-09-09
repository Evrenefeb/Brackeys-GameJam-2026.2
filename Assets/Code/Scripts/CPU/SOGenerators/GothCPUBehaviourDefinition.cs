
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/GothCPUBehaviourDefinition", fileName = "GothCPUBehaviourDefinition")]
public class GothCPUBehaviourDefinition : CPUBehaviourDefinition {

    [SerializeField] private float m_MinPressChanceValue;
    [SerializeField] private float m_ApathyDecayRate;
    [SerializeField] private float m_FixedLongInterval;

    public float MinPressChanceValue => m_MinPressChanceValue;
    public float ApathyDecayRate => m_ApathyDecayRate;
    public float FixedLongInterval => m_FixedLongInterval;

    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new GothCPURuntimeBehaviour(this);
    }
}

