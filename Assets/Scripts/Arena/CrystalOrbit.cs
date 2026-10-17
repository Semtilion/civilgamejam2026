using System.Collections.Generic;
using UnityEngine;

// The Shaman's weapon: crystals circle the hero all the time and hit whatever they touch.
// An enemy can be hit by the orbit at most once per `interval` (0.5s). Bonus: +1 crystal.
public class CrystalOrbit : Weapon
{
    const int MaxRememberedHits = 1000;

    [Header("Crystal Orbit")]
    public int baseCrystals = 2;
    public float orbitRadius = 1.8f;
    [Tooltip("Seconds per full circle.")]
    public float orbitTime = 2f;
    public float crystalRadius = 0.3f;
    public float knockback = 3f;
    public Sprite crystalSprite;
    public Color crystalColor = new Color(0.75f, 0.45f, 1f);

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
        }
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
            Glow.Add(crystal.gameObject, crystalColor, 1.6f, 0.7f);
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
