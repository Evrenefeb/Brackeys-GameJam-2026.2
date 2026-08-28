using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [SerializeField] private InventorySlot_UI r_InventorySlotPrefab;
    [SerializeField] private GameManager2 r_GameManager;
    [SerializeField] private GameObject r_InventoryContainerParent;



    [SerializeField] [ReadOnly] private List<InventorySlot_UI> m_InventorySlotList = new();

    public void BuildInventoryUI() {
        DemolishInventoryUI();

        int size = r_GameManager.PlayerInventoryData.OwnedItemIDList.Count;

        for (int i = 0; i < size; i++) {
            InventorySlot_UI slot = Instantiate(r_InventorySlotPrefab, r_InventoryContainerParent.transform);
            slot.InitializeSlot(r_GameManager.ItemDatabase.ItemList[r_GameManager.PlayerInventoryData.OwnedItemIDList[i]].Icon);

            m_InventorySlotList.Add(slot);
        }
    }

    public void DemolishInventoryUI() {

        foreach (InventorySlot_UI slot in m_InventorySlotList) {
            Destroy(slot.gameObject);
        }

        m_InventorySlotList.Clear();
    }

}
