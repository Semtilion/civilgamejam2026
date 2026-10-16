using System.Collections.Generic;
using UnityEngine;

public enum PickupKind { Xp, Materials }

// Something an enemy dropped: an XP gem or materials. Once the hero comes within pickup range
// it flies to the hero; touching it collects it (ArenaManager.Collect).
public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();

    const float FlySpeed = 14f;
    static readonly Color XpColor = new Color(0.30f, 0.90f, 1f);
    static readonly Color MaterialsColor = new Color(0.85f, 0.65f, 0.35f);

    public PickupKind kind;
    public int amount;

    ArenaManager arena;
    bool pulled;

    public static void Drop(PickupKind kind, Sprite sprite, Vector2 position, int amount, ArenaManager arena)
    {
        GameObject go = new GameObject(kind == PickupKind.Xp ? "XP Gem" : "Materials");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * (amount >= 3 ? 0.35f : 0.25f); // bigger = worth more

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = kind == PickupKind.Xp ? XpColor : MaterialsColor;
        renderer.sortingOrder = 0;

        Pickup pickup = go.AddComponent<Pickup>();
        pickup.kind = kind;
        pickup.amount = amount;
        pickup.arena = arena;
        All.Add(pickup);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!arena.IsPlaying) return;

        Player player = arena.player;
        Vector2 toPlayer = player.transform.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance <= player.radius)
        {
            arena.Collect(kind, amount);
            Destroy(gameObject);
            return;
        }

        if (distance <= player.pickupRange) pulled = true; // once pulled, it keeps flying to the hero
        if (pulled) transform.position += (Vector3)(toPlayer / distance * Mathf.Min(FlySpeed * Time.deltaTime, distance));
    }
}
