using UnityEngine;
using UnityEngine.Rendering.Universal;

// A soft colored light on an object that slowly pulses (crystals, crystal clusters, shrines, ores, the teleporter,
// fire patches). The light is a child, so it goes away with the object. A point light's size doesn't follow its
// parent's scale, so it works on scaled objects too.
public class Glow : MonoBehaviour
{
    [Tooltip("Times the light's strength (an ore glows brighter while it's being mined).")]
    public float boost = 1f;

    Light2D glowLight;
    float baseIntensity;
    float phase;

    public static void Add(GameObject target, Color color, float radius, float intensity = 0.9f)
    {
        Light2D light = new GameObject("Glow Light").AddComponent<Light2D>(); // a point light on every sorting layer
        light.transform.SetParent(target.transform, false);
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = radius * 0.15f;
        light.intensity = intensity;
        light.falloffIntensity = 0.7f;

        Glow glow = target.AddComponent<Glow>();
        glow.glowLight = light;
        glow.baseIntensity = intensity;
        glow.phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        glowLight.intensity = baseIntensity * boost * (0.7f + 0.3f * Mathf.Sin(Time.time * 3f + phase)); // freezes when paused
    }
}
