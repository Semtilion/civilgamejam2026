using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Arena.unity (called from Jam > Build Game Scenes): cave (tileset room + decorations, or the
// placeholder floor/walls), hero with every weapon, camera, HUD, teleporter arrow, pause panel, leveling UI and the
// ArenaManager, all wired up.
public static class ArenaSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Arena.unity";
    const float TorchHeight = 1.6f; // world units: the artist's torch (its whole image)
    static readonly Color HpColor = new Color(0.80f, 0.15f, 0.15f);
    static readonly Color TrailColor = new Color(1f, 0.88f, 0.55f); // a health bar's trail: what a hit just took
    static readonly Vector2 HeroBarSize = new Vector2(120, 14);    // canvas units (100 = 1 world unit)
    const float TorchFlameY = 0.5f; // its flame, above the torch's center

    public static void Build(GameAssets a)
    {
        var scene = NewSceneFromTemplate(ScenePath);

        ArenaManager arena = new GameObject("Arena Manager").AddComponent<ArenaManager>();
        Vector2 size = arena.arenaSize;

        // Cave (visual only: the hero and enemies are kept inside the cave in code): the artist's painted cave if it's in
        // Assets/Art/Cave, else the test tileset (Assets/Art/Tiles) as a walled room, else placeholder floor/walls.
        if (BuildPaintedCave(size) || BuildTiledRoom(size))
        {
            PlaceDecorations(size);
        }
        else
        {
            float wall = ArenaManager.WallThickness;
            NewSprite("Floor", a.square, Vector2.zero, size, new Color(0.16f, 0.13f, 0.12f), -10);
            Color wallColor = new Color(0.35f, 0.30f, 0.28f);
            NewSprite("Wall Top", a.square, new Vector2(0f, (size.y + wall) / 2f), new Vector2(size.x + wall * 2f, wall), wallColor, -5);
            NewSprite("Wall Bottom", a.square, new Vector2(0f, -(size.y + wall) / 2f), new Vector2(size.x + wall * 2f, wall), wallColor, -5);
            NewSprite("Wall Left", a.square, new Vector2(-(size.x + wall) / 2f, 0f), new Vector2(wall, size.y), wallColor, -5);
            NewSprite("Wall Right", a.square, new Vector2((size.x + wall) / 2f, 0f), new Vector2(wall, size.y), wallColor, -5);
        }

        // Hero with every weapon. Which ones are owned (enabled) and their levels come from the town at dive start.
        // Hero root = Player + weapons (scale 1; its position is the hit center). The art lives in a "Body" child
        // (the artist's hero if it's in Assets/Art/Hero, otherwise a blue placeholder circle) so the walk bob, lean and
        // breathing never move the weapon flashes. The art's pivot is at the feet, so the body sits half its height down.
        // A soft shadow child sits under the feet.
        bool hasArt = a.heroIdle != null;
        float feetY = hasArt ? -HeroHeight / 2f : -0.5f;
        GameObject hero = new GameObject("Hero");
        SpriteRenderer shadow = NewSprite("Shadow", a.circle, new Vector2(0f, feetY), new Vector2(1.1f, 0.35f), new Color(0f, 0f, 0f, 0.35f), -1);
        shadow.transform.SetParent(hero.transform, false);
        SpriteRenderer body = NewSprite("Body", hasArt ? a.heroIdle : a.circle, new Vector2(0f, hasArt ? feetY : 0f), Vector2.one,
            hasArt ? Color.white : new Color(0.30f, 0.55f, 0.95f), 5);
        body.transform.SetParent(hero.transform, false);
        body.gameObject.AddComponent<SpriteOutline>(); // black outline, like enemies, ores and pickups
        HitFlash bodyFlash = body.gameObject.AddComponent<HitFlash>(); // red when hit, his moments' colors
        // A small health bar just under the feet: a world-space canvas on the hero, so it follows; over everything in
        // the cave, and unlit (UI).
        RectTransform barCanvas = new GameObject("Health Bar", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
        barCanvas.SetParent(hero.transform, false);
        barCanvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        barCanvas.GetComponent<Canvas>().sortingOrder = 7;
        barCanvas.sizeDelta = HeroBarSize;
        barCanvas.localScale = Vector3.one * 0.01f;
        barCanvas.localPosition = new Vector3(0f, feetY - 0.3f, 0f);
        RectTransform heroBar = NewBar("Bar", barCanvas, Vector2.zero, HeroBarSize, HpColor, out RectTransform heroFill);
        DynamicBar heroHealth = HealthBar(heroBar, heroFill);
        foreach (RectTransform part in new[] { heroFill, heroHealth.trail }) // inset: the dark back is its border
        {
            part.offsetMin = new Vector2(2f, 2f);
            part.offsetMax = new Vector2(-2f, -2f);
        }
        Player player = hero.AddComponent<Player>();
        player.arena = arena;
        player.sprite = body;
        player.bodyFlash = bodyFlash;
        player.particleColor = AverageColor(body.sprite) * body.color; // his chips when hit
        player.body = body.transform;
        player.idleSprite = a.heroIdle;
        player.walkSprites = a.heroWalk;

        //                      weapon                                       id                    card text                                                          bonus text             dmg +lvl interval
        SwordSweep sword    = SetWeapon(hero.AddComponent<SwordSweep>(),   WeaponId.Sword,        "Hits all around you.",                                             "+radius",        10, 3, 1.0f);
        DaggerThrow dagger  = SetWeapon(hero.AddComponent<DaggerThrow>(),  WeaponId.Dagger,       "Throws at the nearest enemy.",                                     "+1 dagger",   8, 2, 1.2f);
        SpearThrust spear   = SetWeapon(hero.AddComponent<SpearThrust>(),  WeaponId.Spear,        "Long thrust at the nearest enemy.",                                  "+reach",      12, 3, 1.3f);
        CrystalOrbit orbit  = SetWeapon(hero.AddComponent<CrystalOrbit>(), WeaponId.CrystalOrbit, "Crystals circle you.",                                              "+1 crystal",            8, 2, 0.5f);
        // Relic weapons (equipped only once you own the relic)
        SwordSweep hammer    = SetWeapon(hero.AddComponent<SwordSweep>(),    WeaponId.QuakeHammer,    "Heavy slam all around you.",                                              "+radius", 20, 4, 2.5f);
        LightningStaff staff = SetWeapon(hero.AddComponent<LightningStaff>(), WeaponId.LightningStaff, "Bolts strike random enemies.",                                           "+1 bolt",     15, 3, 1.2f);
        FireBombThrow bomb   = SetWeapon(hero.AddComponent<FireBombThrow>(),  WeaponId.FireBomb,       "Leaves a burning patch.",                                                 "+bigger fire", 6, 2, 2.0f);
        AxeThrow axe         = SetWeapon(hero.AddComponent<AxeThrow>(),       WeaponId.Axe,            "Spins out and back.",                                                     "+1 axe",      18, 4, 2.0f);
        // Weapon looks: Kenney Particle Pack textures (Assets/Art/Effects, white, tinted here or by the weapon); the old
        // placeholder shapes if a file is missing.
        Sprite Fx(string name, Sprite fallback) => EffectSprite(name) ?? fallback;
        sword.sweepVisual = NewFlashSprite("Slash", Fx("FX_Swirl", a.arc), hero.transform, a.unlit); // spins once around the hero
        dagger.daggerSprite = Fx("FX_Blade", a.square);
        spear.thrustVisual = NewFlashSprite("Thrust", Fx("FX_Streak", a.square), hero.transform, a.unlit);
        spear.tipVisual = NewFlashSprite("Thrust Tip", Fx("FX_Glint", a.circle), hero.transform, a.unlit);
        orbit.crystalSprite = CrystalSprite() ?? a.circle;
        orbit.glowColor = AverageColor(orbit.crystalSprite); // its glow and Shatter burst
        orbit.burstSprite = Fx("FX_Burst", a.circle);
        orbit.burstRingSprite = Fx("FX_Ring", a.ring);
        // Evolution cards (after Lv 5; the names are Weapon.EvolutionNames)
        sword.evolutionText = "a back-swing after every swing";
        dagger.evolutionText = $"{dagger.bounceChance * 100f:0}% chance to bounce (up to {dagger.maxBounces}x)";
        spear.evolutionText = $"pins enemies for {spear.pinTime:0.#} s";
        orbit.evolutionText = $"{orbit.shatterChance * 100f:0}% chance to burst on hit";
        a.evolutionTexts = new string[Weapon.Names.Length]; // the town's evolution popup shows them too
        foreach (Weapon weapon in new Weapon[] { sword, dagger, spear, orbit }) a.evolutionTexts[(int)weapon.id] = weapon.evolutionText;
        hammer.radius = 3.5f;
        hammer.bonusRadius = 0.5f;
        hammer.knockback = 10f;
        hammer.style = SwordSweep.SweepStyle.Shockwave;
        // its slam cracks the ground: the crack patterns baked by Tools/bake_cracks.py (Assets/Art/Effects)
        hammer.crackSprites = Enumerable.Range(0, 4).Select(i => EffectSprite($"QuakeCracks_{i}")).Where(s => s != null).ToArray();

        // The cave is dark: a soft light follows the hero
        NewLight("Hero Light", hero.transform, new Color(1f, 0.92f, 0.80f), 9f, 0.75f);
        staff.boltSprite = Fx("FX_Bolt", a.square);
        staff.impactSprite = Fx("FX_Crackle", a.circle);
        bomb.bombSprite = ItemSprite("Bomb", 1f) ?? Fx("FX_Orb", a.circle); // the artist's bomb, centered on its ball
        bomb.fireSprite = Fx("FX_Fire", a.circle);
        bomb.scorchSprite = EffectSprite("FX_Scorch");
        bomb.shardColor = AverageColor(bomb.bombSprite); // its shards when it breaks
        axe.axeSprite = ItemSprite("Axe", 1f) ?? Fx("FX_Whirl", a.square); // the artist's axe, 1 unit tall
        foreach (Weapon weapon in new Weapon[] { dagger, spear, orbit, hammer, staff, bomb, axe }) weapon.enabled = false;

        // Camera from the template
        Camera camera = Object.FindAnyObjectByType<Camera>(); // the template has exactly one
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        // The template's global light, dimmed to a cold dark: torches, the hero's light and glows do the rest
        Light2D globalLight = Object.FindObjectsByType<Light2D>().FirstOrDefault(l => l.lightType == Light2D.LightType.Global);
        if (globalLight != null)
        {
            globalLight.intensity = 0.35f;
            globalLight.color = new Color(0.70f, 0.75f, 1f);
        }
        CameraFollow follow = camera.gameObject.AddComponent<CameraFollow>();
        follow.target = hero.transform;
        follow.arena = arena;

        // ---------- Effects ----------
        // Damage numbers: their own canvas, drawn below the HUD
        Transform numbersCanvas = NewCanvas("Damage Numbers", -1, eventSystem: false);
        DamageNumbers damageNumbers = numbersCanvas.gameObject.AddComponent<DamageNumbers>();
        damageNumbers.cam = camera;
        damageNumbers.font = TitleFont;
        DeathBursts deathBursts = new GameObject("Death Bursts").AddComponent<DeathBursts>();
        deathBursts.pieceSprite = a.circle;
        deathBursts.sparkSprite = EffectSprite("FX_Glint"); // the impact spark on every hit
        deathBursts.material = a.unlit;
        arena.damageNumbers = damageNumbers;
        arena.deathBursts = deathBursts;

        // ---------- HUD ----------
        Transform canvas = NewCanvas("HUD");
        Color backing = new Color(0.05f, 0.04f, 0.07f, 0.6f); // dark, so the text reads over the floor

        // A tremor's dusty haze at the screen's edges (behind the rest of the HUD; shown by ArenaManager)
        Image dustHaze = NewImage("Dust Haze", canvas, Vector2.zero, Vector2.zero, new Color(0.43f, 0.36f, 0.29f, 0f));
        Stretch(dustHaze.rectTransform);
        dustHaze.sprite = EffectSprite("DustHaze");
        dustHaze.raycastTarget = false;
        arena.dustHaze = dustHaze;
        // A red flash at the screen's edges when the hero is hit
        Image hurtFlash = NewImage("Hurt Flash", canvas, Vector2.zero, Vector2.zero, new Color(0.75f, 0.08f, 0.08f, 0f));
        Stretch(hurtFlash.rectTransform);
        hurtFlash.sprite = EffectSprite("DustHaze"); // (white at the edges, clear in the middle)
        hurtFlash.raycastTarget = false;
        arena.hurtFlash = hurtFlash;

        // Top-left: day / depth / kills, then the dive's loot (the hero's health is the bar under their feet)
        Image topLeft = NewImage("Top-Left Backing", canvas, new Vector2(20, -20), new Vector2(620, 100), backing);
        Pin(topLeft.rectTransform, new Vector2(0f, 1f));
        AddFrame(topLeft.transform, HudFrameThickness, FrameColor);
        Text depthText = Shadowed(NewText("Depth", canvas, new Vector2(40, -36), new Vector2(600, 36), 26, TextAnchor.MiddleLeft));
        Pin(depthText.rectTransform, new Vector2(0f, 1f));
        Text lootText = Shadowed(NewText("Loot", canvas, new Vector2(40, -72), new Vector2(600, 34), 24, TextAnchor.MiddleLeft));
        Pin(lootText.rectTransform, new Vector2(0f, 1f));

        // Bottom-left (above the level): quake meter + ores mined this dive
        Image bottomLeft = NewImage("Bottom-Left Backing", canvas, new Vector2(14, 78), new Vector2(460, 44), backing); // fits its longest line
        Pin(bottomLeft.rectTransform, new Vector2(0f, 0f));
        AddFrame(bottomLeft.transform, HudFrameThickness * 2f, FrameColor); // a thin strip: a thinner frame
        Text quakeText = Shadowed(NewText("Quake", canvas, new Vector2(24, 84), new Vector2(440, 34), 24, TextAnchor.MiddleLeft));
        Pin(quakeText.rectTransform, new Vector2(0f, 0f));

        // Top-center: kill goal + its progress bar, boss bar
        Text goalText = InTitleFont(Shadowed(NewText("Goal", canvas, new Vector2(0, -25), new Vector2(600, 64), 48, TextAnchor.MiddleCenter)));
        Pin(goalText.rectTransform, new Vector2(0.5f, 1f));
        RectTransform goalBar = NewBar("Goal Bar", canvas, new Vector2(0, -92), new Vector2(460, 12), AccentColor, out RectTransform goalFill);
        Pin(goalBar, new Vector2(0.5f, 1f));
        RectTransform bossBar = NewBar("Boss Bar", canvas, new Vector2(0, -130), new Vector2(800, 22), new Color(0.90f, 0.45f, 0.20f), out RectTransform bossFill);
        Pin(bossBar, new Vector2(0.5f, 1f));
        Text bossName = InTitleFont(Shadowed(NewText("Boss Name", bossBar, new Vector2(0, 26), new Vector2(800, 30), 24, TextAnchor.MiddleCenter)));

        // Edge arrow pointing at the teleporter while it's off-screen (ArenaManager moves and rotates it)
        // (a drawn triangle, not a text glyph: the "▲" didn't show in the game's font)
        Image arrow = NewImage("Teleporter Arrow", canvas, Vector2.zero, new Vector2(56, 56), new Color(0.4f, 0.9f, 1f));
        arrow.sprite = a.arrow;
        arrow.raycastTarget = false;
        Outline arrowEdge = arrow.gameObject.AddComponent<Outline>();
        arrowEdge.effectColor = Color.black;
        arrowEdge.effectDistance = new Vector2(2f, -2f);
        arrow.gameObject.SetActive(false);

        // Center: pause panel (depth cleared / defeat / victory)
        Image panel = NewPanel("Panel", canvas, Vector2.zero, new Vector2(1000, 440));
        Text panelTitle = NewText("Title", panel.transform, new Vector2(0, 140), new Vector2(950, 80), 56, TextAnchor.MiddleCenter);
        Title(panelTitle);
        Text panelBody = NewText("Body", panel.transform, new Vector2(0, 5), new Vector2(900, 180), 24, TextAnchor.MiddleCenter);
        RectTransform buttonRow = NewRow("Buttons", panel.transform, new Vector2(0, -140), new Vector2(900, 90), 40);
        Button primaryButton = NewButton("Primary", buttonRow, new Vector2(320, 80));
        Button secondaryButton = NewButton("Secondary", buttonRow, new Vector2(320, 80));
        AddPopIn(panel);
        panel.gameObject.SetActive(false);

        // Bottom: XP bar + level (hidden if Leveling Enabled is unticked)
        RectTransform xpHud = NewUI("XP HUD", canvas, Vector2.zero, Vector2.zero);
        Stretch(xpHud);
        RectTransform xpBar = NewBar("XP Bar", xpHud, new Vector2(0, 16), new Vector2(1880, 16), new Color(0.30f, 0.90f, 1f), out RectTransform xpFill);
        Pin(xpBar, new Vector2(0.5f, 0f));
        DynamicBar xpDynamic = xpBar.gameObject.AddComponent<DynamicBar>();
        xpDynamic.kind = DynamicBar.Kind.Experience;
        xpDynamic.fill = xpFill;
        Text levelText = Shadowed(NewText("Level", xpHud, new Vector2(24, 40), new Vector2(200, 40), 28, TextAnchor.MiddleLeft));
        Pin(levelText.rectTransform, new Vector2(0f, 0f));

        // Center: level-up cards (drawn above everything else)
        Image cardPanel = NewPanel("Level Up Panel", canvas, Vector2.zero, new Vector2(1320, 560));
        Text levelUpTitle = NewText("Title", cardPanel.transform, new Vector2(0, 220), new Vector2(1200, 80), 56, TextAnchor.MiddleCenter);
        levelUpTitle.text = "LEVEL UP!";
        Title(levelUpTitle);
        RectTransform cardRow = NewRow("Cards", cardPanel.transform, new Vector2(0, -40), new Vector2(1260, 360), 30);
        Button[] cardButtons = new Button[3];
        Text[] cardTitles = new Text[3];
        Text[] cardDescriptions = new Text[3];
        for (int i = 0; i < cardButtons.Length; i++)
        {
            Image card = NewImage($"Card {i + 1}", cardRow, Vector2.zero, new Vector2(390, 340), Color.white);
            cardButtons[i] = card.gameObject.AddComponent<Button>();
            cardTitles[i] = InTitleFont(NewText("Title", card.transform, new Vector2(0, 110), new Vector2(370, 80), 30, TextAnchor.MiddleCenter));
            cardDescriptions[i] = NewText("Description", card.transform, new Vector2(0, -40), new Vector2(350, 200), 24, TextAnchor.MiddleCenter);
            StyleButton(cardButtons[i], card, cardTitles[i], CardFrameThickness); // highlights on hover
        }
        AddPopIn(cardPanel);
        cardPanel.gameObject.SetActive(false);

        // Esc pause menu, with How to play and Settings on top of it (last: drawn above everything)
        PauseMenu pauseMenu = MenuBuilder.Pause(canvas, arena);
        pauseMenu.howToPlay = MenuBuilder.HowToPlayPopup(canvas);
        pauseMenu.settings = MenuBuilder.Settings(canvas);
        arena.pauseMenu = pauseMenu;
        arena.debugKeys = MenuBuilder.DebugKeysUI(canvas, arena, null, new Vector2(1f, 1f)); // hint top-right
        MenuBuilder.TransitionUI(a.glow); // scene changes (for when the game starts in this scene)
        MenuBuilder.MusicPlayer("HyperUltraRacing.wav"); // Hyper Ultra Racing (AugustUltraAmbience)
        MenuBuilder.SoundPlayer(); // (for when the game starts in this scene)

        // ---------- Leveling ----------
        LevelUp leveling = new GameObject("Leveling").AddComponent<LevelUp>();
        leveling.weapons = new Weapon[] { sword, dagger, spear, orbit, hammer, staff, bomb, axe };
        leveling.player = player;
        leveling.xpHud = xpHud.gameObject;
        leveling.xpBar = xpDynamic;
        leveling.levelText = levelText;
        // "LEVEL UP!" over the hero for a moment before the cards (LevelUp moves it)
        Text levelUpText = InTitleFont(Shadowed(NewText("Level Up Text", canvas, Vector2.zero, new Vector2(600, 90), 56, TextAnchor.MiddleCenter)));
        levelUpText.text = "LEVEL UP!";
        levelUpText.color = new Color(1f, 0.88f, 0.45f);
        levelUpText.gameObject.SetActive(false);
        leveling.levelUpText = levelUpText;
        leveling.cardPanel = cardPanel.gameObject;
        leveling.panelTitle = levelUpTitle;
        leveling.cardButtons = cardButtons;
        leveling.cardTitles = cardTitles;
        leveling.cardDescriptions = cardDescriptions;

        // ---------- Wire the arena manager ----------
        arena.config = a.townConfig;
        arena.spawnTable = new[]
        {
            new SpawnEntry { enemy = a.crawler, weight = 70f },
            new SpawnEntry { enemy = a.spitter, weight = 15f },
            new SpawnEntry { enemy = a.brute, weight = 15f, depth1Multiplier = 1f / 3f }, // rarer on depth 1
        };
        arena.boss = a.colossus; // every 5th depth
        arena.crate = a.crate;
        arena.crystalCluster = a.crystalCluster;
        // Ores: the artist's crystal vein; without it the tileset's stone pillar, or a tinted, slightly bigger
        // placeholder square.
        Sprite oreArt = OreSprite();
        if (oreArt == null) oreArt = PropSprite("Prop_Pillar");
        arena.oreSprite = oreArt != null ? oreArt : a.square;
        arena.oreColor = oreArt != null ? Color.white : new Color(0.45f, 0.55f, 0.75f);
        arena.oreScale = oreArt != null ? 1f : 1.2f;
        arena.whiteSquare = a.square;
        arena.rockTargetSprite = a.circle;
        // The tremor's art (Tools/bake_tremor.py, Assets/Art/Effects): the rocks, the cracks, the ripples
        arena.rockSprites = Enumerable.Range(0, 3).Select(i => EffectSprite($"CaveRock_{i}")).Where(s => s != null).ToArray();
        arena.rockColor = AverageColor(arena.rockSprites);
        arena.tremorCrackSprites = Enumerable.Range(0, 3).Select(i => EffectSprite($"TremorCracks_{i}")).Where(s => s != null).ToArray();
        arena.rippleSprite = EffectSprite("Ripple");
        arena.oreArtColor = AverageColor(arena.oreSprite);
        arena.unlitMaterial = a.unlit;
        arena.flashMaterial = a.flash;
        Relics relics = arena.gameObject.AddComponent<Relics>();
        relics.arena = arena;
        arena.relics = relics;
        arena.quakeText = quakeText;
        arena.player = player;
        arena.leveling = leveling;
        // Crystals: the artist's crystal (the clusters' art), else a placeholder circle. XP gems: cyan circles.
        Sprite crystalArt = CrystalSprite();
        arena.xpSprite = a.circle;      // tinted cyan by Pickup
        arena.materialSprite = a.square;
        arena.crystalSprite = crystalArt != null ? crystalArt : a.circle;
        arena.goldSprite = ItemSprite("Gold", 1f) ?? a.circle; // the artist's gold nugget, 1 unit tall (Pickup sizes it)
        arena.crystalColor = AverageColor(arena.crystalSprite); // the pickups' sparkles (and the crystal's glow)
        arena.goldColor = AverageColor(arena.goldSprite);
        arena.relicSprite = Fx("FX_Glint", a.circle); // a star, tinted pale gold by Pickup
        arena.fireTrailSprite = Fx("FX_Fire", a.circle);
        arena.blastCoreSprite = a.circle;
        arena.blastRingSprite = a.ring;
        arena.shadowSprite = a.circle;
        arena.dangerZoneSprite = a.roundEnd;
        arena.cam = camera;
        arena.heroHpBar = heroHealth;
        arena.depthText = depthText;
        arena.goalText = goalText;
        arena.goalBar = goalBar.gameObject;
        arena.goalFill = goalFill;
        arena.lootText = lootText;
        arena.teleporterArrow = arrow.rectTransform;
        arena.teleporterSwirl = Fx("FX_Swirl", a.arc); // the swirling portal
        // The effects' shared sprites (Kenney Particle Pack, white; the generated ones if a file is missing)
        arena.fxBurst = Fx("FX_Burst", a.circle);
        arena.fxRing = Fx("FX_Ring", a.ring);
        arena.fxGlint = Fx("FX_Glint", a.circle);
        arena.fxScorch = Fx("FX_Scorch", a.circle);
        arena.fxBolt = Fx("FX_Bolt", a.square);
        arena.fxCrackle = Fx("FX_Crackle", a.circle);
        arena.fxGlow = a.glow;
        arena.bossBar = bossBar.gameObject;
        arena.bossFill = bossFill;
        arena.bossName = bossName;
        arena.panel = panel.gameObject;
        arena.panelTitle = panelTitle;
        arena.panelBody = panelBody;
        arena.primaryButton = primaryButton;
        arena.secondaryButton = secondaryButton;

        EditorSceneManager.SaveScene(scene);
    }

    // Paints the cave from the artist's textures (Assets/Art/Cave: each texture cut into 2x2 tiles of 1 unit) on two
    // Tilemaps: floor over the whole arena, brick walls in the cells around it (the same cells as BuildTiledRoom's walls:
    // 2 rows north and south, 1 column west and east). Every 2x2 block of cells shows one variant of its texture, picked
    // by a fixed hash of the block (the same on every build); the textures join seamlessly, so any variants fit together.
    // Returns false (and paints nothing) if a tile file is missing.
    static bool BuildPaintedCave(Vector2 size)
    {
        Tile[][] floors = CaveTiles("CaveFloor", "A", "B");
        Tile[][] walls = CaveTiles("CaveWall", "A", "B", "C");
        if (floors == null || walls == null) return false;

        Grid grid = new GameObject("Cave Grid").AddComponent<Grid>();
        Tilemap floorMap = NewTilemap("Floor", grid.transform, -10);
        Tilemap wallMap = NewTilemap("Walls", grid.transform, -5);

        int width = Mathf.RoundToInt(size.x), height = Mathf.RoundToInt(size.y);
        int left = -width / 2, bottom = -height / 2, right = left + width - 1, top = bottom + height - 1; // floor cells
        for (int y = bottom - 2; y <= top + 2; y++)
            for (int x = left - 1; x <= right + 1; x++)
            {
                bool inside = x >= left && x <= right && y >= bottom && y <= top;
                (inside ? floorMap : wallMap).SetTile(new Vector3Int(x, y, 0), BlockTile(inside ? floors : walls, x, y));
            }
        return true;
    }

    // The 4 tiles of every variant of a cave texture (<prefix>_<variant>_<0..3>), or null if any file is missing.
    static Tile[][] CaveTiles(string prefix, params string[] variants)
    {
        Tile[][] tiles = new Tile[variants.Length][];
        for (int v = 0; v < variants.Length; v++)
        {
            tiles[v] = new Tile[4];
            for (int i = 0; i < 4; i++)
            {
                Sprite sprite = CaveSprite($"{prefix}_{variants[v]}_{i}");
                if (sprite == null) return null;
                tiles[v][i] = TileAsset(sprite);
            }
        }
        return tiles;
    }

    // Cell (x, y): the variant of its 2x2 block, and its corner of that texture (0 bottom-left, 1 bottom-right,
    // 2 top-left, 3 top-right).
    static Tile BlockTile(Tile[][] set, int x, int y)
    {
        int blockX = Mathf.FloorToInt(x / 2f), blockY = Mathf.FloorToInt(y / 2f);
        uint hash = (uint)(blockX * 73856093) ^ (uint)(blockY * 19349663);
        hash ^= hash >> 13; // mix the bits, so neighbouring blocks don't fall into a pattern
        hash *= 0x5bd1e995;
        hash ^= hash >> 15;
        int variant = (int)(hash % (uint)set.Length);
        return set[variant][(x - blockX * 2) + 2 * (y - blockY * 2)];
    }

    // Paints the cave as a walled room from the test tileset (16 px tiles, 1 unit each) on two Tilemaps.
    // Floor: random floor tiles over the whole arena. Walls, all outside the arena bounds:
    //   north: brick face (just above the floor) + wall top above it,
    //   south: wall top (just below the floor) + its brick face below it,
    //   west/east: one column of wall top; corners from the same wall set.
    // Returns false (and paints nothing) if the tile files aren't in Assets/Art/Tiles.
    static bool BuildTiledRoom(Vector2 size)
    {
        Sprite[] floorSprites = Enumerable.Range(0, 9).Select(i => TileSprite($"Floor_{i}")).Where(s => s != null).ToArray();
        string[] wallNames = { "Wall_TL", "Wall_T", "Wall_TR", "Wall_L", "Wall_Face", "Wall_R", "Wall_BL", "Wall_B", "Wall_BR", "Wall_FL", "Wall_F", "Wall_FR" };
        if (floorSprites.Length == 0 || wallNames.Any(n => TileSprite(n) == null)) return false;

        Tile[] floorTiles = floorSprites.Select(TileAsset).ToArray();
        Tile Wall(string name) => TileAsset(TileSprite(name));

        Grid grid = new GameObject("Cave Grid").AddComponent<Grid>();
        Tilemap floor = NewTilemap("Floor", grid.transform, -10);
        Tilemap walls = NewTilemap("Walls", grid.transform, -5);

        int width = Mathf.RoundToInt(size.x), height = Mathf.RoundToInt(size.y);
        int left = -width / 2, bottom = -height / 2, right = left + width - 1, top = bottom + height - 1; // floor cells
        System.Random random = new System.Random(7); // the same floor pattern on every build

        for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
                floor.SetTile(new Vector3Int(x, y, 0), floorTiles[random.Next(floorTiles.Length)]);

        for (int x = left; x <= right; x++)
        {
            walls.SetTile(new Vector3Int(x, top + 1, 0), Wall("Wall_Face"));
            walls.SetTile(new Vector3Int(x, top + 2, 0), Wall("Wall_T"));
            walls.SetTile(new Vector3Int(x, bottom - 1, 0), Wall("Wall_B"));
            walls.SetTile(new Vector3Int(x, bottom - 2, 0), Wall("Wall_F"));
        }
        for (int y = bottom - 1; y <= top + 1; y++)
        {
            walls.SetTile(new Vector3Int(left - 1, y, 0), Wall("Wall_L"));
            walls.SetTile(new Vector3Int(right + 1, y, 0), Wall("Wall_R"));
        }
        walls.SetTile(new Vector3Int(left - 1, top + 2, 0), Wall("Wall_TL"));
        walls.SetTile(new Vector3Int(right + 1, top + 2, 0), Wall("Wall_TR"));
        walls.SetTile(new Vector3Int(left - 1, bottom - 1, 0), Wall("Wall_BL"));
        walls.SetTile(new Vector3Int(right + 1, bottom - 1, 0), Wall("Wall_BR"));
        walls.SetTile(new Vector3Int(left - 1, bottom - 2, 0), Wall("Wall_FL"));
        walls.SetTile(new Vector3Int(right + 1, bottom - 2, 0), Wall("Wall_FR"));
        return true;
    }

    static Tilemap NewTilemap(string name, Transform grid, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
        return go.GetComponent<Tilemap>();
    }

    // Looks only (no collision): torches on the north wall every 8 tiles.
    static void PlaceDecorations(Vector2 size)
    {
        float halfWidth = size.x / 2f, halfHeight = size.y / 2f;

        // Torches on the north wall, each with a warm flickering light: the artist's (two flame frames, TorchHeight tall,
        // standing against the wall with its foot on the floor, the light at the flame), else the tileset's.
        Sprite[] flames = { ItemSprite("Torch_1", TorchHeight), ItemSprite("Torch_2", TorchHeight) };
        bool painted = flames[0] != null && flames[1] != null;
        Sprite torch = painted ? flames[0] : PropSprite("Prop_Torch");
        float torchY = halfHeight + (painted ? TorchHeight / 2f - 0.15f : 0.5f); // painted: its foot just onto the floor
        if (torch != null)
            for (float x = -halfWidth + 4f; x < halfWidth; x += 8f)
            {
                SpriteRenderer torchSprite = NewSprite("Torch", torch, new Vector2(x, torchY), Vector2.one, Color.white, -4);
                Light2D torchLight = NewLight("Torch Light", torchSprite.transform, new Color(1f, 0.60f, 0.25f), 6f, 1.2f);
                Flicker flicker = torchSprite.gameObject.AddComponent<Flicker>();
                flicker.torchLight = torchLight;
                if (!painted) continue;
                torchLight.transform.localPosition = new Vector3(0f, TorchFlameY, 0f);
                flicker.frames = flames;
            }
    }

    // Makes a NewBar a health bar (DynamicBar): a light trail behind its fill shows what a hit just took.
    static DynamicBar HealthBar(RectTransform bar, RectTransform fill)
    {
        RectTransform trail = NewImage("Trail", bar, Vector2.zero, Vector2.zero, TrailColor).rectTransform;
        Stretch(trail);
        trail.SetSiblingIndex(0); // behind the fill
        DynamicBar health = bar.gameObject.AddComponent<DynamicBar>();
        health.kind = DynamicBar.Kind.Health;
        health.fill = fill;
        health.trail = trail;
        return health;
    }

    // Invisible child sprite a weapon flashes on each attack (the weapon sets its alpha, size and rotation).
    // `material`: unlit, so the flash stays bright in the dark cave (null = the default lit one).
    static SpriteRenderer NewFlashSprite(string name, Sprite sprite, Transform hero, Material material)
    {
        SpriteRenderer renderer = NewSprite(name, sprite, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0f), 4);
        renderer.transform.SetParent(hero, false);
        if (material != null) renderer.sharedMaterial = material;
        return renderer;
    }

    // A 2D point light (lights every sorting layer by default), a child of `parent`.
    static Light2D NewLight(string name, Transform parent, Color color, float radius, float intensity)
    {
        Light2D light = new GameObject(name).AddComponent<Light2D>();
        light.transform.SetParent(parent, false);
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = radius * 0.2f;
        light.intensity = intensity;
        light.falloffIntensity = 0.6f;
        return light;
    }

    static T SetWeapon<T>(T weapon, WeaponId id, string description, string bonusText,
        int damage, int damagePerLevel, float interval) where T : Weapon
    {
        weapon.id = id;
        weapon.description = description;
        weapon.bonusText = bonusText;
        weapon.baseDamage = damage;
        weapon.damagePerLevel = damagePerLevel;
        weapon.interval = interval;
        return weapon;
    }
}
