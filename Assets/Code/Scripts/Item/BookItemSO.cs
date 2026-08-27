using UnityEngine;

[CreateAssetMenu(fileName = "BookItemSO", menuName = "Items/BookItemSO")]
public class BookItemSO : ItemSO {
    public override void OnUse(CPUEngine engine) {
        switch (engine.CPUDataDefinition.CPUID) {

            case 0:
                Debug.Log("Use Book for CPU 0");
                break;

            case 1:
                Debug.Log("Use Book for CPU 1");
                break;

            default:
                break;
        }
    }
}
