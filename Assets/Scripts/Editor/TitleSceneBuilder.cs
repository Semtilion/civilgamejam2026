using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Title.unity (called from Jam > Build Game Scenes): the background (dark violet with falling
// crystals), the game's name (the baked title art, else text), Play / Settings / Quit, and the settings panel.
public static class TitleSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Title.unity";

    public static void Build(GameAssets a)
    {
        var scene = NewSceneFromTemplate(ScenePath);
        Camera camera = Object.FindAnyObjectByType<Camera>(); // the template has exactly one
        camera.backgroundColor = Color.black;

        Transform canvas = NewCanvas("Title UI");
        Sprite sky = MenuArt("TitleBackground");
        Image background = NewImage("Background", canvas, Vector2.zero, Vector2.zero, sky != null ? Color.white : BackgroundColor);
        background.sprite = sky;
        Stretch(background.rectTransform);

        // Crystals falling behind everything else (the artist's crystal, and the Spitter's shard).
        RectTransform falling = NewUI("Falling Crystals", canvas, Vector2.zero, Vector2.zero);
        Stretch(falling);
        FallingCrystals crystals = falling.gameObject.AddComponent<FallingCrystals>();
        crystals.crystal = CrystalSprite() ?? a.circle;
        crystals.shard = a.spitter.projectileSprite;
        crystals.glow = a.glow;

        // The game's name: the baked art (drawn at 2x, shown at half size), else the name as text.
        Sprite titleArt = MenuArt("Title");
        Text titleText = null;
        RectTransform title;
        if (titleArt != null)
        {
            Image image = NewImage("Game Title", canvas, new Vector2(0, 275), titleArt.rect.size / 2f, Color.white);
            image.sprite = titleArt;
            image.raycastTarget = false;
            title = image.rectTransform;
        }
        else
        {
            titleText = NewText("Game Title", canvas, new Vector2(0, 260), new Vector2(1700, 220), 120, TextAnchor.MiddleCenter);
            Title(titleText);
            title = titleText.rectTransform;
        }
        Text best = NewText("Best Run", canvas, new Vector2(0, 90), new Vector2(1400, 50), 28, TextAnchor.MiddleCenter);

        Button play = NewButton("Play", canvas, new Vector2(420, 90), 40);
        Place(play, new Vector2(0, -40));
        Button settings = NewButton("Settings", canvas, new Vector2(420, 90), 40);
        Place(settings, new Vector2(0, -150));
        Button quit = NewButton("Quit", canvas, new Vector2(420, 90), 40);
        Place(quit, new Vector2(0, -260));

        SettingsMenu settingsMenu = MenuBuilder.Settings(canvas);
        MenuBuilder.TransitionUI(a.glow); // scene changes (the first scene's stays for the whole game)
        MenuBuilder.MusicPlayer("CrystalCave.mp3"); // Crystal Cave (song18)
        MenuBuilder.SoundPlayer(); // (the first scene's stays for the whole game)

        TitleManager manager = new GameObject("Title Manager").AddComponent<TitleManager>();
        manager.config = a.townConfig;
        manager.title = title;
        manager.titleText = titleText;
        manager.bestText = best;
        manager.playButton = play;
        manager.settingsButton = settings;
        manager.quitButton = quit;
        manager.settings = settingsMenu;

        EditorSceneManager.SaveScene(scene);
    }
}
