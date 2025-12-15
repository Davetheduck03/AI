using UnityEngine;

/// <summary>
/// Unit vision with Field of View support.
/// Can reveal fog in a cone/sector instead of full circle.
/// </summary>
public class UnitVision : MonoBehaviour
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 5f;
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField] private LayerMask obstacleMask;  // Layers that block vision (e.g., walls)

    [Header("Field of View")]
    [SerializeField] private bool useFOV = true;
    [SerializeField] private float fovAngle = 90f;  // 90 = quarter circle, 180 = half circle, 360 = full circle
    [SerializeField] private bool faceMovementDirection = true;  // Auto-rotate to face movement

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
                RevealFogInFOV();
            }
            else
            {
                // Original circular vision
                fogManager.RevealFogAroundPosition(transform.position);
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
    /// Reveal fog only within the field of view cone.
    /// </summary>
    private void RevealFogInFOV()
    {
        Vector3 position = transform.position;

        // Use FogOfWarManager's method but only for tiles in FOV
        // We need a custom reveal method
        RevealFogInCone(position, facingDirection, visionRange, fovAngle);
    }

    /// <summary>
    /// Custom fog reveal that checks if tiles are within FOV cone.
    /// </summary>
    private void RevealFogInCone(Vector3 center, Vector2 direction, float range, float angle)
    {
        // Get all positions that could be visible
        int radius = Mathf.CeilToInt(range);

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector2 offset = new Vector2(x, y);
                Vector3 checkPos = center + new Vector3(offset.x, offset.y, 0);

                float distance = offset.magnitude;
                if (distance > range) continue;  // Outside range

                // Check if within FOV angle
                if (!IsInFOV(offset, direction, angle)) continue;

                // Check line of sight (walls block vision)
                if (!VisionUtilities.HasLineOfSight(center, checkPos, obstacleMask)) continue;

                // Reveal this tile via FogOfWarManager
                fogManager.RevealFogAroundPosition(checkPos);
            }
        }
    }

    /// <summary>
    /// Check if a position offset is within the FOV cone.
    /// </summary>
    private bool IsInFOV(Vector2 offset, Vector2 direction, float angle)
    {
        if (offset.magnitude < 0.01f) return true;  // Always see own tile

        float angleToPoint = Vector2.Angle(direction, offset);
        return angleToPoint <= angle / 2f;
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
    /// Force immediate fog update.
    /// </summary>
    public void ForceUpdateFog()
    {
        if (fogManager != null)
        {
            if (useFOV)
            {
                RevealFogInFOV();
            }
            else
            {
                fogManager.RevealFogAroundPosition(transform.position);
            }
        }
    }

    /// <summary>
    /// Draw FOV cone in Scene view for debugging.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || !useFOV) return;

        Vector3 position = transform.position;

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
}