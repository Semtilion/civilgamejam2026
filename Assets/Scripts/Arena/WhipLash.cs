using UnityEngine;

// Lashes a straight line toward the nearest enemy (a bit longer than the sword's reach), hitting everything in it.
// Only lashes when an enemy is within reach. Bonus: longer line.
// Look: the lash snaps out from the hero to its full length, then thins and fades.
public class WhipLash : Weapon
{
    const float SnapTime = 0.07f;
    const float FlashTime = 0.22f;
    const float FlashAlpha = 0.85f;

    [Header("Whip")]
    public float length = 3.2f;
    public float width = 0.8f;
    [Tooltip("Extra length at levels 3 and 5.")]
    public float bonusLength = 0.6f;
    public float knockback = 5f;
    [Tooltip("Bar that flashes along the lash (placeholder visual).")]
    public SpriteRenderer lashVisual;

    float sinceLash = 99f;
    Vector2 lashDirection = Vector2.right;
    float lashReach;

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
            if (along >= -r && along <= reach + r && across <= halfWidth + r)
                enemy.TakeDamage(Damage, origin, knockback);
        }

        lashDirection = direction;
        lashReach = reach;
        lashVisual.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        sinceLash = 0f;
        return true;
    }

    protected override void Update()
    {
        base.Update();
        sinceLash += Time.deltaTime;
        FadeFlash(lashVisual, sinceLash, FlashTime, FlashAlpha);

        // Placeholder visual: a bar from the hero that shoots out to the end of the lash, then thins.
        float length = lashReach * Mathf.Clamp01(sinceLash / SnapTime);
        float thin = 1f - 0.5f * Mathf.Clamp01(sinceLash / FlashTime);
        lashVisual.transform.localPosition = lashDirection * (length / 2f);
        lashVisual.transform.localScale = new Vector3(length, Width * thin, 1f);
    }

    void OnDisable()
    {
        if (lashVisual != null) FadeFlash(lashVisual, FlashTime, FlashTime, FlashAlpha); // hide
    }
}
