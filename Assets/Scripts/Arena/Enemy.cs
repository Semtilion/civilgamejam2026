using System.Collections.Generic;
using UnityEngine;

// One enemy in the arena. Moves in code, no physics: enemies overlap freely and are kept inside the cave walls.
// Hits the hero by touch (distance check).
public class Enemy : MonoBehaviour
{
    enum BossState { Chase, WindUp, Charge }

    public static readonly List<Enemy> All = new List<Enemy>(); // every enemy alive, used by weapons

    const float KnockbackTime = 0.15f;
    const float FlashTime = 0.08f;
    static readonly Color WarningColor = new Color(1f, 0.15f, 0.15f);
    static readonly Color IceColor = new Color(0.55f, 0.85f, 1f);

    [Header("Live stats (scaled from data for this depth)")]
    public EnemyData data;
    public int hp, maxHp, contactDamage, projectileDamage;

    Player player;
    ArenaManager arena;
    SpriteRenderer sprite;
    float stunTimer, fireTimer, flashTimer, stateTimer;
    float slowTimer, slowPace = 1f; // Frost Shard: moves and attacks at slowPace speed while slowTimer runs
    float delta;                    // this frame's time for moving and attacking (slowed while chilled)
    Vector2 knockbackVelocity;
    BossState bossState;
    Vector2 chargeDirection;
    bool aiming;          // shooter is flashing red, about to fire
    Vector2 aimDirection; // locked when the flash starts

    public bool IsDead => hp <= 0;

    public static Enemy Spawn(EnemyData data, Vector2 position, float statScale, Player player, ArenaManager arena)
    {
        GameObject go = new GameObject(data.displayName);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * data.radius * 2f; // placeholder sprite is 1 unit wide

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = data.sprite;
        renderer.color = data.color;
        renderer.sortingOrder = 1;

        Enemy enemy = go.AddComponent<Enemy>();
        enemy.Init(data, statScale, player, arena);
        return enemy;
    }

    void Init(EnemyData enemyData, float statScale, Player target, ArenaManager owner)
    {
        data = enemyData;
        player = target;
        arena = owner;
        sprite = GetComponent<SpriteRenderer>();

        maxHp = Mathf.RoundToInt(data.maxHp * statScale);
        hp = maxHp;
        contactDamage = Mathf.RoundToInt(data.contactDamage * statScale);
        projectileDamage = Mathf.RoundToInt(data.projectileDamage * statScale);
        fireTimer = Mathf.Max(0f, data.fireInterval - data.fireWarning);
        stateTimer = data.chargeInterval;
        All.Add(this);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!arena.IsPlaying) return; // frozen while a panel is open

        if (slowTimer > 0f) slowTimer -= Time.deltaTime;
        delta = Time.deltaTime * (slowTimer > 0f ? slowPace : 1f);
        Move();

        // Touch damage. The hero's invulnerability after a hit stops this from landing every frame.
        float touch = data.radius + player.radius;
        if (((Vector2)(player.transform.position - transform.position)).sqrMagnitude < touch * touch)
            player.TakeDamage(contactDamage);

        if (data.behavior == EnemyBehavior.Shooter) UpdateShooter();

        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
        bool warning = aiming || bossState == BossState.WindUp;
        Color normal = slowTimer > 0f ? Color.Lerp(data.color, IceColor, 0.6f) : data.color; // icy while chilled
        sprite.color = flashTimer > 0f ? Color.white : warning ? WarningColor : normal;
    }

    void Move()
    {
        Vector2 position = transform.position;
        Vector2 target = player.transform.position;

        if (stunTimer > 0f)
        {
            stunTimer -= delta; // sliding from knockback
            position += knockbackVelocity * delta;
        }
        else if (data.behavior == EnemyBehavior.Chaser)
        {
            position = Vector2.MoveTowards(position, target, data.moveSpeed * delta);
        }
        else if (data.behavior == EnemyBehavior.Boss)
        {
            position += BossVelocity(target - position) * delta;
        }
        // Shooters never move.

        Rect bounds = arena.Bounds;
        position.x = Mathf.Clamp(position.x, bounds.xMin + data.radius, bounds.xMax - data.radius);
        position.y = Mathf.Clamp(position.y, bounds.yMin + data.radius, bounds.yMax - data.radius);
        transform.position = position;
    }

    // Chase -> stop and glow red (wind-up) -> dash at where the hero was -> chase again.
    Vector2 BossVelocity(Vector2 toPlayer)
    {
        Vector2 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
        stateTimer -= delta;
        switch (bossState)
        {
            case BossState.Chase:
                if (stateTimer <= 0f)
                {
                    bossState = BossState.WindUp;
                    stateTimer = data.chargeWindup;
                }
                return direction * data.moveSpeed;

            case BossState.WindUp:
                if (stateTimer <= 0f)
                {
                    bossState = BossState.Charge;
                    stateTimer = data.chargeDuration;
                    chargeDirection = direction;
                }
                return Vector2.zero;

            default: // Charge
                if (stateTimer <= 0f)
                {
                    bossState = BossState.Chase;
                    stateTimer = data.chargeInterval;
                }
                return chargeDirection * data.chargeSpeed;
        }
    }

    // Reload -> (hero in range) flash red and lock the aim -> fire -> reload. Once the flash starts, the shot always comes.
    void UpdateShooter()
    {
        fireTimer -= delta;
        if (!aiming)
        {
            Vector2 toPlayer = player.transform.position - transform.position;
            if (fireTimer <= 0f && toPlayer.sqrMagnitude <= data.fireRange * data.fireRange)
            {
                aiming = true;
                aimDirection = toPlayer.normalized;
                fireTimer = data.fireWarning;
            }
        }
        else if (fireTimer <= 0f)
        {
            aiming = false;
            fireTimer = Mathf.Max(0f, data.fireInterval - data.fireWarning);
            Projectile.Fire(data.sprite, data.color, transform.position, aimDirection, data.projectileSpeed, projectileDamage, player);
        }
    }

    // `weaponHit`: a hit from one of the hero's weapons, which rolls the on-hit relics. Relic effects pass false,
    // so they never trigger more effects.
    public void TakeDamage(int amount, Vector2 hitFrom, float knockback, bool weaponHit = true)
    {
        if (IsDead) return; // already dying this frame

        hp -= amount;
        flashTimer = FlashTime;
        if (amount > 0) arena.damageNumbers.Show(transform.position, amount, weaponHit);
        if (weaponHit) arena.relics.OnWeaponHit(this, amount);
        if (IsDead)
        {
            arena.OnEnemyKilled(this);
            Destroy(gameObject);
            return;
        }

        // Only chasers get knocked back: bosses are too heavy, shooters never move.
        if (knockback > 0f && data.behavior == EnemyBehavior.Chaser)
        {
            knockbackVelocity = ((Vector2)transform.position - hitFrom).normalized * knockback;
            stunTimer = KnockbackTime;
        }
    }

    // Frost Shard: moves and attacks `amount` slower (0.5 = half speed) for `duration` seconds.
    public void Slow(float duration, float amount)
    {
        slowTimer = duration;
        slowPace = 1f - amount;
    }
}
