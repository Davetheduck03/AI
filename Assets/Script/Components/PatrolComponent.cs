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
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = spawnPosition + new Vector3(randomOffset.x, randomOffset.y, 0f);

            PathNode node = GridGenerator.Instance.GetNodeAtWorldPosition(candidate);

            // Only use GetNearestWalkable as fallback, and validate it's still in radius
            if (node == null)
            {
                node = GridGenerator.Instance.GetNearestWalkableNode(candidate, maxSearchRadius: 3);
            }

            if (node == null) continue;

            // Reject if the found node is outside patrol radius
            float distFromSpawn = Vector3.Distance(spawnPosition, node.transform.position);
            if (distFromSpawn > patrolRadius) continue;

            return node;
        }

        // Fallback: return the node at spawn position itself
        return GridGenerator.Instance.GetNodeAtWorldPosition(spawnPosition)
            ?? GridGenerator.Instance.GetNearestWalkableNode(spawnPosition, maxSearchRadius: 5);
    }

    public void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawWireSphere(Application.isPlaying
            ? spawnPosition
            : transform.position, patrolRadius);
    }
}