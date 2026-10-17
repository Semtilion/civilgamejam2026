using UnityEngine;

// Small pooled pieces for quick effects: death bursts (enemies and crates pop into pieces of their color; bosses into
// many bigger, slower ones), spawn puffs (dust when an enemy appears), pickup sparkles, and tremor debris (pebbles and
// dust falling from the ceiling). Pieces fly, slow down (or fall), shrink and fade; the oldest is reused when they
// run out. They freeze while the game is paused.
public class DeathBursts : MonoBehaviour
{
    const int MaxPieces = 400;
    static readonly Color DustColor = new Color(0.55f, 0.50f, 0.45f, 0.7f);

    [Header("Normal enemies")]
    public int pieces = 6;
    public float lifetime = 0.4f;
    public float speed = 5f;
    public float pieceSize = 0.16f;

    [Header("Bosses")]
    public int bossPieces = 24;
    public float bossLifetime = 0.9f;
    public float bossSpeed = 3.5f;
    public float bossPieceSize = 0.32f;

    [Header("Scene references")]
    [Tooltip("Plain square, tinted per piece.")]
    public Sprite pieceSprite;
    [Tooltip("Unlit sprite material, so the pieces stay bright in the dark cave (empty = lit).")]
    public Material material;

    SpriteRenderer[] renderers;
    Vector2[] velocities;
    float[] ages, lifetimes, sizes, gravities, alphas;
    int next;

    void Awake()
    {
        renderers = new SpriteRenderer[MaxPieces];
        velocities = new Vector2[MaxPieces];
        ages = new float[MaxPieces];
        lifetimes = new float[MaxPieces];
        sizes = new float[MaxPieces];
        gravities = new float[MaxPieces];
        alphas = new float[MaxPieces];
        for (int i = 0; i < MaxPieces; i++)
        {
            SpriteRenderer piece = new GameObject("Piece").AddComponent<SpriteRenderer>();
            piece.transform.SetParent(transform, false);
            piece.sprite = pieceSprite;
            if (material != null) piece.sharedMaterial = material;
            piece.sortingOrder = 2; // over enemies, under the hero
            piece.enabled = false;
            renderers[i] = piece;
        }
    }

    // An enemy or crate dies. `radius`: its size, so big ones spread their pieces wider.
    public void Burst(Vector2 position, Color color, float radius, bool boss)
    {
        int count = boss ? bossPieces : pieces;
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            Emit(position + direction * radius * 0.5f, direction * (boss ? bossSpeed : speed) * Random.Range(0.6f, 1.3f),
                (boss ? bossLifetime : lifetime) * Random.Range(0.8f, 1.2f), boss ? bossPieceSize : pieceSize, color, 0f);
        }
    }

    // An enemy appears: a little dust ring at its feet.
    public void Puff(Vector2 position, float radius)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            Emit(position + direction * radius * 0.6f, direction * 1.5f, 0.35f, 0.18f + radius * 0.2f, DustColor, 0f);
        }
    }

    // A pickup is collected: a few bright specks of its color.
    public void Sparkle(Vector2 position, Color color)
    {
        for (int i = 0; i < 4; i++)
            Emit(position, Random.insideUnitCircle.normalized * 3f, 0.25f, 0.1f, Color.Lerp(color, Color.white, 0.4f), 0f);
    }

    // A tremor: pebbles and dust fall from the ceiling all over `view` (the camera's view, in world units).
    public void Debris(Rect view)
    {
        for (int i = 0; i < 45; i++)
        {
            Vector2 at = new Vector2(Random.Range(view.xMin, view.xMax), Random.Range(view.yMin, view.yMax));
            float shade = Random.Range(0.35f, 0.6f);
            Emit(at, new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-1f, -3f)), Random.Range(0.5f, 0.9f),
                Random.Range(0.08f, 0.18f), new Color(shade, shade * 0.92f, shade * 0.85f), -14f); // pebbles drop
        }
        for (int i = 0; i < 12; i++)
        {
            Vector2 at = new Vector2(Random.Range(view.xMin, view.xMax), Random.Range(view.yMin, view.yMax));
            Emit(at, new Vector2(0f, -0.6f), Random.Range(0.8f, 1.2f), Random.Range(0.5f, 0.9f), DustColor, 0f); // dust clouds
        }
    }

    void Emit(Vector2 position, Vector2 velocity, float life, float size, Color color, float gravity)
    {
        int p = next;
        next = (next + 1) % MaxPieces;
        velocities[p] = velocity;
        ages[p] = 0f;
        lifetimes[p] = life;
        sizes[p] = size;
        gravities[p] = gravity;
        alphas[p] = color.a;

        SpriteRenderer piece = renderers[p];
        piece.transform.position = position;
        piece.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        piece.transform.localScale = Vector3.one * size;
        piece.color = color;
        piece.enabled = true;
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
            if (gravities[p] != 0f) velocities[p].y += gravities[p] * dt; // falling debris
            else velocities[p] *= 1f - 3f * dt;                          // everything else slows down
            piece.transform.position += (Vector3)(velocities[p] * dt);
            piece.transform.localScale = Vector3.one * sizes[p] * (1f - 0.5f * t);
            Color color = piece.color;
            color.a = alphas[p] * (1f - t);
            piece.color = color;
        }
    }
}
