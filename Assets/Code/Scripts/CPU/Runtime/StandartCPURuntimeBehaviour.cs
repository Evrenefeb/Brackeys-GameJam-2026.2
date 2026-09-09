using System;

[Serializable]
public class StandartCPURuntimeBehaviour : CPURuntimeBehaviour
{
    private readonly StandartCPUBehaviourDefinition def;

    public StandartCPURuntimeBehaviour(CPUBehaviourDefinition def)
    {
        this.def = def as StandartCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData)
    {
        ////Debug.Log($"def={def}, current={runtimeData.PressChance}, max={def?.MaxPressChanceValue}, step={def?.PressChangeOvertimeChangeValue}");
        //if (runtimeData.PressChance < def.MaxPressChanceValue)
        //{
        //    runtimeData.PressChance += def.PressChangeOvertimeChangeValue;
        //}
    }
}


