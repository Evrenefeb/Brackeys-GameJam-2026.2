
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/NerdCPUBehaviourDefinition", fileName = "NerdCPUBehaviourDefinition")]
public class NerdCPUBehaviourDefinition : CPUBehaviourDefinition {
    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new NerdCPURuntimeBehaviour(this);
    }
}

