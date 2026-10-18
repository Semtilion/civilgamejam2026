using UnityEngine;

// Starting weapon: hits every enemy within the radius around the hero and knocks them back. Bonus: bigger radius.
// Evolution (Twin Strike, available from the start): a back-swing follows every swing, hitting everything again.
// The Quake Hammer relic is a second one of these with bigger, slower numbers (set by the scene builder).
// Look: the sword spins a swirl slash once around the hero (from the way he faces; the back-swing turns the other
// way); the hammer cracks the ground out to its reach (GroundCracks: glowing cracks, dust where they end) and jolts the
// camera.
public class SwordSweep : Weapon
{
    public enum SweepStyle { Spin, Shockwave }

    const float SwingTime = 0.18f;    // the slash's full turn
    const float FlashTime = 0.25f;    // fade
    const float FlashAlpha = 0.8f;
    const float BackSwingDelay = 0.15f; // Twin Strike

    [Header("Sword")]
    public float radius = 2.2f;
    [Tooltip("Extra radius at levels 3 and 5.")]
    public float bonusRadius = 0.4f;
    public float knockback = 6f;
    public SweepStyle style = SweepStyle.Spin;
    [Tooltip("Spin style: the slash (an arc sprite), a child of the hero.")]
    public SpriteRenderer sweepVisual;
    [Tooltip("Shockwave style: the ground crack patterns (one at random per slam; empty = none).")]
    public Sprite[] crackSprites;

    float sinceSweep = 99f;
    float startAngle;
    float turn = -1f;            // -1 = clockwise (the swing), 1 = counter-clockwise (the back-swing)
    float backSwingIn = -1f;     // Twin Strike: seconds until the back-swing; below 0 = none coming

    float Radius => (radius + bonusRadius * BonusCount) * player.areaMultiplier;

    protected override bool Attack()
    {
        Sweep(-1f);
        if (evolved) backSwingIn = BackSwingDelay;
        return true; // always swings, even with nothing around
    }

    // Hits everything in reach and starts the look (`direction`: -1 clockwise, 1 counter-clockwise).
    void Sweep(float direction)
    {
        Vector2 center = transform.position;
        float reachBase = Radius;
        // Killed enemies are only destroyed at the end of the frame, so the list doesn't change during this loop.
        foreach (Enemy enemy in Enemy.All)
        {
            float reach = reachBase + enemy.radius;
            if (((Vector2)enemy.transform.position - center).sqrMagnitude <= reach * reach)
                enemy.TakeDamage(Damage, center, knockback);
        }
        Sfx.Play(Sound.Swing);
        sinceSweep = 0f;
        turn = direction;
        startAngle = player.sprite.flipX ? 180f : 0f; // the slash starts where he faces
        if (style == SweepStyle.Shockwave)
        {
            player.arena.ShakeCamera(0.15f, 0.15f);
            Sfx.Play(Sound.HammerQuake);
            if (crackSprites.Length > 0)
                GroundCracks.Spawn(crackSprites[Random.Range(0, crackSprites.Length)], center, reachBase, player.arena.deathBursts);
        }
    }

    protected override void Update()
    {
        base.Update();
        if (backSwingIn >= 0f && player.arena.IsPlaying)
        {
            backSwingIn -= Time.deltaTime;
            if (backSwingIn < 0f) Sweep(1f);
        }
        sinceSweep += Time.deltaTime;
        if (style != SweepStyle.Spin) return; // the hammer's cracks look after themselves

        // One fast turn with the arc sprite's bright end leading: mirrored (y flipped) for the counter-clockwise
        // back-swing.
        FadeFlash(sweepVisual, sinceSweep, FlashTime, FlashAlpha);
        float swing = Mathf.Clamp01(sinceSweep / SwingTime);
        float size = Radius * 2f; // placeholder sprites are 1 unit wide
        sweepVisual.transform.localScale = new Vector3(size, -turn * size, 1f);
        sweepVisual.transform.localRotation = Quaternion.Euler(0f, 0f, startAngle + turn * 360f * swing);
    }

    void OnDisable()
    {
        if (sweepVisual != null) FadeFlash(sweepVisual, FlashTime, FlashTime, FlashAlpha); // hide
    }
}
