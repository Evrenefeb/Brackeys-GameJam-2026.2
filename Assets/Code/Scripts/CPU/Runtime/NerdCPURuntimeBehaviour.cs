using UnityEngine;

public class NerdCPURuntimeBehaviour : CPURuntimeBehaviour {
    private NerdCPUBehaviourDefinition def;

    public NerdCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as NerdCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        Debug.Log("NerdCPURuntimeBehaviour.OnEngineUpdate");
    }
}
