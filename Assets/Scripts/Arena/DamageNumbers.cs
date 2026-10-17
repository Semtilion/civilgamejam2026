using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Damage numbers that pop up over hit enemies, float up and fade. Weapon hits are white (25+ big and yellow), relic
// effect damage (explosions, lightning) is orange. Lives on its own screen canvas below the HUD; the numbers are pooled
// (the oldest is reused when they run out) and freeze while the game is paused.
public class DamageNumbers : MonoBehaviour
{
    const int MaxNumbers = 80;
    const float Lifetime = 0.6f;
    const float RiseSpeed = 1.5f; // world units per second
    const int BigHit = 25;
    static readonly Color NormalColor = Color.white;
    static readonly Color BigColor = new Color(1f, 0.85f, 0.2f);
    static readonly Color EffectColor = new Color(1f, 0.55f, 0.2f);

    public int fontSize = 26;
    public int bigFontSize = 38;

    [Header("Scene references")]
    public Camera cam;
    public Font font;

    class Number
    {
        public Text text;
        public Vector2 world;
        public float age = Lifetime; // done
    }

    readonly List<Number> numbers = new List<Number>();
    int next; // the oldest, reused once all MaxNumbers exist

    public void Show(Vector2 position, int amount, bool weaponHit)
    {
        Number number = Take();
        bool big = amount >= BigHit;
        number.world = position + Random.insideUnitCircle * 0.25f;
        number.age = 0f;
        number.text.text = amount.ToString();
        number.text.fontSize = big ? bigFontSize : fontSize;
        number.text.color = !weaponHit ? EffectColor : big ? BigColor : NormalColor;
        number.text.enabled = true;
        Place(number);
    }

    // Hides every number (a new depth, a panel).
    public void Clear()
    {
        foreach (Number number in numbers)
        {
            number.age = Lifetime;
            number.text.enabled = false;
        }
    }

    Number Take()
    {
        if (numbers.Count < MaxNumbers)
        {
            Text text = new GameObject("Number", typeof(RectTransform)).AddComponent<Text>();
            text.rectTransform.SetParent(transform, false);
            text.rectTransform.sizeDelta = new Vector2(200f, 60f);
            text.font = font;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            Number created = new Number { text = text };
            numbers.Add(created);
            return created;
        }
        Number oldest = numbers[next];
        next = (next + 1) % numbers.Count;
        return oldest;
    }

    void LateUpdate()
    {
        foreach (Number number in numbers)
        {
            if (number.age >= Lifetime) continue;
            number.age += Time.deltaTime; // 0 while paused
            if (number.age >= Lifetime)
            {
                number.text.enabled = false;
                continue;
            }
            number.world.y += RiseSpeed * Time.deltaTime;
            Place(number);

            Color color = number.text.color;
            color.a = Mathf.Clamp01(2f - 2f * number.age / Lifetime); // solid for the first half, then fades
            number.text.color = color;
        }
    }

    // Overlay canvas: a UI element's position is in screen pixels.
    void Place(Number number)
    {
        number.text.rectTransform.position = cam.WorldToScreenPoint(number.world);
    }
}
