using UnityEngine;

[System.Serializable]
public class NpcDefinition
{
    public string name = "NPC";
    [Tooltip("The group this NPC leads (e.g. Soldiers, Mages, Peasants).")]
    public string faction = "Faction";
    [Tooltip("The faction's people at the start. Lost people never come back: below half = help halved, 0 = wiped out.")]
    public int startPopulation = 100;
    [Tooltip("The hero stat this NPC's help raises.")]
    public StatType stat;
    [Tooltip("Per tier. Max HP: flat points. Everything else: a fraction (0.05 = +5%).")]
    public float amountPerTier;
    [Tooltip("Their first completed request unlocks this weapon (it can then appear on level-up cards in the dungeon).")]
    public bool unlocksWeapon;
    public WeaponId weapon;
    [Tooltip("Runs the crystal shop (the Shaman). If this faction is wiped out, the shop closes and its upgrades are lost.")]
    public bool hasShop;
}

[System.Serializable]
public class ShopItem
{
    public string name;
    public StatType stat;
    [Tooltip("Per rank, in Player.AddStat units (Max HP: flat; the rest: 0.1 = +10%).")]
    public float amountPerRank;
}

[System.Serializable]
public class KillQuestOption
{
    public EnemyData enemy;
    public int count;
}

// All the town's rules and numbers in one asset (used by both the Town and the Arena scenes).
[CreateAssetMenu(menuName = "Jam/Town Config")]
public class TownConfig : ScriptableObject
{
    [Tooltip("Every NPC leads a faction (see NpcDefinition).")]
    public NpcDefinition[] npcs;

    [Header("Factions")]
    [Tooltip("Below this share of its starting people, a faction's help is halved.")]
    [Range(0f, 1f)] public float halfHelpBelow = 0.5f;

    [Header("Requests (quests)")]
    [Tooltip("Every NPC gets a new request on day 1 and then every this many days. The deadline is the next request day.")]
    public int questInterval = 3;
    public int maxTier = 5;
    public KillQuestOption[] killQuests;
    [Tooltip("Chance (0-1) that a new request is 'bring materials' instead of a kill request.")]
    [Range(0f, 1f)] public float materialQuestChance = 0.25f;
    public int materialQuestAmount = 15;

    [Header("Crystal shop (run by the NPC with Has Shop)")]
    public ShopItem[] shopItems =
    {
        new ShopItem { name = "Max HP", stat = StatType.MaxHp, amountPerRank = 10f },
        new ShopItem { name = "Magic damage", stat = StatType.MagicDamage, amountPerRank = 0.1f },
        new ShopItem { name = "Attack speed", stat = StatType.AttackSpeed, amountPerRank = 0.05f },
        new ShopItem { name = "Pickup range", stat = StatType.PickupRange, amountPerRank = 0.25f },
    };
    public int maxShopRank = 5;
    [Tooltip("Rank N of an upgrade costs N crystals plus N x this many materials.")]
    public int materialsPerCrystal = 5;

    [Header("Days")]
    [Tooltip("Days that pass when the hero dies in the dungeon (a normal dive = 1 day).")]
    public int daysLostOnDefeat = 1;
    public int[] earthquakeDays = { 4, 7, 10, 13 };
    public int hordeDay = 15;

    [Header("Earthquakes (meter in %)")]
    [Tooltip("Meter at the start of the game; after a quake it goes back to this plus all the passive rise so far.")]
    public float quakeStart = 30f;
    [Tooltip("Daily rise that is undone by each quake.")]
    public float quakePerDay = 10f;
    [Tooltip("Daily rise that never goes away: every quake is worse than the last unless you mine ores.")]
    public float quakePassivePerDay = 5f;
    [Tooltip("Meter removed per ore mined (only if the hero gets home safely).")]
    public float orePercent = 4f;
    [Tooltip("Quakes weaker than this cost no one.")]
    public float quakeHarmAt = 40f;
    [Tooltip("Share of an unbraced faction's starting people lost per 1% of quake strength (0.2: a 75% quake costs 15%).")]
    public float lossPerQuakePercent = 0.2f;

    public Quest NewQuest()
    {
        if (killQuests.Length == 0 || Random.value < materialQuestChance)
            return new Quest { needed = materialQuestAmount };

        KillQuestOption option = killQuests[Random.Range(0, killQuests.Length)];
        return new Quest { target = option.enemy, needed = option.count };
    }
}
