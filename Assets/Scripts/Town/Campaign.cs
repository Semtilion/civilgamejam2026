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
// unlocked evolutions, relics, the quake meter, raids, a news log and the report of what happened to the people while
// you were away. Kept in a static so it survives switching between the Town and Arena scenes. Rules come from
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
    public readonly bool[] relicOwned = new bool[Relics.Names.Length];     // by RelicId
    public readonly int[] buildingLevels;                                  // by TownConfig.buildings index
    public readonly List<string> news = new List<string>();
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
    // The last dive or raid, for the town's result screen (shown first when you get back, then cleared).
    public RunResult lastResult;

    // Run stats for the ending screen (every dive counts, lost ones too).
    public int totalKills;
    public int totalOres;
    public int deepestDepth;
    public int raidsHeld, raidsLost;

    // The game is over (every faction wiped out; the Town shows the end screen), and why.
    public bool Ended { get; private set; }
    public string endingReason;
    public bool newBest; // this run beat the best record

    // The best run so far (PlayerPrefs, survives restarts): the day the town fell, and the raids it held.
    const string BestDayKey = "BestDay", BestRaidsKey = "BestRaids";
    public static int BestDay => PlayerPrefs.GetInt(BestDayKey, 0);
    public static int BestRaids => PlayerPrefs.GetInt(BestRaidsKey, 0);

    // Quakes: on firstQuakeDay, then every quakeEvery days, forever. Every raidEveryQuakes-th quake brings a raid
    // that day: the day's dive becomes defending the town (the arena runs the raid).
    public bool IsQuakeDay(int d) => d >= config.firstQuakeDay && (d - config.firstQuakeDay) % config.quakeEvery == 0;
    int QuakeNumber(int d) => (d - config.firstQuakeDay) / config.quakeEvery + 1; // 1 = the first quake
    public bool IsRaidOn(int d) => IsQuakeDay(d) && QuakeNumber(d) % config.raidEveryQuakes == 0;
    public bool IsRaidDay => IsRaidOn(day) && !Ended;
    public int RaidNumber => QuakeNumber(day) / config.raidEveryQuakes; // on a raid day: 1, 2, 3...

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

    // How strongly a leader's district buildings work: 1 = full, 0.5 = their faction is below half, 0 = wiped out.
    public float DistrictStrength(int npcIndex)
    {
        NpcState npc = npcs[npcIndex];
        if (!npc.Alive) return 0f;
        return npc.population < config.npcs[npcIndex].startPopulation * config.halfHelpBelow ? 0.5f : 1f;
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

    // The day that's ending: readiness fades a little, then every faction does its order. One news line.
    void DoOrders()
    {
        List<string> done = new List<string>();
        for (int i = 0; i < npcs.Length; i++)
        {
            NpcState npc = npcs[i];
            string faction = config.npcs[i].faction;
            if (!npc.Alive) continue;

            npc.readiness = Mathf.Max(0f, npc.readiness - config.readinessFade);
            switch (npc.order)
            {
                case TownOrder.Reinforce:
                    if (npc.reinforced >= 100f)
                    {
                        done.Add($"{faction}' homes all reinforced");
                    }
                    else if (materials < config.reinforceMaterials)
                    {
                        done.Add($"{faction} idle (no materials to reinforce with)");
                    }
                    else
                    {
                        materials -= config.reinforceMaterials;
                        npc.reinforced = Mathf.Min(100f, npc.reinforced + ReinforceGain(i));
                        done.Add($"{faction} reinforced homes ({npc.reinforced:0}%)");
                    }
                    break;
                case TownOrder.Stockpile:
                    int stockpiled = StockpileGain(i);
                    supplies += stockpiled;
                    done.Add($"{faction} stockpiled {stockpiled} supplies");
                    break;
                case TownOrder.Drill:
                    npc.readiness = Mathf.Min(100f, npc.readiness + DrillGain(i));
                    done.Add($"{faction} drilled (readiness {npc.readiness:0}%)");
                    break;
                default: // Work
                    NpcDefinition definition = config.npcs[i];
                    int workGold = WorkGain(definition.workGold, i);
                    int workMaterials = WorkGain(definition.workMaterials, i);
                    int workCrystals = WorkGain(definition.workCrystals, i);
                    gold += workGold;
                    materials += workMaterials;
                    crystals += workCrystals;
                    done.Add($"{faction} worked ({Gains(workGold, workMaterials, workCrystals)})");
                    break;
            }
        }
        if (done.Count > 0) news.Add($"Day {day} orders: {string.Join(" · ", done)}.");
    }

    Campaign(TownConfig townConfig)
    {
        config = townConfig;
        quakeMeter = config.quakeStart;
        evolutionUnlocked[(int)WeaponId.Sword] = true; // Twin Strike: no leader needed
        npcs = new NpcState[config.npcs.Length];
        for (int i = 0; i < npcs.Length; i++) npcs[i] = new NpcState { population = config.npcs[i].startPopulation };
        buildingLevels = new int[config.buildings.Length];
        news.Add("Day 1: the faction leaders ask for your help before the earthquake comes.");
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
        PlayerPrefs.SetInt(BestRaidsKey, raidsHeld);
        PlayerPrefs.Save();
    }

    // ---------- Raids ----------

    // The raid was held: news + report (then the arena ends it like a dive: the loot is kept, a day passes).
    public void RaidHeld()
    {
        raidsHeld++;
        reportTitle = $"RAID HELD: DAY {day}";
        Report($"Day {day}: you held the town against raid {RaidNumber}. The crystal monsters fall back into the cracks.");
    }

    // The raid was lost with `timeLeftShare` (0-1) of its timer left: every faction loses (day x raidLossPerDay)% of
    // its starting people, times that share (they never come back). Then the arena ends it like a lost dive.
    public void RaidLost(float timeLeftShare)
    {
        raidsLost++;
        reportTitle = $"RAID LOST: DAY {day}";
        float share = day * config.raidLossPerDay / 100f * timeLeftShare;
        Report($"Day {day}: the raiders broke into the town with {timeLeftShare * 100f:0}% of the raid left.");
        for (int i = 0; i < npcs.Length; i++)
        {
            NpcState npc = npcs[i];
            NpcDefinition definition = config.npcs[i];
            if (!npc.Alive) continue;
            bool wasFull = DistrictStrength(i) == 1f;
            int lost = Mathf.Min(npc.population, Mathf.RoundToInt(definition.startPopulation * share));
            npc.population -= lost;
            npc.injured = Mathf.Min(npc.injured, npc.population);
            Report($"The {definition.faction} lost {lost} people ({npc.population}/{definition.startPopulation} left)." + LossNote(i, wasFull));
        }
        CheckGameOver();
    }

    // ---------- Days ----------

    // Called by the arena when a dive or raid ends. Survived: keep the materials, crystals, gold, supplies and relics,
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
            news.Add($"You return with {run.materials} materials, {run.crystals} crystals, {run.gold} gold, " +
                     $"{run.supplies} supplies and {run.ores} ores destroyed (quake meter -{run.quakeDrop:0}%).");
            foreach (RelicId relic in run.relics)
            {
                relicOwned[(int)relic] = true;
                news.Add($"You bring home a relic: {Relics.Names[(int)relic]}! It's yours for every dive from now on.");
            }
            AddKills(run);
            AdvanceDays(1);
        }
        else
        {
            news.Add("You were defeated. Everything found on the dive is lost.");
            run.requestLines.Add("Kills on a lost run don't count for requests.");
            AdvanceDays(config.daysLostOnDefeat);
        }
    }

    // Each day: the factions do their orders (so today's preparation counts for tomorrow's quake), the quake meter
    // rises (cycle + passive), on a quake day the quake hits, the injured are cared for, on a request day the requests
    // are refreshed, and a raid day is announced.
    void AdvanceDays(int count)
    {
        for (int i = 0; i < count; i++)
        {
            DoOrders();
            day++;
            passiveRise += PassivePerDay;
            quakeMeter = Mathf.Min(100f, quakeMeter + DailyQuakeRise);
            if (IsQuakeDay(day)) Earthquake();
            CareForInjured();
            if ((day - 1) % config.questInterval == 0) RefreshQuests();
            if (IsRaidDay) Report($"Day {day}: crystal monsters pour out of the cracks! Today, defend the town.");
        }
    }

    // A line for both the news log and the report.
    void Report(string line)
    {
        news.Add(line);
        report.Add(line);
    }

    // A quake at `quakeHarmAt`% or more hurts every faction (HitFaction). Then the meter goes back to the start value
    // plus all the passive rise so far.
    void Earthquake()
    {
        float strength = quakeMeter;
        reportTitle = $"EARTHQUAKE: DAY {day}";
        Report($"Day {day}: EARTHQUAKE! Strength {strength:0}%.");
        quakeSinceTown = true;

        if (strength < config.quakeHarmAt)
            Report("The town held. No one was hurt.");
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
        bool wasFull = DistrictStrength(i) == 1f;

        int unprepared = Mathf.RoundToInt(definition.startPopulation * strength * config.lossPerQuakePercent / 100f);
        int sparedByHomes = Mathf.RoundToInt(unprepared * npc.reinforced / 100f);
        int hurt = Mathf.Min(npc.Healthy, unprepared - sparedByHomes);
        int killed = Mathf.RoundToInt(hurt * Mathf.Lerp(config.killedUnready, config.killedReady, npc.readiness / 100f));
        int injured = hurt - killed;
        int sparedByDrills = Mathf.RoundToInt(hurt * config.killedUnready) - killed; // deaths turned into injuries

        npc.population -= killed;
        npc.injured += injured;
        if (injured > 0) npc.injuredDeadline = day + config.injuredDays;

        string line = $"The {definition.faction}: {killed} killed, {injured} injured ({npc.population}/{definition.startPopulation} left).";
        if (sparedByHomes > 0) line += $" Reinforced homes ({npc.reinforced:0}%) spared {sparedByHomes}.";
        if (sparedByDrills > 0) line += $" Drills turned {sparedByDrills} deaths into injuries.";
        Report(line + LossNote(i, wasFull));

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
            if (byInfirmary + bySupplies > 0)
            {
                string how = byInfirmary == 0 ? $"{bySupplies} supplies used" :
                    bySupplies == 0 ? "by the Infirmary" : $"{byInfirmary} by the Infirmary, {bySupplies} with supplies";
                Report($"{byInfirmary + bySupplies} injured {definition.faction} were treated ({how}).");
            }

            if (npc.injured == 0) continue;
            if (day >= npc.injuredDeadline)
            {
                bool wasFull = DistrictStrength(i) == 1f;
                int died = npc.injured;
                npc.population -= died;
                npc.injured = 0;
                Report($"{died} injured {definition.faction} died without treatment ({npc.population}/{definition.startPopulation} left)." +
                       LossNote(i, wasFull));
            }
            else
            {
                Report($"{npc.injured} injured {definition.faction} still need supplies: without them they die on day {npc.injuredDeadline}.");
            }
        }
        CheckGameOver();
    }

    // After a faction lost people: wiped out, or newly under half.
    string LossNote(int i, bool wasFull)
    {
        if (!npcs[i].Alive) return $" They are wiped out, and {config.npcs[i].name} is gone. Their district stands empty.";
        return wasFull && DistrictStrength(i) < 1f ? " Under half left: their district works at half strength." : "";
    }

    void CheckGameOver()
    {
        if (!GameOver || Ended) return;
        news.Add("Every faction is gone. The town has fallen.");
        EndGame("The earthquakes and the raids wiped out every faction.");
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
        news.Add($"The quake damaged the {config.buildings[hit].name} (level {buildingLevels[hit]}).");
    }

    // Request day: finished requests nobody turned in are turned in now, unfinished ones fail (no gold), then every
    // leader left asks for something new.
    void RefreshQuests()
    {
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
                if (QuestReady(i))
                {
                    news.Add($"{config.npcs[i].name} took your finished request at the deadline.");
                    TurnIn(i);
                }
                else
                {
                    news.Add($"{config.npcs[i].name}'s request wasn't met.");
                }
            }
            npc.quest = config.NewQuest();
        }
        if (day > 1 && !GameOver) news.Add($"Day {day}: new requests from the faction leaders.");
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

            string ready = QuestReady(i) ? " - ready to turn in!" : "";
            if (!quest.IsKill)
            {
                run.requestLines.Add($"{name}: {quest.Describe()} (you have {materials}/{quest.needed}){ready}");
                continue;
            }
            bool wasReady = QuestReady(i);
            int gained = run.kills.TryGetValue(quest.target, out int count) ? Mathf.Min(count, quest.needed - quest.progress) : 0;
            quest.progress += gained;
            ready = QuestReady(i) ? " - ready to turn in!" : "";
            if (!wasReady && QuestReady(i)) news.Add($"{name}'s request is done: turn it in!");
            run.requestLines.Add($"{name}: {quest.Describe()} {quest.progress}/{quest.needed}{(gained > 0 ? $" (+{gained})" : "")}{ready}");
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
        news.Add($"{definition.name}'s request is done! They pay you {config.questGold} gold.");

        if (npc.everCompleted) return;
        npc.everCompleted = true;
        if (definition.unlocksEvolution && !evolutionUnlocked[(int)definition.weapon])
        {
            evolutionUnlocked[(int)definition.weapon] = true;
            int w = (int)definition.weapon;
            news.Add($"{definition.name} teaches you {Weapon.EvolutionNames[w]}! Once your {Weapon.Names[w]} reaches " +
                     $"Lv {Weapon.MaxLevel} in a dive, it can evolve.");
        }
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
        Building building = config.buildings[b];
        news.Add($"Built: {building.name} level {buildingLevels[b]} ({building.EffectText(building.amountPerLevel * buildingLevels[b])} in total).");
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
    // (half strength under half, none if wiped out). Call after Player.ResetForNewRun.
    // XP and materials are read by the arena; the quake, reinforcing and injured ones are used here.
    public void ApplyLoadout(Player player, Weapon[] weapons)
    {
        foreach (Weapon weapon in weapons)
        {
            weapon.level = 1;
            weapon.evolved = false;
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
