using Ami.BroAudio;
using UnityEngine;

[CreateAssetMenu(fileName = "GlassesItemSO", menuName = "Items/GlassesItemSO")]
public class GlassesItemSO : ItemSO {
    public override void OnUse(CPUEngine engine) {

        BroAudio.Play(SFX_OnUse);

        engine.TrustSequenceManager.GameManager.TXT_Glasses.gameObject.SetActive(true);

        switch (engine.CPUDataDefinition.CPUID) {

            case 0:
                Debug.Log($"Use {name} for Scared");
                break;

            case 1:
                Debug.Log($"Use {name} for Goth");
                break;

            case 2:
                Debug.Log($"Use {name} for Nerd");
                engine.CPURuntimeData.PressChance -= 0.5f;
                break;

            case 3:
                Debug.Log($"Use {name} for Angry");
                break;

            case 4:
                Debug.Log($"Use {name} for Flirt");
                break;

            default:
                break;
        }
    }
}
