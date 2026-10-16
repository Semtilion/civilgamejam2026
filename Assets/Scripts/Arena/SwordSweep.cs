using UnityEngine;

// Starting weapon: hits every enemy within the radius around the hero and knocks them back. Bonus: bigger radius.
public class SwordSweep : Weapon
{
    const float FlashTime = 0.15f;
    const float FlashAlpha = 0.35f;

    [Header("Sword")]
    public float radius = 2.2f;
    [Tooltip("Extra radius at levels 3 and 5.")]
    public float bonusRadius = 0.4f;
    public float knockback = 6f;
    [Tooltip("Circle that flashes on each sweep (placeholder visual).")]
    public SpriteRenderer sweepVisual;

    float sinceSweep = 99f;

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
        return true; // always swings, even with nothing around
    }

    protected override void Update()
    {
        base.Update();
        sinceSweep += Time.deltaTime;
        FadeFlash(sweepVisual, sinceSweep, FlashTime, FlashAlpha);
        sweepVisual.transform.localScale = Vector3.one * Radius * 2f; // placeholder circle is 1 unit wide
    }

    void OnDisable()
    {
        if (sweepVisual != null) FadeFlash(sweepVisual, FlashTime, FlashTime, FlashAlpha); // hide
    }
}
