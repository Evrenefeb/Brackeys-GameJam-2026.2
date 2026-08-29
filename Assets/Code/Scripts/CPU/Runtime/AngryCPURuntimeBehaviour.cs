using UnityEngine;

public class AngryCPURuntimeBehaviour : CPURuntimeBehaviour {
    private AngryCPUBehaviourDefinition def;

    public AngryCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as AngryCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        Debug.Log("AngryCPURuntimeBehaviour.OnEngineUpdate");
    }
}
