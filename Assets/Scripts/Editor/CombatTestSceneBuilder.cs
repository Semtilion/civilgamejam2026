using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Menu: Jam > Build Combat Test Scene.
// Creates the placeholder data assets (skipped if they already exist, so tuned values are kept)
// and builds Assets/Scenes/CombatTest.unity with a ready-to-play fight.
public static class CombatTestSceneBuilder
{
    const string SkillsFolder = "Assets/Data/Skills";
    const string UnitsFolder = "Assets/Data/Units";
    const string ScenePath = "Assets/Scenes/CombatTest.unity";

    static Font font;

    [MenuItem("Jam/Build Combat Test Scene")]
    static void Build()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Combat Test", "CombatTest.unity already exists. Rebuild it?\n(Data assets are kept.)", "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // ---------- Data (placeholder numbers, tune them in the Inspector) ----------
        CreateFolder("Assets", "Data");
        CreateFolder("Assets/Data", "Skills");
        CreateFolder("Assets/Data", "Units");

        //                     name             target             reach  mana  dmg/heal  acc
        SkillData slash      = Skill("Slash",         SkillTarget.Single, 1, 2,  0,  4, 7,   90);
        SkillData thrust     = Skill("Thrust",        SkillTarget.Single, 1, 3,  0,  3, 5,   85, critBonus: 10);
        SkillData firebolt   = Skill("Firebolt",      SkillTarget.Single, 1, 4,  8,  6, 9,   85);
        SkillData shockwave  = Skill("Shockwave",     SkillTarget.All,    1, 4, 15,  3, 5,   80);
        SkillData mend       = Skill("Mend",          SkillTarget.Self,   1, 4, 10,  8, 12, 100);
        SkillData firebomb   = Skill("Firebomb",      SkillTarget.All,    3, 4,  0,  5, 8,   95);
        SkillData hpPotion   = Skill("Health Potion", SkillTarget.Self,   1, 4,  0, 15, 15, 100);
        SkillData mpPotion   = Skill("Mana Potion",   SkillTarget.Self,   1, 4,  0,  0, 0,  100, manaRestore: 15);
        SkillData bite       = Skill("Bite",          SkillTarget.Single, 1, 4,  0,  3, 5,   85);
        SkillData spit       = Skill("Spit",          SkillTarget.Single, 1, 4,  0,  2, 4,   85);
        SkillData slam       = Skill("Slam",          SkillTarget.Single, 1, 4,  0,  5, 8,   75);

        //                     name               color                          hp mana spd crit dodge
        UnitData heroData = UnitAsset("Hero",            new Color(0.30f, 0.50f, 0.90f), 40, 30, 10, 5, 10, slash, thrust, firebolt, shockwave, mend);
        UnitData crawler  = UnitAsset("Crystal Crawler", new Color(0.60f, 0.30f, 0.70f), 12,  0,  4, 5,  5, bite);
        UnitData spitter  = UnitAsset("Shard Spitter",   new Color(0.30f, 0.70f, 0.70f),  8,  0,  6, 5, 15, spit);
        UnitData brute    = UnitAsset("Crystal Brute",   new Color(0.70f, 0.30f, 0.30f), 25,  0,  2, 5,  0, slam);
        AssetDatabase.SaveAssets();

        // ---------- Scene ----------
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.07f, 0.07f);
        camera.transform.position = new Vector3(0f, 0f, -10f);

        // The project uses the new Input System, so the UI needs its input module (default actions are assigned at runtime).
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform canvas = canvasObject.transform;

        // Combat log (top)
        Image logPanel = NewImage("Log Panel", canvas, new Vector2(0, 400), new Vector2(1440, 220), new Color(0f, 0f, 0f, 0.5f));
        Text log = NewText("Log", logPanel.transform, Vector2.zero, new Vector2(1400, 200), 22, TextAnchor.UpperLeft);

        // Hero (left) and enemy ranks 1-4 (right). Disabled enemies drop out of the layout, so the rest step forward.
        Unit hero = NewUnit("Hero", canvas, new Vector2(-600, 60), true);

        RectTransform enemyRow = NewRow("Enemies", canvas, new Vector2(420, 60), new Vector2(760, 320), 40, TextAnchor.MiddleLeft);
        Unit[] enemySlots = new Unit[4];
        for (int i = 0; i < enemySlots.Length; i++)
            enemySlots[i] = NewUnit($"Enemy Rank {i + 1}", enemyRow, Vector2.zero, false);

        // Skill and item buttons (bottom)
        RectTransform skillRow = NewRow("Skills", canvas, new Vector2(0, -330), new Vector2(1400, 90), 15, TextAnchor.MiddleCenter);
        Button[] skillButtons = new Button[5];
        for (int i = 0; i < skillButtons.Length; i++)
            skillButtons[i] = NewButton($"Skill {i + 1}", skillRow, new Vector2(220, 80));

        RectTransform itemRow = NewRow("Items", canvas, new Vector2(0, -430), new Vector2(1400, 80), 15, TextAnchor.MiddleCenter);
        Button[] itemButtons = new Button[3];
        for (int i = 0; i < itemButtons.Length; i++)
            itemButtons[i] = NewButton($"Item {i + 1}", itemRow, new Vector2(220, 70));

        // Result overlay (last child so it draws on top and blocks clicks)
        Image resultPanel = NewImage("Result Panel", canvas, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f));
        Stretch(resultPanel.rectTransform);
        Text resultText = NewText("Result", resultPanel.transform, new Vector2(0, 60), new Vector2(800, 120), 72, TextAnchor.MiddleCenter);
        Button restartButton = NewButton("Restart", resultPanel.transform, new Vector2(260, 80));
        ((RectTransform)restartButton.transform).anchoredPosition = new Vector2(0, -80);

        // Combat manager wired to everything above
        CombatManager manager = new GameObject("Combat Manager").AddComponent<CombatManager>();
        manager.heroData = heroData;
        manager.enemyGroup = new List<UnitData> { brute, crawler, spitter };
        manager.items = new List<ItemStack>
        {
            new ItemStack { item = firebomb, count = 2 },
            new ItemStack { item = hpPotion, count = 2 },
            new ItemStack { item = mpPotion, count = 2 },
        };
        manager.hero = hero;
        manager.enemySlots = enemySlots;
        manager.skillButtons = skillButtons;
        manager.itemButtons = itemButtons;
        manager.logText = log;
        manager.resultPanel = resultPanel.gameObject;
        manager.resultText = resultText;
        manager.restartButton = restartButton;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildList();
        Debug.Log("Combat test scene built: " + ScenePath + ". Press Play to fight.");
    }

    // ---------- Data helpers ----------

    static void CreateFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    static SkillData Skill(string name, SkillTarget target, int minRank, int maxRank, int manaCost,
        int minAmount, int maxAmount, int accuracy, int critBonus = 0, int manaRestore = 0)
    {
        string path = $"{SkillsFolder}/{name}.asset";
        SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill != null) return skill; // keep tuned values

        skill = ScriptableObject.CreateInstance<SkillData>();
        skill.displayName = name;
        skill.target = target;
        skill.minRank = minRank;
        skill.maxRank = maxRank;
        skill.manaCost = manaCost;
        skill.minAmount = minAmount;
        skill.maxAmount = maxAmount;
        skill.baseAccuracy = accuracy;
        skill.critBonus = critBonus;
        skill.manaRestore = manaRestore;
        AssetDatabase.CreateAsset(skill, path);
        return skill;
    }

    static UnitData UnitAsset(string name, Color color, int hp, int mana, int speed, int crit, int dodge, params SkillData[] skills)
    {
        string path = $"{UnitsFolder}/{name}.asset";
        UnitData unit = AssetDatabase.LoadAssetAtPath<UnitData>(path);
        if (unit != null) return unit; // keep tuned values

        unit = ScriptableObject.CreateInstance<UnitData>();
        unit.displayName = name;
        unit.placeholderColor = color;
        unit.maxHp = hp;
        unit.maxMana = mana;
        unit.speed = speed;
        unit.critChance = crit;
        unit.dodge = dodge;
        unit.skills = skills.ToList();
        AssetDatabase.CreateAsset(unit, path);
        return unit;
    }

    static void AddSceneToBuildList()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
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
        NewText("Label", image.transform, Vector2.zero, size, 20, TextAnchor.MiddleCenter).text = label;
        return button;
    }

    // A row that lines up its children left to right at their own size.
    static RectTransform NewRow(string name, Transform parent, Vector2 position, Vector2 size, float spacing, TextAnchor alignment)
    {
        RectTransform row = NewUI(name, parent, position, size);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    // Placeholder combatant: highlight frame, colored body, name, HP bar (+ mana bar for the hero), status text, ATB gauge.
    static Unit NewUnit(string name, Transform parent, Vector2 position, bool showMana)
    {
        RectTransform root = NewUI(name, parent, position, new Vector2(160, 300));
        Unit unit = root.gameObject.AddComponent<Unit>();

        unit.highlight = NewImage("Highlight", root, new Vector2(0, 20), new Vector2(156, 216), new Color(1f, 0.85f, 0.2f)).gameObject;
        unit.body = NewImage("Body", root, new Vector2(0, 20), new Vector2(140, 200), Color.gray);
        unit.nameText = NewText("Name", root, new Vector2(0, 140), new Vector2(240, 30), 20, TextAnchor.MiddleCenter);
        unit.hpFill = NewBar("HP Bar", root, new Vector2(0, -95), new Vector2(150, 14), new Color(0.80f, 0.15f, 0.15f));
        if (showMana)
            unit.manaFill = NewBar("Mana Bar", root, new Vector2(0, -113), new Vector2(150, 10), new Color(0.25f, 0.45f, 0.95f));
        unit.statusText = NewText("Status", root, new Vector2(0, -138), new Vector2(260, 26), 18, TextAnchor.MiddleCenter);
        unit.atbFill = NewBar("ATB Bar", root, new Vector2(0, -160), new Vector2(150, 8), new Color(0.90f, 0.90f, 0.90f));
        return unit;
    }

    // Dark background with a fill stretched over it. Unit moves the fill's right anchor.
    static RectTransform NewBar(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        Image background = NewImage(name, parent, position, size, new Color(0.15f, 0.15f, 0.15f));
        Image fill = NewImage("Fill", background.transform, Vector2.zero, Vector2.zero, color);
        Stretch(fill.rectTransform);
        return fill.rectTransform;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
