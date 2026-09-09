using Ami.BroAudio;
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/ScaredCPUBehaviourDefinition", fileName = "ScaredCPUBehaviourDefinition")]
public class ScaredCPUBehaviourDefinition : CPUBehaviourDefinition {
    [SerializeField] private float m_MaxPressChanceValue;
    [SerializeField] private float m_PressChangeOvertimeChangeValue;

    public SoundID SFX_Scared_Breathing;
    public SoundID SFX_Scared_Scream;
    public float MaxRetryIntervalBeforeScream;

    public float MaxPressChanceValue => m_MaxPressChanceValue;
    public float PressChangeOvertimeChangeValue => m_PressChangeOvertimeChangeValue;

    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new ScaredCPURuntimeBehaviour(this);
    }
}
