using UnityEngine;
using UnityEngine.UI;

// Shakes a whole screen of UI and flashes it once (the town after an earthquake). `target` is a full-screen root that
// holds the UI; `flash` is a full-screen image drawn on top (see-through until it flashes).
public class UIShake : MonoBehaviour
{
    const float FlashAlpha = 0.45f;

    public RectTransform target;
    public Image flash;

    float duration, strength, left;

    public void Play(float shakeDuration, float shakeStrength)
    {
        duration = shakeDuration;
        strength = shakeStrength;
        left = shakeDuration;
    }

    void Update()
    {
        if (left <= 0f) return;
        left -= Time.unscaledDeltaTime;
        float fade = Mathf.Clamp01(left / duration);
        target.anchoredPosition = left > 0f ? Random.insideUnitCircle * strength * fade : Vector2.zero;

        Color color = flash.color;
        color.a = FlashAlpha * fade * fade; // a quick flash that fades faster than the shake
        flash.color = color;
    }
}
