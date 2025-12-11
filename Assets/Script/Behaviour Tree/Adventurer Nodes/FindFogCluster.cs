using UnityEngine;

/// <summary>
/// ACTION: Finds best fog cluster to explore based on size and health.
/// Better for open/corridor maps than rigid room detection.
/// </summary>
public class FindFogCluster : Node
{
    private float maxSearchRange;
    private FogClusterExplorer clusterExplorer;

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

        // Clean up old target
        Transform oldTarget = bb.Get<Transform>("target");
        if (oldTarget != null && oldTarget.gameObject.name == "ClusterTarget")
        {
            Object.Destroy(oldTarget.gameObject);
        }

        // Get current health
        float healthPercent = 1f;  // Default to full health
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

            if (distance <= maxSearchRange)
            {
                // Create target
                GameObject targetObj = new GameObject("ClusterTarget");
                targetObj.transform.position = clusterTarget.Value;

                bb.Set("target", targetObj.transform);

                Debug.Log($"FindFogCluster: Targeting cluster at {clusterTarget.Value} (dist: {distance:F1})");
                return NodeState.Success;
            }
        }

        Debug.Log("FindFogCluster: No suitable clusters found");
        return NodeState.Failure;
    }
}