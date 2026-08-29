using Ami.BroAudio;
using UnityEngine;

public class ANIMBEH_Scared_OnScream : StateMachineBehaviour {

    public SoundID SFX_Scream;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
    }

    public override void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {

        if (SFX_Scream.HasAnyPlayingInstances() == false) { BroAudio.Play(SFX_Scream); }
    }

}
