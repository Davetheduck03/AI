using UnityEngine;

public static class VisionUtilities
{
    public static bool HasLineOfSight(Vector3 from, Vector3 to, LayerMask blockingLayers)
    {
        Vector2 direction = (to - from).normalized;
        float distance = Vector2.Distance(from, to);
        RaycastHit2D hit = Physics2D.Raycast(from, direction, distance, blockingLayers);
        return hit.collider == null;
    }
}
