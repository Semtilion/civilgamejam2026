using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Town.unity (called from Jam > Build Game Scenes): the hamlet-style hub with a status bar,
// news, one button per faction leader, the detail panel (Give button), the Dive button and the epilogue screen.
public static class TownSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Town.unity";

    public static void Build(GameAssets a)
    {
        var scene = NewSceneFromTemplate(ScenePath);
        Camera camera = Object.FindAnyObjectByType<Camera>(); // the template has exactly one
        camera.backgroundColor = Color.black;

        Transform canvas = NewCanvas("Town UI");
        Image background = NewImage("Background (placeholder town)", canvas, Vector2.zero, Vector2.zero, new Color(0.13f, 0.11f, 0.10f));
        Stretch(background.rectTransform);

        // Top: status bar and news
        Text status = NewText("Status", canvas, new Vector2(0, -30), new Vector2(1860, 50), 26, TextAnchor.MiddleCenter);
        Pin(status.rectTransform, new Vector2(0.5f, 1f));
        Image newsPanel = NewImage("News Panel", canvas, new Vector2(0, 330), new Vector2(1700, 190), new Color(0f, 0f, 0f, 0.5f));
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

        // Bottom: detail panel for the selected leader
        Image detail = NewImage("Detail Panel", canvas, new Vector2(-100, -230), new Vector2(1200, 400), new Color(0f, 0f, 0f, 0.6f));
        Text detailTitle = NewText("Title", detail.transform, new Vector2(0, 160), new Vector2(1100, 60), 36, TextAnchor.MiddleCenter);
        Text detailBody = NewText("Body", detail.transform, new Vector2(0, 15), new Vector2(1100, 230), 22, TextAnchor.UpperLeft);
        Button giveButton = NewButton("Give", detail.transform, new Vector2(380, 56), 24);
        ((RectTransform)giveButton.transform).anchoredPosition = new Vector2(0, -158);

        // Bottom-right: dive ("Defend the town" on the horde day)
        Button dive = NewButton("DIVE", canvas, new Vector2(360, 110), 36);
        RectTransform diveRect = (RectTransform)dive.transform;
        diveRect.anchoredPosition = new Vector2(-40, 40);
        Pin(diveRect, new Vector2(1f, 0f));

        // Epilogue: covers everything once the game has ended (last child, so it draws on top and blocks clicks)
        Image epilogue = NewImage("Epilogue", canvas, Vector2.zero, Vector2.zero, new Color(0.05f, 0.04f, 0.04f, 0.96f));
        Stretch(epilogue.rectTransform);
        Text epilogueTitle = NewText("Title", epilogue.transform, new Vector2(0, 330), new Vector2(1600, 100), 72, TextAnchor.MiddleCenter);
        Text epilogueBody = NewText("Body", epilogue.transform, new Vector2(0, 20), new Vector2(1500, 460), 30, TextAnchor.MiddleCenter);
        Button newGame = NewButton("New game", epilogue.transform, new Vector2(360, 100), 36);
        ((RectTransform)newGame.transform).anchoredPosition = new Vector2(0, -350);
        epilogue.gameObject.SetActive(false);

        TownManager town = new GameObject("Town Manager").AddComponent<TownManager>();
        town.config = a.townConfig;
        town.statusText = status;
        town.newsText = news;
        town.npcButtons = npcButtons;
        town.npcLabels = npcLabels;
        town.detailTitle = detailTitle;
        town.detailBody = detailBody;
        town.giveButton = giveButton;
        town.diveButton = dive;
        town.epiloguePanel = epilogue.gameObject;
        town.epilogueTitle = epilogueTitle;
        town.epilogueBody = epilogueBody;
        town.newGameButton = newGame;

        EditorSceneManager.SaveScene(scene);
    }
}
