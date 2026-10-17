using UnityEngine;
using UnityEngine.UI;

// The How to play popup: a few pages with Back / Next / Close. It opens once when a new game starts (Town), from the
// town's "?" button and from the dungeon's pause menu. The text is below, one entry per page.
public class HowToPlay : MonoBehaviour
{
    static readonly string[] Titles =
    {
        "The goal",
        "The dungeon",
        "Loot and dangers",
        "The town",
        "Earthquakes",
    };

    static readonly string[] Pages =
    {
        "Earthquakes are tearing the island apart, and a force deep in the dungeon below is feeding them.\n\n" +
        "Save the town in one of two ways:\n" +
        "  •  Dive down to depth 10 and destroy the Force.\n" +
        "  •  Or hold out until day 15 and beat the horde that attacks the town.\n\n" +
        "You lose if the horde breaks through, or if the quakes wipe out every faction.",

        "Move with WASD or the arrow keys. Your weapons attack on their own.\n\n" +
        "On every depth, kill the number of enemies shown at the top. Then a teleporter opens (follow the arrow): " +
        "step in to go deeper or to return to town.\n\n" +
        "Every 5th depth a boss appears. Bosses drop relics: rare, permanent finds.\n\n" +
        "Pick up XP gems to level up, then choose a card with the mouse or 1 / 2 / 3. Cards only last for the current " +
        "dive. Esc pauses.",

        "Break crates for materials and crystal clusters for crystals. Enemies drop materials and gold.\n\n" +
        "Stand next to an ore vein to destroy it: it weakens the next earthquake, but the cave trembles and it may set " +
        "off a trap (an ambush or falling rocks).\n\n" +
        "Stand next to a shrine for 5 seconds for XP, materials or crystals.\n\n" +
        "Every dive takes a day. If you die, everything found on that dive is lost.",

        "Each leader has a faction, a request and a district.\n\n" +
        "Finish a request (kill enemies, or bring materials) to earn gold and to brace that faction's homes against the " +
        "next earthquake. A leader's first finished request also gives you a new weapon, which can then show up on " +
        "level-up cards.\n\n" +
        "Spend materials, gold and crystals on district buildings: permanent upgrades for every dive.",

        "Earthquakes hit on days 4, 7, 10 and 13. The quake meter rises every day; the top bar shows how strong the next " +
        "one will be.\n\n" +
        "From 40%, a quake costs every faction that isn't braced some of its people and a building level. People never " +
        "come back: under half, a district works at half strength; at 0, the faction is gone.\n\n" +
        "Destroy ore veins to lower the meter, and build Ley Stones and Stone Foundations to keep it down.",
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
    }
}
