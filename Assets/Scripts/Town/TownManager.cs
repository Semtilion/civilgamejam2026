using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The town hub (Darkest Dungeon hamlet style): status bar, news, one button per faction leader, a detail panel for the
// selected leader (their faction's people, their help, their request, Give button, and the Shaman's crystal shop), and
// Dive ("Defend the town" on the horde day). State lives in Campaign.Current. When the game has ended (won or lost),
// the epilogue covers the screen.
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
    [Tooltip("The crystal shop, shown in the shop owner's detail panel: one button per TownConfig shop item.")]
    public Button[] shopButtons;
    public Button diveButton;
    [Tooltip("Lists the relics you own.")]
    public Text relicsText;
    [Header("Epilogue (shown when the game has ended)")]
    public GameObject epiloguePanel;
    public Text epilogueTitle;
    public Text epilogueBody;
    public Button newGameButton;

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
        for (int i = 0; i < shopButtons.Length; i++)
        {
            int item = i; // copy for the lambda
            shopButtons[i].onClick.AddListener(() => { campaign.Buy(item); Refresh(); });
        }
        diveButton.onClick.AddListener(() => SceneManager.LoadScene(arenaScene)); // the arena runs the horde on day 15
        newGameButton.onClick.AddListener(() =>
        {
            Campaign.StartNew(config);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });

        Refresh();
    }

    void Refresh()
    {
        statusText.text = $"Day {campaign.day}     Materials {campaign.materials}     Crystals {campaign.crystals}     " +
                          $"Population {campaign.Population}/{campaign.StartPopulation}     {NextThreat()}";

        int first = Mathf.Max(0, campaign.news.Count - NewsLines);
        newsText.text = string.Join("\n", campaign.news.GetRange(first, campaign.news.Count - first));

        for (int i = 0; i < NpcCount; i++) npcLabels[i].text = NpcSummary(i);
        ShowDetail(selected);
        relicsText.text = RelicsSummary();

        SetLabel(diveButton, campaign.IsHordeDay ? "DEFEND\nTHE TOWN" : "DIVE");
        ShowEpilogue();
    }

    // Result, what became of each faction, and a few run stats.
    void ShowEpilogue()
    {
        epiloguePanel.SetActive(campaign.Ended);
        if (!campaign.Ended) return;

        epilogueTitle.text = campaign.Won ? "THE ISLAND IS SAVED" : "THE TOWN HAS FALLEN";
        string body = campaign.endingReason + "\n";
        for (int i = 0; i < config.npcs.Length; i++)
        {
            NpcDefinition definition = config.npcs[i];
            NpcState npc = campaign.npcs[i];
            if (!npc.Alive)
                body += $"\nThe {definition.faction} were wiped out.";
            else if (campaign.HelpMultiplier(i) < 1f)
                body += $"\nThe {definition.faction} barely survived: {npc.population} of {definition.startPopulation} remain.";
            else
                body += $"\nThe {definition.faction} endured: {npc.population} of {definition.startPopulation} remain.";
        }
        body += $"\n\nDay reached: {campaign.day}     Deepest depth: {campaign.deepestDepth}     " +
                $"Enemies killed: {campaign.totalKills}     Ores destroyed: {campaign.totalOres}";
        epilogueBody.text = body;
    }

    string NextThreat()
    {
        int quakeDay = campaign.NextQuakeDay;
        if (quakeDay > 0)
            return $"Next quake: day {quakeDay}, {campaign.QuakeForecast:0}% if no more ores are mined (costs people from {config.quakeHarmAt:0}%)";
        return campaign.day <= config.hordeDay ? $"Horde on day {config.hordeDay}" : "";
    }

    string RelicsSummary()
    {
        string list = "";
        int owned = 0;
        for (int i = 0; i < campaign.relicOwned.Length; i++)
        {
            if (!campaign.relicOwned[i]) continue;
            owned++;
            list += "\n" + Relics.Names[i];
        }
        if (owned == 0) list = "\nNone yet. Bosses in the dungeon guard them.";
        return $"RELICS {owned}/{campaign.relicOwned.Length}{list}";
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
            if (definition.hasShop) body += "\nThe crystal shop is closed, and its upgrades are lost.";
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
            if (definition.hasShop)
                body += $"\nCrystal shop: permanent upgrades, rank N costs N crystals + {config.materialsPerCrystal}N materials. " +
                        $"Lost if the {definition.faction} are wiped out.";
        }
        detailBody.text = body;

        // The crystal shop: only in the shop owner's panel, while their faction is alive.
        bool shop = definition.hasShop && campaign.ShopOpen;
        for (int s = 0; s < shopButtons.Length; s++)
        {
            bool show = shop && s < config.shopItems.Length;
            shopButtons[s].gameObject.SetActive(show);
            if (!show) continue;

            ShopItem item = config.shopItems[s];
            int rank = campaign.shopRanks[s];
            SetLabel(shopButtons[s], rank >= config.maxShopRank
                ? $"{item.name} MAX\n{Player.StatText(item.stat, item.amountPerRank * rank)} in total"
                : $"{Player.StatText(item.stat, item.amountPerRank)}   {rank}/{config.maxShopRank}\n" +
                  $"{campaign.ShopCrystalCost(s)} crystals + {campaign.ShopMaterialCost(s)} materials");
            shopButtons[s].interactable = campaign.CanBuy(s);
        }

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
