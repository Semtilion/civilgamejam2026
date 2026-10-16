using UnityEngine;

// Relic weapon (magic): lobs a fire bomb at the nearest enemy in range; it bursts into a burning patch that hurts
// every enemy inside on every tick. Damage is per tick. Bonus: bigger patch.
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
    [Tooltip("Seconds the bomb flies before it bursts.")]
    public float flightTime = 0.4f;
    public Sprite bombSprite;
    public Color bombColor = new Color(1f, 0.5f, 0.15f);

    protected override bool Attack()
    {
        Enemy target = NearestEnemy(range);
        if (target == null) return false;

        float radius = (patchRadius + bonusRadius * BonusCount) * player.areaMultiplier;
        FireBomb.Throw(bombSprite, bombColor, transform.position, target.transform.position, flightTime, radius,
            burnTime, tickInterval, Damage, player);
        return true;
    }
}
