using UnityEngine;

// Relic weapon: throws a heavy axe at the nearest enemy in range. It spins out, then flies back to the
// hero, hitting everything it passes (once on the way out, once on the way back). Bonus: +1 axe (fanned out).
public class AxeThrow : Weapon
{
    [Header("Axe")]
    [Tooltip("Only throws at enemies this close.")]
    public float range = 8f;
    [Tooltip("How far it flies before coming back.")]
    public float distance = 5f;
    public float speed = 7f;
    public float axeRadius = 0.6f;
    public float knockback = 5f;
    [Tooltip("Degrees between axes when throwing more than one.")]
    public float spreadAngle = 20f;
    public Sprite axeSprite;
    [Tooltip("Tint (white = the art's own colors).")]
    public Color axeColor = Color.white;

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
            Axe.Throw(axeSprite, axeColor, transform.position, direction, speed, distance, axeRadius * player.areaMultiplier,
                Damage, knockback, player);
        }
        Sfx.Play(Sound.Whoosh);
        return true;
    }
}
