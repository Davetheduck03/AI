using UnityEngine;

/// <summary>
/// ACTION: Keeps this hero in its designated formation slot behind the party leader.
///
/// Designed to sit just before the Explore sequence in the BT priority list:
///
///   Priority N  : Follow Leader  ← this node (followers only)
///   Priority N+1: Explore        ← used by the leader (and as fallback if leader dies)
///
/// Behaviour
/// ─────────
/// • If this hero IS the leader  → returns Failure  (falls through to Explore).
/// • If no leader is found       → returns Failure  (hero explores independently).
/// • If in formation position    → stops moving, returns Running (stays in sequence).
/// • If out of position          → paths toward the formation slot, returns Running.
/// • If stuck for too long while far from slot → paths directly to leader, returns Running.
/// </summary>
public class FollowLeader : Node
{
    private readonly float _stopRange;

    // Reusable hidden GO used as the A* movement destination.
    private GameObject _targetGO;

    // ── Refresh throttling ────────────────────────────────────────────────────
    // Re-path interval is dynamic: short when far behind, normal when close.
    // This keeps followers tight without hammering A* every frame when in position.
    private Vector3 _lastPathedTo         = new Vector3(float.MaxValue, 0f, 0f);
    private float   _nextMoveCheck        = 0f;
    private const float MoveCheckNear     = 1.0f;  // normal follow: re-check every 1 s
    private const float MoveCheckFar      = 0.4f;  // catch-up mode: re-check every 0.4 s
    private const float SlotDriftThreshold = 0.35f; // retrigger when slot shifts this far

    // Prevent spamming StopAllCoroutines every tick while "in position".
    private bool _arrivedAtSlot = false;

    // Used to detect the frame a path finishes while the hero is still out of
    // position (path ended early, wall repath, or SeparationBehavior stop).
    // When that transition is detected we immediately shorten _nextMoveCheck so
    // recovery re-triggers within 0.25 s instead of waiting up to MoveCheckNear.
    private bool _wasFollowingPath = false;

    // Preemption detection — mirrors the pattern used by MoveTowardsTarget.
    // When a higher-priority sequence (combat, loot, item pick-up) was running
    // last tick, FollowLeader was not evaluated, the path was likely stopped by
    // that sequence's cleanup, and we must retrigger immediately on the first
    // tick back rather than waiting up to MoveCheckNear (1 s) for the recovery timer.
    private const float PreemptionGap = 0.15f;
    private float _lastEvaluateTime = float.MinValue;

    // If snapping the formation slot to the nearest walkable tile moves it more than
    // this distance, the slot is inside a wall. Switch to the corridor fallback instead.
    private const float WallSnapThreshold = 1.5f;

    // ── Stuck detection ───────────────────────────────────────────────────────
    private float   _nextStuckCheck    = 0f;
    private Vector3 _lastStuckPos      = Vector3.zero;
    private const float StuckCheckInterval  = 2f;   // was 3 s
    private const float StuckMoveThreshold  = 0.3f; // was 0.4

    // ── Catch-up & leash ─────────────────────────────────────────────────────
    // CatchUpDist  — hero is this far from its slot → boost speed + fast refresh.
    // LeashMaxDist — hero is this far from the LEADER → ignore slot, path straight
    //                to the leader at full catch-up speed.  Prevents heroes from
    //                drifting an entire room away during/after combat.
    private const float CatchUpDist    = 1.8f;
    private const float CatchUpSpeed   = 1.55f;  // speed multiplier in catch-up mode (bumped for tighter group)
    private const float LeashMaxDist   = 3.5f;   // hard-leash: path to leader, not slot (was 5f — catch stragglers sooner)

    // ── Combat formation compression ──────────────────────────────────────────
    // When the leader has an active combat target the normal traversal slot
    // (up to 2.8 units back) keeps followers too far to engage.  Compress
    // everyone forward to just behind the leader so their own attack/assist
    // sequences can fire and ranged units reach their firing range.
    private const float CombatFollowDist = 1.2f;   // world units directly behind leader

    public FollowLeader(Blackboard bb, float stopRange = 0.7f) : base(bb)
    {
        _stopRange = stopRange;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        FormationManager fm = FormationManager.Instance;
        if (fm == null) return NodeState.Failure;

        // Leaders don't follow — they lead.  Return Failure so their BT
        // falls through to the Explore sequence.
        if (fm.IsLeader(self)) return NodeState.Failure;

        Transform leader = fm.GetLeader();
        if (leader == null) return NodeState.Failure;  // no leader → explore freely

        // ── Yield if ahead of the leader ─────────────────────────────────────
        // When the leader changes direction the slot flips to the other side.
        // A follower that is currently AHEAD of the leader in its facing direction
        // would have to path straight through the leader to reach the new slot,
        // physically blocking it in narrow corridors. Instead, stop and wait for
        // the leader to walk past, then resume normal following from behind.
        //
        // IMPORTANT: only yield when the leader is actively following a path.
        // After combat ends the leader is briefly stationary while computing the
        // next fog cluster.  Followers that were in combat-compression position
        // (behind the leader toward the enemy) can appear "ahead" of the leader in
        // its new exploration direction before the leader starts moving — gating on
        // IsFollowingPath prevents those followers from being frozen during that
        // transition window.
        //
        // Only apply this when close to the leader — if the follower has fallen
        // far behind, the leader's facing direction (e.g. toward an enemy that is
        // also behind the leader) can incorrectly classify the follower as "ahead"
        // and freeze it permanently.
        Vector3 leaderFwd      = fm.GetLeaderForward();
        float   forwardOffset  = Vector3.Dot(self.position - leader.position, leaderFwd);
        float   distToLeader   = Vector3.Distance(self.position, leader.position);
        bool    leaderMoving   = leader.GetComponent<UnitPathFollower>()?.IsFollowingPath == true;
        if (leaderMoving && forwardOffset > 0.5f && distToLeader < 3f)
        {
            self.GetComponent<UnitPathFollower>()?.StopPath();
            _arrivedAtSlot = false;   // reset so we re-trigger movement once behind
            _lastPathedTo  = new Vector3(float.MaxValue, 0f, 0f);
            return NodeState.Running;
        }

        Vector3 formPos = fm.GetFollowPosition(self);

        // Resolve to a walkable tile. If the snap moves the goal further than
        // WallSnapThreshold the slot is inside a wall (narrow corridor) — use the
        // corridor fallback (directly behind the leader, no lateral offset) instead.
        PathNode resolvedNode = GridGenerator.Instance?.GetNodeAtWorldPosition(formPos);
        if (resolvedNode == null ||
            Vector3.Distance(resolvedNode.transform.position, formPos) > WallSnapThreshold)
        {
            Vector3 fallback = fm.GetFallbackPosition(self);
            resolvedNode = GridGenerator.Instance?.GetNodeAtWorldPosition(fallback)
                        ?? GridGenerator.Instance?.GetNearestWalkableNode(fallback);
            if (resolvedNode != null) formPos = resolvedNode.transform.position;
        }
        else
        {
            formPos = resolvedNode.transform.position;
        }

        // If no walkable tile could be found for this formation slot, path directly
        // to the leader rather than returning Failure.  Returning Failure here drops
        // the hero into the Explore sequence, which sends them to an independent fog
        // cluster in a distant room — they then exhaust local fog and stop completely.
        // A follower should ALWAYS return Running while the leader is alive.
        if (resolvedNode == null)
        {
            if (_targetGO == null)
                _targetGO = new GameObject("_FollowPos") { hideFlags = HideFlags.HideAndDontSave };
            _targetGO.transform.position = leader.position;
            self.GetComponent<MovementComponent>()?.OnTriggerMove(self, _targetGO.transform, CatchUpSpeed);
            _lastPathedTo  = leader.position;
            _nextMoveCheck = Time.time + MoveCheckFar;
            return NodeState.Running;
        }

        // ── Combat formation compression ─────────────────────────────────────
        // When the leader has an active combat target, discard the spread-out
        // traversal slot and advance every follower to just behind the leader.
        // This brings ranged units within their own detection/attack range so
        // their higher-priority attack sequences can engage instead of standing
        // idle in a slot designed for dungeon traversal, not group combat.
        Transform combatTarget = TeamBlackboard.Instance?.Get<Transform>("leaderCombatTarget");
        if (combatTarget != null && combatTarget.gameObject != null)
        {
            Vector3  combatPos  = leader.position + (-leaderFwd) * CombatFollowDist;
            PathNode combatNode = GridGenerator.Instance?.GetNearestWalkableNode(combatPos);
            if (combatNode != null)
            {
                formPos      = combatNode.transform.position;
                resolvedNode = combatNode;
            }
        }

        // Preemption: was FollowLeader skipped last tick (e.g. combat was running)?
        // If so the path was likely stopped externally — retrigger immediately on
        // the first tick back, without waiting for the _nextMoveCheck recovery timer.
        bool wasPreempted = Time.time - _lastEvaluateTime > PreemptionGap;
        _lastEvaluateTime = Time.time;

        float distToSlot = Vector3.Distance(self.position, formPos);
        // distToLeader was already computed above for the yield-if-ahead check.

        // ── Hard leash — hero is too far from the leader ─────────────────────
        // Skip formation slot entirely and path straight to the leader at catch-up
        // speed. Only re-trigger A* when not already running a path toward the leader.
        if (distToLeader > LeashMaxDist)
        {
            var   pfLeash      = self.GetComponent<UnitPathFollower>();
            bool  leashRunning = pfLeash != null && pfLeash.IsFollowingPath;
            float leashShift   = Vector3.Distance(leader.position, _lastPathedTo);

            if (!leashRunning || leashShift > 1.5f)
            {
                if (_targetGO == null)
                    _targetGO = new GameObject("_FollowPos") { hideFlags = HideFlags.HideAndDontSave };
                _targetGO.transform.position = leader.position;
                self.GetComponent<MovementComponent>()?.OnTriggerMove(self, _targetGO.transform, CatchUpSpeed);
                _lastPathedTo = leader.position;
                _nextMoveCheck = Time.time + MoveCheckFar;
            }
            _arrivedAtSlot = false;
            return NodeState.Running;
        }

        // ── Stuck detection ──────────────────────────────────────────────────
        if (Time.time >= _nextStuckCheck)
        {
            float moved = Vector3.Distance(self.position, _lastStuckPos);
            _lastStuckPos   = self.position;
            _nextStuckCheck = Time.time + StuckCheckInterval;

            // Stuck AND significantly out of position → path directly to the leader.
            if (moved < StuckMoveThreshold && distToSlot > _stopRange * 3f)
            {
                Debug.Log($"[FollowLeader] {self.name} stuck far from slot — pathing directly to leader.");
                if (leader != null)
                {
                    if (_targetGO == null)
                        _targetGO = new GameObject("_FollowPos") { hideFlags = HideFlags.HideAndDontSave };
                    _targetGO.transform.position = leader.position;
                    // Force a fresh path — hero is genuinely stuck so we must restart.
                    self.GetComponent<MovementComponent>()?.OnTriggerMove(self, _targetGO.transform, CatchUpSpeed);
                    _lastPathedTo  = leader.position;
                    _nextMoveCheck = Time.time + MoveCheckFar;
                }
            }
        }

        // ── Already in position ──────────────────────────────────────────────
        if (distToSlot <= _stopRange)
        {
            if (!_arrivedAtSlot)
            {
                _arrivedAtSlot = true;
                self.GetComponent<UnitPathFollower>()?.StopPath();
            }
            return NodeState.Running;
        }
        _arrivedAtSlot = false;

        // ── Move toward formation slot ───────────────────────────────────────
        // Catch-up mode: hero is CatchUpDist+ from slot → boost speed.
        bool  catchingUp = distToSlot > CatchUpDist;
        float speedMult  = catchingUp ? CatchUpSpeed : 1f;

        // Decide whether a new A* call is needed.
        //
        // Key rule: if a path is ALREADY RUNNING and the slot hasn't moved far
        // from where we last pathed to, let the coroutine finish — restarting it
        // every 0.4 s is exactly what makes movement look rigid and choppy.
        //
        // We only restart when:
        //   a) No path is currently running (hero finished or path was aborted).
        //   b) The slot has shifted significantly while catching up (>1.5 units),
        //      meaning finishing the current path would leave the hero off-target.
        //   c) Recovery timer fired AND hero isn't moving at all (silent failure).
        var    pf             = self.GetComponent<UnitPathFollower>();
        bool   pathRunning    = pf != null && pf.IsFollowingPath;
        float  slotShift      = Vector3.Distance(formPos, _lastPathedTo);
        bool   slotMovedFar   = slotShift > (pathRunning ? 1.5f : SlotDriftThreshold);
        bool   recoveryNeeded = !pathRunning && distToSlot > _stopRange && Time.time >= _nextMoveCheck;

        // Detect the exact frame a path finishes while the hero is still out of
        // position (e.g. path arrived at wrong tile after a wall repath, or
        // SeparationBehavior called StopPath mid-journey).  On that frame, clamp
        // _nextMoveCheck to "now + 0.25 s" so recovery fires quickly instead of
        // waiting up to the full MoveCheckNear (1 s) that was set when the path
        // was first triggered.
        bool pathJustFinished = _wasFollowingPath && !pathRunning;
        _wasFollowingPath = pathRunning;
        if (pathJustFinished && distToSlot > _stopRange)
            _nextMoveCheck = Mathf.Min(_nextMoveCheck, Time.time + 0.25f);

        if (slotMovedFar || recoveryNeeded || wasPreempted)
        {
            _nextMoveCheck = Time.time + (catchingUp ? MoveCheckFar : MoveCheckNear);
            _lastPathedTo  = formPos;

            if (_targetGO == null)
                _targetGO = new GameObject("_FollowPos") { hideFlags = HideFlags.HideAndDontSave };

            _targetGO.transform.position = formPos;
            self.GetComponent<MovementComponent>()?.OnTriggerMove(self, _targetGO.transform, speedMult);
        }

        return NodeState.Running;
    }
}
