using System.Collections.Generic;
using UnityEngine;

// A tremor's start, where the ore was mined (ArenaManager.Tremor): rubble bursts out in the ore's color, three rings
// ripple out across the floor, and dark cracks spread from the spot, then fade. Scaled time, so it freezes while the
// game is paused.
public class TremorEffect : MonoBehaviour
{
    public static readonly List<TremorEffect> All = new List<TremorEffect>(); // cleared with the field

    const int Rings = 3;
    const float RingGap = 0.18f, RingSpeed = 18f, RingLife = 0.9f; // seconds between rings, units per second, seconds
    const float RingAlpha = 0.45f;
    const float CrackGrow = 0.35f, CrackFadeStart = 1f, CrackFadeTime = 0.5f;
    const float CrackReach = 5.5f;                   // units from the spot
    public const float PatternReach = 480f / 512f;  // the crack images: how far out the cracks go (a share of the half-width)
    static readonly Color RingColor = new Color(0.9f, 0.82f, 0.7f);

    SpriteRenderer[] rings;
    SpriteRenderer cracks;
    float age;

    // `crackPattern` / `ripple`: images 1 unit wide (either may be null).
    public static void Spawn(Vector2 at, Sprite crackPattern, Sprite ripple, Color rubbleColor, DeathBursts bursts)
    {
        TremorEffect effect = new GameObject("Tremor").AddComponent<TremorEffect>();
        effect.transform.position = at;
        All.Add(effect);
        bursts.Burst(at, rubbleColor, 0.6f, false); // rubble
        bursts.Burst(at, rubbleColor, 0.6f, false);
        if (crackPattern != null)
        {
            effect.cracks = effect.Layer("Cracks", crackPattern);
            effect.cracks.transform.localRotation = Quaternion.Euler(0f, 0f, Random.value * 360f);
        }
        effect.rings = new SpriteRenderer[ripple != null ? Rings : 0];
        for (int i = 0; i < effect.rings.Length; i++)
        {
            effect.rings[i] = effect.Layer("Ripple", ripple);
            Fx.Unlit(effect.rings[i]);
        }
        effect.Draw();
    }

    SpriteRenderer Layer(string name, Sprite sprite)
    {
        SpriteRenderer layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(transform, false);
        layer.sprite = sprite;
        layer.sortingOrder = -1; // on the floor, under everyone
        return layer;
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= CrackFadeStart + CrackFadeTime)
        {
            Destroy(gameObject);
            return;
        }
        Draw();
    }

    void Draw()
    {
        for (int i = 0; i < rings.Length; i++)
        {
            float ringAge = age - i * RingGap;
            float alpha = ringAge > 0f ? RingAlpha * Mathf.Clamp01(1f - ringAge / RingLife) : 0f;
            rings[i].color = new Color(RingColor.r, RingColor.g, RingColor.b, alpha);
            rings[i].transform.localScale = Vector3.one * Mathf.Max(0.01f, ringAge * RingSpeed * 2f);
        }
        if (cracks == null) return;
        float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / CrackGrow), 3f);
        cracks.transform.localScale = Vector3.one * (CrackReach * 2f / PatternReach) * Mathf.Lerp(0.2f, 1f, grow);
        cracks.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((age - CrackFadeStart) / CrackFadeTime));
    }
}
