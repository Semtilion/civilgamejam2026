using System.Collections.Generic;
using UnityEngine;

// One enemy in the arena. Moves in code, no physics: enemies overlap freely and are kept inside the cave walls.
// Hits the hero by touch (distance check).
// Look: pops in with a dust puff, wobbles (squash and stretch) while moving, a Spitter's flower sways on its stem, then
// shakes, swells and glows red before a volley and pops as it fires, bosses tremble while winding up, and everything
// squishes when hit. Only the look: hits use data.radius.
public class Enemy : MonoBehaviour
{
    enum BossState { Chase, WindUp, Charge }

    public static readonly List<Enemy> All = new List<Enemy>(); // every enemy alive, used by weapons

    const float KnockbackTime = 0.15f;
    const float FlashTime = 0.08f;
    const float SpawnTime = 0.3f;  // grows in
    const float SquishTime = 0.15f; // flattens when hit, then springs back
    // The walk wobble: how much it squashes/stretches (0.05 = 5%), how far it tilts, and how fast (faster enemies bounce faster).
    const float WobbleSquash = 0.05f;
    const float WobbleTilt = 2.5f; // degrees
    const float WobbleSpeed = 4f;  // + 1.2 x move speed, radians per second
    // Spitters' flowers (or the placeholder): breath, sway, the shake at the end of the warning (degrees, and how far it
    // jitters: 1 = the enemy's width), the bounce after a volley, and the flower sits just in front of the stem.
    const float BreathAmount = 0.04f;
    const float SwayAngle = 4f;
    const float ShakeAngle = 7f;
    const float ShakeOffset = 0.05f;
    const float PopTime = 0.2f;
    const float FlowerDepth = -0.01f;
    const float StepLength = 0.45f; // walk cycle: the next frame every this many units walked
    const float TurnSpeed = 400f;   // turnsToFace: degrees per second
    const float SpawnTurn = 20f; // a shooter's flower (and its volley) is turned by up to this many degrees, random per spawn
    static readonly Color WarningColor = new Color(1f, 0.15f, 0.15f);
    static readonly Color IceColor = new Color(0.55f, 0.85f, 1f);

    [Header("Live stats (scaled from data for this depth)")]
    public EnemyData data;
    public int hp, maxHp, contactDamage, projectileDamage;

    Player player;
    ArenaManager arena;
    SpriteRenderer sprite;
    SpriteRenderer flower; // a Spitter's flower (null without the art; `sprite` is then the whole enemy, else its stem)
    HitFlash bodyFlash, flowerFlash; // white silhouettes while flashing (flowerFlash: null without a flower)
    float stunTimer, fireTimer, flashTimer, stateTimer;
    float pinTimer; // the Spear's Impale: held in place
    float slowTimer, slowPace = 1f; // Frost Shard: moves and attacks at slowPace speed while slowTimer runs
    float delta;                    // this frame's time for moving and attacking (slowed while chilled)
    Vector2 knockbackVelocity;
    BossState bossState;
    Vector2 chargeDirection;
    bool aiming;          // shooter is warning (swelling, red), about to fire
    Vector3 baseScale;    // the size from data.radius
    float age, animTime, squishTimer, popTimer, wobblePhase;
    float turn; // shooters: SpawnTurn, picked at spawn
    float walked; // units walked (walk cycle)
    float facing; // turnsToFace: the tilt toward where it walks (degrees, -90..90), added to the wobble

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
        go.AddComponent<SpriteOutline>();
        go.AddComponent<HitFlash>();

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
        bodyFlash = GetComponent<HitFlash>();
        if (data.flower != null)
        {
            flower = new GameObject("Flower").AddComponent<SpriteRenderer>();
            flower.transform.SetParent(transform, false);
            flower.transform.localPosition = new Vector3(0f, 0f, FlowerDepth);
            flower.sprite = data.flower;
            flower.sortingOrder = sprite.sortingOrder;
            flower.gameObject.AddComponent<SpriteOutline>(); // drawn over the stem (between it and the flower)
            flowerFlash = flower.gameObject.AddComponent<HitFlash>();
        }

        maxHp = Mathf.RoundToInt(data.maxHp * statScale);
        hp = maxHp;
        contactDamage = Mathf.RoundToInt(data.contactDamage * statScale);
        projectileDamage = Mathf.RoundToInt(data.projectileDamage * statScale);
        fireTimer = Mathf.Max(0f, data.fireInterval - data.fireWarning);
        stateTimer = data.chargeInterval;

        baseScale = transform.localScale;
        transform.localScale = Vector3.zero; // grows in (Animate)
        wobblePhase = Random.value * 10f;
        if (data.behavior == EnemyBehavior.Shooter) turn = Random.Range(-SpawnTurn, SpawnTurn);
        arena.deathBursts.Puff(transform.position, data.radius);
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
        Color normal = flashTimer > 0f ? Color.white : slowTimer > 0f ? Color.Lerp(data.color, IceColor, 0.6f) : data.color; // icy while chilled
        Color warned = warning && flashTimer <= 0f ? WarningColor : normal;
        sprite.color = flower != null ? normal : warned; // with a flower, only the flower glows red
        if (flower != null) flower.color = warned;
        bodyFlash.On = flashTimer > 0f; // white silhouette (the white tint above only brightens placeholder sprites)
        if (flowerFlash != null) flowerFlash.On = flashTimer > 0f;
        Animate();
    }

    // The look only (scale and tilt of this object and its flower; the size used for hits is data.radius).
    void Animate()
    {
        age += Time.deltaTime;
        animTime += delta; // slower while chilled
        if (squishTimer > 0f) squishTimer -= Time.deltaTime;
        if (popTimer > 0f) popTimer -= Time.deltaTime;

        float sx = 1f, sy = 1f, angle = 0f;
        bool windingUp = data.behavior == EnemyBehavior.Boss && bossState == BossState.WindUp;
        if (windingUp)
        {
            angle = Random.Range(-6f, 6f); // trembles, swollen, before the charge
            sx = sy = 1.1f;
        }
        else if (data.behavior == EnemyBehavior.Chaser || data.behavior == EnemyBehavior.Boss)
        {
            // Bouncy walk: squash and stretch plus a tilt, faster for faster enemies.
            float speed = WobbleSpeed + data.moveSpeed * 1.2f;
            float bounce = Mathf.Sin(animTime * speed + wobblePhase);
            sx += WobbleSquash * bounce;
            sy -= WobbleSquash * bounce;
            angle = WobbleTilt * Mathf.Sin(animTime * speed * 0.5f + wobblePhase);
        }
        else if (data.behavior == EnemyBehavior.Shooter)
        {
            // Breathes and sways; through the warning it swells and shakes harder and harder; after a volley it pops
            // and springs back.
            float breath = BreathAmount * Mathf.Sin(animTime * 3f + wobblePhase);
            float swell = 1f, tilt = SwayAngle * Mathf.Sin(animTime * 1.5f + wobblePhase);
            Vector2 shake = Vector2.zero;
            if (aiming)
            {
                float charge = Mathf.Clamp01(1f - fireTimer / Mathf.Max(data.fireWarning, 0.01f)); // 0 -> 1 through the warning
                swell = 1f + 0.15f * charge;
                tilt += Random.Range(-ShakeAngle, ShakeAngle) * charge;
                shake = Random.insideUnitCircle * ShakeOffset * charge;
            }
            if (popTimer > 0f) swell += 0.25f * popTimer / PopTime;
            float headX = swell * (1f - breath * 0.5f), headY = swell * (1f + breath);
            if (flower != null)
            {
                flower.transform.localScale = new Vector3(headX, headY, 1f);
                flower.transform.localRotation = Quaternion.Euler(0f, 0f, turn + tilt);
                flower.transform.localPosition = new Vector3(shake.x, shake.y, FlowerDepth);
            }
            else // the placeholder moves as a whole (but doesn't jitter: its position is where it is)
            {
                sx *= headX;
                sy *= headY;
                angle = tilt;
            }
        }

        if (squishTimer > 0f)
        {
            float squish = squishTimer / SquishTime;
            sx += 0.35f * squish;
            sy -= 0.3f * squish;
        }

        float grow = Fx.EaseOutBack(age / SpawnTime);
        transform.localScale = new Vector3(baseScale.x * sx * grow, baseScale.y * sy * grow, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, angle + facing);
    }

    void Move()
    {
        Vector2 position = transform.position;
        Vector2 target = player.transform.position;

        if (pinTimer > 0f)
        {
            pinTimer -= delta; // impaled: stays put, and knockback from hits meanwhile is dropped
            stunTimer = 0f;
        }
        else if (stunTimer > 0f)
        {
            stunTimer -= delta; // sliding from knockback
            position += knockbackVelocity * delta;
        }
        else if (data.behavior == EnemyBehavior.Chaser)
        {
            Vector2 next = Vector2.MoveTowards(position, target, data.moveSpeed * delta);
            Walk(next - position);
            position = next;
        }
        else if (data.behavior == EnemyBehavior.Boss)
        {
            Vector2 step = BossVelocity(target - position) * delta;
            Walk(step);
            position += step;
        }
        // Shooters never move.

        Rect bounds = arena.Bounds;
        position.x = Mathf.Clamp(position.x, bounds.xMin + data.radius, bounds.xMax - data.radius);
        position.y = Mathf.Clamp(position.y, bounds.yMin + data.radius, bounds.yMax - data.radius);
        transform.position = position;
    }

    // Enemies with walk frames (Brute, Crawler): the next frame every StepLength units walked, mirrored to face where
    // it goes; turnsToFace ones also tilt their head that way (up to straight up or down). Not while knocked back or
    // pinned (only called for its own steps).
    void Walk(Vector2 moved)
    {
        if (data.walkFrames == null || data.walkFrames.Length == 0) return;
        if (Mathf.Abs(moved.x) > 0.0001f)
        {
            bool flip = data.artFacesLeft ? moved.x > 0f : moved.x < 0f;
            if (flip != sprite.flipX) facing = -facing; // mirrored: the same tilt now reads the other way
            sprite.flipX = flip;
        }
        if (data.turnsToFace && moved.sqrMagnitude > 0.00000001f)
        {
            // Head up = turning toward the side it faces: counter-clockwise facing right, clockwise facing left.
            float elevation = Mathf.Atan2(moved.y, Mathf.Abs(moved.x)) * Mathf.Rad2Deg;
            bool facesRight = data.artFacesLeft == sprite.flipX;
            facing = Mathf.MoveTowardsAngle(facing, facesRight ? elevation : -elevation, TurnSpeed * Time.deltaTime);
        }
        walked += moved.magnitude;
        sprite.sprite = data.walkFrames[(int)(walked / StepLength) % data.walkFrames.Length];
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

    // Reload -> (hero in range) warning -> a volley: one shot per data.shotAngles (+ this Spitter's turn), all at once,
    // starting at the edge of its hit circle (by the flower's tips) -> reload. Once the warning starts, the volley
    // always comes.
    void UpdateShooter()
    {
        fireTimer -= delta;
        if (!aiming)
        {
            Vector2 toPlayer = player.transform.position - transform.position;
            if (fireTimer <= 0f && toPlayer.sqrMagnitude <= data.fireRange * data.fireRange)
            {
                aiming = true;
                fireTimer = data.fireWarning;
            }
        }
        else if (fireTimer <= 0f)
        {
            aiming = false;
            popTimer = PopTime;
            fireTimer = Mathf.Max(0f, data.fireInterval - data.fireWarning);
            Sprite shot = data.projectileSprite != null ? data.projectileSprite : data.sprite;
            foreach (float tip in data.shotAngles)
            {
                float degrees = tip + turn; // the flower's tips, turned with it
                Vector2 direction = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                Vector2 from = (Vector2)transform.position + direction * data.radius;
                Projectile.Fire(shot, data.color, from, direction, data.projectileSpeed, projectileDamage, player);
            }
        }
    }

    // `weaponHit`: a hit from one of the hero's weapons, which rolls the on-hit relics. Relic effects pass false,
    // so they never trigger more effects.
    public void TakeDamage(int amount, Vector2 hitFrom, float knockback, bool weaponHit = true)
    {
        if (IsDead) return; // already dying this frame

        hp -= amount;
        flashTimer = FlashTime;
        squishTimer = SquishTime;
        if (amount > 0)
        {
            arena.damageNumbers.Show(transform.position, amount, weaponHit);
            // The impact spark, on the side the hit came from
            Vector2 toHit = hitFrom - (Vector2)transform.position;
            Vector2 at = toHit.sqrMagnitude > 0.0001f ? (Vector2)transform.position + toHit.normalized * data.radius * 0.7f : (Vector2)transform.position;
            arena.deathBursts.Spark(at);
        }
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

    // The Spear's Impale: a walking enemy (not a boss) can't move at all for `duration` seconds, not even when knocked back.
    public void Pin(float duration)
    {
        if (IsDead || data.behavior != EnemyBehavior.Chaser) return;
        pinTimer = Mathf.Max(pinTimer, duration);
    }
}
