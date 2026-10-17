using System.Collections.Generic;
using UnityEngine;

// A thrown axe: spins out in a straight line for `distance`, then flies back to the hero and is caught.
// Hits every enemy it touches, each once on the way out and once on the way back.
public class Axe : MonoBehaviour
{
    public static readonly List<Axe> All = new List<Axe>();

    const float SpinSpeed = 540f; // degrees per second
    const float CatchDistance = 0.4f;
    const float MaxLifetime = 6f;  // in case the hero outruns it

    Player player;
    Vector2 velocity;
    float speed, outLeft, radius, knockback, age;
    int damage;
    bool returning;
    readonly HashSet<Enemy> hit = new HashSet<Enemy>(); // cleared when it turns around

    public static void Throw(Sprite sprite, Color color, Vector2 from, Vector2 direction, float speed, float distance,
        float radius, int damage, float knockback, Player player)
    {
        GameObject go = new GameObject("Axe");
        go.transform.position = from;
        go.transform.localScale = Vector3.one * radius * 2.4f; // the art (1 unit tall), a bit bigger than what it hits

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 3;
        Fx.Unlit(renderer);

        Axe axe = go.AddComponent<Axe>();
        axe.player = player;
        axe.velocity = direction * speed;
        axe.speed = speed;
        axe.outLeft = distance;
        axe.radius = radius;
        axe.damage = damage;
        axe.knockback = knockback;
        All.Add(axe);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!player.arena.IsPlaying) return;

        age += Time.deltaTime;
        transform.Rotate(0f, 0f, -SpinSpeed * Time.deltaTime);

        if (!returning)
        {
            outLeft -= speed * Time.deltaTime;
            if (outLeft <= 0f)
            {
                returning = true;
                hit.Clear(); // everything can be hit again on the way back
            }
        }
        else
        {
            Vector2 toHero = player.transform.position - transform.position;
            if (toHero.magnitude <= CatchDistance || age >= MaxLifetime)
            {
                Destroy(gameObject);
                return;
            }
            velocity = toHero.normalized * speed;
        }
        transform.position += (Vector3)(velocity * Time.deltaTime);

        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy.IsDead || hit.Contains(enemy)) continue;
            float reach = radius + enemy.radius;
            if (((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude <= reach * reach)
            {
                hit.Add(enemy);
                enemy.TakeDamage(damage, (Vector2)transform.position - velocity, knockback); // pushed along the flight
            }
        }
    }
}
