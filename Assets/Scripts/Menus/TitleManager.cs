using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen (the game starts here): Play, Settings, Quit.
// Play continues the game in memory if one is running (there are no save files), otherwise starts a new one.
public class TitleManager : MonoBehaviour
{
    [Tooltip("The game's name, shown big at the top. Empty for now.")]
    public string gameTitle = "";
    public TownConfig config;
    public string townScene = "Town";

    [Header("Scene references")]
    public Text titleText;
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;
    public SettingsMenu settings;

    bool GameRunning => Campaign.Current != null && !Campaign.Current.Ended;

    void Start()
    {
        Time.timeScale = 1f;
        titleText.text = gameTitle;
        playButton.GetComponentInChildren<Text>().text = GameRunning ? "Continue" : "Play";
        playButton.onClick.AddListener(Play);
        settingsButton.onClick.AddListener(settings.Open);
        quitButton.onClick.AddListener(Application.Quit); // does nothing in the editor
        quitButton.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer); // a browser tab can't quit
    }

    void Play()
    {
        if (!GameRunning) Campaign.StartNew(config);
        SceneManager.LoadScene(townScene);
    }
}
