using UnityEngine;
using UnityEngine.UI;

public class InventorySlot_UI : MonoBehaviour {
    [SerializeField] private Image m_ItemSpriteHolder;
    [SerializeField] private Button m_Button;

    private int m_Index;
    private TrustSequenceManager m_TrustSequenceManager;

    private InventoryManager m_InventoryManager;

    public void InitializeSlot(Sprite itemSprite, int index, TrustSequenceManager trustSequenceManager, InventoryManager inventoryManager) {
        m_ItemSpriteHolder.sprite = itemSprite;
        m_Index = index;
        m_TrustSequenceManager = trustSequenceManager;
        m_InventoryManager = inventoryManager;

        m_Button.onClick.RemoveAllListeners();
        m_Button.onClick.AddListener(OnSlotClicked);
    }

    private void OnSlotClicked() {
        m_TrustSequenceManager.UseItem(m_Index);
        m_InventoryManager.RemoveSlot(this, m_Index);
    }
}