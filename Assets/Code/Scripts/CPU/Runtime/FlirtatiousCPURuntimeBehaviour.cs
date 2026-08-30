using UnityEngine;

public class FlirtatiousCPURuntimeBehaviour : CPURuntimeBehaviour {
    private FlirtatiousCPUBehaviourDefinition def;
    float currentFlirtTime = 0f;

    bool increasing;


    public FlirtatiousCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as FlirtatiousCPUBehaviourDefinition;
        increasing = false;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log("FlirtatiousCPURuntimeBehaviour.OnEngineUpdate");

        currentFlirtTime += Time.deltaTime;

        if (currentFlirtTime > def.FlirtFrequency) {
            currentFlirtTime = 0f;
            increasing = !increasing;
        }

        if (increasing) {
            engine.CPURuntimeData.PressChance += def.FlirtAmplitude * Random.Range(0.9f, 1.5f);
        }
        else {
            engine.CPURuntimeData.PressChance -= def.FlirtAmplitude;
        }


    }
}