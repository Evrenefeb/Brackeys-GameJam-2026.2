
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/AngryCPUBehaviourDefinition", fileName = "AngryCPUBehaviourDefinition")]
public class AngryCPUBehaviourDefinition : CPUBehaviourDefinition {

    [SerializeField] private float p_PerRoundPressChanceMultiplier = 0.5f;
    [SerializeField] private int p_AfterRoundPressChanceMultiplier = 2;
    public float PerRoundPressChanceMultiplier => p_PerRoundPressChanceMultiplier;
    public float AfterRoundPressChanceMultiplier => p_AfterRoundPressChanceMultiplier;

    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new AngryCPURuntimeBehaviour(this);
    }
}

