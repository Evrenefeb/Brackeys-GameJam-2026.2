
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/AngryCPUBehaviourDefinition", fileName = "AngryCPUBehaviourDefinition")]
public class AngryCPUBehaviourDefinition : CPUBehaviourDefinition {
    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new AngryCPURuntimeBehaviour(this);
    }
}

