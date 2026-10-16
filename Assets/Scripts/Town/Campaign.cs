using System.Collections.Generic;
using UnityEngine;

public class NpcState
{
    public int tier;
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

// The whole playthrough: day, materials, NPCs (faction leaders) and their requests and people, unlocked weapons,
// the quake meter, and a news log. Kept in a static so it survives switching between the Town and Arena scenes.
// Rules come from TownConfig.
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
    public readonly List<string> news = new List<string>();
    public int day = 1;
    public int materials;
    public int crystals;
    public float quakeMeter; // %
    public readonly int[] shopRanks; // by TownConfig.shopItems index

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

    float DailyQuakeRise => config.quakePerDay + config.quakePassivePerDay;

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

    // How much of an NPC's help still counts: 1 = full, 0.5 = their faction is below half, 0 = wiped out.
    public float HelpMultiplier(int npcIndex)
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
        shopRanks = new int[config.shopItems.Length];
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

    // Called by the arena when a dive ends. Survived: keep the materials, crystals and relics, kills count for requests,
    // mined ores lower the quake meter, 1 day passes. Defeated: everything found is lost, `daysLostOnDefeat` (1) days pass.
    public void EndDive(bool survived, Dictionary<EnemyData, int> kills, int foundMaterials, int foundCrystals, int oresMined,
        IReadOnlyList<RelicId> foundRelics)
    {
        if (survived)
        {
            materials += foundMaterials;
            crystals += foundCrystals;
            float drop = oresMined * config.orePercent;
            quakeMeter = Mathf.Max(0f, quakeMeter - drop);
            news.Add($"You return with {foundMaterials} materials, {foundCrystals} crystals and {oresMined} ores destroyed (quake meter -{drop:0}%).");
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
            quakeMeter = Mathf.Min(100f, quakeMeter + DailyQuakeRise);
            if (System.Array.IndexOf(config.earthquakeDays, day) >= 0) Earthquake();
            if ((day - 1) % config.questInterval == 0) RefreshQuests();
            if (day == config.hordeDay && !Ended)
                news.Add($"Day {day}: the horde is coming! Defend the town.");
        }
    }

    // A quake at `quakeHarmAt`% or more costs every faction whose leader's request isn't finished (finished = braced,
    // safe) strength x `lossPerQuakePercent` % of its starting people. Lost people never come back.
    // Then the meter goes back to the start value plus all the passive rise so far (danger keeps growing).
    void Earthquake()
    {
        news.Add($"Day {day}: EARTHQUAKE! Strength {quakeMeter:0}%.");

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

                bool wasFullHelp = HelpMultiplier(i) == 1f;
                int lost = Mathf.Min(npc.population, Mathf.RoundToInt(definition.startPopulation * lossShare));
                npc.population -= lost;
                string line = $"The {definition.faction} lost {lost} people ({npc.population}/{definition.startPopulation}).";
                if (!npc.Alive)
                {
                    line += $" They are wiped out, and {definition.name} is gone.";
                    if (definition.hasShop) line += " The crystal shop closes, and its upgrades are lost.";
                }
                else if (wasFullHelp && HelpMultiplier(i) < 1f) line += $" Under half left: {definition.name}'s help is halved.";
                news.Add(line);
            }
        }

        quakeMeter = Mathf.Min(100f, config.quakeStart + config.quakePassivePerDay * (day - 1));
        if (GameOver && !Ended)
        {
            news.Add("Every faction is gone. The town has fallen.");
            EndGame(false, "The earthquakes wiped out every faction.");
        }
    }

    // Request day: unfinished requests fail (that NPC's help weakens a tier), then every leader left asks for something new.
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
                npc.tier = Mathf.Max(0, npc.tier - 1);
                news.Add($"{config.npcs[i].name}'s request wasn't met. Their help weakens (tier {npc.tier}).");
            }
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

    void CompleteQuest(int npcIndex)
    {
        NpcState npc = npcs[npcIndex];
        NpcDefinition definition = config.npcs[npcIndex];
        npc.quest = null;
        npc.tier = Mathf.Min(config.maxTier, npc.tier + 1);
        news.Add($"{definition.name}'s request is done! Their help grows (tier {npc.tier}), and the {definition.faction}' homes are braced.");

        if (npc.everCompleted) return;
        npc.everCompleted = true;
        if (definition.unlocksWeapon && !weaponUnlocked[(int)definition.weapon])
        {
            weaponUnlocked[(int)definition.weapon] = true;
            news.Add($"{definition.name} gives you the {Weapon.Names[(int)definition.weapon]}! It can now show up on level-up cards in the dungeon.");
        }
    }

    // ---------- Crystal shop (the Shaman) ----------

    // The NPC who runs the shop (TownConfig "Has Shop"), or -1.
    int ShopOwner
    {
        get
        {
            for (int i = 0; i < config.npcs.Length; i++)
                if (config.npcs[i].hasShop) return i;
            return -1;
        }
    }

    // Open while the shop owner's faction is alive. Once they're wiped out, the shop and its upgrades are gone.
    public bool ShopOpen => ShopOwner >= 0 && npcs[ShopOwner].Alive;

    // Rank N of an upgrade costs N crystals plus N x `materialsPerCrystal` materials.
    public int ShopCrystalCost(int item) => shopRanks[item] + 1;
    public int ShopMaterialCost(int item) => (shopRanks[item] + 1) * config.materialsPerCrystal;

    public bool CanBuy(int item)
    {
        return ShopOpen && shopRanks[item] < config.maxShopRank &&
               crystals >= ShopCrystalCost(item) && materials >= ShopMaterialCost(item);
    }

    public void Buy(int item)
    {
        if (!CanBuy(item)) return;
        crystals -= ShopCrystalCost(item);
        materials -= ShopMaterialCost(item);
        shopRanks[item]++;
        ShopItem shopItem = config.shopItems[item];
        news.Add($"{config.npcs[ShopOwner].name} empowers you: {shopItem.name} rank {shopRanks[item]} " +
                 $"({Player.StatText(shopItem.stat, shopItem.amountPerRank * shopRanks[item])} in total).");
    }

    // ---------- Dive setup ----------

    // Every dive starts with just the Sword at Lv 1 (other unlocked weapons come from level-up cards in the dive,
    // relic weapons are added after this by Relics.ResetForNewRun),
    // plus each NPC's help (halved if their faction is below half, none if wiped out) and the crystal shop upgrades
    // (only while the shop is open). Call after Player.ResetForNewRun.
    public void ApplyLoadout(Player player, Weapon[] weapons)
    {
        foreach (Weapon weapon in weapons)
        {
            weapon.level = 1;
            weapon.enabled = weapon.id == WeaponId.Sword;
        }

        for (int i = 0; i < npcs.Length; i++)
        {
            float help = HelpMultiplier(i);
            if (help > 0f && npcs[i].tier > 0)
                player.AddStat(config.npcs[i].stat, config.npcs[i].amountPerTier * npcs[i].tier * help);
        }

        if (!ShopOpen) return;
        for (int i = 0; i < shopRanks.Length; i++)
            if (shopRanks[i] > 0)
                player.AddStat(config.shopItems[i].stat, config.shopItems[i].amountPerRank * shopRanks[i]);
    }
}
