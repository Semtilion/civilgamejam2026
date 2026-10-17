using UnityEngine;

// Shared bits for visual effects. The cave is dark (2D lights), and sprites are lit by default; effects, shots and
// pickups use the unlit sprite material instead so they stay bright and readable. ArenaManager sets it at startup.
public static class Fx
{
    public static Material UnlitMaterial; // null = keep the default (lit) material

    public static void Unlit(SpriteRenderer renderer)
    {
        if (UnlitMaterial != null) renderer.sharedMaterial = UnlitMaterial;
    }

    // A flame's gentle squash and stretch instead of turning (fire patches): `size` across, 7% wider and shorter, then
    // narrower and taller, about once a second. `time`: seconds plus a random offset per flame.
    public static Vector3 FlameScale(float size, float time)
    {
        float wobble = 0.07f * Mathf.Sin(time * 6f);
        return new Vector3(size * (1f + wobble), size * (1f - wobble), 1f);
    }

    // 0 -> 1 with a little overshoot at the end (pops).
    public static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t) - 1f;
        return 1f + t * t * (2.7f * t + 1.7f);
    }
}
