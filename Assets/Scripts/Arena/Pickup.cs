using System.Collections.Generic;
using UnityEngine;

public enum PickupKind { Xp, Materials, Crystal, Gold, Supplies }

// Something an enemy dropped: an XP gem, materials, a crystal, gold or supplies. Once the hero comes within pickup range
// it flies to the hero; touching it collects it (ArenaManager.Collect) with a little sparkle.
// Look: a child sprite (unlit, so it shines in the dark cave) that bobs and spins; crystals also glow.
public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();

    const float FlySpeed = 14f;
    const float BobHeight = 0.08f;
    const float SpinSpeed = 120f; // degrees per second
    static readonly Color XpColor = new Color(0.30f, 0.90f, 1f);
    public static readonly Color MaterialsColor = new Color(0.85f, 0.65f, 0.35f); // also the materials shrine's color
    static readonly Color CrystalColor = new Color(0.85f, 0.55f, 1f);
    static readonly Color GoldColor = new Color(1f, 0.85f, 0.15f);
    static readonly Color SuppliesColor = new Color(0.55f, 0.95f, 0.55f);

    public PickupKind kind;
    public int amount;

    ArenaManager arena;
    Transform look;
    Color color;
    float phase;
    bool pulled;

    public static void Drop(PickupKind kind, Sprite sprite, Vector2 position, int amount, ArenaManager arena)
    {
        GameObject go = new GameObject(kind.ToString());
        go.transform.position = position;

        Color color = kind == PickupKind.Xp ? XpColor : kind == PickupKind.Materials ? MaterialsColor :
            kind == PickupKind.Crystal ? CrystalColor : kind == PickupKind.Supplies ? SuppliesColor : GoldColor;
        SpriteRenderer renderer = new GameObject("Sprite").AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(go.transform, false);
        renderer.transform.localScale = Vector3.one * (amount >= 3 || kind == PickupKind.Crystal ? 0.35f : 0.25f); // bigger = worth more
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 0;
        Fx.Unlit(renderer);
        renderer.gameObject.AddComponent<SpriteOutline>();
        if (kind == PickupKind.Crystal) Glow.Add(go, CrystalColor, 1.8f, 0.8f);

        Pickup pickup = go.AddComponent<Pickup>();
        pickup.kind = kind;
        pickup.amount = amount;
        pickup.arena = arena;
        pickup.look = renderer.transform;
        pickup.color = color;
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
        look.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);

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
