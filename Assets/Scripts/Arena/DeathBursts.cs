using UnityEngine;

// Enemies (and crates) pop into a few small pieces of their color that fly out, shrink and fade. Bosses burst into
// many bigger, slower pieces. The pieces are pooled sprite renderers (the oldest is reused when they run out) and
// freeze while the game is paused.
public class DeathBursts : MonoBehaviour
{
    const int MaxPieces = 300;

    [Header("Normal enemies")]
    public int pieces = 5;
    public float lifetime = 0.35f;
    public float speed = 4f;
    public float pieceSize = 0.15f;

    [Header("Bosses")]
    public int bossPieces = 20;
    public float bossLifetime = 0.8f;
    public float bossSpeed = 3f;
    public float bossPieceSize = 0.3f;

    [Header("Scene references")]
    [Tooltip("Plain square, tinted with the enemy's color.")]
    public Sprite pieceSprite;

    SpriteRenderer[] renderers;
    Vector2[] velocities;
    float[] ages, lifetimes, sizes;
    int next;

    void Awake()
    {
        renderers = new SpriteRenderer[MaxPieces];
        velocities = new Vector2[MaxPieces];
        ages = new float[MaxPieces];
        lifetimes = new float[MaxPieces];
        sizes = new float[MaxPieces];
        for (int i = 0; i < MaxPieces; i++)
        {
            SpriteRenderer piece = new GameObject("Piece").AddComponent<SpriteRenderer>();
            piece.transform.SetParent(transform, false);
            piece.sprite = pieceSprite;
            piece.sortingOrder = 2; // over enemies, under the hero
            piece.enabled = false;
            renderers[i] = piece;
        }
    }

    // `radius`: the enemy's size, so big enemies spread their pieces wider.
    public void Burst(Vector2 position, Color color, float radius, bool boss)
    {
        int count = boss ? bossPieces : pieces;
        for (int i = 0; i < count; i++)
        {
            int p = next;
            next = (next + 1) % MaxPieces;

            Vector2 direction = Random.insideUnitCircle.normalized;
            velocities[p] = direction * (boss ? bossSpeed : speed) * Random.Range(0.6f, 1.2f);
            ages[p] = 0f;
            lifetimes[p] = (boss ? bossLifetime : lifetime) * Random.Range(0.8f, 1.2f);
            sizes[p] = boss ? bossPieceSize : pieceSize;

            SpriteRenderer piece = renderers[p];
            piece.transform.position = position + direction * radius * 0.5f;
            piece.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            piece.transform.localScale = Vector3.one * sizes[p];
            piece.color = new Color(color.r, color.g, color.b, 1f);
            piece.enabled = true;
        }
    }

    // Hides every piece (a new depth, a panel).
    public void Clear()
    {
        foreach (SpriteRenderer piece in renderers) piece.enabled = false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused

        for (int p = 0; p < MaxPieces; p++)
        {
            SpriteRenderer piece = renderers[p];
            if (!piece.enabled) continue;

            ages[p] += dt;
            float t = ages[p] / lifetimes[p];
            if (t >= 1f)
            {
                piece.enabled = false;
                continue;
            }
            piece.transform.position += (Vector3)(velocities[p] * dt);
            velocities[p] *= 1f - 3f * dt; // slows down
            piece.transform.localScale = Vector3.one * sizes[p] * (1f - 0.5f * t);
            Color color = piece.color;
            color.a = 1f - t;
            piece.color = color;
        }
    }
}
