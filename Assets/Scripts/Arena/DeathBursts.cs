using UnityEngine;

// Small pooled pieces for quick effects: death bursts (enemies and crates pop into pieces of their color; bosses into
// many bigger, slower ones; a tremor's rubble, a landed rock's pieces), dust puffs (an enemy appearing, in its color;
// ground dust), pickup sparkles, and hit sparks. Pieces fly, slow down (or fall), shrink and fade; the oldest is reused
// when they run out. They freeze while the game is paused.
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

    [Header("Impact sparks (every hit)")]
    public float sparkSize = 0.7f;
    public float sparkTime = 0.12f;

    [Header("Scene references")]
    [Tooltip("Plain circle, tinted per piece.")]
    public Sprite pieceSprite;
    [Tooltip("The impact spark (a 4-point star).")]
    public Sprite sparkSprite;
    [Tooltip("Unlit sprite material, so the pieces stay bright in the dark cave (empty = lit).")]
    public Material material;

    const int MaxSparks = 60;
    SpriteRenderer[] sparks;
    readonly float[] sparkAges = new float[MaxSparks];
    int nextSpark;

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

        sparks = new SpriteRenderer[MaxSparks];
        for (int i = 0; i < MaxSparks; i++)
        {
            SpriteRenderer spark = new GameObject("Spark").AddComponent<SpriteRenderer>();
            spark.transform.SetParent(transform, false);
            spark.sprite = sparkSprite;
            if (material != null) spark.sharedMaterial = material;
            spark.sortingOrder = 4; // over enemies and weapon flashes
            spark.enabled = false;
            sparks[i] = spark;
        }
    }

    // An enemy is hit: a quick star in its color at `position` that pops up and fades (sparkTime).
    public void Spark(Vector2 position, Color color)
    {
        if (sparkSprite == null) return;
        SpriteRenderer spark = sparks[nextSpark];
        sparkAges[nextSpark] = 0f;
        nextSpark = (nextSpark + 1) % MaxSparks;
        spark.transform.position = position;
        spark.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        spark.transform.localScale = Vector3.one * sparkSize * 0.5f;
        spark.color = color;
        spark.enabled = true;
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

    // A little dust ring: ground dust (the hammer's cracks), or in an enemy's color as it appears.
    public void Puff(Vector2 position, float radius)
    {
        Puff(position, radius, DustColor);
    }

    public void Puff(Vector2 position, float radius, Color color)
    {
        color.a = DustColor.a;
        for (int i = 0; i < 4; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            Emit(position + direction * radius * 0.6f, direction * 1.5f, 0.35f, 0.18f + radius * 0.2f, color, 0f);
        }
    }

    // A speck of light drifting up and fading (the teleporter, Second Wind).
    public void Rise(Vector2 position, Color color)
    {
        Emit(position, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.8f, 1.4f)), Random.Range(0.6f, 1f), 0.09f, color, 0f);
    }

    // A pickup is collected: a few bright specks of its color.
    public void Sparkle(Vector2 position, Color color)
    {
        for (int i = 0; i < 4; i++)
            Emit(position, Random.insideUnitCircle.normalized * 3f, 0.25f, 0.1f, Color.Lerp(color, Color.white, 0.4f), 0f);
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
        foreach (SpriteRenderer spark in sparks) spark.enabled = false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused

        for (int s = 0; s < MaxSparks; s++)
        {
            SpriteRenderer spark = sparks[s];
            if (!spark.enabled) continue;
            sparkAges[s] += dt;
            float t = sparkAges[s] / sparkTime;
            if (t >= 1f)
            {
                spark.enabled = false;
                continue;
            }
            spark.transform.localScale = Vector3.one * sparkSize * (0.5f + 0.5f * Mathf.Sqrt(t)); // pops open
            spark.color = new Color(1f, 1f, 1f, 1f - t * t);                                     // then fades
        }

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
