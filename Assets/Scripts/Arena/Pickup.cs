using System.Collections.Generic;
using UnityEngine;

public enum PickupKind { Xp, Materials, Crystal, Gold, Supplies, Relic }

// Something an enemy dropped: an XP gem, materials, a crystal, gold, supplies or a relic (a variant's rare drop: a
// big glowing star; collecting it shows a relic card). Once the hero comes within pickup range
// it flies to the hero; touching it collects it (ArenaManager.Collect) with a little sparkle.
// Look: a child sprite (unlit, so it shines in the dark cave) that bobs and spins; crystals also glow. Crystals, gold,
// wood and food use the artist's art (the crystal is the clusters' crystal, gold a nugget, wood one of two logs, food a
// drumstick), drawn bigger, in their own colors and only bobbing (painted art spinning flat looks odd).
public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();

    const float FlySpeed = 14f;
    const float BobHeight = 0.08f;
    const float SpinSpeed = 120f; // degrees per second
    static readonly Color XpColor = new Color(0.30f, 0.90f, 1f);
    public static readonly Color MaterialsColor = new Color(0.85f, 0.65f, 0.35f); // also the materials shrine's color
    public static readonly Color SuppliesColor = new Color(0.85f, 0.55f, 0.42f); // the food art's warm brown, lifted
    static readonly Color RelicColor = new Color(1f, 0.90f, 0.60f);

    public PickupKind kind;
    public int amount;

    ArenaManager arena;
    Transform look;
    Color color;
    float phase;
    bool pulled;
    bool spins;

    public static void Drop(PickupKind kind, Sprite sprite, Vector2 position, int amount, ArenaManager arena)
    {
        GameObject go = new GameObject(kind.ToString());
        go.transform.position = position;

        // Its color: the sparkle when it's collected (and the tint of the plain shapes, the crystal's glow); the
        // crystal and gold are the artist's art, so theirs come from it (ArenaManager).
        Color color = kind == PickupKind.Xp ? XpColor : kind == PickupKind.Materials ? MaterialsColor :
            kind == PickupKind.Crystal ? arena.crystalColor : kind == PickupKind.Supplies ? SuppliesColor :
            kind == PickupKind.Relic ? RelicColor : arena.goldColor;
        bool paintedArt = kind == PickupKind.Crystal || kind == PickupKind.Gold || kind == PickupKind.Materials ||
                          kind == PickupKind.Supplies;
        // World size (the sprites are 1 unit): bigger = worth more.
        float size = kind == PickupKind.Crystal ? 0.5f : kind == PickupKind.Relic ? 0.9f :
            kind == PickupKind.Gold || kind == PickupKind.Supplies ? (amount >= 3 ? 0.5f : 0.4f) :
            kind == PickupKind.Materials ? (amount >= 3 ? 0.75f : 0.6f) : // (a log's width)
            amount >= 3 ? 0.35f : 0.25f;
        SpriteRenderer renderer = new GameObject("Sprite").AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(go.transform, false);
        renderer.transform.localScale = Vector3.one * size;
        renderer.sprite = sprite;
        renderer.color = paintedArt ? Color.white : color; // painted art keeps its own colors
        renderer.sortingOrder = 0;
        Fx.Unlit(renderer);
        if (kind != PickupKind.Relic) renderer.gameObject.AddComponent<SpriteOutline>(); // (the relic star is all soft glow)
        if (kind == PickupKind.Crystal) Glow.Add(go, color, 1.8f, 0.8f);
        if (kind == PickupKind.Relic) Glow.Add(go, RelicColor, 2.2f, 1f);

        Pickup pickup = go.AddComponent<Pickup>();
        pickup.kind = kind;
        pickup.amount = amount;
        pickup.arena = arena;
        pickup.look = renderer.transform;
        pickup.color = color;
        pickup.spins = !paintedArt;
        pickup.phase = Random.value * Mathf.PI * 2f;
        All.Add(pickup);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!arena.IsPlaying) return;

        // Bob and spin (the look only; the pickup itself stays put until it's pulled).
        phase += Time.deltaTime * 4f;
        look.localPosition = new Vector3(0f, BobHeight * Mathf.Sin(phase), 0f);
        if (spins) look.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);

        Player player = arena.player;
        Vector2 toPlayer = player.transform.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance <= player.radius)
        {
            arena.Collect(kind, amount);
            arena.deathBursts.Sparkle(transform.position, color);
            Destroy(gameObject);
            return;
        }

        if (distance <= player.pickupRange) pulled = true; // once pulled, it keeps flying to the hero
        if (pulled) transform.position += (Vector3)(toPlayer / distance * Mathf.Min(FlySpeed * Time.deltaTime, distance));
    }
}
