using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The town hub (Darkest Dungeon hamlet style): status bar, one button per faction leader, the relics you own,
// a "?" button (How to play, also shown once when a new game starts), and Dive ("Defend the town" on a raid day).
// Clicking a leader opens their window: portrait on the left; on the right their situation in three boxes (people,
// preparedness, request; their thanks typed out above them after a purchase or turn-in), their daily order, their
// district's buildings and the turn-in button below (closes with X, Esc or a click outside).
// A yellow "!" marks a leader whose request is finished (turn it in). Coming back from a dive or raid shows the result
// screen, then the report (after a quake, or while the injured wait for care), then a popup for each evolution a leader
// taught you (also right after you turn in their first request). State lives in Campaign.Current. When the run has
// ended (every faction wiped out), the end screen covers the town (New game / Title screen).
public class TownManager : MonoBehaviour
{
    static readonly Color GoneTint = new Color(0.55f, 0.55f, 0.55f); // a wiped-out leader's portrait: grey and dark

    public TownConfig config;
    public string arenaScene = "Arena";
    public string titleScene = "Title";

    [Header("Scene references")]
    public Text statusText;
    public Button[] npcButtons;
    public Text[] npcLabels;
    [Tooltip("The yellow \"!\" on each leader button: their request is finished, turn it in.")]
    public Text[] npcMarks;
    public Button diveButton;
    [Tooltip("Lists the relics you own.")]
    public Text relicsText;
    [Tooltip("The \"?\" button: opens How to play.")]
    public Button helpButton;
    public HowToPlay howToPlay;
    [Tooltip("Shakes and flashes the town once after an earthquake.")]
    public UIShake quakeShake;
    [Header("Leader window (opens when a leader is clicked)")]
    [Tooltip("Holds the backdrop and the window (siblings, so clicks in the window don't reach the backdrop).")]
    public GameObject leaderWindow;
    [Tooltip("The dark see-through backdrop over the town: clicking it (outside the window) closes the window.")]
    public Button backdrop;
    public Button closeButton;
    public Image portrait;
    [Tooltip("Breathes, and hops after a purchase.")]
    public PortraitMotion portraitMotion;
    [Tooltip("Violet + gold pieces burst around the portrait after a purchase.")]
    public UIBurst purchaseBurst;
    [Tooltip("The name on the bar across the bottom of the portrait.")]
    public Text nameplate;
    [Tooltip("Take the open leader's color (NpcDefinition.color; grey once wiped out). The frames may be empty.")]
    public OrnateFrame windowFrame;
    public OrnateFrame portraitBorder;
    public Image portraitGlow;
    public Image nameplateLine;
    public Text leaderTitle;
    [Tooltip("Their thanks (and what a turn-in paid), typed out above the info boxes; empty until you buy or turn in.")]
    public Typewriter dialogue;
    [Tooltip("The three info boxes' texts: people, preparedness, request.")]
    public Text peopleText, preparedText, requestText;
    [Tooltip("The three info boxes' frames (take the leader's color).")]
    public OrnateFrame[] infoFrames;
    [Tooltip("One button per order, in TownOrder order (Work, Reinforce, Stockpile, Drill).")]
    public Button[] orderButtons;
    [Tooltip("The gold bar on each order button (same order): shown on the faction's current order.")]
    public Image[] orderMarks;
    [Tooltip("One button per building in the leader's district.")]
    public Button[] buildingButtons;
    [Tooltip("Complete request / Give materials: turns in the leader's finished request.")]
    public Button turnInButton;
    [Tooltip("The yellow \"!\" by the portrait while the open leader's request is finished.")]
    public Text leaderMark;
    [Tooltip("Hidden when the faction is wiped out (the section captions).")]
    public GameObject[] aliveOnly;
    [Tooltip("Shown first when you come back from a dive or raid.")]
    public ResultsScreen results;
    [Header("Report (what happened to the people while you were away)")]
    public GameObject reportPanel;
    public Text reportHeading;
    public Text reportText;
    public Button reportClose;
    [Header("Evolution popup (a leader taught you their weapon's evolution)")]
    public GameObject evolutionPanel;
    [Tooltip("Takes the leader's color.")]
    public OrnateFrame evolutionFrame;
    [Tooltip("The evolution's name (in the leader's color).")]
    public Text evolutionName;
    [Tooltip("\"Spear: pins enemies for 1 s\".")]
    public Text evolutionText;
    public Button evolutionClose;
    [Tooltip("The dark backdrop: a click anywhere closes the popup too.")]
    public Button evolutionBackdrop;
    [Tooltip("Pops off the name in the leader's color + white when the popup opens.")]
    public UIBurst evolutionBurst;
    [Tooltip("What each evolution does, in WeaponId order (copied from the arena weapons by the builder).")]
    public string[] evolutionTexts;
    [Header("Epilogue (shown when the game has ended)")]
    public GameObject epiloguePanel;
    public Text epilogueTitle;
    public Text epilogueBody;
    public Button newGameButton;
    public Button titleButton;

    Campaign campaign;
    int selected;       // whose window is open
    string spokenLine;  // what they say above the info: their thanks after a purchase or turn-in (null: nothing)
    string reward;      // after it on a turn-in: what the request paid (null otherwise)
    Ticker materialsShown, crystalsShown, goldShown; // the status bar's counters roll to the real amounts
    readonly List<int> shownBuildings = new List<int>(); // TownConfig.buildings index behind each building button
    readonly Dictionary<Sprite, Sprite> greyPortraits = new Dictionary<Sprite, Sprite>(); // made once each

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
            npcButtons[i].onClick.AddListener(() => OpenLeader(index));
            npcButtons[i].gameObject.SetActive(i < NpcCount);
        }
        backdrop.onClick.AddListener(CloseLeader);
        closeButton.onClick.AddListener(CloseLeader);
        turnInButton.onClick.AddListener(TurnIn);
        for (int i = 0; i < orderButtons.Length; i++)
        {
            TownOrder order = (TownOrder)i; // copy for the lambda
            orderButtons[i].onClick.AddListener(() =>
            {
                campaign.npcs[selected].order = order;
                Refresh();
            });
        }
        reportClose.onClick.AddListener(CloseReport);
        evolutionClose.onClick.AddListener(CloseEvolution);
        evolutionBackdrop.onClick.AddListener(CloseEvolution);
        for (int i = 0; i < buildingButtons.Length; i++)
        {
            int slot = i; // copy for the lambda
            buildingButtons[i].onClick.AddListener(() => { if (campaign.Build(shownBuildings[slot])) Thank(); });
        }
        diveButton.onClick.AddListener(() => Leave(arenaScene)); // the arena runs the raid on a raid day
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
        // The run's result first (unless the run is over: the end screen covers everything), then the report.
        RunResult run = campaign.lastResult;
        campaign.lastResult = null;
        if (run != null && !campaign.Ended) results.Show(ResultTitle(run), ResultLines(run), ShowReport);
        else ShowReport();
    }

    // What happened to the people while you were away (the epilogue covers it if the game has ended), shown once;
    // then (or right away if there's nothing to report) the evolutions leaders taught you.
    void ShowReport()
    {
        bool show = campaign.report.Count > 0 && !campaign.Ended;
        if (show)
        {
            reportHeading.text = campaign.reportTitle ?? $"DAY {campaign.day}";
            reportText.text = string.Join("\n", campaign.report);
            reportPanel.SetActive(true);
        }
        campaign.report.Clear();
        campaign.reportTitle = null;
        if (!show) ShowEvolution();
    }

    void CloseReport()
    {
        reportPanel.SetActive(false);
        ShowEvolution();
    }

    // The next evolution a leader just taught you (one popup each): its name, what it does, the frame and a burst of
    // sparkles in the leader's color. Nothing once the game has ended (the end screen covers the town).
    void ShowEvolution()
    {
        if (campaign.Ended) campaign.newEvolutions.Clear();
        if (campaign.newEvolutions.Count == 0) return;
        int i = campaign.newEvolutions[0];
        campaign.newEvolutions.RemoveAt(0);

        int weapon = (int)config.npcs[i].weapon;
        Color color = config.npcs[i].color;
        evolutionName.text = Weapon.EvolutionNames[weapon];
        evolutionName.color = color;
        evolutionText.text = $"{Weapon.Names[weapon]}: {evolutionTexts[weapon]}";
        if (evolutionFrame != null) evolutionFrame.Color = color;
        evolutionBurst.colors = new[] { color, Color.white };
        evolutionPanel.SetActive(true);
        evolutionBurst.Burst();
    }

    // Continue, Esc or a click: closed, and the next one shows if two leaders taught you at once.
    void CloseEvolution()
    {
        evolutionPanel.SetActive(false);
        ShowEvolution();
    }

    void Update()
    {
        statusText.text = StatusLine(Time.unscaledDeltaTime); // the counters roll

        // The "!" marks bob
        Vector3 bob = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f));
        foreach (Text mark in npcMarks) mark.transform.localScale = bob;
        leaderMark.transform.localScale = bob;

        Keyboard keyboard = Keyboard.current;
        if (howToPlay.IsOpen || keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        if (results.IsOpen) results.Continue();
        else if (reportPanel.activeSelf) CloseReport();
        else if (evolutionPanel.activeSelf) CloseEvolution();
        else if (LeaderOpen) CloseLeader();
    }

    bool LeaderOpen => leaderWindow.activeSelf;

    void OpenLeader(int i)
    {
        selected = i;
        spokenLine = null; // leaders only speak to thank you
        reward = null;
        leaderWindow.SetActive(true);
        ShowLeader(true);
    }

    // Turns in the open leader's finished request: they thank you, and the dialogue shows what it paid.
    void TurnIn()
    {
        NpcDefinition definition = config.npcs[selected];
        bool teaches = definition.unlocksEvolution && !campaign.npcs[selected].everCompleted;
        if (!campaign.TurnIn(selected)) return;
        Thank($"+{config.questGold} gold{(teaches ? $" · learned {Weapon.EvolutionNames[(int)definition.weapon]}" : "")}");
        ShowEvolution(); // their first request: the evolution popup over the window
    }

    // After buying a building level or turning in a request: the leader hops in a burst of sparkles, and their thanks
    // line types out (`rewardLine` after it).
    void Thank(string rewardLine = null)
    {
        spokenLine = config.npcs[selected].thanks;
        reward = rewardLine;
        Refresh();
        dialogue.Play(Spoken(selected));
        portraitMotion.Hop();
        purchaseBurst.Burst();
    }

    void CloseLeader()
    {
        leaderWindow.SetActive(false);
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
               $"Supplies {campaign.supplies}    People {campaign.Population}/{campaign.StartPopulation}    {NextThreat()}";
    }

    // Redraws everything from Campaign.Current (also called by the debug keys).
    public void Refresh()
    {
        statusText.text = StatusLine(0f);

        for (int i = 0; i < NpcCount; i++)
        {
            npcLabels[i].text = NpcSummary(i);
            npcMarks[i].gameObject.SetActive(campaign.QuestReady(i));
        }
        if (LeaderOpen) ShowLeader(false); // e.g. after building: new prices, no retyping
        relicsText.text = RelicsSummary();

        SetLabel(diveButton, campaign.IsRaidDay ? "DEFEND\nTHE TOWN" : "DIVE");
        ShowEpilogue();
    }

    // The end of the run: how long the town held out, the run's stats, and the best run.
    void ShowEpilogue()
    {
        epiloguePanel.SetActive(campaign.Ended);
        if (!campaign.Ended) return;

        epilogueTitle.text = "THE TOWN HAS FALLEN";
        string body = campaign.endingReason;
        body += $"\n\nHeld out until day {campaign.day}\nRaids: {campaign.raidsHeld} held, {campaign.raidsLost} lost";
        body += $"\nDeepest depth {campaign.deepestDepth} · Kills {campaign.totalKills} · Ores {campaign.totalOres}";
        body += campaign.newBest ? "\n\nNEW RECORD!" : $"\n\nBest: day {Campaign.BestDay}";
        epilogueBody.text = body;
    }

    // "RAID TODAY!" on a raid day, then the next quake (and whether a raid comes with it).
    string NextThreat()
    {
        int quakeDay = campaign.NextQuakeDay;
        string today = campaign.IsRaidDay ? "RAID TODAY!    " : "";
        string raid = campaign.IsRaidOn(quakeDay) ? " + raid" : "";
        return $"{today}Quake day {quakeDay}{raid}: {campaign.QuakeForecast:0}%";
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
        if (owned == 0) list = "\nNone yet";
        return $"RELICS {owned}/{campaign.relicOwned.Length}{list}";
    }

    string QuestProgress(Quest quest)
    {
        return quest.IsKill ? $"{quest.progress}/{quest.needed}" : $"{campaign.materials}/{quest.needed}";
    }

    string NpcSummary(int i)
    {
        NpcDefinition definition = config.npcs[i];
        NpcState npc = campaign.npcs[i];
        if (!npc.Alive) return $"{definition.name}\n{definition.faction}: WIPED OUT";

        string half = campaign.DistrictStrength(i) < 1f ? " · half" : "";
        string injured = npc.injured > 0 ? $" · {npc.injured} hurt" : "";
        string request = npc.quest == null ? "Request done" : $"{npc.quest.Describe()} {QuestProgress(npc.quest)}";
        return $"{definition.name}\n{definition.faction} {npc.population}/{definition.startPopulation}{half}{injured}\n" +
               $"{npc.order} · Homes {npc.reinforced:0}% · Ready {npc.readiness:0}%\n{request}";
    }

    // Fills the leader window for `selected`. `retype` starts the dialogue over (opening the window); otherwise the new
    // text just replaces the old (e.g. after building, the numbers change).
    void ShowLeader(bool retype)
    {
        int i = selected;
        NpcDefinition definition = config.npcs[i];
        NpcState npc = campaign.npcs[i];

        leaderTitle.text = $"{definition.name} · {definition.faction}";
        portrait.enabled = definition.portrait != null;
        portrait.sprite = npc.Alive ? definition.portrait : GreyPortrait(definition.portrait);
        portrait.color = npc.Alive ? Color.white : GoneTint;
        nameplate.text = definition.name.ToUpper();
        Color color = npc.Alive ? definition.color : GoneTint;
        if (windowFrame != null) windowFrame.Color = color;
        if (portraitBorder != null) portraitBorder.Color = color;
        portraitGlow.color = new Color(color.r, color.g, color.b, portraitGlow.color.a);
        nameplateLine.color = color;
        // The window's buttons wear the leader's color too
        foreach (Button button in orderButtons) FrameIn(button, color);
        foreach (Button button in buildingButtons) FrameIn(button, color);
        FrameIn(turnInButton, color);
        FrameIn(closeButton, color);
        foreach (OrnateFrame frame in infoFrames)
            if (frame != null) frame.Color = color;

        string text = Spoken(i);
        if (retype) dialogue.Play(text);
        else dialogue.Set(text);
        ShowInfo(i);

        ShowOrders(i);
        ShowBuildings(i);
        foreach (GameObject section in aliveOnly) section.SetActive(npc.Alive);

        // Turn in: usable once the request is finished
        bool hasRequest = npc.Alive && npc.quest != null;
        turnInButton.gameObject.SetActive(hasRequest);
        if (hasRequest)
        {
            SetLabel(turnInButton, npc.quest.IsKill ? "Turn in" : $"Give {npc.quest.needed} materials");
            turnInButton.interactable = campaign.QuestReady(i);
        }
        leaderMark.gameObject.SetActive(campaign.QuestReady(i));
    }

    // What the leader says: their thanks in quotes (and what a turn-in paid) after a purchase, nothing otherwise; a
    // wiped-out leader's line says they're gone.
    string Spoken(int i)
    {
        if (!campaign.npcs[i].Alive) return $"The {config.npcs[i].faction} are gone.";
        if (string.IsNullOrEmpty(spokenLine)) return "";
        return reward != null ? $"\"{spokenLine}\"   {reward}" : $"\"{spokenLine}\"";
    }

    // The three info boxes (hidden with the rest once the faction is wiped out): people, preparedness, request.
    void ShowInfo(int i)
    {
        NpcDefinition definition = config.npcs[i];
        NpcState npc = campaign.npcs[i];

        string people = $"{npc.population} / {definition.startPopulation}";
        if (campaign.DistrictStrength(i) < 1f) people += "\nhalf strength";
        if (npc.injured > 0) people += $"\n{npc.injured} hurt · die day {npc.injuredDeadline}";
        peopleText.text = people;

        preparedText.text = $"Homes {npc.reinforced:0}%\nReady {npc.readiness:0}%\nSupplies {campaign.supplies}";

        if (npc.quest == null)
        {
            requestText.text = $"Done\nNext: day {campaign.NextQuestDay}";
            return;
        }
        string evolution = definition.unlocksEvolution && !npc.everCompleted
            ? $" + {Weapon.EvolutionNames[(int)definition.weapon]}" : "";
        requestText.text = $"{npc.quest.Describe()}\n{QuestProgress(npc.quest).Replace("/", " / ")}{(campaign.QuestReady(i) ? " · DONE" : "")}\n" +
                           $"by day {campaign.NextQuestDay} · {config.questGold} gold{evolution}";
    }

    // ---------- Result screen ----------

    static string ResultTitle(RunResult run)
    {
        if (run.raid) return run.survived ? $"RAID {run.raidNumber} HELD" : $"RAID {run.raidNumber} LOST";
        return run.survived ? "BACK FROM THE DEPTHS" : $"DEFEATED ON DEPTH {run.depth}";
    }

    // How the run went, line by line ("{0}" = a number that rolls up): time held or depths, kills by type, level and
    // evolutions, the loot (brought home or lost), ores, and each leader's request.
    static List<ResultLine> ResultLines(RunResult run)
    {
        List<ResultLine> lines = new List<ResultLine>();
        if (run.raid)
        {
            lines.Add(new ResultLine($"Held {{0}} / {Mathf.RoundToInt(run.raidLength)} s", Mathf.RoundToInt(run.secondsHeld)));
            if (!run.survived) lines.Add(new ResultLine($"Every faction lost {run.lossPercent:0.#}%"));
        }
        else
        {
            lines.Add(new ResultLine("Depths cleared {0}", run.depthsCleared));
            lines.Add(new ResultLine("Deepest {0}", run.depth));
        }
        lines.Add(new ResultLine("Kills {0}", run.totalKills));
        foreach (KeyValuePair<EnemyData, int> pair in run.kills)
            lines.Add(new ResultLine($"      {pair.Key.displayName} {{0}}", pair.Value));
        lines.Add(new ResultLine("Level {0}", run.level));
        if (run.evolutions.Count > 0) lines.Add(new ResultLine("Evolved: " + string.Join(", ", run.evolutions)));

        // The loot (only what there was)
        lines.Add(new ResultLine(""));
        lines.Add(new ResultLine(run.survived ? "Loot" : "Lost"));
        void Loot(string name, int amount)
        {
            if (amount > 0) lines.Add(new ResultLine($"      {name} {{0}}", amount));
        }
        Loot("Materials", run.materials);
        Loot("Crystals", run.crystals);
        Loot("Gold", run.gold);
        Loot("Supplies", run.supplies);
        foreach (RelicId relic in run.relics) lines.Add(new ResultLine($"      Relic: {Relics.Names[(int)relic]}"));
        if (run.ores > 0)
            lines.Add(run.survived
                ? new ResultLine($"Ores {{0}} (quake -{run.quakeDrop:0}%)", run.ores)
                : new ResultLine("Ores {0} (no effect)", run.ores));

        if (run.requestLines.Count > 0)
        {
            lines.Add(new ResultLine(""));
            lines.Add(new ResultLine("Requests"));
            foreach (string request in run.requestLines) lines.Add(new ResultLine("      " + request));
        }
        return lines;
    }

    // A grey copy of a portrait (made once; the builder imports portraits with Read/Write on so the pixels can be read).
    // The window also tints it dark (GoneTint), so it still reads as gone if the copy can't be made.
    Sprite GreyPortrait(Sprite colored)
    {
        if (colored == null || !colored.texture.isReadable) return colored;
        if (greyPortraits.TryGetValue(colored, out Sprite grey)) return grey;

        Texture2D source = colored.texture;
        Color32[] pixels = source.GetPixels32();
        for (int p = 0; p < pixels.Length; p++)
        {
            byte value = (byte)(pixels[p].r * 0.30f + pixels[p].g * 0.59f + pixels[p].b * 0.11f);
            pixels[p] = new Color32(value, value, value, pixels[p].a);
        }
        Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        grey = Sprite.Create(texture, colored.rect, new Vector2(0.5f, 0.5f));
        greyPortraits[colored] = grey;
        return grey;
    }

    // The faction's order buttons: what one day of each gives them now; the current order has the gold bar.
    void ShowOrders(int npcIndex)
    {
        NpcState npc = campaign.npcs[npcIndex];
        for (int k = 0; k < orderButtons.Length; k++)
        {
            orderButtons[k].gameObject.SetActive(npc.Alive);
            SetLabel(orderButtons[k], OrderLabel(npcIndex, (TownOrder)k));
            orderMarks[k].enabled = npc.order == (TownOrder)k;
        }
    }

    string OrderLabel(int i, TownOrder order)
    {
        switch (order)
        {
            case TownOrder.Reinforce: return $"REINFORCE\n+{campaign.ReinforceGain(i):0}% homes, {config.reinforceMaterials} materials";
            case TownOrder.Stockpile: return $"STOCKPILE\n+{campaign.StockpileGain(i)} supplies";
            case TownOrder.Drill: return $"DRILL\n+{campaign.DrillGain(i):0}% readiness";
            default: return $"WORK\n{campaign.WorkGains(i)}";
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
                string crystals = building.costsCrystals ? $" · {campaign.BuildingCrystalCost(b)} crystals" : "";
                label = $"{building.name}  {level}/{config.maxBuildingLevel}\n{building.EffectText(building.amountPerLevel)}\n" +
                        $"{campaign.BuildingMaterialCost(b)} materials · {campaign.BuildingGoldCost(b)} gold{crystals}";
            }
            SetLabel(buildingButtons[slot], label);
            buildingButtons[slot].interactable = campaign.CanBuild(b);
        }
    }

    // A button's frame color (its ButtonLabelDim fades it while the button can't be used).
    static void FrameIn(Button button, Color color)
    {
        ButtonLabelDim dim = button.GetComponent<ButtonLabelDim>();
        if (dim != null) dim.frameColor = color;
    }

    static void SetLabel(Button button, string text)
    {
        button.GetComponentInChildren<Text>().text = text;
    }
}
