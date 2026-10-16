using UnityEngine;
using UnityEngine.InputSystem;

// Hero stats that town NPC buffs and level-up cards can raise. See Player.AddStat.
public enum StatType { MaxHp, MoveSpeed, Damage, PhysicalDamage, MagicDamage, AttackSpeed, Area, PickupRange }

// The hero: moves with the project's "Player/Move" action (WASD, arrow keys, gamepad stick), stays inside the arena,
// has HP and a short invulnerability after each hit.
// Base stats are where every dive starts; the dive stats below = base + town buffs + level-up cards.
public class Player : MonoBehaviour
{
    static readonly string[] StatLabels =
        { "max HP", "move speed", "damage", "physical damage", "magic damage", "attack speed", "area", "pickup range" };

    [Header("Base stats (every dive starts from these)")]
    public int baseMaxHp = 100;
    public float baseMoveSpeed = 5f;
    [Tooltip("Pickups within this distance fly to the hero.")]
    public float basePickupRange = 1.5f;
    [Tooltip("Hit size in world units.")]
    public float radius = 0.5f;
    [Tooltip("Seconds of invulnerability after taking a hit.")]
    public float invulnerableTime = 0.5f;

    [Header("Current dive (base + town buffs + level-up cards)")]
    public int maxHp;
    public float moveSpeed;
    public float pickupRange;
    public float physicalDamageMultiplier = 1f;
    public float magicDamageMultiplier = 1f;
    public float attackSpeedMultiplier = 1f;
    public float areaMultiplier = 1f;

    [Header("Scene references")]
    public ArenaManager arena;
    public SpriteRenderer sprite;

    public int Hp { get; private set; }
    public bool IsDead => Hp <= 0;

    InputAction moveAction;
    float invulnerableTimer;

    void Awake()
    {
        moveAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Move") : null;
        if (moveAction == null) Debug.LogError("No 'Player/Move' action found in the project-wide Input Actions.");
        else moveAction.Enable();
    }

    void Update()
    {
        if (IsDead) return;

        Vector2 input = moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
        Vector2 position = (Vector2)transform.position + input * (moveSpeed * Time.deltaTime);
        Rect bounds = arena.Bounds;
        position.x = Mathf.Clamp(position.x, bounds.xMin + radius, bounds.xMax - radius);
        position.y = Mathf.Clamp(position.y, bounds.yMin + radius, bounds.yMax - radius);
        transform.position = position;

        if (invulnerableTimer > 0f)
        {
            invulnerableTimer -= Time.deltaTime;
            sprite.enabled = invulnerableTimer <= 0f || Mathf.Repeat(invulnerableTimer, 0.1f) > 0.05f; // blink
        }
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || invulnerableTimer > 0f || amount <= 0 || !arena.IsPlaying) return;

        Hp = Mathf.Max(0, Hp - amount);
        invulnerableTimer = invulnerableTime;
        if (IsDead) arena.OnPlayerDied();
    }

    public void Heal(int amount)
    {
        Hp = Mathf.Min(maxHp, Hp + amount);
    }

    // MaxHp: flat points (also heals them). MoveSpeed / PickupRange: a fraction of the base value (0.1 = +10%).
    // The rest add to their multiplier (0.1 = +10%). Damage raises both physical and magic.
    public void AddStat(StatType stat, float amount)
    {
        switch (stat)
        {
            case StatType.MaxHp:
                maxHp += Mathf.RoundToInt(amount);
                Heal(Mathf.RoundToInt(amount));
                break;
            case StatType.MoveSpeed: moveSpeed += baseMoveSpeed * amount; break;
            case StatType.Damage:
                physicalDamageMultiplier += amount;
                magicDamageMultiplier += amount;
                break;
            case StatType.PhysicalDamage: physicalDamageMultiplier += amount; break;
            case StatType.MagicDamage: magicDamageMultiplier += amount; break;
            case StatType.AttackSpeed: attackSpeedMultiplier += amount; break;
            case StatType.Area: areaMultiplier += amount; break;
            case StatType.PickupRange: pickupRange += basePickupRange * amount; break;
        }
    }

    // "+10 max HP", "+5% physical damage", ... (same units as AddStat).
    public static string StatText(StatType stat, float amount)
    {
        return stat == StatType.MaxHp ? $"+{amount:0} {StatLabels[(int)stat]}" : $"+{amount * 100f:0}% {StatLabels[(int)stat]}";
    }

    // Back to base stats and full HP, in the middle of the cave.
    public void ResetForNewRun()
    {
        maxHp = baseMaxHp;
        moveSpeed = baseMoveSpeed;
        pickupRange = basePickupRange;
        physicalDamageMultiplier = 1f;
        magicDamageMultiplier = 1f;
        attackSpeedMultiplier = 1f;
        areaMultiplier = 1f;

        Hp = maxHp;
        invulnerableTimer = 0f;
        sprite.enabled = true;
        transform.position = arena.Bounds.center;
    }
}
