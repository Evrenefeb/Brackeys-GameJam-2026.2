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

        float mouseFactor = GetMouseCenterFactor();
        Debug.Log(mouseFactor);

        runtimeData.PressChance += ((float)currentGameRound / (float)maxGameRound)
            * Random.Range(def.RandChange / 2f, def.RandChange) * mouseFactor;

        runtimeData.RetryInterval = engine.TrustSequenceManager.SequenceArgs.p_RoundTime - 1f;
    }

    private float GetMouseCenterFactor() {
        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 mousePos = Input.mousePosition;

        float maxDist = screenCenter.magnitude;
        float dist = Vector2.Distance(mousePos, screenCenter);

        float t = Mathf.Clamp01(dist / maxDist); 

        // t=0 -> 2, t=1 -> 1
        return Mathf.Lerp(2f, 1f, t);
    }
}