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

        foreach (GameObject lootable in lootables)
        {
            float dist = Vector3.Distance(self.position, lootable.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = lootable.transform;
            }
        }

        if (nearest != null)
        {
            bb.Set("target", nearest);
            return NodeState.Success;
        }
        return NodeState.Failure;
    }
}
