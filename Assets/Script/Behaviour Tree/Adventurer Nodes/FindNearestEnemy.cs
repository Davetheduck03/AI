using UnityEngine;

/// <summary>
/// ACTION: Finds the nearest enemy (tagged "Enemy") and sets it in Blackboard.
/// Now notifies KnightAI when enemy is found (for combat priority).
/// Returns Success if found, Failure if none.
/// </summary>
public class FindNearestEnemy : Node
{
    private float maxDetectionRange = 20f;

    public FindNearestEnemy(Blackboard bb, float range = 20f) : base(bb)
    {
        maxDetectionRange = range;
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

            // Notify KnightAI that we found an enemy (triggers combat mode)
            KnightAI knight = self.GetComponent<KnightAI>();
            if (knight != null)
            {
                knight.NotifyEnemyFound();
            }

            Debug.Log($"[FindNearestEnemy] Found {nearest.name} at distance {closestDist:F1}");
            return NodeState.Success;
        }

        return NodeState.Failure;
    }
}