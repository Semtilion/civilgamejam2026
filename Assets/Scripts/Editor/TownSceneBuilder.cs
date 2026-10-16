using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Town.unity (called from Jam > Build Game Scenes): the hamlet-style hub with a status bar,
// news, one button per NPC, the NPC detail panel (Give / weapon upgrades) and the Dive button.
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

        // Middle: one button per NPC
        int npcCount = a.townConfig.npcs.Length;
        RectTransform npcRow = NewRow("NPCs", canvas, new Vector2(0, 110), new Vector2(1760, 220), 40);
        Button[] npcButtons = new Button[npcCount];
        Text[] npcLabels = new Text[npcCount];
        for (int i = 0; i < npcCount; i++)
        {
            npcButtons[i] = NewButton($"NPC {i + 1}", npcRow, new Vector2(410, 210), 24);
            npcLabels[i] = npcButtons[i].GetComponentInChildren<Text>();
        }

        // Bottom: detail panel for the selected NPC
        Image detail = NewImage("Detail Panel", canvas, new Vector2(-100, -230), new Vector2(1200, 400), new Color(0f, 0f, 0f, 0.6f));
        Text detailTitle = NewText("Title", detail.transform, new Vector2(0, 160), new Vector2(1100, 60), 40, TextAnchor.MiddleCenter);
        Text detailBody = NewText("Body", detail.transform, new Vector2(0, 65), new Vector2(1100, 140), 24, TextAnchor.UpperLeft);
        Button giveButton = NewButton("Give", detail.transform, new Vector2(380, 60), 24);
        ((RectTransform)giveButton.transform).anchoredPosition = new Vector2(-200, -45);
        Button repairButton = NewButton("Repair", detail.transform, new Vector2(380, 60), 24);
        ((RectTransform)repairButton.transform).anchoredPosition = new Vector2(200, -45);
        RectTransform upgradeRow = NewRow("Weapon Upgrades", detail.transform, new Vector2(0, -140), new Vector2(1100, 90), 20);
        Button[] upgradeButtons = new Button[Weapon.Names.Length];
        for (int i = 0; i < upgradeButtons.Length; i++)
            upgradeButtons[i] = NewButton(Weapon.Names[i], upgradeRow, new Vector2(260, 80), 20);

        // Bottom-right: reinforce (one-time) and dive
        Button reinforce = NewButton("Reinforce", canvas, new Vector2(360, 90), 24);
        RectTransform reinforceRect = (RectTransform)reinforce.transform;
        reinforceRect.anchoredPosition = new Vector2(-40, 170);
        Pin(reinforceRect, new Vector2(1f, 0f));
        Button dive = NewButton("DIVE", canvas, new Vector2(360, 110), 40);
        RectTransform diveRect = (RectTransform)dive.transform;
        diveRect.anchoredPosition = new Vector2(-40, 40);
        Pin(diveRect, new Vector2(1f, 0f));

        TownManager town = new GameObject("Town Manager").AddComponent<TownManager>();
        town.config = a.townConfig;
        town.statusText = status;
        town.newsText = news;
        town.npcButtons = npcButtons;
        town.npcLabels = npcLabels;
        town.detailTitle = detailTitle;
        town.detailBody = detailBody;
        town.giveButton = giveButton;
        town.repairButton = repairButton;
        town.upgradeButtons = upgradeButtons;
        town.reinforceButton = reinforce;
        town.diveButton = dive;

        EditorSceneManager.SaveScene(scene);
    }
}
