using UnityEngine;

// The Quake Hammer's slam on the floor: glowing cracks shoot out from where it hit to its reach (one of the baked crack
// patterns, Tools/bake_cracks.py, turned at random), then fade, and dust bursts up where each crack ends. Stays where
// it was made (not on the hero). Scaled time, so it freezes while the game is paused.
public class GroundCracks : MonoBehaviour
{
    const int Cracks = 9;              // the patterns' cracks end at 0, 40, 80 ... degrees...
    const float Reach = 240f / 256f;   // ...this far out (a share of the image's half-width)
    const float GrowTime = 0.1f;       // the cracks shoot out
    const float FadeStart = 0.12f, FadeTime = 0.4f;
    const float DustSize = 0.6f;       // DeathBursts.Puff's radius

    SpriteRenderer sprite;
    DeathBursts bursts;
    float radius, age;
    bool dusted;

    // `pattern`: one of the crack images (1 unit wide); `radius`: the slam's reach.
    public static void Spawn(Sprite pattern, Vector2 at, float radius, DeathBursts bursts)
    {
        GameObject go = new GameObject("Ground Cracks");
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, Random.value * 360f));
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = pattern;
        renderer.sortingOrder = -1; // on the floor, under everyone
        Fx.Unlit(renderer);         // the glow stays bright in the dark cave

        GroundCracks cracks = go.AddComponent<GroundCracks>();
        cracks.sprite = renderer;
        cracks.bursts = bursts;
        cracks.radius = radius;
        cracks.Draw();
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= FadeStart + FadeTime)
        {
            Destroy(gameObject);
            return;
        }
        if (!dusted && age >= GrowTime) Dust();
        Draw();
    }

    // Grows from a fifth of its size out to the reach (easing out), then fades.
    void Draw()
    {
        float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / GrowTime), 3f);
        transform.localScale = Vector3.one * (radius * 2f / Reach) * Mathf.Lerp(0.2f, 1f, grow);
        sprite.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((age - FadeStart) / FadeTime));
    }

    // Dust where each crack ends, once they've reached the end.
    void Dust()
    {
        dusted = true;
        for (int i = 0; i < Cracks; i++)
        {
            float angle = (transform.eulerAngles.z + i * 360f / Cracks) * Mathf.Deg2Rad;
            bursts.Puff((Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, DustSize);
        }
    }
}
