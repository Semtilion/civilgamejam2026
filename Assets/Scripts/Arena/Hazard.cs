using System.Collections.Generic;
using UnityEngine;

// A danger on the floor left by a variant enemy (EnemyVariant); it only hurts the hero. Fire (a red variant's trail):
// flames that burn for a while, hurting the hero every tick while inside, fading out at the end. Blast (a purple
// variant's death): its area as a faint ring, a ring that grows to fill it and a core that pulses faster and faster;
// when the fuse runs out, one hit to the hero inside and a burst. Frozen while a panel is open; cleared with the field.
public class Hazard : MonoBehaviour
{
    public static readonly List<Hazard> All = new List<Hazard>();

    const float FireAlpha = 0.8f;
    const float FireFadeIn = 0.15f;  // seconds
    const float FireFadeOut = 0.4f;  // the last seconds
    const float FireTurnSpeed = 60f; // degrees per second, so the flames look alive
    const float CoreSize = 0.6f;     // world units

    ArenaManager arena;
    SpriteRenderer look;       // the flames, or the blast's growing ring
    SpriteRenderer area, core; // blast only
    Color color;
    bool blast;
    float radius, life, tick, age, tickTimer;
    int damage;

    // A fire patch at `at` (the fire art is 1 unit wide): burns `life` seconds, `damage` every `tick` seconds while the
    // hero is inside (the first right away).
    public static void Fire(Sprite fire, Color color, Vector2 at, float radius, float life, float tick, int damage, ArenaManager arena)
    {
        Hazard hazard = Create("Fire Patch", at, color, radius, life, damage, arena);
        hazard.tick = tick;
        hazard.look = hazard.Layer("Flames", fire, radius * 2f, -1);
        hazard.transform.Rotate(0f, 0f, Random.value * 360f);
        hazard.Draw();
    }

    // A blast at `at` (sprites 1 unit wide): after `fuse` seconds, `damage` to the hero within `radius`.
    public static void Blast(Sprite circle, Sprite ring, Color color, Vector2 at, float radius, float fuse, int damage, ArenaManager arena)
    {
        Hazard hazard = Create("Blast", at, color, radius, fuse, damage, arena);
        hazard.blast = true;
        hazard.area = hazard.Layer("Area", ring, radius * 2f, -1);
        hazard.look = hazard.Layer("Fuse", ring, 0f, -1);
        hazard.core = hazard.Layer("Core", circle, CoreSize, 2); // over the enemies, so it can't hide in the swarm
        Glow.Add(hazard.gameObject, color, radius * 1.5f, 1f);
        hazard.Draw();
    }

    static Hazard Create(string name, Vector2 at, Color color, float radius, float life, int damage, ArenaManager arena)
    {
        GameObject go = new GameObject(name);
        go.transform.position = at;
        Hazard hazard = go.AddComponent<Hazard>();
        hazard.arena = arena;
        hazard.color = color;
        hazard.radius = radius;
        hazard.life = life;
        hazard.damage = damage;
        All.Add(hazard);
        return hazard;
    }

    SpriteRenderer Layer(string name, Sprite sprite, float size, int sortingOrder)
    {
        SpriteRenderer layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(transform, false);
        layer.transform.localScale = Vector3.one * size;
        layer.sprite = sprite;
        layer.sortingOrder = sortingOrder;
        Fx.Unlit(layer);
        return layer;
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!arena.IsPlaying) return;

        age += Time.deltaTime;
        if (blast)
        {
            if (age >= life)
            {
                Explode();
                return;
            }
        }
        else
        {
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }
            transform.Rotate(0f, 0f, FireTurnSpeed * Time.deltaTime);
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f && HeroInside())
            {
                tickTimer = tick;
                arena.player.TakeDamage(damage); // (the hero's hit invulnerability still applies)
            }
        }
        Draw();
    }

    void Draw()
    {
        if (!blast)
        {
            float fade = Mathf.Min(age / FireFadeIn, (life - age) / FireFadeOut); // in, then out at the end
            look.color = new Color(color.r, color.g, color.b, FireAlpha * Mathf.Clamp01(fade));
            return;
        }
        float t = Mathf.Clamp01(age / life); // 0 -> 1 over the fuse
        area.color = new Color(color.r, color.g, color.b, 0.35f);
        look.transform.localScale = Vector3.one * radius * 2f * t;
        look.color = new Color(color.r, color.g, color.b, 0.5f + 0.4f * t);
        float pulse = 0.5f + 0.5f * Mathf.Sin(age * Mathf.Lerp(8f, 30f, t)); // faster and faster
        core.transform.localScale = Vector3.one * CoreSize * (1f + 0.25f * pulse);
        core.color = Color.Lerp(color, Color.white, 0.6f * pulse);
    }

    void Explode()
    {
        if (HeroInside()) arena.player.TakeDamage(damage);
        arena.deathBursts.Burst(transform.position, color, radius * 0.6f, true);
        Destroy(gameObject);
    }

    bool HeroInside()
    {
        Player player = arena.player;
        float reach = radius + player.radius;
        return ((Vector2)(player.transform.position - transform.position)).sqrMagnitude <= reach * reach;
    }
}
