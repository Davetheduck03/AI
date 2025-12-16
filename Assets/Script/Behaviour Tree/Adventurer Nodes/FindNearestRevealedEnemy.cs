using UnityEngine;

/// <summary>
/// ACTION: Finds the nearest REVEALED enemy (not in fog).
/// Always re-checks for enemies (doesn't cache).
/// </summary>
public class FindNearestRevealedEnemy : Node
{
    private float maxDetectionRange;
    private FogOfWarManager fogManager;

    public FindNearestRevealedEnemy(Blackboard bb, float range = 20f) : base(bb)
    {
        maxDetectionRange = range;
        fogManager = Object.FindAnyObjectByType<FogOfWarManager>();
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float closestDist = maxDetectionRange;

        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;

            // Skip enemies still in fog
            if (fogManager != null && !fogManager.IsRevealed(enemyObj.transform.position))
            {
                continue;
            }

            float dist = Vector3.Distance(self.position, enemyObj.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = enemyObj.transform;
            }
        }

        if (nearest != null)
        {
            bb.Set("target", nearest);
            Debug.Log($"[FindNearestRevealedEnemy] Targeting {nearest.name} at distance {closestDist:F1}");
            return NodeState.Success;
        }

        return NodeState.Failure;
    }
}