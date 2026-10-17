using UnityEngine;
using UnityEngine.UI;

// A bar that feels alive (the HUD and hero health bars, the XP bar). Its owner calls Set(0-1) every frame and the bar
// slides there (Snap() jumps, for a new run).
// Health: a drop leaves the lost part as a light trail that shrinks away after a moment, flashes the fill white and
// shakes the bar; a rise flashes it green; under LowShare it pulses red.
// Experience: a gain makes the fill glow; LevelUp() runs it to the end, flashes it white and starts it over from empty.
// On the bar's background (the shake moves it, children included). Unscaled time, so it also plays behind the
// level-up cards.
public class DynamicBar : MonoBehaviour
{
    public enum Kind { Health, Experience }

    const float DropSpeed = 18f, RiseSpeed = 7f;        // how fast the fill catches up (exponential, per second)
    const float TrailDelay = 0.45f, TrailSpeed = 5f;    // the trail waits, then catches up
    const float FlashTime = 0.18f, GlowTime = 0.3f;
    const float ShakeTime = 0.25f, ShakeAmount = 6f;    // UI units
    const float LowShare = 0.3f;
    const float FillTime = 0.12f, LevelFlashTime = 0.22f; // level-up: run to the end, then flash
    static readonly Color HealColor = new Color(0.45f, 1f, 0.5f);
    static readonly Color LowColor = new Color(1f, 0.45f, 0.4f);

    public Kind kind;
    [Tooltip("The colored part (its right edge follows the value).")]
    public RectTransform fill;
    [Tooltip("Health only (optional): the light part behind the fill showing what was just lost.")]
    public RectTransform trail;

    RectTransform rect;
    Vector2 rest;
    Image fillImage;
    Color baseColor, flashColor;
    float target, shown, trailShown;
    float trailWait, flash, glow, shake, levelUp; // timers, counting down

    void Awake()
    {
        rect = (RectTransform)transform;
        rest = rect.anchoredPosition;
        fillImage = fill.GetComponent<Image>();
        baseColor = fillImage.color;
    }

    // Jumps to `value`, no effects (a new run).
    public void Snap(float value)
    {
        target = shown = trailShown = Mathf.Clamp01(value);
        trailWait = flash = glow = shake = levelUp = 0f;
        Draw();
    }

    public void Set(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Abs(value - target) < 0.0001f) return;
        bool drop = value < target;
        target = value;
        if (kind == Kind.Health)
        {
            flash = FlashTime;
            flashColor = drop ? Color.white : HealColor;
            if (!drop) return;
            shake = ShakeTime;
            trailWait = TrailDelay;
        }
        else if (!drop)
        {
            glow = GlowTime; // (an experience bar only drops when it levels up)
        }
    }

    // Experience: a level was gained.
    public void LevelUp()
    {
        levelUp = FillTime + LevelFlashTime;
    }

    void Update()
    {
        float delta = Time.unscaledDeltaTime;
        if (levelUp > 0f)
        {
            levelUp -= delta;
            shown = levelUp > LevelFlashTime ? Mathf.MoveTowards(shown, 1f, delta / FillTime) : 1f;
            if (levelUp <= 0f) shown = 0f; // starts over, then slides up to the new level's XP
        }
        else
        {
            shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-(target < shown ? DropSpeed : RiseSpeed) * delta));
        }
        if (trailWait > 0f) trailWait -= delta;
        else trailShown = Mathf.Lerp(trailShown, shown, 1f - Mathf.Exp(-TrailSpeed * delta));
        trailShown = Mathf.Max(trailShown, shown);

        flash = Mathf.Max(0f, flash - delta);
        glow = Mathf.Max(0f, glow - delta);
        shake = Mathf.Max(0f, shake - delta);
        Color color = baseColor;
        if (kind == Kind.Health && target > 0f && target < LowShare)
            color = Color.Lerp(baseColor, LowColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
        if (glow > 0f) color = Color.Lerp(color, Color.white, 0.6f * glow / GlowTime);
        if (flash > 0f) color = Color.Lerp(color, flashColor, flash / FlashTime);
        if (levelUp > 0f && levelUp <= LevelFlashTime) color = Color.Lerp(color, Color.white, levelUp / LevelFlashTime);
        fillImage.color = color;
        rect.anchoredPosition = shake > 0f ? rest + Random.insideUnitCircle * ShakeAmount * (shake / ShakeTime) : rest;
        Draw();
    }

    void Draw()
    {
        fill.anchorMax = new Vector2(shown, 1f);
        if (trail != null) trail.anchorMax = new Vector2(trailShown, 1f);
    }
}
