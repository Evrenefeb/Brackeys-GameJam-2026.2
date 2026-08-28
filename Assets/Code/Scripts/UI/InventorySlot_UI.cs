using UnityEngine;
using UnityEngine.UI;

public class InventorySlot_UI : MonoBehaviour
{
    [SerializeField] private Image m_ItemSpriteHolder;

    public void InitializeSlot(Sprite itemSprite) {

        m_ItemSpriteHolder.sprite = itemSprite;
    }
}
