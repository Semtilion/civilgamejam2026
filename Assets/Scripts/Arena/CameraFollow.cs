using UnityEngine;

// Follows the hero smoothly, without showing past the arena walls. Can shake (a tremor).
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public ArenaManager arena;
    public float smoothing = 10f;

    Camera cam;
    float shakeLeft;
    float shakeTime;
    float shakeStrength;
    Vector3 shakeOffset; // added on top of the follow position, removed again next frame

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Rect area = arena.Bounds;
        // Let the walls show at the edges: one tile at the sides, wall top + face above and below.
        float marginX = ArenaManager.WallThickness;
        float marginY = ArenaManager.WallHeight;

        Vector3 goal = target.position;
        goal.x = ClampAxis(goal.x, area.xMin - marginX + halfWidth, area.xMax + marginX - halfWidth);
        goal.y = ClampAxis(goal.y, area.yMin - marginY + halfHeight, area.yMax + marginY - halfHeight);
        goal.z = transform.position.z;

        Vector3 follow = Vector3.Lerp(transform.position - shakeOffset, goal, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        // Random jolts that fade out; none while the game is paused.
        shakeOffset = Vector3.zero;
        if (shakeLeft > 0f && Time.deltaTime > 0f)
        {
            shakeLeft -= Time.deltaTime;
            shakeOffset = (Vector3)(Random.insideUnitCircle * shakeStrength * Mathf.Clamp01(shakeLeft / shakeTime));
        }
        transform.position = follow + shakeOffset;
    }

    // Shakes the view for `duration` seconds, up to `strength` units off. A weaker shake doesn't cut a stronger one short.
    public void Shake(float duration, float strength)
    {
        if (shakeLeft > 0f && shakeStrength * shakeLeft / shakeTime > strength) return;
        shakeTime = duration;
        shakeLeft = duration;
        shakeStrength = strength;
    }

    // If the arena is smaller than the view on this axis, just center it.
    static float ClampAxis(float value, float min, float max)
    {
        return min <= max ? Mathf.Clamp(value, min, max) : (min + max) / 2f;
    }
}
