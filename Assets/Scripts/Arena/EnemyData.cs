using UnityEngine;

public enum EnemyBehavior
{
    Chaser,  // runs straight at the hero
    Shooter, // never moves; flashes red, then fires at where the hero was when the flash started
    Boss,    // chases, and every few seconds winds up and charges
    Crate    // a breakable prop (supply crate, crystal cluster): never moves, harmless, weapons break it for its loot (not a kill)
}

// Stats for one enemy type. Enemies are built from this at spawn time (no prefab needed).
[CreateAssetMenu(menuName = "Jam/Enemy")]
public class EnemyData : ScriptableObject
{
    public string displayName;
    [Tooltip("Placeholder circle for now. Real art should be 1 unit wide: the size comes from Radius.")]
    public Sprite sprite;
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
    [Tooltip("Chance (0-1) to drop gold coins on death (crates never do).")]
    [Range(0f, 1f)] public float goldChance;
    public int goldAmount;

    [Header("Shooter only (stands still, ignores moveSpeed)")]
    [Tooltip("Only starts a shot when the hero is this close.")]
    public float fireRange = 10f;
    [Tooltip("Red flash before each shot. The aim locks when the flash starts.")]
    public float fireWarning = 0.5f;
    [Tooltip("Seconds per shot, including the warning flash.")]
    public float fireInterval = 2.5f;
    public float projectileSpeed = 4f;
    public int projectileDamage = 8;

    [Header("Boss only")]
    public float chargeInterval = 5f;
    public float chargeWindup = 0.8f;
    public float chargeSpeed = 12f;
    public float chargeDuration = 0.6f;
}
