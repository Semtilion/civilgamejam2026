using System.Collections.Generic;
using UnityEngine;

// One rock of the rock rain trap. For `warning` seconds its target grows on the floor (a dark red circle with a bright
// edge) while the rock (a lump of the cave's stone, one of the arena's rockSprites) falls into it from above, growing
// as it comes. Then it lands: it hurts the hero if they're under it (enemies are never hurt), pieces fly and dust puffs
// up around it, the floor cracks under it, the camera bumps if the hero is close; the rock then fades away.
public class FallingRock : MonoBehaviour
{
    public static readonly List<FallingRock> All = new List<FallingRock>();

    const float FallHeight = 6f;   // units above its target when it starts falling
    const float FadeTime = 0.5f;   // after landing
    const float BumpRange = 3f;    // units from the hero
    const float CrackSize = 1.2f;  // the cracks under it reach this far (x its radius)
    static readonly Color TargetColor = new Color(0.43f, 0.08f, 0.08f, 0.7f);
    static readonly Color EdgeColor = new Color(1f, 0.35f, 0.27f, 0.8f);

    ArenaManager arena;
    SpriteRenderer target, edge, rock, cracks;
    float radius, warning, age;
    int damage;
    bool landed;

    public static void Drop(Vector2 position, float radius, float warning, int damage, ArenaManager arena)
    {
        FallingRock falling = new GameObject("Falling Rock").AddComponent<FallingRock>();
        falling.transform.position = position;
        falling.arena = arena;
        falling.radius = radius;
        falling.warning = warning;
        falling.damage = damage;
        falling.target = falling.Layer("Target", arena.rockTargetSprite, TargetColor, -1);
        falling.edge = falling.Layer("Edge", arena.blastRingSprite, EdgeColor, -1); // (the plain ring)
        bool art = arena.rockSprites.Length > 0;
        falling.rock = falling.Layer("Rock", art ? arena.rockSprites[Random.Range(0, arena.rockSprites.Length)] : arena.rockTargetSprite,
            art ? Color.white : arena.rockColor, 6); // over everyone while it falls and as it lands
        if (arena.tremorCrackSprites.Length > 0)
        {
            falling.cracks = falling.Layer("Cracks", arena.tremorCrackSprites[Random.Range(0, arena.tremorCrackSprites.Length)], Color.white, -1);
            falling.cracks.transform.localRotation = Quaternion.Euler(0f, 0f, Random.value * 360f);
            falling.cracks.transform.localScale = Vector3.one * radius * CrackSize * 2f / TremorEffect.PatternReach;
            falling.cracks.enabled = false; // until it lands
        }
        falling.Draw();
        All.Add(falling);
    }

    SpriteRenderer Layer(string name, Sprite sprite, Color color, int sortingOrder)
    {
        SpriteRenderer layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(transform, false);
        layer.sprite = sprite;
        layer.color = color;
        layer.sortingOrder = sortingOrder;
        Fx.Unlit(layer); // the warning and the rock must stay readable in the dark
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
        if (!landed && age >= warning) Land();
        if (landed && age >= warning + FadeTime)
        {
            Destroy(gameObject);
            return;
        }
        Draw();
    }

    void Draw()
    {
        if (!landed)
        {
            // The target grows from 40%; the rock comes down from FallHeight (faster and faster), from half its size.
            float k = Mathf.Clamp01(age / warning);
            Vector3 targetSize = Vector3.one * radius * 2f * Mathf.Lerp(0.4f, 1f, k); // the circle and ring are 1 unit wide
            target.transform.localScale = targetSize;
            edge.transform.localScale = targetSize;
            target.color = new Color(TargetColor.r, TargetColor.g, TargetColor.b, TargetColor.a * Mathf.Lerp(0.7f, 1f, k));
            rock.transform.localPosition = new Vector3(0f, (1f - k) * (1f - k) * FallHeight, 0f);
            rock.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.5f, 1f, k);
            return;
        }
        // Landed: the rock and its cracks fade, the rock sinking a little.
        float fade = Mathf.Clamp01((age - warning) / FadeTime);
        rock.transform.localPosition = new Vector3(0f, -0.1f * fade, 0f);
        SetAlpha(rock, 1f - fade);
        if (cracks != null) SetAlpha(cracks, 1f - fade);
    }

    void Land()
    {
        landed = true;
        target.enabled = false;
        edge.enabled = false;
        rock.transform.localScale = Vector3.one * radius * 2f;
        if (cracks != null) cracks.enabled = true;

        Player player = arena.player;
        Vector2 at = transform.position;
        Vector2 toHero = (Vector2)player.transform.position - at;
        float reach = radius + player.radius;
        if (toHero.sqrMagnitude < reach * reach) player.TakeDamage(damage);
        if (toHero.sqrMagnitude < BumpRange * BumpRange) arena.ShakeCamera(0.12f, 0.12f);
        Sfx.Play(Sound.RockLand);

        arena.deathBursts.Burst(at, arena.rockColor, radius * 0.6f, false); // pieces of the rock
        for (int i = 0; i < 6; i++) // dust all around it
        {
            float angle = i * Mathf.PI / 3f;
            arena.deathBursts.Puff(at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.9f, 0.5f);
        }
    }

    static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }
}
