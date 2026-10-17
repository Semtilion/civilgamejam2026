using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Town.unity (called from Jam > Build Game Scenes): the hamlet-style hub with a status bar,
// one button per faction leader, the relics box, the Dive button, the "?" button, the leader window (portrait,
// dialogue, daily order, district buildings, Give button), the report, the epilogue screen and the How to play popup.
public static class TownSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Town.unity";
    static readonly Color OrderMarkColor = new Color(1f, 0.80f, 0.30f); // gold: the faction's current order
    static readonly Color ReadyMarkColor = new Color(1f, 0.85f, 0.15f); // yellow "!": a request to turn in
    const float NpcWidth = 520f, NpcStep = 560f; // the leader buttons' width, and from one's middle to the next (their banners too)

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
        // The night village (Assets/Art/UI/Town, baked by Tools/bake_town.py; else a flat color): covering the screen at
        // its own shape, a little bigger so a quake's shake doesn't show its edges, with the window lights and the cracks'
        // light over it (TownScenery moves them and adds twinkling stars).
        Sprite village = MenuArt("Town/TownBackground");
        Image background = NewImage("Town Backdrop", canvas, Vector2.zero, Vector2.zero, village != null ? Color.white : BackgroundColor);
        Stretch(background.rectTransform);
        TownScenery scenery = null;
        if (village != null)
        {
            background.sprite = village;
            AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = village.rect.width / village.rect.height;
            background.rectTransform.localScale = Vector3.one * 1.06f;
            scenery = background.gameObject.AddComponent<TownScenery>();
            scenery.windows = Overlay("Window Lights", background.transform, MenuArt("Town/TownWindows"));
            scenery.cracks = Overlay("Crack Light", background.transform, MenuArt("Town/TownCracks"));
            scenery.star = a.glow;
        }

        // A banner behind each leader's button (their color, their portrait faintly on it), hanging from the top; grey
        // once the faction is wiped out.
        int npcCount = a.townConfig.npcs.Length;
        Image[] banners = new Image[npcCount];
        Sprite[] goneBanners = new Sprite[npcCount];
        for (int i = 0; i < npcCount; i++)
        {
            string name = a.townConfig.npcs[i].name;
            Sprite banner = MenuArt($"Town/Banner_{name}");
            if (banner == null) continue;
            banners[i] = NewImage($"Banner {name}", canvas, new Vector2((i - (npcCount - 1) / 2f) * NpcStep, 0), banner.rect.size, Color.white);
            banners[i].sprite = banner;
            banners[i].raycastTarget = false;
            Pin(banners[i].rectTransform, new Vector2(0.5f, 1f)); // hangs (and sways) from its top
            goneBanners[i] = MenuArt($"Town/Banner_{name}_Gone");
        }
        if (scenery != null)
        {
            scenery.banners = banners.Where(b => b != null).Select(b => b.rectTransform).ToArray();
            Image beam = NewImage("Beam", canvas, Vector2.zero, new Vector2(0, 14), new Color(0.16f, 0.11f, 0.09f)); // they hang from it
            Pin(beam.rectTransform, new Vector2(0.5f, 1f));
            beam.rectTransform.anchorMin = new Vector2(0f, 1f); // across the top
            beam.rectTransform.anchorMax = new Vector2(1f, 1f);
        }

        // Top: status bar
        Text status = NewText("Status", canvas, new Vector2(0, -30), new Vector2(1860, 50), 24, TextAnchor.MiddleCenter);
        Pin(status.rectTransform, new Vector2(0.5f, 1f));

        // Middle: one button per faction leader
        RectTransform npcRow = NewRow("NPCs", canvas, new Vector2(0, 110), new Vector2(1760, 220), NpcStep - NpcWidth);
        Button[] npcButtons = new Button[npcCount];
        Text[] npcLabels = new Text[npcCount];
        Text[] npcMarks = new Text[npcCount];
        for (int i = 0; i < npcCount; i++)
        {
            npcButtons[i] = NewButton($"NPC {i + 1}", npcRow, new Vector2(NpcWidth, 210), 24, useTitleFont: false);
            npcLabels[i] = npcButtons[i].GetComponentInChildren<Text>();
            npcMarks[i] = ReadyMark(npcButtons[i].transform, new Vector2(225, 65), 72); // top-right corner
            npcButtons[i].GetComponent<ButtonLabelDim>().frameColor = a.townConfig.npcs[i].color; // framed in their color
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
        CanvasGroup backdropGroup = backdropImage.gameObject.AddComponent<CanvasGroup>(); // fades in and out (SlideIn)
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
        Image plateLine = NewImage("Line", plate.transform, new Vector2(0, 45), new Vector2(540, 4), BorderColor);
        Text nameplate = NewText("Name", plate.transform, Vector2.zero, new Vector2(520, 80), 32, TextAnchor.MiddleCenter);
        Title(nameplate);
        // The ornate frame over the portrait and nameplate (the leader's color, set by TownManager); a thick outline
        // without the frame art.
        OrnateFrame portraitBorder = AddFrame(frame.transform, PanelFrameThickness, BorderColor, onTop: true);
        if (portraitBorder == null)
        {
            Outline frameBorder = frame.gameObject.AddComponent<Outline>();
            frameBorder.effectColor = BorderColor;
            frameBorder.effectDistance = new Vector2(6f, -6f);
        }
        // After a purchase: violet + gold diamonds burst out from the leader's head and shoulders, over the frame
        UIBurst purchaseBurst = NewUI("Purchase Burst", window.transform, framePosition + new Vector2(0, 60), new Vector2(420, 360))
            .gameObject.AddComponent<UIBurst>();
        purchaseBurst.sprite = a.circle;
        Text leaderMark = ReadyMark(window.transform, framePosition + new Vector2(235, 270), 96); // the frame's top-right

        // Right side, top to bottom: title, their thanks (typed out, only after a purchase), three info boxes (people,
        // preparedness, request), the daily order (4 buttons, the current one with a gold bar along its bottom), the
        // district's buildings, Turn in.
        Text leaderTitle = NewText("Title", window.transform, new Vector2(280, 295), new Vector2(940, 60), 40, TextAnchor.MiddleLeft);
        Title(leaderTitle);
        Text dialogueText = NewText("Thanks", window.transform, new Vector2(280, 245), new Vector2(940, 40), 24, TextAnchor.MiddleLeft);
        dialogueText.raycastTarget = true; // click to show it all at once
        Typewriter dialogue = dialogueText.gameObject.AddComponent<Typewriter>();
        Text peopleText = InfoBox(window.transform, "PEOPLE", new Vector2(-65, 125), 250, out OrnateFrame peopleFrame);
        Text preparedText = InfoBox(window.transform, "PREPARED", new Vector2(210, 125), 260, out OrnateFrame preparedFrame);
        Text requestText = InfoBox(window.transform, "REQUEST", new Vector2(555, 125), 390, out OrnateFrame requestFrame);
        Text ordersCaption = Title(NewText("Orders Caption", window.transform, new Vector2(280, 5), new Vector2(940, 30), 20, TextAnchor.MiddleLeft));
        ordersCaption.text = "DAILY ORDER";
        RectTransform orderRow = NewRow("Orders", window.transform, new Vector2(280, -50), new Vector2(940, 70), 12);
        int orderCount = System.Enum.GetValues(typeof(TownOrder)).Length;
        Button[] orderButtons = new Button[orderCount];
        Image[] orderMarks = new Image[orderCount];
        for (int i = 0; i < orderCount; i++)
        {
            orderButtons[i] = NewButton(((TownOrder)i).ToString(), orderRow, new Vector2(226, 70), 18, useTitleFont: false); // 18: "+20% homes, 15 materials" just fits
            orderMarks[i] = NewImage("Current", orderButtons[i].transform, new Vector2(0, -31), new Vector2(206, 5), OrderMarkColor);
            orderMarks[i].raycastTarget = false;
        }
        Text districtCaption = Title(NewText("District Caption", window.transform, new Vector2(280, -105), new Vector2(940, 30), 20, TextAnchor.MiddleLeft));
        districtCaption.text = "BUILDINGS";
        RectTransform buildingRow = NewRow("District", window.transform, new Vector2(280, -175), new Vector2(940, 100), 12);
        Button[] buildingButtons = new Button[MostBuildingsInADistrict(a.townConfig)];
        for (int i = 0; i < buildingButtons.Length; i++) buildingButtons[i] = NewButton($"Building {i + 1}", buildingRow, new Vector2(305, 100), 19, useTitleFont: false);
        Button turnInButton = NewButton("Turn in", window.transform, new Vector2(380, 56), 26);
        Place(turnInButton, new Vector2(280, -295));
        Button close = NewButton("X", window.transform, new Vector2(56, 56), 30);
        Place(close, new Vector2(762, 312));
        SlideIn slide = leaderWindow.gameObject.AddComponent<SlideIn>(); // in from above, and back up
        slide.window = window.rectTransform;
        slide.windowGroup = window.gameObject.AddComponent<CanvasGroup>();
        slide.backdrop = backdropGroup;
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

        // The evolution popup (a leader's first finished request taught you their weapon's evolution): its name in the
        // leader's color, what it does, and leader-colored + white sparkles popping off the name; the frame takes the
        // leader's color too. Continue, Esc or a click anywhere closes it.
        Image evolution = NewImage("Evolution", canvas, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        Stretch(evolution.rectTransform);
        Button evolutionBackdrop = evolution.gameObject.AddComponent<Button>();
        evolutionBackdrop.transition = Selectable.Transition.None;
        Image evolutionWindow = NewPanel("Window", evolution.transform, Vector2.zero, new Vector2(1000, 460));
        Title(NewText("Title", evolutionWindow.transform, new Vector2(0, 160), new Vector2(900, 60), 40, TextAnchor.MiddleCenter)).text = "EVOLUTION UNLOCKED";
        Text evolutionName = Title(NewText("Name", evolutionWindow.transform, new Vector2(0, 75), new Vector2(900, 100), 72, TextAnchor.MiddleCenter));
        Text evolutionText = NewText("Effect", evolutionWindow.transform, new Vector2(0, -15), new Vector2(900, 50), 30, TextAnchor.MiddleCenter);
        Button evolutionClose = NewButton("Continue", evolutionWindow.transform, new Vector2(320, 80), 32);
        Place(evolutionClose, new Vector2(0, -120));
        UIBurst evolutionBurst = NewUI("Burst", evolutionWindow.transform, new Vector2(0, 75), new Vector2(700, 160)).gameObject.AddComponent<UIBurst>();
        evolutionBurst.sprite = a.circle;
        evolutionBurst.count = 48;
        evolutionBurst.speed = 750f;
        evolutionBurst.upward = 350f;
        evolutionBurst.lifetime = 1.1f;
        evolutionBurst.size = 18f;
        AddPopIn(evolutionWindow);
        evolution.gameObject.SetActive(false);

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
        town.leaderSlide = slide;
        town.banners = banners;
        town.goneBanners = goneBanners;
        town.backdrop = backdrop;
        town.closeButton = close;
        town.portrait = portrait;
        town.windowFrame = PanelFrame(window);
        town.portraitBorder = portraitBorder;
        town.portraitGlow = glow;
        town.nameplateLine = plateLine;
        town.portraitMotion = portraitMotion;
        town.purchaseBurst = purchaseBurst;
        town.nameplate = nameplate;
        town.leaderTitle = leaderTitle;
        town.dialogue = dialogue;
        town.peopleText = peopleText;
        town.preparedText = preparedText;
        town.requestText = requestText;
        town.infoFrames = new[] { peopleFrame, preparedFrame, requestFrame };
        town.orderButtons = orderButtons;
        town.orderMarks = orderMarks;
        town.buildingButtons = buildingButtons;
        town.turnInButton = turnInButton;
        town.leaderMark = leaderMark;
        town.results = results;
        town.aliveOnly = new[]
        {
            ordersCaption.gameObject, districtCaption.gameObject,
            peopleText.transform.parent.gameObject, preparedText.transform.parent.gameObject, requestText.transform.parent.gameObject,
        };
        town.reportPanel = report.gameObject;
        town.reportHeading = reportHeading;
        town.reportText = reportText;
        town.reportClose = reportClose;
        town.evolutionPanel = evolution.gameObject;
        town.evolutionFrame = PanelFrame(evolutionWindow);
        town.evolutionName = evolutionName;
        town.evolutionText = evolutionText;
        town.evolutionClose = evolutionClose;
        town.evolutionBackdrop = evolutionBackdrop;
        town.evolutionBurst = evolutionBurst;
        town.evolutionTexts = a.evolutionTexts;
        MenuBuilder.DebugKeysUI(canvas, null, town, new Vector2(0f, 0f)); // F3 resources; hint bottom-left

        EditorSceneManager.SaveScene(scene);
    }

    // One of the leader window's info boxes (190 tall): a dark panel in a thin frame (the leader's color, set by
    // TownManager), its title on top and up to 3 lines under it. Returns the body text.
    static Text InfoBox(Transform window, string title, Vector2 position, float width, out OrnateFrame frame)
    {
        Image box = NewImage(title + " Box", window, position, new Vector2(width, 190), PanelColor);
        frame = AddFrame(box.transform, ButtonFrameThickness, FrameColor); // thin: the box is small
        Text header = Title(NewText("Header", box.transform, new Vector2(0, 58), new Vector2(width - 40, 34), 22, TextAnchor.MiddleCenter));
        header.text = title;
        return NewText("Body", box.transform, new Vector2(0, -22), new Vector2(width - 40, 110), 24, TextAnchor.UpperCenter);
    }

    // A yellow "!" (a request is finished: turn it in), hidden at first; TownManager shows it and makes it bob.
    // An image over the whole of `parent` (a layer of the backdrop), or null without the art.
    static Image Overlay(string name, Transform parent, Sprite sprite)
    {
        if (sprite == null) return null;
        Image image = NewImage(name, parent, Vector2.zero, Vector2.zero, Color.white);
        image.sprite = sprite;
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        return image;
    }

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
