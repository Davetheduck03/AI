using UnityEngine;

/// <summary>
/// Utility class to manage temporary target GameObjects in the Blackboard.
/// Prevents memory leaks from orphaned target objects.
/// </summary>
public static class TargetManager
{
    private const string EXPLORATION_TARGET = "ExplorationTarget";
    private const string CLUSTER_TARGET = "ClusterTarget";
    private const string SPAWN_TARGET = "SpawnTarget";

    /// <summary>
    /// Creates a new target at the specified position, cleaning up any previous temporary target.
    /// </summary>
    public static Transform CreateTarget(Blackboard bb, Vector3 position, string targetName)
    {
        CleanupTarget(bb, targetName);

        GameObject targetObj = new GameObject(targetName);
        targetObj.transform.position = position;

        bb.Set("target", targetObj.transform);
        return targetObj.transform;
    }

    /// <summary>
    /// Cleans up a temporary target if it exists and matches the given name.
    /// </summary>
    public static void CleanupTarget(Blackboard bb, string targetName)
    {
        Transform oldTarget = bb.Get<Transform>("target");
        if (oldTarget != null && oldTarget.gameObject.name == targetName)
        {
            Object.Destroy(oldTarget.gameObject);
        }
    }

    /// <summary>
    /// Cleans up any temporary target (exploration, cluster, or spawn).
    /// </summary>
    public static void CleanupAllTemporaryTargets(Blackboard bb)
    {
        Transform target = bb.Get<Transform>("target");
        if (target == null) return;

        string name = target.gameObject.name;
        if (name == EXPLORATION_TARGET || name == CLUSTER_TARGET || name == SPAWN_TARGET)
        {
            Object.Destroy(target.gameObject);
        }
    }

    /// <summary>
    /// Checks if the current target is a temporary target (not an actual game entity).
    /// </summary>
    public static bool IsTemporaryTarget(Blackboard bb)
    {
        Transform target = bb.Get<Transform>("target");
        if (target == null) return false;

        string name = target.gameObject.name;
        return name == EXPLORATION_TARGET || name == CLUSTER_TARGET || name == SPAWN_TARGET;
    }
}
