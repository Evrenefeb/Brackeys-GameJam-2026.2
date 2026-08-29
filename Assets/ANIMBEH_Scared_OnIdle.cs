using Ami.BroAudio;
using UnityEngine;

public class ANIMBEH_Scared_OnIdle : StateMachineBehaviour
{

    public SoundID SFX_Breahting;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {        
    }

    public override void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {

        if(SFX_Breahting.HasAnyPlayingInstances() == false) { BroAudio.Play(SFX_Breahting); }
    }

}
