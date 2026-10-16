using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The data assets both scenes use.
public class GameAssets
{
    public Sprite circle, square;
    public EnemyData crawler, brute, spitter, colossus, force, crate;
    public TownConfig townConfig;
}

// Menu: Jam > Build Game Scenes. Creates the placeholder sprites, enemy data and Town Config (existing ones are kept,
// so tuned values are safe), then builds Assets/Scenes/Town.unity and Arena.unity. Also holds the shared helpers.
public static class GameBuilder
{
    const string TemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    const string ArtFolder = "Assets/Art/Placeholder";
    const string EnemiesFolder = "Assets/Data/Enemies";
    const string TownConfigPath = "Assets/Data/TownConfig.asset";

    static Font font;

    [MenuItem("Jam/Build Game Scenes")]
    static void BuildAll()
    {
        bool exists = File.Exists(ArenaSceneBuilder.ScenePath) || File.Exists(TownSceneBuilder.ScenePath);
        if (exists && !EditorUtility.DisplayDialog("Build Game Scenes",
                "Rebuild Town.unity and Arena.unity?\n(Sprites, enemy data and the Town Config are kept.)", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (!File.Exists(TemplatePath))
        {
            EditorUtility.DisplayDialog("Build Game Scenes", "Missing scene template: " + TemplatePath, "OK");
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // so neither target scene is open
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameAssets assets = EnsureAssets();
        ArenaSceneBuilder.Build(assets);
        TownSceneBuilder.Build(assets); // built last, so it's the open scene: press Play to start the game
        SetBuildList(TownSceneBuilder.ScenePath, ArenaSceneBuilder.ScenePath);
        Debug.Log("Built Town and Arena. Press Play in the Town scene to start a new game.");
    }

    // ---------- Assets ----------

    static GameAssets EnsureAssets()
    {
        CreateFolder("Assets", "Art");
        CreateFolder("Assets/Art", "Placeholder");
        CreateFolder("Assets", "Data");
        CreateFolder("Assets/Data", "Enemies");

        GameAssets a = new GameAssets();
        a.circle = PlaceholderSprite("Circle", 64, true);
        a.square = PlaceholderSprite("Square", 4, false);

        //                         name               behavior                color                          radius  hp  speed touch  xp  mat% mat
        a.crawler  = EnemyAsset("Crystal Crawler",  EnemyBehavior.Chaser,  new Color(0.60f, 0.30f, 0.70f), 0.3f,   10, 3.2f,  5,   1, 0.05f,  1, a.circle);
        a.brute    = EnemyAsset("Crystal Brute",    EnemyBehavior.Chaser,  new Color(0.70f, 0.30f, 0.30f), 0.6f,   60, 1.4f, 15,   3, 0.25f,  1, a.circle);
        a.spitter  = EnemyAsset("Shard Spitter",    EnemyBehavior.Shooter, new Color(0.30f, 0.70f, 0.70f), 0.4f,   20, 2.2f,  5,   2, 0.15f,  1, a.circle);
        a.colossus = EnemyAsset("Crystal Colossus", EnemyBehavior.Boss,    new Color(0.90f, 0.45f, 0.20f), 1.5f,  300, 1.6f, 25,  20, 1f,    10, a.circle);
        a.force    = EnemyAsset("The Force",        EnemyBehavior.Boss,    new Color(0.45f, 0.10f, 0.55f), 2.2f,  800, 1.8f, 30,   0, 0f,     0, a.circle);
        a.crate    = EnemyAsset("Supply Crate",     EnemyBehavior.Crate,   new Color(0.55f, 0.38f, 0.20f), 0.5f,   20, 0f,    0,   0, 1f,     3, a.square);
        a.townConfig = TownConfigAsset(a);
        AssetDatabase.SaveAssets();
        return a;
    }

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

    static EnemyData EnemyAsset(string name, EnemyBehavior behavior, Color color, float radius, int hp, float speed,
        int contactDamage, int xp, float materialChance, int materialAmount, Sprite sprite)
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
        enemy.xp = xp;
        enemy.materialChance = materialChance;
        enemy.materialAmount = materialAmount;
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

    static TownConfig TownConfigAsset(GameAssets a)
    {
        TownConfig config = AssetDatabase.LoadAssetAtPath<TownConfig>(TownConfigPath);
        if (config != null) return config; // keep tuned values

        config = ScriptableObject.CreateInstance<TownConfig>();
        config.npcs = new[]
        {
            new NpcDefinition { name = "Guard", stat = StatType.MaxHp, amountPerTier = 10f, unlocksWeapon = true, weapon = WeaponId.Whip },
            new NpcDefinition { name = "Shaman", stat = StatType.MagicDamage, amountPerTier = 0.08f, unlocksWeapon = true, weapon = WeaponId.CrystalOrbit },
            new NpcDefinition { name = "NPC 3", stat = StatType.PhysicalDamage, amountPerTier = 0.05f, unlocksWeapon = true, weapon = WeaponId.Dagger },
            new NpcDefinition { name = "NPC 4 (Smith)", stat = StatType.MoveSpeed, amountPerTier = 0.04f, isSmith = true },
        };
        config.killQuests = new[]
        {
            new KillQuestOption { enemy = a.crawler, count = 40 },
            new KillQuestOption { enemy = a.spitter, count = 10 },
            new KillQuestOption { enemy = a.brute, count = 8 },
        };
        AssetDatabase.CreateAsset(config, TownConfigPath);
        return config;
    }

    // ---------- Scenes ----------

    // A copy of the URP 2D template (brings the camera and the 2D global light), opened for editing.
    public static Scene NewSceneFromTemplate(string path)
    {
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(TemplatePath, path);
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    // These scenes first, in this order (the first one is where the game starts); drop missing and archived scenes.
    static void SetBuildList(params string[] paths)
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => !paths.Contains(s.path) && File.Exists(s.path) && !s.path.StartsWith("Assets/_Archive/"))
            .ToList();
        scenes.InsertRange(0, paths.Select(p => new EditorBuildSettingsScene(p, true)));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------- World objects ----------

    public static SpriteRenderer NewSprite(string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
    {
        SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.position = position;
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f); // placeholder sprites are 1 unit wide
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    // ---------- UI ----------

    // Screen-space canvas scaled for 1920x1080, plus the EventSystem the UI needs (new Input System module).
    public static Transform NewCanvas(string name)
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvasObject.transform;
    }

    public static RectTransform NewUI(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    // Anchors and pivots an element to a screen corner/edge (e.g. (0,1) = top-left), keeping its offset.
    public static void Pin(RectTransform rect, Vector2 anchor)
    {
        Vector2 position = rect.anchoredPosition;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static Image NewImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        Image image = NewUI(name, parent, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static Text NewText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        Text text = NewUI(name, parent, position, size).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    public static Button NewButton(string label, Transform parent, Vector2 size, int fontSize = 26)
    {
        Image image = NewImage(label, parent, Vector2.zero, size, new Color(0.30f, 0.26f, 0.22f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        NewText("Label", image.transform, Vector2.zero, size - new Vector2(16, 8), fontSize, TextAnchor.MiddleCenter).text = label;
        return button;
    }

    // Lines up its children left to right at their own size, centered (hidden children drop out).
    public static RectTransform NewRow(string name, Transform parent, Vector2 position, Vector2 size, float spacing)
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
    public static RectTransform NewBar(string name, Transform parent, Vector2 position, Vector2 size, Color color, out RectTransform fill)
    {
        Image background = NewImage(name, parent, position, size, new Color(0.12f, 0.12f, 0.12f, 0.9f));
        fill = NewImage("Fill", background.transform, Vector2.zero, Vector2.zero, color).rectTransform;
        Stretch(fill);
        return background.rectTransform;
    }
}
