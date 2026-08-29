using UnityEngine;

public class NerdCPURuntimeBehaviour : CPURuntimeBehaviour {
    private NerdCPUBehaviourDefinition def;

    bool absored;



    public NerdCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as NerdCPUBehaviourDefinition;
        absored = false;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log("NerdCPURuntimeBehaviour.OnEngineUpdate");

        if (!absored) {


            absored = true;
        }

        int currentGameRound = engine.TrustSequenceManager.CurrentRoundIndex;

        int maxGameRound = engine.TrustSequenceManager.SequenceArgs.p_MaxRounds;

        runtimeData.PressChance += ((float)currentGameRound / ((float)maxGameRound) * Random.Range(def.RandChange / 2f, def.RandChange));
        
        runtimeData.RetryInterval = engine.TrustSequenceManager.SequenceArgs.p_RoundTime - 1f;
    }
}
