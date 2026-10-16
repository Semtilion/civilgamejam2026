using System.Collections.Generic;
using UnityEngine;

// One rock of the rock rain trap: a shadow grows on the floor for `warning` seconds, then the rock lands and hurts
// the hero if they're under it (enemies are never hurt). The landed rock stays a moment, then disappears.
public class FallingRock : MonoBehaviour
{
    public static readonly List<FallingRock> All = new List<FallingRock>();

    const float LandedTime = 0.3f;
    static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color RockColor = new Color(0.50f, 0.45f, 0.40f);

    SpriteRenderer sprite;
    Player player;
    float radius;
    float warning;
    int damage;
    float age;
    bool landed;

    public static void Drop(Sprite sprite, Vector2 position, float radius, float warning, int damage, Player player)
    {
        GameObject go = new GameObject("Falling Rock");
        go.transform.position = position;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = ShadowColor;
        renderer.sortingOrder = -1; // on the floor, under everyone

        FallingRock rock = go.AddComponent<FallingRock>();
        rock.sprite = renderer;
        rock.player = player;
        rock.radius = radius;
        rock.warning = warning;
        rock.damage = damage;
        rock.UpdateShadow();
        All.Add(rock);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (!player.arena.IsPlaying) return;

        age += Time.deltaTime;
        if (!landed)
        {
            UpdateShadow();
            if (age >= warning) Land();
        }
        else if (age >= warning + LandedTime)
        {
            Destroy(gameObject);
        }
    }

    // The shadow grows from a third of the rock's size as it comes down (placeholder circle is 1 unit wide).
    void UpdateShadow()
    {
        transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.33f, 1f, age / warning);
    }

    void Land()
    {
        landed = true;
        transform.localScale = Vector3.one * radius * 2f;
        sprite.color = RockColor;
        sprite.sortingOrder = 6; // on top of the hero

        float reach = radius + player.radius;
        if (((Vector2)(player.transform.position - transform.position)).sqrMagnitude < reach * reach)
            player.TakeDamage(damage);
    }
}
