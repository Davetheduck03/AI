using UnityEngine;

/// <summary>
/// ACTION: Moves enemy to a random walkable tile within patrol area.
/// Handles interruption cleanly when higher-priority sequences take over.
/// </summary>
public class Patrol : Node
{
    private const string PATROL_TARGET = "PatrolTarget";

    private PatrolComponent patrolComp;
    private MovementComponent movementComp;
    private bool isMoving = false;
    private Transform currentPatrolTarget = null;

    public Patrol(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        if (patrolComp == null) patrolComp = self.GetComponent<PatrolComponent>();
        if (movementComp == null) movementComp = self.GetComponent<MovementComponent>();

        if (patrolComp == null || movementComp == null)
        {
            Debug.LogWarning("Patrol: Missing PatrolComponent or MovementComponent!");
            return NodeState.Failure;
        }

        // If our patrol target was destroyed externally (enemy interrupted by combat),
        // reset so we pick a fresh destination next evaluation
        if (isMoving && (currentPatrolTarget == null || currentPatrolTarget.gameObject == null))
        {
            isMoving = false;
            currentPatrolTarget = null;
        }

        // Tick wait timer between patrol points
        if (patrolComp.IsWaiting)
        {
            patrolComp.TickWait();
            return NodeState.Running;
        }

        // Check arrival at current patrol point
        if (isMoving && currentPatrolTarget != null)
        {
            float dist = Vector2.Distance(self.position, currentPatrolTarget.position);
            if (dist <= 0.5f)
            {
                CleanupPatrolTarget();
                isMoving = false;
                patrolComp.StartWait();
                return NodeState.Running;
            }

            // Still travelling — keep target on blackboard so MoveTowardsTarget
            // doesn't get confused if something reads it
            return NodeState.Running;
        }

        // Pick a new patrol destination
        PathNode patrolNode = patrolComp.GetRandomPatrolNode();
        if (patrolNode == null)
        {
            Debug.LogWarning("Patrol: No walkable node found in patrol area!");
            return NodeState.Failure;
        }

        // Clean up any old patrol target before creating new one
        CleanupPatrolTarget();

        GameObject targetObj = new GameObject(PATROL_TARGET);
        targetObj.transform.position = patrolNode.transform.position;
        currentPatrolTarget = targetObj.transform;
        bb.Set("target", currentPatrolTarget);

        movementComp.OnTriggerMove(self, currentPatrolTarget);
        isMoving = true;

        Debug.Log($"Patrol: Moving to {patrolNode.name}");
        return NodeState.Running;
    }

    private void CleanupPatrolTarget()
    {
        if (currentPatrolTarget != null && currentPatrolTarget.gameObject != null
            && currentPatrolTarget.gameObject.name == PATROL_TARGET)
        {
            Object.Destroy(currentPatrolTarget.gameObject);
        }
        currentPatrolTarget = null;

        // Also clear from blackboard if it's still pointing to our target
        Transform bbTarget = bb.Get<Transform>("target");
        if (bbTarget != null && bbTarget.gameObject != null
            && bbTarget.gameObject.name == PATROL_TARGET)
        {
            bb.Set<Transform>("target", null);
        }
    }
}