using UnityEngine;

/// <summary>
/// ACTION: Moves towards target until in "approach range" (e.g., attack range + buffer).
/// Returns Running while moving, Success when close.
/// </summary>
public class MoveTowardsTarget : Node
{
    private float speed = 3f;
    private float approachRange = 1f;  // Stop approaching when this close
    private bool hasStartedMoving;

    public MoveTowardsTarget(Blackboard bb, float speed = 3f, float range = 3f) : base(bb)
    {
        this.speed = speed;
        this.approachRange = range;
        hasStartedMoving = false;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");
        if (self == null || target == null)
            return NodeState.Failure;


        if (!hasStartedMoving)
        {
            self.GetComponent<MovementComponent>().OnTriggerMove(self, target);
            hasStartedMoving = true;
        }

        float distance = Vector3.Distance(self.position, target.position);

        // SUCCESS: Already close enough - stop and let next node (e.g., Attack) take over
        if (distance <= approachRange)
        {
            Debug.Log("Reached approach range - Success!");
            return NodeState.Success;
        }

        Debug.Log($"Moving towards enemy (dist: {distance:F1})");
        return NodeState.Running;
    }
}
