using UnityEngine;

// A black outline around this object's sprite: 8 black copies of it, pushed `Width` world units out in 8 directions
// and drawn just behind it (same sorting order, a little further from the camera). They follow the sprite, flip,
// visibility and alpha, and keep the width the same in the world however the sprite is scaled (squash, grow-in).
// The copies are only touched when something changed (there can be hundreds of these).
// On the hero, enemies (and a Spitter's flower), ores, the teleporter stairs and pickups.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutline : MonoBehaviour
{
    const float Width = 0.05f;   // world units
    const float Depth = 0.005f;  // behind the sprite (world units along the camera's view)
    static readonly Vector2[] Directions =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f), new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
    };

    SpriteRenderer source;
    readonly SpriteRenderer[] copies = new SpriteRenderer[8];
    // What the copies show now
    bool shown = true;
    Sprite sprite;
    bool flipX, flipY;
    float alpha = -1f;
    Vector3 scale;

    void Awake()
    {
        source = GetComponent<SpriteRenderer>();
        for (int i = 0; i < copies.Length; i++)
        {
            copies[i] = new GameObject("Outline").AddComponent<SpriteRenderer>();
            copies[i].transform.SetParent(transform, false);
            copies[i].sharedMaterial = source.sharedMaterial;
            copies[i].sortingLayerID = source.sortingLayerID;
            copies[i].sortingOrder = source.sortingOrder;
        }
        LateUpdate();
    }

    void LateUpdate()
    {
        Vector3 newScale = transform.lossyScale;
        bool show = source.enabled && source.sprite != null && Mathf.Abs(newScale.x) > 0.0001f && Mathf.Abs(newScale.y) > 0.0001f;
        if (show != shown)
        {
            shown = show;
            foreach (SpriteRenderer copy in copies) copy.enabled = show;
        }
        if (!show) return;

        float newAlpha = source.color.a;
        if (source.sprite != sprite || source.flipX != flipX || source.flipY != flipY || newAlpha != alpha)
        {
            sprite = source.sprite;
            flipX = source.flipX;
            flipY = source.flipY;
            alpha = newAlpha;
            foreach (SpriteRenderer copy in copies)
            {
                copy.sprite = sprite;
                copy.flipX = flipX;
                copy.flipY = flipY;
                copy.color = new Color(0f, 0f, 0f, alpha);
            }
        }

        if (newScale != scale)
        {
            scale = newScale;
            float depth = Mathf.Abs(scale.z) > 0.0001f ? Depth / scale.z : Depth;
            for (int i = 0; i < copies.Length; i++)
                copies[i].transform.localPosition = new Vector3(Directions[i].x * Width / scale.x, Directions[i].y * Width / scale.y, depth);
        }
    }
}
