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

    // 0 -> 1 with a little overshoot at the end (pops).
    public static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t) - 1f;
        return 1f + t * t * (2.7f * t + 1.7f);
    }
}
