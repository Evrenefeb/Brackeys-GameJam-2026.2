using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [SerializeField] private GameManager2 r_GameManager;
    [SerializeField] private PlayerInventoryData r_CommonPlayerInventoryData;
    [SerializeField] private ShopUIManager r_UImanager;




    public bool BuyItem(int itemID) {

        float currentMoney = r_GameManager.m_CurrentUsableQuota;
        float itemPrice = r_GameManager.ItemDatabase.ItemList[itemID].ItemPrice;
        
        if(currentMoney >= itemPrice) {
            r_GameManager.m_CurrentUsableQuota -= itemPrice;
            r_CommonPlayerInventoryData.OwnedItemIDList.Add(itemID);
            return true;
        }

        Debug.Log("Could not buy item: money is not enough.");

        return false;
    }
}
