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
    // `dim`: a dark backdrop over the whole screen behind it (the town: so its buttons can't be clicked meanwhile).
    public static PauseMenu Pause(Transform canvas, ArenaManager arena, bool dim = false)
    {
        Transform parent = canvas;
        if (dim)
        {
            Image backdrop = NewImage("Pause Backdrop", canvas, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
            Stretch(backdrop.rectTransform);
            parent = backdrop.transform;
        }
        Image panel = NewPanel("Pause Panel", parent, Vector2.zero, new Vector2(600, 560));
        Text title = NewText("Title", panel.transform, new Vector2(0, 210), new Vector2(500, 80), 56, TextAnchor.MiddleCenter);
        title.text = "PAUSED";
        Title(title);
        Button resume = PauseButton("Resume", panel.transform, 100);
        Button howToPlay = PauseButton("How to play", panel.transform, 10);
        Button settings = PauseButton("Settings", panel.transform, -80);
        Button quit = PauseButton("Quit to title", panel.transform, -170);
        AddPopIn(panel);
        GameObject shown = dim ? parent.gameObject : panel.gameObject; // what opens and closes
        shown.SetActive(false);

        PauseMenu menu = new GameObject("Pause Menu").AddComponent<PauseMenu>();
        menu.arena = arena;
        menu.panel = shown;
        menu.resumeButton = resume;
        menu.howToPlayButton = howToPlay;
        menu.settingsButton = settings;
        menu.quitButton = quit;
        return menu;
    }

    // The scene's song (Music, Assets/Audio/Music/<file>). Every scene gets one; the first one stays for the whole game.
    public static void MusicPlayer(string file)
    {
        new GameObject("Music").AddComponent<Music>().song = GameBuilder.Song(file);
    }

    // The sound effects (Sfx): which files (Assets/Audio/Sounds, a folder = one at random) play for each Sound, how
    // loud, and for the cut ones how long (fading out over the last `fade` seconds; the rest play whole). Every scene
    // gets one; the first one stays for the whole game.
    public static void SoundPlayer()
    {
        Sfx sfx = new GameObject("Sound Effects").AddComponent<Sfx>();
        sfx.sounds = new Sfx.Entry[System.Enum.GetValues(typeof(Sound)).Length];
        void Add(Sound sound, string path, float volume, float length = 0f, float fade = 0f) => sfx.sounds[(int)sound] =
            new Sfx.Entry { name = sound.ToString(), clips = GameBuilder.SoundClips(path), volume = volume, length = length, fade = fade };

        Add(Sound.CrateBreak, "CrateBreak.mp3", 0.8f, 1f, 0.15f);     // wood crack, its first second: a crate breaks
        Add(Sound.GlassBreak, "GlassBreak.mp3", 0.7f);                // breaking glass: a crystal cluster breaks, an ore vein is mined
        Add(Sound.CrystalPickup, "CrystalPickup.aif", 0.5f);          // metal8: crystals picked up
        Add(Sound.MaterialsPickup, "MaterialsPickup.aif", 0.5f);      // wood12: materials and supplies picked up
        Add(Sound.GoldPickup, "Coins", 0.5f);                         // the coin sounds: gold picked up
        Add(Sound.Swing, "Swing.mp3", 0.45f);                         // item swing: sword, spear
        Add(Sound.HammerSmash, "HammerSmash.mp3", 0.7f);              // hammer smash: a Quake Hammer slam (with HammerQuake)
        Add(Sound.Whoosh, "Whooshes", 0.45f);                         // swing whoosh + swishes: dagger, axe, fire bomb, lightning
        Add(Sound.BombBreak, "BombBreak.ogg", 0.6f);                  // glass breaking 2: a fire bomb lands and breaks
        Add(Sound.Flame, "Flame.ogg", 0.5f);                          // catching fire: its fire patch lights
        Add(Sound.Zap, "Zap.mp3", 0.5f, 0.3f, 0.08f);                 // simple zaps, a short piece: Lightning Staff, Storm Orb

        // Dungeon: combat
        Add(Sound.HeroHurt, "HeroHurt.ogg", 0.7f);                    // punch 1: the hero is hit
        Add(Sound.HeroDeath, "SoundPack01/Downer01.aif", 0.8f);       // DEFEATED
        Add(Sound.Revive, "SoundPack01/Rise01.aif", 0.8f);            // Second Wind
        Add(Sound.SpitterShot, "Whooshes", 0.25f);                    // a Spitter's volley (quiet)
        Add(Sound.Explosion, "RockBreaking", 0.6f);                   // a Volatile enemy's blast, Volatile Core
        Add(Sound.BossArrive, "SoundPack01/Alarm.aif", 0.7f, 1.5f, 0.4f); // the alarm, a piece: a boss walks in
        Add(Sound.BossDash, "Whooshes/SwingWhoosh.mp3", 0.6f);        // a boss dashes
        Add(Sound.BossDeath, "Breaking", 0.8f);                       // a boss dies
        Add(Sound.Freeze, "GlassHits", 0.4f);                         // Frost Shard slows an enemy
        Add(Sound.Ricochet, "MetalHits", 0.35f);                      // a dagger bounces (Ricochet)

        // Dungeon: progress
        Add(Sound.XpPickup, "XpPickup.wav", 0.35f);                   // a tiny chime (made for the game): XP picked up
        Add(Sound.LevelUp, "SoundPack01/Upper01.aif", 0.7f);          // the level-up moment
        Add(Sound.CardPick, "WoodHits", 0.6f);                        // a level-up card is picked
        Add(Sound.RelicFound, "SoundPack01/Rise03.aif", 0.8f);        // a relic is found
        Add(Sound.Mining, "RockHit.ogg", 0.35f);                      // each chip while mining an ore or shrine
        Add(Sound.ShrineDone, "SoundPack01/Rise04.aif", 0.7f);        // a shrine is used up
        Add(Sound.TeleporterOpen, "SoundPack01/FX01.aif", 0.7f);      // the teleporter opens
        Add(Sound.TeleporterEnter, "SoundPack01/Rise05.aif", 0.7f);   // stepping into it

        // Menus, transitions, town
        Add(Sound.Click, "Click.wav", 0.5f);                          // a tiny tap (made for the game): every button, the pause menu (Esc)
        Add(Sound.Descent, "RockFalling", 0.6f);                      // sinking through the rock into the depths
        Add(Sound.Build, "WoodHits", 0.6f);                           // a building level is built
        Add(Sound.TurnIn, "SoundPack01/Coin01.aif", 0.6f);            // a request is turned in
        Add(Sound.LeaderSlide, "Whooshes", 0.35f);                    // the leader window slides in / out
        Add(Sound.Report, "SoundPack01/Downer01.aif", 0.6f);          // the quake report
        Add(Sound.Evolution, "SoundPack01/Upper01.aif", 0.7f);        // a leader taught an evolution
        Add(Sound.Results, "Coins", 0.5f);                            // a dive's results
        Add(Sound.GameOver, "SoundPack01/Downer01.aif", 0.8f);        // the town has fallen
        Add(Sound.Hit, "Hit.wav", 0.4f);                              // a thud tick (made for the game): an enemy is hit
        Add(Sound.Tremor, "RockBreak.mp3", 0.4f);                     // small rock break, lowered: a mined ore's tremor
        Add(Sound.RockLand, "RockBreak.mp3", 0.25f);                  // ... quieter: each rock of a rock rain lands
        Add(Sound.Quake, "Earthquake.mp3", 0.9f, 3f, 1.5f);           // the earthquake loop, a piece: the town's quake
        Add(Sound.HammerQuake, "Earthquake.mp3", 0.5f, 1.2f, 0.6f);   // ... a shorter piece: a Quake Hammer slam
    }

    // The scene transitions' overlay (SceneTransition): its own canvas on top of everything, hidden until a scene
    // change; the snapshot, the rock (Assets/Art/UI/Descent.png, Tools/bake_descent.py), black, the arrival flash and
    // the card. Every scene gets one; the first one stays for the whole game.
    public static SceneTransition TransitionUI(Sprite glow)
    {
        Transform canvas = NewCanvas("Scene Transition", 1000, eventSystem: false);
        SceneTransition transition = canvas.gameObject.AddComponent<SceneTransition>();
        transition.group = canvas.gameObject.AddComponent<CanvasGroup>();
        transition.group.alpha = 0f; // hidden (also in the editor)

        RectTransform snapshot = NewUI("Snapshot", canvas, Vector2.zero, Vector2.zero);
        Stretch(snapshot);
        transition.snapshot = snapshot.gameObject.AddComponent<RawImage>();
        Sprite rockArt = MenuArt("Descent");
        transition.rock = NewImage("Rock", canvas, Vector2.zero, Vector2.zero, rockArt != null ? Color.white : new Color(0.06f, 0.04f, 0.09f));
        transition.rock.sprite = rockArt;
        Stretch(transition.rock.rectTransform);
        transition.black = NewImage("Black", canvas, Vector2.zero, Vector2.zero, new Color(0.03f, 0.02f, 0.05f, 0f));
        Stretch(transition.black.rectTransform);
        transition.flash = NewImage("Arrival Flash", canvas, Vector2.zero, new Vector2(700, 700), new Color(0.6f, 0.95f, 1f, 0f));
        transition.flash.sprite = glow;

        Text title = NewText("Card Title", canvas, new Vector2(0, 30), new Vector2(1600, 150), 104, TextAnchor.MiddleCenter);
        title.font = CardFont;
        title.fontStyle = FontStyle.Bold; // (the font has no bold: Unity thickens it)
        title.color = TitleColor;
        Outline edge = title.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0.31f, 0.18f, 0.53f);
        edge.effectDistance = new Vector2(4f, -4f);
        transition.title = title;
        transition.subtitle = Shadowed(NewText("Card Subtitle", canvas, new Vector2(0, -60), new Vector2(1400, 50), 34, TextAnchor.MiddleCenter));
        transition.subtitle.color = new Color(0.82f, 0.78f, 0.9f);
        foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = graphic == transition.black;
        return transition;
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
