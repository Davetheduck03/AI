using UnityEngine;

/// <summary>
/// CONDITION: Checks if target is within attack range.
/// </summary>
public class IsInAttackRange : Node
{
    private float attackRange = 2f;

    public IsInAttackRange(Blackboard bb, float range = 2f) : base(bb)
    {
        attackRange = range;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");
        if (self == null || target == null) return NodeState.Failure;

        return Vector3.Distance(self.position, target.position) <= attackRange
            ? NodeState.Success : NodeState.Failure;
    }
}
