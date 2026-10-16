using UnityEngine;

// Every weapon in the game. The town saves weapon levels by this id (Campaign.weaponLevels).
public enum WeaponId { Sword, Dagger, Whip, CrystalOrbit }

// Base for the hero's auto-firing weapons. All weapons sit on the hero; owned = component enabled.
// Levels 1-5: every level adds damage, levels 3 and 5 also add the weapon's bonus (BonusCount).
// The hero's damage / attack speed / area multipliers (town buffs, level-up cards) apply to every weapon.
public abstract class Weapon : MonoBehaviour
{
    public const int MaxLevel = 5;
    public static readonly string[] Names = { "Sword", "Dagger", "Whip", "Crystal Orbit" }; // in WeaponId order

    [Header("Weapon")]
    public WeaponId id;
    [Tooltip("Magic weapons use the hero's magic damage (Shaman); the others use physical damage.")]
    public bool isMagic;
    [Tooltip("What the weapon does (for UI and docs).")]
    public string description;
    [Tooltip("What levels 3 and 5 add (for UI and docs).")]
    public string bonusText;
    public int baseDamage = 10;
    public int damagePerLevel = 3;
    [Tooltip("Seconds between attacks, before attack speed bonuses.")]
    public float interval = 1f;

    [Header("Live")]
    [Range(1, MaxLevel)] public int level = 1;
    [Tooltip("Extra damage from level-up cards (this dive only).")]
    public int cardDamage;

    protected Player player;
    float timer;

    public string DisplayName => Names[(int)id];
    protected int BonusCount => (level >= 3 ? 1 : 0) + (level >= 5 ? 1 : 0);
    protected float DamageMultiplier => isMagic ? player.magicDamageMultiplier : player.physicalDamageMultiplier;
    protected int Damage => Mathf.RoundToInt((baseDamage + damagePerLevel * (level - 1) + cardDamage) * DamageMultiplier);

    protected virtual void Awake()
    {
        player = GetComponent<Player>();
    }

    protected virtual void Update()
    {
        if (!player.arena.IsPlaying) return;

        timer -= Time.deltaTime;
        if (timer <= 0f && Attack()) timer = interval / player.attackSpeedMultiplier;
    }

    // Returns false when there was nothing to attack, so it tries again next frame.
    protected abstract bool Attack();

    protected Enemy NearestEnemy(float maxRange)
    {
        Enemy nearest = null;
        float best = maxRange * maxRange;
        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy.IsDead) continue;
            float distance = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude;
            if (distance <= best)
            {
                best = distance;
                nearest = enemy;
            }
        }
        return nearest;
    }

    // Direction to an enemy, with a fallback when it's standing right on the hero.
    protected Vector2 DirectionTo(Enemy enemy)
    {
        Vector2 offset = enemy.transform.position - transform.position;
        return offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.right;
    }

    // Shows a placeholder flash that fades out over `flashTime` after each attack.
    protected static void FadeFlash(SpriteRenderer visual, float sinceAttack, float flashTime, float maxAlpha)
    {
        Color color = visual.color;
        color.a = Mathf.Clamp01(1f - sinceAttack / flashTime) * maxAlpha;
        visual.color = color;
    }
}
