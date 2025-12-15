using UnityEngine;

/// <summary>
/// Unit vision with Field of View support.
/// Passes its own vision settings to FogOfWarManager.
/// Can reveal fog in a cone/sector instead of full circle.
/// </summary>
public class UnitVision : MonoBehaviour
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 5f;
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField] private LayerMask visionBlockingLayers;  // Walls that block vision

    [Header("Field of View")]
    [SerializeField] private bool useFOV = false;  // Set true for cone vision
    [SerializeField] private float fovAngle = 90f;  // 90 = quarter circle, 180 = half circle, 360 = full circle
    [SerializeField] private bool faceMovementDirection = true;  // Auto-rotate FOV to face movement

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color visionColor = new Color(1f, 1f, 0f, 0.2f);

    private FogOfWarManager fogManager;
    private float lastUpdateTime;
    private Vector2 lastPosition;
    private Vector2 facingDirection = Vector2.right;  // Default facing direction

    private void Start()
    {
        fogManager = FindAnyObjectByType<FogOfWarManager>();

        if (fogManager == null)
        {
            Debug.LogWarning("UnitVision: No FogOfWarManager found in scene!");
        }

        lastPosition = transform.position;
    }

    private void Update()
    {
        UpdateFacingDirection();

        if (fogManager == null) return;

        if (Time.time - lastUpdateTime >= updateInterval)
        {
            if (useFOV)
            {
                // Reveal fog in cone - passes OUR settings to manager
                fogManager.RevealFogInCone(
                    transform.position,
                    facingDirection,
                    visionRange,
                    fovAngle,
                    visionBlockingLayers
                );
            }
            else
            {
                // Reveal fog in circle - passes OUR settings to manager
                fogManager.RevealFogAroundPosition(
                    transform.position,
                    visionRange,
                    visionBlockingLayers
                );
            }

            lastUpdateTime = Time.time;
        }
    }

    /// <summary>
    /// Update which direction the unit is facing.
    /// </summary>
    private void UpdateFacingDirection()
    {
        if (!faceMovementDirection) return;

        Vector2 currentPos = transform.position;
        Vector2 movement = currentPos - lastPosition;

        // Only update direction if moving
        if (movement.magnitude > 0.01f)
        {
            facingDirection = movement.normalized;
        }

        lastPosition = currentPos;
    }

    /// <summary>
    /// Manually set facing direction (if not using movement direction).
    /// </summary>
    public void SetFacingDirection(Vector2 direction)
    {
        facingDirection = direction.normalized;
    }

    /// <summary>
    /// Get current facing direction.
    /// </summary>
    public Vector2 GetFacingDirection()
    {
        return facingDirection;
    }

    /// <summary>
    /// Force immediate fog update (useful after teleporting).
    /// </summary>
    public void ForceUpdateFog()
    {
        if (fogManager != null)
        {
            if (useFOV)
            {
                fogManager.RevealFogInCone(
                    transform.position,
                    facingDirection,
                    visionRange,
                    fovAngle,
                    visionBlockingLayers
                );
            }
            else
            {
                fogManager.RevealFogAroundPosition(
                    transform.position,
                    visionRange,
                    visionBlockingLayers
                );
            }
        }
    }

    /// <summary>
    /// Draw FOV cone in Scene view for debugging.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying) return;

        Vector3 position = transform.position;

        if (useFOV)
        {
            // Draw FOV cone
            Gizmos.color = visionColor;

            // Calculate cone edges
            float halfAngle = fovAngle / 2f;
            Vector2 leftEdge = RotateVector(facingDirection, -halfAngle);
            Vector2 rightEdge = RotateVector(facingDirection, halfAngle);

            // Draw cone lines
            Gizmos.DrawLine(position, position + (Vector3)(leftEdge * visionRange));
            Gizmos.DrawLine(position, position + (Vector3)(rightEdge * visionRange));
            Gizmos.DrawLine(position, position + (Vector3)(facingDirection * visionRange));

            // Draw arc (approximate with segments)
            int segments = 20;
            Vector3 prevPoint = position + (Vector3)(leftEdge * visionRange);

            for (int i = 1; i <= segments; i++)
            {
                float angle = -halfAngle + (fovAngle * i / segments);
                Vector2 direction = RotateVector(facingDirection, angle);
                Vector3 point = position + (Vector3)(direction * visionRange);

                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }

            // Draw facing direction arrow
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(position, (Vector3)(facingDirection * visionRange));
        }
        else
        {
            // Draw circular vision
            Gizmos.color = visionColor;
            DrawCircle(position, visionRange, 32);
        }
    }

    /// <summary>
    /// Rotate a 2D vector by an angle (in degrees).
    /// </summary>
    private Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        );
    }

    /// <summary>
    /// Draw a circle gizmo.
    /// </summary>
    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0);

            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}