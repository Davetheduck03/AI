using UnityEngine;

/// <summary>
/// CONDITION: Checks if a target enemy exists in Blackboard.
/// Instant Success/Failure - pure check.
/// </summary>
public class HasTarget : Node
{
    public HasTarget(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        return bb.Has("target") && bb.Get<Transform>("target") != null
            ? NodeState.Success : NodeState.Failure;
    }
}
