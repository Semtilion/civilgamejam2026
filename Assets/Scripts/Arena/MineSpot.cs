using System.Collections.Generic;
using UnityEngine;

public enum SpotKind { Ore, XpShrine, MaterialsShrine, CrystalShrine }

// An ore vein or a shrine. Stand next to it for `mineTime` seconds to use it (walking away resets the progress;
// taking hits doesn't); then it's gone and ArenaManager.SpotMined gives the reward. Mined ores lower the quake meter,
// but only if the hero gets home safely (see Campaign.EndDive), and every one shakes the cave (a tremor, maybe a trap).
public class MineSpot : MonoBehaviour
{
    public static readonly List<MineSpot> All = new List<MineSpot>();

    const float BarWidth = 1.4f;

    SpotKind kind;
    float mineTime;
    ArenaManager arena;
    Transform barFill;
    GameObject bar;
    float progress;

    // `sprite` drawn at `scale` with `color` (ore art: white, 1; shrines: the same art tinted by type).
    // `barSprite` is a plain white square for the progress bar.
    public static void Place(SpotKind kind, Sprite sprite, Color color, float scale, float mineTime, Sprite barSprite,
        Vector2 position, ArenaManager arena)
    {
        GameObject go = new GameObject(kind == SpotKind.Ore ? "Ore Vein" : kind.ToString());
        go.transform.position = position;
        NewPart("Rock", go.transform, sprite, Vector2.zero, Vector2.one * scale, color, -1).gameObject.AddComponent<SpriteOutline>();
        // Ores smoulder red (they feed the quake); shrines glow in their own color.
        Glow.Add(go, kind == SpotKind.Ore ? new Color(1f, 0.4f, 0.25f) : color, 3.5f, 1f);

        // Progress bar just above the sprite, only shown while mining
        float barY = sprite.bounds.size.y * scale / 2f + 0.25f;
        GameObject bar = new GameObject("Bar");
        bar.transform.SetParent(go.transform, false);
        NewPart("Back", bar.transform, barSprite, new Vector2(0f, barY), new Vector2(BarWidth, 0.15f), new Color(0f, 0f, 0f, 0.7f), 6);
        Transform fill = NewPart("Fill", bar.transform, barSprite, new Vector2(0f, barY), new Vector2(0f, 0.15f), new Color(1f, 0.85f, 0.2f), 7);
        bar.SetActive(false);

        MineSpot spot = go.AddComponent<MineSpot>();
        spot.kind = kind;
        spot.mineTime = mineTime;
        spot.arena = arena;
        spot.bar = bar;
        spot.barFill = fill;
        All.Add(spot);
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
        progress = near ? progress + Time.deltaTime / mineTime : 0f;

        bar.SetActive(progress > 0f);
        float width = BarWidth * Mathf.Clamp01(progress);
        barFill.localScale = new Vector3(width, barFill.localScale.y, 1f);
        barFill.localPosition = new Vector3((width - BarWidth) / 2f, barFill.localPosition.y, 0f); // grows from the left

        if (progress >= 1f)
        {
            arena.SpotMined(kind);
            Destroy(gameObject);
        }
    }
}
