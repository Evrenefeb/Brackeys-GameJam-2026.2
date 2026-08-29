using UnityEngine;

public class FlirtatiousCPURuntimeBehaviour : CPURuntimeBehaviour {
    private FlirtatiousCPUBehaviourDefinition def;

    public FlirtatiousCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as FlirtatiousCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        Debug.Log("FlirtatiousCPURuntimeBehaviour.OnEngineUpdate");
    }
}