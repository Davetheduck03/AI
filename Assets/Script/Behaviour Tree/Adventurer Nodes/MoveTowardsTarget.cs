using UnityEngine;

/// <summary>
/// ACTION: Moves towards target until in "approach range".
/// Returns Running while moving, Success when close.
/// FIXED: Properly resets state when target changes or sequence restarts.
/// </summary>
public class MoveTowardsTarget : Node
{
    private float approachRange = 1f;
    private Transform lastTarget = null;  // Track if target changed

    public MoveTowardsTarget(Blackboard bb, float range = 3f) : base(bb)
    {
        this.approachRange = range;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");

        if (self == null || target == null)
        {
            Debug.Log("MoveTowardsTarget: Self or target is null");
            lastTarget = null;
            return NodeState.Failure;
        }

        // If target changed, start new movement
        if (target != lastTarget)
        {
            Debug.Log($"MoveTowardsTarget: New target detected - {target.name}");

            MovementComponent movementComp = self.GetComponent<MovementComponent>();
            if (movementComp == null)
            {
                Debug.LogError("MoveTowardsTarget: No MovementComponent found!");
                return NodeState.Failure;
            }

            movementComp.OnTriggerMove(self, target);
            lastTarget = target;
        }

        // Check if we've arrived
        float distance = Vector3.Distance(self.position, target.position);

        if (distance <= approachRange)
        {
            Debug.Log($"MoveTowardsTarget: Arrived! (dist: {distance:F2})");

            // Stop movement
            UnitPathFollower pathFollower = self.GetComponent<UnitPathFollower>();
            if (pathFollower != null)
            {
                pathFollower.StopAllCoroutines();
            }

            lastTarget = null;  // Reset for next time
            return NodeState.Success;
        }

        // Still moving
        Debug.Log($"MoveTowardsTarget: Moving... (dist: {distance:F2})");
        return NodeState.Running;
    }
}