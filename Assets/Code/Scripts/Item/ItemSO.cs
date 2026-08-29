using UnityEngine;

public abstract class ItemSO : ScriptableObject
{
    public int ItemID;
    public string DisplayName;
    public Sprite Icon;
    [TextArea(3,5)]public string Description;
    public float ItemPrice;

    public abstract void OnUse(CPUEngine engine);

}
