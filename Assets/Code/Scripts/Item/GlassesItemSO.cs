using Ami.BroAudio;
using UnityEngine;

[CreateAssetMenu(fileName = "GlassesItemSO", menuName = "Items/GlassesItemSO")]
public class GlassesItemSO : ItemSO {
    public override void OnUse(CPUEngine engine) {

        BroAudio.Play(SFX_OnUse);


        switch (engine.CPUDataDefinition.CPUID) {

            case 0:
                Debug.Log("Use Book for CPU 0");
                break;

            case 1:
                Debug.Log("Use Book for CPU 1");
                break;

            case 2:
                Debug.Log("Use Book for CPU 2");
                break;

            case 3:
                Debug.Log("Use Book for CPU 3");
                break;

            case 4:
                Debug.Log("Use Book for CPU 4");
                break;

            default:
                break;
        }
    }
}
