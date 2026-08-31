using Ami.BroAudio;
using UnityEngine;

[CreateAssetMenu(fileName = "WineItemSO", menuName = "Items/WineItemSO")]
public class WineItemSO : ItemSO {

    private float m_MaxRandomChance = 0.7f;
    private float m_MinRandomChance = 0.25f;

    public override void OnUse(CPUEngine engine) {

        BroAudio.Play(SFX_OnUse);


        switch (engine.CPUDataDefinition.CPUID) {

            case 0:
                Debug.Log($"Use {name} for Scared");
                SetRandomChance(engine);
                break;

            case 1:
                Debug.Log($"Use {name} for Goth");
                SetRandomChance(engine);
                break;

            case 2:
                Debug.Log($"Use {name} for Nerd");
                SetRandomChance(engine);
                break;

            case 3:
                Debug.Log($"Use {name} for Angry");
                SetRandomChance(engine);
                break;

            case 4:
                Debug.Log($"Use {name} for Flirt");

                engine.CPUAnimationHandler.CPUAnimator.SetBool("ANIMBOOL_DRUNK", true);

                FlirtatiousCPURuntimeBehaviour beh = engine.RuntimeBehaviour as FlirtatiousCPURuntimeBehaviour;
                beh.Drunk = true;

                int betrayalChance = Random.Range(5, 1001);
                if(betrayalChance <= 5) {
                    engine.CPURuntimeData.PressChance = 100f;
                }
                else {
                    engine.CPURuntimeData.PressChance = -0.25f;
                }

                break;

            default:
                break;
        }
    }

    private void SetRandomChance(CPUEngine engine) {
        engine.CPURuntimeData.PressChance = Random.Range(m_MinRandomChance, m_MaxRandomChance);
    }
}
