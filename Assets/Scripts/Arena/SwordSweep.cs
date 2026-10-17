using UnityEngine;

// Starting weapon: hits every enemy within the radius around the hero and knocks them back. Bonus: bigger radius.
// The Quake Hammer relic is a second one of these with bigger, slower numbers (set by the scene builder).
// Look: the sword spins an arc slash once around the hero (from the way he faces); the hammer sends out an expanding
// shockwave ring and jolts the camera.
public class SwordSweep : Weapon
{
    public enum SweepStyle { Spin, Shockwave }

    const float SwingTime = 0.18f;    // the slash's full turn / the ring's growth
    const float FlashTime = 0.25f;    // fade
    const float FlashAlpha = 0.8f;

    [Header("Sword")]
    public float radius = 2.2f;
    [Tooltip("Extra radius at levels 3 and 5.")]
    public float bonusRadius = 0.4f;
    public float knockback = 6f;
    public SweepStyle style = SweepStyle.Spin;
    [Tooltip("The slash (an arc sprite) or the shockwave (a ring sprite); placeholder visual, a child of the hero.")]
    public SpriteRenderer sweepVisual;

    float sinceSweep = 99f;
    float startAngle;

    float Radius => (radius + bonusRadius * BonusCount) * player.areaMultiplier;

    protected override bool Attack()
    {
        Vector2 center = transform.position;
        float reachBase = Radius;
        // Killed enemies are only destroyed at the end of the frame, so the list doesn't change during this loop.
        foreach (Enemy enemy in Enemy.All)
        {
            float reach = reachBase + enemy.data.radius;
            if (((Vector2)enemy.transform.position - center).sqrMagnitude <= reach * reach)
                enemy.TakeDamage(Damage, center, knockback);
        }
        sinceSweep = 0f;
        startAngle = player.sprite.flipX ? 180f : 0f; // the slash starts where he faces
        if (style == SweepStyle.Shockwave) player.arena.ShakeCamera(0.15f, 0.15f);
        return true; // always swings, even with nothing around
    }

    protected override void Update()
    {
        base.Update();
        sinceSweep += Time.deltaTime;
        FadeFlash(sweepVisual, sinceSweep, FlashTime, FlashAlpha);

        float swing = Mathf.Clamp01(sinceSweep / SwingTime);
        if (style == SweepStyle.Spin)
        {
            // One fast clockwise turn (the arc sprite's bright end leads).
            sweepVisual.transform.localScale = Vector3.one * Radius * 2f; // placeholder sprites are 1 unit wide
            sweepVisual.transform.localRotation = Quaternion.Euler(0f, 0f, startAngle - 360f * swing);
        }
        else
        {
            // The ring grows from a third of the radius out to the full radius.
            sweepVisual.transform.localScale = Vector3.one * Radius * 2f * Mathf.Lerp(0.3f, 1f, Fx.EaseOutBack(swing));
        }
    }

    void OnDisable()
    {
        if (sweepVisual != null) FadeFlash(sweepVisual, FlashTime, FlashTime, FlashAlpha); // hide
    }
}
