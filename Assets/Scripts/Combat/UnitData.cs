using System.Collections.Generic;
using UnityEngine;

// Base stats for the hero or an enemy type.
[CreateAssetMenu(menuName = "Jam/Unit")]
public class UnitData : ScriptableObject
{
    public string displayName;
    [Tooltip("Leave empty to use the placeholder color.")]
    public Sprite sprite;
    public Color placeholderColor = Color.gray;

    [Header("Stats")]
    public int maxHp = 10;
    public int maxMana;
    [Tooltip("Mana gained at the start of each of this unit's turns.")]
    public int manaRegen;
    [Tooltip("ATB gauge fill rate. Seconds to fill = CombatManager.gaugeFillTime / speed.")]
    public int speed = 5;
    [Tooltip("Added to every skill's base accuracy (buffs/debuffs change it).")]
    public int accuracyMod;
    [Range(0, 100)] public int critChance = 5;
    [Range(0, 100)] public int dodge;

    [Header("Skills (hero: first 5 get buttons, enemy: picks one at random)")]
    public List<SkillData> skills = new List<SkillData>();
}
