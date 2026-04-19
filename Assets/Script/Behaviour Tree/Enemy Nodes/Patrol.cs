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

    // When no patrol node can be found (e.g. spawn in a map-edge room with no floor
    // nearby), idle for this many seconds before retrying.  Without this, Patrol
    // returns Failure every BT tick (20 Hz), causing BehaviorTreeRunner to spam the
    // BT-fallback path and the console every 50 ms.
    private const float NoNodeRetryDelay = 3f;
    private float _nextRetryTime = 0f;

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
        // Guard: if the last attempt failed, wait before retrying so we don't spam
        // GetRandomPatrolNode (and the console) at 20 Hz when the spawn is in an
        // unreachable area.
        if (Time.time < _nextRetryTime)
            return NodeState.Running;   // idle — will retry when timer elapses

        PathNode patrolNode = patrolComp.GetRandomPatrolNode();
        if (patrolNode == null)
        {
            // PatrolComponent already logged a warning.  Back off before retrying.
            _nextRetryTime = Time.time + NoNodeRetryDelay;
            return NodeState.Running;   // idle rather than cascading Failure every tick
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