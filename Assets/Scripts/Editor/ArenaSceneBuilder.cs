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

    public static void Build(GameAssets a)
    {
        var scene = NewSceneFromTemplate(ScenePath);

        ArenaManager arena = new GameObject("Arena Manager").AddComponent<ArenaManager>();
        Vector2 size = arena.arenaSize;

        // Cave (visual only: the hero and enemies are kept inside the cave in code).
        // The test tileset as a walled room if its pieces are in Assets/Art/Tiles, otherwise placeholder floor/walls.
        if (BuildTiledRoom(size))
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
        Player player = hero.AddComponent<Player>();
        player.arena = arena;
        player.sprite = body;
        player.body = body.transform;
        player.idleSprite = a.heroIdle;
        player.walkSprites = a.heroWalk;

        //                      weapon                                       id                    card text                                                          bonus text             dmg +lvl interval
        SwordSweep sword    = SetWeapon(hero.AddComponent<SwordSweep>(),   WeaponId.Sword,        "Hits every enemy around you.",                                     "+sweep radius",        10, 3, 1.0f);
        DaggerThrow dagger  = SetWeapon(hero.AddComponent<DaggerThrow>(),  WeaponId.Dagger,       "Throws a dagger at the nearest enemy.",                            "+1 dagger per throw",   8, 2, 1.2f);
        WhipLash whip       = SetWeapon(hero.AddComponent<WhipLash>(),     WeaponId.Whip,         "Lashes a line toward the nearest enemy, reaching past the sword.", "+longer lash",         12, 3, 1.3f);
        CrystalOrbit orbit  = SetWeapon(hero.AddComponent<CrystalOrbit>(), WeaponId.CrystalOrbit, "Crystals circle you and hit whatever they touch.",                  "+1 crystal",            8, 2, 0.5f);
        // Relic weapons (equipped only once you own the relic)
        SwordSweep hammer    = SetWeapon(hero.AddComponent<SwordSweep>(),    WeaponId.QuakeHammer,    "Slams the ground every few seconds, hitting every enemy around you hard.", "+slam radius", 20, 4, 2.5f);
        LightningStaff staff = SetWeapon(hero.AddComponent<LightningStaff>(), WeaponId.LightningStaff, "A lightning bolt strikes a random enemy on screen.",                     "+1 bolt",     15, 3, 1.2f);
        FireBombThrow bomb   = SetWeapon(hero.AddComponent<FireBombThrow>(),  WeaponId.FireBomb,       "Lobs a fire bomb that leaves a burning patch.",                           "+bigger fire", 6, 2, 2.0f);
        AxeThrow axe         = SetWeapon(hero.AddComponent<AxeThrow>(),       WeaponId.Axe,            "Throws a heavy axe that spins out and flies back, hitting everything.",   "+1 axe",      18, 4, 2.0f);
        sword.sweepVisual = NewFlashSprite("Slash", a.arc, hero.transform, a.unlit); // spins once around the hero
        dagger.daggerSprite = a.square;
        whip.lashVisual = NewFlashSprite("Lash", a.square, hero.transform, a.unlit);
        orbit.crystalSprite = a.circle;
        hammer.radius = 3.5f;
        hammer.bonusRadius = 0.5f;
        hammer.knockback = 10f;
        hammer.style = SwordSweep.SweepStyle.Shockwave;
        hammer.sweepVisual = NewFlashSprite("Shockwave", a.ring, hero.transform, a.unlit);
        hammer.sweepVisual.color = new Color(0.95f, 0.70f, 0.40f, 0f); // dusty orange (the alpha is set by the weapon)

        // The cave is dark: a soft light follows the hero
        NewLight("Hero Light", hero.transform, new Color(1f, 0.92f, 0.80f), 9f, 0.75f);
        staff.boltSprite = a.square;
        staff.impactSprite = a.circle;
        bomb.bombSprite = a.circle;
        axe.axeSprite = a.square;
        foreach (Weapon weapon in new Weapon[] { dagger, whip, orbit, hammer, staff, bomb, axe }) weapon.enabled = false;

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
        deathBursts.pieceSprite = a.square;
        deathBursts.material = a.unlit;
        arena.damageNumbers = damageNumbers;
        arena.deathBursts = deathBursts;

        // ---------- HUD ----------
        Transform canvas = NewCanvas("HUD");
        Color backing = new Color(0.05f, 0.04f, 0.07f, 0.6f); // dark, so the text reads over the floor

        // Top-left: HP bar, day / depth / kills, then the dive's loot
        Image topLeft = NewImage("Top-Left Backing", canvas, new Vector2(20, -20), new Vector2(620, 140), backing);
        Pin(topLeft.rectTransform, new Vector2(0f, 1f));
        RectTransform hpBar = NewBar("HP Bar", canvas, new Vector2(40, -40), new Vector2(420, 30), new Color(0.80f, 0.15f, 0.15f), out RectTransform hpFill);
        Pin(hpBar, new Vector2(0f, 1f));
        Text hpText = Shadowed(NewText("HP Text", hpBar, Vector2.zero, new Vector2(420, 30), 20, TextAnchor.MiddleCenter));
        Text depthText = Shadowed(NewText("Depth", canvas, new Vector2(40, -80), new Vector2(600, 36), 26, TextAnchor.MiddleLeft));
        Pin(depthText.rectTransform, new Vector2(0f, 1f));
        Text lootText = Shadowed(NewText("Loot", canvas, new Vector2(40, -116), new Vector2(600, 32), 24, TextAnchor.MiddleLeft));
        Pin(lootText.rectTransform, new Vector2(0f, 1f));

        // Bottom-left (above the level): quake meter + ores mined this dive
        Image bottomLeft = NewImage("Bottom-Left Backing", canvas, new Vector2(14, 78), new Vector2(920, 44), backing);
        Pin(bottomLeft.rectTransform, new Vector2(0f, 0f));
        Text quakeText = Shadowed(NewText("Quake", canvas, new Vector2(24, 84), new Vector2(900, 32), 24, TextAnchor.MiddleLeft));
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
        Text arrow = NewText("Teleporter Arrow", canvas, Vector2.zero, new Vector2(60, 60), 48, TextAnchor.MiddleCenter);
        arrow.text = "▲";
        arrow.color = new Color(0.4f, 0.9f, 1f);
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
        Text levelText = Shadowed(NewText("Level", xpHud, new Vector2(24, 40), new Vector2(200, 36), 28, TextAnchor.MiddleLeft));
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
            StyleButton(cardButtons[i], card, cardTitles[i]); // highlights on hover
        }
        AddPopIn(cardPanel);
        cardPanel.gameObject.SetActive(false);

        // Esc pause menu, with How to play and Settings on top of it (last: drawn above everything)
        PauseMenu pauseMenu = MenuBuilder.Pause(canvas, arena);
        pauseMenu.howToPlay = MenuBuilder.HowToPlayPopup(canvas);
        pauseMenu.settings = MenuBuilder.Settings(canvas);
        arena.pauseMenu = pauseMenu;
        arena.debugKeys = MenuBuilder.DebugKeysUI(canvas, arena, null, new Vector2(1f, 1f)); // hint top-right

        // ---------- Leveling ----------
        LevelUp leveling = new GameObject("Leveling").AddComponent<LevelUp>();
        leveling.weapons = new Weapon[] { sword, dagger, whip, orbit, hammer, staff, bomb, axe };
        leveling.player = player;
        leveling.xpHud = xpHud.gameObject;
        leveling.xpFill = xpFill;
        leveling.levelText = levelText;
        leveling.cardPanel = cardPanel.gameObject;
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
        arena.boss = a.colossus;
        arena.forceBoss = a.force;
        // Day-15 horde: the default ramp x1.5 (25 / 30+5 / 30+6+4 / 40+8+8 / 30+6+6 + Colossus)
        arena.hordeWaves = new[]
        {
            Wave((a.crawler, 38)),
            Wave((a.crawler, 45), (a.spitter, 8)),
            Wave((a.crawler, 45), (a.spitter, 9), (a.brute, 6)),
            Wave((a.crawler, 60), (a.spitter, 12), (a.brute, 12)),
            Wave((a.crawler, 45), (a.spitter, 9), (a.brute, 9), (a.colossus, 1)),
        };
        arena.crate = a.crate;
        arena.crystalCluster = a.crystalCluster;
        // Ores: the tileset's stone pillar if it's there, otherwise a tinted, slightly bigger placeholder square.
        Sprite pillar = PropSprite("Prop_Pillar");
        arena.oreSprite = pillar != null ? pillar : a.square;
        arena.oreColor = pillar != null ? Color.white : new Color(0.45f, 0.55f, 0.75f);
        arena.oreScale = pillar != null ? 1f : 1.2f;
        arena.whiteSquare = a.square;
        arena.rockSprite = a.circle;
        arena.unlitMaterial = a.unlit;
        Relics relics = arena.gameObject.AddComponent<Relics>();
        relics.arena = arena;
        relics.circleSprite = a.circle;
        relics.squareSprite = a.square;
        arena.relics = relics;
        arena.quakeText = quakeText;
        arena.player = player;
        arena.leveling = leveling;
        arena.xpSprite = a.circle;
        arena.materialSprite = a.square;
        arena.crystalSprite = a.circle; // tinted violet by Pickup
        arena.goldSprite = a.circle;    // tinted yellow by Pickup
        arena.cam = camera;
        arena.hpFill = hpFill;
        arena.hpText = hpText;
        arena.depthText = depthText;
        arena.goalText = goalText;
        arena.goalBar = goalBar.gameObject;
        arena.goalFill = goalFill;
        arena.lootText = lootText;
        arena.teleporterArrow = arrow.rectTransform;
        arena.teleporterSprite = a.circle;
        arena.teleporterStairs = PropSprite("Prop_Stairs"); // null = glow only
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

    // Looks only (no collision): torches on the north wall's face every 8 tiles, and 8 barrels near the walls.
    static void PlaceDecorations(Vector2 size)
    {
        float halfWidth = size.x / 2f, halfHeight = size.y / 2f;

        // Torches on the north wall, each with a warm flickering light
        Sprite torch = PropSprite("Prop_Torch");
        if (torch != null)
            for (float x = -halfWidth + 4f; x < halfWidth; x += 8f)
            {
                SpriteRenderer torchSprite = NewSprite("Torch", torch, new Vector2(x, halfHeight + 0.5f), Vector2.one, Color.white, -4);
                Light2D torchLight = NewLight("Torch Light", torchSprite.transform, new Color(1f, 0.60f, 0.25f), 6f, 1.2f);
                torchSprite.gameObject.AddComponent<Flicker>().torchLight = torchLight;
            }

        Sprite barrel = PropSprite("Prop_Barrel");
        if (barrel == null) return;
        System.Random random = new System.Random(11); // the same spots on every build
        for (int i = 0; i < 8; i++)
        {
            float along = (float)random.NextDouble() * 2f - 1f; // -1..1 along the chosen wall
            Vector2 position;
            switch (i % 4)
            {
                case 0: position = new Vector2(-halfWidth + 0.8f, along * (halfHeight - 2f)); break; // west wall
                case 1: position = new Vector2(halfWidth - 0.8f, along * (halfHeight - 2f)); break;  // east wall
                case 2: position = new Vector2(along * (halfWidth - 2f), -halfHeight + 0.8f); break; // south wall
                default: position = new Vector2(along * (halfWidth - 2f), halfHeight - 1.2f); break;  // under the north wall
            }
            NewSprite("Barrel", barrel, position, Vector2.one, Color.white, 0);
        }
    }

    static HordeWave Wave(params (EnemyData enemy, int count)[] groups)
    {
        HordeWave wave = new HordeWave { groups = new HordeGroup[groups.Length] };
        for (int i = 0; i < groups.Length; i++)
            wave.groups[i] = new HordeGroup { enemy = groups[i].enemy, count = groups[i].count };
        return wave;
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
