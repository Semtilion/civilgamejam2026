using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Town.unity (called from Jam > Build Game Scenes): the hamlet-style hub with a status bar,
// news, one button per faction leader, the relics box, the Dive button, the "?" button, the leader window (portrait,
// dialogue, daily order, district buildings, Give button), the report, the epilogue screen and the How to play popup.
public static class TownSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Town.unity";
    static readonly Color OrderMarkColor = new Color(1f, 0.80f, 0.30f); // gold: the faction's current order
    static readonly Color ReadyMarkColor = new Color(1f, 0.85f, 0.15f); // yellow "!": a request to turn in

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
        Text[] npcMarks = new Text[npcCount];
        for (int i = 0; i < npcCount; i++)
        {
            npcButtons[i] = NewButton($"NPC {i + 1}", npcRow, new Vector2(520, 210), 24, useTitleFont: false);
            npcLabels[i] = npcButtons[i].GetComponentInChildren<Text>();
            npcMarks[i] = ReadyMark(npcButtons[i].transform, new Vector2(225, 65), 72); // top-right corner
        }

        // Bottom right: the relics you own
        Image relicsPanel = NewPanel("Relics Panel", canvas, new Vector2(720, -210), new Vector2(400, 340));
        Text relicsText = NewText("Relics", relicsPanel.transform, Vector2.zero, new Vector2(360, 310), 22, TextAnchor.UpperLeft);
        relicsText.verticalOverflow = VerticalWrapMode.Overflow; // all 11 relics + the heading run a little past the box

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

        Image window = NewPanel("Window", leaderWindow, Vector2.zero, new Vector2(1620, 720));

        // Portrait frame: a soft violet glow behind a thick violet border. The bust hangs 30 below the frame's bottom edge
        // (clipped there, so it can hop up without showing it) and breathes; the nameplate covers its painted, uneven bottom.
        Vector2 framePosition = new Vector2(-490, 0), frameSize = new Vector2(540, 620);
        Image glow = NewImage("Portrait Glow", window.transform, framePosition, frameSize + Vector2.one * 62,
            new Color(BorderColor.r, BorderColor.g, BorderColor.b, 0.7f)); // 31 out on every side (the sprite's border)
        glow.sprite = a.glow;
        glow.type = Image.Type.Sliced;
        glow.raycastTarget = false;
        Image frame = NewImage("Portrait Frame", window.transform, framePosition, frameSize, new Color(0.04f, 0.03f, 0.06f));
        Outline frameBorder = frame.gameObject.AddComponent<Outline>();
        frameBorder.effectColor = BorderColor;
        frameBorder.effectDistance = new Vector2(6f, -6f);
        RectTransform portraitClip = NewUI("Portrait Clip", frame.transform, Vector2.zero, Vector2.zero);
        Stretch(portraitClip);
        portraitClip.gameObject.AddComponent<RectMask2D>();
        Image portrait = NewImage("Portrait", portraitClip, new Vector2(0, -30), new Vector2(540, 580), Color.white);
        Pin(portrait.rectTransform, new Vector2(0.5f, 0f)); // bottom center
        portrait.preserveAspect = true; // the busts are narrower than this box, so each one fills its height
        portrait.raycastTarget = false;
        PortraitMotion portraitMotion = portrait.gameObject.AddComponent<PortraitMotion>();
        // 90 tall: covers the uneven bottom (up to ~14% of a bust's height = 80 of 580, so 50 above the frame's edge at rest
        // and ~75 at the top of the hop)
        Image plate = NewImage("Nameplate", frame.transform, Vector2.zero, new Vector2(540, 90), new Color(0.07f, 0.05f, 0.10f));
        Pin(plate.rectTransform, new Vector2(0.5f, 0f));
        NewImage("Line", plate.transform, new Vector2(0, 45), new Vector2(540, 4), BorderColor);
        Text nameplate = NewText("Name", plate.transform, Vector2.zero, new Vector2(520, 80), 32, TextAnchor.MiddleCenter);
        Title(nameplate);
        // After a purchase: violet + gold diamonds burst out from the leader's head and shoulders, over the frame
        UIBurst purchaseBurst = NewUI("Purchase Burst", window.transform, framePosition + new Vector2(0, 60), new Vector2(420, 360))
            .gameObject.AddComponent<UIBurst>();
        purchaseBurst.sprite = a.circle;
        Text leaderMark = ReadyMark(window.transform, framePosition + new Vector2(235, 270), 96); // the frame's top-right

        // Right side, top to bottom: title, dialogue, the daily order (4 buttons, the current one with a gold bar along
        // its bottom), the district's buildings, Give.
        Text leaderTitle = NewText("Title", window.transform, new Vector2(280, 290), new Vector2(940, 60), 36, TextAnchor.MiddleLeft);
        Title(leaderTitle);
        Text dialogueText = NewText("Dialogue", window.transform, new Vector2(280, 135), new Vector2(940, 230), 22, TextAnchor.UpperLeft);
        dialogueText.raycastTarget = true; // click to show it all at once
        Typewriter dialogue = dialogueText.gameObject.AddComponent<Typewriter>();
        Text ordersCaption = Title(NewText("Orders Caption", window.transform, new Vector2(280, 5), new Vector2(940, 30), 20, TextAnchor.MiddleLeft));
        ordersCaption.text = "DAILY ORDER (every day until changed)";
        RectTransform orderRow = NewRow("Orders", window.transform, new Vector2(280, -50), new Vector2(940, 70), 12);
        int orderCount = System.Enum.GetValues(typeof(TownOrder)).Length;
        Button[] orderButtons = new Button[orderCount];
        Image[] orderMarks = new Image[orderCount];
        for (int i = 0; i < orderCount; i++)
        {
            orderButtons[i] = NewButton(((TownOrder)i).ToString(), orderRow, new Vector2(226, 70), 17, useTitleFont: false);
            orderMarks[i] = NewImage("Current", orderButtons[i].transform, new Vector2(0, -31), new Vector2(206, 5), OrderMarkColor);
            orderMarks[i].raycastTarget = false;
        }
        Text districtCaption = Title(NewText("District Caption", window.transform, new Vector2(280, -105), new Vector2(940, 30), 20, TextAnchor.MiddleLeft));
        districtCaption.text = "DISTRICT BUILDINGS (permanent)";
        RectTransform buildingRow = NewRow("District", window.transform, new Vector2(280, -175), new Vector2(940, 100), 12);
        Button[] buildingButtons = new Button[MostBuildingsInADistrict(a.townConfig)];
        for (int i = 0; i < buildingButtons.Length; i++) buildingButtons[i] = NewButton($"Building {i + 1}", buildingRow, new Vector2(305, 100), 17, useTitleFont: false);
        Button turnInButton = NewButton("Complete request", window.transform, new Vector2(380, 56), 22);
        Place(turnInButton, new Vector2(280, -295));
        Button close = NewButton("X", window.transform, new Vector2(56, 56), 30);
        Place(close, new Vector2(762, 312));
        AddPopIn(window);
        leaderWindow.gameObject.SetActive(false);

        // The report: what happened to the people while you were away (a quake, the injured). A dark backdrop blocks
        // the town until Continue (or Esc).
        Image report = NewImage("Report", canvas, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        Stretch(report.rectTransform);
        Image reportWindow = NewPanel("Window", report.transform, Vector2.zero, new Vector2(1400, 760));
        Text reportHeading = Title(NewText("Title", reportWindow.transform, new Vector2(0, 320), new Vector2(1300, 70), 48, TextAnchor.MiddleCenter));
        Text reportText = NewText("Body", reportWindow.transform, new Vector2(0, 15), new Vector2(1300, 510), 24, TextAnchor.UpperLeft);
        reportText.resizeTextForBestFit = true; // a bad quake can mean a long report
        reportText.resizeTextMinSize = 16;
        reportText.resizeTextMaxSize = 24;
        Button reportClose = NewButton("Continue", reportWindow.transform, new Vector2(320, 80), 32);
        Place(reportClose, new Vector2(0, -315));
        AddPopIn(reportWindow);
        report.gameObject.SetActive(false);

        // The result screen after a dive or raid (before the report): lines appear one by one, numbers roll up.
        Image resultsPanel = NewImage("Results", canvas, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        Stretch(resultsPanel.rectTransform);
        Image resultsWindow = NewPanel("Window", resultsPanel.transform, Vector2.zero, new Vector2(1400, 820));
        Text resultsHeading = Title(NewText("Title", resultsWindow.transform, new Vector2(0, 345), new Vector2(1300, 70), 48, TextAnchor.MiddleCenter));
        Text resultsBody = NewText("Body", resultsWindow.transform, new Vector2(0, 10), new Vector2(1300, 590), 24, TextAnchor.UpperLeft);
        resultsBody.resizeTextForBestFit = true; // a long run means many lines
        resultsBody.resizeTextMinSize = 14;
        resultsBody.resizeTextMaxSize = 24;
        Button resultsContinue = NewButton("Continue", resultsWindow.transform, new Vector2(320, 80), 32);
        Place(resultsContinue, new Vector2(0, -345));
        AddPopIn(resultsWindow);
        resultsPanel.gameObject.SetActive(false);
        ResultsScreen results = new GameObject("Results Screen").AddComponent<ResultsScreen>();
        results.panel = resultsPanel.gameObject;
        results.heading = resultsHeading;
        results.body = resultsBody;
        results.continueButton = resultsContinue;

        // Epilogue: covers everything once the game has ended (draws over the town and blocks its clicks)
        Image epilogue = NewImage("Epilogue", canvas, Vector2.zero, Vector2.zero, new Color(0.05f, 0.04f, 0.07f, 0.97f));
        Stretch(epilogue.rectTransform);
        Text epilogueTitle = NewText("Title", epilogue.transform, new Vector2(0, 330), new Vector2(1600, 100), 72, TextAnchor.MiddleCenter);
        Title(epilogueTitle);
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
        town.npcMarks = npcMarks;
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
        town.portraitMotion = portraitMotion;
        town.purchaseBurst = purchaseBurst;
        town.nameplate = nameplate;
        town.leaderTitle = leaderTitle;
        town.dialogue = dialogue;
        town.orderButtons = orderButtons;
        town.orderMarks = orderMarks;
        town.buildingButtons = buildingButtons;
        town.turnInButton = turnInButton;
        town.leaderMark = leaderMark;
        town.results = results;
        town.aliveOnly = new[] { ordersCaption.gameObject, districtCaption.gameObject };
        town.reportPanel = report.gameObject;
        town.reportHeading = reportHeading;
        town.reportText = reportText;
        town.reportClose = reportClose;
        MenuBuilder.DebugKeysUI(canvas, null, town, new Vector2(0f, 0f)); // F3 resources; hint bottom-left

        EditorSceneManager.SaveScene(scene);
    }

    // A yellow "!" (a request is finished: turn it in), hidden at first; TownManager shows it and makes it bob.
    static Text ReadyMark(Transform parent, Vector2 position, int size)
    {
        Text mark = InTitleFont(NewText("Ready Mark", parent, position, new Vector2(90, 120), size, TextAnchor.MiddleCenter));
        mark.text = "!";
        mark.color = ReadyMarkColor;
        mark.raycastTarget = false;
        Outline edge = mark.gameObject.AddComponent<Outline>();
        edge.effectColor = Color.black;
        edge.effectDistance = new Vector2(3f, -3f);
        mark.gameObject.SetActive(false);
        return mark;
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
