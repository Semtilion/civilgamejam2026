using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Town.unity (called from Jam > Build Game Scenes): the hamlet-style hub with a status bar,
// news, one button per faction leader, the relics box, the Dive button, the "?" button, the leader window (portrait,
// dialogue, district buildings, Give button), the epilogue screen and the How to play popup.
public static class TownSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Town.unity";

    public static void Build(GameAssets a)
    {
        var scene = NewSceneFromTemplate(ScenePath);
        Camera camera = Object.FindAnyObjectByType<Camera>(); // the template has exactly one
        camera.backgroundColor = Color.black;

        // Everything goes under a full-screen "Shake Root", so an earthquake can shake the whole town screen
        Transform canvasRoot = NewCanvas("Town UI");
        RectTransform shakeRoot = NewUI("Shake Root", canvasRoot, Vector2.zero, Vector2.zero);
        Stretch(shakeRoot);
        Transform canvas = shakeRoot;
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

        // Bottom right: the relics you own
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

        // Leader window: a dark backdrop over the town (click it to close) and, beside it rather than on it (so clicks in
        // the window don't reach it), the window: portrait on the left; name, dialogue, buildings and Give on the right.
        RectTransform leaderWindow = NewUI("Leader Window", canvas, Vector2.zero, Vector2.zero);
        Stretch(leaderWindow);
        Image backdropImage = NewImage("Backdrop", leaderWindow, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        Stretch(backdropImage.rectTransform);
        Button backdrop = backdropImage.gameObject.AddComponent<Button>();
        backdrop.transition = Selectable.Transition.None;
        backdrop.navigation = new Navigation { mode = Navigation.Mode.None };

        Image window = NewPanel("Window", leaderWindow, Vector2.zero, new Vector2(1560, 680));
        Image frame = NewImage("Portrait Frame", window.transform, new Vector2(-490, 0), new Vector2(540, 620), new Color(0.04f, 0.03f, 0.06f));
        Image portrait = NewImage("Portrait", frame.transform, Vector2.zero, new Vector2(520, 600), Color.white);
        portrait.preserveAspect = true; // wider box than the busts, so each one fills the height
        portrait.raycastTarget = false;
        Text leaderTitle = NewText("Title", window.transform, new Vector2(280, 290), new Vector2(940, 60), 36, TextAnchor.MiddleLeft);
        leaderTitle.color = TitleColor;
        Text dialogueText = NewText("Dialogue", window.transform, new Vector2(280, 100), new Vector2(940, 300), 22, TextAnchor.UpperLeft);
        dialogueText.raycastTarget = true; // click to show it all at once
        Typewriter dialogue = dialogueText.gameObject.AddComponent<Typewriter>();
        RectTransform buildingRow = NewRow("District", window.transform, new Vector2(280, -130), new Vector2(940, 100), 12);
        Button[] buildingButtons = new Button[MostBuildingsInADistrict(a.townConfig)];
        for (int i = 0; i < buildingButtons.Length; i++) buildingButtons[i] = NewButton($"Building {i + 1}", buildingRow, new Vector2(305, 100), 17);
        Button giveButton = NewButton("Give", window.transform, new Vector2(380, 56), 22);
        Place(giveButton, new Vector2(280, -250));
        Button close = NewButton("X", window.transform, new Vector2(56, 56), 30);
        Place(close, new Vector2(732, 292));
        AddPopIn(window);
        leaderWindow.gameObject.SetActive(false);

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
        AddPopIn(epilogue, 0.97f);
        epilogue.gameObject.SetActive(false);

        HowToPlay howToPlay = MenuBuilder.HowToPlayPopup(canvas); // on top of the town

        // The quake flash: over everything, see-through until it flashes (UIShake)
        Image quakeFlash = NewImage("Quake Flash", canvasRoot, Vector2.zero, Vector2.zero, new Color(1f, 0.85f, 0.6f, 0f));
        Stretch(quakeFlash.rectTransform);
        quakeFlash.raycastTarget = false;
        UIShake quakeShake = new GameObject("Quake Shake").AddComponent<UIShake>();
        quakeShake.target = shakeRoot;
        quakeShake.flash = quakeFlash;

        TownManager town = new GameObject("Town Manager").AddComponent<TownManager>();
        town.config = a.townConfig;
        town.statusText = status;
        town.newsText = news;
        town.npcButtons = npcButtons;
        town.npcLabels = npcLabels;
        town.diveButton = dive;
        town.relicsText = relicsText;
        town.epiloguePanel = epilogue.gameObject;
        town.epilogueTitle = epilogueTitle;
        town.epilogueBody = epilogueBody;
        town.newGameButton = newGame;
        town.titleButton = titleScreen;
        town.helpButton = help;
        town.howToPlay = howToPlay;
        town.quakeShake = quakeShake;
        town.leaderWindow = leaderWindow.gameObject;
        town.backdrop = backdrop;
        town.closeButton = close;
        town.portrait = portrait;
        town.leaderTitle = leaderTitle;
        town.dialogue = dialogue;
        town.buildingButtons = buildingButtons;
        town.giveButton = giveButton;
        MenuBuilder.DebugKeysUI(canvas, null, town, new Vector2(0f, 0f)); // F3 resources; hint bottom-left

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
