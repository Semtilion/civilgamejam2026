using System.Collections.Generic;
using UnityEngine;

// A thrown fire bomb: flies to where the enemy was, then burns there as a patch (flames over a scorch mark, slowly
// turning). Every tick, every enemy inside takes the damage (a weapon hit, so it can trigger on-hit relics).
public class FireBomb : MonoBehaviour
{
    public static readonly List<FireBomb> All = new List<FireBomb>();

    const float BombSize = 0.5f;
    const float PatchAlpha = 0.85f;
    const float PatchTurnSpeed = 40f; // degrees per second, so the flames look alive
    static readonly Color ScorchColor = new Color(0.12f, 0.06f, 0.04f, 0.7f);

    SpriteRenderer sprite;
    Sprite fireSprite, scorchSprite;
    Player player;
    Vector2 from, to;
    float flightTime, radius, burnTime, tickInterval;
    int damage;
    float age;       // since thrown, then since it started burning
    float tickTimer;
    bool burning;

    public static void Throw(Sprite sprite, Sprite fireSprite, Sprite scorchSprite, Color color, Vector2 from, Vector2 to,
        float flightTime, float radius, float burnTime, float tickInterval, int damage, Player player)
    {
        GameObject go = new GameObject("Fire Bomb");
        go.transform.position = from;
        go.transform.localScale = Vector3.one * BombSize;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 3;
        Fx.Unlit(renderer);

        FireBomb bomb = go.AddComponent<FireBomb>();
        bomb.sprite = renderer;
        bomb.fireSprite = fireSprite;
        bomb.scorchSprite = scorchSprite;
        bomb.player = player;
        bomb.from = from;
        bomb.to = to;
        bomb.flightTime = flightTime;
        bomb.radius = radius;
        bomb.burnTime = burnTime;
        bomb.tickInterval = tickInterval;
        bomb.damage = damage;
        All.Add(bomb);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!player.arena.IsPlaying) return;

        age += Time.deltaTime;
        if (!burning)
        {
            transform.position = Vector2.Lerp(from, to, age / flightTime);
            if (age >= flightTime) Burst();
            return;
        }

        if (age >= burnTime)
        {
            Destroy(gameObject);
            return;
        }
        transform.Rotate(0f, 0f, PatchTurnSpeed * Time.deltaTime);
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            Burn();
        }
    }

    // Lands: becomes flames on the floor over a scorch mark (the art is 1 unit wide). The first tick is right away.
    void Burst()
    {
        burning = true;
        age = 0f;
        tickTimer = 0f;
        transform.position = to;
        transform.localScale = Vector3.one * radius * 2f;
        if (fireSprite != null) sprite.sprite = fireSprite;
        Color color = sprite.color;
        color.a = PatchAlpha;
        sprite.color = color;
        sprite.sortingOrder = -1;
        if (scorchSprite != null)
        {
            SpriteRenderer scorch = new GameObject("Scorch").AddComponent<SpriteRenderer>();
            scorch.transform.SetParent(transform, false);
            scorch.sprite = scorchSprite;
            scorch.color = ScorchColor;
            scorch.sortingOrder = -2; // under the flames
        }
        Glow.Add(gameObject, new Color(1f, 0.55f, 0.2f), radius * 2.5f, 1.1f); // the fire lights up the cave
    }

    void Burn()
    {
        Vector2 center = transform.position;
        foreach (Enemy enemy in Enemy.All)
        {
            float reach = radius + enemy.data.radius;
            if (!enemy.IsDead && ((Vector2)enemy.transform.position - center).sqrMagnitude <= reach * reach)
                enemy.TakeDamage(damage, center, 0f);
        }
    }
}
