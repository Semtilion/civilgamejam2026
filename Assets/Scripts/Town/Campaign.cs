using System.Collections.Generic;
using UnityEngine;

public class NpcState
{
    public int population;     // the faction's people (the injured included); lost people never come back
    public int injured;        // hurt by a quake: can't work, and die on injuredDeadline unless treated
    public int injuredDeadline;
    public float reinforced;   // % of homes reinforced: a quake's casualties x (1 - reinforced)
    public float readiness;    // % drilled: more of a quake's casualties are injured instead of killed; fades daily
    public TownOrder order;    // the faction's job every day until changed (Work at the start)
    public bool everCompleted; // their first completed request unlocks their weapon
    public Quest quest;        // null = request done

    public bool Alive => population > 0; // 0 = faction wiped out, the leader is gone
    public int Healthy => population - injured;
}

public class Quest
{
    public EnemyData target; // kill request; null = "bring materials"
    public int needed;
    public int progress;     // kills so far (materials requests are paid in one go)

    public bool IsKill => target != null;
    public string Describe() => IsKill ? $"Kill {needed} {target.displayName}s" : $"Bring {needed} materials";
}

// The whole playthrough (endless: it ends when every faction is wiped out): day, materials / crystals / gold /
// supplies, NPCs (faction leaders) with their requests, people, preparedness (daily orders) and district buildings,
// unlocked evolutions, relics, the quake meter and the report of what happened to the people while you were
// away. Kept in a static so it survives switching between the Town and Arena scenes. Rules come from
// TownConfig. The best run (the day the town fell) is kept in PlayerPrefs.
public class Campaign
{
    public static Campaign Current { get; private set; }

    // Pressing Play again starts fresh, even if the editor doesn't reload scripts between plays.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Current = null;
    }

    public static Campaign StartNew(TownConfig config)
    {
        Current = new Campaign(config);
        return Current;
    }

    public readonly TownConfig config;
    public readonly NpcState[] npcs;
    public readonly bool[] evolutionUnlocked = new bool[Weapon.Names.Length]; // by WeaponId (leaders teach them)
    public readonly List<int> newEvolutions = new List<int>(); // leaders who just taught you theirs (the town pops each up once)
    public readonly bool[] relicOwned = new bool[Relics.Names.Length];     // by RelicId
    public readonly int[] buildingLevels;                                  // by TownConfig.buildings index
    public int day = 1;
    public int materials;
    public int crystals;
    public int gold;
    public int supplies;     // emergency supplies: each treats one injured person
    public float quakeMeter; // %
    float passiveRise;       // all the passive quake rise so far (never undone by quakes)
    public bool howToPlayShown; // the town pops up How to play once per new game
    public bool quakeSinceTown; // a quake hit while you were away: the town screen shakes once when you get back
    // What the town's counters showed when you left, so they roll up (or down) to the new amounts when you return.
    public int shownMaterials, shownCrystals, shownGold;
    // What happened to the town's people while you were away (a quake, the injured): the town shows it as a report
    // when you get back, then clears it.
    public readonly List<string> report = new List<string>();
    public string reportTitle;
    // The last dive, for the town's result screen (shown first when you get back, then cleared).
    public RunResult lastResult;

    // Run stats for the ending screen (every dive counts, lost ones too).
    public int totalKills;
    public int totalOres;
    public int deepestDepth;

    // The game is over (every faction wiped out; the Town shows the end screen), and why.
    public bool Ended { get; private set; }
    public string endingReason;
    public bool newBest; // this run beat the best record

    // The best run so far (PlayerPrefs, survives restarts): the day the town fell.
    const string BestDayKey = "BestDay";
    public static int BestDay => PlayerPrefs.GetInt(BestDayKey, 0);

    // Quakes: on firstQuakeDay, then every quakeEvery days, forever.
    public bool IsQuakeDay(int d) => d >= config.firstQuakeDay && (d - config.firstQuakeDay) % config.quakeEvery == 0;

    // The day the current requests are due (and new ones arrive).
    public int NextQuestDay => day + config.questInterval - (day - 1) % config.questInterval;

    // The next quake day after today.
    public int NextQuakeDay => day < config.firstQuakeDay
        ? config.firstQuakeDay
        : day + config.quakeEvery - (day - config.firstQuakeDay) % config.quakeEvery;

    // Ley Stones lower the passive rise.
    float PassivePerDay => Mathf.Max(0f, config.quakePassivePerDay - BuildingBonus(BuildingEffect.QuakePassive));
    float DailyQuakeRise => config.quakePerDay + PassivePerDay;
    float QuakeBaseline => config.quakeStart + passiveRise;

    // How strong the next quake will be if no more ores are mined.
    public float QuakeForecast => Mathf.Min(100f, quakeMeter + DailyQuakeRise * (NextQuakeDay - day));

    public int Population
    {
        get
        {
            int total = 0;
            foreach (NpcState npc in npcs) total += npc.population;
            return total;
        }
    }

    public int StartPopulation
    {
        get
        {
            int total = 0;
            foreach (NpcDefinition definition in config.npcs) total += definition.startPopulation;
            return total;
        }
    }

    // Every faction wiped out: the town has fallen.
    public bool GameOver
    {
        get
        {
            foreach (NpcState npc in npcs)
                if (npc.Alive) return false;
            return true;
        }
    }

    // How strongly a leader's district buildings work: the share of their faction's people left (the injured count),
    // e.g. 34 of 50 Mages = 0.68; 0 = wiped out.
    public float DistrictStrength(int npcIndex)
    {
        return (float)npcs[npcIndex].population / config.npcs[npcIndex].startPopulation;
    }

    // ---------- Orders (what each faction does every day) ----------

    // How strongly a faction's order works: its healthy people / its starting people.
    public float WorkShare(int npcIndex) => (float)npcs[npcIndex].Healthy / config.npcs[npcIndex].startPopulation;

    // What one day of each order gives this faction now (Stone Foundations add to Reinforce).
    public float ReinforceGain(int i) => (config.reinforcePerDay + BuildingBonus(BuildingEffect.ReinforceBonus)) * WorkShare(i);
    public int StockpileGain(int i) => Mathf.CeilToInt(config.npcs[i].startPopulation * config.stockpileShare * WorkShare(i));
    public float DrillGain(int i) => config.drillPerDay * WorkShare(i);
    int WorkGain(int amount, int i) => Mathf.CeilToInt(amount * WorkShare(i));
    public string WorkGains(int i)
    {
        NpcDefinition definition = config.npcs[i];
        return Gains(WorkGain(definition.workGold, i), WorkGain(definition.workMaterials, i), WorkGain(definition.workCrystals, i));
    }

    // "+20 gold, +8 materials, +1 crystal" (only the ones above 0).
    static string Gains(int gold, int materials, int crystals)
    {
        List<string> parts = new List<string>();
        if (gold > 0) parts.Add($"+{gold} gold");
        if (materials > 0) parts.Add($"+{materials} materials");
        if (crystals > 0) parts.Add($"+{crystals} crystal{(crystals == 1 ? "" : "s")}");
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    // The day that's ending: readiness fades a little, then every faction does its order (Reinforce needs materials:
    // without them, or once fully reinforced, that faction idles). What each did goes into `lines` (the dive's result
    // screen: "Mages worked: +10 gold, +1 crystal", "Soldiers reinforced homes: +15% (now 45%)", or why it idled).
    void DoOrders(List<string> lines)
    {
        for (int i = 0; i < npcs.Length; i++)
        {
            NpcState npc = npcs[i];
            if (!npc.Alive) continue;
            string faction = config.npcs[i].faction;

            npc.readiness = Mathf.Max(0f, npc.readiness - config.readinessFade);
            switch (npc.order)
            {
                case TownOrder.Reinforce:
                    if (npc.reinforced >= 100f)
                        lines.Add($"{faction} idled: homes already fully reinforced");
                    else if (materials < config.reinforceMaterials)
                        lines.Add($"{faction} idled: no materials to reinforce (needs {config.reinforceMaterials})");
                    else
                    {
                        materials -= config.reinforceMaterials;
                        float before = npc.reinforced;
                        npc.reinforced = Mathf.Min(100f, npc.reinforced + ReinforceGain(i));
                        lines.Add($"{faction} reinforced homes: +{npc.reinforced - before:0}% (now {npc.reinforced:0}%)");
                    }
                    break;
                case TownOrder.Stockpile:
                    int stock = StockpileGain(i);
                    supplies += stock;
                    lines.Add($"{faction} stockpiled: +{stock} {(stock == 1 ? "supply" : "supplies")}");
                    break;
                case TownOrder.Drill:
                    float faded = npc.readiness;
                    npc.readiness = Mathf.Min(100f, npc.readiness + DrillGain(i));
                    lines.Add($"{faction} drilled: +{npc.readiness - faded:0}% readiness (now {npc.readiness:0}%)");
                    break;
                default: // Work
                    NpcDefinition definition = config.npcs[i];
                    int workGold = WorkGain(definition.workGold, i);
                    int workMaterials = WorkGain(definition.workMaterials, i);
                    int workCrystals = WorkGain(definition.workCrystals, i);
                    gold += workGold;
                    materials += workMaterials;
                    crystals += workCrystals;
                    lines.Add($"{faction} worked: {Gains(workGold, workMaterials, workCrystals)}");
                    break;
            }
        }
    }

    Campaign(TownConfig townConfig)
    {
        config = townConfig;
        quakeMeter = config.quakeStart;
        evolutionUnlocked[(int)WeaponId.Sword] = true; // Twin Strike: no leader needed
        npcs = new NpcState[config.npcs.Length];
        for (int i = 0; i < npcs.Length; i++) npcs[i] = new NpcState { population = config.npcs[i].startPopulation };
        buildingLevels = new int[config.buildings.Length];
        RefreshQuests();
    }

    // ---------- Run stats and endings ----------

    public void RecordDive(int kills, int depth, int ores)
    {
        totalKills += kills;
        totalOres += ores;
        deepestDepth = Mathf.Max(deepestDepth, depth);
    }

    // Every faction is gone: the run is over. Saves it as the best run if the town lasted longer than ever.
    public void EndGame(string reason)
    {
        Ended = true;
        endingReason = reason;
        if (day <= BestDay) return;
        newBest = true;
        PlayerPrefs.SetInt(BestDayKey, day);
        PlayerPrefs.Save();
    }

    // ---------- Days ----------

    // Called by the arena when a dive ends. Survived: keep the materials, crystals, gold, supplies and relics,
    // kills count for requests, mined ores lower the quake meter, 1 day passes. Defeated: everything found is lost,
    // `daysLostOnDefeat` (1) days pass. The run is kept for the result screen (lastResult).
    public void EndDive(RunResult run)
    {
        lastResult = run;
        if (run.survived)
        {
            materials += run.materials;
            crystals += run.crystals;
            gold += run.gold;
            supplies += run.supplies;
            run.quakeDrop = run.ores * config.orePercent;
            quakeMeter = Mathf.Max(0f, quakeMeter - run.quakeDrop);
            foreach (RelicId relic in run.relics) relicOwned[(int)relic] = true;
            AddKills(run);
            AdvanceDays(1, run);
        }
        else
        {
            run.requestLines.Add("Kills don't count (you fell).");
            AdvanceDays(config.daysLostOnDefeat, run);
        }
    }

    // Each day: the factions do their orders (so today's preparation counts for tomorrow's quake; what they did goes
    // on the dive's result screen), the quake meter rises (cycle + passive), on a quake day the quake hits, the injured
    // are cared for, on a request day the requests are refreshed.
    void AdvanceDays(int count, RunResult run)
    {
        for (int i = 0; i < count; i++)
        {
            DoOrders(run.townLines);
            day++;
            passiveRise += PassivePerDay;
            quakeMeter = Mathf.Min(100f, quakeMeter + DailyQuakeRise);
            if (IsQuakeDay(day)) Earthquake();
            CareForInjured();
            if ((day - 1) % config.questInterval == 0) RefreshQuests();
        }
    }

    // A line for the report (shown when you're back in town).
    void Report(string line)
    {
        report.Add(line);
    }

    // A quake at `quakeHarmAt`% or more hurts every faction (HitFaction). Then the meter goes back to the start value
    // plus all the passive rise so far.
    void Earthquake()
    {
        float strength = quakeMeter;
        reportTitle = $"EARTHQUAKE: DAY {day}";
        Report($"Strength {strength:0}%.");
        quakeSinceTown = true;

        if (strength < config.quakeHarmAt)
            Report("No one was hurt.");
        else
            for (int i = 0; i < npcs.Length; i++)
                if (npcs[i].Alive) HitFaction(i, strength);

        quakeMeter = Mathf.Min(100f, QuakeBaseline);
        CheckGameOver();
    }

    // strength x `lossPerQuakePercent` % of the faction's starting people would be hurt; reinforced homes spare their
    // share of them. Of the hurt (healthy people only), readiness decides how many are killed and how many injured.
    // Then the quake cracks the reinforcement, and the district loses a building level with a chance of the
    // unreinforced share.
    void HitFaction(int i, float strength)
    {
        NpcState npc = npcs[i];
        NpcDefinition definition = config.npcs[i];

        int unprepared = Mathf.RoundToInt(definition.startPopulation * strength * config.lossPerQuakePercent / 100f);
        int sparedByHomes = Mathf.RoundToInt(unprepared * npc.reinforced / 100f);
        int hurt = Mathf.Min(npc.Healthy, unprepared - sparedByHomes);
        int killed = Mathf.RoundToInt(hurt * Mathf.Lerp(config.killedUnready, config.killedReady, npc.readiness / 100f));
        int injured = hurt - killed;
        int sparedByDrills = Mathf.RoundToInt(hurt * config.killedUnready) - killed; // deaths turned into injuries

        npc.population -= killed;
        npc.injured += injured;
        if (injured > 0) npc.injuredDeadline = day + config.injuredDays;

        string line = $"{definition.faction}: {killed} dead, {injured} hurt ({npc.population}/{definition.startPopulation})";
        if (sparedByHomes > 0) line += $" · homes saved {sparedByHomes}";
        if (sparedByDrills > 0) line += $" · drills saved {sparedByDrills}";
        Report(line + LossNote(i));

        float damageChance = 1f - npc.reinforced / 100f;
        npc.reinforced = Mathf.Max(0f, npc.reinforced - strength * config.reinforceCrack);
        if (npc.Alive && Random.value < damageChance) DamageDistrict(i);
    }

    // Every day after a quake: the Infirmary treats some of the injured for free, then the supplies treat the rest
    // (1 each), faction by faction. Treated people are healthy again; those still untreated on their deadline die.
    void CareForInjured()
    {
        int infirmary = Mathf.RoundToInt(BuildingBonus(BuildingEffect.TreatInjured));
        for (int i = 0; i < npcs.Length; i++)
        {
            NpcState npc = npcs[i];
            NpcDefinition definition = config.npcs[i];
            if (!npc.Alive || npc.injured == 0) continue;
            if (reportTitle == null) reportTitle = $"AFTERMATH: DAY {day}";

            int byInfirmary = Mathf.Min(npc.injured, infirmary);
            infirmary -= byInfirmary;
            int bySupplies = Mathf.Min(npc.injured - byInfirmary, supplies);
            supplies -= bySupplies;
            npc.injured -= byInfirmary + bySupplies;
            if (byInfirmary + bySupplies > 0) Report($"{definition.faction}: {byInfirmary + bySupplies} hurt treated");

            if (npc.injured == 0) continue;
            if (day >= npc.injuredDeadline)
            {
                int died = npc.injured;
                npc.population -= died;
                npc.injured = 0;
                Report($"{definition.faction}: {died} hurt died untreated ({npc.population}/{definition.startPopulation}){LossNote(i)}");
            }
            else
            {
                Report($"{definition.faction}: {npc.injured} hurt need supplies by day {npc.injuredDeadline}");
            }
        }
        CheckGameOver();
    }

    // After a faction lost people: wiped out?
    string LossNote(int i)
    {
        return npcs[i].Alive ? "" : " · WIPED OUT";
    }

    void CheckGameOver()
    {
        if (!GameOver || Ended) return;
        EndGame("Every faction is gone.");
    }

    // A random built building in this leader's district loses a level.
    void DamageDistrict(int npcIndex)
    {
        List<int> built = new List<int>();
        for (int b = 0; b < config.buildings.Length; b++)
            if (config.buildings[b].district == npcIndex && buildingLevels[b] > 0) built.Add(b);
        if (built.Count == 0) return;

        int hit = built[Random.Range(0, built.Count)];
        buildingLevels[hit]--;
        Report($"{config.buildings[hit].name} damaged (now Lv {buildingLevels[hit]})");
    }

    // Request day: finished requests nobody turned in are turned in now, unfinished ones fail (no gold), then every
    // leader left asks for something new, never the same as another leader's (same enemy, or both materials).
    void RefreshQuests()
    {
        HashSet<EnemyData> taken = new HashSet<EnemyData>(); // this round's request targets (null = materials)
        for (int i = 0; i < npcs.Length; i++)
        {
            NpcState npc = npcs[i];
            if (!npc.Alive)
            {
                npc.quest = null;
                continue;
            }
            if (npc.quest != null && day > 1)
            {
                bool done = TurnIn(i); // a finished one is turned in for you
                Report($"{config.npcs[i].name}: request {(done ? "turned in" : "missed")}");
            }
            Quest quest = config.NewQuest();
            for (int tries = 0; taken.Contains(quest.target) && tries < 20; tries++) quest = config.NewQuest();
            taken.Add(quest.target);
            npc.quest = quest;
        }
    }

    // ---------- Requests ----------

    // A run's kills count toward the kill requests (up to what's needed: you turn them in at the leader). Adds one line
    // per request to the run's result.
    void AddKills(RunResult run)
    {
        for (int i = 0; i < npcs.Length; i++)
        {
            NpcState npc = npcs[i];
            Quest quest = npc.quest;
            string name = config.npcs[i].name;
            if (!npc.Alive || quest == null) continue;

            if (!quest.IsKill)
            {
                run.requestLines.Add($"{name}: {quest.Describe()} {materials}/{quest.needed}{(QuestReady(i) ? " · done!" : "")}");
                continue;
            }
            int gained = run.kills.TryGetValue(quest.target, out int count) ? Mathf.Min(count, quest.needed - quest.progress) : 0;
            quest.progress += gained;
            run.requestLines.Add($"{name}: {quest.Describe()} {quest.progress}/{quest.needed}{(gained > 0 ? $" (+{gained})" : "")}{(QuestReady(i) ? " · done!" : "")}");
        }
    }

    // The request is finished and waits to be turned in (a yellow "!"): enough kills, or enough materials to give.
    public bool QuestReady(int npcIndex)
    {
        NpcState npc = npcs[npcIndex];
        if (!npc.Alive || npc.quest == null) return false;
        return npc.quest.IsKill ? npc.quest.progress >= npc.quest.needed : materials >= npc.quest.needed;
    }

    // Turns in a finished request (a materials request hands the materials over). False if it isn't finished.
    public bool TurnIn(int npcIndex)
    {
        if (!QuestReady(npcIndex)) return false;
        Quest quest = npcs[npcIndex].quest;
        if (!quest.IsKill) materials -= quest.needed;
        CompleteQuest(npcIndex);
        return true;
    }

    // A finished request pays gold; the first one also unlocks the leader's weapon evolution.
    void CompleteQuest(int npcIndex)
    {
        NpcState npc = npcs[npcIndex];
        NpcDefinition definition = config.npcs[npcIndex];
        npc.quest = null;
        gold += config.questGold;

        if (npc.everCompleted) return;
        npc.everCompleted = true;
        if (!definition.unlocksEvolution) return;
        evolutionUnlocked[(int)definition.weapon] = true;
        newEvolutions.Add(npcIndex);
    }

    // ---------- District buildings ----------

    // Level N costs N x the per-level prices (crystals only for buildings that cost them).
    public int BuildingMaterialCost(int b) => (buildingLevels[b] + 1) * config.materialsPerLevel;
    public int BuildingGoldCost(int b) => (buildingLevels[b] + 1) * config.goldPerLevel;
    public int BuildingCrystalCost(int b) => config.buildings[b].costsCrystals ? (buildingLevels[b] + 1) * config.crystalsPerLevel : 0;

    // Only in a district whose faction is alive, below the max level, and if you can pay.
    public bool CanBuild(int b)
    {
        return npcs[config.buildings[b].district].Alive && buildingLevels[b] < config.maxBuildingLevel &&
               materials >= BuildingMaterialCost(b) && gold >= BuildingGoldCost(b) && crystals >= BuildingCrystalCost(b);
    }

    // False if you can't (nothing happens).
    public bool Build(int b)
    {
        if (!CanBuild(b)) return false;
        materials -= BuildingMaterialCost(b);
        gold -= BuildingGoldCost(b);
        crystals -= BuildingCrystalCost(b);
        buildingLevels[b]++;
        return true;
    }

    // What one building gives right now: level x amount per level x its district's strength.
    public float BuildingAmount(int b)
    {
        Building building = config.buildings[b];
        return building.amountPerLevel * buildingLevels[b] * DistrictStrength(building.district);
    }

    // The total of every building with this effect (not used for Stat buildings: those apply one stat each).
    public float BuildingBonus(BuildingEffect effect)
    {
        float total = 0f;
        for (int b = 0; b < config.buildings.Length; b++)
            if (config.buildings[b].effect == effect) total += BuildingAmount(b);
        return total;
    }

    // ---------- Dive setup ----------

    // Every dive starts with just the Sword at Lv 1 (other unlocked weapons come from level-up cards in the dive,
    // relic weapons are added after this by Relics.ResetForNewRun), plus the stat buildings of every district
    // (scaled by its faction's share of people left, none if wiped out). Call after Player.ResetForNewRun.
    // XP and materials are read by the arena; the quake, reinforcing and injured ones are used here.
    public void ApplyLoadout(Player player, Weapon[] weapons)
    {
        foreach (Weapon weapon in weapons)
        {
            weapon.level = 1;
            weapon.evolved = false;
            weapon.bonusDamage = 0;
            weapon.enabled = weapon.id == WeaponId.Sword;
        }

        for (int b = 0; b < config.buildings.Length; b++)
        {
            float amount = BuildingAmount(b);
            if (config.buildings[b].effect == BuildingEffect.Stat && amount > 0f)
                player.AddStat(config.buildings[b].stat, amount);
        }
    }
}
