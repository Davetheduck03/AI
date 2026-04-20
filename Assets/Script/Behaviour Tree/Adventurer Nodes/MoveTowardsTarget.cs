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
	private const float StuckCheckInterval = 1.5f;
	private const float StuckDistanceThreshold = 0.5f;

	// Minimum time between path-stopped retriggers when the destination is unchanged.
	private const float PathStoppedRetriggerInterval = 0.2f;   // was 0.3 — tighter so heroes don't pause after combat
	private float _nextPathRetriggerTime = 0f;

	// If the target GO is the same object but its position has shifted more than this, retrigger.
	private const float TargetMovedThreshold = 1.5f;

	// If this node was NOT evaluated last tick (preempted by combat / loot), retrigger immediately.
	private const float PreemptionGap = 0.15f;
	private float _lastEvaluateTime = float.MinValue;

	// If the hero has been continuously idle (path stopped, no movement trigger) for longer
	// than this, force a retrigger even when within the PathStoppedRetriggerInterval window.
	// This catches the post-combat freeze where the BT re-enters MoveTowardsTarget but the
	// throttle prevents the first path call from firing for up to 0.3 s.
	private const float MaxIdleBeforeForceRetrigger = 0.25f;
	private float _pathStoppedSince = float.MaxValue;   // time when IsFollowingPath last became false

	private float nextStuckCheckTime = 0f;
	private Vector3 lastCheckedPosition = Vector3.zero;

	// ── Constructor ───────────────────────────────────────────────────────────

	public MoveTowardsTarget(Blackboard bb, float range = 3f, string targetKey = "target") : base(bb)
	{
		this.approachRange = range;
		this.targetKey = targetKey;
	}

	public override NodeState Evaluate()
	{
		Transform self = bb.Get<Transform>("self");
		Transform target = bb.Get<Transform>(targetKey);

		if (self == null || target == null)
		{
			Reset();
			return NodeState.Failure;
		}

		if (target.gameObject == null)
		{
			Debug.Log("MoveTowardsTarget: Target destroyed");
			Reset();
			return NodeState.Failure;
		}

		// ── Arrival check ─────────────────────────────────────────────────────
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

		// ── Preemption / idle tracking ────────────────────────────────────────
		bool wasPreempted = Time.time - _lastEvaluateTime > PreemptionGap;
		_lastEvaluateTime = Time.time;

		var pf = self.GetComponent<UnitPathFollower>();
		bool pathRunning = pf != null && pf.IsFollowingPath;

		// Track how long we've been sitting with no active path.
		if (pathRunning)
		{
			_pathStoppedSince = float.MaxValue;   // reset — path is running
		}
		else if (_pathStoppedSince == float.MaxValue)
		{
			_pathStoppedSince = Time.time;   // path just stopped
		}

		float idleSeconds = pathRunning ? 0f : (Time.time - _pathStoppedSince);

		// ── Decide whether to (re-)trigger movement ───────────────────────────
		bool targetMoved = actualDestination.HasValue &&
						   Vector3.Distance(target.position, actualDestination.Value) > TargetMovedThreshold;

		// KEY: if the target reference changed (e.g. combat ended and loot sequence
		// set a chest as the new target) we must always retrigger, regardless of any
		// throttle.  The old code only checked (target != lastTarget) inside
		// isNewRawTarget, but lastTarget was still set from the previous sequence's
		// target GO, so this comparison could silently pass as false.
		bool targetChanged = target != lastTarget;

		bool pathStopped = lastTarget != null && !pathRunning;
		bool pathStoppedThrottle = pathStopped && Time.time >= _nextPathRetriggerTime;

		// Force retrigger if we've been idle too long — kills the post-combat freeze.
		bool forceRetrigger = !pathRunning && idleSeconds >= MaxIdleBeforeForceRetrigger
							  && lastTarget != null;

		bool isNewRawTarget = targetChanged || targetMoved || wasPreempted
							  || pathStoppedThrottle || forceRetrigger;

		bool genuinelyNewDest = targetChanged || targetMoved || wasPreempted;

		if (isNewRawTarget)
		{
			if (forceRetrigger && !wasPreempted && !targetChanged && !targetMoved)
				Debug.Log($"MoveTowardsTarget: Force-retrigger after {idleSeconds:F2}s idle — {target.name}");
			else if (pathStoppedThrottle && !wasPreempted && !targetChanged && !targetMoved)
				Debug.Log($"MoveTowardsTarget: Path stopped without arrival — retriggering to {target.name}");
			else if (wasPreempted && !targetChanged && !targetMoved)
				Debug.Log($"MoveTowardsTarget: Resuming after preemption — retriggering path to {target.name}");
			else
				Debug.Log($"MoveTowardsTarget: New target - {target.name}");

			pf?.StopPath();

			MovementComponent movementComp = self.GetComponent<MovementComponent>();
			if (movementComp == null)
			{
				Debug.LogError("MoveTowardsTarget: No MovementComponent found!");
				return NodeState.Failure;
			}

			PathNode goalNode = GridGenerator.Instance.GetNodeAtWorldPosition(target.position);
			if (goalNode == null)
				goalNode = GridGenerator.Instance.GetNearestWalkableNode(target.position);
			actualDestination = goalNode != null ? goalNode.transform.position : target.position;

			movementComp.OnTriggerMove(self, target);

			lastTarget = target;
			_pathStoppedSince = float.MaxValue;   // reset — we just triggered a new path

			if (!genuinelyNewDest)
				_nextPathRetriggerTime = Time.time + PathStoppedRetriggerInterval;
			else
				_nextPathRetriggerTime = 0f;

			if (genuinelyNewDest)
			{
				lastCheckedPosition = self.position;
				nextStuckCheckTime = Time.time + StuckCheckInterval;
			}
		}

		// ── Stuck detection ───────────────────────────────────────────────────
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
			nextStuckCheckTime = Time.time + StuckCheckInterval;
		}

		return NodeState.Running;
	}

	private void Reset()
	{
		lastTarget = null;
		actualDestination = null;
		nextStuckCheckTime = 0f;
		lastCheckedPosition = Vector3.zero;
		_nextPathRetriggerTime = 0f;
		_pathStoppedSince = float.MaxValue;
	}
}