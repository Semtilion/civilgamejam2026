using UnityEngine;

// Starting weapon: every `interval` seconds, hits every enemy within `radius` of the hero and knocks them back.
public class SwordSweep : MonoBehaviour
{
    const float FlashTime = 0.15f;
    const float FlashAlpha = 0.35f;

    public int damage = 10;
    public float interval = 1f;
    public float radius = 2.2f;
    public float knockback = 6f;

    [Header("Scene references")]
    [Tooltip("Circle that flashes on each sweep (placeholder visual).")]
    public SpriteRenderer sweepVisual;

    float timer;

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = interval;
            Sweep();
        }

        float sinceSweep = interval - timer;
        Color color = sweepVisual.color;
        color.a = Mathf.Clamp01(1f - sinceSweep / FlashTime) * FlashAlpha;
        sweepVisual.color = color;
        sweepVisual.transform.localScale = Vector3.one * radius * 2f; // placeholder circle is 1 unit wide
    }

    void Sweep()
    {
        Vector2 center = transform.position;
        // Killed enemies are only destroyed at the end of the frame, so the list doesn't change during this loop.
        foreach (Enemy enemy in Enemy.All)
        {
            float reach = radius + enemy.data.radius;
            if (((Vector2)enemy.transform.position - center).sqrMagnitude <= reach * reach)
                enemy.TakeDamage(damage, center, knockback);
        }
    }
}
