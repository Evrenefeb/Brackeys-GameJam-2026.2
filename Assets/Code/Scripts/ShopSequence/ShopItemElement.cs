using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemElement : MonoBehaviour {
    public ItemSO r_ItemDefinition;
    public int ItemID => r_ItemDefinition.ItemID;

    [SerializeField] private TMP_Text DisplayTextRef;
    [SerializeField] private Image DisplayImageRef;


    private void OnValidate() {
        DisplayImageRef.sprite = r_ItemDefinition.Icon;
    }

    private void Awake() {

        if(!DisplayTextRef) return;

        SetHoveredText(r_ItemDefinition.DisplayName, r_ItemDefinition.Description);
    }

    public void SetHoveredText(string displayName, string description) {
        DisplayTextRef.text = $"<b>{displayName}</b>\n{description}\n\n<b>${r_ItemDefinition.ItemPrice}</b>";
    }

}
