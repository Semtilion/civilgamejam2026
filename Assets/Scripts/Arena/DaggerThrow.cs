using UnityEngine;

// Throws a dagger at the nearest enemy in range. Each dagger stops at the first enemy it hits.
// Bonus: +1 dagger per throw (fanned out).
// Evolution (Ricochet, from the Elder): after every hit a dagger may bounce on to a nearby enemy (see Dagger).
public class DaggerThrow : Weapon
{
    [Header("Dagger")]
    [Tooltip("Only throws at enemies this close; daggers vanish after flying this far.")]
    public float range = 10f;
    public float speed = 12f;
    public float knockback = 2f;
    [Tooltip("Degrees between daggers when throwing more than one.")]
    public float spreadAngle = 12f;
    public Sprite daggerSprite;
    public Color daggerColor = new Color(0.85f, 0.85f, 0.90f);
    [Header("Ricochet (evolved)")]
    [Tooltip("Chance after every hit to bounce to the nearest other enemy within bounceRange.")]
    [Range(0f, 1f)] public float bounceChance = 0.2f;
    public int maxBounces = 3;
    public float bounceRange = 4f;

    protected override bool Attack()
    {
        Enemy target = NearestEnemy(range);
        if (target == null) return false;

        Vector2 aim = DirectionTo(target);
        int count = 1 + BonusCount;
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) / 2f) * spreadAngle;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * aim;
            Dagger.Throw(daggerSprite, daggerColor, transform.position, direction, speed, range, Damage, knockback, player,
                evolved ? maxBounces : 0, bounceChance, bounceRange);
        }
        Sfx.Play(Sound.Whoosh);
        return true;
    }
}
