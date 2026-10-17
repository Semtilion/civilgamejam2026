using UnityEngine;

// A number on screen that rolls toward its real value instead of jumping (gold, materials, crystals). Big changes
// roll faster, so every change takes about the same short time.
public class Ticker
{
    float shown;
    bool started;

    // `start`: where it rolls from the first time (e.g. the amount before the dive); otherwise it starts at the value.
    public Ticker(int? start = null)
    {
        if (start.HasValue)
        {
            shown = start.Value;
            started = true;
        }
    }

    // Call every frame: moves toward `target`, returns what to show.
    public int Next(int target, float deltaTime)
    {
        if (!started)
        {
            shown = target;
            started = true;
        }
        float speed = Mathf.Max(Mathf.Abs(target - shown) * 5f, 20f); // per second
        shown = Mathf.MoveTowards(shown, target, speed * deltaTime);
        return Mathf.RoundToInt(shown);
    }
}
