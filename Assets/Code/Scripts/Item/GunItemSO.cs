using UnityEngine;

[CreateAssetMenu(fileName = "GunItemSO", menuName = "Items/GunItemSO")]
public class GunItemSO : ItemSO {
    public override void OnUse(CPUEngine engine) {
        switch (engine.CPUDataDefinition.CPUID) {

            case 0:
                Debug.Log("Use Gun for CPU 0");
                engine.CPURuntimeData.RetryInterval = 9999999f; // Korkak çok korktu ve bir daha basamıyor
                break;

            case 1:
                Debug.Log("Use Gun for CPU 1");
                engine.CPURuntimeData.PressChance += 0.1f; // Sinirli için ters tepti
                break;

            default:
                break;
        }
    }
}