using UnityEngine;

public enum EnemyBehavior
{
    Chaser,  // runs straight at the hero
    Shooter, // never moves; warns (swells, red), then fires a volley in fixed directions (shotAngles)
    Boss,    // chases, and every few seconds winds up and charges
    Crate    // a breakable prop (supply crate, crystal cluster): never moves, harmless, weapons break it for its loot (not a kill)
}

// Stats for one enemy type. Enemies are built from this at spawn time (no prefab needed).
[CreateAssetMenu(menuName = "Jam/Enemy")]
public class EnemyData : ScriptableObject
{
    public string displayName;
    [Tooltip("Scaled by Radius x 2, so a 1-unit-wide sprite (the placeholders) fills the hit circle. A Spitter's stem.")]
    public Sprite sprite;
    [Tooltip("Optional walk cycle: shown in turn as it walks (the first is usually Sprite). Empty = Sprite only.")]
    public Sprite[] walkFrames;
    [Tooltip("The art faces left: mirrored while walking right (off: faces right, mirrored walking left). Only with walk frames.")]
    public bool artFacesLeft;
    public Color color = Color.white;
    [Tooltip("Size in world units (hit size and sprite size).")]
    public float radius = 0.4f;

    [Header("Stats (depth 1, scaled up on deeper levels)")]
    public int maxHp = 10;
    public float moveSpeed = 2f;
    public int contactDamage = 5;
    public EnemyBehavior behavior;
    [Tooltip("XP gem dropped on death. A boss's XP (plus every gem on the floor) is collected automatically.")]
    public int xp = 1;

    [Header("Loot")]
    [Tooltip("Chance (0-1) to drop materials on death. Bosses' materials are collected automatically.")]
    [Range(0f, 1f)] public float materialChance;
    public int materialAmount = 1;
    [Tooltip("Crystals dropped when a Crate-type enemy (e.g. a crystal cluster) is broken.")]
    public int crystalAmount;
    [Tooltip("Supplies (for the town's injured) dropped when a Crate-type enemy (e.g. a supply crate) is broken.")]
    public int supplyAmount;
    [Tooltip("Chance (0-1) to drop gold coins on death (crates never do).")]
    [Range(0f, 1f)] public float goldChance;
    public int goldAmount;

    [Header("Shooter only (stands still, ignores moveSpeed)")]
    [Tooltip("Optional: the part that sways, then shakes, swells and glows red before each volley (the Spitter's flower), " +
             "drawn over Sprite at the same size and center (both images share one canvas). Empty = Sprite does it all.")]
    public Sprite flower;
    [Tooltip("Each volley fires one shot per entry, all at once, in these directions (degrees: 0 = right, counter-clockwise; " +
             "the flower's tips). They never turn.")]
    public float[] shotAngles = { 18f, 90f, 162f, 234f, 306f };
    [Tooltip("The shots' art, pointing right (each turns to face where it flies). Empty = Sprite.")]
    public Sprite projectileSprite;
    [Tooltip("Only starts a shot when the hero is this close.")]
    public float fireRange = 10f;
    [Tooltip("The warning (swell, shake, red) before each volley.")]
    public float fireWarning = 0.5f;
    [Tooltip("Seconds per volley, including the warning.")]
    public float fireInterval = 2.5f;
    public float projectileSpeed = 4f;
    public int projectileDamage = 8;

    [Header("Boss only")]
    public float chargeInterval = 5f;
    public float chargeWindup = 0.8f;
    public float chargeSpeed = 12f;
    public float chargeDuration = 0.6f;
}
