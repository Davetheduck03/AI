using UnityEngine;

public class FindLootInRange : Node
{
    private float maxDetectionRange;

    public FindLootInRange(Blackboard blackboard, float maxDetectionRange) : base(blackboard)
    {
        this.maxDetectionRange = maxDetectionRange;

    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        GameObject[] lootables = GameObject.FindGameObjectsWithTag("Lootable");
        Transform nearest = null;
        float closestDist = maxDetectionRange;

        foreach (GameObject lootableObj in lootables)
        {
            if (lootableObj == null) continue;

            Lootable lootable = lootableObj.GetComponent<Lootable>();
            if (lootable == null) continue;

            // Skip chests that are fully looted
            if (lootable.isLooted) continue;

            // Skip chests claimed by a different alive hero
            if (lootable.claimedBy != null &&
                lootable.claimedBy != self &&
                lootable.claimedBy.gameObject != null &&
                lootable.claimedBy.gameObject.activeInHierarchy)
                continue;

            float dist = Vector3.Distance(self.position, lootableObj.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = lootableObj.transform;
            }
        }

        if (nearest != null)
        {
            // Claim it so other heroes route to a different chest
            nearest.GetComponent<Lootable>()?.TryClaim(self);
            bb.Set("target", nearest);
            return NodeState.Success;
        }
        return NodeState.Failure;
    }
}
