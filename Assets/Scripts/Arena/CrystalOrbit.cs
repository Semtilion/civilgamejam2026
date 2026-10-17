using System.Collections.Generic;
using UnityEngine;

// Crystals circle the hero all the time and hit whatever they touch.
// An enemy can be hit by the orbit at most once per `interval` (0.5s). Bonus: +1 crystal.
// Evolution (Shatter, from the Shaman): a hit may make the crystal burst, hitting every enemy around it.
public class CrystalOrbit : Weapon
{
    const int MaxRememberedHits = 1000;
    static readonly Color ShatterColor = new Color(0.75f, 0.45f, 1f, 0.6f);

    [Header("Crystal Orbit")]
    public int baseCrystals = 2;
    public float orbitRadius = 1.8f;
    [Tooltip("Seconds per full circle.")]
    public float orbitTime = 2f;
    public float crystalRadius = 0.3f;
    public float knockback = 3f;
    [Tooltip("The artist's crystal (1 unit tall), drawn crystalRadius x 2 tall.")]
    public Sprite crystalSprite;
    [Tooltip("Tint (white = the art's own colors).")]
    public Color crystalColor = Color.white;
    public Color glowColor = new Color(0.75f, 0.45f, 1f);
    [Header("Shatter (evolved)")]
    [Tooltip("Chance per crystal hit to burst.")]
    [Range(0f, 1f)] public float shatterChance = 0.25f;
    [Tooltip("The burst hits every enemy this close to the crystal (times area), for the orbit's damage.")]
    public float shatterRadius = 1.5f;
    [Tooltip("The burst's flash and its ring (white, tinted violet).")]
    public Sprite burstSprite;
    public Sprite burstRingSprite;

    readonly List<Transform> crystals = new List<Transform>();
    readonly Dictionary<Enemy, float> nextHitTime = new Dictionary<Enemy, float>();
    float angle;

    // Hits happen continuously while crystals touch enemies, not on a cooldown, so this is never used.
    protected override bool Attack() => false;

    // Replaces the base cooldown loop: spin the crystals and check what they touch every frame.
    protected override void Update()
    {
        if (!player.arena.IsPlaying) return;

        SyncCrystalCount();
        angle += 360f / orbitTime * Time.deltaTime;
        float radius = orbitRadius * player.areaMultiplier;
        for (int i = 0; i < crystals.Count; i++)
        {
            float a = (angle + 360f * i / crystals.Count) * Mathf.Deg2Rad;
            Vector2 position = (Vector2)transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            crystals[i].position = position;
            HitAround(position);
        }
    }

    void HitAround(Vector2 position)
    {
        if (nextHitTime.Count > MaxRememberedHits) nextHitTime.Clear(); // forget long-dead enemies now and then

        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy.IsDead) continue;
            float reach = crystalRadius + enemy.data.radius;
            if (((Vector2)enemy.transform.position - position).sqrMagnitude > reach * reach) continue;
            if (nextHitTime.TryGetValue(enemy, out float time) && Time.time < time) continue;

            nextHitTime[enemy] = Time.time + interval / player.attackSpeedMultiplier;
            enemy.TakeDamage(Damage, transform.position, knockback);
            if (evolved && Random.value < shatterChance) Shatter(position);
        }
    }

    // Hits every enemy within shatterRadius of the crystal (the one it touched too, if it's still alive), with a
    // violet flash. Not a weapon hit, so it doesn't set off on-hit relics.
    void Shatter(Vector2 center)
    {
        float radius = shatterRadius * player.areaMultiplier;
        foreach (Enemy enemy in Enemy.All)
        {
            float reach = radius + enemy.data.radius;
            if (!enemy.IsDead && ((Vector2)enemy.transform.position - center).sqrMagnitude <= reach * reach)
                enemy.TakeDamage(Damage, center, 0f, false);
        }
        FadeEffect.Circle(burstSprite, center, radius, ShatterColor, 0.3f);
        FadeEffect.Circle(burstRingSprite, center, radius, ShatterColor, 0.25f);
    }

    // One crystal object per crystal; more are added when the level bonus kicks in.
    void SyncCrystalCount()
    {
        int wanted = baseCrystals + BonusCount;
        while (crystals.Count < wanted)
        {
            SpriteRenderer crystal = new GameObject("Crystal").AddComponent<SpriteRenderer>();
            crystal.sprite = crystalSprite;
            crystal.color = crystalColor;
            crystal.sortingOrder = 4;
            Fx.Unlit(crystal);
            Glow.Add(crystal.gameObject, glowColor, 1.6f, 0.7f);
            crystal.transform.localScale = Vector3.one * crystalRadius * 2f; // placeholder circle is 1 unit wide
            crystals.Add(crystal.transform);
        }
        while (crystals.Count > wanted)
        {
            Destroy(crystals[crystals.Count - 1].gameObject);
            crystals.RemoveAt(crystals.Count - 1);
        }
    }

    void OnDisable()
    {
        foreach (Transform crystal in crystals)
            if (crystal != null) Destroy(crystal.gameObject);
        crystals.Clear();
        nextHitTime.Clear();
    }
}
