using UnityEngine;

// A panel pops in every time it opens: it scales up from a little smaller (with a small overshoot) and fades in.
// Uses unscaled time, so it also plays while the game is paused. Added to panels by the scene builders.
[RequireComponent(typeof(CanvasGroup))]
public class PopIn : MonoBehaviour
{
    const float Duration = 0.18f;

    [Tooltip("Scale it starts from (1 = only fades).")]
    public float startScale = 0.9f;

    CanvasGroup group;
    float age = Duration;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        age = 0f;
        Apply();
    }

    void Update()
    {
        if (age >= Duration) return;
        age += Time.unscaledDeltaTime;
        Apply();
    }

    void Apply()
    {
        float t = Mathf.Clamp01(age / Duration);
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, Fx.EaseOutBack(t));
        group.alpha = Mathf.Clamp01(t * 2f); // fully visible halfway through
    }
}
