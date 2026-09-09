
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Behaviour/FlirtatiousCPUBehaviourDefinition", fileName = "FlirtatiousCPUBehaviourDefinition")]
public class FlirtatiousCPUBehaviourDefinition : CPUBehaviourDefinition {

    [SerializeField] private float p_FlirtFrequency = 0.05f;
    [SerializeField] private float p_FlirtAmplitude = 0.05f;

    public float FlirtFrequency => p_FlirtFrequency;
    public float FlirtAmplitude => p_FlirtAmplitude;

    public override CPURuntimeBehaviour CreateRuntimeBehaviour() {
        return new FlirtatiousCPURuntimeBehaviour(this);
    }
}

