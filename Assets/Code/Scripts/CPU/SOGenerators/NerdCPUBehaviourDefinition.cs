
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/NerdCPUBehaviourDefinition", fileName = "NerdCPUBehaviourDefinition")]
public class NerdCPUBehaviourDefinition : CPUBehaviourDefinition {

    public float RandChange = 0.05f;

    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new NerdCPURuntimeBehaviour(this);
    }
}

