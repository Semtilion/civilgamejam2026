using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Arena.unity (called from Jam > Build Game Scenes): cave, hero with every weapon, camera, HUD,
// teleporter arrow, pause panel, leveling UI and the ArenaManager, all wired up.
public static class ArenaSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Arena.unity";

    public static void Build(GameAssets a)
    {
        var scene = NewSceneFromTemplate(ScenePath);

        ArenaManager arena = new GameObject("Arena Manager").AddComponent<ArenaManager>();
        Vector2 size = arena.arenaSize;
        float wall = ArenaManager.WallThickness;

        // Floor and walls (visual only: the hero and enemies are kept inside the cave in code)
        NewSprite("Floor", a.square, Vector2.zero, size, new Color(0.16f, 0.13f, 0.12f), -10);
        Color wallColor = new Color(0.35f, 0.30f, 0.28f);
        NewSprite("Wall Top", a.square, new Vector2(0f, (size.y + wall) / 2f), new Vector2(size.x + wall * 2f, wall), wallColor, -5);
        NewSprite("Wall Bottom", a.square, new Vector2(0f, -(size.y + wall) / 2f), new Vector2(size.x + wall * 2f, wall), wallColor, -5);
        NewSprite("Wall Left", a.square, new Vector2(-(size.x + wall) / 2f, 0f), new Vector2(wall, size.y), wallColor, -5);
        NewSprite("Wall Right", a.square, new Vector2((size.x + wall) / 2f, 0f), new Vector2(wall, size.y), wallColor, -5);

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

        //                      weapon                                       id                    magic  card text                                                          bonus text             dmg +lvl interval
        SwordSweep sword    = SetWeapon(hero.AddComponent<SwordSweep>(),   WeaponId.Sword,        false, "Hits every enemy around you.",                                     "+sweep radius",        10, 3, 1.0f);
        DaggerThrow dagger  = SetWeapon(hero.AddComponent<DaggerThrow>(),  WeaponId.Dagger,       false, "Throws a dagger at the nearest enemy.",                            "+1 dagger per throw",   8, 2, 1.2f);
        WhipLash whip       = SetWeapon(hero.AddComponent<WhipLash>(),     WeaponId.Whip,         false, "Lashes a line toward the nearest enemy, reaching past the sword.", "+longer lash",         12, 3, 1.3f);
        CrystalOrbit orbit  = SetWeapon(hero.AddComponent<CrystalOrbit>(), WeaponId.CrystalOrbit, true,  "Crystals circle you and hit whatever they touch (magic).",          "+1 crystal",            8, 2, 0.5f);
        sword.sweepVisual = NewFlashSprite("Sweep", a.circle, hero.transform);
        dagger.daggerSprite = a.square;
        whip.lashVisual = NewFlashSprite("Lash", a.square, hero.transform);
        orbit.crystalSprite = a.circle;
        dagger.enabled = false;
        whip.enabled = false;
        orbit.enabled = false;

        // Camera from the template
        Camera camera = Object.FindAnyObjectByType<Camera>(); // the template has exactly one
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        CameraFollow follow = camera.gameObject.AddComponent<CameraFollow>();
        follow.target = hero.transform;
        follow.arena = arena;

        // ---------- HUD ----------
        Transform canvas = NewCanvas("HUD");

        // Top-left: HP bar, day / depth / kills / materials
        RectTransform hpBar = NewBar("HP Bar", canvas, new Vector2(40, -40), new Vector2(420, 30), new Color(0.80f, 0.15f, 0.15f), out RectTransform hpFill);
        Pin(hpBar, new Vector2(0f, 1f));
        Text hpText = NewText("HP Text", hpBar, Vector2.zero, new Vector2(420, 30), 20, TextAnchor.MiddleCenter);
        Text depthText = NewText("Depth", canvas, new Vector2(40, -80), new Vector2(900, 36), 26, TextAnchor.MiddleLeft);
        Pin(depthText.rectTransform, new Vector2(0f, 1f));

        // Bottom-left (above the level): quake meter + ores mined this dive
        Text quakeText = NewText("Quake", canvas, new Vector2(24, 84), new Vector2(900, 32), 24, TextAnchor.MiddleLeft);
        Pin(quakeText.rectTransform, new Vector2(0f, 0f));

        // Top-center: kill goal, boss bar
        Text goalText = NewText("Goal", canvas, new Vector2(0, -25), new Vector2(600, 64), 48, TextAnchor.MiddleCenter);
        Pin(goalText.rectTransform, new Vector2(0.5f, 1f));
        RectTransform bossBar = NewBar("Boss Bar", canvas, new Vector2(0, -130), new Vector2(800, 22), new Color(0.90f, 0.45f, 0.20f), out RectTransform bossFill);
        Pin(bossBar, new Vector2(0.5f, 1f));
        Text bossName = NewText("Boss Name", bossBar, new Vector2(0, 26), new Vector2(800, 30), 24, TextAnchor.MiddleCenter);

        // Edge arrow pointing at the teleporter while it's off-screen (ArenaManager moves and rotates it)
        Text arrow = NewText("Teleporter Arrow", canvas, Vector2.zero, new Vector2(60, 60), 48, TextAnchor.MiddleCenter);
        arrow.text = "▲";
        arrow.color = new Color(0.4f, 0.9f, 1f);
        arrow.gameObject.SetActive(false);

        // Center: pause panel (depth cleared / defeat / victory)
        Image panel = NewImage("Panel", canvas, Vector2.zero, new Vector2(1000, 440), new Color(0f, 0f, 0f, 0.85f));
        Text panelTitle = NewText("Title", panel.transform, new Vector2(0, 140), new Vector2(950, 80), 56, TextAnchor.MiddleCenter);
        Text panelBody = NewText("Body", panel.transform, new Vector2(0, 20), new Vector2(900, 140), 26, TextAnchor.MiddleCenter);
        RectTransform buttonRow = NewRow("Buttons", panel.transform, new Vector2(0, -140), new Vector2(900, 90), 40);
        Button primaryButton = NewButton("Primary", buttonRow, new Vector2(320, 80));
        Button secondaryButton = NewButton("Secondary", buttonRow, new Vector2(320, 80));
        panel.gameObject.SetActive(false);

        // Bottom: XP bar + level (hidden if Leveling Enabled is unticked)
        RectTransform xpHud = NewUI("XP HUD", canvas, Vector2.zero, Vector2.zero);
        Stretch(xpHud);
        RectTransform xpBar = NewBar("XP Bar", xpHud, new Vector2(0, 16), new Vector2(1880, 16), new Color(0.30f, 0.90f, 1f), out RectTransform xpFill);
        Pin(xpBar, new Vector2(0.5f, 0f));
        Text levelText = NewText("Level", xpHud, new Vector2(24, 40), new Vector2(200, 36), 28, TextAnchor.MiddleLeft);
        Pin(levelText.rectTransform, new Vector2(0f, 0f));

        // Center: level-up cards (drawn above everything else)
        Image cardPanel = NewImage("Level Up Panel", canvas, Vector2.zero, new Vector2(1320, 560), new Color(0f, 0f, 0f, 0.85f));
        NewText("Title", cardPanel.transform, new Vector2(0, 220), new Vector2(1200, 80), 56, TextAnchor.MiddleCenter).text = "LEVEL UP!";
        RectTransform cardRow = NewRow("Cards", cardPanel.transform, new Vector2(0, -40), new Vector2(1260, 360), 30);
        Button[] cardButtons = new Button[3];
        Text[] cardTitles = new Text[3];
        Text[] cardDescriptions = new Text[3];
        for (int i = 0; i < cardButtons.Length; i++)
        {
            Image card = NewImage($"Card {i + 1}", cardRow, Vector2.zero, new Vector2(390, 340), new Color(0.30f, 0.26f, 0.22f));
            cardButtons[i] = card.gameObject.AddComponent<Button>();
            cardButtons[i].targetGraphic = card;
            cardTitles[i] = NewText("Title", card.transform, new Vector2(0, 110), new Vector2(370, 80), 30, TextAnchor.MiddleCenter);
            cardDescriptions[i] = NewText("Description", card.transform, new Vector2(0, -40), new Vector2(350, 200), 24, TextAnchor.MiddleCenter);
        }
        cardPanel.gameObject.SetActive(false);

        // ---------- Leveling ----------
        LevelUp leveling = new GameObject("Leveling").AddComponent<LevelUp>();
        leveling.weapons = new Weapon[] { sword, dagger, whip, orbit };
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
        arena.crate = a.crate;
        arena.oreSprite = a.square;
        arena.quakeText = quakeText;
        arena.player = player;
        arena.leveling = leveling;
        arena.xpSprite = a.circle;
        arena.materialSprite = a.square;
        arena.cam = camera;
        arena.hpFill = hpFill;
        arena.hpText = hpText;
        arena.depthText = depthText;
        arena.goalText = goalText;
        arena.teleporterArrow = arrow.rectTransform;
        arena.teleporterSprite = a.circle;
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

    // Invisible child sprite a weapon flashes on each attack (the weapon sets its alpha, size and rotation).
    static SpriteRenderer NewFlashSprite(string name, Sprite sprite, Transform hero)
    {
        SpriteRenderer renderer = NewSprite(name, sprite, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0f), 4);
        renderer.transform.SetParent(hero, false);
        return renderer;
    }

    static T SetWeapon<T>(T weapon, WeaponId id, bool isMagic, string description, string bonusText,
        int damage, int damagePerLevel, float interval) where T : Weapon
    {
        weapon.id = id;
        weapon.isMagic = isMagic;
        weapon.description = description;
        weapon.bonusText = bonusText;
        weapon.baseDamage = damage;
        weapon.damagePerLevel = damagePerLevel;
        weapon.interval = interval;
        return weapon;
    }
}
