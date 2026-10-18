using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// The data assets both scenes use.
public class GameAssets
{
    public Sprite circle, square;
    public Sprite ring, arc;  // generated: the hammer's shockwave, the sword's slash
    public Sprite shard;      // generated: the Spitter's shots if the artist's are missing
    public Sprite arrow;      // generated: the teleporter arrow (a triangle pointing up)
    public Sprite glow;       // generated: soft-edged, 9-sliced (the glow around the leader portrait)
    public Sprite roundEnd;   // generated: a bar with a round right end, 9-sliced (the boss's dash warning)
    public Material unlit;    // URP's unlit sprite material (effects stay bright in the dark cave)
    public Material flash;    // the hit flash (Assets/Shaders/SpriteSilhouette.shader): a sprite's shape in white
    public Sprite heroIdle;   // null = no hero art yet (placeholder circle)
    public Sprite[] heroWalk;
    public EnemyData crawler, brute, spitter, colossus, crate, crystalCluster;
    public TownConfig townConfig;
    public string[] evolutionTexts; // what each evolution does (WeaponId order): set by ArenaSceneBuilder, the town's popup shows them
}

// Menu: Jam > Build Game Scenes. Creates the placeholder sprites, enemy data and Town Config (existing ones are kept,
// so tuned values are safe), then builds Assets/Scenes/Title.unity, Town.unity and Arena.unity. Also holds the shared
// helpers (the shared menu panels are in MenuBuilder).
public static class GameBuilder
{
    const string TemplatePath = "Assets/Settings/Scenes/URP2DSceneTemplate.unity";
    const string ArtFolder = "Assets/Art/Placeholder";
    const string HeroFolder = "Assets/Art/Hero";
    public const float HeroHeight = 2.2f; // world units
    const string TilesFolder = "Assets/Art/Tiles";
    const string PropsFolder = "Assets/Art/Props";
    const string NpcFolder = "Assets/Art/NPCs";
    const string EnemyArtFolder = "Assets/Art/Enemies";
    const string DungeonArtFolder = "Assets/Art/Dungeon";
    const string ItemsFolder = "Assets/Art/Items";     // the artist's items sheet, cut up: crate, torches, gold, axe, bomb
    const string EffectsFolder = "Assets/Art/Effects"; // weapon effects: Kenney Particle Pack textures (CC0), white
    const string CaveFolder = "Assets/Art/Cave";       // the artist's cave textures, cut into 1-unit tiles
    const string UiFolder = "Assets/Art/UI";             // the title / town screens' art (Tools/bake_title.py, bake_town.py)
    const string FrameFolder = "Assets/Art/UI/Frame";    // the artist's ornate frame in 12 pieces, white lines (tinted)
    // The frame art's sizes in px: the top / bottom bands and the side columns, and the bands' fixed pieces (the end
    // pieces with the corners, and the centered ornament); the strips between them stretch.
    const float FrameTop = 100f, FrameBottom = 150f, FrameSide = 80f;
    const float FrameTopLeft = 200f, FrameTopMiddle = 140f, FrameTopRight = 192f;
    const float FrameBottomLeft = 200f, FrameBottomMiddle = 215f, FrameBottomRight = 202f;
    static readonly string[] FramePieceNames =
    {
        "TopLeft", "TopStretchLeft", "TopMiddle", "TopStretchRight", "TopRight",
        "BottomLeft", "BottomStretchLeft", "BottomMiddle", "BottomStretchRight", "BottomRight", "SideLeft", "SideRight",
    };
    const float OreHeight = 1.8f;  // world units (the whole image, a few px of padding included)
    const float ShotLength = 0.5f; // world units: the Spitter's shot (its art points right)
    const float ClusterWidth = 1.35f; // world units: the crystal cluster (three crystals; its hit circle is 1.2 across)
    const float CrateWidth = 1.3f;    // world units: the supply crate (its hit circle is 1 across)
    const int PixelsPerTile = 16; // the test tileset: 16 px = 1 world unit
    // The Spitter's stem and flower share one canvas centered on the flower's core (its hit center). 210 px = 1 unit,
    // times the enemy's size (radius 0.4 x 2): the plant (419 px) stands 1.6 units tall and the flower's body fills
    // its hit circle.
    const float SpitterPixelsPerUnit = 210f;
    // The Brute's two poses (stand, step) share one canvas centered on its torso (its hit center). The figure (264 px)
    // stands 2.6 units tall at the Brute's size (radius 0.6 x 2 = 1.2): 264 / (2.6 / 1.2) = 122 px per unit.
    const float BrutePixelsPerUnit = 122f;
    // The Crawler's two poses (stretched, bunched up), same treatment (canvas centered on its body): the figure (289 px)
    // is 1.3 units long at the Crawler's size (radius 0.3 x 2 = 0.6): 289 / (1.3 / 0.6) = 133 px per unit.
    const float CrawlerPixelsPerUnit = 133f;
    const string EnemiesFolder = "Assets/Data/Enemies";
    const string TownConfigPath = "Assets/Data/TownConfig.asset";
    const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // The UI fonts (Assets/Fonts, SIL Open Font License; set when building starts): Alegreya for text, MedievalSharp for
    // titles, main buttons, card names, the boss name, the kill goal and damage numbers. A missing file falls back to
    // Unity's built-in font.
    const string FontsFolder = "Assets/Fonts";
    const float AlegreyaLineSpacing = 0.88f; // its lines are ~20% taller than the built-in font's: keeps them about the same
    static Font font, titleFont;
    static Sprite[] framePieces; // the frame's 12 pieces, in FramePieceNames order (loaded when building starts; null = thin outlines instead)
    static float lineSpacing = 1f;
    public static Font TitleFont => titleFont;

    [MenuItem("Jam/Build Game Scenes")]
    static void BuildAll()
    {
        bool exists = File.Exists(ArenaSceneBuilder.ScenePath) || File.Exists(TownSceneBuilder.ScenePath) ||
                      File.Exists(TitleSceneBuilder.ScenePath);
        if (exists && !EditorUtility.DisplayDialog("Build Game Scenes",
                "Rebuild Title.unity, Town.unity and Arena.unity?\n(Sprites, enemy data and the Town Config are kept.)", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (!File.Exists(TemplatePath))
        {
            EditorUtility.DisplayDialog("Build Game Scenes", "Missing scene template: " + TemplatePath, "OK");
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // so neither target scene is open
        LoadFonts();
        framePieces = FramePieces();
        GameAssets assets = EnsureAssets();
        ArenaSceneBuilder.Build(assets);
        TownSceneBuilder.Build(assets);
        TitleSceneBuilder.Build(assets); // built last, so it's the open scene: press Play to start the game
        SetBuildList(TitleSceneBuilder.ScenePath, TownSceneBuilder.ScenePath, ArenaSceneBuilder.ScenePath);
        Debug.Log("Built Title, Town and Arena. Press Play in the Title scene to start (Town or Arena also work for testing).");
    }

    static void LoadFonts()
    {
        Font builtIn = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        font = AssetDatabase.LoadAssetAtPath<Font>($"{FontsFolder}/Alegreya-Regular.ttf");
        lineSpacing = AlegreyaLineSpacing;
        if (font == null)
        {
            Debug.LogWarning($"{FontsFolder}/Alegreya-Regular.ttf not found: using the built-in font.");
            font = builtIn;
            lineSpacing = 1f;
        }
        titleFont = AssetDatabase.LoadAssetAtPath<Font>($"{FontsFolder}/MedievalSharp.ttf");
        if (titleFont == null)
        {
            Debug.LogWarning($"{FontsFolder}/MedievalSharp.ttf not found: titles use the text font.");
            titleFont = font;
        }
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
        // A thin ring, and a crescent whose bright end (pointing right) fades out counter-clockwise like a slash's tail.
        a.ring = GeneratedSprite("Ring", 128, (r, angle) => Mathf.Clamp01((r - 0.80f) / 0.06f) * Mathf.Clamp01((0.99f - r) / 0.04f));
        a.arc = GeneratedSprite("Arc", 128, (r, angle) =>
            Mathf.Clamp01((r - 0.55f) / 0.12f) * Mathf.Clamp01((0.99f - r) / 0.05f) * Mathf.Clamp01(1f - angle / 220f));
        // A slim diamond pointing right (shots turn to face where they fly).
        a.shard = GeneratedSprite("Shard", 64, (r, angle) =>
        {
            float x = r * Mathf.Cos(angle * Mathf.Deg2Rad), y = r * Mathf.Sin(angle * Mathf.Deg2Rad);
            return (0.95f - Mathf.Abs(x) - Mathf.Abs(y) / 0.45f) / 0.08f;
        });
        // A triangle pointing up, centered (it turns around its middle), corners inside the circle: the teleporter arrow.
        a.arrow = GeneratedSprite("Arrow", 64, (r, angle) =>
        {
            float x = r * Mathf.Cos(angle * Mathf.Deg2Rad), y = r * Mathf.Sin(angle * Mathf.Deg2Rad);
            float aboveBase = y + 0.7f;                       // the flat bottom at -0.7
            float insideSides = 0.5f * (0.7f - y) - Mathf.Abs(x); // the slanted sides: (+-0.7, -0.7) up to (0, 0.7)
            return Mathf.Min(aboveBase, insideSides) / 0.06f;
        });
        a.glow = GlowSprite();
        a.roundEnd = RoundEndSprite();
        a.unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
        if (a.unlit == null) Debug.LogWarning("Unlit sprite material not found at " + UnlitMaterialPath + ": effects will use the lit one.");
        a.flash = FlashMaterial();
        a.heroIdle = HeroSprite("Hero_Idle");
        a.heroWalk = new[] { HeroSprite("Hero_Walk1"), HeroSprite("Hero_Walk2") }.Where(s => s != null).ToArray();

        //                         name               behavior                color                          radius  hp  speed touch  xp  mat% mat
        a.crawler  = EnemyAsset("Crystal Crawler",  EnemyBehavior.Chaser,  new Color(0.60f, 0.30f, 0.70f), 0.3f,   10, 3.2f,  5,   1, 0.05f,  1, a.circle, goldChance: 0.10f, gold: 2);
        a.brute    = EnemyAsset("Crystal Brute",    EnemyBehavior.Chaser,  new Color(0.70f, 0.30f, 0.30f), 0.6f,   60, 1.4f, 15,   3, 0.25f,  1, a.circle, goldChance: 0.25f, gold: 5);
        a.spitter  = EnemyAsset("Shard Spitter",    EnemyBehavior.Shooter, new Color(0.30f, 0.70f, 0.70f), 0.4f,   20, 2.2f,  5,   2, 0.15f,  1, a.circle, goldChance: 0.15f, gold: 3);
        a.colossus = EnemyAsset("Crystal Colossus", EnemyBehavior.Boss,    new Color(0.90f, 0.45f, 0.20f), 1.2f,  300, 1.6f, 25,  20, 1f,    10, a.circle, goldChance: 1f, gold: 50);
        a.crate    = EnemyAsset("Supply Crate",     EnemyBehavior.Crate,   new Color(0.55f, 0.38f, 0.20f), 0.5f,   20, 0f,    0,   0, 1f,     3, a.square, supplies: 1);
        a.crystalCluster = EnemyAsset("Crystal Cluster", EnemyBehavior.Crate, new Color(0.80f, 0.60f, 1f), 0.6f, 30, 0f, 0,   0, 0f,     0, a.circle, crystals: 1);

        // The artist's crate (CrateWidth wide; Enemy.Spawn draws a sprite at radius x 2 per unit), else the tileset's,
        // replaces the brown placeholder square on the existing asset.
        Sprite crateArt = ItemSprite("Crate", CrateWidth / (a.crate.radius * 2f), byWidth: true) ?? PropSprite("Prop_Crate");
        if (crateArt != null && a.crate.sprite != crateArt)
        {
            a.crate.sprite = crateArt;
            a.crate.color = Color.white;
            EditorUtility.SetDirty(a.crate);
        }
        // The artist's Brute (a crystal-backed hulk) and Crawler (a crystal-backed slug): 2-pose walk cycles, facing left.
        // The boss is the Brute recolored gold (Boss_1/_2.png, made from Brute_1/_2) at twice its size (radius 1.2).
        WalkArt(a.brute, "Brute", BrutePixelsPerUnit);
        WalkArt(a.crawler, "Crawler", CrawlerPixelsPerUnit);
        WalkArt(a.colossus, "Boss", BrutePixelsPerUnit);
        // The artist's Spitter (a crystal flower on a stem) replaces its placeholder circle and fires from the flower's 5 tips.
        Sprite spitterStem = EnemySprite("Spitter_Stem", SpitterPixelsPerUnit);
        Sprite spitterFlower = EnemySprite("Spitter_Flower", SpitterPixelsPerUnit);
        if (spitterStem != null && spitterFlower != null && a.spitter.flower != spitterFlower)
        {
            a.spitter.sprite = spitterStem;
            a.spitter.flower = spitterFlower;
            a.spitter.color = Color.white;
            a.spitter.shotAngles = new[] { 13f, 79f, 148f, 216f, 305f };
            EditorUtility.SetDirty(a.spitter);
        }
        // Shadows under the creatures' feet, measured on their art (the boss uses the Brute's). Crates have none.
        Shadow(a.brute, new Vector2(1.05f, 0.34f), new Vector2(0.12f, -0.88f));
        Shadow(a.colossus, new Vector2(1.05f, 0.34f), new Vector2(0.12f, -0.88f));
        Shadow(a.crawler, new Vector2(1.9f, 0.5f), new Vector2(-0.05f, -0.37f));
        Shadow(a.spitter, new Vector2(1.3f, 0.42f), new Vector2(0f, -1.1f));
        // Its shots: the artist's crystal shard (ShotLength long; Projectile draws a sprite at its radius x 2), else the
        // generated one. A sprite picked by hand in the Inspector is kept.
        Sprite shotArt = SizedSprite($"{EnemyArtFolder}/Spitter_Shot.png", ShotLength / (Projectile.Radius * 2f), byWidth: true);
        Sprite shot = shotArt != null ? shotArt : a.shard;
        if (a.spitter.projectileSprite != shot && (a.spitter.projectileSprite == null || a.spitter.projectileSprite == a.shard))
        {
            a.spitter.projectileSprite = shot;
            EditorUtility.SetDirty(a.spitter);
        }
        // The crystal cluster: the crystal art three times (Crystal_Cluster.png), ClusterWidth wide (Enemy.Spawn draws a
        // sprite at radius x 2 per unit), untinted (its glow stays violet: ArenaManager).
        Sprite clusterArt = SizedSprite($"{DungeonArtFolder}/Crystal_Cluster.png", ClusterWidth / (a.crystalCluster.radius * 2f), byWidth: true);
        if (clusterArt != null && a.crystalCluster.sprite != clusterArt)
        {
            a.crystalCluster.sprite = clusterArt;
            a.crystalCluster.color = Color.white;
            EditorUtility.SetDirty(a.crystalCluster);
        }
        ParticleColors(a.crawler, a.brute, a.spitter, a.colossus, a.crate, a.crystalCluster); // after their art is set
        a.townConfig = TownConfigAsset(a);
        // The artist's portraits go on the leaders that don't have one yet.
        foreach (NpcDefinition npc in a.townConfig.npcs)
        {
            if (npc.portrait != null) continue;
            npc.portrait = PortraitSprite(npc.name);
            if (npc.portrait != null) EditorUtility.SetDirty(a.townConfig);
        }
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

    // One of the artist's hero frames (Assets/Art/Hero), imported as a sprite exactly HeroHeight units tall with the
    // pivot at the feet (bottom center), so leaning and breathing keep the feet planted.
    // Returns null if the file isn't there (the builder then uses the placeholder circle).
    static Sprite HeroSprite(string name)
    {
        string path = $"{HeroFolder}/{name}.png";
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path); // in case Unity hasn't picked up a freshly dropped file yet
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        float pixelsPerUnit = height / HeroHeight;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
            Mathf.Abs(importer.spritePixelsPerUnit - pixelsPerUnit) > 0.01f || importer.mipmapEnabled ||
            settings.spriteAlignment != (int)SpriteAlignment.BottomCenter)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.ReadTextureSettings(settings); // re-read: the lines above changed the importer
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // A leader's bust for the leader window (Assets/Art/NPCs/Portrait_<name>.png): smooth (painted art), no mipmaps, and
    // uncompressed with Read/Write on, so the town can make a grey copy once the faction is wiped out.
    // Returns null if the file isn't there.
    static Sprite PortraitSprite(string name)
    {
        string path = $"{NpcFolder}/Portrait_{name}.png";
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path); // in case Unity hasn't picked up a freshly dropped file yet
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
            !importer.isReadable || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // The hit flash material (Assets/Art/Materials/HitFlash.mat, the "Jam/Sprite Silhouette" shader at 80% white), made
    // once. Being a scene reference, it also makes builds include the shader. Null (no flash) if the shader is missing.
    static Material FlashMaterial()
    {
        const string path = "Assets/Art/Materials/HitFlash.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Jam/Sprite Silhouette");
        if (shader == null)
        {
            Debug.LogWarning("Shader Jam/Sprite Silhouette (Assets/Shaders) not found: enemies won't flash white when hit.");
            return null;
        }
        CreateFolder("Assets/Art", "Materials");
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // An enemy's 2-pose walk cycle (Assets/Art/Enemies/<name>_1.png and _2.png, drawn facing left) replaces its
    // placeholder: sprite = pose 1, walkFrames = both, untinted. Nothing changes once pose 1 is its sprite.
    static void WalkArt(EnemyData enemy, string name, float pixelsPerUnit)
    {
        Sprite pose1 = EnemySprite(name + "_1", pixelsPerUnit), pose2 = EnemySprite(name + "_2", pixelsPerUnit);
        if (pose1 == null || pose2 == null || enemy.sprite == pose1) return;
        enemy.sprite = pose1;
        enemy.walkFrames = new[] { pose1, pose2 };
        enemy.artFacesLeft = true;
        enemy.color = Color.white;
        EditorUtility.SetDirty(enemy);
    }

    // Every enemy's particle color (EnemyData.particleColor: death pieces, hit sparks, spawn dust): its art's average
    // color (sprite + flower) times its tint. Recomputed on every build, so it follows new art.
    static void ParticleColors(params EnemyData[] enemies)
    {
        foreach (EnemyData enemy in enemies)
        {
            Color color = AverageColor(enemy.sprite, enemy.flower) * enemy.color;
            color.a = 1f;
            if (enemy.particleColor == color) continue;
            enemy.particleColor = color;
            EditorUtility.SetDirty(enemy);
        }
    }

    // The average color of the sprites' image files: opaque pixels only, the dark outlines left out (white if none).
    public static Color AverageColor(params Sprite[] sprites)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Sprite sprite in sprites)
        {
            string path = sprite != null ? AssetDatabase.GetAssetPath(sprite) : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));
            foreach (Color32 pixel in texture.GetPixels32())
            {
                if (pixel.a < 128 || pixel.r + pixel.g + pixel.b < 120) continue;
                sum += new Vector3(pixel.r, pixel.g, pixel.b);
                count++;
            }
            Object.DestroyImmediate(texture);
        }
        if (count == 0) return Color.white;
        sum /= count * 255f;
        return new Color(sum.x, sum.y, sum.z);
    }

    // An enemy's shadow (EnemyData.shadowSize / shadowOffset), set once: values tuned in the asset are kept.
    static void Shadow(EnemyData enemy, Vector2 size, Vector2 offset)
    {
        if (enemy.shadowSize != Vector2.zero) return;
        enemy.shadowSize = size;
        enemy.shadowOffset = offset;
        EditorUtility.SetDirty(enemy);
    }

    // A piece of enemy art (Assets/Art/Enemies/<name>.png), `pixelsPerUnit` px = 1 unit before the enemy's size scales
    // it (Enemy.Spawn: radius x 2). Returns null if the file isn't there.
    static Sprite EnemySprite(string name, float pixelsPerUnit) => PaintedSprite($"{EnemyArtFolder}/{name}.png", pixelsPerUnit);

    // The artist's dungeon pieces (Assets/Art/Dungeon/<name>.png): the ore vein OreHeight units tall; the crystal
    // pickup 1 unit tall (Pickup sizes it). Return null if the file isn't there.
    public static Sprite OreSprite() => SizedSprite($"{DungeonArtFolder}/Ore.png", OreHeight);
    public static Sprite CrystalSprite() => SizedSprite($"{DungeonArtFolder}/Crystal.png", 1f);

    // The title and town screens' art (Assets/Art/UI/<name>.png, baked by Tools/bake_title.py and bake_town.py; `name`
    // may include a folder, e.g. "Town/TownBackground"): smooth, no mipmaps, uncompressed (a compressed dark gradient
    // shows bands). Returns null if the file isn't there.
    public static Sprite MenuArt(string name)
    {
        string path = $"{UiFolder}/{name}.png";
        Sprite sprite = PaintedSprite(path, 100f);
        if (sprite == null) return null;
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureCompression == TextureImporterCompression.Uncompressed) return sprite;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // A weapon effect texture (Assets/Art/Effects/<name>.png), 1 unit wide like the placeholders it replaces.
    // Returns null if the file isn't there.
    public static Sprite EffectSprite(string name) => SizedSprite($"{EffectsFolder}/{name}.png", 1f, byWidth: true);

    // One of the artist's items (Assets/Art/Items/<name>.png), its whole image `units` tall (or wide, `byWidth`).
    // Returns null if the file isn't there.
    public static Sprite ItemSprite(string name, float units, bool byWidth = false) => SizedSprite($"{ItemsFolder}/{name}.png", units, byWidth);

    // One tile of the artist's cave (Assets/Art/Cave/<name>.png), 1 unit wide, with mipmaps (the painted floor would
    // shimmer when the camera scrolls if it were only shrunk). Returns null if the file isn't there.
    public static Sprite CaveSprite(string name) => SizedSprite($"{CaveFolder}/{name}.png", 1f, byWidth: true, mipmaps: true);

    // Painted art imported so its whole image is `units` tall (or wide, `byWidth`). Returns null if the file isn't there.
    static Sprite SizedSprite(string path, float units, bool byWidth = false, bool mipmaps = false)
    {
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path); // in case Unity hasn't picked up a freshly dropped file yet
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        return PaintedSprite(path, (byWidth ? width : height) / units, mipmaps);
    }

    // Painted art (not pixel art): smooth, center pivot, `pixelsPerUnit` px = 1 unit, mipmaps only if asked.
    // Returns null if the file isn't there.
    static Sprite PaintedSprite(string path, float pixelsPerUnit, bool mipmaps = false)
    {
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path); // in case Unity hasn't picked up a freshly dropped file yet
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
            Mathf.Abs(importer.spritePixelsPerUnit - pixelsPerUnit) > 0.01f || importer.mipmapEnabled != mipmaps ||
            settings.spriteAlignment != (int)SpriteAlignment.Center)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = mipmaps;
            importer.alphaIsTransparency = true;
            importer.ReadTextureSettings(settings); // re-read: the lines above changed the importer
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // A white sprite drawn from a formula, 1 world unit wide: alpha(r, angle) with r = 0 (center) to 1 (edge) and
    // angle in degrees, 0 = right, counter-clockwise. Made once (delete the file to remake it).
    static Sprite GeneratedSprite(string name, int size, System.Func<float, float, float> alpha)
    {
        string path = $"{ArtFolder}/{name}.png";
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, 360f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, r > 1f ? 0f : Mathf.Clamp01(alpha(r, angle))));
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

    // A white square whose edges fade out, with rounded corners, 9-sliced with a 31 px border: as a Sliced UI image it
    // makes an even, soft halo 31 units wide around whatever it's stretched behind. Made once (delete the file to remake it).
    static Sprite GlowSprite()
    {
        const int Size = 64, Border = 31;
        string path = $"{ArtFolder}/Glow.png";
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Distance outside the 2x2 px middle: 0 inside it, Border at the edge.
                float dx = Mathf.Max(0f, Border - x, x - (Size - 1 - Border));
                float dy = Mathf.Max(0f, Border - y, y - (Size - 1 - Border));
                float inside = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / Border);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, inside * inside));
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(Border, Border, Border, Border);
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // A white bar whose right end is a half circle, 1 unit tall, 9-sliced so only that end keeps its shape: drawn Sliced
    // and stretched, a strip with a flat start and a round tip (the boss's dash warning). Made once (delete the file to
    // remake it).
    static Sprite RoundEndSprite()
    {
        const int Size = 64, Half = Size / 2;
        string path = $"{ArtFolder}/RoundEnd.png";
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Left half: the bar. Right half: a half circle around the middle of the left half's right edge.
                float dx = Mathf.Max(0f, x + 0.5f - Half), dy = y + 0.5f - Half;
                float inside = Mathf.Clamp01(Half - Mathf.Sqrt(dx * dx + dy * dy)); // a 1 px soft edge
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, inside));
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Size;
            importer.spriteBorder = new Vector4(0, 0, Half, 0); // left, bottom, right, top: only the round end is kept
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // needed for Sliced drawing
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // A piece of the pixel-art tileset (Assets/Art/Tiles or Props), imported crisp: 16 px = 1 world unit,
    // point filtering, no compression, no mipmaps, center pivot. Returns null if the file isn't there.
    public static Sprite PixelSprite(string path)
    {
        if (!File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path); // in case Unity hasn't picked up a freshly dropped file yet
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
            Mathf.Abs(importer.spritePixelsPerUnit - PixelsPerTile) > 0.01f || importer.filterMode != FilterMode.Point ||
            importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerTile;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static Sprite TileSprite(string name) => PixelSprite($"{TilesFolder}/{name}.png");
    public static Sprite PropSprite(string name) => PixelSprite($"{PropsFolder}/{name}.png");

    // The Tilemap tile for a sprite (Assets/Art/Tiles/TileAssets/<name>.asset), created once.
    public static Tile TileAsset(Sprite sprite)
    {
        CreateFolder(TilesFolder, "TileAssets");
        string path = $"{TilesFolder}/TileAssets/{sprite.name}.asset";
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile != null) return tile;

        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        AssetDatabase.CreateAsset(tile, path);
        return tile;
    }

    static EnemyData EnemyAsset(string name, EnemyBehavior behavior, Color color, float radius, int hp, float speed,
        int contactDamage, int xp, float materialChance, int materialAmount, Sprite sprite, int crystals = 0,
        float goldChance = 0f, int gold = 0, int supplies = 0)
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
        enemy.crystalAmount = crystals;
        enemy.supplyAmount = supplies;
        enemy.goldChance = goldChance;
        enemy.goldAmount = gold;
        enemy.sprite = sprite;
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
            new NpcDefinition { name = "Guard",  faction = "Soldiers", startPopulation = 100, unlocksEvolution = true, weapon = WeaponId.Spear,
                                thanks = "Good. My soldiers will put it to use.", workGold = 20, color = new Color(1f, 0.80f, 0.30f) },
            new NpcDefinition { name = "Shaman", faction = "Mages",    startPopulation = 50,  unlocksEvolution = true, weapon = WeaponId.CrystalOrbit,
                                thanks = "The crystals approve. So do I.", workGold = 10, workCrystals = 1, color = new Color(0.65f, 0.45f, 0.95f) },
            new NpcDefinition { name = "Elder",  faction = "Peasants", startPopulation = 200, unlocksEvolution = true, weapon = WeaponId.Dagger,
                                thanks = "Bless you. That will hold a while longer.", workMaterials = 8, color = new Color(0.85f, 0.65f, 0.35f) },
        }; // their district buildings are TownConfig.buildings (code defaults); portraits are filled in by EnsureAssets
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
    // Extra canvases in a scene (e.g. damage numbers) pass eventSystem: false; lower sortingOrder = drawn below.
    public static Transform NewCanvas(string name, int sortingOrder = 0, bool eventSystem = true)
    {
        if (eventSystem) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<Canvas>().sortingOrder = sortingOrder;
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

    // Moves a UI element (e.g. a button made by NewButton) within its parent.
    public static void Place(Component element, Vector2 position)
    {
        ((RectTransform)element.transform).anchoredPosition = position;
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
        text.lineSpacing = lineSpacing;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // Main buttons (Play, DIVE, Resume, Next, ...) use the title font; buttons whose labels carry info and numbers (the
    // leader and building buttons) pass useTitleFont: false and use the text font.
    public static Button NewButton(string label, Transform parent, Vector2 size, int fontSize = 26, bool useTitleFont = true)
    {
        Image image = NewImage(label, parent, Vector2.zero, size, Color.white);
        Button button = image.gameObject.AddComponent<Button>();
        Text text = NewText("Label", image.transform, Vector2.zero, size - new Vector2(16, 8), fontSize, TextAnchor.MiddleCenter);
        text.text = label;
        if (useTitleFont) InTitleFont(text);
        StyleButton(button, image, text);
        return button;
    }

    // Switches a text to the title font.
    public static Text InTitleFont(Text text)
    {
        text.font = titleFont;
        text.lineSpacing = 1f;
        return text;
    }

    // A title: the title font in the title color.
    public static Text Title(Text text)
    {
        text.color = TitleColor;
        return InTitleFont(text);
    }

    // ---------- UI look: dark with crystal purple ----------

    public static readonly Color BackgroundColor = new Color(0.07f, 0.06f, 0.09f);
    public static readonly Color PanelColor = new Color(0.08f, 0.06f, 0.11f, 0.94f);
    public static readonly Color BorderColor = new Color(0.55f, 0.35f, 0.85f);
    public static readonly Color FrameColor = new Color(0.43f, 0.59f, 0.92f); // steel blue: the artist's frame
    // How thin the frame is drawn (the art's 80-150 px edges / this, in canvas units) on each kind of element.
    public const float PanelFrameThickness = 2f, CardFrameThickness = 2.5f, HudFrameThickness = 3f, ButtonFrameThickness = 4f;
    public static readonly Color TitleColor = new Color(0.82f, 0.70f, 1f);
    public static readonly Color AccentColor = new Color(0.65f, 0.45f, 0.95f); // bar fills, slider fills
    static readonly Color ButtonColor = new Color(0.22f, 0.16f, 0.32f);
    static readonly Color ButtonHover = new Color(0.36f, 0.26f, 0.52f);
    static readonly Color ButtonPressed = new Color(0.15f, 0.10f, 0.22f);
    static readonly Color ButtonDisabled = new Color(0.14f, 0.13f, 0.16f);

    // A dark panel in the artist's frame (steel blue; a thin violet outline if the frame art is missing). The frame is
    // its first child, named "Frame" (PanelFrame(panel) finds it, e.g. to tint the leader window).
    public static Image NewPanel(string name, Transform parent, Vector2 position, Vector2 size)
    {
        Image panel = NewImage(name, parent, position, size, PanelColor);
        if (AddFrame(panel.transform, PanelFrameThickness, FrameColor) == null)
        {
            Outline border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = BorderColor;
            border.effectDistance = new Vector2(3f, -3f);
            border.useGraphicAlpha = false; // a solid border even on a see-through panel
        }
        return panel;
    }

    public static OrnateFrame PanelFrame(Image panel)
    {
        Transform frame = panel.transform.Find("Frame");
        return frame != null ? frame.GetComponent<OrnateFrame>() : null;
    }

    // The artist's frame over `parent` (an OrnateFrame of 12 anchored pieces: the corners and the top / bottom
    // ornaments keep their shape, the ornaments stay centered, the plain line between them and the sides stretch),
    // tinted `color`. `thickness`: how much smaller than the art it's drawn (PanelFrameThickness ...), and smaller still
    // if its fixed pieces wouldn't fit the parent's size. Behind the parent's other children, or over them (`onTop`:
    // call it after they're made, e.g. around the portrait). Null (nothing added) without the frame art.
    public static OrnateFrame AddFrame(Transform parent, float thickness, Color color, bool onTop = false)
    {
        if (framePieces == null) return null;
        RectTransform parentRect = (RectTransform)parent;
        Vector2 size = parentRect.rect.size;
        if (size.x <= 0f || size.y <= 0f) size = parentRect.sizeDelta;
        float k = Mathf.Min(1f / thickness, size.x / (FrameTopLeft + FrameTopMiddle + FrameTopRight),
            size.x / (FrameBottomLeft + FrameBottomMiddle + FrameBottomRight), size.y / (FrameTop + FrameBottom));

        RectTransform root = NewUI("Frame", parent, Vector2.zero, Vector2.zero);
        Stretch(root);
        if (!onTop) root.SetAsFirstSibling();
        float top = FrameTop * k, bottom = FrameBottom * k, side = FrameSide * k;
        float topLeft = FrameTopLeft * k, topMiddle = FrameTopMiddle * k / 2f, topRight = FrameTopRight * k;
        float bottomLeft = FrameBottomLeft * k, bottomMiddle = FrameBottomMiddle * k / 2f, bottomRight = FrameBottomRight * k;
        const float Overlap = 0.5f; // the stretching strips reach a little under their neighbours: no hairline gaps

        Image[] pieces = new Image[FramePieceNames.Length];
        Image Piece(int i, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            Image piece = NewImage(FramePieceNames[i], root, Vector2.zero, Vector2.zero, color);
            piece.sprite = framePieces[i];
            piece.raycastTarget = false;
            RectTransform rect = piece.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return pieces[i] = piece;
        }
        // Top band (anchored to the top edge)
        Piece(0, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -top), new Vector2(topLeft, 0f));
        Piece(1, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(topLeft - Overlap, -top), new Vector2(-topMiddle + Overlap, 0f));
        Piece(2, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-topMiddle, -top), new Vector2(topMiddle, 0f));
        Piece(3, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(topMiddle - Overlap, -top), new Vector2(-topRight + Overlap, 0f));
        Piece(4, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-topRight, -top), new Vector2(0f, 0f));
        // Bottom band (anchored to the bottom edge)
        Piece(5, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(bottomLeft, bottom));
        Piece(6, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(bottomLeft - Overlap, 0f), new Vector2(-bottomMiddle + Overlap, bottom));
        Piece(7, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-bottomMiddle, 0f), new Vector2(bottomMiddle, bottom));
        Piece(8, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(bottomMiddle - Overlap, 0f), new Vector2(-bottomRight + Overlap, bottom));
        Piece(9, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-bottomRight, 0f), new Vector2(0f, bottom));
        // Sides (between the bands)
        Piece(10, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, bottom - Overlap), new Vector2(side, -top + Overlap));
        Piece(11, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-side, bottom - Overlap), new Vector2(0f, -top + Overlap));

        OrnateFrame frame = root.gameObject.AddComponent<OrnateFrame>();
        frame.pieces = pieces;
        return frame;
    }

    // The frame's 12 pieces (Assets/Art/UI/Frame/Frame_<name>.png), smooth, no mipmaps; null if any is missing.
    static Sprite[] FramePieces()
    {
        Sprite[] pieces = new Sprite[FramePieceNames.Length];
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i] = PaintedSprite($"{FrameFolder}/Frame_{FramePieceNames[i]}.png", 100f);
            if (pieces[i] == null) return null;
        }
        return pieces;
    }

    // Violet button states (lighter on hover, darker when pressed, grey when it can't be used; the image is white so
    // these are the real colors), a label that dims while the button can't be used, and no keyboard navigation (WASD
    // moves the hero, it shouldn't move a UI selection).
    public static void StyleButton(Button button, Image image, Text label, float frameThickness = ButtonFrameThickness)
    {
        image.color = Color.white;
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHover;
        colors.pressedColor = ButtonPressed;
        colors.selectedColor = ButtonColor; // no "stuck" highlight after a click
        colors.disabledColor = ButtonDisabled;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ButtonLabelDim dim = button.gameObject.AddComponent<ButtonLabelDim>();
        dim.button = button;
        dim.label = label;
        dim.labelColor = label.color;
        dim.frame = AddFrame(image.transform, frameThickness, FrameColor); // dimmed with the label; leaders recolor it
        dim.frameColor = FrameColor;
        button.gameObject.AddComponent<ButtonMotion>().button = button; // grows on hover, bounces on click
    }

    // The panel pops in (scale + fade) every time it's shown.
    public static void AddPopIn(Component panel, float startScale = 0.9f)
    {
        panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.AddComponent<PopIn>().startScale = startScale;
    }

    // A dark outline so HUD text reads over the cave floor.
    public static Text Shadowed(Text text)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
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
        Image background = NewImage(name, parent, position, size, new Color(0.10f, 0.08f, 0.13f, 0.9f));
        fill = NewImage("Fill", background.transform, Vector2.zero, Vector2.zero, color).rectTransform;
        Stretch(fill);
        return background.rectTransform;
    }
}
