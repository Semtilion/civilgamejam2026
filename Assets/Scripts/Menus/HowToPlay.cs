using UnityEngine;
using UnityEngine.UI;

// The How to play popup: one page (Back / Next only show if there are more) and Close. It opens once when a new game
// starts (Town), from the town's "?" button and from the dungeon's pause menu. The text is below, one entry per page.
public class HowToPlay : MonoBehaviour
{
    static readonly string[] Titles =
    {
        "How to play",
    };

    static readonly string[] Pages =
    {
        "Keep the town alive as long as you can.\n\n" +
        "\u2022  WASD to move. Weapons attack on their own. Esc pauses.\n" +
        "\u2022  Dungeon: reach the kill goal, then take the teleporter deeper or home. Dying loses the loot.\n" +
        "\u2022  Level up: pick a card (1 / 2 / 3). Lv 5 weapons can evolve.\n" +
        "\u2022  Mine ores to weaken quakes. Crates give materials and supplies.\n" +
        "\u2022  Leaders: finish their requests and turn them in at the yellow !.\n" +
        "\u2022  Give each faction a daily order: Work, Reinforce, Stockpile or Drill.\n" +
        "\u2022  Quakes come every 3 days: prepare the town before they hit.\n" +
        "\u2022  Supplies treat the hurt. Untreated, they die in 2 days.\n" +
        "\u2022  The run ends when every faction is gone.",
    };

    [Header("Scene references")]
    public GameObject panel;
    public Text titleText;
    public Text bodyText;
    public Text pageText;
    public Button backButton;
    public Button nextButton;
    public Button closeButton;

    int page;

    public bool IsOpen => panel.activeSelf;

    void Start()
    {
        backButton.onClick.AddListener(() => Turn(-1));
        nextButton.onClick.AddListener(() => Turn(1));
        closeButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        page = 0;
        Show();
        panel.SetActive(true);
    }

    public void Close()
    {
        panel.SetActive(false);
    }

    void Turn(int step)
    {
        page = Mathf.Clamp(page + step, 0, Pages.Length - 1);
        Show();
    }

    void Show()
    {
        titleText.text = Titles[page];
        bodyText.text = Pages[page];
        pageText.text = $"{page + 1} / {Pages.Length}";
        backButton.interactable = page > 0;
        nextButton.interactable = page < Pages.Length - 1;
        bool pages = Pages.Length > 1; // one page: no page turning
        backButton.gameObject.SetActive(pages);
        nextButton.gameObject.SetActive(pages);
        pageText.gameObject.SetActive(pages);
    }
}
