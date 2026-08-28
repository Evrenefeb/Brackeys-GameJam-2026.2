using UnityEngine;

public class ShopItemElement : MonoBehaviour {
    public ItemSO r_ItemDefinition;

    public int ItemID => r_ItemDefinition.ItemID;
}
