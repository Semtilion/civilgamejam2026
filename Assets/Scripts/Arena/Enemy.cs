using System.Collections.Generic;
using UnityEngine;

// One enemy in the arena. Moves with a Rigidbody2D so enemies push each other apart and stop at the walls.
// Hits the hero by touch (distance check); the hero has no collider, so crowds can't body-block.
public class Enemy : MonoBehaviour
{
    enum BossState { Chase, WindUp, Charge }

    public static readonly List<Enemy> All = new List<Enemy>(); // every enemy alive, used by weapons

    const float KnockbackTime = 0.15f;
    const float FlashTime = 0.08f;
    static readonly Color WindUpColor = new Color(1f, 0.15f, 0.15f);

    [Header("Live stats (scaled from data for this depth)")]
    public EnemyData data;
    public int hp, maxHp, contactDamage, projectileDamage;

    Player player;
    ArenaManager arena;
    Rigidbody2D body;
    SpriteRenderer sprite;
    float stunTimer, fireTimer, flashTimer, stateTimer;
    BossState bossState;
    Vector2 chargeDirection;

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

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.mass = data.radius * data.radius * 4f; // big enemies shove small ones
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        go.AddComponent<CircleCollider2D>().radius = 0.5f; // local units, scaled to data.radius

        Enemy enemy = go.AddComponent<Enemy>();
        enemy.Init(data, statScale, player, arena);
        return enemy;
    }

    void Init(EnemyData enemyData, float statScale, Player target, ArenaManager owner)
    {
        data = enemyData;
        player = target;
        arena = owner;
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();

        maxHp = Mathf.RoundToInt(data.maxHp * statScale);
        hp = maxHp;
        contactDamage = Mathf.RoundToInt(data.contactDamage * statScale);
        projectileDamage = Mathf.RoundToInt(data.projectileDamage * statScale);
        fireTimer = data.fireInterval;
        stateTimer = data.chargeInterval;
        All.Add(this);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        // Touch damage. The hero's invulnerability after a hit stops this from landing every frame.
        float touch = data.radius + player.radius;
        if (((Vector2)(player.transform.position - transform.position)).sqrMagnitude < touch * touch)
            player.TakeDamage(contactDamage);

        if (data.behavior == EnemyBehavior.Shooter)
        {
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f)
            {
                fireTimer = data.fireInterval;
                Vector2 direction = ((Vector2)(player.transform.position - transform.position)).normalized;
                Projectile.Fire(data.sprite, data.color, transform.position, direction, data.projectileSpeed, projectileDamage, player);
            }
        }

        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
        sprite.color = flashTimer > 0f ? Color.white : bossState == BossState.WindUp ? WindUpColor : data.color;
    }

    void FixedUpdate()
    {
        if (stunTimer > 0f)
        {
            stunTimer -= Time.fixedDeltaTime; // sliding from knockback
            return;
        }

        Vector2 toPlayer = (Vector2)player.transform.position - body.position;
        float distance = toPlayer.magnitude;
        Vector2 direction = distance > 0.001f ? toPlayer / distance : Vector2.zero;

        switch (data.behavior)
        {
            case EnemyBehavior.Chaser:
                body.linearVelocity = direction * data.moveSpeed;
                break;

            case EnemyBehavior.Shooter:
                // Walk in until preferred distance, back off when the hero gets too close.
                float step = distance > data.preferredDistance + 0.5f ? 1f : distance < data.preferredDistance - 0.5f ? -1f : 0f;
                body.linearVelocity = direction * (data.moveSpeed * step);
                break;

            case EnemyBehavior.Boss:
                UpdateBoss(direction);
                break;
        }
    }

    // Chase -> stop and glow red (wind-up) -> dash at where the hero was -> chase again.
    void UpdateBoss(Vector2 direction)
    {
        stateTimer -= Time.fixedDeltaTime;
        switch (bossState)
        {
            case BossState.Chase:
                body.linearVelocity = direction * data.moveSpeed;
                if (stateTimer <= 0f)
                {
                    bossState = BossState.WindUp;
                    stateTimer = data.chargeWindup;
                }
                break;

            case BossState.WindUp:
                body.linearVelocity = Vector2.zero;
                if (stateTimer <= 0f)
                {
                    bossState = BossState.Charge;
                    stateTimer = data.chargeDuration;
                    chargeDirection = direction;
                }
                break;

            case BossState.Charge:
                body.linearVelocity = chargeDirection * data.chargeSpeed;
                if (stateTimer <= 0f)
                {
                    bossState = BossState.Chase;
                    stateTimer = data.chargeInterval;
                }
                break;
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

        if (knockback > 0f && data.behavior != EnemyBehavior.Boss)
        {
            body.linearVelocity = ((Vector2)transform.position - hitFrom).normalized * knockback;
            stunTimer = KnockbackTime;
        }
    }
}
