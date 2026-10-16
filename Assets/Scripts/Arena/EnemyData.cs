using UnityEngine;

public enum EnemyBehavior
{
    Chaser,  // runs straight at the hero
    Shooter, // keeps its distance and fires projectiles
    Boss     // chases, and every few seconds winds up and charges
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

    [Header("Shooter only")]
    public float preferredDistance = 6f;
    public float fireInterval = 2.5f;
    public float projectileSpeed = 4f;
    public int projectileDamage = 8;

    [Header("Boss only")]
    public float chargeInterval = 5f;
    public float chargeWindup = 0.8f;
    public float chargeSpeed = 12f;
    public float chargeDuration = 0.6f;
}
