using UnityEngine;
using UnityEngine.Rendering.Universal;

// Makes a torch flicker: its light's brightness and size wander a little (noise), like a flame, and the flame art
// (frames, if any) changes.
public class Flicker : MonoBehaviour
{
    public Light2D torchLight;
    [Tooltip("How fast the flame changes.")]
    public float speed = 8f;
    [Tooltip("Optional: the torch's flame frames, shown in turn on this object's sprite.")]
    public Sprite[] frames;
    [Tooltip("Flame frames per second.")]
    public float frameRate = 6f;

    SpriteRenderer sprite;
    float baseIntensity;
    float baseRadius;
    float seed;

    void Start()
    {
        sprite = GetComponent<SpriteRenderer>();
        baseIntensity = torchLight.intensity;
        baseRadius = torchLight.pointLightOuterRadius;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * speed, seed); // 0..1
        torchLight.intensity = baseIntensity * (0.75f + 0.45f * noise);
        torchLight.pointLightOuterRadius = baseRadius * (0.93f + 0.14f * noise);
        if (frames != null && frames.Length > 0) sprite.sprite = frames[(int)(Time.time * frameRate + seed) % frames.Length];
    }
}
