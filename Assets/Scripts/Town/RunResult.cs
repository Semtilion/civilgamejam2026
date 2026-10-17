using System.Collections.Generic;

// What happened on one dive or raid, for the town's result screen. The arena fills it in when the run ends
// (ArenaManager.EndDive), Campaign.EndDive adds the quake drop and the requests' progress and keeps it as
// Campaign.lastResult until the town has shown it.
public class RunResult
{
    public bool raid;      // a raid (else a dive)
    public bool survived;  // came home / held the raid
    public int raidNumber;
    public float secondsHeld, raidLength;
    public float lossPercent; // a lost raid: every faction lost this % of its starting people
    public int depth;         // a dive: the deepest depth reached
    public int depthsCleared;
    public int level;         // in-dive level reached
    public readonly List<string> evolutions = new List<string>();
    public int materials, crystals, gold, supplies, ores;
    public readonly List<RelicId> relics = new List<RelicId>();
    public int totalKills;
    public readonly Dictionary<EnemyData, int> kills = new Dictionary<EnemyData, int>();

    // Filled in by Campaign.EndDive
    public float quakeDrop;
    public readonly List<string> requestLines = new List<string>();
}
