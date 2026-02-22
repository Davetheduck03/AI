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
            if (node == null)
                node = GridGenerator.Instance.GetNearestWalkableNode(candidate, maxSearchRadius: 5);

            if (node != null)
                return node;
        }

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