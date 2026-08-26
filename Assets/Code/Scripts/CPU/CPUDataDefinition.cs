using System;
using UnityEngine;

[CreateAssetMenu(menuName = "CPU Data", fileName = "CPU/CPU Data")]
public class CPUDataDefinition : ScriptableObject, ICPUDataLike {

    [SerializeField] private float m_InitialPressChance;
    [SerializeField] private float m_InitialRetryInterval;
    [SerializeField] private AnimationCurve m_InitialPressChanceCurve;


    public float PressChance { get => m_InitialPressChance; set => m_InitialPressChance = value; }
    public float RetryInterval { get => m_InitialRetryInterval; set => m_InitialRetryInterval = value; }
    public AnimationCurve PressChanceCurve { get => m_InitialPressChanceCurve; set => m_InitialPressChanceCurve = value; }


}


[Serializable]
public abstract class CPUBehaviourDefinition : ScriptableObject
{
    public abstract CPURuntimeBehaviour CreateRuntimeBehaviour();
}


//[CreateAssetMenu(menuName = "CPU Behaviour/ScaredCPUBehaviourDefinition", fileName = "ScaredCPUBehaviourDefinition")]
//public class ScaredCPUBehaviourDefinition : CPUBehaviourDefinition
//{
//    public override CPURuntimeBehaviour CreateRuntimeBehaviour()
//    {
//        throw new NotImplementedException();
//    }
//}


[Serializable]
public abstract class CPURuntimeBehaviour
{
    public abstract void OnEngineUpdate(CPURuntimeData runtimeData);
}

[Serializable]
public class StandartCPURuntimeBehaviour : CPURuntimeBehaviour
{
    private readonly StandartCPUBehaviourDefinition def;

    public StandartCPURuntimeBehaviour(CPUBehaviourDefinition def)
    {
        this.def = def as StandartCPUBehaviourDefinition;
    }

    public override void OnEngineUpdate(CPURuntimeData runtimeData)
    {
        //Debug.Log($"def={def}, current={runtimeData.PressChance}, max={def?.MaxPressChanceValue}, step={def?.PressChangeOvertimeChangeValue}");
        if (runtimeData.PressChance < def.MaxPressChanceValue)
        {
            runtimeData.PressChance += def.PressChangeOvertimeChangeValue;
        }
    }
}



//[Serializable]
//public class ScaredCPURuntimeBehaviour : CPURuntimeBehaviour
//{

//}
