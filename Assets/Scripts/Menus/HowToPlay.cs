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
        "Be prepared",
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
        "Pick up XP gems to level up, then choose a card with the mouse or 1 / 2 / 3: a new weapon, a weapon level or " +
        "a small boost. Cards only last for the current dive. Esc pauses.\n\n" +
        "A weapon at Lv 5 can evolve (gold card, up to 3 per dive): the Sword from the start, the others once their " +
        "leader teaches you.",

        "Break crates for materials and supplies, and crystal clusters for crystals. Enemies drop materials and gold.\n\n" +
        "Stand next to an ore vein to destroy it: it weakens the next earthquake, but the cave trembles and it may set " +
        "off a trap (an ambush or falling rocks).\n\n" +
        "Every dive takes a day. If you die, everything found on that dive is lost.",

        "Each leader has a faction, a request, a daily order and a district. Click a leader to see them.\n\n" +
        "Finish a request (kill enemies, or bring materials) for gold. A leader's first finished request also teaches " +
        "you their weapon's evolution: the Guard's Spear, the Elder's Dagger, the Shaman's Crystal Orbit.\n\n" +
        "Spend materials, gold and crystals on district buildings: permanent upgrades. Under half its people, a " +
        "faction's district works at half strength; at 0, the faction is gone.",

        "Earthquakes hit on days 4, 7, 10 and 13. The quake meter rises every day; the top bar shows how strong the next " +
        "one will be. Destroy ore veins in the dungeon to weaken it, and build Ley Stones to slow it down.\n\n" +
        "From 40%, a quake hurts every faction: some people are killed, some injured. People never come back.\n\n" +
        "The injured can't work. Supplies treat them (one each, automatically); anyone still untreated 2 days after " +
        "the quake dies. A report shows what happened when you get back.",

        "Every faction does one order a day until you change it (in its leader's window):\n" +
        "  •  Reinforce homes (5 materials): reinforced homes spare people when a quake hits. Quakes crack them.\n" +
        "  •  Stockpile supplies: to treat the injured after a quake.\n" +
        "  •  Drill: drilled people are hurt instead of killed. It fades if they stop.\n" +
        "  •  Work: gold, crystals or materials.\n\n" +
        "You can't do everything at once: decide who prepares, and how, before each quake. Today's orders count for " +
        "tomorrow's quake.",
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
