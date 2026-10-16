using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The town hub (Darkest Dungeon hamlet style): status bar, news, one button per faction leader, a detail panel for the
// selected leader (their faction's people, their help, their request, Give button), and Dive. State lives in
// Campaign.Current. When every faction is wiped out, Dive becomes "New game".
public class TownManager : MonoBehaviour
{
    const int NewsLines = 6;

    public TownConfig config;
    public string arenaScene = "Arena";

    [Header("Scene references")]
    public Text statusText;
    public Text newsText;
    public Button[] npcButtons;
    public Text[] npcLabels;
    public Text detailTitle;
    public Text detailBody;
    public Button giveButton;
    public Button diveButton;

    Campaign campaign;
    int selected;

    // Buttons beyond the NPCs in the config are hidden (e.g. a scene built before an NPC was removed).
    int NpcCount => Mathf.Min(npcButtons.Length, config.npcs.Length);

    void Start()
    {
        Time.timeScale = 1f;
        campaign = Campaign.Current ?? Campaign.StartNew(config); // a new game when the game starts here

        for (int i = 0; i < npcButtons.Length; i++)
        {
            int index = i; // copy for the lambda
            npcButtons[i].onClick.AddListener(() => { selected = index; Refresh(); });
            npcButtons[i].gameObject.SetActive(i < NpcCount);
        }
        giveButton.onClick.AddListener(() => { campaign.GiveMaterials(selected); Refresh(); });
        diveButton.onClick.AddListener(OnDive);

        Refresh();
    }

    void OnDive()
    {
        if (campaign.GameOver)
        {
            Campaign.StartNew(config);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            SceneManager.LoadScene(arenaScene);
        }
    }

    void Refresh()
    {
        statusText.text = $"Day {campaign.day}      Materials {campaign.materials}      " +
                          $"Population {campaign.Population}/{campaign.StartPopulation}      {NextThreat()}";

        int first = Mathf.Max(0, campaign.news.Count - NewsLines);
        newsText.text = string.Join("\n", campaign.news.GetRange(first, campaign.news.Count - first));

        for (int i = 0; i < NpcCount; i++) npcLabels[i].text = NpcSummary(i);
        ShowDetail(selected);

        SetLabel(diveButton, campaign.GameOver ? "Town fallen\nNew game" : "DIVE");
    }

    string NextThreat()
    {
        int quakeDay = campaign.NextQuakeDay;
        if (quakeDay > 0)
            return $"Next quake: day {quakeDay}, {campaign.QuakeForecast:0}% if no more ores are mined (costs people from {config.quakeHarmAt:0}%)";
        return campaign.day <= config.hordeDay ? $"Horde on day {config.hordeDay}" : "";
    }

    string QuestProgress(Quest quest)
    {
        return quest.IsKill ? $"{quest.progress}/{quest.needed}" : $"you have {campaign.materials}/{quest.needed}";
    }

    string NpcSummary(int i)
    {
        NpcDefinition definition = config.npcs[i];
        NpcState npc = campaign.npcs[i];
        if (!npc.Alive) return $"{definition.name}\n{definition.faction}: WIPED OUT";

        string halved = campaign.HelpMultiplier(i) < 1f ? "  (help halved)" : "";
        string request = npc.quest == null ? "Request done (braced)" : $"{npc.quest.Describe()} ({QuestProgress(npc.quest)})";
        return $"{definition.name}   tier {npc.tier}/{config.maxTier}\n{definition.faction} {npc.population}/{definition.startPopulation}{halved}\n{request}";
    }

    void ShowDetail(int i)
    {
        NpcDefinition definition = config.npcs[i];
        NpcState npc = campaign.npcs[i];

        detailTitle.text = $"{definition.name}, leader of the {definition.faction}";
        string body;
        if (!npc.Alive)
        {
            body = $"The {definition.faction} were wiped out. {definition.name} is gone, and so is their help.";
        }
        else
        {
            int halfLine = Mathf.CeilToInt(definition.startPopulation * config.halfHelpBelow);
            float help = campaign.HelpMultiplier(i);
            body = $"{definition.faction}: {npc.population}/{definition.startPopulation} people. Lost people never come back. " +
                   $"Under {halfLine}, their help is halved; at 0 the faction is wiped out.";
            body += $"\nHelp: {Player.StatText(definition.stat, definition.amountPerTier)} per tier. " +
                    $"Tier {npc.tier}/{config.maxTier} = {Player.StatText(definition.stat, definition.amountPerTier * npc.tier * help)}" +
                    (help < 1f ? " (halved)." : ".");
            body += npc.quest != null
                ? $"\nRequest: {npc.quest.Describe()} ({QuestProgress(npc.quest)}). Due before day {campaign.NextQuestDay}."
                : $"\nRequest done. A new one comes on day {campaign.NextQuestDay}.";
            if (definition.unlocksWeapon)
            {
                string weapon = Weapon.Names[(int)definition.weapon];
                body += npc.everCompleted
                    ? $"\nYou have their {weapon}: it can show up on level-up cards in the dungeon."
                    : $"\nTheir first finished request gives you the {weapon}.";
            }
            body += npc.quest == null
                ? $"\nHomes braced: the next quake can't hurt the {definition.faction}."
                : $"\nHomes at risk: the next quake will cost the {definition.faction} people. Finish the request to brace them.";
        }
        detailBody.text = body;

        bool materialsRequest = npc.Alive && npc.quest != null && !npc.quest.IsKill;
        giveButton.gameObject.SetActive(materialsRequest);
        if (materialsRequest)
        {
            SetLabel(giveButton, $"Give {npc.quest.needed} materials");
            giveButton.interactable = campaign.CanGive(i);
        }
    }

    static void SetLabel(Button button, string text)
    {
        button.GetComponentInChildren<Text>().text = text;
    }
}
