using UnityEngine;

// Thrusts a spear in a straight line toward the nearest enemy (a bit longer than the sword's reach), hitting
// everything in it. Only thrusts when an enemy is within reach. Bonus: longer reach.
// Evolution (Impale, from the Guard): walking enemies hit are pinned in place for `pinTime` (bosses aren't).
// Look: a bright streak shoots out from the hero to its full length with a glint at its tip, then thins and fades.
public class SpearThrust : Weapon
{
    const float SnapTime = 0.07f;
    const float FlashTime = 0.22f;
    const float FlashAlpha = 0.85f;
    const float TipSize = 0.9f; // the glint at the tip (world units)

    [Header("Spear")]
    public float length = 3.2f;
    public float width = 0.8f;
    [Tooltip("Extra length at levels 3 and 5.")]
    public float bonusLength = 0.6f;
    public float knockback = 5f;
    [Tooltip("Impale (evolved): seconds a walking enemy hit stays pinned.")]
    public float pinTime = 1f;
    [Tooltip("The streak that flashes along the thrust (a child of the hero).")]
    public SpriteRenderer thrustVisual;
    [Tooltip("The glint at the tip of the thrust (a child of the hero).")]
    public SpriteRenderer tipVisual;

    float sinceThrust = 99f;
    Vector2 thrustDirection = Vector2.right;
    float thrustReach;

    float Length => (length + bonusLength * BonusCount) * player.areaMultiplier;
    float Width => width * player.areaMultiplier;

    protected override bool Attack()
    {
        float reach = Length;
        Enemy target = NearestEnemy(reach);
        if (target == null) return false;

        Vector2 origin = transform.position;
        Vector2 direction = DirectionTo(target);
        float halfWidth = Width / 2f;

        // Hit test: how far along the line, and how far off to the side of it, each enemy is.
        foreach (Enemy enemy in Enemy.All)
        {
            Vector2 offset = (Vector2)enemy.transform.position - origin;
            float along = Vector2.Dot(offset, direction);
            float across = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
            float r = enemy.data.radius;
            if (along < -r || along > reach + r || across > halfWidth + r) continue;

            enemy.TakeDamage(Damage, origin, knockback);
            if (evolved) enemy.Pin(pinTime);
        }

        thrustDirection = direction;
        thrustReach = reach;
        thrustVisual.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        sinceThrust = 0f;
        return true;
    }

    protected override void Update()
    {
        base.Update();
        sinceThrust += Time.deltaTime;
        FadeFlash(thrustVisual, sinceThrust, FlashTime, FlashAlpha);

        // The streak shoots out from the hero to the end of the thrust, then thins; the glint rides its tip.
        float length = thrustReach * Mathf.Clamp01(sinceThrust / SnapTime);
        float thin = 1f - 0.5f * Mathf.Clamp01(sinceThrust / FlashTime);
        thrustVisual.transform.localPosition = thrustDirection * (length / 2f);
        thrustVisual.transform.localScale = new Vector3(length, Width * thin, 1f);
        FadeFlash(tipVisual, sinceThrust, FlashTime, 1f);
        tipVisual.transform.localPosition = thrustDirection * length;
        tipVisual.transform.localScale = Vector3.one * TipSize;
    }

    void OnDisable()
    {
        if (thrustVisual != null) FadeFlash(thrustVisual, FlashTime, FlashTime, FlashAlpha); // hide
        if (tipVisual != null) FadeFlash(tipVisual, FlashTime, FlashTime, 1f);
    }
}
