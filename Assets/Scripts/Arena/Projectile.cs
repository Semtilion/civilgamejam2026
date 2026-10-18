using System.Collections.Generic;
using UnityEngine;

// Enemy shot: flies straight (its art turned to face that way: drawn pointing right), hurts the hero on touch and
// shatters (pieces in its shooter's color and a spark); after a few seconds it fades out with a sparkle.
public class Projectile : MonoBehaviour
{
    public static readonly List<Projectile> All = new List<Projectile>();

    public const float Radius = 0.2f; // hit size; the sprite is drawn Radius x 2 per unit of its size
    const float Lifetime = 5f;
    const float FadeTime = 0.3f; // its last moments

    SpriteRenderer sprite;
    Color particleColor;
    Vector2 velocity;
    int damage;
    float age;
    Player player;

    // `particleColor`: its pieces when it shatters (its shooter's).
    public static void Fire(Sprite sprite, Color color, Color particleColor, Vector2 from, Vector2 direction, float speed,
        int damage, Player player)
    {
        GameObject go = new GameObject("Projectile");
        go.transform.position = from;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        go.transform.localScale = Vector3.one * Radius * 2f;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 2;
        Fx.Unlit(renderer); // shots must stay visible in the dark

        Projectile projectile = go.AddComponent<Projectile>();
        projectile.sprite = renderer;
        projectile.particleColor = particleColor;
        projectile.velocity = direction * speed;
        projectile.damage = damage;
        projectile.player = player;
        All.Add(projectile);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!player.arena.IsPlaying) return;

        transform.position += (Vector3)(velocity * Time.deltaTime);
        age += Time.deltaTime;

        float touch = Radius + player.radius;
        DeathBursts bursts = player.arena.deathBursts;
        if (((Vector2)(player.transform.position - transform.position)).sqrMagnitude < touch * touch)
        {
            player.TakeDamage(damage);
            bursts.Burst(transform.position, particleColor, 0.25f, false); // shatters
            bursts.Spark(transform.position, Color.Lerp(particleColor, Color.white, 0.5f));
            Destroy(gameObject);
        }
        else if (age >= Lifetime)
        {
            bursts.Sparkle(transform.position, particleColor);
            Destroy(gameObject);
        }
        else if (age > Lifetime - FadeTime)
        {
            Color color = sprite.color;
            color.a = (Lifetime - age) / FadeTime;
            sprite.color = color;
        }
    }
}
