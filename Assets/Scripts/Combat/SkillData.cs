using UnityEngine;

public enum SkillTarget
{
    Single, // one opposing unit within reach
    All,    // every opposing unit within reach
    Self    // the user (heal / restore mana)
}

// A hero skill, an enemy attack, or a usable item (items are skills with a count, see CombatManager.items).
[CreateAssetMenu(menuName = "Jam/Skill")]
public class SkillData : ScriptableObject
{
    public string displayName;
    public SkillTarget target = SkillTarget.Single;

    [Header("Reach: enemy ranks this can hit (hero only)")]
    [Range(1, 4)] public int minRank = 1;
    [Range(1, 4)] public int maxRank = 4;

    [Header("Cost")]
    public int manaCost;

    [Header("Effect")]
    [Tooltip("Damage for Single/All skills, HP healed for Self skills.")]
    public int minAmount;
    public int maxAmount;
    [Tooltip("Mana given back to the user (Self skills).")]
    public int manaRestore;

    [Header("Rolls (Single/All only)")]
    [Tooltip("Hit chance = baseAccuracy + attacker accuracyMod - target dodge.")]
    [Range(0, 100)] public int baseAccuracy = 90;
    [Tooltip("Added to the attacker's crit chance.")]
    [Range(0, 100)] public int critBonus;
}
