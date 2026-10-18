using UnityEngine;
using UnityEngine.UI;

// Rain over the town's village (in front of the backdrop and banners, behind the buttons and boxes): layers of a tiling
// streak texture (Assets/Art/UI/Town/Rain.png, Tools/bake_rain.py) sliding down and a little left, the near ones
// bigger and faster. A drizzle that gets heavier as the next quake nears: the far layer always, the middle one from two
// days before, the near one the day before. Unscaled time.
public class TownRain : MonoBehaviour
{
    const float Slant = 0.18f;        // sideways per fall (the streaks are drawn at the same slant)
    const float LayerAlpha = 0.3f;
    static readonly float[] Scales = { 0.7f, 1f, 1.4f };       // far to near: the texture's size on screen
    static readonly float[] FallSpeeds = { 1.2f, 1.3f, 1.4f }; // tiles per second (on screen: x 512 x scale px)

    [Tooltip("Far to near (made by the town builder).")]
    public RawImage[] layers;

    void Start()
    {
        Campaign campaign = Campaign.Current;
        int interval = campaign != null ? campaign.config.quakeEvery : 3;
        int daysLeft = campaign != null ? campaign.NextQuakeDay - campaign.day : interval;
        float heavy = interval > 1 ? Mathf.Clamp01((float)(interval - daysLeft) / (interval - 1)) : 1f; // 0 drizzle, 1 the day before

        for (int i = 0; i < layers.Length; i++)
        {
            RawImage layer = layers[i];
            float shown = i == 0 ? 1f : Mathf.Clamp01(heavy * (layers.Length - 1) - (i - 1));
            layer.color = new Color(1f, 1f, 1f, LayerAlpha * shown);
            layer.enabled = shown > 0f;
            Vector2 tiles = layer.rectTransform.rect.size / (layer.texture.width * Scales[i]);
            layer.uvRect = new Rect(Random.value, Random.value, tiles.x, tiles.y); // each layer starts somewhere else
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < layers.Length; i++)
        {
            if (!layers[i].enabled) continue;
            Rect uv = layers[i].uvRect;
            uv.y = Mathf.Repeat(uv.y + FallSpeeds[i] * dt, 1f);         // the streaks move down...
            uv.x = Mathf.Repeat(uv.x + FallSpeeds[i] * Slant * dt, 1f); // ...and a little left
            layers[i].uvRect = uv;
        }
    }
}
