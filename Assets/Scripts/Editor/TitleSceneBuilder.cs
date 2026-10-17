using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static GameBuilder;

// Builds Assets/Scenes/Title.unity (called from Jam > Build Game Scenes): the game's name (empty for now, set it on the
// Title Manager), Play / Settings / Quit, and the settings panel.
public static class TitleSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Title.unity";

    public static void Build(GameAssets a)
    {
        var scene = NewSceneFromTemplate(ScenePath);
        Camera camera = Object.FindAnyObjectByType<Camera>(); // the template has exactly one
        camera.backgroundColor = Color.black;

        Transform canvas = NewCanvas("Title UI");
        Image background = NewImage("Background (placeholder)", canvas, Vector2.zero, Vector2.zero, BackgroundColor);
        Stretch(background.rectTransform);
        Text title = NewText("Game Title", canvas, new Vector2(0, 260), new Vector2(1700, 220), 120, TextAnchor.MiddleCenter);
        Title(title);
        Text best = NewText("Best Run", canvas, new Vector2(0, 90), new Vector2(1400, 50), 28, TextAnchor.MiddleCenter);

        Button play = NewButton("Play", canvas, new Vector2(420, 90), 40);
        Place(play, new Vector2(0, -40));
        Button settings = NewButton("Settings", canvas, new Vector2(420, 90), 40);
        Place(settings, new Vector2(0, -150));
        Button quit = NewButton("Quit", canvas, new Vector2(420, 90), 40);
        Place(quit, new Vector2(0, -260));

        SettingsMenu settingsMenu = MenuBuilder.Settings(canvas);

        TitleManager manager = new GameObject("Title Manager").AddComponent<TitleManager>();
        manager.config = a.townConfig;
        manager.titleText = title;
        manager.bestText = best;
        manager.playButton = play;
        manager.settingsButton = settings;
        manager.quitButton = quit;
        manager.settings = settingsMenu;

        EditorSceneManager.SaveScene(scene);
    }
}
