using UnityEngine;

// A flash that fades out and disappears (explosions, lightning, the Second Wind burst, the teleporter, the level-up
// light), optionally growing from `startScale` of its size (easing out). Scaled time, so it freezes while the game is
// paused, unless `unscaled` (the moments that freeze the game: the level-up, stepping into the teleporter).
public class FadeEffect : MonoBehaviour
{
    SpriteRenderer sprite;
    Vector3 fullScale;
    float duration, startAlpha, startScale, age;
    bool unscaled;

    // A circle of `radius` at `position` (the sprite is 1 unit wide).
    public static void Circle(Sprite sprite, Vector2 position, float radius, Color color, float duration,
        float startScale = 1f, bool unscaled = false, int sortingOrder = 4)
    {
        Create(sprite, position, Quaternion.identity, Vector3.one * radius * 2f, color, duration, startScale, unscaled, sortingOrder);
    }

    // A bar from `from` to `to`, `width` thick (the sprite is 1 unit wide).
    public static void Line(Sprite sprite, Vector2 from, Vector2 to, float width, Color color, float duration)
    {
        Vector2 offset = to - from;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
        Create(sprite, (from + to) / 2f, rotation, new Vector3(offset.magnitude, width, 1f), color, duration, 1f, false, 4);
    }

    // A column of light standing on `feet`, `width` x `height` (a soft sprite 1 unit wide), behind the hero.
    public static void Column(Sprite sprite, Vector2 feet, float width, float height, Color color, float duration, bool unscaled = false)
    {
        Create(sprite, feet + Vector2.up * height / 2f, Quaternion.identity, new Vector3(width, height, 1f), color, duration, 1f, unscaled, 4);
    }

    static void Create(Sprite sprite, Vector2 position, Quaternion rotation, Vector3 scale, Color color, float duration,
        float startScale, bool unscaled, int sortingOrder)
    {
        GameObject go = new GameObject("Effect");
        go.transform.SetPositionAndRotation(position, rotation);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        Fx.Unlit(renderer); // bright in the dark cave

        FadeEffect effect = go.AddComponent<FadeEffect>();
        effect.sprite = renderer;
        effect.fullScale = scale;
        effect.duration = duration;
        effect.startAlpha = color.a;
        effect.startScale = startScale;
        effect.unscaled = unscaled;
        effect.Draw();
    }

    void Update()
    {
        age += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }
        Draw();
    }

    void Draw()
    {
        float t = age / duration;
        float grow = 1f - (1f - t) * (1f - t) * (1f - t); // ease out
        transform.localScale = fullScale * Mathf.Lerp(startScale, 1f, grow);
        Color color = sprite.color;
        color.a = startAlpha * (1f - t);
        sprite.color = color;
    }
}
