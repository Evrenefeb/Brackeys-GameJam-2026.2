using Ami.BroAudio;
using UnityEngine;

public class ANIMBEH_PlaySoundOneShot : StateMachineBehaviour {

    public SoundID SFX_Sound;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {

        if (SFX_Sound.HasAnyPlayingInstances() == false) { BroAudio.Play(SFX_Sound); }
    }

}
