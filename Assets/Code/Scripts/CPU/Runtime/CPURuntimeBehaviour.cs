using System;

[Serializable]
public abstract class CPURuntimeBehaviour
{
    public abstract void OnEngineUpdate(CPUEngine engine, CPURuntimeData runtimeData);
}


