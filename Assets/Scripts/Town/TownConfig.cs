using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class NpcDefinition
{
    public string name = "NPC";
    [Tooltip("The group this NPC leads (e.g. Soldiers, Mages, Peasants).")]
    public string faction = "Faction";
    [Tooltip("The faction's people at the start. Lost people never come back: below half = their district works at half " +
             "strength, 0 = wiped out (the district stops).")]
    public int startPopulation = 100;
    [Tooltip("Their first completed request unlocks this weapon's evolution (a card once it reaches Lv 5 in a dive).")]
    [FormerlySerializedAs("unlocksWeapon")] public bool unlocksEvolution;
    public WeaponId weapon;
    [Tooltip("Their bust in the leader window. Jam > Build Game Scenes fills in Assets/Art/NPCs/Portrait_<name>.png when empty.")]
    public Sprite portrait;
    [Tooltip("What they say first in the leader window (shown in quotes).")]
    public string greeting;
    [Tooltip("What they say after you buy a building level in their district or finish their materials request.")]
    public string thanks;
    [Tooltip("Their color: the frame of their button in town, of their window and of their portrait.")]
    public Color color = Color.white;
    [Tooltip("What a day of the Work order makes at full health (scaled by the faction's healthy share, rounded up).")]
    public int workGold, workMaterials, workCrystals;
}

// A faction's job for the day (TownConfig > Preparedness has the numbers). It stays until changed.
public enum TownOrder { Work, Reinforce, Stockpile, Drill }

// What a district building does per level (see Building.amountPerLevel for the units).
public enum BuildingEffect { Stat, QuakePassive, ReinforceBonus, XpGain, MaterialBonus, TreatInjured }

[System.Serializable]
public class Building
{
    public string name;
    [Tooltip("Whose district it's in: index into Npcs (0 Guard, 1 Shaman, 2 Elder).")]
    public int district;
    public BuildingEffect effect;
    [Tooltip("Stat buildings only.")]
    public StatType stat;
    [Tooltip("Per level. Stat: Player.AddStat units (Max HP flat, the rest 0.05 = +5%). QuakePassive / ReinforceBonus: " +
             "% points. XpGain: a share (0.15 = 15%). MaterialBonus: materials added to every drop. TreatInjured: injured " +
             "people treated per day without supplies.")]
    public float amountPerLevel;
    [Tooltip("Also costs crystals (the Shaman's district).")]
    public bool costsCrystals;

    // "+10 max HP", "-1% quake rise/day", ... for `amount` of this building's effect.
    public string EffectText(float amount)
    {
        switch (effect)
        {
            case BuildingEffect.Stat: return Player.StatText(stat, amount);
            case BuildingEffect.QuakePassive: return $"-{amount:0.#}% quake rise/day";
            case BuildingEffect.ReinforceBonus: return $"+{amount:0.#}% per Reinforce day";
            case BuildingEffect.XpGain: return $"+{amount * 100f:0}% XP";
            case BuildingEffect.MaterialBonus: return $"+{amount:0.#} material per drop";
            default: return $"treats {amount:0.#} injured a day"; // TreatInjured
        }
    }
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
    [Tooltip("Below this share of its starting people, a faction's district works at half strength.")]
    [Range(0f, 1f)] public float halfHelpBelow = 0.5f;

    [Header("Requests (quests)")]
    [Tooltip("Every NPC gets a new request on day 1 and then every this many days. The deadline is the next request day.")]
    public int questInterval = 3;
    [Tooltip("Gold paid for every finished request.")]
    public int questGold = 50;
    public KillQuestOption[] killQuests;
    [Tooltip("Chance (0-1) that a new request is 'bring materials' instead of a kill request.")]
    [Range(0f, 1f)] public float materialQuestChance = 0.25f;
    public int materialQuestAmount = 15;

    [Header("Districts (each leader's buildings)")]
    public Building[] buildings =
    {
        new Building { name = "Barracks", district = 0, effect = BuildingEffect.Stat, stat = StatType.MaxHp, amountPerLevel = 10f },
        new Building { name = "Armory", district = 0, effect = BuildingEffect.Stat, stat = StatType.Damage, amountPerLevel = 0.05f },
        new Building { name = "Training Yard", district = 0, effect = BuildingEffect.Stat, stat = StatType.AttackSpeed, amountPerLevel = 0.05f },
        new Building { name = "Ley Stones", district = 1, effect = BuildingEffect.QuakePassive, amountPerLevel = 1f, costsCrystals = true },
        new Building { name = "Scrying Pool", district = 1, effect = BuildingEffect.XpGain, amountPerLevel = 0.15f, costsCrystals = true },
        new Building { name = "Crystal Altar", district = 1, effect = BuildingEffect.Stat, stat = StatType.Area, amountPerLevel = 0.1f, costsCrystals = true },
        new Building { name = "Stone Foundations", district = 2, effect = BuildingEffect.ReinforceBonus, amountPerLevel = 5f },
        new Building { name = "Workshop", district = 2, effect = BuildingEffect.MaterialBonus, amountPerLevel = 1f },
        new Building { name = "Infirmary", district = 2, effect = BuildingEffect.TreatInjured, amountPerLevel = 4f },
    };
    public int maxBuildingLevel = 3;
    [Tooltip("Level N of a building costs N x these (crystals only for buildings that cost crystals).")]
    public int materialsPerLevel = 10;
    public int goldPerLevel = 40;
    public int crystalsPerLevel = 1;

    [Header("Days")]
    [Tooltip("Days that pass when the hero dies in the dungeon (a normal dive = 1 day).")]
    public int daysLostOnDefeat = 1;
    [Tooltip("The first quake hits on this day...")]
    public int firstQuakeDay = 4;
    [Tooltip("...then one every this many days, forever (the game is endless).")]
    public int quakeEvery = 3;
    [Tooltip("Every this-many-th quake brings a raid the same day (2: days 7, 13, 19...).")]
    public int raidEveryQuakes = 2;
    [Tooltip("A lost raid: every faction loses (day x this)% of its starting people, times the share of the raid's " +
             "timer that was left.")]
    public float raidLossPerDay = 1f;

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
    [Tooltip("Share of a faction's starting people hurt per 1% of quake strength, before its preparation (0.2: a 75% " +
             "quake hurts 15%).")]
    public float lossPerQuakePercent = 0.2f;

    [Header("Preparedness (daily orders, at full health; scaled by the faction's healthy share)")]
    [Tooltip("Reinforce: homes reinforced gained per day (% points). A quake's casualties x (1 - reinforced).")]
    public float reinforcePerDay = 15f;
    [Tooltip("Materials a Reinforce day costs. Can't pay: that faction idles.")]
    public int reinforceMaterials = 5;
    [Tooltip("Each quake cracks reinforcement by this share of its strength (0.5: a 75% quake takes 37 points off).")]
    public float reinforceCrack = 0.5f;
    [Tooltip("Stockpile: supplies gained per day, as a share of the faction's starting people (0.1: Peasants 200 -> 20).")]
    public float stockpileShare = 0.1f;
    [Tooltip("Drill: readiness gained per day (% points).")]
    public float drillPerDay = 35f;
    [Tooltip("Readiness lost every day (% points): people forget.")]
    public float readinessFade = 10f;
    [Tooltip("Share of a quake's casualties killed at 0% readiness (the rest are injured).")]
    [Range(0f, 1f)] public float killedUnready = 0.7f;
    [Tooltip("Share of a quake's casualties killed at 100% readiness.")]
    [Range(0f, 1f)] public float killedReady = 0.2f;
    [Tooltip("The injured are treated from the supplies (1 each) right after the quake and every day after; those still " +
             "untreated this many days after the quake die.")]
    public int injuredDays = 2;

    public Quest NewQuest()
    {
        if (killQuests.Length == 0 || Random.value < materialQuestChance)
            return new Quest { needed = materialQuestAmount };

        KillQuestOption option = killQuests[Random.Range(0, killQuests.Length)];
        return new Quest { target = option.enemy, needed = option.count };
    }
}
