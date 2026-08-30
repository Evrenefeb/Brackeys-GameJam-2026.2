using Ami.BroAudio;
using UnityEngine;
using System;

[Serializable]
public class ScaredCPURuntimeBehaviour : CPURuntimeBehaviour {

    private readonly ScaredCPUBehaviourDefinition def;
    private readonly string ANIMP_FLOAT_RETRY_INTERVAL = "ANIMP_FLOAT_RETRYINTERVAL";

    

    public ScaredCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as ScaredCPUBehaviourDefinition;
        BroAudio.Stop(BroAudioType.SFX);
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log(runtimeData.RetryInterval);

        if (engine.TrustSequenceManager.CurrentRoundIndex < 2) {         
            return;
        }else if (engine.TrustSequenceManager.CurrentRoundIndex == 2) {
            runtimeData.PressChance = 0.75f;
        }
        else {
            if(runtimeData.PressChance < def.MaxPressChanceValue)
                runtimeData.PressChance += def.PressChangeOvertimeChangeValue;
        }

        engine.CPUAnimationHandler.CPUAnimator.SetFloat(ANIMP_FLOAT_RETRY_INTERVAL, runtimeData.RetryInterval);


        

    }
}


