using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Menu: Jam > Build Arena Scene.
// Creates placeholder sprites and enemy data (skipped if they already exist, so tuned values are kept)
// and builds Assets/Scenes/Arena.unity from the URP 2D template (which brings the camera and the 2D global light).
public static class ArenaSceneBuilder
{
    const string TemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    const string ScenePath = "Assets/Scenes/Arena.unity";
    const string ArtFolder = "Assets/Art/Placeholder";
    const string EnemiesFolder = "Assets/Data/Enemies";

    static Font font;

    [MenuItem("Jam/Build Arena Scene")]
    static void Build()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Arena", "Arena.unity already exists. Rebuild it?\n(Sprites and enemy data are kept.)", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (!File.Exists(TemplatePath))
        {
            EditorUtility.DisplayDialog("Arena", "Missing scene template: " + TemplatePath, "OK");
            return;
        }

        // ---------- Placeholder art + data ----------
        CreateFolder("Assets", "Art");
        CreateFolder("Assets/Art", "Placeholder");
        CreateFolder("Assets", "Data");
        CreateFolder("Assets/Data", "Enemies");
        Sprite circle = PlaceholderSprite("Circle", 64, true);
        Sprite square = PlaceholderSprite("Square", 4, false);

        //                                name               behavior                color                          radius  hp  speed  touch dmg
        EnemyData crawler = EnemyAsset("Crystal Crawler",  EnemyBehavior.Chaser,  new Color(0.60f, 0.30f, 0.70f), 0.3f,   10, 3.2f,  5, circle);
        EnemyData brute   = EnemyAsset("Crystal Brute",    EnemyBehavior.Chaser,  new Color(0.70f, 0.30f, 0.30f), 0.6f,   60, 1.4f, 15, circle);
        EnemyData spitter = EnemyAsset("Shard Spitter",    EnemyBehavior.Shooter, new Color(0.30f, 0.70f, 0.70f), 0.4f,   20, 2.2f,  5, circle);
        EnemyData boss    = EnemyAsset("Crystal Colossus", EnemyBehavior.Boss,    new Color(0.90f, 0.45f, 0.20f), 1.5f,  300, 1.6f, 25, circle);
        EnemyData force   = EnemyAsset("The Force",        EnemyBehavior.Boss,    new Color(0.45f, 0.10f, 0.55f), 2.2f,  800, 1.8f, 30, circle);
        AssetDatabase.SaveAssets();

        // ---------- Scene (copy of the URP 2D template) ----------
        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        AssetDatabase.CopyAsset(TemplatePath, ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        ArenaManager arena = new GameObject("Arena Manager").AddComponent<ArenaManager>();
        Vector2 size = arena.arenaSize;
        float wall = ArenaManager.WallThickness;

        // Floor and walls (walls are static colliders so enemies stop at them; the hero is clamped in code)
        NewSprite("Floor", square, Vector2.zero, size, new Color(0.16f, 0.13f, 0.12f), -10);
        NewWall("Wall Top", square, new Vector2(0f, (size.y + wall) / 2f), new Vector2(size.x + wall * 2f, wall));
        NewWall("Wall Bottom", square, new Vector2(0f, -(size.y + wall) / 2f), new Vector2(size.x + wall * 2f, wall));
        NewWall("Wall Left", square, new Vector2(-(size.x + wall) / 2f, 0f), new Vector2(wall, size.y));
        NewWall("Wall Right", square, new Vector2((size.x + wall) / 2f, 0f), new Vector2(wall, size.y));

        // Hero with the sword sweep
        SpriteRenderer heroSprite = NewSprite("Hero", circle, Vector2.zero, Vector2.one, new Color(0.30f, 0.55f, 0.95f), 5);
        Player player = heroSprite.gameObject.AddComponent<Player>();
        player.arena = arena;
        player.sprite = heroSprite;
        SwordSweep sword = heroSprite.gameObject.AddComponent<SwordSweep>();
        SpriteRenderer sweep = NewSprite("Sweep", circle, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0f), 4);
        sweep.transform.SetParent(heroSprite.transform, false);
        sword.sweepVisual = sweep;

        // Camera from the template
        Camera camera = Object.FindAnyObjectByType<Camera>(); // the template has exactly one
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        CameraFollow follow = camera.gameObject.AddComponent<CameraFollow>();
        follow.target = heroSprite.transform;
        follow.arena = arena;

        // ---------- HUD ----------
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        GameObject canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform canvas = canvasObject.transform;

        // Top-left: HP bar, depth + kills
        RectTransform hpBar = NewBar("HP Bar", canvas, new Vector2(40, -40), new Vector2(420, 30), new Color(0.80f, 0.15f, 0.15f), out RectTransform hpFill);
        Pin(hpBar, new Vector2(0f, 1f));
        Text hpText = NewText("HP Text", hpBar, Vector2.zero, new Vector2(420, 30), 20, TextAnchor.MiddleCenter);
        Text depthText = NewText("Depth", canvas, new Vector2(40, -80), new Vector2(700, 36), 26, TextAnchor.MiddleLeft);
        Pin(depthText.rectTransform, new Vector2(0f, 1f));

        // Top-center: timer, boss bar
        Text timerText = NewText("Timer", canvas, new Vector2(0, -25), new Vector2(300, 64), 52, TextAnchor.MiddleCenter);
        Pin(timerText.rectTransform, new Vector2(0.5f, 1f));
        RectTransform bossBar = NewBar("Boss Bar", canvas, new Vector2(0, -130), new Vector2(800, 22), new Color(0.90f, 0.45f, 0.20f), out RectTransform bossFill);
        Pin(bossBar, new Vector2(0.5f, 1f));
        Text bossName = NewText("Boss Name", bossBar, new Vector2(0, 26), new Vector2(800, 30), 24, TextAnchor.MiddleCenter);

        // Center: pause panel (depth cleared / defeat / victory)
        Image panel = NewImage("Panel", canvas, Vector2.zero, new Vector2(1000, 440), new Color(0f, 0f, 0f, 0.85f));
        Text panelTitle = NewText("Title", panel.transform, new Vector2(0, 140), new Vector2(950, 80), 56, TextAnchor.MiddleCenter);
        Text panelBody = NewText("Body", panel.transform, new Vector2(0, 20), new Vector2(900, 140), 26, TextAnchor.MiddleCenter);
        RectTransform buttonRow = NewRow("Buttons", panel.transform, new Vector2(0, -140), new Vector2(900, 90), 40);
        Button primaryButton = NewButton("Primary", buttonRow, new Vector2(320, 80));
        Button secondaryButton = NewButton("Secondary", buttonRow, new Vector2(320, 80));
        panel.gameObject.SetActive(false);

        // ---------- Wire the arena manager ----------
        arena.spawnTable = new[]
        {
            new SpawnEntry { enemy = crawler, weight = 70f },
            new SpawnEntry { enemy = spitter, weight = 15f },
            new SpawnEntry { enemy = brute, weight = 15f },
        };
        arena.boss = boss;
        arena.forceBoss = force;
        arena.player = player;
        arena.cam = camera;
        arena.hpFill = hpFill;
        arena.hpText = hpText;
        arena.depthText = depthText;
        arena.timerText = timerText;
        arena.bossBar = bossBar.gameObject;
        arena.bossFill = bossFill;
        arena.bossName = bossName;
        arena.panel = panel.gameObject;
        arena.panelTitle = panelTitle;
        arena.panelBody = panelBody;
        arena.primaryButton = primaryButton;
        arena.secondaryButton = secondaryButton;

        EditorSceneManager.SaveScene(scene);
        PutSceneFirstInBuildList();
        Debug.Log("Arena scene built: " + ScenePath + ". Press Play.");
    }

    // ---------- Assets ----------

    static void CreateFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    // White circle or square, imported as a sprite exactly 1 world unit wide.
    static Sprite PlaceholderSprite(string name, int size, bool circle)
    {
        string path = $"{ArtFolder}/{name}.png";
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float alpha = circle ? Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r))) : 1f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static EnemyData EnemyAsset(string name, EnemyBehavior behavior, Color color, float radius, int hp, float speed, int contactDamage, Sprite sprite)
    {
        string path = $"{EnemiesFolder}/{name}.asset";
        EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
        if (enemy != null) return enemy; // keep tuned values

        enemy = ScriptableObject.CreateInstance<EnemyData>();
        enemy.displayName = name;
        enemy.behavior = behavior;
        enemy.color = color;
        enemy.radius = radius;
        enemy.maxHp = hp;
        enemy.moveSpeed = speed;
        enemy.contactDamage = contactDamage;
        enemy.sprite = sprite;
        if (name == "The Force")
        {
            enemy.chargeInterval = 4f;
            enemy.chargeWindup = 0.7f;
            enemy.chargeSpeed = 14f;
            enemy.chargeDuration = 0.7f;
        }
        AssetDatabase.CreateAsset(enemy, path);
        return enemy;
    }

    // Arena first (it's the scene the game starts in); drop missing and archived scenes.
    static void PutSceneFirstInBuildList()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.path != ScenePath && File.Exists(s.path) && !s.path.StartsWith("Assets/_Archive/"))
            .ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------- World objects ----------

    static SpriteRenderer NewSprite(string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
    {
        SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.position = position;
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f); // placeholder sprites are 1 unit wide
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    static void NewWall(string name, Sprite square, Vector2 position, Vector2 size)
    {
        SpriteRenderer wall = NewSprite(name, square, position, size, new Color(0.35f, 0.30f, 0.28f), -5);
        wall.gameObject.AddComponent<BoxCollider2D>(); // 1x1 local, scaled to the wall size
    }

    // ---------- UI helpers ----------

    static RectTransform NewUI(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    // Anchors and pivots a HUD element to a screen corner/edge (e.g. (0,1) = top-left), keeping its offset.
    static void Pin(RectTransform rect, Vector2 anchor)
    {
        Vector2 position = rect.anchoredPosition;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
    }

    static Image NewImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        Image image = NewUI(name, parent, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    static Text NewText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        Text text = NewUI(name, parent, position, size).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static Button NewButton(string label, Transform parent, Vector2 size)
    {
        Image image = NewImage(label, parent, Vector2.zero, size, new Color(0.30f, 0.26f, 0.22f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        NewText("Label", image.transform, Vector2.zero, size, 26, TextAnchor.MiddleCenter).text = label;
        return button;
    }

    // Lines up its children left to right at their own size, centered (hidden children drop out).
    static RectTransform NewRow(string name, Transform parent, Vector2 position, Vector2 size, float spacing)
    {
        RectTransform row = NewUI(name, parent, position, size);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    // Dark background with a fill stretched over it. The fill's right anchor shows the amount.
    static RectTransform NewBar(string name, Transform parent, Vector2 position, Vector2 size, Color color, out RectTransform fill)
    {
        Image background = NewImage(name, parent, position, size, new Color(0.12f, 0.12f, 0.12f, 0.9f));
        fill = NewImage("Fill", background.transform, Vector2.zero, Vector2.zero, color).rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        return background.rectTransform;
    }
}
