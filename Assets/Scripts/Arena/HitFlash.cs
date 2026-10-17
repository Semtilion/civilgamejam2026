using UnityEngine;

// The hit flash: a white silhouette of this object's sprite (the "Jam/Sprite Silhouette" material, 80% white), drawn
// just in front of it while `On` is true. Needed because tinting a sprite white leaves full-color art unchanged.
// Enemy turns it on for FlashTime after each hit (the Spitter's stem and flower each have one).
[RequireComponent(typeof(SpriteRenderer))]
public class HitFlash : MonoBehaviour
{
    const float Depth = -0.002f; // just in front of the sprite (the outline is behind it)

    public static Material Material; // set by ArenaManager from its scene reference; null = no flash

    SpriteRenderer source;
    SpriteRenderer overlay;

    void Awake()
    {
        source = GetComponent<SpriteRenderer>();
        overlay = new GameObject("Flash").AddComponent<SpriteRenderer>();
        overlay.transform.SetParent(transform, false);
        overlay.transform.localPosition = new Vector3(0f, 0f, Depth);
        overlay.sharedMaterial = Material;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder;
        overlay.enabled = false;
    }

    // Shows (copying the sprite and its flip, as a mirroring scale) or hides the silhouette.
    public bool On
    {
        set
        {
            bool show = value && Material != null && source.enabled && source.sprite != null;
            overlay.enabled = show;
            if (!show) return;
            overlay.sprite = source.sprite;
            overlay.transform.localScale = new Vector3(source.flipX ? -1f : 1f, source.flipY ? -1f : 1f, 1f);
        }
    }
}
