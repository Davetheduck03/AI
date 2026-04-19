using UnityEngine;

/// <summary>
/// ACTION: Keeps this hero close to the party leader.
///
/// Simple radius-based following — no fixed formation slots.
/// Followers path directly to the leader and stop when within StopRange.
/// SeparationBehavior naturally spreads the cluster so heroes don't overlap.
///
/// Returns Failure for the leader (falls through to Explore).
/// Returns Running for all followers while the leader is alive.
/// </summary>
public class FollowLeader : Node
{
    // Hero stops moving when within this distance of the leader.
    // SeparationBehavior (radius 1 u) will spread overlapping heroes apart;
    // keep StopRange comfortably above the separation radius so heroes that
    // are pushed slightly outward don't immediately retrigger A*.
    private const float StopRange    = 2.0f;

    // Hysteresis: once a hero has arrived (distToLeader <= StopRange), don't
    // retrigger movement until they've drifted past this larger threshold.
    // SeparationBehavior can push a stopped hero at ~5 u/s, so ResumeRange
    // must be larger than StopRange + the maximum push in one BT tick (≈0.5 u).
    // Setting it to 3.0 u gives a comfortable margin and prevents the
    // arrive → separation-push → retrigger → arrive oscillation loop.
    private const float ResumeRange  = 3.0f;

    // Past this distance from the leader apply a speed boost.
    private const float CatchUpDist  = 3.5f;
    private const float CatchUpSpeed = 1.55f;

    // Minimum time between new A* calls while following (seconds).
    // Short interval when catching up so the hero tracks the leader closely;
    // longer interval when already nearby to avoid hammering pathfinding.
    private const float MoveCheckNear = 0.8f;
    private const float MoveCheckFar  = 0.35f;

    // If this node wasn't evaluated for longer than PreemptionGap (because a
    // higher-priority sequence like combat was running), the path was likely
    // stopped externally — retrigger immediately on the first tick back.
    private const float PreemptionGap = 0.15f;

    // Stuck detection — if the hero hasn't moved StuckMoveThreshold units in
    // StuckCheckInterval seconds while out of range, force a fresh repath.
    private const float StuckCheckInterval = 2.5f;
    private const float StuckMoveThreshold = 0.3f;

    // ── State ─────────────────────────────────────────────────────────────────

    private GameObject _targetGO;
    private Vector3    _lastPathedTo     = new Vector3(float.MaxValue, 0f, 0f);
    private float      _nextMoveCheck    = 0f;
    private bool       _arrivedAtLeader  = false;
    private bool       _wasFollowingPath = false;
    private float      _lastEvaluateTime = float.MinValue;
    private float      _nextStuckCheck   = 0f;
    private Vector3    _lastStuckPos     = Vector3.zero;

    public FollowLeader(Blackboard bb) : base(bb) { }

    /// <summary>
    /// Call this from the hero's OnDestroy so the hidden helper GO is cleaned up.
    /// Node is a plain C# class with no Unity lifecycle, so the owner MonoBehaviour
    /// must relay the destroy event here.
    /// </summary>
    public void Cleanup()
    {
        if (_targetGO != null)
        {
            UnityEngine.Object.Destroy(_targetGO);
            _targetGO = null;
        }
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        FormationManager fm = FormationManager.Instance;
        if (fm == null) return NodeState.Failure;

        // Leaders explore on their own — fall through to the Explore sequence.
        if (fm.IsLeader(self)) return NodeState.Failure;

        Transform leader = fm.GetLeader();
        if (leader == null) return NodeState.Failure;

        float distToLeader = Vector3.Distance(self.position, leader.position);

        // ── Already close enough ─────────────────────────────────────────────
        if (distToLeader <= StopRange)
        {
            if (!_arrivedAtLeader)
            {
                _arrivedAtLeader = true;
                self.GetComponent<UnitPathFollower>()?.StopPath();
            }
            _lastEvaluateTime = Time.time;
            return NodeState.Running;
        }

        // ── Hysteresis dead-band ──────────────────────────────────────────────
        // Once arrived, stay "arrived" until the hero drifts past ResumeRange.
        // Without this, SeparationBehavior pushing the hero a fraction past
        // StopRange triggers a new A* call every tick, causing the
        // arrive → push → repath oscillation loop.
        if (_arrivedAtLeader && distToLeader <= ResumeRange)
        {
            _lastEvaluateTime = Time.time;
            return NodeState.Running;
        }

        _arrivedAtLeader = false;

        // ── Preemption detection ─────────────────────────────────────────────
        bool wasPreempted = Time.time - _lastEvaluateTime > PreemptionGap;
        _lastEvaluateTime = Time.time;

        // ── Stuck detection ──────────────────────────────────────────────────
        if (Time.time >= _nextStuckCheck)
        {
            float moved     = Vector3.Distance(self.position, _lastStuckPos);
            _lastStuckPos   = self.position;
            _nextStuckCheck = Time.time + StuckCheckInterval;

            if (moved < StuckMoveThreshold && distToLeader > StopRange * 1.5f)
            {
                // Hero hasn't moved while out of range — force an immediate repath.
                _nextMoveCheck = 0f;
                Debug.Log($"[FollowLeader] {self.name} stuck — forcing repath to leader.");
            }
        }

        // ── Path state ───────────────────────────────────────────────────────
        var  pf          = self.GetComponent<UnitPathFollower>();
        bool pathRunning = pf != null && pf.IsFollowingPath;

        // If a path just finished but we're still out of range (e.g. the goal
        // tile was occupied, or SeparationBehavior stopped us mid-journey),
        // clamp the recovery timer to 0.25 s so we don't wait the full interval.
        bool pathJustFinished = _wasFollowingPath && !pathRunning;
        _wasFollowingPath = pathRunning;
        if (pathJustFinished && distToLeader > StopRange)
            _nextMoveCheck = Mathf.Min(_nextMoveCheck, Time.time + 0.25f);

        // ── Decide whether to issue a new path ───────────────────────────────
        // Retrigger when:
        //   a) Leader has moved significantly from where we last aimed  (leaderMoved)
        //   b) No path is running and the recovery timer has elapsed    (needRecovery)
        //   c) We just returned from a preempting high-priority node    (wasPreempted)
        float leaderShift  = Vector3.Distance(leader.position, _lastPathedTo);
        // When the path is running, use a generous 2.5 u threshold so the follower
        // doesn't interrupt its own smooth path every 0.5 s as the leader moves.
        // At 3 u/s the leader covers 2.5 u in ~0.8 s — a comfortable repath cadence
        // that keeps followers close without constant direction-change twitching.
        // When stopped, 1.0 u absorbs SeparationBehavior jitter on the leader's own
        // position so a nudge of a few pixels doesn't constantly restart A*.
        bool  leaderMoved  = leaderShift > (pathRunning ? 2.5f : 1.0f);
        bool  needRecovery = !pathRunning && Time.time >= _nextMoveCheck;

        bool  catchingUp = distToLeader > CatchUpDist;
        float speedMult  = catchingUp ? CatchUpSpeed : 1f;

        if (leaderMoved || needRecovery || wasPreempted)
        {
            _nextMoveCheck = Time.time + (catchingUp ? MoveCheckFar : MoveCheckNear);
            _lastPathedTo  = leader.position;

            if (_targetGO == null)
                _targetGO = new GameObject("_FollowPos") { hideFlags = HideFlags.HideAndDontSave };

            _targetGO.transform.position = leader.position;
            self.GetComponent<MovementComponent>()?.OnTriggerMove(self, _targetGO.transform, speedMult);
        }

        return NodeState.Running;
    }
}
