using UnityEngine;

/// <summary>
/// ACTION: Moves towards target until in "approach range".
/// Interrupts current movement if a new target is assigned.
/// </summary>
public class MoveTowardsTarget : Node
{
    private float approachRange = 1f;
    private Transform lastTarget = null;
    private Vector3? actualDestination = null;

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
            Reset();
            return NodeState.Failure;
        }

        // Check if target is destroyed
        if (target == null || target.gameObject == null)
        {
            Debug.Log("MoveTowardsTarget: Target destroyed");
            Reset();
            return NodeState.Failure;
        }

        // If target changed, start new movement immediately
        if (target != lastTarget)
        {
            Debug.Log($"MoveTowardsTarget: New target - {target.name}");

            // Stop current movement
            UnitPathFollower pathFollower = self.GetComponent<UnitPathFollower>();
            if (pathFollower != null)
            {
                pathFollower.StopAllCoroutines();
            }

            MovementComponent movementComp = self.GetComponent<MovementComponent>();
            if (movementComp == null)
            {
                Debug.LogError("MoveTowardsTarget: No MovementComponent found!");
                return NodeState.Failure;
            }

            // Get actual destination
            PathNode goalNode = GridGenerator.Instance.GetNodeAtWorldPosition(target.position);
            if (goalNode == null)
            {
                goalNode = GridGenerator.Instance.GetNearestWalkableNode(target.position);
            }

            actualDestination = goalNode != null ? goalNode.transform.position : target.position;

            movementComp.OnTriggerMove(self, target);
            lastTarget = target;
        }

        // For enemies, check distance to the enemy itself
        // For static targets, check distance to actual destination
        bool isEnemy = target.CompareTag("Enemy");
        Vector3 checkPosition = isEnemy ? target.position : (actualDestination ?? target.position);
        float distance = Vector3.Distance(self.position, checkPosition);

        if (distance <= approachRange)
        {
            Debug.Log($"MoveTowardsTarget: Arrived at {target.name} (dist: {distance:F2})");

            UnitPathFollower pathFollower = self.GetComponent<UnitPathFollower>();
            if (pathFollower != null)
            {
                pathFollower.StopAllCoroutines();
            }

            Reset();
            return NodeState.Success;
        }

        return NodeState.Running;
    }

    private void Reset()
    {
        // lastTarget = null;  ← REMOVE
        actualDestination = null;
    }
}