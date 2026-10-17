using System.Collections.Generic;
using UnityEngine;

// A thrown dagger: flies straight, damages the first enemy it touches and disappears (or vanishes at max range).
public class Dagger : MonoBehaviour
{
    public static readonly List<Dagger> All = new List<Dagger>();

    const float Radius = 0.15f;

    Vector2 velocity;
    int damage;
    float knockback;
    float lifeLeft;
    Player player;

    public static void Throw(Sprite sprite, Color color, Vector2 from, Vector2 direction, float speed, float range,
        int damage, float knockback, Player player)
    {
        GameObject go = new GameObject("Dagger");
        go.transform.position = from;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(0.6f, 0.15f, 1f); // placeholder: thin bar pointing where it flies

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 3;
        Fx.Unlit(renderer);

        Dagger dagger = go.AddComponent<Dagger>();
        dagger.velocity = direction * speed;
        dagger.damage = damage;
        dagger.knockback = knockback;
        dagger.lifeLeft = range / speed;
        dagger.player = player;
        All.Add(dagger);
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
            if (enemy.IsDead) continue;
            float reach = Radius + enemy.data.radius;
            if (((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude <= reach * reach)
            {
                enemy.TakeDamage(damage, (Vector2)transform.position - velocity, knockback); // pushed along the flight
                Destroy(gameObject);
                return;
            }
        }

        if (lifeLeft <= 0f) Destroy(gameObject);
    }
}
