using UnityEngine;
using UnityEngine.UI;

// The town's backdrop coming alive (its art is baked by Tools/bake_town.py): the village's windows flicker a little (all
// together), the crystal light in the ground cracks slowly breathes, some stars twinkle, and the leaders' banners sway
// gently from their beam. On the backdrop image (the stars are made as its children). Unscaled time, so it keeps
// going under the popups.
public class TownScenery : MonoBehaviour
{
    const int StarCount = 35;
    const float StarTop = 0.5f;      // stars only in the top half of the backdrop (above the hills and the spire)
    const float CrackTime = 4f;      // seconds per breath of the cracks
    const float SwayAngle = 1.2f;    // degrees each way

    [Tooltip("The window lights, over the backdrop.")]
    public Image windows;
    [Tooltip("The cracks' crystal light, over the backdrop.")]
    public Image cracks;
    [Tooltip("The leaders' banners (pivot at the top, where they hang).")]
    public RectTransform[] banners;
    [Tooltip("A soft white dot (the twinkling stars).")]
    public Sprite star;

    Image[] stars;
    float[] starAlphas, starSpeeds, starPhases, swayTimes, swayPhases;

    void Start()
    {
        stars = new Image[StarCount];
        starAlphas = new float[StarCount];
        starSpeeds = new float[StarCount];
        starPhases = new float[StarCount];
        for (int i = 0; i < StarCount; i++)
        {
            Image dot = new GameObject("Star", typeof(RectTransform)).AddComponent<Image>();
            dot.transform.SetParent(transform, false);
            dot.sprite = star;
            dot.raycastTarget = false;
            RectTransform rect = dot.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(Random.value, 1f - Random.value * StarTop); // anywhere in the sky
            rect.sizeDelta = Vector2.one * Random.Range(5f, 11f);
            stars[i] = dot;
            starAlphas[i] = Random.Range(0.5f, 1f);
            starSpeeds[i] = Random.Range(1.5f, 4f); // radians per second
            starPhases[i] = Random.value * 10f;
        }
        swayTimes = new float[banners.Length];
        swayPhases = new float[banners.Length];
        for (int i = 0; i < banners.Length; i++)
        {
            swayTimes[i] = Random.Range(4.5f, 6.5f); // seconds per swing, so they don't move together
            swayPhases[i] = Random.value * 10f;
        }
    }

    void Update()
    {
        float time = Time.unscaledTime;
        SetAlpha(windows, 0.8f + 0.2f * Mathf.PerlinNoise(time * 2.5f, 0.3f));
        SetAlpha(cracks, 0.5f + 0.25f * (1f + Mathf.Sin(time * 2f * Mathf.PI / CrackTime)));
        for (int i = 0; i < stars.Length; i++)
            SetAlpha(stars[i], starAlphas[i] * (0.35f + 0.325f * (1f + Mathf.Sin(time * starSpeeds[i] + starPhases[i]))));
        for (int i = 0; i < banners.Length; i++)
            banners[i].localRotation = Quaternion.Euler(0f, 0f, SwayAngle * Mathf.Sin(time * 2f * Mathf.PI / swayTimes[i] + swayPhases[i]));
    }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
