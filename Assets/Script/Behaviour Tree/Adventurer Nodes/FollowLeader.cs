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

    // Only retrigger A* when the slot target has actually moved this far from
    // the last position we pathed to, OR when the periodic timer fires.
    // This prevents the janky stop-start from restarting paths every frame.
    private Vector3 _lastPathedTo      = new Vector3(float.MaxValue, 0f, 0f);
    private float   _nextMoveCheck     = 0f;
    private const float MoveCheckInterval   = 1.0f;
    private const float SlotDriftThreshold  = 0.8f;

    // Prevent spamming StopAllCoroutines every tick while "in position".
    private bool _arrivedAtSlot = false;

    // If snapping the formation slot to the nearest walkable tile moves it more than
    // this distance, the slot is inside a wall. Switch to the corridor fallback instead.
    private const float WallSnapThreshold = 1.5f;

    // Stuck detection: if the hero hasn't moved meaningfully for a while while
    // still far from the slot, fall back to independent exploration.
    private float   _nextStuckCheck    = 0f;
    private Vector3 _lastStuckPos      = Vector3.zero;
    private const float StuckCheckInterval  = 3f;
    private const float StuckMoveThreshold  = 0.4f;

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
        Vector3 leaderFwd      = fm.GetLeaderForward();
        float   forwardOffset  = Vector3.Dot(self.position - leader.position, leaderFwd);
        if (forwardOffset > 0.5f)
        {
            self.GetComponent<UnitPathFollower>()?.StopAllCoroutines();
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

        // If no walkable tile could be found for this formation slot (e.g. spawn room
        // is too small, or the leader's direction hasn't been established yet), fall
        // through to Explore rather than returning Running and freezing the hero.
        if (resolvedNode == null) return NodeState.Failure;

        float distToSlot = Vector3.Distance(self.position, formPos);

        // ── Stuck detection ──────────────────────────────────────────────────
        if (Time.time >= _nextStuckCheck)
        {
            float moved = Vector3.Distance(self.position, _lastStuckPos);
            _lastStuckPos   = self.position;
            _nextStuckCheck = Time.time + StuckCheckInterval;

            // Stuck AND significantly out of position → path directly to the leader
            // instead of returning Failure. Returning Failure here drops to Explore,
            // which also fails when there are no fog clusters, leaving the hero permanently
            // idle. Pathing to the leader's actual position (not the formation slot)
            // is a stronger recovery — it works even in narrow corridors.
            if (moved < StuckMoveThreshold && distToSlot > _stopRange * 3f)
            {
                Debug.Log($"[FollowLeader] {self.name} stuck far from slot — pathing directly to leader.");
                if (leader != null)
                {
                    if (_targetGO == null)
                        _targetGO = new GameObject("_FollowPos") { hideFlags = HideFlags.HideAndDontSave };
                    _targetGO.transform.position = leader.position;
                    self.GetComponent<MovementComponent>()?.OnTriggerMove(self, _targetGO.transform);
                    _nextMoveCheck = Time.time + MoveCheckInterval;
                }
            }
        }

        // ── Already in position ──────────────────────────────────────────────
        if (distToSlot <= _stopRange)
        {
            if (!_arrivedAtSlot)
            {
                // Stop any lingering movement exactly once when we arrive.
                _arrivedAtSlot = true;
                self.GetComponent<UnitPathFollower>()?.StopAllCoroutines();
            }
            return NodeState.Running;
        }
        _arrivedAtSlot = false;

        // ── Move toward formation slot ───────────────────────────────────────
        // Retrigger A* only when the slot target has drifted significantly from
        // where we last pathed (leader moved, so slot moved), OR when the periodic
        // timer fires to recover from silent path failures.
        //
        // Do NOT bypass the timer unconditionally when far from the slot — that was
        // the source of jankiness: calling OnTriggerMove every frame restarted the
        // A* coroutine before it could complete, causing constant stop-start movement.
        bool slotDrifted = Vector3.Distance(formPos, _lastPathedTo) > SlotDriftThreshold;

        if (slotDrifted || Time.time >= _nextMoveCheck)
        {
            _nextMoveCheck = Time.time + MoveCheckInterval;
            _lastPathedTo  = formPos;

            if (_targetGO == null)
                _targetGO = new GameObject("_FollowPos") { hideFlags = HideFlags.HideAndDontSave };

            _targetGO.transform.position = formPos;
            self.GetComponent<MovementComponent>()?.OnTriggerMove(self, _targetGO.transform);
        }

        return NodeState.Running;
    }
}
