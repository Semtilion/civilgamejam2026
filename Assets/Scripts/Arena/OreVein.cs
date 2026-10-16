using System.Collections.Generic;
using UnityEngine;

// An ore vein feeding the earthquake. Stand next to it for `ArenaManager.oreMineTime` seconds to mine it
// (walking away resets the progress; taking hits doesn't). Mined ores lower the quake meter, but only if the hero
// gets home safely (see Campaign.EndDive).
public class OreVein : MonoBehaviour
{
    public static readonly List<OreVein> All = new List<OreVein>();

    const float BarWidth = 1.4f;

    ArenaManager arena;
    Transform barFill;
    GameObject bar;
    float progress;

    // `sprite` drawn at `scale` with `color` (art: white, 1; placeholder square: tinted, bigger).
    // `barSprite` is a plain white square for the progress bar.
    public static void Place(Sprite sprite, Color color, float scale, Sprite barSprite, Vector2 position, ArenaManager arena)
    {
        GameObject go = new GameObject("Ore Vein");
        go.transform.position = position;
        NewPart("Rock", go.transform, sprite, Vector2.zero, Vector2.one * scale, color, -1);

        // Mining progress bar just above the rock, only shown while mining
        float barY = sprite.bounds.size.y * scale / 2f + 0.25f;
        GameObject bar = new GameObject("Bar");
        bar.transform.SetParent(go.transform, false);
        NewPart("Back", bar.transform, barSprite, new Vector2(0f, barY), new Vector2(BarWidth, 0.15f), new Color(0f, 0f, 0f, 0.7f), 6);
        Transform fill = NewPart("Fill", bar.transform, barSprite, new Vector2(0f, barY), new Vector2(0f, 0.15f), new Color(1f, 0.85f, 0.2f), 7);
        bar.SetActive(false);

        OreVein ore = go.AddComponent<OreVein>();
        ore.arena = arena;
        ore.bar = bar;
        ore.barFill = fill;
        All.Add(ore);
    }

    static Transform NewPart(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
    {
        SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.transform.localPosition = position;
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer.transform;
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!arena.IsPlaying) return;

        float reach = arena.oreMineRadius;
        bool near = ((Vector2)(arena.player.transform.position - transform.position)).sqrMagnitude <= reach * reach;
        progress = near ? progress + Time.deltaTime / arena.oreMineTime : 0f;

        bar.SetActive(progress > 0f);
        float width = BarWidth * Mathf.Clamp01(progress);
        barFill.localScale = new Vector3(width, barFill.localScale.y, 1f);
        barFill.localPosition = new Vector3((width - BarWidth) / 2f, barFill.localPosition.y, 0f); // grows from the left

        if (progress >= 1f)
        {
            arena.OreMined();
            Destroy(gameObject);
        }
    }
}
