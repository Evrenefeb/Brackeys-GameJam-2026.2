using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    [SerializeField] private UpgradeData r_UpgradeData;
    public UpgradeData UpgradeData => r_UpgradeData;

    public void ResetData() => r_UpgradeData.ResetData();

    public void UpgradeCharisma(int amount) {
        r_UpgradeData.Charisma += amount;
    }

    public void UpgradeGoodLooking(int amount) {
        r_UpgradeData.GoodLooking += amount;
    }
}
