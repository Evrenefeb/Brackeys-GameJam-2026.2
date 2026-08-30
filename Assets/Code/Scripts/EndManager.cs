using Ami.BroAudio;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

public class EndManager : MonoBehaviour {
    public bool isWin;

    public SoundID BGM_Win;
    public SoundID BGM_Lose;

    [SerializeField] private Image r_FadeImage;
    [SerializeField] private float p_FadeDuration = 1f;
    [SerializeField] private SceneManager r_SceneManager;

    private void OnEnable() {

        if (isWin) {

            BroAudio.Play(BGM_Win);

            return;
        }

        BroAudio.Play(BGM_Lose);
    }

    private void OnDisable() {
        BroAudio.Stop(BroAudioType.All);
    }

    public void DoorOpen() {

        Color targetColor = isWin ? Color.white : Color.black;

        r_FadeImage.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
        r_FadeImage.gameObject.SetActive(true);

        Tween.Alpha(r_FadeImage, 1f, p_FadeDuration).OnComplete(OnFadeComplete);
    }

    private void OnFadeComplete() {
        Debug.Log("Fade tamamlandı.");
        r_SceneManager.ChangeScene("SCENE_MainMenu");
    }

}