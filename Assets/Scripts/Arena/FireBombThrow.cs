using UnityEngine;

// Relic weapon: lobs a fire bomb at the nearest enemy in range (FireBomb: it catches fire at the top of its arc and
// breaks on landing) into a burning patch that hurts every enemy inside on every tick. Damage is per tick.
// Bonus: bigger patch.
public class FireBombThrow : Weapon
{
    [Header("Fire Bomb")]
    [Tooltip("Only throws at enemies this close.")]
    public float range = 8f;
    public float patchRadius = 1.2f;
    [Tooltip("Extra patch radius at levels 3 and 5.")]
    public float bonusRadius = 0.3f;
    public float burnTime = 2f;
    public float tickInterval = 0.5f;
    [Tooltip("Seconds the bomb flies (an arc) before it breaks.")]
    public float flightTime = 0.6f;
    [Tooltip("The bomb (spins in flight; keeps its own colors).")]
    public Sprite bombSprite;
    [Tooltip("The fire: on the bomb from the top of its arc, then the burning patch, over a scorch mark.")]
    public Sprite fireSprite;
    public Sprite scorchSprite;
    [Tooltip("The flames' color (and their light).")]
    public Color fireColor = new Color(1f, 0.5f, 0.15f);
    [Tooltip("The shards when it breaks: the bomb art's average color (set by the builder).")]
    public Color shardColor = new Color(0.47f, 0.41f, 0.38f);

    protected override bool Attack()
    {
        Enemy target = NearestEnemy(range);
        if (target == null) return false;

        float radius = (patchRadius + bonusRadius * BonusCount) * player.areaMultiplier;
        FireBomb.Throw(this, transform.position, target.transform.position, radius, Damage, player);
        return true;
    }
}
