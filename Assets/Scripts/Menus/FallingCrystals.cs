using UnityEngine;
using UnityEngine.UI;

// The title screen's background: crystals falling slowly behind the title and buttons, each with a soft glow in its
// color. Near ones (bigger, brighter) fall faster than far ones; each turns gently and sways a little. They keep out
// of the middle column (the title and buttons) and start again above the top once they leave the bottom.
// Varied: white / blue / cyan / pink / violet, slimmer or stouter, mirrored, and some are the Spitter's shard.
public class FallingCrystals : MonoBehaviour
{
    const int Count = 26;
    const float ClearMiddle = 330f;          // UI units each side of the middle kept clear
    const float MinSize = 30f, MaxSize = 100f; // UI units (the long side), far -> near
    const float SlowCrossing = 25f, FastCrossing = 12f; // seconds to fall across the screen, far -> near
    const float GlowSize = 2.2f;             // x the crystal's size
    const float Margin = 120f;               // UI units past the top / bottom edge (glow included)
    const float ShardShare = 0.3f;
    static readonly Color[] Tints =
    {
        Color.white, new Color(0.55f, 0.75f, 1f), new Color(0.6f, 1f, 1f), new Color(1f, 0.65f, 0.9f), new Color(0.8f, 0.6f, 1f),
    };
    static readonly Color[] GlowColors = // in Tints order
    {
        new Color(0.6f, 0.35f, 1f), new Color(0.35f, 0.5f, 1f), new Color(0.3f, 0.8f, 1f), new Color(0.95f, 0.4f, 0.85f), new Color(0.55f, 0.3f, 1f),
    };

    public Sprite crystal;
    [Tooltip("Optional: the Spitter's shard (lying art stands up).")]
    public Sprite shard;
    [Tooltip("A soft round glow, white (tinted per crystal).")]
    public Sprite glow;

    class Falling
    {
        public RectTransform root, body; // the glow, and the crystal (a child, so it's drawn over its glow)
        public float x, y, crossTime, spin, sway, swaySpeed, phase;
    }

    Falling[] crystals; // made on the first Update, once the canvas has its size

    void Update()
    {
        Rect area = ((RectTransform)transform).rect;
        if (area.width <= 0f) return; // the canvas isn't sized yet
        if (crystals == null) Begin(area);

        float distance = area.height + 2f * Margin;
        foreach (Falling falling in crystals)
        {
            falling.y -= distance / falling.crossTime * Time.unscaledDeltaTime;
            if (falling.y < area.yMin - Margin)
            {
                falling.y = area.yMax + Margin;
                falling.x = RandomX(area);
            }
            falling.root.anchoredPosition = new Vector2(falling.x + falling.sway * Mathf.Sin(Time.unscaledTime * falling.swaySpeed + falling.phase), falling.y);
            falling.body.Rotate(0f, 0f, falling.spin * Time.unscaledDeltaTime);
        }
    }

    // Spread over the screen, far ones first so the near ones are drawn over them.
    void Begin(Rect area)
    {
        float[] depths = new float[Count]; // 0 far, 1 near
        for (int i = 0; i < Count; i++) depths[i] = Random.value;
        System.Array.Sort(depths);
        crystals = new Falling[Count];
        for (int i = 0; i < Count; i++)
        {
            crystals[i] = Make(depths[i]);
            crystals[i].x = RandomX(area);
            crystals[i].y = Random.Range(area.yMin, area.yMax);
        }
    }

    Falling Make(float depth)
    {
        float size = Mathf.Lerp(MinSize, MaxSize, depth);
        int tint = Random.Range(0, Tints.Length);
        Sprite art = shard != null && Random.value < ShardShare ? shard : crystal;

        Color glowColor = GlowColors[tint];
        glowColor.a = Mathf.Lerp(0.25f, 0.5f, depth);
        Image halo = NewImage("Crystal", transform, glow, glowColor);
        halo.rectTransform.sizeDelta = Vector2.one * size * GlowSize;

        Color bodyColor = Tints[tint] * Mathf.Lerp(0.45f, 1f, depth); // further = dimmer
        bodyColor.a = 1f;
        Image body = NewImage("Body", halo.transform, art, bodyColor);
        Vector2 box = art.rect.size * (size / Mathf.Max(art.rect.width, art.rect.height));
        float stretch = Random.Range(0.72f, 1.3f); // slimmer or stouter
        if (box.x < box.y) box.x *= stretch;
        else box.y *= stretch;
        body.rectTransform.sizeDelta = box;
        body.rectTransform.localScale = new Vector3(Random.value < 0.5f ? -1f : 1f, 1f, 1f);
        float standUp = art.rect.width > art.rect.height ? 90f : 0f;
        body.rectTransform.localRotation = Quaternion.Euler(0f, 0f, standUp + Random.Range(-35f, 35f));

        return new Falling
        {
            root = halo.rectTransform,
            body = body.rectTransform,
            crossTime = Mathf.Lerp(SlowCrossing, FastCrossing, depth),
            spin = Random.Range(5f, 20f) * (Random.value < 0.5f ? -1f : 1f), // degrees per second
            sway = Random.Range(8f, 25f),
            swaySpeed = 2f * Mathf.PI / Random.Range(3f, 6f),
            phase = Random.value * 10f,
        };
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false; // never in the way of the buttons
        return image;
    }

    // Left or right of the middle column, inside the screen.
    static float RandomX(Rect area)
    {
        float side = Random.value < 0.5f ? -1f : 1f;
        return side * Random.Range(ClearMiddle, Mathf.Max(ClearMiddle, area.xMax - 20f));
    }
}
