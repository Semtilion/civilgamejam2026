using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Town.unity (called from Jam > Build Game Scenes): the hamlet-style hub with a status bar,
// news, one button per faction leader, the detail panel (Give button, district buildings), the relics box, the Dive button,
// the "?" button, the epilogue screen and the How to play popup.
public static class TownSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Town.unity";

    public static void Build(GameAssets a)
    {
        var scene = NewSceneFromTemplate(ScenePath);
        Camera camera = Object.FindAnyObjectByType<Camera>(); // the template has exactly one
        camera.backgroundColor = Color.black;

        Transform canvas = NewCanvas("Town UI");
        Image background = NewImage("Background (placeholder town)", canvas, Vector2.zero, Vector2.zero, BackgroundColor);
        Stretch(background.rectTransform);

        // Top: status bar and news
        Text status = NewText("Status", canvas, new Vector2(0, -30), new Vector2(1860, 50), 24, TextAnchor.MiddleCenter);
        Pin(status.rectTransform, new Vector2(0.5f, 1f));
        Image newsPanel = NewPanel("News Panel", canvas, new Vector2(0, 330), new Vector2(1700, 190));
        Text news = NewText("News", newsPanel.transform, Vector2.zero, new Vector2(1660, 170), 22, TextAnchor.UpperLeft);

        // Middle: one button per faction leader
        int npcCount = a.townConfig.npcs.Length;
        RectTransform npcRow = NewRow("NPCs", canvas, new Vector2(0, 110), new Vector2(1760, 220), 40);
        Button[] npcButtons = new Button[npcCount];
        Text[] npcLabels = new Text[npcCount];
        for (int i = 0; i < npcCount; i++)
        {
            npcButtons[i] = NewButton($"NPC {i + 1}", npcRow, new Vector2(520, 210), 24);
            npcLabels[i] = npcButtons[i].GetComponentInChildren<Text>();
        }

        // Bottom: detail panel for the selected leader, with their district's buildings
        Image detail = NewPanel("Detail Panel", canvas, new Vector2(-100, -230), new Vector2(1200, 400));
        Text detailTitle = NewText("Title", detail.transform, new Vector2(0, 160), new Vector2(1100, 60), 36, TextAnchor.MiddleCenter);
        detailTitle.color = TitleColor;
        Text detailBody = NewText("Body", detail.transform, new Vector2(0, 40), new Vector2(1100, 180), 20, TextAnchor.UpperLeft);
        RectTransform buildingRow = NewRow("District", detail.transform, new Vector2(0, -100), new Vector2(1160, 80), 15);
        Button[] buildingButtons = new Button[MostBuildingsInADistrict(a.townConfig)];
        for (int i = 0; i < buildingButtons.Length; i++) buildingButtons[i] = NewButton($"Building {i + 1}", buildingRow, new Vector2(360, 80), 17);
        Button giveButton = NewButton("Give", detail.transform, new Vector2(380, 48), 22);
        ((RectTransform)giveButton.transform).anchoredPosition = new Vector2(0, -170);

        // Right of the detail panel: the relics you own
        Image relicsPanel = NewPanel("Relics Panel", canvas, new Vector2(720, -210), new Vector2(400, 340));
        Text relicsText = NewText("Relics", relicsPanel.transform, Vector2.zero, new Vector2(360, 310), 22, TextAnchor.UpperLeft);

        // Bottom-right: dive ("Defend the town" on the horde day)
        Button dive = NewButton("DIVE", canvas, new Vector2(360, 110), 36);
        RectTransform diveRect = (RectTransform)dive.transform;
        diveRect.anchoredPosition = new Vector2(-40, 40);
        Pin(diveRect, new Vector2(1f, 0f));

        // Top-right: "?" opens How to play
        Button help = NewButton("?", canvas, new Vector2(70, 70), 44);
        RectTransform helpRect = (RectTransform)help.transform;
        helpRect.anchoredPosition = new Vector2(-20, -20);
        Pin(helpRect, new Vector2(1f, 1f));

        // Epilogue: covers everything once the game has ended (draws over the town and blocks its clicks)
        Image epilogue = NewImage("Epilogue", canvas, Vector2.zero, Vector2.zero, new Color(0.05f, 0.04f, 0.07f, 0.97f));
        Stretch(epilogue.rectTransform);
        Text epilogueTitle = NewText("Title", epilogue.transform, new Vector2(0, 330), new Vector2(1600, 100), 72, TextAnchor.MiddleCenter);
        epilogueTitle.color = TitleColor;
        Text epilogueBody = NewText("Body", epilogue.transform, new Vector2(0, 20), new Vector2(1500, 460), 30, TextAnchor.MiddleCenter);
        Button newGame = NewButton("New game", epilogue.transform, new Vector2(360, 100), 36);
        Place(newGame, new Vector2(-220, -350));
        Button titleScreen = NewButton("Title screen", epilogue.transform, new Vector2(360, 100), 36);
        Place(titleScreen, new Vector2(220, -350));
        epilogue.gameObject.SetActive(false);

        HowToPlay howToPlay = MenuBuilder.HowToPlayPopup(canvas); // last: on top of everything

        TownManager town = new GameObject("Town Manager").AddComponent<TownManager>();
        town.config = a.townConfig;
        town.statusText = status;
        town.newsText = news;
        town.npcButtons = npcButtons;
        town.npcLabels = npcLabels;
        town.detailTitle = detailTitle;
        town.detailBody = detailBody;
        town.giveButton = giveButton;
        town.buildingButtons = buildingButtons;
        town.diveButton = dive;
        town.relicsText = relicsText;
        town.epiloguePanel = epilogue.gameObject;
        town.epilogueTitle = epilogueTitle;
        town.epilogueBody = epilogueBody;
        town.newGameButton = newGame;
        town.titleButton = titleScreen;
        town.helpButton = help;
        town.howToPlay = howToPlay;

        EditorSceneManager.SaveScene(scene);
    }

    // The detail panel needs one building button per building in the biggest district.
    static int MostBuildingsInADistrict(TownConfig config)
    {
        int most = 0;
        for (int npc = 0; npc < config.npcs.Length; npc++)
            most = Mathf.Max(most, config.buildings.Count(b => b.district == npc));
        return most;
    }
}
