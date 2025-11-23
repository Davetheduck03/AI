using UnityEngine;

/// <summary>
/// ACTION: Moves towards target until in "approach range" (e.g., attack range + buffer).
/// Returns Running while moving, Success when close.
/// </summary>
public class MoveTowardsTarget : Node
{
    private float speed = 3f;
    private float approachRange = 3f;  // Stop approaching when this close

    public MoveTowardsTarget(Blackboard bb, float speed = 3f, float range = 3f) : base(bb)
    {
        this.speed = speed;
        this.approachRange = range;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");
        if (self == null || target == null) return NodeState.Failure;

        float dist = Vector3.Distance(self.position, target.position);
        if (dist <= approachRange) return NodeState.Success;

        Vector3 direction = (target.position - self.position).normalized;
        self.position += direction * speed * Time.deltaTime;
        self.LookAt(target);

        return NodeState.Running;
    }
}
