using UnityEngine;

// A normal enemy (Crawler, Spitter, Brute) can spawn as a variant: bigger, tinted and outlined in its color, more XP,
// a small chance to drop a relic, and one twist. Blue: tough (more HP, a bit slower). Red: leaves a trail of fire
// patches that hurt the hero (walking enemies only: a Spitter never moves). Purple: explodes a moment after it dies.
public enum EnemyVariant { None, Tough, Burning, Volatile }

// The variant numbers (ArenaManager.variants). Damage scales with depth like every enemy's.
[System.Serializable]
public class VariantSettings
{
    [Tooltip("Chance a normal spawn is a variant: chance + chancePerDepth x (depth - 1), at most maxChance. The 3 kinds are equally likely.")]
    [Range(0f, 1f)] public float chance = 0.04f;
    public float chancePerDepth = 0.01f;
    [Range(0f, 1f)] public float maxChance = 0.15f;
    [Tooltip("Drawn and hit this much bigger.")]
    public float size = 1.25f;
    [Tooltip("Its XP gem is worth this many times the normal one.")]
    public int xpMultiplier = 3;
    [Tooltip("Chance it drops a relic (a pickup that shows a relic card), while there are relics you don't have.")]
    [Range(0f, 1f)] public float relicChance = 0.03f;

    [Header("Blue: tough")]
    public Color toughColor = new Color(0.30f, 0.60f, 1f);
    public float toughHp = 3f;
    public float toughSpeed = 0.85f;

    [Header("Red: a trail of fire (only the hero gets hurt)")]
    public Color burningColor = new Color(1f, 0.30f, 0.15f);
    [Tooltip("Seconds between fire patches.")]
    public float trailInterval = 0.3f;
    [Tooltip("How long each patch burns (seconds).")]
    public float trailLife = 1.5f;
    public float trailRadius = 0.45f;
    [Tooltip("Damage to the hero standing in a patch, every trailTick seconds.")]
    public int trailDamage = 2;
    public float trailTick = 0.5f;

    [Header("Purple: explodes after dying (only the hero gets hurt)")]
    public Color volatileColor = new Color(0.75f, 0.35f, 1f);
    [Tooltip("Seconds from its death to the blast.")]
    public float blastFuse = 2f;
    public float blastRadius = 1.8f;
    public int blastDamage = 12;

    public float ChanceAt(int depth) => Mathf.Min(maxChance, chance + chancePerDepth * (depth - 1));

    public Color ColorOf(EnemyVariant variant)
    {
        switch (variant)
        {
            case EnemyVariant.Tough: return toughColor;
            case EnemyVariant.Burning: return burningColor;
            case EnemyVariant.Volatile: return volatileColor;
            default: return Color.white;
        }
    }
}
