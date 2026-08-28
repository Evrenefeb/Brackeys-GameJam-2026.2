using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [SerializeField] private GameManager2 r_GameManager;
    [SerializeField] private PlayerInventoryData r_CommonPlayerInventoryData;
    [SerializeField] private ShopUIManager r_UImanager;




    public void BuyItem(int p_ItemID) {
        r_CommonPlayerInventoryData.OwnedItemIDList.Add(p_ItemID);
    }
}
