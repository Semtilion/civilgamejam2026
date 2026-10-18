using System.Collections.Generic;
using UnityEngine;

// A lobbed fire bomb: arcs to where the enemy was (spinning, its shadow sliding along the ground under it), catches
// fire at the top of the arc (flames behind it and a warm light), breaks on landing (shards and sparks), then burns
// there as a patch (flames over a scorch mark, gently wobbling). Every tick, every enemy inside takes the damage (a
// weapon hit, so it can trigger on-hit relics).
// The object itself moves along the ground; the bomb rides above it on the arc.
public class FireBomb : MonoBehaviour
{
    public static readonly List<FireBomb> All = new List<FireBomb>();

    const float BombSize = 0.7f;      // world units (the art's ball is about 3/4 of it)
    const float SpinSpeed = 360f;     // degrees per second in flight
    const float LobHeight = 0.6f;     // the top of the arc: this + LobPerUnit x the throw's length
    const float LobPerUnit = 0.25f;
    const float FlameSize = 0.9f;     // the flames on the bomb in flight (world units), a little above it
    const float FlameRise = 0.15f;
    const float ShadowWidth = 0.5f;   // world units (60% of it at the top of the arc)
    const float PatchAlpha = 0.85f;
    const float PatchGrowTime = 0.2f; // the patch pops up after the break
    static readonly Color ScorchColor = new Color(0.12f, 0.06f, 0.04f, 0.7f);
    static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.35f); // like the hero's

    FireBombThrow weapon;
    Player player;
    Transform bomb, shadow;
    SpriteRenderer flames; // on the bomb from the top of the arc, then the patch
    Vector2 from, to;
    float arcHeight, radius, age, tickTimer, wobblePhase;
    int damage;
    bool lit, burning;

    public static void Throw(FireBombThrow weapon, Vector2 from, Vector2 to, float radius, int damage, Player player)
    {
        FireBomb fireBomb = new GameObject("Fire Bomb").AddComponent<FireBomb>();
        fireBomb.transform.position = from;
        fireBomb.weapon = weapon;
        fireBomb.player = player;
        fireBomb.from = from;
        fireBomb.to = to;
        fireBomb.arcHeight = LobHeight + LobPerUnit * Vector2.Distance(from, to);
        fireBomb.radius = radius;
        fireBomb.damage = damage;
        fireBomb.wobblePhase = Random.value * 10f;

        fireBomb.shadow = fireBomb.Layer("Shadow", player.arena.shadowSprite, ShadowColor, -1).transform;
        SpriteRenderer bomb = fireBomb.Layer("Bomb", weapon.bombSprite, Color.white, 3);
        bomb.transform.localScale = Vector3.one * BombSize;
        Fx.Unlit(bomb);
        fireBomb.bomb = bomb.transform;
        fireBomb.flames = fireBomb.Layer("Flames", weapon.fireSprite, Color.clear, 2); // behind the bomb, so it peeks out
        Fx.Unlit(fireBomb.flames);
        All.Add(fireBomb);
    }

    SpriteRenderer Layer(string name, Sprite sprite, Color color, int sortingOrder)
    {
        SpriteRenderer layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(transform, false);
        layer.sprite = sprite;
        layer.color = color;
        layer.sortingOrder = sortingOrder;
        return layer;
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
            float t = Mathf.Clamp01(age / weapon.flightTime);
            float lift = 4f * t * (1f - t); // 0 -> 1 at the top of the arc -> 0
            transform.position = Vector2.Lerp(from, to, t);
            bomb.localPosition = new Vector3(0f, arcHeight * lift, 0f);
            bomb.Rotate(0f, 0f, -SpinSpeed * Time.deltaTime);
            shadow.localScale = new Vector3(ShadowWidth, ShadowWidth * 0.35f, 1f) * (1f - 0.4f * lift);

            float fire = Mathf.Clamp01((t - 0.5f) / 0.1f); // catches fire at the top of the arc
            if (fire > 0f && !lit)
            {
                lit = true;
                Glow.Add(bomb.gameObject, weapon.fireColor, 1.5f, 0.8f); // goes with the bomb when it breaks
            }
            flames.transform.localPosition = bomb.localPosition + new Vector3(0f, FlameRise, 0f);
            ShowFlames(FlameSize, fire);
            if (t >= 1f) Break();
            return;
        }

        if (age >= weapon.burnTime)
        {
            Destroy(gameObject);
            return;
        }
        ShowFlames(radius * 2f * Fx.EaseOutBack(age / PatchGrowTime), 1f);
        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = weapon.tickInterval;
            Burn();
        }
    }

    // The flames `size` across (the art is 1 unit wide), wobbling, at `alpha` x PatchAlpha.
    void ShowFlames(float size, float alpha)
    {
        flames.transform.localScale = Fx.FlameScale(size, age + wobblePhase);
        Color color = weapon.fireColor;
        color.a = PatchAlpha * alpha;
        flames.color = color;
    }

    // Lands: the bomb breaks into shards and sparks, and the fire spreads into a patch over a scorch mark. The first
    // tick is right away.
    void Break()
    {
        burning = true;
        age = 0f;
        tickTimer = 0f;
        transform.position = to;
        Destroy(bomb.gameObject); // (its light too)
        Destroy(shadow.gameObject);
        player.arena.deathBursts.Burst(to, weapon.shardColor, 0.3f, false);
        player.arena.deathBursts.Burst(to, weapon.fireColor, 0.3f, false);
        Sfx.Play(Sound.BombBreak);
        Sfx.Play(Sound.Flame);

        flames.transform.localPosition = Vector3.zero;
        flames.sortingOrder = -1;
        if (weapon.scorchSprite != null)
        {
            SpriteRenderer scorch = Layer("Scorch", weapon.scorchSprite, ScorchColor, -2); // under the flames
            scorch.transform.localScale = Vector3.one * radius * 2f;
        }
        Glow.Add(gameObject, weapon.fireColor, radius * 2.5f, 1.1f); // the fire lights up the cave
    }

    void Burn()
    {
        Vector2 center = transform.position;
        foreach (Enemy enemy in Enemy.All)
        {
            float reach = radius + enemy.radius;
            if (!enemy.IsDead && ((Vector2)enemy.transform.position - center).sqrMagnitude <= reach * reach)
                enemy.TakeDamage(damage, center, 0f);
        }
    }
}
