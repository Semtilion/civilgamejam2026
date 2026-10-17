using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The town hub (Darkest Dungeon hamlet style): status bar, news, one button per faction leader, a detail panel for the
// selected leader (their faction's people, their request, Give button, and their district's buildings), the relics you
// own, a "?" button (How to play, also shown once when a new game starts), and Dive ("Defend the town" on the horde
// day). State lives in Campaign.Current. When the game has ended (won or lost), the epilogue covers the screen
// (New game / Title screen).
public class TownManager : MonoBehaviour
{
    const int NewsLines = 6;

    public TownConfig config;
    public string arenaScene = "Arena";
    public string titleScene = "Title";

    [Header("Scene references")]
    public Text statusText;
    public Text newsText;
    public Button[] npcButtons;
    public Text[] npcLabels;
    public Text detailTitle;
    public Text detailBody;
    public Button giveButton;
    [Tooltip("The selected leader's district: one button per building in it.")]
    public Button[] buildingButtons;
    public Button diveButton;
    [Tooltip("Lists the relics you own.")]
    public Text relicsText;
    [Tooltip("The \"?\" button: opens How to play.")]
    public Button helpButton;
    public HowToPlay howToPlay;
    [Tooltip("Shakes and flashes the town once after an earthquake.")]
    public UIShake quakeShake;
    [Header("Epilogue (shown when the game has ended)")]
    public GameObject epiloguePanel;
    public Text epilogueTitle;
    public Text epilogueBody;
    public Button newGameButton;
    public Button titleButton;

    Campaign campaign;
    int selected;
    Ticker materialsShown, crystalsShown, goldShown; // the status bar's counters roll to the real amounts
    readonly List<int> shownBuildings = new List<int>(); // TownConfig.buildings index behind each building button

    // Buttons beyond the NPCs in the config are hidden (e.g. a scene built before an NPC was removed).
    int NpcCount => Mathf.Min(npcButtons.Length, config.npcs.Length);

    void Start()
    {
        Time.timeScale = 1f;
        campaign = Campaign.Current ?? Campaign.StartNew(config); // a new game when the game starts here
        materialsShown = new Ticker(campaign.shownMaterials); // from what you saw when you left
        crystalsShown = new Ticker(campaign.shownCrystals);
        goldShown = new Ticker(campaign.shownGold);

        for (int i = 0; i < npcButtons.Length; i++)
        {
            int index = i; // copy for the lambda
            npcButtons[i].onClick.AddListener(() => { selected = index; Refresh(); });
            npcButtons[i].gameObject.SetActive(i < NpcCount);
        }
        giveButton.onClick.AddListener(() => { campaign.GiveMaterials(selected); Refresh(); });
        for (int i = 0; i < buildingButtons.Length; i++)
        {
            int slot = i; // copy for the lambda
            buildingButtons[i].onClick.AddListener(() => { campaign.Build(shownBuildings[slot]); Refresh(); });
        }
        diveButton.onClick.AddListener(() => Leave(arenaScene)); // the arena runs the horde on day 15
        newGameButton.onClick.AddListener(() =>
        {
            Campaign.StartNew(config);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });
        titleButton.onClick.AddListener(() => Leave(titleScene));
        helpButton.onClick.AddListener(howToPlay.Open);

        Refresh();
        if (!campaign.howToPlayShown) // a new game: explain it once
        {
            campaign.howToPlayShown = true;
            howToPlay.Open();
        }
        if (campaign.quakeSinceTown) // the quake hit while you were away
        {
            campaign.quakeSinceTown = false;
            quakeShake.Play(0.9f, 24f);
        }
    }

    void Update()
    {
        statusText.text = StatusLine(Time.unscaledDeltaTime); // the counters roll
    }

    // Remembers the counters (so they roll from here next time), then loads the scene.
    void Leave(string scene)
    {
        campaign.shownMaterials = campaign.materials;
        campaign.shownCrystals = campaign.crystals;
        campaign.shownGold = campaign.gold;
        SceneManager.LoadScene(scene);
    }

    string StatusLine(float deltaTime)
    {
        return $"Day {campaign.day}    Materials {materialsShown.Next(campaign.materials, deltaTime)}    " +
               $"Crystals {crystalsShown.Next(campaign.crystals, deltaTime)}    Gold {goldShown.Next(campaign.gold, deltaTime)}    " +
               $"Population {campaign.Population}/{campaign.StartPopulation}    {NextThreat()}";
    }

    // Redraws everything from Campaign.Current (also called by the debug keys).
    public void Refresh()
    {
        statusText.text = StatusLine(0f);

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
            else if (campaign.DistrictStrength(i) < 1f)
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
            return $"Next quake: day {quakeDay}, {campaign.QuakeForecast:0}% if no more ores are mined (harmful from {config.quakeHarmAt:0}%)";
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

        string half = campaign.DistrictStrength(i) < 1f ? "  (half strength)" : "";
        string request = npc.quest == null ? "Request done (braced)" : $"{npc.quest.Describe()} ({QuestProgress(npc.quest)})";
        return $"{definition.name}\n{definition.faction} {npc.population}/{definition.startPopulation}{half}\n{request}";
    }

    void ShowDetail(int i)
    {
        NpcDefinition definition = config.npcs[i];
        NpcState npc = campaign.npcs[i];

        detailTitle.text = $"{definition.name}, leader of the {definition.faction}";
        string body;
        if (!npc.Alive)
        {
            body = $"The {definition.faction} were wiped out. {definition.name} is gone, and their district stands empty: " +
                   "its buildings no longer work.";
        }
        else
        {
            int halfLine = Mathf.CeilToInt(definition.startPopulation * config.halfHelpBelow);
            body = $"{definition.faction}: {npc.population}/{definition.startPopulation} people (lost ones never come back). " +
                   $"Under {halfLine}: district at half strength. At 0: wiped out.";
            body += npc.quest != null
                ? $"\nRequest: {npc.quest.Describe()} ({QuestProgress(npc.quest)}). Due before day {campaign.NextQuestDay}. Pays {config.questGold} gold."
                : $"\nRequest done. A new one comes on day {campaign.NextQuestDay}.";
            if (definition.unlocksWeapon)
            {
                string weapon = Weapon.Names[(int)definition.weapon];
                body += npc.everCompleted
                    ? $"\nYou have their {weapon}: it can show up on level-up cards in the dungeon."
                    : $"\nTheir first finished request also gives you the {weapon}.";
            }
            body += npc.quest == null
                ? $"\nHomes braced: the next quake can't hurt the {definition.faction} or their district."
                : $"\nAt risk: a harmful quake costs the {definition.faction} people and a building level. Finish the request to brace them.";
        }
        detailBody.text = body;

        ShowBuildings(i);

        bool materialsRequest = npc.Alive && npc.quest != null && !npc.quest.IsKill;
        giveButton.gameObject.SetActive(materialsRequest);
        if (materialsRequest)
        {
            SetLabel(giveButton, $"Give {npc.quest.needed} materials");
            giveButton.interactable = campaign.CanGive(i);
        }
    }

    // One button per building in this leader's district: name, level, effect per level and the next level's price.
    // Hidden once the faction is wiped out.
    void ShowBuildings(int npcIndex)
    {
        shownBuildings.Clear();
        if (campaign.npcs[npcIndex].Alive)
            for (int b = 0; b < config.buildings.Length; b++)
                if (config.buildings[b].district == npcIndex) shownBuildings.Add(b);

        for (int slot = 0; slot < buildingButtons.Length; slot++)
        {
            bool show = slot < shownBuildings.Count;
            buildingButtons[slot].gameObject.SetActive(show);
            if (!show) continue;

            int b = shownBuildings[slot];
            Building building = config.buildings[b];
            int level = campaign.buildingLevels[b];
            string label;
            if (level >= config.maxBuildingLevel)
            {
                label = $"{building.name}  MAX\n{building.EffectText(building.amountPerLevel * level)}";
            }
            else
            {
                string crystals = building.costsCrystals ? $" + {campaign.BuildingCrystalCost(b)} crystals" : "";
                label = $"{building.name}  {level}/{config.maxBuildingLevel}\nPer level: {building.EffectText(building.amountPerLevel)}\n" +
                        $"{campaign.BuildingMaterialCost(b)} materials + {campaign.BuildingGoldCost(b)} gold{crystals}";
            }
            SetLabel(buildingButtons[slot], label);
            buildingButtons[slot].interactable = campaign.CanBuild(b);
        }
    }

    static void SetLabel(Button button, string text)
    {
        button.GetComponentInChildren<Text>().text = text;
    }
}
