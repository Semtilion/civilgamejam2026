using System.Collections.Generic;
using UnityEngine;

public class NpcState
{
    public int tier;
    public bool alive = true;
    public bool everCompleted;  // their first completed request unlocks their weapon
    public bool buildingBroken; // no help until repaired; the next quake kills them
    public Quest quest;         // null = request done (also braces their home for a quake), waiting for the next request day
}

public class Quest
{
    public EnemyData target; // kill request; null = "bring materials"
    public int needed;
    public int progress;     // kills so far (materials requests are paid in one go)

    public bool IsKill => target != null;
    public string Describe() => IsKill ? $"Kill {needed} {target.displayName}s" : $"Bring {needed} materials";
}

// The whole playthrough: day, materials, NPCs and their requests, weapon levels, the quake meter, and a news log.
// Kept in a static so it survives switching between the Town and Arena scenes. Rules come from TownConfig.
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
    public readonly List<string> news = new List<string>();
    public int day = 1;
    public int materials;
    public int population;
    public float quakeMeter; // %
    public bool reinforced;

    // The day the current requests are due (and new ones arrive).
    public int NextQuestDay => day + config.questInterval - (day - 1) % config.questInterval;
    public float BreakAt => reinforced ? config.reinforcedBreakAt : config.breakAt;

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

    float DailyQuakeRise => config.quakePerDay + config.quakePassivePerDay;

    // How strong the next quake will be if no more ores are mined.
    public float QuakeForecast => NextQuakeDay < 0 ? quakeMeter : Mathf.Min(100f, quakeMeter + DailyQuakeRise * (NextQuakeDay - day));

    Campaign(TownConfig townConfig)
    {
        config = townConfig;
        population = config.startPopulation;
        quakeMeter = config.quakeStart;
        weaponUnlocked[(int)WeaponId.Sword] = true;
        npcs = new NpcState[config.npcs.Length];
        for (int i = 0; i < npcs.Length; i++) npcs[i] = new NpcState();
        news.Add("Day 1: the townsfolk ask for your help before the earthquake comes.");
        RefreshQuests();
    }

    // ---------- Days ----------

    // Called by the arena when a dive ends. Survived: keep the materials, kills count for requests, mined ores lower
    // the quake meter, 1 day passes. Defeated: everything found (ores too) is lost, `daysLostOnDefeat` (1) days pass.
    public void EndDive(bool survived, Dictionary<EnemyData, int> kills, int foundMaterials, int oresMined)
    {
        if (survived)
        {
            materials += foundMaterials;
            float drop = oresMined * config.orePercent;
            quakeMeter = Mathf.Max(0f, quakeMeter - drop);
            news.Add($"You return with {foundMaterials} materials and {oresMined} ores destroyed (quake meter -{drop:0}%).");
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
    // finished ones still protect their NPC's home); on a request day the requests are refreshed.
    void AdvanceDays(int count)
    {
        for (int i = 0; i < count; i++)
        {
            day++;
            quakeMeter = Mathf.Min(100f, quakeMeter + DailyQuakeRise);
            if (System.Array.IndexOf(config.earthquakeDays, day) >= 0) Earthquake();
            if ((day - 1) % config.questInterval == 0) RefreshQuests();
            if (day == config.hordeDay)
                news.Add($"Day {day}: the final horde should attack today. (Not built yet.)");
        }
    }

    // 1) Homes still broken from the last quake collapse: those NPCs die.
    // 2) If the meter is at the break threshold or above, the weakest NPC whose request isn't finished
    //    (finished = their home is braced) gets their building broken: no help until repaired.
    // 3) The meter goes back to the start value plus all the passive rise so far (danger keeps growing).
    //    (Population doesn't change yet; it's waiting for a rework.)
    void Earthquake()
    {
        news.Add($"Day {day}: EARTHQUAKE! Strength {quakeMeter:0}%.");

        for (int i = 0; i < npcs.Length; i++)
        {
            if (!npcs[i].alive || !npcs[i].buildingBroken) continue;
            npcs[i].alive = false;
            news.Add($"{config.npcs[i].name}'s home was still broken and collapsed. They didn't survive.");
        }

        if (quakeMeter >= BreakAt)
        {
            int weakest = -1;
            int ties = 0;
            for (int i = 0; i < npcs.Length; i++)
            {
                NpcState npc = npcs[i];
                if (!npc.alive || npc.quest == null) continue; // dead, or home braced by a finished request
                if (weakest < 0 || npc.tier < npcs[weakest].tier)
                {
                    weakest = i;
                    ties = 1;
                }
                else if (npc.tier == npcs[weakest].tier && Random.Range(0, ++ties) == 0)
                {
                    weakest = i; // equal chance among NPCs tied for the lowest tier
                }
            }

            if (weakest >= 0)
            {
                npcs[weakest].buildingBroken = true;
                news.Add($"{config.npcs[weakest].name}'s home was wrecked: no help until repaired ({config.repairCost} materials). Another quake would kill them.");
            }
            else
            {
                news.Add("Every home was braced in time. Nothing broke.");
            }
        }
        else
        {
            news.Add("The town held. Nothing broke.");
        }

        quakeMeter = Mathf.Min(100f, config.quakeStart + config.quakePassivePerDay * (day - 1));
    }

    // Request day: unfinished requests fail (that NPC's help weakens a tier), then everyone alive asks for something new.
    void RefreshQuests()
    {
        for (int i = 0; i < npcs.Length; i++)
        {
            NpcState npc = npcs[i];
            if (!npc.alive) continue;
            if (npc.quest != null && day > 1)
            {
                npc.tier = Mathf.Max(0, npc.tier - 1);
                news.Add($"{config.npcs[i].name}'s request wasn't met. Their help weakens (tier {npc.tier}).");
            }
            npc.quest = config.NewQuest();
        }
        if (day > 1) news.Add($"Day {day}: new requests from the townsfolk.");
    }

    // ---------- Requests ----------

    void AddKills(Dictionary<EnemyData, int> kills)
    {
        for (int i = 0; i < npcs.Length; i++)
        {
            Quest quest = npcs[i].quest;
            if (!npcs[i].alive || quest == null || !quest.IsKill) continue;

            if (kills.TryGetValue(quest.target, out int count)) quest.progress += count;
            if (quest.progress >= quest.needed) CompleteQuest(i);
        }
    }

    public bool CanGive(int npcIndex)
    {
        NpcState npc = npcs[npcIndex];
        return npc.alive && npc.quest != null && !npc.quest.IsKill && materials >= npc.quest.needed;
    }

    public void GiveMaterials(int npcIndex)
    {
        if (!CanGive(npcIndex)) return;
        materials -= npcs[npcIndex].quest.needed;
        CompleteQuest(npcIndex);
    }

    void CompleteQuest(int npcIndex)
    {
        NpcState npc = npcs[npcIndex];
        NpcDefinition definition = config.npcs[npcIndex];
        npc.quest = null;
        npc.tier = Mathf.Min(config.maxTier, npc.tier + 1);
        news.Add($"{definition.name}'s request is done! Their help grows (tier {npc.tier}).");

        if (npc.everCompleted) return;
        npc.everCompleted = true;
        if (definition.unlocksWeapon && !weaponUnlocked[(int)definition.weapon])
        {
            weaponUnlocked[(int)definition.weapon] = true;
            news.Add($"{definition.name} gives you the {Weapon.Names[(int)definition.weapon]}! It can now show up on level-up cards in the dungeon.");
        }
    }

    // ---------- Buildings ----------

    public bool CanRepair(int npcIndex)
    {
        NpcState npc = npcs[npcIndex];
        return npc.alive && npc.buildingBroken && materials >= config.repairCost;
    }

    public void Repair(int npcIndex)
    {
        if (!CanRepair(npcIndex)) return;
        materials -= config.repairCost;
        npcs[npcIndex].buildingBroken = false;
        news.Add($"{config.npcs[npcIndex].name}'s home is repaired. Their help is back.");
    }

    public bool CanReinforce => !reinforced && materials >= config.reinforceCost;

    // One-time: buildings only break from a quake at `reinforcedBreakAt` or above.
    public void Reinforce()
    {
        if (!CanReinforce) return;
        materials -= config.reinforceCost;
        reinforced = true;
        news.Add($"The town is reinforced. Homes now only break at {config.reinforcedBreakAt:0}%+ quakes.");
    }

    // ---------- Dive setup ----------

    // Every dive starts with just the Sword at Lv 1 (other unlocked weapons come from level-up cards in the dive),
    // plus the help of every living NPC whose home is standing, on top of base stats. Call after Player.ResetForNewRun.
    public void ApplyLoadout(Player player, Weapon[] weapons)
    {
        foreach (Weapon weapon in weapons)
        {
            weapon.level = 1;
            weapon.enabled = weapon.id == WeaponId.Sword;
        }

        for (int i = 0; i < npcs.Length; i++)
        {
            if (npcs[i].alive && !npcs[i].buildingBroken && npcs[i].tier > 0)
                player.AddStat(config.npcs[i].stat, config.npcs[i].amountPerTier * npcs[i].tier);
        }
    }
}
