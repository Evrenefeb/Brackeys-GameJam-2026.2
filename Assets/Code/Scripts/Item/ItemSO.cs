using UnityEngine;

public abstract class ItemSO : ScriptableObject
{
    public string DisplayName;
    public Sprite Icon;
    public string Description;

    public abstract void OnUse(CPUEngine engine);

}
