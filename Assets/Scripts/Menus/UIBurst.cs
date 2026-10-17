using UnityEngine;
using UnityEngine.UI;

// A burst of small spinning diamonds for the UI (a ParticleSystem doesn't draw on an overlay canvas, so these are
// pooled Images moved here). Burst(): they pop out of random points in this object's rect, away from its center and
// up, then arc down with gravity, shrink and fade (unscaled time). The town's leader window: a violet + gold burst
// around the portrait after a purchase.
public class UIBurst : MonoBehaviour
{
    const int PoolSize = 60;

    [Tooltip("Each piece (white, tinted by a random entry of colors). A square shows as a diamond.")]
    public Sprite sprite;
    public Color[] colors = { new Color(0.65f, 0.45f, 0.95f), new Color(1f, 0.80f, 0.30f) }; // violet, gold
    public int count = 24;
    [Tooltip("Pieces per burst fly out at up to this speed (UI units per second), plus the upward kick.")]
    public float speed = 500f;
    public float upward = 300f;
    public float gravity = 1400f;
    [Tooltip("Seconds (each piece lives 75-100% of this).")]
    public float lifetime = 0.8f;
    public float size = 14f;

    class Piece
    {
        public Image image;
        public Color color;
        public Vector2 velocity;
        public float age, life, spin, scale;
    }

    Piece[] pieces;
    int next; // ring buffer: the oldest piece gets reused

    public void Burst()
    {
        if (pieces == null) CreatePool();
        Rect area = ((RectTransform)transform).rect;
        for (int i = 0; i < count; i++)
        {
            Piece piece = pieces[next];
            next = (next + 1) % PoolSize;

            Vector2 start = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
            Vector2 outward = start.sqrMagnitude > 1f ? start.normalized : Random.insideUnitCircle.normalized;
            piece.velocity = outward * speed * Random.Range(0.5f, 1f) + Vector2.up * upward * Random.Range(0.5f, 1f);
            piece.color = colors[Random.Range(0, colors.Length)];
            piece.age = 0f;
            piece.life = lifetime * Random.Range(0.75f, 1f);
            piece.spin = Random.Range(-540f, 540f);
            piece.scale = Random.Range(0.6f, 1.2f);
            piece.image.rectTransform.anchoredPosition = start;
            piece.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f); // a square stands as a diamond
            piece.image.enabled = true;
            Draw(piece);
        }
    }

    void CreatePool()
    {
        pieces = new Piece[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject go = new GameObject("Piece", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false; // never blocks clicks
            image.enabled = false;
            pieces[i] = new Piece { image = image, life = 0f };
        }
    }

    void OnDisable()
    {
        if (pieces == null) return;
        foreach (Piece piece in pieces) piece.image.enabled = false; // a closed window starts clean next time
    }

    void Update()
    {
        if (pieces == null) return;
        float dt = Time.unscaledDeltaTime;
        foreach (Piece piece in pieces)
        {
            if (!piece.image.enabled) continue;
            piece.age += dt;
            if (piece.age >= piece.life)
            {
                piece.image.enabled = false;
                continue;
            }
            piece.velocity.y -= gravity * dt;
            RectTransform rect = piece.image.rectTransform;
            rect.anchoredPosition += piece.velocity * dt;
            rect.localRotation *= Quaternion.Euler(0f, 0f, piece.spin * dt);
            Draw(piece);
        }
    }

    // Shrinks to half and fades out over the second half of its life.
    void Draw(Piece piece)
    {
        float t = piece.age / piece.life;
        piece.image.rectTransform.sizeDelta = Vector2.one * size * piece.scale * (1f - 0.5f * t);
        Color color = piece.color;
        color.a = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
        piece.image.color = color;
    }
}
