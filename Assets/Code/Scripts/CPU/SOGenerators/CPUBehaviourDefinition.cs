using System;
using UnityEngine;

[Serializable]
public abstract class CPUBehaviourDefinition : ScriptableObject
{
    public abstract CPURuntimeBehaviour CreateRuntimeBehaviour();
}



