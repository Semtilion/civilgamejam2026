using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// The menu panels several scenes share (called by the scene builders): Settings (Title, Arena pause), How to play
// (Town, Arena pause) and the Arena's Esc pause menu. Each panel is built hidden; its component sits on its own object.
// Build them after the rest of the canvas, so they draw on top.
public static class MenuBuilder
{
    public static SettingsMenu Settings(Transform canvas)
    {
        Image panel = NewPanel("Settings Panel", canvas, Vector2.zero, new Vector2(900, 620));
        Text title = NewText("Title", panel.transform, new Vector2(0, 250), new Vector2(800, 80), 52, TextAnchor.MiddleCenter);
        title.text = "SETTINGS";
        Title(title);

        RectTransform resolutionRow = Row("Resolution", panel.transform, 140);
        Button previous = NewButton("<", resolutionRow, new Vector2(60, 50), 30);
        Place(previous, new Vector2(30, 0));
        Text resolution = NewText("Value", resolutionRow, new Vector2(200, 0), new Vector2(240, 50), 26, TextAnchor.MiddleCenter);
        Button next = NewButton(">", resolutionRow, new Vector2(60, 50), 30);
        Place(next, new Vector2(370, 0));

        RectTransform displayRow = Row("Display", panel.transform, 50);
        Button display = NewButton("Fullscreen", displayRow, new Vector2(400, 50), 26);
        Place(display, new Vector2(200, 0));

        Slider music = NewSlider("Slider", Row("Music", panel.transform, -40), new Vector2(200, 0), new Vector2(380, 24));
        Slider effects = NewSlider("Slider", Row("Effects", panel.transform, -130), new Vector2(200, 0), new Vector2(380, 24));

        Button close = NewButton("Close", panel.transform, new Vector2(260, 64), 30);
        Place(close, new Vector2(0, -240));
        AddPopIn(panel);
        panel.gameObject.SetActive(false);

        SettingsMenu menu = new GameObject("Settings").AddComponent<SettingsMenu>();
        menu.panel = panel.gameObject;
        menu.resolutionRow = resolutionRow.gameObject;
        menu.resolutionText = resolution;
        menu.previousResolution = previous;
        menu.nextResolution = next;
        menu.displayButton = display;
        menu.musicSlider = music;
        menu.effectsSlider = effects;
        menu.closeButton = close;
        return menu;
    }

    public static HowToPlay HowToPlayPopup(Transform canvas)
    {
        Image panel = NewPanel("How To Play Panel", canvas, Vector2.zero, new Vector2(1400, 820));
        Text title = NewText("Title", panel.transform, new Vector2(0, 340), new Vector2(1300, 80), 52, TextAnchor.MiddleCenter);
        Title(title);
        Text body = NewText("Body", panel.transform, new Vector2(0, 20), new Vector2(1260, 520), 28, TextAnchor.UpperLeft);
        Text page = NewText("Page", panel.transform, new Vector2(0, -290), new Vector2(300, 40), 24, TextAnchor.MiddleCenter);
        Button back = NewButton("Back", panel.transform, new Vector2(220, 64), 28);
        Place(back, new Vector2(-450, -350));
        Button close = NewButton("Close", panel.transform, new Vector2(220, 64), 28);
        Place(close, new Vector2(0, -350));
        Button next = NewButton("Next", panel.transform, new Vector2(220, 64), 28);
        Place(next, new Vector2(450, -350));
        AddPopIn(panel);
        panel.gameObject.SetActive(false);

        HowToPlay howToPlay = new GameObject("How To Play").AddComponent<HowToPlay>();
        howToPlay.panel = panel.gameObject;
        howToPlay.titleText = title;
        howToPlay.bodyText = body;
        howToPlay.pageText = page;
        howToPlay.backButton = back;
        howToPlay.nextButton = next;
        howToPlay.closeButton = close;
        return howToPlay;
    }

    // The Esc menu. Its How to play and Settings panels are built after it (on top) by the caller.
    public static PauseMenu Pause(Transform canvas, ArenaManager arena)
    {
        Image panel = NewPanel("Pause Panel", canvas, Vector2.zero, new Vector2(600, 560));
        Text title = NewText("Title", panel.transform, new Vector2(0, 210), new Vector2(500, 80), 56, TextAnchor.MiddleCenter);
        title.text = "PAUSED";
        Title(title);
        Button resume = PauseButton("Resume", panel.transform, 100);
        Button howToPlay = PauseButton("How to play", panel.transform, 10);
        Button settings = PauseButton("Settings", panel.transform, -80);
        Button quit = PauseButton("Quit to title", panel.transform, -170);
        AddPopIn(panel);
        panel.gameObject.SetActive(false);

        PauseMenu menu = new GameObject("Pause Menu").AddComponent<PauseMenu>();
        menu.arena = arena;
        menu.panel = panel.gameObject;
        menu.resumeButton = resume;
        menu.howToPlayButton = howToPlay;
        menu.settingsButton = settings;
        menu.quitButton = quit;
        return menu;
    }

    // Test keys (DebugKeys): a small hint in a corner, and in the dungeon (arena != null) the F1 relic list.
    // `hintCorner`: where the hint goes, e.g. (1,1) = top-right.
    public static DebugKeys DebugKeysUI(Transform canvas, ArenaManager arena, TownManager town, Vector2 hintCorner)
    {
        DebugKeys keys = new GameObject("Debug Keys").AddComponent<DebugKeys>();
        keys.arena = arena;
        keys.town = town;

        TextAnchor alignment = hintCorner.x > 0.5f ? TextAnchor.UpperRight : TextAnchor.LowerLeft;
        Text hint = NewText("Debug Hint", canvas, new Vector2(hintCorner.x > 0.5f ? -20 : 12, hintCorner.y > 0.5f ? -20 : 8),
            new Vector2(900, 50), 18, alignment);
        hint.color = new Color(1f, 1f, 1f, 0.45f);
        Pin(hint.rectTransform, hintCorner);
        keys.hintText = hint;
        if (arena == null) return keys;

        Image panel = NewPanel("Debug Relic Panel", canvas, Vector2.zero, new Vector2(760, 620));
        Text title = NewText("Title", panel.transform, new Vector2(0, 260), new Vector2(700, 60), 40, TextAnchor.MiddleCenter);
        title.text = "DEBUG: GET A RELIC";
        Title(title);
        keys.relicButtons = new Button[Relics.Names.Length];
        for (int i = 0; i < keys.relicButtons.Length; i++)
        {
            keys.relicButtons[i] = NewButton(Relics.Names[i], panel.transform, new Vector2(340, 50), 24);
            Place(keys.relicButtons[i], new Vector2(i % 2 == 0 ? -180 : 180, 190 - 60 * (i / 2))); // two columns
        }
        keys.relicCloseButton = NewButton("Close (F1)", panel.transform, new Vector2(260, 56), 26);
        Place(keys.relicCloseButton, new Vector2(0, -250));
        AddPopIn(panel);
        panel.gameObject.SetActive(false);
        keys.relicPanel = panel.gameObject;
        return keys;
    }

    static Button PauseButton(string label, Transform panel, float y)
    {
        Button button = NewButton(label, panel, new Vector2(380, 70), 30);
        Place(button, new Vector2(0, y));
        return button;
    }

    // A settings row: the label on the left, the controls go right of the middle (x 0 to 400).
    static RectTransform Row(string label, Transform panel, float y)
    {
        RectTransform row = NewUI(label + " Row", panel, new Vector2(0, y), new Vector2(800, 60));
        NewText("Label", row, new Vector2(-250, 0), new Vector2(300, 50), 30, TextAnchor.MiddleLeft).text = label;
        return row;
    }

    // A 0-1 slider (Unity's default slider, plain colored bars).
    static Slider NewSlider(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = name;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        go.transform.Find("Background").GetComponent<Image>().color = new Color(0.18f, 0.14f, 0.26f);
        go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = AccentColor;
        go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = Color.white;

        Slider slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }
}
