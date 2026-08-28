using PrimeTween;
using System;
using UnityEngine;

public class EnvironmentManager : MonoBehaviour {
    [SerializeField] private SpriteRenderer r_Background;
    public float p_BackgroundToggleDuration = 1f;

    [SerializeField] private SpriteRenderer r_FadeScreen;
    public float p_FadeScreenToggleDuration = 1f;


    private void Start() {

        r_FadeScreen.gameObject.SetActive(true);
        HandleTransition();
    }

    public void HandleTransition() {
        ToggleBackground();
        ToggleFadeScreen();
    }

    public void ToggleBackground() => ToggleSpriteRenderer(r_Background, p_BackgroundToggleDuration);

    public void ToggleFadeScreen() => ToggleSpriteRenderer(r_FadeScreen, p_FadeScreenToggleDuration);

    private void ToggleSpriteRenderer(SpriteRenderer renderer, float duration) {
        if (renderer.color.a == 1f) {
            Tween.Alpha(renderer, 0f, duration);
        }
        else {
            Tween.Alpha(renderer, 1f, duration);
        }

    }
}
