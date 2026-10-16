using UnityEngine;

[System.Serializable]
public class NpcDefinition
{
    public string name = "NPC";
    [Tooltip("The hero stat this NPC's help raises.")]
    public StatType stat;
    [Tooltip("Per tier. Max HP: flat points. Everything else: a fraction (0.05 = +5%).")]
    public float amountPerTier;
    [Tooltip("Their first completed request unlocks this weapon (it can then appear on level-up cards in the dungeon).")]
    public bool unlocksWeapon;
    public WeaponId weapon;
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
    public NpcDefinition[] npcs;

    [Header("Requests (quests)")]
    [Tooltip("Every NPC gets a new request on day 1 and then every this many days. The deadline is the next request day.")]
    public int questInterval = 3;
    public int maxTier = 5;
    public KillQuestOption[] killQuests;
    [Tooltip("Chance (0-1) that a new request is 'bring materials' instead of a kill request.")]
    [Range(0f, 1f)] public float materialQuestChance = 0.25f;
    public int materialQuestAmount = 15;

    [Header("Days")]
    public int startPopulation = 100;
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
    [Tooltip("At this meter or above, a quake breaks the weakest unprotected NPC's building.")]
    public float breakAt = 40f;
    [Tooltip("The same threshold once the town is reinforced.")]
    public float reinforcedBreakAt = 60f;
    public int repairCost = 20;
    public int reinforceCost = 50;

    public Quest NewQuest()
    {
        if (killQuests.Length == 0 || Random.value < materialQuestChance)
            return new Quest { needed = materialQuestAmount };

        KillQuestOption option = killQuests[Random.Range(0, killQuests.Length)];
        return new Quest { target = option.enemy, needed = option.count };
    }
}
