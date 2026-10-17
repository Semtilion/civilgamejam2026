using UnityEngine;
using UnityEngine.Rendering.Universal;

// Makes a torch light flicker: its brightness and size wander a little (noise), like a flame.
public class Flicker : MonoBehaviour
{
    public Light2D torchLight;
    [Tooltip("How fast the flame changes.")]
    public float speed = 8f;

    float baseIntensity;
    float baseRadius;
    float seed;

    void Start()
    {
        baseIntensity = torchLight.intensity;
        baseRadius = torchLight.pointLightOuterRadius;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * speed, seed); // 0..1
        torchLight.intensity = baseIntensity * (0.75f + 0.45f * noise);
        torchLight.pointLightOuterRadius = baseRadius * (0.93f + 0.14f * noise);
    }
}
