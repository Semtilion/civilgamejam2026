using UnityEngine;

// A placeholder flash that fades out and disappears (explosions, lightning, the Second Wind burst).
// Uses scaled time, so it freezes while the game is paused.
public class FadeEffect : MonoBehaviour
{
    SpriteRenderer sprite;
    float duration;
    float startAlpha;
    float age;

    // A circle of `radius` at `position` (placeholder circle sprite, 1 unit wide).
    public static void Circle(Sprite sprite, Vector2 position, float radius, Color color, float duration)
    {
        Create(sprite, position, Quaternion.identity, Vector3.one * radius * 2f, color, duration);
    }

    // A bar from `from` to `to`, `width` thick (placeholder square sprite, 1 unit wide).
    public static void Line(Sprite sprite, Vector2 from, Vector2 to, float width, Color color, float duration)
    {
        Vector2 offset = to - from;
        Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
        Create(sprite, (from + to) / 2f, rotation, new Vector3(offset.magnitude, width, 1f), color, duration);
    }

    static void Create(Sprite sprite, Vector2 position, Quaternion rotation, Vector3 scale, Color color, float duration)
    {
        GameObject go = new GameObject("Effect");
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 4;

        FadeEffect effect = go.AddComponent<FadeEffect>();
        effect.sprite = renderer;
        effect.duration = duration;
        effect.startAlpha = color.a;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }
        Color color = sprite.color;
        color.a = startAlpha * (1f - age / duration);
        sprite.color = color;
    }
}
