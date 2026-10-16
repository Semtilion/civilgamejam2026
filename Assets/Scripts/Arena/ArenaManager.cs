using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[System.Serializable]
public class SpawnEntry
{
    public EnemyData enemy;
    [Min(0f)] public float weight = 1f;
}

// Runs a dungeon dive (Vampire Survivors style): survive the timer on each depth, then a boss appears while the
// horde keeps coming. Kill it, then go deeper or return to town. The boss on `forceDepth` is the force itself.
public class ArenaManager : MonoBehaviour
{
    public const float WallThickness = 1f;

    [Header("Arena")]
    public Vector2 arenaSize = new Vector2(64f, 40f);

    [Header("Depth")]
    public float depthTime = 90f;
    public int forceDepth = 5;
    [Tooltip("Enemy HP/damage multiplier added per depth below the first (0.3 = +30% each depth).")]
    public float statScalePerDepth = 0.3f;
    [Tooltip("Spawn speed multiplier added per depth below the first.")]
    public float spawnScalePerDepth = 0.2f;

    [Header("Spawning")]
    public SpawnEntry[] spawnTable;
    [Tooltip("Seconds between spawns at the start of a depth...")]
    public float spawnIntervalStart = 1f;
    [Tooltip("...and when the timer runs out (also used during the boss).")]
    public float spawnIntervalEnd = 0.25f;
    public int maxEnemies = 250;
    public EnemyData boss;
    public EnemyData forceBoss;

    [Header("Scene references")]
    public Player player;
    public Camera cam;
    public RectTransform hpFill;
    public Text hpText;
    public Text depthText;
    public Text timerText;
    public GameObject bossBar;
    public RectTransform bossFill;
    public Text bossName;
    public GameObject panel;
    public Text panelTitle;
    public Text panelBody;
    public Button primaryButton;
    public Button secondaryButton;

    public Rect Bounds { get; private set; }
    public bool IsPlaying => !panel.activeSelf;

    int depth;
    int kills;
    float timeLeft;
    float spawnTimer;
    bool bossSpawned;
    Enemy currentBoss;

    float StatScale => 1f + statScalePerDepth * (depth - 1);

    void Awake()
    {
        Bounds = new Rect(-arenaSize / 2f, arenaSize);
    }

    void Start()
    {
        StartRun();
    }

    // ---------- Run flow ----------

    void StartRun()
    {
        player.ResetForNewRun();
        kills = 0;
        depth = 0;
        NextDepth();
    }

    // HP carries over between depths; the hero starts each depth in the middle of a fresh cave.
    void NextDepth()
    {
        depth++;
        ClearField();
        player.transform.position = Bounds.center;
        timeLeft = depthTime;
        spawnTimer = 0f;
        bossSpawned = false;
        currentBoss = null;
        bossBar.SetActive(false);
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    void Update()
    {
        UpdateHud();
        if (!IsPlaying) return;

        if (!bossSpawned)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) SpawnBoss();
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = CurrentSpawnInterval();
            if (Enemy.All.Count < maxEnemies) SpawnRandomEnemy();
        }
    }

    void SpawnBoss()
    {
        bossSpawned = true;
        EnemyData data = depth >= forceDepth && forceBoss != null ? forceBoss : boss;
        currentBoss = Enemy.Spawn(data, OffscreenPoint(), StatScale, player, this);
        bossName.text = data.displayName;
        bossBar.SetActive(true);
    }

    public void OnEnemyKilled(Enemy enemy)
    {
        kills++;
        if (enemy != currentBoss) return;

        if (depth >= forceDepth)
            ShowPanel("THE FORCE IS DESTROYED", "The island is safe. You win!", "Play again", StartRun);
        else
            ShowPanel($"Depth {depth} cleared!", "Go deeper, or return to town with what you found?",
                "Go deeper", NextDepth, "Return to town", ReturnToTown);
    }

    public void OnPlayerDied()
    {
        ShowPanel("DEFEATED", "Forced retreat. Everything found on this run is lost,\nand you start the next run weakened.\n(The town isn't built yet.)",
            "Dive again", StartRun);
    }

    // Placeholder until the town exists.
    void ReturnToTown()
    {
        ShowPanel("Back in town", "You return safely with your loot. A day passes.\n(The town isn't built yet.)", "Dive again", StartRun);
    }

    // ---------- Spawning ----------

    // Spawns speed up over the timer, and more on deeper levels.
    float CurrentSpawnInterval()
    {
        float progress = 1f - Mathf.Clamp01(timeLeft / depthTime);
        float interval = Mathf.Lerp(spawnIntervalStart, spawnIntervalEnd, progress);
        return interval / (1f + spawnScalePerDepth * (depth - 1));
    }

    void SpawnRandomEnemy()
    {
        if (spawnTable.Length == 0) return;

        float total = 0f;
        foreach (SpawnEntry entry in spawnTable) total += entry.weight;

        float roll = Random.value * total;
        EnemyData pick = spawnTable[spawnTable.Length - 1].enemy; // fallback for float rounding
        foreach (SpawnEntry entry in spawnTable)
        {
            roll -= entry.weight;
            if (roll <= 0f)
            {
                pick = entry.enemy;
                break;
            }
        }
        if (pick != null) Enemy.Spawn(pick, OffscreenPoint(), StatScale, player, this);
    }

    // A point on a rectangle just outside the camera view, inside the arena, so enemies don't pop in on screen.
    Vector2 OffscreenPoint()
    {
        float halfHeight = cam.orthographicSize + 1f;
        float halfWidth = cam.orthographicSize * cam.aspect + 1f;
        Vector2 center = cam.transform.position;
        Rect inside = new Rect(Bounds.xMin + 1f, Bounds.yMin + 1f, Bounds.width - 2f, Bounds.height - 2f);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 offset = Random.value < halfWidth / (halfWidth + halfHeight)
                ? new Vector2(Random.Range(-halfWidth, halfWidth), Random.value < 0.5f ? -halfHeight : halfHeight)
                : new Vector2(Random.value < 0.5f ? -halfWidth : halfWidth, Random.Range(-halfHeight, halfHeight));
            Vector2 point = center + offset;
            if (inside.Contains(point)) return point;
        }
        // The view covers the whole arena on this side: spawn anywhere inside instead.
        return new Vector2(Random.Range(inside.xMin, inside.xMax), Random.Range(inside.yMin, inside.yMax));
    }

    // Destroy every enemy and enemy shot (they leave the lists at the end of the frame).
    void ClearField()
    {
        foreach (Enemy enemy in Enemy.All) Destroy(enemy.gameObject);
        foreach (Projectile projectile in Projectile.All) Destroy(projectile.gameObject);
    }

    // ---------- UI ----------

    void UpdateHud()
    {
        SetFill(hpFill, (float)player.Hp / player.maxHp);
        hpText.text = $"{player.Hp} / {player.maxHp}";
        depthText.text = (depth >= forceDepth ? $"Depth {depth} (the force)" : $"Depth {depth}") + $"     Kills {kills}";

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
        timerText.text = bossSpawned ? "BOSS" : $"{seconds / 60}:{seconds % 60:00}";

        if (currentBoss != null) SetFill(bossFill, (float)currentBoss.hp / currentBoss.maxHp);
    }

    // Pauses the game and shows a message with one or two buttons.
    void ShowPanel(string title, string body, string primaryLabel, UnityAction primary,
        string secondaryLabel = null, UnityAction secondary = null)
    {
        Time.timeScale = 0f;
        ClearField();
        panel.SetActive(true);
        panelTitle.text = title;
        panelBody.text = body;
        SetButton(primaryButton, primaryLabel, primary);
        secondaryButton.gameObject.SetActive(secondary != null);
        if (secondary != null) SetButton(secondaryButton, secondaryLabel, secondary);
    }

    static void SetButton(Button button, string label, UnityAction action)
    {
        button.GetComponentInChildren<Text>().text = label;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    static void SetFill(RectTransform fill, float amount)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
    }
}
