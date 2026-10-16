using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The town hub (Darkest Dungeon hamlet style): status bar, news, one button per NPC, a detail panel for the selected
// NPC (their help, their request, their home, Give / Repair buttons), Reinforce and Dive. State lives in Campaign.Current.
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
    public Button repairButton;
    public Button reinforceButton;
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
        repairButton.onClick.AddListener(() => { campaign.Repair(selected); Refresh(); });
        reinforceButton.onClick.AddListener(() => { campaign.Reinforce(); Refresh(); });
        diveButton.onClick.AddListener(() => SceneManager.LoadScene(arenaScene));

        Refresh();
    }

    void Refresh()
    {
        statusText.text = $"Day {campaign.day}      Materials {campaign.materials}      Population {campaign.population}      {NextThreat()}";

        int first = Mathf.Max(0, campaign.news.Count - NewsLines);
        newsText.text = string.Join("\n", campaign.news.GetRange(first, campaign.news.Count - first));

        for (int i = 0; i < NpcCount; i++) npcLabels[i].text = NpcSummary(i);
        ShowDetail(selected);

        SetLabel(reinforceButton, campaign.reinforced ? "Town reinforced" : $"Reinforce town\n{config.reinforceCost} materials");
        reinforceButton.interactable = campaign.CanReinforce;
    }

    string NextThreat()
    {
        int quakeDay = campaign.NextQuakeDay;
        if (quakeDay > 0)
            return $"Next quake: day {quakeDay}, {campaign.QuakeForecast:0}% if no more ores are mined (homes break at {campaign.BreakAt:0}%)";
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
        if (!npc.alive) return $"{definition.name}\n(dead)";

        string request = npc.quest == null ? "Request done (home braced)" : $"{npc.quest.Describe()}\n({QuestProgress(npc.quest)})";
        string home = npc.buildingBroken ? "\nHOME BROKEN" : "";
        return $"{definition.name}   tier {npc.tier}/{config.maxTier}\n{request}{home}";
    }

    void ShowDetail(int i)
    {
        NpcDefinition definition = config.npcs[i];
        NpcState npc = campaign.npcs[i];

        detailTitle.text = definition.name + (npc.alive ? "" : " (dead)");
        string body;
        if (!npc.alive)
        {
            body = "Killed by the disaster. Their help is gone for good.";
        }
        else
        {
            body = $"Help: {Player.StatText(definition.stat, definition.amountPerTier)} per tier. " +
                   $"Tier {npc.tier}/{config.maxTier} = {Player.StatText(definition.stat, definition.amountPerTier * npc.tier)}.";
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
            body += npc.buildingBroken
                ? "\nHOME BROKEN: no help until repaired. If the next quake hits first, they die."
                : npc.quest == null ? "\nHome braced: safe from the next quake." : "\nHome at risk: finish their request to brace it.";
        }
        detailBody.text = body;

        bool materialsRequest = npc.alive && npc.quest != null && !npc.quest.IsKill;
        giveButton.gameObject.SetActive(materialsRequest);
        if (materialsRequest)
        {
            SetLabel(giveButton, $"Give {npc.quest.needed} materials");
            giveButton.interactable = campaign.CanGive(i);
        }

        bool broken = npc.alive && npc.buildingBroken;
        repairButton.gameObject.SetActive(broken);
        if (broken)
        {
            SetLabel(repairButton, $"Repair home ({config.repairCost} materials)");
            repairButton.interactable = campaign.CanRepair(i);
        }
    }

    static void SetLabel(Button button, string text)
    {
        button.GetComponentInChildren<Text>().text = text;
    }
}
