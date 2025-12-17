using UnityEngine;

/// <summary>
/// Utility class for vision-related calculations.
/// Used by FogOfWarManager and AI nodes for line-of-sight checks.
/// </summary>
public static class VisionUtilities
{
    /// <summary>
    /// Checks if there's a clear line of sight between two positions.
    /// </summary>
    /// <param name="from">Starting position</param>
    /// <param name="to">Target position</param>
    /// <param name="blockingLayers">LayerMask for obstacles that block vision</param>
    /// <returns>True if line of sight is clear, false if blocked</returns>
    public static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask blockingLayers)
    {
        Vector2 from2D = (Vector2)from;
        Vector2 to2D = (Vector2)to;
        Vector2 direction = to2D - from2D;
        float distance = direction.magnitude;

        if (distance < 0.01f) return true;

        RaycastHit2D hit = Physics2D.Raycast(from2D, direction.normalized, distance, blockingLayers);

        return hit.collider == null;
    }

    /// <summary>
    /// Checks if there's line of sight with debug visualization.
    /// </summary>
    public static bool HasLineOfSightDebug(Vector3 from, Vector3 to, LayerMask blockingLayers, float debugDuration = 0.1f)
    {
        bool hasLOS = HasLineOfSight(from, to, blockingLayers);

        Debug.DrawLine(from, to, hasLOS ? Color.green : Color.red, debugDuration);

        return hasLOS;
    }

    /// <summary>
    /// Checks if a position is within a cone/sector defined by facing direction and angle.
    /// </summary>
    /// <param name="origin">Position of the viewer</param>
    /// <param name="facingDirection">Direction the viewer is facing</param>
    /// <param name="targetPosition">Position to check</param>
    /// <param name="maxAngle">Half-angle of the cone (e.g., 45 for 90-degree FOV)</param>
    /// <returns>True if target is within the cone</returns>
    public static bool IsInCone(Vector3 origin, Vector2 facingDirection, Vector3 targetPosition, float maxAngle)
    {
        Vector2 toTarget = ((Vector2)targetPosition - (Vector2)origin).normalized;

        if (toTarget.magnitude < 0.01f) return true;

        float angle = Vector2.Angle(facingDirection, toTarget);
        return angle <= maxAngle;
    }
}
