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

// Runs one dungeon dive (Vampire Survivors style).
// Dive: every depth, kill N enemies while the horde builds up. Normal depth: reaching N opens a teleporter. Boss depth
// (every `bossEvery` depths, endless): reaching N summons the boss while the horde keeps coming; killing it opens the
// teleporter. Once the teleporter is open, spawning stops; stepping in clears the depth: go deeper or return to town.
// Kills, materials, crystals, gold, supplies, ores and relics go back to the town.
// Ores and shrines (MineSpot) are used by standing next to them; every mined ore shakes the cave and may set off a trap.
// Every boss drops a relic (Relics, on this object). A few normal enemies spawn as variants (`variants`, EnemyVariant:
// blue tough, red fire trail, purple blast after death) worth more XP, with a small chance to drop a relic pickup
// (collecting it shows a relic card).
// The hero starts with the town's buffs (Campaign).
public class ArenaManager : MonoBehaviour
{
    enum Trap { None, Ambush, RockRain }

    public const float WallThickness = 1f; // walls left/right of the arena (how far the camera may show past the bounds)
    public const float WallHeight = 2f;    // walls above/below the arena (wall top + brick face)

    [Header("Game")]
    public TownConfig config;
    public string townScene = "Town";
    public string titleScene = "Title";

    [Header("Arena")]
    public Vector2 arenaSize = new Vector2(64f, 40f);

    [Header("Depth")]
    [Tooltip("Kills needed to clear depth 1...")]
    public int killsToClear = 40;
    [Tooltip("...plus this many more for every depth below the first.")]
    public int extraKillsPerDepth = 10;
    [Tooltip("A boss appears on every depth divisible by this (5, 10, ...).")]
    [Min(1)] public int bossEvery = 5;
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

    [Header("Shrines (the ore art tinted by type; used like an ore)")]
    [Tooltip("0 = no shrines (switched off for now, until they get their own art; they'd use the ore art tinted).")]
    public int shrinesPerDepth = 0;
    [Tooltip("Seconds of standing next to a shrine to use it.")]
    public float shrineTime = 5f;
    [Tooltip("XP shrine: this share of the XP needed for the current level (0.25 = a quarter of the bar).")]
    public float shrineXpShare = 0.25f;
    [Tooltip("Materials shrine: this many on depth 1...")]
    public int shrineMaterials = 20;
    [Tooltip("...plus this many more for every depth below the first.")]
    public int shrineMaterialsPerDepth = 5;
    [Tooltip("Crystal shrine: this many on depth 1...")]
    public int shrineCrystals = 1;
    [Tooltip("...plus this many more for every depth below the first.")]
    public int shrineCrystalsPerDepth = 1;

    [Header("Tremors (every mined ore shakes the cave, then: nothing, an ambush or a rock rain)")]
    public float tremorTime = 1f;
    [Tooltip("How far the view jolts (units).")]
    public float tremorStrength = 0.25f;
    [Tooltip("Ambush: this many enemies (the normal spawn mix) in a ring just off-screen on depth 1...")]
    public int ambushEnemies = 8;
    [Tooltip("...plus this many more for every depth below the first.")]
    public int ambushEnemiesPerDepth = 2;
    [Tooltip("Rock rain: rocks fall at random spots on screen for this many seconds...")]
    public float rockRainTime = 4f;
    [Tooltip("...one every this many seconds.")]
    public float rockInterval = 0.1f;
    [Tooltip("Seconds the shadow shows before a rock lands.")]
    public float rockWarning = 1f;
    public float rockRadius = 1f;
    [Tooltip("Damage to the hero (depth 1; scaled like enemy damage). Enemies are never hurt.")]
    public int rockDamage = 15;
    [Tooltip("A falling rock's target on the floor (a circle; its edge is blastRingSprite).")]
    public Sprite rockTargetSprite;
    [Tooltip("The falling rocks: lumps of the cave's stone (Tools/bake_tremor.py), one at random each.")]
    public Sprite[] rockSprites;
    [Tooltip("The rocks' pieces when they land: their art's average color (set by the builder).")]
    public Color rockColor = new Color(0.35f, 0.34f, 0.40f);
    [Tooltip("A tremor's cracks (from the mined ore; small under a landed rock) and its ripples across the floor.")]
    public Sprite[] tremorCrackSprites;
    public Sprite rippleSprite;
    [Tooltip("The ore art's average color: a tremor's rubble (times Ore Color).")]
    public Color oreArtColor = Color.white;
    [Tooltip("The dusty haze at the screen's edges during a tremor (a HUD image).")]
    public Image dustHaze;

    [Header("Teleporter (opens when the depth's goal is done)")]
    [Tooltip("The swirling portal (FX_Swirl, tinted cyan).")]
    public Sprite teleporterSwirl;
    public float teleporterRadius = 1.2f;

    [Header("Effects (Kenney's white textures and generated sprites, tinted where used)")]
    public Sprite fxBurst;   // a bright burst
    public Sprite fxRing;    // a soft ring
    public Sprite fxGlint;   // a 4-point star
    public Sprite fxScorch;  // a scorch mark
    public Sprite fxBolt;    // a jagged bolt along its length
    public Sprite fxCrackle; // an electric crackle
    public Sprite fxGlow;    // a soft white blob (columns of light)
    [Tooltip("A red flash at the screen's edges when the hero is hit (a HUD image).")]
    public Image hurtFlash;

    [Header("Scene references")]
    public Player player;
    public LevelUp leveling;
    [Tooltip("On this object: the relics owned and found, and their effects.")]
    public Relics relics;
    [Tooltip("The Esc menu (Resume / How to play / Settings / Quit to title).")]
    public PauseMenu pauseMenu;
    public DamageNumbers damageNumbers;
    public DeathBursts deathBursts;
    [Tooltip("F1-F5 test keys (editor and Development builds only).")]
    public DebugKeys debugKeys;
    [Tooltip("Unlit sprite material: effects, shots and pickups use it to stay bright in the dark cave.")]
    public Material unlitMaterial;
    [Tooltip("The hit flash (HitFlash): draws a sprite's shape in white. Empty = enemies don't flash white.")]
    public Material flashMaterial;
    public Sprite xpSprite;
    public Sprite materialSprite;
    public Sprite crystalSprite;
    public Sprite goldSprite;
    [Tooltip("The crystal and gold pickups' sparkle (and the crystal's glow): their art's average colors, set by the builder.")]
    public Color crystalColor = new Color(0.51f, 0.50f, 0.84f);
    public Color goldColor = new Color(0.91f, 0.76f, 0.45f);
    [Tooltip("A variant's relic drop (a star, tinted by Pickup).")]
    public Sprite relicSprite;
    [Tooltip("Enemy shadows (a circle, squashed and darkened by Enemy).")]
    public Sprite shadowSprite;
    [Tooltip("The boss's dash warning: a white bar with a round right end, 9-sliced (Enemy stretches it).")]
    public Sprite dangerZoneSprite;

    [Header("Variant enemies")]
    public VariantSettings variants = new VariantSettings();
    [Tooltip("A red variant's fire patches (white, tinted).")]
    public Sprite fireTrailSprite;
    [Tooltip("A purple variant's blast: the pulsing core (circle) and its area / fuse (ring), white, tinted.")]
    public Sprite blastCoreSprite;
    public Sprite blastRingSprite;
    [Tooltip("Plain white square, used for simple bars (ore mining progress).")]
    public Sprite whiteSquare;
    public Camera cam;
    [Tooltip("The hero's health: the small bar under their feet (a DynamicBar health bar).")]
    public DynamicBar heroHpBar;
    public Text depthText;
    public Text quakeText;
    [Tooltip("Top-left, second line: the dive's materials, crystals and gold.")]
    public Text lootText;
    [Tooltip("Top-center: kills toward the depth's goal, BOSS, the teleporter hint, or the horde wave.")]
    [FormerlySerializedAs("timerText")] public Text goalText;
    [Tooltip("Under the goal text: progress toward the kill goal (or through the horde wave). Hidden otherwise.")]
    public GameObject goalBar;
    public RectTransform goalFill;
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
    public bool IsPlaying => enabled && !PanelOpen && !leveling.IsChoosing && !pauseMenu.IsOpen && !debugKeys.IsOpen;

    Campaign campaign;
    int depth;
    int kills;      // whole dive
    int depthKills; // this depth, toward the goal
    int depthsCleared;
    int diveMaterials;
    int diveCrystals;
    int diveGold;
    int diveSupplies;
    int diveOres;
    readonly Ticker materialsShown = new Ticker(), crystalsShown = new Ticker(), goldShown = new Ticker(),
        suppliesShown = new Ticker(); // HUD counters
    int materialBonus; // Workshop: added to every materials drop
    readonly Dictionary<EnemyData, int> diveKills = new Dictionary<EnemyData, int>();
    float spawnTimer;
    bool bossSpawned;
    bool bossDue;  // the kill goal summoned the boss; it spawns in the next Update
    bool relicDue; // a boss died; its relic is found in the next Update
    Enemy currentBoss;
    GameObject teleporter; // null = not open
    Transform teleporterPortal;
    float sparkleTimer;      // the next speck rising from the open teleporter
    float hurtAge = 99f;     // since the hero was last hit (the red at the screen's edges)

    CameraFollow cameraFollow;
    Trap trap;          // what comes after the current tremor
    float trapDelay;    // seconds until it comes (0 = no tremor going on)
    float rockRainLeft; // seconds of rock rain left
    float rockTimer;
    float hazeAge = 99f;     // since the tremor's dusty haze started (HazeEnd or more = not showing)

    static readonly Color TeleporterColor = new Color(0.4f, 0.9f, 1f, 0.8f);
    static readonly Color XpShrineColor = new Color(0.55f, 0.80f, 1f);    // light blue
    static readonly Color CrystalShrineColor = new Color(0.65f, 0.35f, 0.95f); // purple

    int KillsNeeded => killsToClear + extraKillsPerDepth * (depth - 1);
    bool IsBossDepth => depth % bossEvery == 0;
    float StatScale => 1f + statScalePerDepth * (depth - 1);

    void Awake()
    {
        Bounds = new Rect(-arenaSize / 2f, arenaSize);
        Fx.UnlitMaterial = unlitMaterial;
        HitFlash.Material = flashMaterial;
    }

    void Start()
    {
        if (leveling == null || config == null || quakeText == null || crate == null || teleporterArrow == null ||
            whiteSquare == null || crystalCluster == null || crystalSprite == null ||
            rockTargetSprite == null || dustHaze == null || hurtFlash == null || fxBurst == null || relics == null || goldSprite == null || pauseMenu == null || damageNumbers == null ||
            deathBursts == null || lootText == null || goalBar == null || debugKeys == null || heroHpBar == null)
        {
            Debug.LogError("The Arena scene is out of date. Rebuild it with Jam > Build Game Scenes.");
            enabled = false;
            return;
        }
        cameraFollow = cam.GetComponent<CameraFollow>();
        campaign = Campaign.Current ?? Campaign.StartNew(config); // also lets you press Play in this scene directly
        StartDive();
    }

    // ---------- Dive flow ----------

    // Base stats + district buildings, the Sword (and relic weapons), owned relics, level 1, nothing found yet.
    void StartDive()
    {
        ResetRun();
        NextDepth();
    }

    void ResetRun()
    {
        player.ResetForNewRun();
        campaign.ApplyLoadout(player, leveling.weapons);
        relics.ResetForNewRun(campaign.relicOwned); // after the loadout: Glass Cannon cuts the final max HP
        leveling.ResetForNewRun(campaign.evolutionUnlocked);
        leveling.XpGain = 1f + campaign.BuildingBonus(BuildingEffect.XpGain);
        materialBonus = Mathf.FloorToInt(campaign.BuildingBonus(BuildingEffect.MaterialBonus) + 0.5f); // (scaled by people left: rounded)
        kills = 0;
        diveMaterials = 0;
        diveCrystals = 0;
        diveGold = 0;
        diveSupplies = 0;
        diveOres = 0;
        diveKills.Clear();
        depth = 0;
        depthsCleared = 0;
        heroHpBar.Snap(1f); // full, without a heal flash
    }

    // HP, levels and loot carry over between depths; the hero starts each depth in the middle of a fresh cave.
    void NextDepth()
    {
        depth++;
        ClearField();
        player.transform.position = Bounds.center;
        player.ResetLook(); // no flash left over
        depthKills = 0;
        spawnTimer = 0f;
        bossSpawned = false;
        bossDue = false;
        relicDue = false;
        currentBoss = null;
        trapDelay = 0f;
        rockRainLeft = 0f;
        hazeAge = HazeEnd;
        SetHaze(0f);
        bossBar.SetActive(false);
        panel.SetActive(false);
        Time.timeScale = 1f;
        PlaceCratesAndOres();
        leveling.TryShowCards(); // level-ups earned at the end of the last depth are picked now
    }

    // Supply crates (broken by weapons for materials), crystal clusters (broken for crystals), ore veins (mined to
    // weaken the quake) and shrines (a random type each), scattered away from the middle where the hero starts.
    void PlaceCratesAndOres()
    {
        for (int i = 0; i < cratesPerDepth; i++) Enemy.Spawn(crate, RandomFloorPoint(Bounds.center, crate.radius), 1f, player, this);
        for (int i = 0; i < clustersPerDepth; i++)
            Glow.Add(Enemy.Spawn(crystalCluster, RandomFloorPoint(Bounds.center, crystalCluster.radius), 1f, player, this).gameObject, crystalColor, 4f, 1.1f); // the crystals' color
        for (int i = 0; i < oresPerDepth; i++)
            MineSpot.Place(SpotKind.Ore, oreSprite, oreColor, oreScale, oreMineTime, blastRingSprite, RandomFloorPoint(Bounds.center, SpotRadius), this);
        for (int i = 0; i < shrinesPerDepth; i++)
        {
            SpotKind kind = (SpotKind)Random.Range(1, 4); // XpShrine, MaterialsShrine or CrystalShrine
            Color color = kind == SpotKind.XpShrine ? XpShrineColor : kind == SpotKind.MaterialsShrine ? Pickup.MaterialsColor : CrystalShrineColor;
            MineSpot.Place(kind, oreSprite, color, oreScale, shrineTime, blastRingSprite, RandomFloorPoint(Bounds.center, SpotRadius), this);
        }
    }

    // A random spot in the cave at least 6 units from `awayFrom` (the hero's start, or the hero for the teleporter),
    // with room for something `radius` big (IsFree).
    Vector2 RandomFloorPoint(Vector2 awayFrom, float radius)
    {
        const float minDistance = 6f;
        Vector2 point = Vector2.zero;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            point = new Vector2(Random.Range(Bounds.xMin + 2f, Bounds.xMax - 2f), Random.Range(Bounds.yMin + 2f, Bounds.yMax - 2f));
            if ((point - awayFrom).sqrMagnitude >= minDistance * minDistance && IsFree(point, radius)) break;
        }
        return point;
    }

    const float SpotRadius = 1f;  // an ore vein's or shrine's footprint (its progress ring is 2.1 across)
    const float SpawnGap = 0.5f;  // the least space left between something new and what's already there

    // Room at `point` for something `radius` big: it wouldn't be inside a crate, cluster, enemy, ore vein, shrine or the
    // teleporter.
    bool IsFree(Vector2 point, float radius)
    {
        foreach (Enemy enemy in Enemy.All)
            if (Vector2.Distance(point, enemy.transform.position) < radius + enemy.radius + SpawnGap) return false;
        foreach (MineSpot spot in MineSpot.All)
            if (Vector2.Distance(point, spot.transform.position) < radius + SpotRadius + SpawnGap) return false;
        return teleporter == null || Vector2.Distance(point, teleporter.transform.position) >= radius + teleporterRadius + SpawnGap;
    }

    // `point` if there's room (IsFree), else the first spot with room up to 1.5 units around it in the cave (none in
    // 20 tries: `point` anyway).
    Vector2 FreeNear(Vector2 point, float radius)
    {
        Rect inside = InsideBounds;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 spot = attempt == 0 ? point : point + Random.insideUnitCircle * 1.5f;
            spot.x = Mathf.Clamp(spot.x, inside.xMin, inside.xMax);
            spot.y = Mathf.Clamp(spot.y, inside.yMin, inside.yMax);
            if (IsFree(spot, radius)) return spot;
        }
        return point;
    }

    void Update()
    {
        UpdateHud();
        UpdateArrow();
        Music.Dimmed = !IsPlaying; // half volume while paused: level-up cards, the pause menu, the panels
        if (!IsPlaying) return;

        UpdateTraps(); // traps still come after the teleporter opens

        // Set by OnEnemyKilled, done here: a weapon may still be looping over Enemy.All when the kill happens
        // (spawning would change the list mid-loop), and the relic pauses the game with a panel.
        if (bossDue)
        {
            bossDue = false;
            SpawnBoss();
        }
        if (relicDue)
        {
            relicDue = false;
            FindRelic();
            if (!IsPlaying) return; // its panel is up
        }

        // The horde keeps spawning until the teleporter opens; stepping into it clears the depth.
        if (teleporter != null)
        {
            teleporterPortal.Rotate(0f, 0f, -PortalSpin * Time.deltaTime);
            sparkleTimer -= Time.deltaTime;
            if (sparkleTimer <= 0f) // specks of light rising from it
            {
                sparkleTimer = 0.1f;
                deathBursts.Rise((Vector2)teleporter.transform.position + Random.insideUnitCircle * teleporterRadius * 0.8f, PortalSparkle);
            }
            if (((Vector2)(player.transform.position - teleporter.transform.position)).sqrMagnitude <= teleporterRadius * teleporterRadius)
                DepthCleared(); // the moment he touches it
            return; // no spawns while it's open
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = CurrentSpawnInterval();
            if (Enemy.All.Count < maxEnemies) SpawnRandomEnemy(OffscreenPoint());
        }
    }

    void SpawnBoss()
    {
        ShowBoss(Enemy.Spawn(boss, FreeNear(OffscreenPoint(), boss.radius), StatScale, player, this));
    }

    void ShowBoss(Enemy enemy)
    {
        currentBoss = enemy;
        bossName.text = enemy.data.displayName;
        bossBar.SetActive(true);
    }

    public void OnEnemyKilled(Enemy enemy)
    {
        bool isBoss = enemy.data.behavior == EnemyBehavior.Boss;
        deathBursts.Burst(enemy.transform.position, enemy.ParticleColor, enemy.radius, isBoss);
        if (isBoss) cameraFollow.Shake(0.4f, 0.3f);

        if (!IsPlaying) return; // the depth already ended this frame (e.g. several enemies died in one sweep)

        if (enemy.data.behavior == EnemyBehavior.Crate) // broken crate or crystal cluster: just its loot, not a kill
        {
            Vector2 at = enemy.transform.position;
            if (enemy.data.materialAmount > 0)
                Pickup.Drop(PickupKind.Materials, materialSprite, at, enemy.data.materialAmount + materialBonus, this);
            if (enemy.data.crystalAmount > 0)
                Pickup.Drop(PickupKind.Crystal, crystalSprite, at, enemy.data.crystalAmount, this);
            if (enemy.data.supplyAmount > 0)
                Pickup.Drop(PickupKind.Supplies, materialSprite, at + Random.insideUnitCircle * 0.3f, enemy.data.supplyAmount, this);
            return;
        }

        kills++;
        depthKills++;
        diveKills[enemy.data] = diveKills.TryGetValue(enemy.data, out int count) ? count + 1 : 1;
        relics.OnKill(); // Bloodstone

        DropLoot(enemy); // a boss's loot lands on the floor too; what's left is collected at the teleporter

        if (enemy == currentBoss)
        {
            if (teleporter == null) OpenTeleporter(); // (already open: a boss summoned with the debug key)
            relicDue = true; // every boss drops a relic
        }
        else if (!bossSpawned && teleporter == null && depthKills >= KillsNeeded)
        {
            GoalReached();
        }
    }

    // Kill goal reached: a boss depth summons its boss (next Update), a normal depth opens the teleporter.
    void GoalReached()
    {
        if (IsBossDepth)
        {
            bossSpawned = true;
            bossDue = true;
        }
        else
        {
            OpenTeleporter();
        }
    }

    // Debug (F4): the depth's kill goal is met right away (or once it's already met: nothing).
    public void DebugClearDepth()
    {
        if (bossSpawned || teleporter != null) return;
        depthKills = Mathf.Max(depthKills, KillsNeeded);
        GoalReached();
    }

    // Debug (F5): the boss walks in now (boss bar), as on a boss depth: killing it on a dive opens the teleporter (if it
    // isn't open yet) and drops a relic. The depth's own goal and boss are unchanged.
    public void DebugSummonBoss()
    {
        SpawnBoss();
    }

    // A variant's XP gem is worth variants.xpMultiplier x, and it may drop a relic (while one can still be found).
    void DropLoot(Enemy enemy)
    {
        bool variant = enemy.variant != EnemyVariant.None;
        if (variant && Random.value < variants.relicChance && relics.AnyMissing)
            Pickup.Drop(PickupKind.Relic, relicSprite, (Vector2)enemy.transform.position + Random.insideUnitCircle * 0.3f, 1, this);
        if (Random.value < enemy.data.materialChance)
            Pickup.Drop(PickupKind.Materials, materialSprite, enemy.transform.position, enemy.data.materialAmount + materialBonus, this);
        if (Random.value < enemy.data.goldChance)
            Pickup.Drop(PickupKind.Gold, goldSprite, (Vector2)enemy.transform.position + Random.insideUnitCircle * 0.3f, enemy.data.goldAmount, this);
        if (leveling.levelingEnabled && enemy.data.xp > 0)
            Pickup.Drop(PickupKind.Xp, xpSprite, (Vector2)enemy.transform.position + Random.insideUnitCircle * 0.3f,
                enemy.data.xp * (variant ? variants.xpMultiplier : 1), this);
    }

    // The depth's goal is done: spawning stops, Spitters off-screen vanish (no kill, no loot), and a teleporter opens
    // somewhere in the cave. Enemies still around keep fighting. Stepping in clears the depth.
    void OpenTeleporter()
    {
        foreach (Enemy enemy in Enemy.All)
            if (enemy.data.behavior == EnemyBehavior.Shooter && !OnScreen(enemy.transform.position)) Destroy(enemy.gameObject);

        // A swirling portal lighting up the cave around it; it opens with a burst, a ring and a flash of light.
        Vector2 at = RandomFloorPoint(player.transform.position, teleporterRadius); // (picked before it exists)
        teleporter = new GameObject("Teleporter");
        teleporter.transform.position = at;
        Glow.Add(teleporter, TeleporterColor, 5f, 1.3f);

        SpriteRenderer portal = new GameObject("Portal").AddComponent<SpriteRenderer>();
        portal.transform.SetParent(teleporter.transform, false);
        portal.transform.localScale = Vector3.one * teleporterRadius * 2.2f;
        portal.sprite = teleporterSwirl;
        portal.color = new Color(TeleporterColor.r, TeleporterColor.g, TeleporterColor.b, 0.9f);
        portal.sortingOrder = -1;
        Fx.Unlit(portal);
        teleporterPortal = portal.transform;

        FadeEffect.Circle(fxBurst, at, teleporterRadius * 1.5f, new Color(0.5f, 0.95f, 1f, 0.9f), 0.45f);
        FadeEffect.Circle(fxRing, at, teleporterRadius * 1.8f, new Color(0.5f, 0.95f, 1f, 0.9f), 0.5f, 0.2f);
        FadeEffect.Circle(fxGlint, at, teleporterRadius * 1.6f, new Color(0.85f, 1f, 1f, 1f), 0.35f, 0.3f);
    }

    const float PortalSpin = 90f; // degrees per second
    static readonly Color PortalSparkle = new Color(0.75f, 0.97f, 1f);

    // The hero took a hit (Player.TakeDamage): a jolt, red at the screen's edges, chips in his colors, the damage in red.
    public void OnHeroHit(int amount)
    {
        cameraFollow.Shake(0.15f, 0.15f);
        hurtAge = 0f;
        deathBursts.Burst(player.transform.position, player.particleColor, 0.4f, false);
        damageNumbers.Show((Vector2)player.transform.position + Vector2.up * 1.3f, $"-{amount}", HeroDamageColor);
    }

    const float HurtTime = 0.35f, HurtAlpha = 0.5f;
    static readonly Color HeroDamageColor = new Color(1f, 0.35f, 0.3f);

    public bool OnScreen(Vector2 worldPosition)
    {
        Vector3 viewport = cam.WorldToViewportPoint(worldPosition);
        return viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    // Stepped into the teleporter: everything still on the floor is collected, then: go deeper or go home.
    void DepthCleared()
    {
        depthsCleared++;
        int foundMaterials = 0;
        int foundCrystals = 0;
        int foundGold = 0;
        int foundSupplies = 0;
        int xp = 0;
        foreach (Pickup pickup in Pickup.All)
        {
            if (pickup.kind == PickupKind.Materials) foundMaterials += pickup.amount;
            else if (pickup.kind == PickupKind.Crystal) foundCrystals += pickup.amount;
            else if (pickup.kind == PickupKind.Gold) foundGold += pickup.amount;
            else if (pickup.kind == PickupKind.Supplies) foundSupplies += pickup.amount;
            else if (pickup.kind == PickupKind.Relic) relics.TryFind(out _); // listed with the relics found below
            else xp += pickup.amount;
        }
        diveMaterials += foundMaterials;
        diveCrystals += foundCrystals;
        diveGold += foundGold;
        diveSupplies += foundSupplies;

        string relicLine = "";
        if (relics.FoundThisDive.Count > 0)
        {
            List<string> names = new List<string>();
            foreach (RelicId relic in relics.FoundThisDive) names.Add(Relics.Names[(int)relic]);
            relicLine = $"\nRelics: {string.Join(", ", names)}";
        }

        ShowPanel($"Depth {depth} cleared",
            $"Carrying {diveMaterials} materials · {diveCrystals} crystals · {diveGold} gold · {diveSupplies} supplies" + relicLine,
            "Go deeper", NextDepth, "Return to town", () => EndDive(true));
        leveling.AddXp(xp); // the cards wait until "Go deeper"
    }

    // A boss died: one random relic you don't have yet. It works right away; the game pauses to show it.
    void FindRelic()
    {
        if (!relics.TryFind(out RelicId relic)) return; // you have them all
        ShowNotice($"RELIC: {Relics.Names[(int)relic]}", relics.Describe(relic));
    }

    public void OnPlayerDied()
    {
        if (relics.TrySecondWind()) return; // back up, the fight goes on

        ShowPanel("DEFEATED", "The loot is lost.", "Return to town", () => EndDive(false));
    }

    public void Collect(PickupKind kind, int amount)
    {
        if (kind == PickupKind.Materials) diveMaterials += amount;
        else if (kind == PickupKind.Crystal) diveCrystals += amount;
        else if (kind == PickupKind.Gold) diveGold += amount;
        else if (kind == PickupKind.Supplies) diveSupplies += amount;
        else if (kind == PickupKind.Relic)
        {
            // A relic you don't have yet, working right away, shown as a card (pauses the game)
            if (relics.TryFind(out RelicId relic)) leveling.ShowRelic(Relics.Names[(int)relic], relics.Describe(relic));
        }
        else leveling.AddXp(amount);
    }

    // An ore or a shrine at `at` was used up (MineSpot). Shrine loot goes straight into the dive's loot (lost if the hero
    // dies).
    public void SpotMined(SpotKind kind, Vector2 at)
    {
        switch (kind)
        {
            case SpotKind.Ore:
                diveOres++;
                Tremor(at);
                break;
            case SpotKind.XpShrine:
                leveling.AddLevelShare(shrineXpShare);
                break;
            case SpotKind.MaterialsShrine:
                diveMaterials += shrineMaterials + shrineMaterialsPerDepth * (depth - 1);
                break;
            case SpotKind.CrystalShrine:
                diveCrystals += shrineCrystals + shrineCrystalsPerDepth * (depth - 1);
                break;
        }
    }

    // ---------- Tremors and traps ----------

    // A destroyed ore (at `at`) shakes the cave: rubble, ripples and cracks spread from the spot (TremorEffect) and a
    // dusty haze fills the screen's edges; when the shaking ends: nothing, an ambush or a rock rain (equal chance).
    void Tremor(Vector2 at)
    {
        cameraFollow.Shake(tremorTime, tremorStrength);
        Sprite cracks = tremorCrackSprites.Length > 0 ? tremorCrackSprites[Random.Range(0, tremorCrackSprites.Length)] : null;
        TremorEffect.Spawn(at, cracks, rippleSprite, oreArtColor * oreColor, deathBursts);
        hazeAge = 0f;
        trap = (Trap)Random.Range(0, 3);
        trapDelay = tremorTime;
    }

    const float HazeIn = 0.3f, HazeOut = 0.6f, HazeAlpha = 0.55f; // the haze: fades in, holds, fades out after the shake
    float HazeEnd => tremorTime + HazeOut;

    void UpdateHaze()
    {
        if (hazeAge >= HazeEnd) return;
        hazeAge += Time.deltaTime;
        float fadeOut = 1f - Mathf.Clamp01((hazeAge - (HazeEnd - HazeOut)) / HazeOut);
        SetHaze(HazeAlpha * Mathf.Clamp01(hazeAge / HazeIn) * fadeOut);
    }

    void SetHaze(float alpha)
    {
        Color color = dustHaze.color;
        color.a = alpha;
        dustHaze.color = color;
    }

    void UpdateTraps()
    {
        UpdateHaze();
        if (trapDelay > 0f)
        {
            trapDelay -= Time.deltaTime;
            if (trapDelay <= 0f)
            {
                if (trap == Trap.Ambush) Ambush();
                else if (trap == Trap.RockRain) rockRainLeft = rockRainTime;
            }
        }

        if (rockRainLeft > 0f)
        {
            rockRainLeft -= Time.deltaTime;
            rockTimer -= Time.deltaTime;
            if (rockTimer <= 0f)
            {
                rockTimer = rockInterval;
                FallingRock.Drop(RandomScreenPoint(), rockRadius, rockWarning, Mathf.RoundToInt(rockDamage * StatScale), this);
            }
        }
    }

    // Enemies (the normal spawn mix) evenly spaced in a ring just outside the view, closing in from every side.
    // Near a wall, the ring's points are pulled back inside the cave.
    void Ambush()
    {
        int count = ambushEnemies + ambushEnemiesPerDepth * (depth - 1);
        float halfHeight = cam.orthographicSize + 1f;
        float halfWidth = cam.orthographicSize * cam.aspect + 1f;
        Vector2 center = cam.transform.position;
        Rect inside = InsideBounds;
        float turn = Random.value * Mathf.PI * 2f; // a different ring every time

        for (int i = 0; i < count && Enemy.All.Count < maxEnemies; i++)
        {
            float angle = turn + Mathf.PI * 2f * i / count;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            // Out from the center until the view's edge (plus 1) on whichever side comes first.
            float reachX = direction.x != 0f ? halfWidth / Mathf.Abs(direction.x) : float.MaxValue;
            float reachY = direction.y != 0f ? halfHeight / Mathf.Abs(direction.y) : float.MaxValue;
            Vector2 point = center + direction * Mathf.Min(reachX, reachY);
            point.x = Mathf.Clamp(point.x, inside.xMin, inside.xMax);
            point.y = Mathf.Clamp(point.y, inside.yMin, inside.yMax);
            SpawnRandomEnemy(point);
        }
    }

    // A short camera jolt (e.g. the Quake Hammer's slam).
    public void ShakeCamera(float duration, float strength)
    {
        cameraFollow.Shake(duration, strength);
    }

    // A random spot in the camera view, inside the cave.
    Vector2 RandomScreenPoint()
    {
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Rect inside = InsideBounds;
        Vector2 point = (Vector2)cam.transform.position + new Vector2(Random.Range(-halfWidth, halfWidth), Random.Range(-halfHeight, halfHeight));
        point.x = Mathf.Clamp(point.x, inside.xMin, inside.xMax);
        point.y = Mathf.Clamp(point.y, inside.yMin, inside.yMax);
        return point;
    }

    // Back to town: the campaign takes the run's result (the loot, or not), the day passes, and the Town scene loads
    // (it shows the result screen first).
    void EndDive(bool survived, string nextScene = null)
    {
        campaign.RecordDive(kills, depth, diveOres);
        RunResult run = new RunResult
        {
            survived = survived,
            depth = depth,
            depthsCleared = depthsCleared,
            level = leveling.Level,
            materials = diveMaterials,
            crystals = diveCrystals,
            gold = diveGold,
            supplies = diveSupplies,
            ores = diveOres,
            totalKills = kills,
        };
        foreach (KeyValuePair<EnemyData, int> pair in diveKills) run.kills[pair.Key] = pair.Value;
        run.relics.AddRange(relics.FoundThisDive);
        foreach (Weapon weapon in leveling.weapons)
            if (weapon.enabled && weapon.evolved) run.evolutions.Add(weapon.EvolutionName);
        campaign.EndDive(run);
        Time.timeScale = 1f;
        if (nextScene != null) SceneTransition.Load(nextScene); // (the title screen)
        else SceneTransition.Load(townScene, SceneTransition.Style.Fade, $"Day {campaign.day}", $"Quake on day {campaign.NextQuakeDay}");
    }

    // Pause menu > Quit to title. Counts as dying, so it can't be used to retry a bad dive: the dive's loot is lost and
    // a day passes.
    public void QuitToTitle()
    {
        EndDive(false, titleScene);
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

    // A weighted pick from the spawn table, at `position`.
    void SpawnRandomEnemy(Vector2 position)
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
        if (pick != null) Enemy.Spawn(pick, FreeNear(position, pick.radius), StatScale, player, this, RollVariant(pick));
    }

    // Maybe a variant (variants.ChanceAt(depth)): blue, red or purple, equally likely; Spitters (they never move, so no
    // trail) are never red. Never crates or bosses.
    EnemyVariant RollVariant(EnemyData data)
    {
        if (data.behavior == EnemyBehavior.Crate || data.behavior == EnemyBehavior.Boss) return EnemyVariant.None;
        if (Random.value >= variants.ChanceAt(depth)) return EnemyVariant.None;
        if (data.behavior == EnemyBehavior.Shooter) return Random.value < 0.5f ? EnemyVariant.Tough : EnemyVariant.Volatile;
        return (EnemyVariant)Random.Range(1, 4); // Tough, Burning or Volatile
    }

    // The cave minus a 1-unit margin along the walls (where enemies and rocks may appear).
    Rect InsideBounds => new Rect(Bounds.xMin + 1f, Bounds.yMin + 1f, Bounds.width - 2f, Bounds.height - 2f);

    // A point on a rectangle just outside the camera view, inside the arena, so enemies don't pop in on screen.
    Vector2 OffscreenPoint()
    {
        float halfHeight = cam.orthographicSize + 1f;
        float halfWidth = cam.orthographicSize * cam.aspect + 1f;
        Vector2 center = cam.transform.position;
        Rect inside = InsideBounds;

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

    // Destroy every enemy, crate, shot, dagger, axe, fire bomb, variant hazard, pickup, ore, shrine, falling rock and the
    // teleporter (lists empty at the end of the frame; enemies' and spots' now, so the new depth's crates and ores don't
    // keep away from the old ones: IsFree), and hide the damage numbers and death bursts.
    void ClearField()
    {
        if (teleporter != null) Destroy(teleporter);
        teleporter = null;
        foreach (Enemy enemy in Enemy.All) Destroy(enemy.gameObject);
        foreach (Projectile projectile in Projectile.All) Destroy(projectile.gameObject);
        foreach (Dagger dagger in Dagger.All) Destroy(dagger.gameObject);
        foreach (Axe axe in Axe.All) Destroy(axe.gameObject);
        foreach (FireBomb bomb in FireBomb.All) Destroy(bomb.gameObject);
        foreach (Hazard hazard in Hazard.All) Destroy(hazard.gameObject);
        foreach (Pickup pickup in Pickup.All) Destroy(pickup.gameObject);
        foreach (MineSpot spot in MineSpot.All) Destroy(spot.gameObject);
        foreach (FallingRock rock in FallingRock.All) Destroy(rock.gameObject);
        foreach (TremorEffect tremor in TremorEffect.All) Destroy(tremor.gameObject);
        Enemy.All.Clear();
        MineSpot.All.Clear();
        damageNumbers.Clear();
        deathBursts.Clear();
    }

    // ---------- UI ----------

    void UpdateHud()
    {
        heroHpBar.Set((float)player.Hp / player.maxHp);
        hurtAge += Time.unscaledDeltaTime;
        Color hurt = hurtFlash.color;
        hurt.a = HurtAlpha * Mathf.Clamp01(1f - hurtAge / HurtTime);
        hurtFlash.color = hurt;

        float goal = -1f; // the goal bar's fill; below 0 = hidden
        float dt = Time.unscaledDeltaTime; // the loot counters roll up toward the real numbers
        lootText.text = $"Materials {materialsShown.Next(diveMaterials, dt)}     Crystals {crystalsShown.Next(diveCrystals, dt)}     " +
                        $"Gold {goldShown.Next(diveGold, dt)}     Supplies {suppliesShown.Next(diveSupplies, dt)}";
        depthText.text = $"Day {campaign.day}     {(IsBossDepth ? $"Depth {depth} (boss)" : $"Depth {depth}")}     Kills {kills}";
        float oreDrop = diveOres * config.orePercent;
        quakeText.text = $"Quake day {campaign.NextQuakeDay}: {campaign.QuakeForecast:0}%     Ores {diveOres} (-{oreDrop:0}%)";

        goalText.text = teleporter != null ? "Teleporter open!" : bossSpawned ? "BOSS" : $"Kills {depthKills} / {KillsNeeded}";
        if (teleporter == null && !bossSpawned) goal = (float)depthKills / KillsNeeded;

        // The boss bar takes the goal bar's place.
        bool showGoal = goal >= 0f && !bossBar.activeSelf;
        goalBar.SetActive(showGoal);
        if (showGoal) SetFill(goalFill, goal);
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
        teleporterArrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f); // the arrow sprite points up
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

    // Pauses the game with a message and a Continue button, without clearing the cave: the fight goes on after.
    void ShowNotice(string title, string body)
    {
        Time.timeScale = 0f;
        panel.SetActive(true);
        panelTitle.text = title;
        panelBody.text = body;
        SetButton(primaryButton, "Continue", () =>
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            leveling.TryShowCards(); // a level-up that waited for the panel
        });
        secondaryButton.gameObject.SetActive(false);
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
