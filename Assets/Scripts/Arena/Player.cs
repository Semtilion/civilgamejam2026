using UnityEngine;
using UnityEngine.InputSystem;

// The hero: moves with the project's "Player/Move" action (WASD, arrow keys, gamepad stick), stays inside the arena,
// has HP and a short invulnerability after each hit. Stats live here so town buffs and relics can change them later.
public class Player : MonoBehaviour
{
    [Header("Stats")]
    public int maxHp = 100;
    public float moveSpeed = 5f;
    [Tooltip("Hit size in world units.")]
    public float radius = 0.5f;
    [Tooltip("Seconds of invulnerability after taking a hit.")]
    public float invulnerableTime = 0.5f;

    [Header("Scene references")]
    public ArenaManager arena;
    public SpriteRenderer sprite;

    public int Hp { get; private set; }
    public bool IsDead => Hp <= 0;

    InputAction moveAction;
    float invulnerableTimer;

    void Awake()
    {
        Hp = maxHp;
        moveAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Move") : null;
        if (moveAction == null) Debug.LogError("No 'Player/Move' action found in the project-wide Input Actions.");
        else moveAction.Enable();
    }

    void Update()
    {
        if (IsDead) return;

        Vector2 input = moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
        Vector2 position = (Vector2)transform.position + input * (moveSpeed * Time.deltaTime);
        Rect bounds = arena.Bounds;
        position.x = Mathf.Clamp(position.x, bounds.xMin + radius, bounds.xMax - radius);
        position.y = Mathf.Clamp(position.y, bounds.yMin + radius, bounds.yMax - radius);
        transform.position = position;

        if (invulnerableTimer > 0f)
        {
            invulnerableTimer -= Time.deltaTime;
            sprite.enabled = invulnerableTimer <= 0f || Mathf.Repeat(invulnerableTimer, 0.1f) > 0.05f; // blink
        }
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || invulnerableTimer > 0f || amount <= 0 || !arena.IsPlaying) return;

        Hp = Mathf.Max(0, Hp - amount);
        invulnerableTimer = invulnerableTime;
        if (IsDead) arena.OnPlayerDied();
    }

    // Full HP, back to the middle of the cave.
    public void ResetForNewRun()
    {
        Hp = maxHp;
        invulnerableTimer = 0f;
        sprite.enabled = true;
        transform.position = arena.Bounds.center;
    }
}
