using UnityEngine;
using UnityEngine.InputSystem;

// Hero stats that town NPC buffs, the crystal shop and level-up cards can raise. See Player.AddStat.
// Saved as numbers in assets (TownConfig): only add new ones at the end.
public enum StatType { MaxHp, MoveSpeed, Damage, AttackSpeed, Area, PickupRange }

// The hero: moves with the project's "Player/Move" action (WASD, arrow keys, gamepad stick), stays inside the arena,
// has HP and a short invulnerability after each hit (he flashes red, then blinks; ArenaManager.OnHeroHit does the
// rest). His body flash also shows his moments: a level-up, Second Wind's gold, rising into the teleporter.
// Base stats are where every dive starts; the dive stats below = base + town buffs + level-up cards.
public class Player : MonoBehaviour
{
    static readonly string[] StatLabels =
        { "max HP", "move speed", "damage", "attack speed", "area", "pickup range" };

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
    [Tooltip("Every weapon's damage (there is one damage type).")]
    public float damageMultiplier = 1f;
    public float attackSpeedMultiplier = 1f;
    public float areaMultiplier = 1f;

    [Header("Look (the art faces right)")]
    [Tooltip("Shown while standing still. Empty = keep whatever the SpriteRenderer has.")]
    public Sprite idleSprite;
    [Tooltip("Looped while moving.")]
    public Sprite[] walkSprites;
    [Tooltip("Walk pose swaps per second (animation only; move speed is separate).")]
    public float walkFps = 6f;

    [Header("Motion (moves only the Body child, pivoted at the feet)")]
    [Tooltip("How high he bounces on each step, in world units.")]
    public float bobHeight = 0.08f;
    [Tooltip("Degrees he leans into the direction he walks.")]
    public float leanAngle = 6f;
    public float leanSpeed = 10f;
    [Tooltip("Idle breathing: how much he stretches (0.025 = 2.5%), and seconds per breath.")]
    public float breathAmount = 0.025f;
    public float breathTime = 1.6f;

    [Header("Scene references")]
    public ArenaManager arena;
    public SpriteRenderer sprite;
    [Tooltip("The body's flash (red when hit; other colors for his moments). Optional.")]
    public HitFlash bodyFlash;
    [Tooltip("His chips when hit: the art's average color (set by the builder).")]
    public Color particleColor = new Color(0.4f, 0.4f, 0.45f);
    [Tooltip("The art's own transform (a child of the hero). Bob, lean and breathing are applied here.")]
    public Transform body;

    public int Hp { get; private set; }
    public bool IsDead => Hp <= 0;
    // Where his feet are (the body's pivot): columns of light stand here.
    public Vector2 Feet => HasBody ? (Vector2)body.position : (Vector2)transform.position;

    const float HitFlashTime = 0.12f;
    static readonly Color HurtColor = new Color(1f, 0.25f, 0.2f, 0.75f);

    InputAction moveAction;
    float invulnerableTimer;
    float flashTimer, shineTimer; // a short flash (a hit, a level-up), a longer shine (Second Wind, the teleporter)
    Color flashColor, shineColor;
    float walkTime;
    float lean;
    Vector3 bodyRestPosition;

    // Only animate a real child: moving/scaling the hero itself would also move the weapon flashes.
    bool HasBody => body != null && body != transform;

    void Awake()
    {
        if (HasBody) bodyRestPosition = body.localPosition;
        moveAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Move") : null;
        if (moveAction == null) Debug.LogError("No 'Player/Move' action found in the project-wide Input Actions.");
        else moveAction.Enable();
    }

    void Update()
    {
        UpdateFlash(); // also while the game is frozen for a moment (a level-up, the teleporter)
        if (IsDead || !arena.IsPlaying) return;

        Vector2 input = moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
        Vector2 position = (Vector2)transform.position + input * (moveSpeed * Time.deltaTime);
        Rect bounds = arena.Bounds;
        position.x = Mathf.Clamp(position.x, bounds.xMin + radius, bounds.xMax - radius);
        position.y = Mathf.Clamp(position.y, bounds.yMin + radius, bounds.yMax - radius);
        transform.position = position;
        Animate(input);

        if (invulnerableTimer > 0f)
        {
            invulnerableTimer -= Time.deltaTime;
            // The hit flash first, then the blink
            sprite.enabled = flashTimer > 0f || invulnerableTimer <= 0f || Mathf.Repeat(invulnerableTimer, 0.1f) > 0.05f;
        }
    }

    // Faces the way they walk (keeps the last facing when standing or moving straight up/down);
    // idle pose when still, walk frames while moving. Then the body motion: a bounce on every step and a lean into
    // the walk while moving, slow breathing while standing.
    void Animate(Vector2 input)
    {
        if (input.x > 0.01f) sprite.flipX = false;
        else if (input.x < -0.01f) sprite.flipX = true;

        bool moving = input.sqrMagnitude > 0.0001f;
        walkTime = moving ? walkTime + Time.deltaTime : 0f;
        if (moving && walkSprites != null && walkSprites.Length > 0)
            sprite.sprite = walkSprites[(int)(walkTime * walkFps) % walkSprites.Length];
        else if (idleSprite != null)
            sprite.sprite = idleSprite;

        if (!HasBody) return;

        float bob = moving ? bobHeight * Mathf.Abs(Mathf.Sin(walkTime * walkFps * Mathf.PI)) : 0f; // one bounce per frame swap
        lean = Mathf.Lerp(lean, -input.x * leanAngle, 1f - Mathf.Exp(-leanSpeed * Time.deltaTime)); // right = lean clockwise
        float breath = moving ? 0f : breathAmount * Mathf.Sin(Time.time * 2f * Mathf.PI / breathTime);

        body.localPosition = bodyRestPosition + new Vector3(0f, bob, 0f);
        body.localRotation = Quaternion.Euler(0f, 0f, lean);
        body.localScale = new Vector3(1f - breath * 0.5f, 1f + breath, 1f); // stretch up, thin a little
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || invulnerableTimer > 0f || amount <= 0 || !arena.IsPlaying) return;

        Hp = Mathf.Max(0, Hp - amount);
        invulnerableTimer = invulnerableTime;
        Flash(HurtColor, HitFlashTime);
        arena.OnHeroHit(amount);
        if (IsDead) arena.OnPlayerDied();
    }

    // A short flash of the body in `color` (alpha = how strong).
    public void Flash(Color color, float time)
    {
        flashColor = color;
        flashTimer = time;
    }

    // A longer glow in `color` (the blink still shows through).
    public void Shine(Color color, float time)
    {
        shineColor = color;
        shineTimer = time;
    }

    void UpdateFlash()
    {
        if (bodyFlash == null) return;
        flashTimer -= Time.unscaledDeltaTime;
        shineTimer -= Time.unscaledDeltaTime;
        if (flashTimer > 0f) bodyFlash.Color = flashColor;
        else if (shineTimer > 0f) bodyFlash.Color = shineColor;
        bodyFlash.On = flashTimer > 0f || shineTimer > 0f;
    }

    // Stepping into the teleporter (`k`: 0 -> 1): the body rises and stretches thin into the light.
    public void Ascend(float k)
    {
        if (!HasBody) return;
        body.localPosition = bodyRestPosition + Vector3.up * 1.2f * k * k;
        body.localRotation = Quaternion.identity;
        body.localScale = new Vector3(1f - 0.85f * k, 1f + 0.6f * k, 1f);
    }

    // Back to normal (a new depth or run): the body in place, shown, no flash.
    public void ResetLook()
    {
        flashTimer = shineTimer = 0f;
        sprite.enabled = true;
        if (bodyFlash != null) bodyFlash.On = false;
        if (!HasBody) return;
        body.localPosition = bodyRestPosition;
        body.localRotation = Quaternion.identity;
        body.localScale = Vector3.one;
    }

    public void Heal(int amount)
    {
        Hp = Mathf.Min(maxHp, Hp + amount);
    }

    // Glass Cannon: max HP x factor (HP drops with it if needed).
    public void ScaleMaxHp(float factor)
    {
        maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * factor));
        Hp = Mathf.Min(Hp, maxHp);
    }

    // Second Wind: back up from 0 HP with a share of max HP and a longer invulnerability.
    public void Revive(float hpShare, float invulnerable)
    {
        Hp = Mathf.Max(1, Mathf.RoundToInt(maxHp * hpShare));
        invulnerableTimer = invulnerable;
    }

    // MaxHp: flat points (also heals them). MoveSpeed / PickupRange: a fraction of the base value (0.1 = +10%).
    // The rest add to their multiplier (0.1 = +10%).
    public void AddStat(StatType stat, float amount)
    {
        switch (stat)
        {
            case StatType.MaxHp:
                maxHp += Mathf.RoundToInt(amount);
                Heal(Mathf.RoundToInt(amount));
                break;
            case StatType.MoveSpeed: moveSpeed += baseMoveSpeed * amount; break;
            case StatType.Damage: damageMultiplier += amount; break;
            case StatType.AttackSpeed: attackSpeedMultiplier += amount; break;
            case StatType.Area: areaMultiplier += amount; break;
            case StatType.PickupRange: pickupRange += basePickupRange * amount; break;
        }
    }

    // "+10 max HP", "+5% damage", ... (same units as AddStat).
    public static string StatText(StatType stat, float amount)
    {
        return stat == StatType.MaxHp ? $"+{amount:0} {StatLabels[(int)stat]}" : $"+{amount * 100f:0.#}% {StatLabels[(int)stat]}";
    }

    // Back to base stats and full HP, in the middle of the cave.
    public void ResetForNewRun()
    {
        maxHp = baseMaxHp;
        moveSpeed = baseMoveSpeed;
        pickupRange = basePickupRange;
        damageMultiplier = 1f;
        attackSpeedMultiplier = 1f;
        areaMultiplier = 1f;

        Hp = maxHp;
        invulnerableTimer = 0f;
        ResetLook();
        transform.position = arena.Bounds.center;
    }
}
