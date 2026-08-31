using PrimeTween;
using System;
using UnityEngine;

public class EnvironmentManager : MonoBehaviour {
    [SerializeField] private SpriteRenderer r_Background;
    public float p_BackgroundToggleDuration = 1f;

    [SerializeField] private SpriteRenderer r_FadeScreen;
    public float p_FadeScreenToggleDuration = 1f;

    private bool m_BackgroundVisible;

    private void Awake() {
        // Gerçek başlangıç durumunu al, tahmin etme
        m_BackgroundVisible = r_Background.color.a > 0.5f;
    }

    private void Start() {
        r_FadeScreen.gameObject.SetActive(true);
        Tween.Alpha(r_FadeScreen, 0f, p_FadeScreenToggleDuration);
    }

    // Sequence bitince: ekranı karart -> (siyahken) background'u değiştir + callback çalıştır -> tekrar aç
    public void HandleTransition(float duration, Action onFadedOut) {

        Tween.Alpha(r_FadeScreen, 1f, duration).OnComplete(() => {
            ToggleBackground(duration);
            onFadedOut?.Invoke();
            Tween.Alpha(r_FadeScreen, 0f, duration);
        });
    }

    public void ToggleBackground(float duration) {
        m_BackgroundVisible = !m_BackgroundVisible;
        Tween.Alpha(r_Background, m_BackgroundVisible ? 1f : 0f, duration);
    }
}