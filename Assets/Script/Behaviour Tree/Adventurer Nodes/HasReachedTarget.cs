using UnityEngine;

/// <summary>
/// CONDITION: Checks if unit has reached the target position.
/// Returns Success if close enough, Failure otherwise.
/// </summary>
public class HasReachedTarget : Node
{
    private float arrivalDistance;

    public HasReachedTarget(Blackboard bb, float distance = 1f) : base(bb)
    {
        arrivalDistance = distance;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");

        if (self == null || target == null) 
            return NodeState.Failure;

        float dist = Vector2.Distance(self.position, target.position);
        
        return dist <= arrivalDistance ? NodeState.Success : NodeState.Failure;
    }
}
