using Ami.BroAudio;
using UnityEngine;

[CreateAssetMenu(fileName = "BookItemSO", menuName = "Items/BookItemSO")]
public class BookItemSO : ItemSO {
    public override void OnUse(CPUEngine engine) {

        BroAudio.Play(SFX_OnUse);


        switch (engine.CPUDataDefinition.CPUID) {

            case 0:
                Debug.Log($"Use {name} for Scared");
                break;

            case 1:
                Debug.Log($"Use {name} for Goth");
                break;

            case 2:
                Debug.Log($"Use {name} for Nerd");
                engine.CPURuntimeData.PressChance *= 0.5f;
                engine.CPURuntimeData.RetryInterval *= 2f;
                break;

            case 3:
                Debug.Log($"Use {name} for Angry");
                engine.CPURuntimeData.PressChance *= 1.2f;
                break;

            case 4:
                Debug.Log($"Use {name} for Flirt");
                break;

            default:
                break;
        }
    }
}
