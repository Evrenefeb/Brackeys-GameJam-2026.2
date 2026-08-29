using UnityEngine;

public class AngryCPURuntimeBehaviour : CPURuntimeBehaviour {
    private AngryCPUBehaviourDefinition def;

    public AngryCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as AngryCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log("AngryCPURuntimeBehaviour.OnEngineUpdate");

        engine.CPUAnimationHandler.CPUAnimator.SetFloat("ANIMP_FLOAT_PRESS_CHANCE", runtimeData.PressChance);

        int currentGameRound = engine.TrustSequenceManager.CurrentRoundIndex;

        if(currentGameRound > def.AfterRoundPressChanceMultiplier) {
            engine.CPURuntimeData.PressChance += def.PerRoundPressChanceMultiplier * (currentGameRound);
        }

    }
}
