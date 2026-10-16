using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class SpawnEntry
{
    public EnemyData enemy;
    [Min(0f)] public float weight = 1f;
    [Tooltip("Weight multiplier on depth 1 only (e.g. 0.33 = a third as often).")]
    [Min(0f)] public float depth1Multiplier = 1f;
}

// Runs one dungeon dive (Vampire Survivors style). Every depth: survive the timer while the horde builds up.
// Normal depth: then spawning stops and the depth is cleared once the stragglers are dead.
// Boss depth (every `bossEvery` depths): then the boss appears while the horde keeps coming; kill it to clear.
// The boss on `forceDepth` is the force itself (victory). After each cleared depth: go deeper or return to town.
// The hero starts with the town's weapons and NPC buffs (Campaign); kills and materials go back to the town.
public class ArenaManager : MonoBehaviour
{
    public const float WallThickness = 1f;

    [Header("Game")]
    public TownConfig config;
    public string townScene = "Town";

    [Header("Arena")]
    public Vector2 arenaSize = new Vector2(64f, 40f);

    [Header("Depth")]
    public float depthTime = 90f;
    [Tooltip("A boss appears on every depth divisible by this (5, 10, ...).")]
    [Min(1)] public int bossEvery = 5;
    public int forceDepth = 10;
    [Tooltip("Enemy HP/damage multiplier added per depth below the first (0.3 = +30% each depth).")]
    public float statScalePerDepth = 0.3f;
    [Tooltip("Spawn speed multiplier added per depth below the first.")]
    public float spawnScalePerDepth = 0.2f;

    [Header("Spawning")]
    public SpawnEntry[] spawnTable;
    [Tooltip("Seconds between spawns at the start of a depth...")]
    public float spawnIntervalStart = 1f;
    [Tooltip("...and when the timer runs out (also used during a boss).")]
    public float spawnIntervalEnd = 0.25f;
    public int maxEnemies = 250;
    public EnemyData boss;
    public EnemyData forceBoss;

    [Header("Crates and ores (placed at the start of every depth)")]
    public EnemyData crate;
    public int cratesPerDepth = 4;
    public int oresPerDepth = 3;
    [Tooltip("Seconds of standing next to an ore to mine it.")]
    public float oreMineTime = 3f;
    [Tooltip("How close (from the ore's center) the hero must stand to mine.")]
    public float oreMineRadius = 1.6f;
    public Sprite oreSprite;

    [Header("Scene references")]
    public Player player;
    public LevelUp leveling;
    public Sprite xpSprite;
    public Sprite materialSprite;
    public Camera cam;
    public RectTransform hpFill;
    public Text hpText;
    public Text depthText;
    public Text quakeText;
    public Text timerText;
    [Tooltip("Points at the nearest enemy left while clearing stragglers.")]
    public RectTransform enemyArrow;
    public GameObject bossBar;
    public RectTransform bossFill;
    public Text bossName;
    public GameObject panel;
    public Text panelTitle;
    public Text panelBody;
    public Button primaryButton;
    public Button secondaryButton;

    public Rect Bounds { get; private set; }
    public bool PanelOpen => panel.activeSelf;
    // False = paused (or the scene is out of date and this component switched itself off): nothing should act.
    public bool IsPlaying => enabled && !PanelOpen && !leveling.IsChoosing;

    Campaign campaign;
    int depth;
    int kills;
    int diveMaterials;
    int diveOres;
    readonly Dictionary<EnemyData, int> diveKills = new Dictionary<EnemyData, int>();
    float timeLeft;
    float spawnTimer;
    Enemy currentBoss;

    bool IsBossDepth => depth % bossEvery == 0 || depth >= forceDepth;
    bool ClearingStragglers => timeLeft <= 0f && !IsBossDepth;
    float StatScale => 1f + statScalePerDepth * (depth - 1);

    void Awake()
    {
        Bounds = new Rect(-arenaSize / 2f, arenaSize);
    }

    void Start()
    {
        if (leveling == null || config == null || enemyArrow == null || quakeText == null || crate == null)
        {
            Debug.LogError("The Arena scene is out of date. Rebuild it with Jam > Build Game Scenes.");
            enabled = false;
            return;
        }
        campaign = Campaign.Current ?? Campaign.StartNew(config); // also lets you press Play in this scene directly
        StartDive();
    }

    // ---------- Dive flow ----------

    // Base stats + the town's weapons and NPC buffs, level 1, nothing found yet.
    void StartDive()
    {
        player.ResetForNewRun();
        campaign.ApplyLoadout(player, leveling.weapons);
        leveling.ResetForNewRun();
        kills = 0;
        diveMaterials = 0;
        diveOres = 0;
        diveKills.Clear();
        depth = 0;
        NextDepth();
    }

    // HP, levels and loot carry over between depths; the hero starts each depth in the middle of a fresh cave.
    void NextDepth()
    {
        depth++;
        ClearField();
        player.transform.position = Bounds.center;
        timeLeft = depthTime;
        spawnTimer = 0f;
        currentBoss = null;
        bossBar.SetActive(false);
        panel.SetActive(false);
        Time.timeScale = 1f;
        PlaceCratesAndOres();
        leveling.TryShowCards(); // level-ups earned at the end of the last depth are picked now
    }

    // Supply crates (broken by weapons for materials) and ore veins (mined to weaken the quake),
    // scattered away from the middle where the hero starts.
    void PlaceCratesAndOres()
    {
        for (int i = 0; i < cratesPerDepth; i++) Enemy.Spawn(crate, RandomFloorPoint(), 1f, player, this);
        for (int i = 0; i < oresPerDepth; i++) OreVein.Place(oreSprite, RandomFloorPoint(), this);
    }

    Vector2 RandomFloorPoint()
    {
        const float minDistanceFromStart = 6f;
        Vector2 point = Vector2.zero;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            point = new Vector2(Random.Range(Bounds.xMin + 2f, Bounds.xMax - 2f), Random.Range(Bounds.yMin + 2f, Bounds.yMax - 2f));
            if ((point - Bounds.center).sqrMagnitude >= minDistanceFromStart * minDistanceFromStart) break;
        }
        return point;
    }

    void Update()
    {
        UpdateHud();
        UpdateArrow();
        if (!IsPlaying) return;

        if (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                if (IsBossDepth) SpawnBoss();
                else CrumbleShooters();
            }
        }

        if (!ClearingStragglers)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = CurrentSpawnInterval();
                if (Enemy.All.Count < maxEnemies) SpawnRandomEnemy();
            }
        }
        else if (NearestLivingEnemy() == null)
        {
            DepthCleared(0, 0); // every straggler is dead
        }
    }

    // Stationary Spitters are a chore to hunt down, so they vanish when the clean-up starts: no kill, no loot.
    void CrumbleShooters()
    {
        foreach (Enemy enemy in Enemy.All)
            if (enemy.data.behavior == EnemyBehavior.Shooter) Destroy(enemy.gameObject);
    }

    void SpawnBoss()
    {
        EnemyData data = depth >= forceDepth && forceBoss != null ? forceBoss : boss;
        currentBoss = Enemy.Spawn(data, OffscreenPoint(), StatScale, player, this);
        bossName.text = data.displayName;
        bossBar.SetActive(true);
    }

    public void OnEnemyKilled(Enemy enemy)
    {
        if (enemy.data.behavior == EnemyBehavior.Crate) // broken crate: just its materials, not a kill
        {
            Pickup.Drop(PickupKind.Materials, materialSprite, enemy.transform.position, enemy.data.materialAmount, this);
            return;
        }

        kills++;
        diveKills[enemy.data] = diveKills.TryGetValue(enemy.data, out int count) ? count + 1 : 1;

        if (enemy != currentBoss)
        {
            if (Random.value < enemy.data.materialChance)
                Pickup.Drop(PickupKind.Materials, materialSprite, enemy.transform.position, enemy.data.materialAmount, this);
            if (leveling.levelingEnabled && enemy.data.xp > 0)
                Pickup.Drop(PickupKind.Xp, xpSprite, (Vector2)enemy.transform.position + Random.insideUnitCircle * 0.3f, enemy.data.xp, this);
            return;
        }

        if (depth >= forceDepth)
        {
            ShowPanel("THE FORCE IS DESTROYED", "The island is safe. You win!", "New game", NewGame);
            return;
        }

        int bossMaterials = Random.value < enemy.data.materialChance ? enemy.data.materialAmount : 0;
        DepthCleared(bossMaterials, enemy.data.xp);
    }

    // Everything still on the floor (plus any boss loot) is collected, then: go deeper or go home.
    void DepthCleared(int bonusMaterials, int bonusXp)
    {
        int foundMaterials = bonusMaterials;
        int xp = bonusXp;
        foreach (Pickup pickup in Pickup.All)
        {
            if (pickup.kind == PickupKind.Materials) foundMaterials += pickup.amount;
            else xp += pickup.amount;
        }
        diveMaterials += foundMaterials;

        ShowPanel($"Depth {depth} cleared!",
            $"+{foundMaterials} materials collected.\nGo deeper, or return to town with {diveMaterials} materials?",
            "Go deeper", NextDepth, "Return to town", () => EndDive(true));
        leveling.AddXp(xp); // the cards wait until "Go deeper"
    }

    public void OnPlayerDied()
    {
        ShowPanel("DEFEATED", $"Everything found on this dive is lost.\nRecovering takes {config.daysLostOnDefeat} days.",
            "Return to town", () => EndDive(false));
    }

    public void Collect(PickupKind kind, int amount)
    {
        if (kind == PickupKind.Materials) diveMaterials += amount;
        else leveling.AddXp(amount);
    }

    public void OreMined()
    {
        diveOres++;
    }

    // Back to town: the campaign takes the loot (or not), the days pass, and the Town scene loads.
    void EndDive(bool survived)
    {
        campaign.EndDive(survived, diveKills, diveMaterials, diveOres);
        Time.timeScale = 1f;
        SceneManager.LoadScene(townScene);
    }

    void NewGame()
    {
        Campaign.StartNew(config);
        Time.timeScale = 1f;
        SceneManager.LoadScene(townScene);
    }

    // ---------- Spawning ----------

    // Spawns speed up over the timer, and more on deeper levels.
    float CurrentSpawnInterval()
    {
        float progress = 1f - Mathf.Clamp01(timeLeft / depthTime);
        float interval = Mathf.Lerp(spawnIntervalStart, spawnIntervalEnd, progress);
        return interval / (1f + spawnScalePerDepth * (depth - 1));
    }

    float SpawnWeight(SpawnEntry entry)
    {
        return depth == 1 ? entry.weight * entry.depth1Multiplier : entry.weight;
    }

    void SpawnRandomEnemy()
    {
        if (spawnTable.Length == 0) return;

        float total = 0f;
        foreach (SpawnEntry entry in spawnTable) total += SpawnWeight(entry);

        float roll = Random.value * total;
        EnemyData pick = spawnTable[spawnTable.Length - 1].enemy; // fallback for float rounding
        foreach (SpawnEntry entry in spawnTable)
        {
            roll -= SpawnWeight(entry);
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

    // Living enemies that have to be dealt with (crates don't count).
    static bool IsThreat(Enemy enemy)
    {
        return !enemy.IsDead && enemy.data.behavior != EnemyBehavior.Crate;
    }

    Enemy NearestLivingEnemy()
    {
        Enemy nearest = null;
        float best = float.MaxValue;
        foreach (Enemy enemy in Enemy.All)
        {
            if (!IsThreat(enemy)) continue;
            float distance = ((Vector2)(enemy.transform.position - player.transform.position)).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = enemy;
            }
        }
        return nearest;
    }

    int LivingEnemyCount()
    {
        int count = 0;
        foreach (Enemy enemy in Enemy.All)
            if (IsThreat(enemy)) count++;
        return count;
    }

    // Destroy every enemy, crate, shot, dagger, pickup and ore (they leave their lists at the end of the frame).
    void ClearField()
    {
        foreach (Enemy enemy in Enemy.All) Destroy(enemy.gameObject);
        foreach (Projectile projectile in Projectile.All) Destroy(projectile.gameObject);
        foreach (Dagger dagger in Dagger.All) Destroy(dagger.gameObject);
        foreach (Pickup pickup in Pickup.All) Destroy(pickup.gameObject);
        foreach (OreVein ore in OreVein.All) Destroy(ore.gameObject);
    }

    // ---------- UI ----------

    void UpdateHud()
    {
        SetFill(hpFill, (float)player.Hp / player.maxHp);
        hpText.text = $"{player.Hp} / {player.maxHp}";
        string depthLabel = depth >= forceDepth ? $"Depth {depth} (the force)" : IsBossDepth ? $"Depth {depth} (boss)" : $"Depth {depth}";
        depthText.text = $"Day {campaign.day}     {depthLabel}     Kills {kills}     Materials {diveMaterials}";
        float oreDrop = diveOres * config.orePercent;
        string quake = campaign.NextQuakeDay > 0 ? $"Next quake (day {campaign.NextQuakeDay}): {campaign.QuakeForecast:0}%" : "No more quakes";
        quakeText.text = $"{quake}     Ores mined {diveOres} (-{oreDrop:0}% if you get home)";

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
        if (timeLeft > 0f) timerText.text = $"{seconds / 60}:{seconds % 60:00}";
        else timerText.text = IsBossDepth ? "BOSS" : $"Enemies left: {LivingEnemyCount()}";

        if (currentBoss != null) SetFill(bossFill, (float)currentBoss.hp / currentBoss.maxHp);
    }

    // While clearing stragglers: an arrow at the screen edge points to the nearest enemy left, if it's off-screen.
    void UpdateArrow()
    {
        Enemy target = IsPlaying && ClearingStragglers ? NearestLivingEnemy() : null;
        Vector2 screen = target != null ? (Vector2)cam.WorldToScreenPoint(target.transform.position) : Vector2.zero;
        bool offScreen = target != null && (screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height);
        enemyArrow.gameObject.SetActive(offScreen);
        if (!offScreen) return;

        Vector2 center = new Vector2(Screen.width, Screen.height) / 2f;
        Vector2 direction = (screen - center).normalized;
        const float margin = 60f;
        // Slide out from the center along the direction until the arrow hits the edge (minus the margin).
        float reachX = direction.x != 0f ? (center.x - margin) / Mathf.Abs(direction.x) : float.MaxValue;
        float reachY = direction.y != 0f ? (center.y - margin) / Mathf.Abs(direction.y) : float.MaxValue;
        enemyArrow.position = center + direction * Mathf.Min(reachX, reachY);
        enemyArrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f); // the glyph points up
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
