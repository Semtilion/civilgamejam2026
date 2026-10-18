using System.Collections.Generic;
using UnityEngine;

// A thrown dagger: flies straight, damages the first enemy it touches and disappears (or vanishes at max range).
// Ricochet (the evolved Dagger): after each hit, `bounceChance` to fly on to the nearest other enemy within
// `bounceRange`, up to `bounces` times.
public class Dagger : MonoBehaviour
{
    public static readonly List<Dagger> All = new List<Dagger>();

    const float Radius = 0.15f;

    Vector2 velocity;
    int damage;
    float knockback;
    float lifeLeft;
    Player player;
    int bouncesLeft;
    float bounceChance, bounceRange;
    Enemy lastHit; // not hit again right after bouncing off it

    public static void Throw(Sprite sprite, Color color, Vector2 from, Vector2 direction, float speed, float range,
        int damage, float knockback, Player player, int bounces = 0, float bounceChance = 0f, float bounceRange = 0f)
    {
        GameObject go = new GameObject("Dagger");
        go.transform.position = from;
        go.transform.localScale = Vector3.one; // the blade streak art: 1 unit long, pointing where it flies

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 3;
        Fx.Unlit(renderer);

        Dagger dagger = go.AddComponent<Dagger>();
        dagger.Fly(direction * speed, range / speed);
        dagger.damage = damage;
        dagger.knockback = knockback;
        dagger.player = player;
        dagger.bouncesLeft = bounces;
        dagger.bounceChance = bounceChance;
        dagger.bounceRange = bounceRange;
        All.Add(dagger);
    }

    // Sets off in a new direction (turned to face it) for `seconds`.
    void Fly(Vector2 newVelocity, float seconds)
    {
        velocity = newVelocity;
        lifeLeft = seconds;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!player.arena.IsPlaying) return;

        transform.position += (Vector3)(velocity * Time.deltaTime);
        lifeLeft -= Time.deltaTime;

        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy.IsDead || enemy == lastHit) continue;
            float reach = Radius + enemy.radius;
            if (((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude <= reach * reach)
            {
                enemy.TakeDamage(damage, (Vector2)transform.position - velocity, knockback); // pushed along the flight
                if (bouncesLeft <= 0 || Random.value >= bounceChance || !BounceFrom(enemy)) Destroy(gameObject);
                else Sfx.Play(Sound.Ricochet);
                return;
            }
        }

        if (lifeLeft <= 0f) Destroy(gameObject);
    }

    // Ricochet: turns toward the nearest other living enemy within bounceRange. False if there's none.
    bool BounceFrom(Enemy hit)
    {
        Enemy next = null;
        float best = bounceRange * bounceRange;
        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy.IsDead || enemy == hit) continue;
            float distance = ((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude;
            if (distance <= best)
            {
                best = distance;
                next = enemy;
            }
        }
        if (next == null) return false;

        float speed = velocity.magnitude;
        Vector2 offset = next.transform.position - transform.position;
        Vector2 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : velocity / speed;
        Fly(direction * speed, (bounceRange + 1f) / speed); // enough to reach it
        bouncesLeft--;
        lastHit = hit;
        return true;
    }
}
