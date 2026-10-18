using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen (the game starts here): the game's name (floating gently), Play, Settings, Quit, and the best run so
// far, over falling crystals (FallingCrystals).
// Play continues the game in memory if one is running (there are no save files), otherwise starts a new one.
public class TitleManager : MonoBehaviour
{
    const float FloatHeight = 6f; // UI units: the title drifts this far up and down...
    const float FloatTime = 4f;   // ...once every this many seconds

    [Tooltip("The game's name: shown as text only when there's no title art (Assets/Art/UI/Title.png).")]
    public string gameTitle = "Depths of the CrystalFall";
    public TownConfig config;
    public string townScene = "Town";

    [Header("Scene references")]
    [Tooltip("The title (its art, or the text): floats up and down slowly.")]
    public RectTransform title;
    [Tooltip("Only without the title art: the text that shows Game Title.")]
    public Text titleText;
    [Tooltip("The best run (Campaign.BestDay); empty before the first one ends.")]
    public Text bestText;
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;
    public SettingsMenu settings;

    Vector2 titleRest;

    bool GameRunning => Campaign.Current != null && !Campaign.Current.Ended;

    void Start()
    {
        Time.timeScale = 1f;
        titleRest = title.anchoredPosition;
        if (titleText != null) titleText.text = gameTitle;
        bestText.text = Campaign.BestDay > 0 ? $"Best: day {Campaign.BestDay}" : "";
        playButton.GetComponentInChildren<Text>().text = GameRunning ? "Continue" : "Play";
        playButton.onClick.AddListener(Play);
        settingsButton.onClick.AddListener(settings.Open);
        quitButton.onClick.AddListener(Application.Quit); // does nothing in the editor
        quitButton.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer); // a browser tab can't quit
    }

    void Update()
    {
        title.anchoredPosition = titleRest + Vector2.up * FloatHeight * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / FloatTime);
    }

    void Play()
    {
        if (!GameRunning) Campaign.StartNew(config);
        Campaign campaign = Campaign.Current;
        SceneTransition.Load(townScene, SceneTransition.Style.Fade, $"Day {campaign.day}", $"Quake on day {campaign.NextQuakeDay}");
    }
}
