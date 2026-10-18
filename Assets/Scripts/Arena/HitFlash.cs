using System.Collections.Generic;
using UnityEngine;

// The hit flash: a silhouette of this object's sprite (the "Jam/Sprite Silhouette" material, 80% white unless Color
// is set), drawn just in front of it while `On` is true. Needed because tinting a sprite white leaves full-color art
// unchanged. Enemy turns it on for FlashTime after each hit (the Spitter's stem and flower each have one); the hero's
// flashes red when hit and takes other colors for his moments (Player).
[RequireComponent(typeof(SpriteRenderer))]
public class HitFlash : MonoBehaviour
{
    const float Depth = -0.002f; // just in front of the sprite (the outline is behind it)

    public static Material Material; // set by ArenaManager from its scene reference; null = no flash
    static readonly Dictionary<Color, Material> ColoredMaterials = new Dictionary<Color, Material>(); // one per color, made once

    SpriteRenderer source;
    SpriteRenderer overlay;
    Material material; // null = Material (white)

    // The flash material in `color` (its alpha too): a copy of Material, one per color. Null without Material.
    public static Material Colored(Color color)
    {
        if (Material == null) return null;
        if (!ColoredMaterials.TryGetValue(color, out Material colored))
        {
            colored = new Material(Material) { color = color };
            ColoredMaterials[color] = colored;
        }
        return colored;
    }

    void Awake()
    {
        source = GetComponent<SpriteRenderer>();
        overlay = new GameObject("Flash").AddComponent<SpriteRenderer>();
        overlay.transform.SetParent(transform, false);
        overlay.transform.localPosition = new Vector3(0f, 0f, Depth);
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder;
        overlay.enabled = false;
    }

    // The silhouette's color from now on (alpha included).
    public Color Color
    {
        set => material = Colored(value);
    }

    // Shows (copying the sprite and its flip, as a mirroring scale) or hides the silhouette.
    public bool On
    {
        set
        {
            bool show = value && Material != null && source.enabled && source.sprite != null;
            overlay.enabled = show;
            if (!show) return;
            overlay.sharedMaterial = material != null ? material : Material; // (set here: Material may come after Awake)
            overlay.sprite = source.sprite;
            overlay.transform.localScale = new Vector3(source.flipX ? -1f : 1f, source.flipY ? -1f : 1f, 1f);
        }
    }
}
