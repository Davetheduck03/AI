using UnityEngine;

/// <summary>
/// ACTION: Moves towards target until in "approach range".
/// Interrupts current movement if a new target is assigned.
/// </summary>
public class MoveTowardsTarget : Node
{
    private float approachRange = 1f;
    private string targetKey = "target";
    private Transform lastTarget = null;
    private Vector3? actualDestination = null;

    // Fail if the hero hasn't moved this far within the check interval.
    // This catches truly unreachable targets (isolated rooms, tiles inside walls)
    // without penalising long-but-valid paths across the full map.
    private const float StuckCheckInterval = 3f;
    private const float StuckDistanceThreshold = 0.5f;
    private float nextStuckCheckTime = 0f;
    private Vector3 lastCheckedPosition = Vector3.zero;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MoveTowardsTarget(Blackboard bb, float range = 3f, string targetKey = "target") : base(bb)
    {
        this.approachRange = range;
        this.targetKey     = targetKey;
    }

    public override NodeState Evaluate()
    {
        Transform self   = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>(targetKey);

        if (self == null || target == null)
        {
            Reset();
            return NodeState.Failure;
        }

        // Check if target is destroyed
        if (target.gameObject == null)
        {
            Debug.Log("MoveTowardsTarget: Target destroyed");
            Reset();
            return NodeState.Failure;
        }

        // ── Decide whether to (re-)trigger movement ───────────────────────────

        bool isNewRawTarget = target != lastTarget;

        if (isNewRawTarget)
        {
            Debug.Log($"MoveTowardsTarget: New target - {target.name}");

            // Stop previous movement
            UnitPathFollower pathFollower = self.GetComponent<UnitPathFollower>();
            pathFollower?.StopAllCoroutines();

            MovementComponent movementComp = self.GetComponent<MovementComponent>();
            if (movementComp == null)
            {
                Debug.LogError("MoveTowardsTarget: No MovementComponent found!");
                return NodeState.Failure;
            }

            // Resolve the actual grid destination
            PathNode goalNode = GridGenerator.Instance.GetNodeAtWorldPosition(target.position);
            if (goalNode == null)
                goalNode = GridGenerator.Instance.GetNearestWalkableNode(target.position);
            actualDestination = goalNode != null ? goalNode.transform.position : target.position;

            movementComp.OnTriggerMove(self, target);

            lastTarget          = target;
            lastCheckedPosition = self.position;
            nextStuckCheckTime  = Time.time + StuckCheckInterval;
        }

        // ── Arrival check ─────────────────────────────────────────────────────

        // For enemies follow their live position; for static goals use the grid-snapped destination.
        bool isEnemy = target.CompareTag("Enemy");
        Vector3 checkPosition = isEnemy
            ? target.position
            : (actualDestination ?? target.position);

        float distance = Vector3.Distance(self.position, checkPosition);

        if (distance <= approachRange)
        {
            Debug.Log($"MoveTowardsTarget: Arrived at {target.name} (dist: {distance:F2})");
            self.GetComponent<UnitPathFollower>()?.StopAllCoroutines();
            Reset();
            return NodeState.Success;
        }

        // ── Stuck detection ───────────────────────────────────────────────────

        // Periodically check if the hero has actually moved. If they haven't
        // covered StuckDistanceThreshold units since the last check, the target
        // is likely unreachable (isolated tile, blocked corridor, inside a wall).
        // Long-but-valid paths across the full map pass this check fine because
        // the hero IS making progress each interval.
        if (Time.time >= nextStuckCheckTime)
        {
            float movedDistance = Vector3.Distance(self.position, lastCheckedPosition);
            if (movedDistance < StuckDistanceThreshold)
            {
                Debug.Log($"MoveTowardsTarget: Hero hasn't moved ({movedDistance:F2} units in " +
                          $"{StuckCheckInterval}s) — target likely unreachable, returning Failure");
                self.GetComponent<UnitPathFollower>()?.StopAllCoroutines();
                Reset();
                return NodeState.Failure;
            }

            lastCheckedPosition = self.position;
            nextStuckCheckTime  = Time.time + StuckCheckInterval;
        }

        return NodeState.Running;
    }

    private void Reset()
    {
        // lastTarget intentionally not cleared — change detection still works
        actualDestination     = null;
        nextStuckCheckTime    = 0f;
        lastCheckedPosition   = Vector3.zero;
    }
}
