using UnityEngine;

public class GothCPURuntimeBehaviour : CPURuntimeBehaviour {
    private GothCPUBehaviourDefinition def;

    public GothCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as GothCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log("GothCPURuntimeBehaviour.OnEngineUpdate");

        if (runtimeData.PressChance > def.MinPressChanceValue)
        runtimeData.PressChance -= def.ApathyDecayRate * Time.deltaTime;

        runtimeData.RetryInterval = def.FixedLongInterval;
    }
}
