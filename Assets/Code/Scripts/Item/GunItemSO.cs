using Ami.BroAudio;
using UnityEngine;

[CreateAssetMenu(fileName = "GunItemSO", menuName = "Items/GunItemSO")]
public class GunItemSO : ItemSO {

    public override void OnUse(CPUEngine engine) {

        BroAudio.Play(SFX_OnUse);


        switch (engine.CPUDataDefinition.CPUID) {

            case 0:
                Debug.Log($"Use {name} for Scared");
                engine.CPURuntimeData.RetryInterval = 99999f;
                break;

            case 1:
                Debug.Log($"Use {name} for Goth");
                engine.CPURuntimeData.PressChance *= 1.6f;
                engine.CPUAnimationHandler.CPUAnimator.SetBool("ANIMBOOL_SAD", true);
                break;

            case 2:
                Debug.Log($"Use {name} for Nerd");
                engine.CPUAnimationHandler.CPUAnimator.SetBool("ANIMBOOL_SAD", true);
                engine.CPURuntimeData.PressChance *= 0.5f;
                break;

            case 3:
                Debug.Log($"Use {name} for Angry");
                engine.CPURuntimeData.RetryInterval = 10f;
                engine.CPURuntimeData.PressChance *= 1.8f;
                break;

            case 4:
                Debug.Log($"Use {name} for Flirt");
                engine.CPURuntimeData.PressChance *= 1.6f;
                break;

            default:
                break;
        }
    }
}
