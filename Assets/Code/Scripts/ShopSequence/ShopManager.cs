using Ami.BroAudio;
using UnityEngine;

public class ShopManager : MonoBehaviour {
    [SerializeField] private GameManager2 r_GameManager;
    [SerializeField] private PlayerInventoryData r_CommonPlayerInventoryData;
    [SerializeField] private ShopUIManager r_UImanager;

    [SerializeField] private SoundID SFX_Purchased;
    [SerializeField] private SoundID SFX_Failed;

    public GameManager2 GameManager => r_GameManager;


    public bool BuyItem(int itemID) {

        float currentMoney = r_GameManager.m_CurrentUsableQuota;
        float itemPrice = r_GameManager.ItemDatabase.ItemList[itemID].ItemPrice;

        if (currentMoney >= itemPrice) {
            r_GameManager.m_CurrentUsableQuota -= itemPrice;
            r_CommonPlayerInventoryData.OwnedItemIDList.Add(itemID);
            BroAudio.Play(SFX_Purchased);
            return true;
        }

        BroAudio.Play(SFX_Failed);
        Debug.Log("Could not buy item: money is not enough.");

        return false;
    }
}
