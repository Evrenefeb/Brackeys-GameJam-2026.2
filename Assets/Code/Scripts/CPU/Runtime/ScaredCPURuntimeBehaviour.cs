using System;

[Serializable]
public class ScaredCPURuntimeBehaviour : CPURuntimeBehaviour {

    private readonly ScaredCPUBehaviourDefinition def;
    private readonly string ANIMP_FLOAT_RETRY_INTERVAL = "ANIMP_FLOAT_RETRYINTERVAL";

    public ScaredCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as ScaredCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log(runtimeData.RetryInterval);
        engine.CPUAnimationHandler.CPUAnimator.SetFloat(ANIMP_FLOAT_RETRY_INTERVAL, runtimeData.RetryInterval);
    }
}


