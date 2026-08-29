
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/FlirtatiousCPUBehaviourDefinition", fileName = "FlirtatiousCPUBehaviourDefinition")]
public class FlirtatiousCPUBehaviourDefinition : CPUBehaviourDefinition {
    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new FlirtatiousCPURuntimeBehaviour(this);
    }
}

