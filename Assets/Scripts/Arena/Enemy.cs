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

    [Header("Live stats (scaled from data for this depth)")]
    public EnemyData data;
    public int hp, maxHp, contactDamage, projectileDamage;

    Player player;
    ArenaManager arena;
    SpriteRenderer sprite;
    float stunTimer, fireTimer, flashTimer, stateTimer;
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

        Move();

        // Touch damage. The hero's invulnerability after a hit stops this from landing every frame.
        float touch = data.radius + player.radius;
        if (((Vector2)(player.transform.position - transform.position)).sqrMagnitude < touch * touch)
            player.TakeDamage(contactDamage);

        if (data.behavior == EnemyBehavior.Shooter) UpdateShooter();

        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
        bool warning = aiming || bossState == BossState.WindUp;
        sprite.color = flashTimer > 0f ? Color.white : warning ? WarningColor : data.color;
    }

    void Move()
    {
        Vector2 position = transform.position;
        Vector2 target = player.transform.position;

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime; // sliding from knockback
            position += knockbackVelocity * Time.deltaTime;
        }
        else if (data.behavior == EnemyBehavior.Chaser)
        {
            position = Vector2.MoveTowards(position, target, data.moveSpeed * Time.deltaTime);
        }
        else if (data.behavior == EnemyBehavior.Boss)
        {
            position += BossVelocity(target - position) * Time.deltaTime;
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
        stateTimer -= Time.deltaTime;
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
        fireTimer -= Time.deltaTime;
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

    public void TakeDamage(int amount, Vector2 hitFrom, float knockback)
    {
        if (IsDead) return; // already dying this frame

        hp -= amount;
        flashTimer = FlashTime;
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
}
