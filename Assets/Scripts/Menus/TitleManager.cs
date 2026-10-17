using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen (the game starts here): Play, Settings, Quit, and the best run so far.
// Play continues the game in memory if one is running (there are no save files), otherwise starts a new one.
public class TitleManager : MonoBehaviour
{
    [Tooltip("The game's name, shown big at the top. Empty for now.")]
    public string gameTitle = "";
    public TownConfig config;
    public string townScene = "Town";

    [Header("Scene references")]
    public Text titleText;
    [Tooltip("The best run (Campaign.BestDay); empty before the first one ends.")]
    public Text bestText;
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;
    public SettingsMenu settings;

    bool GameRunning => Campaign.Current != null && !Campaign.Current.Ended;

    void Start()
    {
        Time.timeScale = 1f;
        titleText.text = gameTitle;
        bestText.text = Campaign.BestDay > 0 ? $"Best: day {Campaign.BestDay} · {Campaign.BestRaids} raids held" : "";
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
