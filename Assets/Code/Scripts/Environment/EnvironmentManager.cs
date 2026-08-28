using PrimeTween;
using System;
using UnityEngine;

public class EnvironmentManager : MonoBehaviour {
    [SerializeField] private GameManager2 r_GameManager;
    [SerializeField] private SpriteRenderer r_Background;
    //public float p_BackgroundToggleDuration = 1f;

    [SerializeField] private SpriteRenderer r_FadeScreen;
    //public float p_FadeScreenToggleDuration = 1f;


    private void Start() {

        r_FadeScreen.gameObject.SetActive(true);
        HandleTransition(r_GameManager.FlowInvokeDelay);
    }

    public void HandleTransition(float duration) {
        ToggleBackground(duration);
        ToggleFadeScreen(duration);
    }

    public void ToggleBackground(float duration) => ToggleSpriteRenderer(r_Background, duration);

    public void ToggleFadeScreen(float duration) => ToggleSpriteRenderer(r_FadeScreen, duration);

    private void ToggleSpriteRenderer(SpriteRenderer renderer, float duration) {
        if (renderer.color.a == 1f) {
            Tween.Alpha(renderer, 0f, duration);
        }
        else {
            Tween.Alpha(renderer, 1f, duration);
        }

    }
}
