using System.Collections.Generic;
using UnityEngine;

// One enemy in the arena. Moves in code, no physics: enemies overlap freely and are kept inside the cave walls.
// Hits the hero by touch (distance check). Creatures have a shadow under their feet (EnemyData.shadowSize).
// Look: pops in with a dust puff, wobbles (squash and stretch) while moving, a Spitter's flower sways on its stem, then
// shakes, swells and glows red before a volley and pops as it fires, bosses walk in place and tremble while a red strip
// fades in over their dash, and everything squishes when hit. Only the look: hits use `radius`.
// A variant (EnemyVariant, rolled by ArenaManager) is bigger, tinted toward its color with an outline in it, and blue
// ones are tough, red ones drop fire patches as they go, purple ones leave a blast behind when they die.
public class Enemy : MonoBehaviour
{
    enum BossState { Chase, WindUp, Charge, Rest }

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
    const float WindUpSteps = 10f;  // a boss's wind-up: walk-cycle frames per second, walking in place
    const float ZoneAlpha = 0.3f;   // a boss's danger zone at the end of the wind-up
    const float SpawnTurn = 20f; // a shooter's flower (and its volley) is turned by up to this many degrees, random per spawn
    static readonly Color WarningColor = new Color(1f, 0.15f, 0.15f);
    static readonly Color IceColor = new Color(0.55f, 0.85f, 1f);
    static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.35f); // like the hero's

    [Header("Live stats (scaled from data for this depth)")]
    public EnemyData data;
    public int hp, maxHp, contactDamage, projectileDamage;
    public EnemyVariant variant;
    [Tooltip("Hit size: data.radius, bigger for a variant (weapons use it too).")]
    public float radius;

    Player player;
    ArenaManager arena;
    SpriteRenderer sprite;
    SpriteRenderer flower; // a Spitter's flower (null without the art; `sprite` is then the whole enemy, else its stem)
    HitFlash bodyFlash, flowerFlash; // white silhouettes while flashing (flowerFlash: null without a flower)
    Transform shadow; // under its feet (null without data.shadowSize)
    float stunTimer, fireTimer, flashTimer, stateTimer;
    float pinTimer; // the Spear's Impale: held in place
    float slowTimer, slowPace = 1f; // Frost Shard: moves and attacks at slowPace speed while slowTimer runs
    float delta;                    // this frame's time for moving and attacking (slowed while chilled)
    Vector2 knockbackVelocity;
    BossState bossState;
    Vector2 chargeDirection;
    float dashLeft;            // a boss's dash: units still to go (cut short by a wall)
    SpriteRenderer dangerZone; // a boss's dash path, shown through the wind-up
    bool aiming;          // shooter is warning (swelling, red), about to fire
    Vector3 baseScale;    // the size from radius
    float age, animTime, squishTimer, popTimer, wobblePhase;
    float turn; // shooters: SpawnTurn, picked at spawn
    float walked; // units walked (walk cycle)
    float speed;  // data.moveSpeed (slower for a tough variant)
    Color tint;   // data.color (toward the variant's color for a variant)
    Color particleColor; // data.particleColor, toward the variant's color like the tint
    float trailTimer;      // red variant: the next fire patch
    int hazardDamage;      // red variant: per fire tick; purple: the blast (scaled with depth)

    // Its death pieces, hit sparks and spawn dust are in this color (its art's, tinted like the enemy).
    public Color ParticleColor => particleColor;

    public bool IsDead => hp <= 0;

    public static Enemy Spawn(EnemyData data, Vector2 position, float statScale, Player player, ArenaManager arena,
        EnemyVariant variant = EnemyVariant.None)
    {
        GameObject go = new GameObject(data.displayName);
        go.transform.position = position;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = data.sprite;
        renderer.color = data.color;
        renderer.sortingOrder = 1;
        go.AddComponent<SpriteOutline>();
        go.AddComponent<HitFlash>();

        Enemy enemy = go.AddComponent<Enemy>();
        enemy.Init(data, statScale, player, arena, variant);
        return enemy;
    }

    void Init(EnemyData enemyData, float statScale, Player target, ArenaManager owner, EnemyVariant kind)
    {
        data = enemyData;
        player = target;
        arena = owner;
        variant = kind;
        VariantSettings settings = arena.variants;
        bool isVariant = variant != EnemyVariant.None;
        radius = data.radius * (isVariant ? settings.size : 1f);
        transform.localScale = Vector3.one * radius * 2f; // placeholder sprite is 1 unit wide
        speed = data.moveSpeed * (variant == EnemyVariant.Tough ? settings.toughSpeed : 1f);
        Color variantColor = settings.ColorOf(variant);
        Color variantTint = isVariant ? Color.Lerp(Color.white, variantColor, 0.55f) : Color.white;
        tint = data.color * variantTint;
        particleColor = (data.particleColor.a > 0f ? data.particleColor : data.color) * variantTint;
        particleColor.a = 1f;
        sprite = GetComponent<SpriteRenderer>();
        if (isVariant) GetComponent<SpriteOutline>().SetColor(variantColor);
        bodyFlash = GetComponent<HitFlash>();
        if (data.flower != null)
        {
            flower = new GameObject("Flower").AddComponent<SpriteRenderer>();
            flower.transform.SetParent(transform, false);
            flower.transform.localPosition = new Vector3(0f, 0f, FlowerDepth);
            flower.sprite = data.flower;
            flower.sortingOrder = sprite.sortingOrder;
            SpriteOutline flowerOutline = flower.gameObject.AddComponent<SpriteOutline>(); // drawn over the stem (between it and the flower)
            if (isVariant) flowerOutline.SetColor(variantColor);
            flowerFlash = flower.gameObject.AddComponent<HitFlash>();
        }

        maxHp = Mathf.RoundToInt(data.maxHp * statScale * (variant == EnemyVariant.Tough ? settings.toughHp : 1f));
        hp = maxHp;
        int hazardBase = variant == EnemyVariant.Burning ? settings.trailDamage : settings.blastDamage;
        hazardDamage = Mathf.Max(1, Mathf.RoundToInt(hazardBase * statScale));
        contactDamage = Mathf.RoundToInt(data.contactDamage * statScale);
        projectileDamage = Mathf.RoundToInt(data.projectileDamage * statScale);
        fireTimer = Mathf.Max(0f, data.fireInterval - data.fireWarning);
        stateTimer = data.chargeInterval;

        baseScale = transform.localScale;
        transform.localScale = Vector3.zero; // grows in (Animate)
        wobblePhase = Random.value * 10f;
        if (data.behavior == EnemyBehavior.Shooter) turn = Random.Range(-SpawnTurn, SpawnTurn);
        if (data.shadowSize != Vector2.zero)
        {
            SpriteRenderer shade = new GameObject("Shadow").AddComponent<SpriteRenderer>(); // a child: it moves, grows and squishes with it
            shade.transform.SetParent(transform, false);
            shade.transform.localPosition = data.shadowOffset;
            shade.transform.localScale = new Vector3(data.shadowSize.x, data.shadowSize.y, 1f); // the circle is 1 unit wide
            shade.sprite = arena.shadowSprite;
            shade.color = ShadowColor;
            shade.sortingOrder = -1; // on the floor, like the hero's
            shadow = shade.transform;
        }
        if (data.behavior == EnemyBehavior.Boss)
        {
            dangerZone = new GameObject("Danger Zone").AddComponent<SpriteRenderer>(); // not a child: the boss wobbles and flips
            dangerZone.sprite = arena.dangerZoneSprite;
            dangerZone.drawMode = SpriteDrawMode.Sliced; // stretched, the round tip keeps its shape
            dangerZone.sortingOrder = -2; // on the floor, under the shadows
            dangerZone.enabled = false;
            Fx.Unlit(dangerZone);
        }
        arena.deathBursts.Puff(transform.position, radius, particleColor);
        All.Add(this);
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (dangerZone != null) Destroy(dangerZone.gameObject);
    }

    void Update()
    {
        if (!arena.IsPlaying) return; // frozen while a panel is open

        if (slowTimer > 0f) slowTimer -= Time.deltaTime;
        delta = Time.deltaTime * (slowTimer > 0f ? slowPace : 1f);
        Move();

        // Touch damage. The hero's invulnerability after a hit stops this from landing every frame.
        float touch = radius + player.radius;
        if (((Vector2)(player.transform.position - transform.position)).sqrMagnitude < touch * touch)
            player.TakeDamage(contactDamage);

        if (data.behavior == EnemyBehavior.Shooter) UpdateShooter();
        if (variant == EnemyVariant.Burning) UpdateTrail();

        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
        Color normal = flashTimer > 0f ? Color.white : slowTimer > 0f ? Color.Lerp(tint, IceColor, 0.6f) : tint; // icy while chilled
        Color warned = aiming && flashTimer <= 0f ? WarningColor : normal;
        sprite.color = flower != null ? normal : warned; // with a flower, only the flower glows red
        if (flower != null) flower.color = warned;
        bodyFlash.On = flashTimer > 0f; // white silhouette (the white tint above only brightens placeholder sprites)
        if (flowerFlash != null) flowerFlash.On = flashTimer > 0f;
        Animate();
    }

    // The look only (scale and tilt of this object and its flower; the size used for hits is radius).
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
            angle = Random.Range(-6f, 6f); // trembles before the dash
        }
        else if (data.behavior == EnemyBehavior.Chaser || data.behavior == EnemyBehavior.Boss)
        {
            // Bouncy walk: squash and stretch plus a tilt, faster for faster enemies.
            float pace = WobbleSpeed + speed * 1.2f;
            float bounce = Mathf.Sin(animTime * pace + wobblePhase);
            sx += WobbleSquash * bounce;
            sy -= WobbleSquash * bounce;
            angle = WobbleTilt * Mathf.Sin(animTime * pace * 0.5f + wobblePhase);
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
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
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
            Vector2 next = Vector2.MoveTowards(position, target, speed * delta);
            Walk(next - position);
            position = next;
        }
        else if (data.behavior == EnemyBehavior.Boss)
        {
            position += BossStep(target - position);
        }
        // Shooters never move.

        Rect bounds = arena.Bounds;
        position.x = Mathf.Clamp(position.x, bounds.xMin + radius, bounds.xMax - radius);
        position.y = Mathf.Clamp(position.y, bounds.yMin + radius, bounds.yMax - radius);
        transform.position = position;
    }

    // Enemies with walk frames (Brute, Crawler, the boss): the next frame every StepLength units walked, mirrored to
    // face where it goes (its shadow too). Not while knocked back or pinned (only called for its own steps).
    void Walk(Vector2 moved)
    {
        if (data.walkFrames == null || data.walkFrames.Length == 0) return;
        if (Mathf.Abs(moved.x) > 0.0001f)
        {
            sprite.flipX = data.artFacesLeft ? moved.x > 0f : moved.x < 0f;
            if (shadow != null) shadow.localPosition = new Vector2(sprite.flipX ? -data.shadowOffset.x : data.shadowOffset.x, data.shadowOffset.y);
        }
        walked += moved.magnitude;
        sprite.sprite = data.walkFrames[(int)(walked / StepLength) % data.walkFrames.Length];
    }

    // Chase -> wind-up: locks onto where the hero is and walks in place (trembling) while a red strip fades in over its
    // path -> dashes along it -> stands still a moment (rest) -> chase again. Returns this frame's move.
    Vector2 BossStep(Vector2 toPlayer)
    {
        Vector2 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;
        stateTimer -= delta;
        switch (bossState)
        {
            case BossState.Chase:
                if (stateTimer <= 0f)
                {
                    StartWindUp(direction); // stops right here, where the strip starts
                    return Vector2.zero;
                }
                Vector2 step = direction * speed * delta;
                Walk(step);
                return step;

            case BossState.WindUp:
                Walk(chargeDirection * WindUpSteps * StepLength * delta); // quick steps on the spot
                float shown = Mathf.Clamp01(1f - stateTimer / Mathf.Max(data.chargeWindup, 0.01f)); // 0 -> 1
                dangerZone.color = new Color(WarningColor.r, WarningColor.g, WarningColor.b, ZoneAlpha * shown);
                if (stateTimer <= 0f)
                {
                    bossState = BossState.Charge;
                    dangerZone.enabled = false;
                    Sfx.Play(Sound.BossDash);
                }
                return Vector2.zero;

            case BossState.Charge:
                float move = Mathf.Min(data.chargeSpeed * delta, dashLeft);
                dashLeft -= move;
                if (dashLeft <= 0f)
                {
                    bossState = BossState.Rest;
                    stateTimer = data.chargeRest;
                }
                return chargeDirection * move;

            default: // Rest
                if (stateTimer <= 0f)
                {
                    bossState = BossState.Chase;
                    stateTimer = data.chargeInterval;
                }
                return Vector2.zero;
        }
    }

    // Locks the dash onto `direction` (at the hero) and lays the red strip over its path: as wide as the boss, from its
    // middle (flat) to the front of its body where the dash will stop (round; chargeSpeed x chargeDuration, or sooner
    // at a wall).
    void StartWindUp(Vector2 direction)
    {
        bossState = BossState.WindUp;
        stateTimer = data.chargeWindup;
        chargeDirection = direction != Vector2.zero ? direction : Vector2.right; // (standing right on the hero)
        dashLeft = Mathf.Min(data.chargeSpeed * data.chargeDuration, DistanceToWall(chargeDirection));
        float length = dashLeft + radius;
        dangerZone.transform.position = (Vector2)transform.position + chargeDirection * (length / 2f);
        dangerZone.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(chargeDirection.y, chargeDirection.x) * Mathf.Rad2Deg);
        float width = radius * 2f;
        dangerZone.transform.localScale = Vector3.one * width;
        dangerZone.size = new Vector2(length / width, 1f); // sprite units: 1 = the width; the round tip is half of it
        dangerZone.color = Color.clear;
        dangerZone.enabled = true;
    }

    // How far it can go from where it is along `direction` (normalized) before its body meets the cave wall.
    float DistanceToWall(Vector2 direction)
    {
        Rect bounds = arena.Bounds;
        Vector2 from = transform.position;
        float distance = float.MaxValue;
        if (direction.x > 0.0001f) distance = Mathf.Min(distance, (bounds.xMax - radius - from.x) / direction.x);
        if (direction.x < -0.0001f) distance = Mathf.Min(distance, (bounds.xMin + radius - from.x) / direction.x);
        if (direction.y > 0.0001f) distance = Mathf.Min(distance, (bounds.yMax - radius - from.y) / direction.y);
        if (direction.y < -0.0001f) distance = Mathf.Min(distance, (bounds.yMin + radius - from.y) / direction.y);
        return Mathf.Max(0f, distance);
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
                Vector2 from = (Vector2)transform.position + direction * radius;
                Projectile.Fire(shot, data.color, particleColor, from, direction, data.projectileSpeed, projectileDamage, player);
            }
            Sfx.Play(Sound.SpitterShot);
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
            Vector2 at = toHit.sqrMagnitude > 0.0001f ? (Vector2)transform.position + toHit.normalized * radius * 0.7f : (Vector2)transform.position;
            arena.deathBursts.Spark(at, particleColor);
            if (data.behavior != EnemyBehavior.Crate) Sfx.Play(Sound.Hit); // (crates and clusters just break)
        }
        if (weaponHit) arena.relics.OnWeaponHit(this, amount);
        if (IsDead)
        {
            if (variant == EnemyVariant.Volatile && arena.IsPlaying)
            {
                VariantSettings settings = arena.variants;
                Hazard.Blast(arena.blastCoreSprite, arena.blastRingSprite, settings.volatileColor, transform.position,
                    settings.blastRadius, settings.blastFuse, hazardDamage, arena);
            }
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

    // Red variant: a fire patch where it is every trailInterval seconds (slower while chilled).
    void UpdateTrail()
    {
        trailTimer -= delta;
        if (trailTimer > 0f) return;
        VariantSettings settings = arena.variants;
        trailTimer = settings.trailInterval;
        Hazard.Fire(arena.fireTrailSprite, settings.burningColor, transform.position, settings.trailRadius,
            settings.trailLife, settings.trailTick, hazardDamage, arena);
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
