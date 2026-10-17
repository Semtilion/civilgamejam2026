using UnityEngine;

// The leader portrait's motion, around its pivot at the bottom (unscaled time):
// - a slow, gentle breath: up to `breath` taller every `period` seconds, from rest each time it's shown;
// - Hop() (after a purchase): crouch, jump up with a stretch, land with a smaller squash.
// The portrait hangs a little below its frame (clipped), so the nameplate still hides its uneven bottom mid-hop.
public class PortraitMotion : MonoBehaviour
{
    const float HopTime = 0.45f;

    [Tooltip("Seconds per breath.")]
    public float period = 4f;
    [Tooltip("How much taller at the top of a breath (0.015 = 1.5%). The width changes by a third of that.")]
    public float breath = 0.015f;
    [Tooltip("How high the hop goes (UI units).")]
    public float hopHeight = 22f;

    RectTransform rect;
    Vector2 restPosition;
    float time;
    float hopAge = HopTime; // HopTime = not hopping

    void Awake()
    {
        rect = (RectTransform)transform;
        restPosition = rect.anchoredPosition;
    }

    void OnEnable()
    {
        time = 0f;
        hopAge = HopTime;
        Apply();
    }

    public void Hop()
    {
        hopAge = 0f;
    }

    void Update()
    {
        time += Time.unscaledDeltaTime;
        hopAge += Time.unscaledDeltaTime;
        Apply();
    }

    void Apply()
    {
        float breathNow = breath * 0.5f * (1f - Mathf.Cos(time * 2f * Mathf.PI / period)); // 0 -> breath -> 0
        float stretch = 0f, height = 0f; // stretch > 0: taller and thinner, < 0: squashed
        if (hopAge < HopTime)
        {
            float t = hopAge / HopTime;
            if (t < 0.2f)
            {
                stretch = -0.075f * Mathf.Sin(Mathf.PI * t / 0.2f); // crouch
            }
            else if (t < 0.7f)
            {
                float air = Mathf.Sin(Mathf.PI * (t - 0.2f) / 0.5f); // up and down
                stretch = 0.045f * air;
                height = hopHeight * air;
            }
            else
            {
                stretch = -0.045f * Mathf.Sin(Mathf.PI * (t - 0.7f) / 0.3f); // landing
            }
        }
        rect.localScale = new Vector3((1f + breathNow / 3f) * (1f - stretch * 0.6f), (1f + breathNow) * (1f + stretch), 1f);
        rect.anchoredPosition = restPosition + new Vector2(0f, height);
    }
}
