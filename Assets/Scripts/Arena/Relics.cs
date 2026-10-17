using System.Collections.Generic;
using UnityEngine;

public enum RelicId
{
    QuakeHammer, LightningStaff, FireBomb, Axe,         // new weapons (the first four, in RelicWeapons order)
    GlassCannon, SecondWind, Bloodstone, BerserkerMask, // change how you play
    VolatileCore, StormOrb, FrostShard,                 // on-hit items
}

// Relics: rare, permanent finds. Every boss killed on a dive drops one random relic you don't have
// yet. It works right away and is yours for good once you get home (lost if you die on that dive, like other loot).
// Weapon relics are equipped at Lv 1 at the start of every dive and raid; the rest change how you play.
// Sits on the Arena Manager object; ArenaManager, Enemy and the weapons ask it (Has, DamageMultiplier, OnWeaponHit...).
public class Relics : MonoBehaviour
{
    public static readonly string[] Names = // in RelicId order
    {
        "Quake Hammer", "Lightning Staff", "Fire Bomb", "Axe",
        "Glass Cannon", "Second Wind", "Bloodstone", "Berserker Mask",
        "Volatile Core", "Storm Orb", "Frost Shard",
    };

    // The weapons the first relics give, in RelicId order.
    static readonly WeaponId[] RelicWeapons = { WeaponId.QuakeHammer, WeaponId.LightningStaff, WeaponId.FireBomb, WeaponId.Axe };

    static readonly Color BurstColor = new Color(1f, 1f, 1f, 0.5f);
    static readonly Color ExplosionColor = new Color(1f, 0.55f, 0.15f, 0.6f);
    static readonly Color LightningColor = new Color(1f, 0.95f, 0.5f);

    [Header("Glass Cannon")]
    public float glassCannonDamage = 0.5f;
    [Tooltip("Share of max HP lost (0.4 = -40%).")]
    public float glassCannonHpLoss = 0.4f;

    [Header("Second Wind (once per dive)")]
    [Tooltip("Share of max HP you get back up with.")]
    public float secondWindHp = 0.5f;
    public float secondWindInvulnerable = 2f;
    [Tooltip("Enemies this close are knocked away.")]
    public float secondWindRadius = 4f;
    public float secondWindPush = 12f;

    [Header("Bloodstone")]
    public int healPerKill = 1;

    [Header("Berserker Mask")]
    [Tooltip("Extra damage near 0 HP (0.6 = +60%), less the more HP you have.")]
    public float berserkerMaxDamage = 0.6f;

    [Header("On-hit items (every weapon hit rolls; their own damage never triggers more)")]
    [Range(0f, 1f)] public float procChance = 0.1f;
    public float explosionRadius = 1.5f;
    [Tooltip("Share of the hit's damage.")]
    public float explosionShare = 0.5f;
    public int chainTargets = 3;
    [Tooltip("How far each lightning jump reaches.")]
    public float chainRange = 4f;
    public float chainShare = 0.5f;
    public float slowTime = 3f;
    [Tooltip("0.5 = 50% slower (moving and attacking).")]
    public float slowAmount = 0.5f;
    public float bossSlowAmount = 0.25f;

    [Header("Scene references")]
    public ArenaManager arena;
    public Sprite circleSprite;
    public Sprite squareSprite;

    readonly bool[] active = new bool[Names.Length]; // owned + found on this dive
    readonly List<RelicId> foundThisDive = new List<RelicId>();
    bool secondWindUsed;

    public bool Has(RelicId id) => active[(int)id];
    public bool AnyMissing => System.Array.IndexOf(active, false) >= 0; // a relic could still be found
    public IReadOnlyList<RelicId> FoundThisDive => foundThisDive;

    // Start of a dive or the horde, after the town loadout: the relics you own (weapons at Lv 1).
    public void ResetForNewRun(bool[] owned)
    {
        foundThisDive.Clear();
        secondWindUsed = false;
        for (int i = 0; i < active.Length; i++)
        {
            active[i] = i < owned.Length && owned[i];
            if (active[i]) Apply((RelicId)i);
        }
    }

    // A boss died: one random relic you don't have yet, working right away. False if you have them all.
    public bool TryFind(out RelicId found)
    {
        List<int> missing = new List<int>();
        for (int i = 0; i < active.Length; i++)
            if (!active[i]) missing.Add(i);

        found = default;
        if (missing.Count == 0) return false;
        found = (RelicId)missing[Random.Range(0, missing.Count)];
        active[(int)found] = true;
        foundThisDive.Add(found);
        Apply(found);
        return true;
    }

    // Debug (F1 list): this relic, working right away. The caller also makes it owned for good.
    public void DebugGive(RelicId id)
    {
        if (active[(int)id]) return;
        active[(int)id] = true;
        Apply(id);
    }

    // Relics that change something once: weapons (equipped at Lv 1) and Glass Cannon. The rest are checked as they happen.
    void Apply(RelicId id)
    {
        Weapon weapon = WeaponFor(id);
        if (weapon != null)
        {
            weapon.level = 1;
            weapon.enabled = true;
        }
        else if (id == RelicId.GlassCannon)
        {
            arena.player.AddStat(StatType.Damage, glassCannonDamage);
            arena.player.ScaleMaxHp(1f - glassCannonHpLoss);
        }
    }

    // The hero's weapon component a weapon relic gives, or null for the other relics.
    Weapon WeaponFor(RelicId id)
    {
        if ((int)id >= RelicWeapons.Length) return null;
        foreach (Weapon weapon in arena.leveling.weapons)
            if (weapon.id == RelicWeapons[(int)id]) return weapon;
        return null;
    }

    public string Describe(RelicId id)
    {
        Weapon weapon = WeaponFor(id);
        if (weapon != null) return $"New weapon: {weapon.description}";

        switch (id)
        {
            case RelicId.GlassCannon:
                return $"+{glassCannonDamage * 100f:0}% damage, -{glassCannonHpLoss * 100f:0}% max HP";
            case RelicId.SecondWind:
                return $"Once per dive: revive with {secondWindHp * 100f:0}% HP";
            case RelicId.Bloodstone:
                return $"Kills heal {healPerKill} HP";
            case RelicId.BerserkerMask:
                return $"Up to +{berserkerMaxDamage * 100f:0}% damage at low HP";
            case RelicId.VolatileCore:
                return $"Hits: {procChance * 100f:0}% chance to explode";
            case RelicId.StormOrb:
                return $"Hits: {procChance * 100f:0}% chance to chain lightning";
            default: // FrostShard
                return $"Hits: {procChance * 100f:0}% chance to slow";
        }
    }

    // ---------- Effects ----------

    // Berserker Mask: x1 at full HP, up to x(1 + berserkerMaxDamage) near 0 HP. Every weapon's damage uses this.
    public float DamageMultiplier
    {
        get
        {
            if (!Has(RelicId.BerserkerMask)) return 1f;
            Player player = arena.player;
            return 1f + berserkerMaxDamage * (1f - (float)player.Hp / player.maxHp);
        }
    }

    // Bloodstone: an enemy was killed (not a crate).
    public void OnKill()
    {
        if (Has(RelicId.Bloodstone)) arena.player.Heal(healPerKill);
    }

    // Second Wind: called when the hero dies. True = they got back up (once per dive), enemies around knocked away.
    public bool TrySecondWind()
    {
        if (!Has(RelicId.SecondWind) || secondWindUsed) return false;
        secondWindUsed = true;

        Player player = arena.player;
        player.Revive(secondWindHp, secondWindInvulnerable);
        Vector2 center = player.transform.position;
        foreach (Enemy enemy in Enemy.All)
        {
            float reach = secondWindRadius + enemy.radius;
            if (!enemy.IsDead && ((Vector2)enemy.transform.position - center).sqrMagnitude <= reach * reach)
                enemy.TakeDamage(0, center, secondWindPush, false); // a push, no damage (only chasers get pushed)
        }
        FadeEffect.Circle(circleSprite, center, secondWindRadius, BurstColor, 0.4f);
        return true;
    }

    // Every weapon hit (Enemy.TakeDamage) rolls the on-hit items.
    public void OnWeaponHit(Enemy enemy, int damage)
    {
        if (Has(RelicId.VolatileCore) && Random.value < procChance)
            Explode(enemy.transform.position, Share(damage, explosionShare));
        if (Has(RelicId.StormOrb) && Random.value < procChance)
            ChainLightning(enemy, Share(damage, chainShare));
        if (Has(RelicId.FrostShard) && Random.value < procChance)
            enemy.Slow(slowTime, enemy.data.behavior == EnemyBehavior.Boss ? bossSlowAmount : slowAmount);
    }

    static int Share(int damage, float share) => Mathf.Max(1, Mathf.RoundToInt(damage * share));

    // Volatile Core: hits every enemy within explosionRadius (the one that was hit too, if it's still alive).
    void Explode(Vector2 center, int damage)
    {
        foreach (Enemy enemy in Enemy.All)
        {
            float reach = explosionRadius + enemy.radius;
            if (!enemy.IsDead && ((Vector2)enemy.transform.position - center).sqrMagnitude <= reach * reach)
                enemy.TakeDamage(damage, center, 0f, false);
        }
        FadeEffect.Circle(circleSprite, center, explosionRadius, ExplosionColor, 0.25f);
    }

    // Storm Orb: lightning jumps from the hit enemy to the nearest enemy not struck yet, `chainTargets` times.
    void ChainLightning(Enemy from, int damage)
    {
        List<Enemy> struck = new List<Enemy> { from };
        Vector2 current = from.transform.position;
        for (int jump = 0; jump < chainTargets; jump++)
        {
            Enemy next = null;
            float best = chainRange * chainRange;
            foreach (Enemy enemy in Enemy.All)
            {
                if (enemy.IsDead || struck.Contains(enemy)) continue;
                float distance = ((Vector2)enemy.transform.position - current).sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    next = enemy;
                }
            }
            if (next == null) return;

            Vector2 target = next.transform.position;
            FadeEffect.Line(squareSprite, current, target, 0.12f, LightningColor, 0.15f);
            next.TakeDamage(damage, current, 0f, false);
            struck.Add(next);
            current = target;
        }
    }
}
