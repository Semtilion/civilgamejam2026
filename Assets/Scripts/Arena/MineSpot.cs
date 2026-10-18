using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum SpotKind { Ore, XpShrine, MaterialsShrine, CrystalShrine }

// An ore vein or a shrine. Stand next to it for `mineTime` seconds to use it (walking away resets the progress;
// taking hits doesn't); then it's gone and ArenaManager.SpotMined gives the reward. Mined ores lower the quake meter,
// but only if the hero gets home safely (see Campaign.EndDive), and every one shakes the cave (a tremor, maybe a trap).
// While it's being mined: a ring around its foot fills up (in its color), chips fly off, it shakes and glows brighter
// and brighter; done, it bursts (an ore's rubble is the tremor's).
public class MineSpot : MonoBehaviour
{
    public static readonly List<MineSpot> All = new List<MineSpot>();

    const float RingSize = 2.1f;       // world units across, flattened to half that (lying on the floor)
    const float ChipInterval = 0.2f;   // seconds between chips
    const float ShakeAmount = 0.05f;   // world units, at full progress
    static readonly Color RingBackColor = new Color(0f, 0f, 0f, 0.55f);

    SpotKind kind;
    float mineTime;
    ArenaManager arena;
    Transform rock;
    GameObject progressRing;
    Image ringFill;
    Glow glow;
    Color chipColor;
    float progress, chipTimer;

    // `sprite` drawn at `scale` with `color` (ore art: white, 1; shrines: the same art tinted by type).
    // `ringSprite`: a white ring 1 unit wide, for the progress ring.
    public static void Place(SpotKind kind, Sprite sprite, Color color, float scale, float mineTime, Sprite ringSprite,
        Vector2 position, ArenaManager arena)
    {
        GameObject go = new GameObject(kind == SpotKind.Ore ? "Ore Vein" : kind.ToString());
        go.transform.position = position;
        Transform rock = NewPart("Rock", go.transform, sprite, color, scale);
        rock.gameObject.AddComponent<SpriteOutline>();
        // Ores smoulder red (they feed the quake); shrines glow in their own color.
        Glow.Add(go, kind == SpotKind.Ore ? new Color(1f, 0.4f, 0.25f) : color, 3.5f, 1f);
        Color chipColor = arena.oreArtColor * color;
        chipColor.a = 1f;

        // The progress ring around its foot: a world-space UI image (radial fill), only shown while mining
        Canvas canvas = new GameObject("Progress", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(go.transform, false);
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = -2; // on the floor, behind the rock
        RectTransform area = (RectTransform)canvas.transform;
        area.sizeDelta = Vector2.one * 100f;
        area.localScale = new Vector3(RingSize / 100f, RingSize / 200f, 1f);
        area.localPosition = new Vector3(0f, -sprite.bounds.size.y * scale / 2f + 0.25f, 0f);
        RingImage("Back", area, ringSprite, RingBackColor);
        Image fill = RingImage("Fill", area, ringSprite, Color.Lerp(chipColor, Color.white, 0.4f));
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.fillAmount = 0f;
        canvas.gameObject.SetActive(false);

        MineSpot spot = go.AddComponent<MineSpot>();
        spot.kind = kind;
        spot.mineTime = mineTime;
        spot.arena = arena;
        spot.rock = rock;
        spot.progressRing = canvas.gameObject;
        spot.ringFill = fill;
        spot.glow = go.GetComponent<Glow>();
        spot.chipColor = chipColor;
        All.Add(spot);
    }

    static Transform NewPart(string name, Transform parent, Sprite sprite, Color color, float scale)
    {
        SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.transform.localScale = Vector3.one * scale;
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = -1;
        return renderer.transform;
    }

    static Image RingImage(string name, Transform parent, Sprite sprite, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        RectTransform rect = image.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
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

        progressRing.SetActive(progress > 0f);
        ringFill.fillAmount = Mathf.Clamp01(progress);
        rock.localPosition = progress > 0f ? (Vector3)(Random.insideUnitCircle * ShakeAmount * progress) : Vector3.zero;
        if (glow != null) glow.boost = 1f + progress;
        if (progress > 0f)
        {
            chipTimer -= Time.deltaTime;
            if (chipTimer <= 0f)
            {
                chipTimer = ChipInterval;
                Sfx.Play(Sound.Mining);
                arena.deathBursts.Sparkle((Vector2)transform.position + Random.insideUnitCircle * 0.4f, chipColor);
            }
        }

        if (progress >= 1f)
        {
            FadeEffect.Circle(arena.fxBurst, transform.position, 1.2f, new Color(chipColor.r, chipColor.g, chipColor.b, 0.9f), 0.3f);
            arena.SpotMined(kind, transform.position);
            Destroy(gameObject);
        }
    }
}
