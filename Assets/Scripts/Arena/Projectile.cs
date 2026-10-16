using System.Collections.Generic;
using UnityEngine;

// Enemy shot: flies straight, hurts the hero on touch, disappears after a few seconds.
public class Projectile : MonoBehaviour
{
    public static readonly List<Projectile> All = new List<Projectile>();

    const float Radius = 0.2f;
    const float Lifetime = 5f;

    Vector2 velocity;
    int damage;
    float age;
    Player player;

    public static void Fire(Sprite sprite, Color color, Vector2 from, Vector2 direction, float speed, int damage, Player player)
    {
        GameObject go = new GameObject("Projectile");
        go.transform.position = from;
        go.transform.localScale = Vector3.one * Radius * 2f;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 2;

        Projectile projectile = go.AddComponent<Projectile>();
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
        if (((Vector2)(player.transform.position - transform.position)).sqrMagnitude < touch * touch)
        {
            player.TakeDamage(damage);
            Destroy(gameObject);
        }
        else if (age >= Lifetime)
        {
            Destroy(gameObject);
        }
    }
}
