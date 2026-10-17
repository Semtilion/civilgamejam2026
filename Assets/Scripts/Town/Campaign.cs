using System.Collections.Generic;
using UnityEngine;

public class NpcState
{
    public int population;     // the faction's people; lost people never come back
    public bool everCompleted; // their first completed request unlocks their weapon
    public Quest quest;        // null = request done (also braces their faction's homes for a quake)

    public bool Alive => population > 0; // 0 = faction wiped out, the leader is gone
}

public class Quest
{
    public EnemyData target; // kill request; null = "bring materials"
    public int needed;
    public int progress;     // kills so far (materials requests are paid in one go)

    public bool IsKill => target != null;
    public string Describe() => IsKill ? $"Kill {needed} {target.displayName}s" : $"Bring {needed} materials";
}

// The whole playthrough: day, materials / crystals / gold, NPCs (faction leaders) with their requests, people and
// district buildings, unlocked weapons, relics, the quake meter, and a news log. Kept in a static so it survives
// switching between the Town and Arena scenes. Rules come from TownConfig.
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
    public readonly bool[] weaponUnlocked = new bool[Weapon.Names.Length]; // by WeaponId
    public readonly bool[] relicOwned = new bool[Relics.Names.Length];     // by RelicId
    public readonly int[] buildingLevels;                                  // by TownConfig.buildings index
    public readonly List<string> news = new List<string>();
    public int day = 1;
    public int materials;
    public int crystals;
    public int gold;
    public float quakeMeter; // %
    float passiveRise;       // all the passive quake rise so far (never undone by quakes)
    public bool howToPlayShown; // the town pops up How to play once per new game
    public bool quakeSinceTown; // a quake hit while you were away: the town screen shakes once when you get back
    // What the town's counters showed when you left, so they roll up (or down) to the new amounts when you return.
    public int shownMaterials, shownCrystals, shownGold;

    // Run stats for the ending screen (every dive counts, lost ones too).
    public int totalKills;
    public int totalOres;
    public int deepestDepth;

    // The game is over (the Town shows the epilogue): won or lost, and why.
    public bool Ended { get; private set; }
    public bool Won { get; private set; }
    public string endingReason;

    // Day 15: the only thing left is to defend the town from the horde.
    public bool IsHordeDay => day >= config.hordeDay && !Ended;

    // The day the current requests are due (and new ones arrive).
    public int NextQuestDay => day + config.questInterval - (day - 1) % config.questInterval;

    // The next quake day after today, or -1 if there are no more.
    public int NextQuakeDay
    {
        get
        {
            foreach (int quakeDay in config.earthquakeDays)
                if (quakeDay > day) return quakeDay;
            return -1;
        }
    }

    // Ley Stones lower the passive rise, Stone Foundations the value the meter goes back to after a quake.
    float PassivePerDay => Mathf.Max(0f, config.quakePassivePerDay - BuildingBonus(BuildingEffect.QuakePassive));
    float DailyQuakeRise => config.quakePerDay + PassivePerDay;
    float QuakeBaseline => Mathf.Max(0f, config.quakeStart - BuildingBonus(BuildingEffect.QuakeStart)) + passiveRise;

    // How strong the next quake will be if no more ores are mined.
    public float QuakeForecast => NextQuakeDay < 0 ? quakeMeter : Mathf.Min(100f, quakeMeter + DailyQuakeRise * (NextQuakeDay - day));

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

    Campaign(TownConfig townConfig)
    {
        config = townConfig;
        quakeMeter = config.quakeStart;
        weaponUnlocked[(int)WeaponId.Sword] = true;
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

    public void EndGame(bool won, string reason)
    {
        Ended = true;
        Won = won;
        endingReason = reason;
    }

    // ---------- Days ----------

    // Called by the arena when a dive ends. Survived: keep the materials, crystals, gold and relics, kills count for
    // requests, mined ores lower the quake meter, 1 day passes. Defeated: everything found is lost,
    // `daysLostOnDefeat` (1) days pass.
    public void EndDive(bool survived, Dictionary<EnemyData, int> kills, int foundMaterials, int foundCrystals, int foundGold,
        int oresMined, IReadOnlyList<RelicId> foundRelics)
    {
        if (survived)
        {
            materials += foundMaterials;
            crystals += foundCrystals;
            gold += foundGold;
            float drop = oresMined * config.orePercent;
            quakeMeter = Mathf.Max(0f, quakeMeter - drop);
            news.Add($"You return with {foundMaterials} materials, {foundCrystals} crystals, {foundGold} gold and " +
                     $"{oresMined} ores destroyed (quake meter -{drop:0}%).");
            foreach (RelicId relic in foundRelics)
            {
                relicOwned[(int)relic] = true;
                news.Add($"You bring home a relic: {Relics.Names[(int)relic]}! It's yours for every dive from now on.");
            }
            AddKills(kills);
            AdvanceDays(1);
        }
        else
        {
            news.Add("You were defeated. Everything found on the dive is lost.");
            AdvanceDays(config.daysLostOnDefeat);
        }
    }

    // Each day: the quake meter rises (cycle + passive); on a quake day the quake hits (before new requests, so
    // finished ones still protect their faction); on a request day the requests are refreshed.
    void AdvanceDays(int count)
    {
        for (int i = 0; i < count; i++)
        {
            day++;
            passiveRise += PassivePerDay;
            quakeMeter = Mathf.Min(100f, quakeMeter + DailyQuakeRise);
            if (System.Array.IndexOf(config.earthquakeDays, day) >= 0) Earthquake();
            if ((day - 1) % config.questInterval == 0) RefreshQuests();
            if (day == config.hordeDay && !Ended)
                news.Add($"Day {day}: the horde is coming! Defend the town.");
        }
    }

    // A quake at `quakeHarmAt`% or more hits every faction whose leader's request isn't finished (finished = braced,
    // safe): it loses strength x `lossPerQuakePercent` % of its starting people (they never come back), and one of its
    // district's buildings loses a level. Then the meter goes back to the start value plus all the passive rise so far.
    void Earthquake()
    {
        news.Add($"Day {day}: EARTHQUAKE! Strength {quakeMeter:0}%.");
        quakeSinceTown = true;

        if (quakeMeter < config.quakeHarmAt)
        {
            news.Add("The town held. No one was lost.");
        }
        else
        {
            float lossShare = quakeMeter * config.lossPerQuakePercent / 100f;
            for (int i = 0; i < npcs.Length; i++)
            {
                NpcState npc = npcs[i];
                NpcDefinition definition = config.npcs[i];
                if (!npc.Alive) continue;
                if (npc.quest == null)
                {
                    news.Add($"The {definition.faction}' homes were braced: no one lost.");
                    continue;
                }

                bool wasFull = DistrictStrength(i) == 1f;
                int lost = Mathf.Min(npc.population, Mathf.RoundToInt(definition.startPopulation * lossShare));
                npc.population -= lost;
                string line = $"The {definition.faction} lost {lost} people ({npc.population}/{definition.startPopulation}).";
                if (!npc.Alive) line += $" They are wiped out, and {definition.name} is gone. Their district stands empty.";
                else if (wasFull && DistrictStrength(i) < 1f) line += $" Under half left: their district works at half strength.";
                news.Add(line);
                DamageDistrict(i);
            }
        }

        quakeMeter = Mathf.Min(100f, QuakeBaseline);
        if (GameOver && !Ended)
        {
            news.Add("Every faction is gone. The town has fallen.");
            EndGame(false, "The earthquakes wiped out every faction.");
        }
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

    // Request day: unfinished requests fail (no gold, homes unbraced), then every leader left asks for something new.
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
            if (npc.quest != null && day > 1) news.Add($"{config.npcs[i].name}'s request wasn't met.");
            npc.quest = config.NewQuest();
        }
        if (day > 1 && !GameOver) news.Add($"Day {day}: new requests from the faction leaders.");
    }

    // ---------- Requests ----------

    void AddKills(Dictionary<EnemyData, int> kills)
    {
        for (int i = 0; i < npcs.Length; i++)
        {
            Quest quest = npcs[i].quest;
            if (!npcs[i].Alive || quest == null || !quest.IsKill) continue;

            if (kills.TryGetValue(quest.target, out int count)) quest.progress += count;
            if (quest.progress >= quest.needed) CompleteQuest(i);
        }
    }

    public bool CanGive(int npcIndex)
    {
        NpcState npc = npcs[npcIndex];
        return npc.Alive && npc.quest != null && !npc.quest.IsKill && materials >= npc.quest.needed;
    }

    public void GiveMaterials(int npcIndex)
    {
        if (!CanGive(npcIndex)) return;
        materials -= npcs[npcIndex].quest.needed;
        CompleteQuest(npcIndex);
    }

    // A finished request pays gold and braces the faction's homes; the first one also unlocks the leader's weapon.
    void CompleteQuest(int npcIndex)
    {
        NpcState npc = npcs[npcIndex];
        NpcDefinition definition = config.npcs[npcIndex];
        npc.quest = null;
        gold += config.questGold;
        news.Add($"{definition.name}'s request is done! They pay you {config.questGold} gold, and the {definition.faction}' homes are braced.");

        if (npc.everCompleted) return;
        npc.everCompleted = true;
        if (definition.unlocksWeapon && !weaponUnlocked[(int)definition.weapon])
        {
            weaponUnlocked[(int)definition.weapon] = true;
            news.Add($"{definition.name} gives you the {Weapon.Names[(int)definition.weapon]}! It can now show up on level-up cards in the dungeon.");
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

    public void Build(int b)
    {
        if (!CanBuild(b)) return;
        materials -= BuildingMaterialCost(b);
        gold -= BuildingGoldCost(b);
        crystals -= BuildingCrystalCost(b);
        buildingLevels[b]++;
        Building building = config.buildings[b];
        news.Add($"Built: {building.name} level {buildingLevels[b]} ({building.EffectText(building.amountPerLevel * buildingLevels[b])} in total).");
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
    // The other building effects (XP, materials, healing) are read by the arena; the quake ones are used here.
    public void ApplyLoadout(Player player, Weapon[] weapons)
    {
        foreach (Weapon weapon in weapons)
        {
            weapon.level = 1;
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
