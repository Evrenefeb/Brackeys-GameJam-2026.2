using UnityEngine;
using UnityEngine.UI;

public class ShopUIManager : MonoBehaviour{

    [SerializeField] private ShopManager r_ShopManager;

    [SerializeField] private Button btn_BuyGun;
    [SerializeField] private Button btn_BuyBook;

    private void Awake() {
        
        btn_BuyGun.onClick.AddListener(() => OnVendorBuyItem(btn_BuyGun));
        btn_BuyBook.onClick.AddListener(() => OnVendorBuyItem(btn_BuyBook));
    }


    public void OnVendorBuyItem(Button pressedButton) {
        pressedButton.gameObject.SetActive(false);
        ShopItemElement itemElement = pressedButton.GetComponent<ShopItemElement>();
        r_ShopManager.BuyItem(itemElement.ItemID);
        Debug.Log("Pressed " + pressedButton.name);
    }    

}
