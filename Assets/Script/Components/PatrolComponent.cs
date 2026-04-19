using UnityEngine;

/// <summary>
/// Defines the patrol area and picks random walkable tiles within it.
/// Attach to enemy alongside BasicEnemyAI.
/// </summary>
public class PatrolComponent : MonoBehaviour
{
    [Header("Patrol Settings")]
    [SerializeField] private float patrolRadius = 5f;
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 3f;

    private Vector3 spawnPosition;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    private void Start()
    {
        spawnPosition = transform.position;
    }

    public bool IsWaiting => isWaiting;

    /// <summary>
    /// Call when the enemy arrives at a patrol point.
    /// Starts the wait timer before moving again.
    /// </summary>
    public void StartWait()
    {
        waitTimer = Random.Range(minWaitTime, maxWaitTime);
        isWaiting = true;
    }

    public void TickWait()
    {
        if (!isWaiting) return;
        waitTimer -= Time.deltaTime;
        if (waitTimer <= 0f)
            isWaiting = false;
    }

    /// <summary>
    /// Picks a random walkable tile within patrolRadius of spawn.
    /// Returns null if none found after maxAttempts tries.
    /// </summary>
    public PathNode GetRandomPatrolNode(int maxAttempts = 10)
    {
        var grid = GridGenerator.Instance;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * patrolRadius;
            Vector3 candidate    = spawnPosition + new Vector3(randomOffset.x, randomOffset.y, 0f);

            // Use the direct (silent) lookup only — random candidates frequently land
            // on wall tiles and that is expected.  Calling GetNearestWalkableNode for
            // every miss emits a warning per attempt (up to 10 × 20 Hz = 200/s).
            PathNode node = grid.GetNodeAtWorldPosition(candidate);
            if (node == null || !node.isWalkable) continue;

            // Reject if the found node has drifted outside patrol radius.
            float distFromSpawn = Vector3.Distance(spawnPosition, node.transform.position);
            if (distFromSpawn > patrolRadius) continue;

            return node;
        }

        // Fallback 1: snap to spawn itself with a generous search radius.
        PathNode spawnNode = grid.GetNodeAtWorldPosition(spawnPosition)
                          ?? grid.GetNearestWalkableNode(spawnPosition, maxSearchRadius: 10);
        if (spawnNode != null) return spawnNode;

        // Fallback 2: spawn is truly in an unreachable area (map-edge, disconnected room).
        // Warn once and return null so Patrol can idle rather than spam failures.
        Debug.LogWarning($"[PatrolComponent] {name}: spawn at {spawnPosition} has no " +
                         $"walkable tile within 10 u — enemy will idle at spawn.");
        return null;
    }

    public void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawWireSphere(Application.isPlaying
            ? spawnPosition
            : transform.position, patrolRadius);
    }
}