using UnityEngine;

// A window that slides down from above the screen when it opens (settling with a tiny bounce) while its dark backdrop
// fades in, and slides back up (the backdrop fading out) when closed with Close(), then hides. On the object that holds
// both (the leader window). Unscaled time.
public class SlideIn : MonoBehaviour
{
    const float OpenTime = 0.35f, CloseTime = 0.25f;
    const float Margin = 20f; // UI units past the top of the screen

    public RectTransform window;
    [Tooltip("The window's CanvasGroup: can't be clicked while it leaves.")]
    public CanvasGroup windowGroup;
    [Tooltip("The backdrop's CanvasGroup (faded in and out).")]
    public CanvasGroup backdrop;

    Vector2 rest;
    float age;
    bool closing;

    public bool IsClosing => closing;

    void Awake()
    {
        rest = window.anchoredPosition;
    }

    void OnEnable()
    {
        closing = false;
        age = 0f;
        windowGroup.interactable = true;
        Apply();
    }

    // Slides it away, then hides this object.
    public void Close()
    {
        if (closing) return;
        closing = true;
        age = 0f;
        windowGroup.interactable = false;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        Apply();
        if (closing && age >= CloseTime) gameObject.SetActive(false);
    }

    void Apply()
    {
        float above = ((RectTransform)transform).rect.height / 2f + window.rect.height / 2f + Margin - rest.y; // just off the top
        float t = Mathf.Clamp01(age / (closing ? CloseTime : OpenTime));
        float away = closing ? t * t : 1f - Settle(t); // 1 = off the top, 0 = in place (a little below it while it settles)
        window.anchoredPosition = rest + Vector2.up * above * away;
        backdrop.alpha = closing ? 1f - t : t;
    }

    // 0 -> 1 overshooting by under 1% (a tiny bounce as it lands).
    static float Settle(float t)
    {
        t -= 1f;
        return 1f + t * t * (1.5f * t + 0.5f);
    }
}
