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
    private const float StuckCheckInterval = 2.5f;
    private const float StuckDistanceThreshold = 0.5f;

    // If the target GO is the same object but its position has shifted by more than
    // this amount (e.g. FindFogCluster moved the cached GO to a new cluster),
    // treat it as a new destination and retrigger pathfinding.
    private const float TargetMovedThreshold = 1.5f;

    // If this node was NOT evaluated last tick (because a higher-priority node like
    // combat was running), the path may have been externally stopped.  Retrigger
    // movement immediately when we return so the hero doesn't stand idle.
    // Using last-evaluate-time rather than last-trigger-time is critical: the old
    // 0.5 s trigger-time approach fired every 0.5 s during normal long walks and
    // constantly restarted A*, causing the "2 steps forward, 1 step back" stutter.
    private const float PreemptionGap = 0.15f;  // > 1 frame at 60 fps
    private float _lastEvaluateTime = float.MinValue;

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

        // Detect preemption: was this node skipped last tick because a higher-priority
        // node (e.g. combat) was running?  If so, the path may have been externally
        // stopped and we must retrigger — but ONLY on the first tick back, not every
        // tick during normal exploration (which was the old 0.5 s trigger-time bug).
        bool wasPreempted   = Time.time - _lastEvaluateTime > PreemptionGap;
        _lastEvaluateTime   = Time.time;

        // A "new target" means either a different GO reference, OR the same GO has
        // been repositioned far from where we last pathed (e.g. FindFogCluster moved
        // its cached GO to a completely different cluster centre), OR the node was
        // preempted (combat ran) and the path was externally stopped.
        bool targetMoved    = actualDestination.HasValue &&
                              Vector3.Distance(target.position, actualDestination.Value) > TargetMovedThreshold;
        bool isNewRawTarget = target != lastTarget || targetMoved || wasPreempted;

        if (isNewRawTarget)
        {
            if (wasPreempted && target == lastTarget && !targetMoved)
                Debug.Log($"MoveTowardsTarget: Resuming after preemption — retriggering path to {target.name}");
            else
                Debug.Log($"MoveTowardsTarget: New target - {target.name}");

            // Stop previous movement
            UnitPathFollower pathFollower = self.GetComponent<UnitPathFollower>();
            pathFollower?.StopPath();

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
            self.GetComponent<UnitPathFollower>()?.StopPath();
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
                self.GetComponent<UnitPathFollower>()?.StopPath();
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
        // Clear lastTarget so the next evaluation treats any returning target (even the
        // same cached FogCluster GO) as new and retriggers pathfinding.  Without this,
        // after a stuck detection the hero loops: stuck → Reset → not-new → Running →
        // stuck → Reset forever, because isNewRawTarget never becomes true again.
        lastTarget          = null;
        actualDestination   = null;
        nextStuckCheckTime  = 0f;
        lastCheckedPosition = Vector3.zero;
        // Do NOT reset _lastEvaluateTime here — preemption detection must remain accurate
        // across Reset() calls so the first post-combat retrigger still fires correctly.
    }
}
