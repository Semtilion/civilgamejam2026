using System.Collections.Generic;

// What happened on one dive, for the town's result screen. The arena fills it in when the run ends
// (ArenaManager.EndDive), Campaign.EndDive adds the quake drop and the requests' progress and keeps it as
// Campaign.lastResult until the town has shown it.
public class RunResult
{
    public bool survived;  // came home
    public int depth;      // the deepest depth reached
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
