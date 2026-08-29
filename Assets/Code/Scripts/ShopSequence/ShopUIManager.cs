using Ami.BroAudio;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

public class ShopUIManager : MonoBehaviour {

    [SerializeField] private ShopManager r_ShopManager;

    [SerializeField] private Button btn_BuyGun;
    [SerializeField] private Button btn_BuyBook;

    [SerializeField] private CanvasGroup m_LightGroup;
    [SerializeField] private CanvasGroup m_FadeGroup;

    [SerializeField] private float p_LightDelay = 0.5f;
    [SerializeField] private float p_LightFadeDuration = 0.4f;
    [SerializeField] private float p_FadeOutDelay = 2f;
    [SerializeField] private float p_FadeOutDuration = 0.6f;

    [SerializeField] private SoundID SFX_LightON;

    private void Awake() {
        btn_BuyGun.onClick.AddListener(() => OnVendorBuyItem(btn_BuyGun));
        btn_BuyBook.onClick.AddListener(() => OnVendorBuyItem(btn_BuyBook));
    }

    private void OnEnable() {
        m_LightGroup.alpha = 0f;
        m_FadeGroup.alpha = 1f;

        BroAudio.Play(SFX_LightON);

        Tween.Alpha(m_LightGroup, 1f, p_LightFadeDuration, startDelay: p_LightDelay);
        Tween.Alpha(m_FadeGroup, 0f, p_FadeOutDuration, startDelay: p_FadeOutDelay);
    }

    private void OnDisable() {
        Tween.StopAll(m_LightGroup);
        Tween.StopAll(m_FadeGroup);
        m_FadeGroup.alpha = 1f;
        m_LightGroup.alpha = 0f;
        BroAudio.Stop(SFX_LightON);
    }

    public void OnVendorBuyItem(Button pressedButton) {
        ShopItemElement itemElement = pressedButton.GetComponent<ShopItemElement>();
        bool canBuy = r_ShopManager.BuyItem(itemElement.ItemID);
        pressedButton.gameObject.SetActive(!canBuy);
    }
}