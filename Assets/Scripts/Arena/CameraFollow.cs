using UnityEngine;

// Follows the hero smoothly, without showing past the arena walls.
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public ArenaManager arena;
    public float smoothing = 10f;

    Camera cam;

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

        transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }

    // If the arena is smaller than the view on this axis, just center it.
    static float ClampAxis(float value, float min, float max)
    {
        return min <= max ? Mathf.Clamp(value, min, max) : (min + max) / 2f;
    }
}
