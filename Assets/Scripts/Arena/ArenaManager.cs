using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

[System.Serializable]
public class SpawnEntry
{
    public EnemyData enemy;
    [Min(0f)] public float weight = 1f;
    [Tooltip("Weight multiplier on depth 1 only (e.g. 0.33 = a third as often).")]
    [Min(0f)] public float depth1Multiplier = 1f;
}

[System.Serializable]
public class HordeGroup
{
    public EnemyData enemy;
    public int count;
}

[System.Serializable]
public class HordeWave
{
    public HordeGroup[] groups;
}

// Runs one dungeon dive (Vampire Survivors style), or the final horde on day 15.
// Dive: every depth, kill N enemies while the horde builds up. Normal depth: reaching N opens a teleporter. Boss depth
// (every `bossEvery` depths): reaching N summons the boss while the horde keeps coming; killing it opens the teleporter.
// The boss on `forceDepth` is the force itself (victory). Once the teleporter is open, spawning stops; stepping in
// clears the depth: go deeper or return to town. Kills, materials, crystals and ores go back to the town.
// Horde: the same cave, `hordeWaves` one after another; beat them all to win, die and the town falls.
// The hero starts with the town's NPC buffs (Campaign).
public class ArenaManager : MonoBehaviour
{
    enum HordeState { Break, Spawning, Fighting }

    public const float WallThickness = 1f; // walls left/right of the arena (how far the camera may show past the bounds)
    public const float WallHeight = 2f;    // walls above/below the arena (wall top + brick face)

    [Header("Game")]
    public TownConfig config;
    public string townScene = "Town";

    [Header("Arena")]
    public Vector2 arenaSize = new Vector2(64f, 40f);

    [Header("Depth")]
    [Tooltip("Kills needed to clear depth 1...")]
    public int killsToClear = 40;
    [Tooltip("...plus this many more for every depth below the first.")]
    public int extraKillsPerDepth = 10;
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
    [Tooltip("...and once the kill count is reached (also used during a boss).")]
    public float spawnIntervalEnd = 0.25f;
    public int maxEnemies = 250;
    public EnemyData boss;
    public EnemyData forceBoss;

    [Header("Crates, crystal clusters and ores (placed at the start of every depth)")]
    public EnemyData crate;
    public int cratesPerDepth = 4;
    [Tooltip("Broken like a crate; drops crystals for the Shaman's shop.")]
    public EnemyData crystalCluster;
    public int clustersPerDepth = 1;
    public int oresPerDepth = 3;
    [Tooltip("Seconds of standing next to an ore to mine it.")]
    public float oreMineTime = 3f;
    [Tooltip("How close (from the ore's center) the hero must stand to mine.")]
    public float oreMineRadius = 1.6f;
    public Sprite oreSprite;
    [Tooltip("White + 1 for real art; a tint and a bigger scale for the placeholder square.")]
    public Color oreColor = Color.white;
    public float oreScale = 1f;

    [Header("Teleporter (opens when the depth's goal is done)")]
    [Tooltip("The pulsing glow (placeholder circle).")]
    public Sprite teleporterSprite;
    [Tooltip("Optional art drawn on top of the glow (the stairs down).")]
    public Sprite teleporterStairs;
    public float teleporterRadius = 1.2f;

    [Header("Final horde (day 15)")]
    public HordeWave[] hordeWaves;
    [Tooltip("Horde enemies are as strong as on this depth.")]
    public int hordeStrengthDepth = 4;
    [Tooltip("Free level-up picks at the start of the horde (all unlocked weapons are also given at Lv 1).")]
    public int hordeFreePicks = 5;
    [Tooltip("Seconds of calm before each wave.")]
    public float waveBreak = 3f;
    [Tooltip("Seconds between spawns while a wave pours in.")]
    public float waveSpawnInterval = 0.1f;

    [Header("Scene references")]
    public Player player;
    public LevelUp leveling;
    public Sprite xpSprite;
    public Sprite materialSprite;
    public Sprite crystalSprite;
    [Tooltip("Plain white square, used for simple bars (ore mining progress).")]
    public Sprite whiteSquare;
    public Camera cam;
    public RectTransform hpFill;
    public Text hpText;
    public Text depthText;
    public Text quakeText;
    [Tooltip("Top-center: kills toward the depth's goal, BOSS, the teleporter hint, or the horde wave.")]
    [FormerlySerializedAs("timerText")] public Text goalText;
    [Tooltip("Points at the teleporter while it's off-screen.")]
    public RectTransform teleporterArrow;
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
    bool horde;     // this run is the final horde, not a dive
    int depth;
    int kills;      // whole dive
    int depthKills; // this depth, toward the goal
    int diveMaterials;
    int diveCrystals;
    int diveOres;
    readonly Dictionary<EnemyData, int> diveKills = new Dictionary<EnemyData, int>();
    float spawnTimer;
    bool bossSpawned;
    Enemy currentBoss;
    GameObject teleporter; // null = not open
    Transform teleporterGlow;

    HordeState hordeState;
    int wave;              // current horde wave, 1-based (0 = before the first)
    float waveBreakLeft;
    readonly List<EnemyData> waveQueue = new List<EnemyData>(); // still to spawn in this wave

    static readonly Color TeleporterColor = new Color(0.4f, 0.9f, 1f, 0.8f);

    int KillsNeeded => killsToClear + extraKillsPerDepth * (depth - 1);
    bool IsBossDepth => depth % bossEvery == 0 || depth >= forceDepth;
    float StatScale => 1f + statScalePerDepth * ((horde ? hordeStrengthDepth : depth) - 1);

    void Awake()
    {
        Bounds = new Rect(-arenaSize / 2f, arenaSize);
    }

    void Start()
    {
        if (leveling == null || config == null || quakeText == null || crate == null || teleporterArrow == null ||
            hordeWaves == null || hordeWaves.Length == 0 || whiteSquare == null || crystalCluster == null || crystalSprite == null)
        {
            Debug.LogError("The Arena scene is out of date. Rebuild it with Jam > Build Game Scenes.");
            enabled = false;
            return;
        }
        campaign = Campaign.Current ?? Campaign.StartNew(config); // also lets you press Play in this scene directly
        horde = campaign.IsHordeDay;
        if (horde) StartHorde();
        else StartDive();
    }

    // ---------- Dive flow ----------

    // Base stats + NPC buffs, the Sword only, level 1, nothing found yet.
    void StartDive()
    {
        ResetRun();
        NextDepth();
    }

    void ResetRun()
    {
        player.ResetForNewRun();
        campaign.ApplyLoadout(player, leveling.weapons);
        leveling.ResetForNewRun(campaign.weaponUnlocked);
        kills = 0;
        diveMaterials = 0;
        diveCrystals = 0;
        diveOres = 0;
        diveKills.Clear();
        depth = 0;
    }

    // HP, levels and loot carry over between depths; the hero starts each depth in the middle of a fresh cave.
    void NextDepth()
    {
        depth++;
        ClearField();
        player.transform.position = Bounds.center;
        depthKills = 0;
        spawnTimer = 0f;
        bossSpawned = false;
        currentBoss = null;
        bossBar.SetActive(false);
        panel.SetActive(false);
        Time.timeScale = 1f;
        PlaceCratesAndOres();
        leveling.TryShowCards(); // level-ups earned at the end of the last depth are picked now
    }

    // Supply crates (broken by weapons for materials), crystal clusters (broken for crystals) and ore veins
    // (mined to weaken the quake), scattered away from the middle where the hero starts.
    void PlaceCratesAndOres()
    {
        for (int i = 0; i < cratesPerDepth; i++) Enemy.Spawn(crate, RandomFloorPoint(Bounds.center), 1f, player, this);
        for (int i = 0; i < clustersPerDepth; i++) Enemy.Spawn(crystalCluster, RandomFloorPoint(Bounds.center), 1f, player, this);
        for (int i = 0; i < oresPerDepth; i++) OreVein.Place(oreSprite, oreColor, oreScale, whiteSquare, RandomFloorPoint(Bounds.center), this);
    }

    // A random spot in the cave at least 6 units from `awayFrom` (the hero's start, or the hero for the teleporter).
    Vector2 RandomFloorPoint(Vector2 awayFrom)
    {
        const float minDistance = 6f;
        Vector2 point = Vector2.zero;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            point = new Vector2(Random.Range(Bounds.xMin + 2f, Bounds.xMax - 2f), Random.Range(Bounds.yMin + 2f, Bounds.yMax - 2f));
            if ((point - awayFrom).sqrMagnitude >= minDistance * minDistance) break;
        }
        return point;
    }

    void Update()
    {
        UpdateHud();
        UpdateArrow();
        if (!IsPlaying) return;

        if (horde)
        {
            UpdateHorde();
            return;
        }

        // The horde keeps spawning until the teleporter opens; stepping into it clears the depth.
        if (teleporter != null)
        {
            float pulse = 1f + 0.1f * Mathf.Sin(Time.time * 6f);
            teleporterGlow.localScale = Vector3.one * teleporterRadius * 2f * pulse; // placeholder circle is 1 unit wide
            if (((Vector2)(player.transform.position - teleporter.transform.position)).sqrMagnitude <= teleporterRadius * teleporterRadius)
                DepthCleared();
            return; // no spawns while it's open
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
        ShowBoss(Enemy.Spawn(data, OffscreenPoint(), StatScale, player, this));
    }

    void ShowBoss(Enemy enemy)
    {
        currentBoss = enemy;
        bossName.text = enemy.data.displayName;
        bossBar.SetActive(true);
    }

    public void OnEnemyKilled(Enemy enemy)
    {
        if (!IsPlaying) return; // the depth already ended this frame (e.g. several enemies died in one sweep)

        if (enemy.data.behavior == EnemyBehavior.Crate) // broken crate or crystal cluster: just its loot, not a kill
        {
            if (enemy.data.materialAmount > 0)
                Pickup.Drop(PickupKind.Materials, materialSprite, enemy.transform.position, enemy.data.materialAmount, this);
            if (enemy.data.crystalAmount > 0)
                Pickup.Drop(PickupKind.Crystal, crystalSprite, enemy.transform.position, enemy.data.crystalAmount, this);
            return;
        }

        kills++;
        depthKills++;
        diveKills[enemy.data] = diveKills.TryGetValue(enemy.data, out int count) ? count + 1 : 1;

        if (horde)
        {
            if (enemy == currentBoss) bossBar.SetActive(false);
            DropLoot(enemy);
            return; // waves are handled in UpdateHorde
        }

        if (enemy == currentBoss && depth >= forceDepth)
        {
            ShowPanel("THE FORCE IS DESTROYED", "Deep below the island, the force falls silent.\nThe earthquakes are over.",
                "Continue", () => FinishGame(true, "You destroyed the Force at the bottom of the dungeon. The earthquakes stopped."));
            return;
        }

        DropLoot(enemy); // a boss's loot lands on the floor too; what's left is collected at the teleporter

        if (enemy == currentBoss)
        {
            OpenTeleporter();
        }
        else if (!bossSpawned && teleporter == null && depthKills >= KillsNeeded)
        {
            // Kill goal reached: a boss depth summons its boss, a normal depth opens the teleporter.
            if (IsBossDepth) SpawnBoss();
            else OpenTeleporter();
        }
    }

    void DropLoot(Enemy enemy)
    {
        if (Random.value < enemy.data.materialChance)
            Pickup.Drop(PickupKind.Materials, materialSprite, enemy.transform.position, enemy.data.materialAmount, this);
        if (leveling.levelingEnabled && enemy.data.xp > 0)
            Pickup.Drop(PickupKind.Xp, xpSprite, (Vector2)enemy.transform.position + Random.insideUnitCircle * 0.3f, enemy.data.xp, this);
    }

    // The depth's goal is done: spawning stops, Spitters off-screen vanish (no kill, no loot), and a teleporter opens
    // somewhere in the cave. Enemies still around keep fighting. Stepping in clears the depth.
    void OpenTeleporter()
    {
        foreach (Enemy enemy in Enemy.All)
            if (enemy.data.behavior == EnemyBehavior.Shooter && !OnScreen(enemy.transform.position)) Destroy(enemy.gameObject);

        // A pulsing glow, with the stairs down drawn on top of it (if there's art for them).
        teleporter = new GameObject("Teleporter");
        teleporter.transform.position = RandomFloorPoint(player.transform.position);

        SpriteRenderer glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
        glow.transform.SetParent(teleporter.transform, false);
        glow.transform.localScale = Vector3.one * teleporterRadius * 2f;
        glow.sprite = teleporterSprite;
        glow.color = TeleporterColor;
        glow.sortingOrder = -2; // under the stairs, pickups and enemies
        teleporterGlow = glow.transform;

        if (teleporterStairs != null)
        {
            SpriteRenderer stairs = new GameObject("Stairs").AddComponent<SpriteRenderer>();
            stairs.transform.SetParent(teleporter.transform, false);
            stairs.sprite = teleporterStairs;
            stairs.sortingOrder = -1;
        }
    }

    bool OnScreen(Vector2 worldPosition)
    {
        Vector3 viewport = cam.WorldToViewportPoint(worldPosition);
        return viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    // Stepped into the teleporter: everything still on the floor is collected, then: go deeper or go home.
    void DepthCleared()
    {
        int foundMaterials = 0;
        int foundCrystals = 0;
        int xp = 0;
        foreach (Pickup pickup in Pickup.All)
        {
            if (pickup.kind == PickupKind.Materials) foundMaterials += pickup.amount;
            else if (pickup.kind == PickupKind.Crystal) foundCrystals += pickup.amount;
            else xp += pickup.amount;
        }
        diveMaterials += foundMaterials;
        diveCrystals += foundCrystals;

        ShowPanel($"Depth {depth} cleared!",
            $"+{foundMaterials} materials and +{foundCrystals} crystals collected from the floor.\n" +
            $"Go deeper, or return to town with {diveMaterials} materials and {diveCrystals} crystals?",
            "Go deeper", NextDepth, "Return to town", () => EndDive(true));
        leveling.AddXp(xp); // the cards wait until "Go deeper"
    }

    public void OnPlayerDied()
    {
        if (horde)
            ShowPanel("DEFEATED", "The horde overruns the town.", "Continue", () => FinishGame(false, "The horde overran the town on the last night."));
        else
            ShowPanel("DEFEATED", "Everything found on this dive is lost.", "Return to town", () => EndDive(false));
    }

    public void Collect(PickupKind kind, int amount)
    {
        if (kind == PickupKind.Materials) diveMaterials += amount;
        else if (kind == PickupKind.Crystal) diveCrystals += amount;
        else leveling.AddXp(amount);
    }

    public void OreMined()
    {
        diveOres++;
    }

    // Back to town: the campaign takes the loot (or not), the day passes, and the Town scene loads.
    void EndDive(bool survived)
    {
        campaign.RecordDive(kills, depth, diveOres);
        campaign.EndDive(survived, diveKills, diveMaterials, diveCrystals, diveOres);
        Time.timeScale = 1f;
        SceneManager.LoadScene(townScene);
    }

    // The game is over (won or lost): record this fight's stats, then the Town scene shows the epilogue.
    void FinishGame(bool won, string reason)
    {
        campaign.RecordDive(kills, depth, diveOres);
        campaign.EndGame(won, reason);
        Time.timeScale = 1f;
        SceneManager.LoadScene(townScene);
    }

    // ---------- Final horde ----------

    // Every unlocked weapon at Lv 1 plus some free level-up picks, then wave 1 after a short calm.
    void StartHorde()
    {
        ResetRun();
        foreach (Weapon weapon in leveling.weapons)
            if (campaign.weaponUnlocked[(int)weapon.id]) weapon.enabled = true;

        ClearField();
        player.transform.position = Bounds.center;
        bossBar.SetActive(false);
        panel.SetActive(false);
        Time.timeScale = 1f;
        wave = 0;
        hordeState = HordeState.Break;
        waveBreakLeft = waveBreak;
        leveling.GrantPicks(hordeFreePicks);
    }

    // Break ("Wave N incoming") -> Spawning (the wave pours in from off-screen) -> Fighting (until it's beaten).
    void UpdateHorde()
    {
        switch (hordeState)
        {
            case HordeState.Break:
                waveBreakLeft -= Time.deltaTime;
                if (waveBreakLeft <= 0f) StartNextWave();
                break;

            case HordeState.Spawning:
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    spawnTimer = waveSpawnInterval;
                    EnemyData data = waveQueue[waveQueue.Count - 1];
                    waveQueue.RemoveAt(waveQueue.Count - 1);
                    Enemy enemy = Enemy.Spawn(data, OffscreenPoint(), StatScale, player, this);
                    if (data.behavior == EnemyBehavior.Boss) ShowBoss(enemy);
                    if (waveQueue.Count == 0) hordeState = HordeState.Fighting;
                }
                break;

            case HordeState.Fighting:
                if (WaveFightersLeft() == 0) WaveBeaten();
                break;
        }
    }

    void StartNextWave()
    {
        wave++;
        waveQueue.Clear();
        foreach (HordeGroup group in hordeWaves[wave - 1].groups)
            for (int i = 0; i < group.count; i++) waveQueue.Add(group.enemy);

        // Mix the enemy types so they don't arrive in blocks.
        for (int i = waveQueue.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (waveQueue[i], waveQueue[j]) = (waveQueue[j], waveQueue[i]);
        }

        spawnTimer = 0f;
        hordeState = waveQueue.Count > 0 ? HordeState.Spawning : HordeState.Fighting;
    }

    // Spitters left behind don't have to be hunted down: they vanish when the rest of the wave is beaten.
    void WaveBeaten()
    {
        foreach (Enemy enemy in Enemy.All)
            if (enemy.data.behavior == EnemyBehavior.Shooter) Destroy(enemy.gameObject);

        if (wave >= hordeWaves.Length)
        {
            ShowPanel("THE HORDE IS BROKEN", "You held the town through the last night.", "Continue",
                () => FinishGame(true, "You held the town against the horde on the last night."));
            return;
        }
        hordeState = HordeState.Break;
        waveBreakLeft = waveBreak;
    }

    // Enemies that still have to be beaten for the wave to end (Spitters and crates don't count).
    int WaveFightersLeft()
    {
        int count = 0;
        foreach (Enemy enemy in Enemy.All)
            if (!enemy.IsDead && enemy.data.behavior != EnemyBehavior.Shooter && enemy.data.behavior != EnemyBehavior.Crate) count++;
        return count;
    }

    // ---------- Spawning ----------

    // Spawns speed up as the kill count gets closer to the goal, and more on deeper levels.
    float CurrentSpawnInterval()
    {
        float progress = Mathf.Clamp01((float)depthKills / KillsNeeded);
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

    // Destroy every enemy, crate, shot, dagger, pickup, ore and the teleporter (lists empty at the end of the frame).
    void ClearField()
    {
        if (teleporter != null) Destroy(teleporter);
        teleporter = null;
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

        if (horde)
        {
            depthText.text = $"Day {campaign.day}     THE HORDE     Wave {Mathf.Max(1, wave)}/{hordeWaves.Length}     Kills {kills}";
            quakeText.text = $"Hold the town: beat all {hordeWaves.Length} waves";
            goalText.text = hordeState == HordeState.Break ? $"Wave {wave + 1} incoming..." : $"Wave {wave}: {WaveFightersLeft() + waveQueue.Count} left";
        }
        else
        {
            string depthLabel = depth >= forceDepth ? $"Depth {depth} (the force)" : IsBossDepth ? $"Depth {depth} (boss)" : $"Depth {depth}";
            depthText.text = $"Day {campaign.day}     {depthLabel}     Kills {kills}     Materials {diveMaterials}     Crystals {diveCrystals}";

            float oreDrop = diveOres * config.orePercent;
            string quake = campaign.NextQuakeDay > 0 ? $"Next quake (day {campaign.NextQuakeDay}): {campaign.QuakeForecast:0}%" : "No more quakes";
            quakeText.text = $"{quake}     Ores mined {diveOres} (-{oreDrop:0}% if you get home)";

            goalText.text = teleporter != null ? "Teleporter open: step in!" : bossSpawned ? "BOSS" : $"Kills {depthKills} / {KillsNeeded}";
        }

        if (currentBoss != null) SetFill(bossFill, (float)currentBoss.hp / currentBoss.maxHp);
    }

    // While the teleporter is open and off-screen, an arrow at the screen edge points to it.
    void UpdateArrow()
    {
        bool show = IsPlaying && teleporter != null && !OnScreen(teleporter.transform.position);
        teleporterArrow.gameObject.SetActive(show);
        if (!show) return;

        Vector2 center = new Vector2(Screen.width, Screen.height) / 2f;
        Vector2 direction = ((Vector2)cam.WorldToScreenPoint(teleporter.transform.position) - center).normalized;
        const float margin = 60f;
        // Slide out from the center along the direction until the arrow reaches the edge (minus the margin).
        float reachX = direction.x != 0f ? (center.x - margin) / Mathf.Abs(direction.x) : float.MaxValue;
        float reachY = direction.y != 0f ? (center.y - margin) / Mathf.Abs(direction.y) : float.MaxValue;
        teleporterArrow.position = center + direction * Mathf.Min(reachX, reachY);
        teleporterArrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f); // the glyph points up
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
