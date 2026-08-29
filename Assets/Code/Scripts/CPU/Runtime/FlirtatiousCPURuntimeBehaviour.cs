using UnityEngine;

public class FlirtatiousCPURuntimeBehaviour : CPURuntimeBehaviour {
    private FlirtatiousCPUBehaviourDefinition def;

    public FlirtatiousCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as FlirtatiousCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log("FlirtatiousCPURuntimeBehaviour.OnEngineUpdate");

        float wave = Mathf.Sin(Time.time * def.FlirtFrequency) * def.FlirtAmplitude;
        runtimeData.PressChance = Mathf.Clamp01(engine.CPUDataDefinition.PressChance + wave);


    }
}