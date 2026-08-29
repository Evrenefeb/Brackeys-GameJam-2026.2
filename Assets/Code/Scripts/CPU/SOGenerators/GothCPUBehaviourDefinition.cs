
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/GothCPUBehaviourDefinition", fileName = "GothCPUBehaviourDefinition")]
public class GothCPUBehaviourDefinition : CPUBehaviourDefinition {
    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new GothCPURuntimeBehaviour(this);
    }
}

