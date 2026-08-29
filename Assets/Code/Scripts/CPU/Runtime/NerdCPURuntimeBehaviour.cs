using UnityEngine;

public class NerdCPURuntimeBehaviour : CPURuntimeBehaviour {
    private NerdCPUBehaviourDefinition def;

    public NerdCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as NerdCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log("NerdCPURuntimeBehaviour.OnEngineUpdate");

        int currentGameRound = engine.TrustSequenceManager.CurrentRoundIndex;

        int maxGameRound = engine.TrustSequenceManager.SequenceArgs.p_MaxRounds;

        runtimeData.PressChance = ((float)currentGameRound / ((float)maxGameRound * 1.5f)) + Random.Range(-def.RandChange, def.RandChange);
        
        runtimeData.RetryInterval = engine.TrustSequenceManager.SequenceArgs.p_RoundTime - 1f;
    }
}
