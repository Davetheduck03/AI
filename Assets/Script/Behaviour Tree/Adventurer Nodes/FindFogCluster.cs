using UnityEngine;

/// <summary>
/// ACTION: Finds best fog cluster to explore based on size and health.
/// Better for open/corridor maps than rigid room detection.
/// </summary>
public class FindFogCluster : Node
{
    private float maxSearchRange;
    private FogClusterExplorer clusterExplorer;
    private const string TARGET_NAME = "ClusterTarget";

    // Minimum distance the cluster centre must be from the hero.
    // If the target is already within this range MoveTowardsTarget would
    // succeed immediately without the hero moving, so we skip it and let
    // the cluster explorer fall back to a more distant unrevealed tile.
    private const float MinExploreDistance = 2.5f;

    public FindFogCluster(Blackboard bb, float range = 100f) : base(bb)
    {
        maxSearchRange = range;
        clusterExplorer = Object.FindAnyObjectByType<FogClusterExplorer>();

        if (clusterExplorer == null)
        {
            Debug.LogWarning("FindFogCluster: No FogClusterExplorer found in scene!");
        }
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null)
        {
            return NodeState.Failure;
        }

        if (clusterExplorer == null)
        {
            return NodeState.Failure;
        }

        // Clean up any previous exploration target
        CleanupOldTarget();

        // Get current health
        float healthPercent = 1f;
        HealthComponent healthComp = self.GetComponent<HealthComponent>();
        if (healthComp != null)
        {
            healthPercent = healthComp.currentHealth / healthComp.maxHealth;
        }

        // Find best cluster
        Vector3? clusterTarget = clusterExplorer.GetBestExplorationTarget(self.position, healthPercent);

        if (clusterTarget.HasValue)
        {
            float distance = Vector3.Distance(self.position, clusterTarget.Value);

            if (distance < MinExploreDistance)
            {
                // The cluster centre is already on top of us — MoveTowardsTarget would
                // succeed instantly without any movement, creating an infinite tight loop.
                // Returning Failure here lets the BT fall back to other behaviours or
                // simply wait a tick until the hero's position or revealed area changes.
                Debug.Log($"FindFogCluster: Cluster at {clusterTarget.Value} is too close " +
                          $"({distance:F1} < {MinExploreDistance}) — skipping to avoid spin");
                return NodeState.Failure;
            }

            if (distance <= maxSearchRange)
            {
                // Create target
                GameObject targetObj = new GameObject(TARGET_NAME);
                targetObj.transform.position = clusterTarget.Value;

                bb.Set("target", targetObj.transform);

                Debug.Log($"FindFogCluster: Targeting cluster at {clusterTarget.Value} (dist: {distance:F1})");
                return NodeState.Success;
            }
        }

        Debug.Log("FindFogCluster: No suitable clusters found");
        return NodeState.Failure;
    }

    private void CleanupOldTarget()
    {
        Transform oldTarget = bb.Get<Transform>("target");
        if (oldTarget != null && oldTarget.gameObject.name == TARGET_NAME)
        {
            Object.Destroy(oldTarget.gameObject);
        }
    }
}
