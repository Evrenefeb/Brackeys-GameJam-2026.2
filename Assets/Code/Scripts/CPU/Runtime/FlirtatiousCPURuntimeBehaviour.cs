using UnityEngine;

public class FlirtatiousCPURuntimeBehaviour : CPURuntimeBehaviour {
    private FlirtatiousCPUBehaviourDefinition def;
    float currentFlirtTime = 0f;

    bool increasing;
    float rangeModMin = 1.5f;
    float rangeModMax = 2.0f;

    float rangeModMult = 1f;

    float maxPressChance = 0.33f;

    public bool Drunk;


    public FlirtatiousCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as FlirtatiousCPUBehaviourDefinition;
        increasing = false;
        Drunk = false;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log("FlirtatiousCPURuntimeBehaviour.OnEngineUpdate");

        currentFlirtTime += Time.deltaTime;

        if(engine.TrustSequenceManager.CurrentRoundIndex < 2) return;

        if (currentFlirtTime > def.FlirtFrequency) {
            currentFlirtTime = 0f;
            increasing = !increasing;
        }

        if (Drunk) {
            rangeModMult = 0.5f;
        }

        if (increasing) {
            engine.CPURuntimeData.PressChance += def.FlirtAmplitude * Random.Range(rangeModMin * rangeModMult, rangeModMax * rangeModMult);
        }
        else {
            engine.CPURuntimeData.PressChance -= def.FlirtAmplitude * rangeModMult;
        }

        if(engine.CPURuntimeData.PressChance > maxPressChance) {
            engine.CPURuntimeData.PressChance = maxPressChance;
        }

    }
}