using UnityEngine;

public class GothCPURuntimeBehaviour : CPURuntimeBehaviour {
    private GothCPUBehaviourDefinition def;

    public GothCPURuntimeBehaviour(CPUBehaviourDefinition def) {
        this.def = def as GothCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData) {
        //Debug.Log("GothCPURuntimeBehaviour.OnEngineUpdate");


        if (runtimeData.PressChance > def.MinPressChanceValue)
        runtimeData.PressChance -= def.ApathyDecayRate * Time.deltaTime * GetMouseCenterFactor();

        runtimeData.RetryInterval = def.FixedLongInterval;
    }

    private float GetMouseCenterFactor() {
        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 mousePos = Input.mousePosition;

        float maxDist = screenCenter.magnitude;
        float dist = Vector2.Distance(mousePos, screenCenter);

        float t = Mathf.Clamp01(dist / maxDist);

        return Mathf.Lerp(1f, 0.9f, t);
    }
}
