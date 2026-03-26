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
/// • If stuck for too long while far from slot → returns Failure (independent explore).
/// </summary>
public class FollowLeader : Node
{
    private readonly float _stopRange;

    // Reusable hidden GO used as the A* movement destination.
    private GameObject _targetGO;

    // Throttle A* calls — retrigger every interval while out of position,
    // regardless of whether the slot has drifted (path may have failed silently).
    private float _nextMoveCheck    = 0f;
    private const float MoveCheckInterval = 0.6f;

    // If snapping the formation slot to the nearest walkable tile moves it more than
    // this distance, the slot is inside a wall. Switch to the corridor fallback instead.
    private const float WallSnapThreshold = 1.5f;

    // Stuck detection: if the hero hasn't moved meaningfully for a while while
    // still far from the slot, fall back to independent exploration.
    private float   _nextStuckCheck    = 0f;
    private Vector3 _lastStuckPos      = Vector3.zero;
    private const float StuckCheckInterval  = 3f;
    private const float StuckMoveThreshold  = 0.4f;

    public FollowLeader(Blackboard bb, float stopRange = 1.5f) : base(bb)
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

        float distToSlot = Vector3.Distance(self.position, formPos);

        // ── Stuck detection ──────────────────────────────────────────────────
        if (Time.time >= _nextStuckCheck)
        {
            float moved = Vector3.Distance(self.position, _lastStuckPos);
            _lastStuckPos   = self.position;
            _nextStuckCheck = Time.time + StuckCheckInterval;

            // Stuck AND significantly out of position → give up, explore alone.
            if (moved < StuckMoveThreshold && distToSlot > _stopRange * 3f)
            {
                Debug.Log($"[FollowLeader] {self.name} stuck far from slot — falling back to explore.");
                self.GetComponent<UnitPathFollower>()?.StopAllCoroutines();
                return NodeState.Failure;
            }
        }

        // ── Already in position ──────────────────────────────────────────────
        if (distToSlot <= _stopRange)
        {
            // Stop drifting, wait for the leader to move again.
            self.GetComponent<UnitPathFollower>()?.StopAllCoroutines();
            return NodeState.Running;
        }

        // ── Move toward formation slot ───────────────────────────────────────
        // Retrigger A* on a fixed interval while out of position. The drift
        // check was removed — if a path fails silently we need to retry even
        // when the leader (and therefore the slot) hasn't moved.
        if (Time.time >= _nextMoveCheck)
        {
            _nextMoveCheck = Time.time + MoveCheckInterval;

            if (_targetGO == null)
            {
                _targetGO = new GameObject("_FollowPos")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            // formPos is already resolved to a walkable tile above (wall-snap + fallback).
            _targetGO.transform.position = formPos;
            self.GetComponent<MovementComponent>()?.OnTriggerMove(self, _targetGO.transform);
        }

        return NodeState.Running;
    }
}
