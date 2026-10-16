using System.Collections.Generic;
using UnityEngine;

// Relic weapon: every interval, a lightning bolt strikes a random enemy on screen (never a crate).
// Bonus: +1 bolt (each on a different enemy).
public class LightningStaff : Weapon
{
    [Header("Lightning Staff")]
    public float boltWidth = 0.15f;
    [Tooltip("Plain square, stretched into the bolt.")]
    public Sprite boltSprite;
    [Tooltip("Circle flashed where the bolt hits.")]
    public Sprite impactSprite;
    public Color boltColor = new Color(1f, 0.95f, 0.5f);

    readonly List<Enemy> targets = new List<Enemy>();

    protected override bool Attack()
    {
        targets.Clear();
        foreach (Enemy enemy in Enemy.All)
            if (!enemy.IsDead && enemy.data.behavior != EnemyBehavior.Crate && player.arena.OnScreen(enemy.transform.position))
                targets.Add(enemy);
        if (targets.Count == 0) return false;

        int bolts = 1 + BonusCount;
        for (int i = 0; i < bolts && targets.Count > 0; i++)
        {
            int pick = Random.Range(0, targets.Count);
            Enemy target = targets[pick];
            targets.RemoveAt(pick);

            Vector2 at = target.transform.position;
            FadeEffect.Line(boltSprite, at + Vector2.up * 8f, at, boltWidth, boltColor, 0.15f); // straight down from the sky
            FadeEffect.Circle(impactSprite, at, 0.5f, boltColor, 0.2f);
            target.TakeDamage(Damage, at, 0f);
        }
        return true;
    }
}
