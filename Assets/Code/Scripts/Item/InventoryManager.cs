using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [SerializeField] private InventorySlot_UI r_InventorySlotPrefab;
    [SerializeField] private GameManager2 r_GameManager;
    [SerializeField] private GameObject r_InventoryContainerParent;



    [SerializeField] [ReadOnly] private List<InventorySlot_UI> m_InventorySlotList = new();

    [SerializeField] private TrustSequenceManager m_TrustSequenceManager;

    public void BuildInventoryUI(TrustSequenceManager _currentTrustSequenceManager) {
        DemolishInventoryUI();

        m_TrustSequenceManager = _currentTrustSequenceManager;

        int size = r_GameManager.PlayerInventoryData.OwnedItemIDList.Count;

        for (int i = 0; i < size; i++) {
            InventorySlot_UI slot = Instantiate(r_InventorySlotPrefab, r_InventoryContainerParent.transform);
            int itemID = r_GameManager.PlayerInventoryData.OwnedItemIDList[i];
            slot.InitializeSlot(r_GameManager.ItemDatabase.ItemList[itemID].Icon, i, m_TrustSequenceManager, this);

            m_InventorySlotList.Add(slot);
        }
    }

    public void DemolishInventoryUI() {

        foreach (InventorySlot_UI slot in m_InventorySlotList) {
            Destroy(slot.gameObject);
        }

        m_InventorySlotList.Clear();
    }

    public void RemoveSlot(InventorySlot_UI slot, int index) {
        r_GameManager.PlayerInventoryData.OwnedItemIDList.RemoveAt(index);
        BuildInventoryUI(m_TrustSequenceManager); // indexler kaydığı için UI'ı yeniden kur
    }

}
