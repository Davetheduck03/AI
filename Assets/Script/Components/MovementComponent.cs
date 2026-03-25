using System.Collections.Generic;
using UnityEngine;

public class MovementComponent : UnitComponent
{
    public float movement_Speed;
    private UnitPathFollower agent;

    // ── Claimed-destination registry ─────────────────────────────────────
    private static readonly Dictionary<PathNode, Transform> _claimedNodes
        = new Dictionary<PathNode, Transform>();

    private PathNode _myClaimedNode = null;

    protected override void OnInitialize()
    {
        movement_Speed = data.Speed;
        agent = GetComponent<UnitPathFollower>();
    }

    private void OnDisable()
    {
        ReleaseClaim();
    }

    // Release on destroy so dead units don't keep tiles claimed forever.
    private void OnDestroy()
    {
        ReleaseClaim();
    }

    public void OnTriggerMove(Transform self, Transform target)
    {
        // Guard: component or its GameObject may already be destroyed.
        if (this == null || !this || self == null) return;

        PathNode start = GridGenerator.Instance.GetNodeAtWorldPosition(self.position);
        PathNode goal = GridGenerator.Instance.GetNodeAtWorldPosition(target.position);

        if (start == null)
        {
            start = GridGenerator.Instance.GetNearestWalkableNode(self.position, maxSearchRadius: 5);
            if (start != null)
                self.position = start.transform.position;
        }

        if (goal == null)
        {
            Debug.LogWarning($"Target {target.position} not on walkable tile. Finding nearest...");
            goal = GridGenerator.Instance.GetNearestWalkableNode(target.position, maxSearchRadius: 20);
        }

        if (start == null)
        {
            Debug.LogError($"Start position {self.position} has no walkable PathNode within range!");
            return;
        }

        if (goal == null)
        {
            Debug.LogError($"No walkable node found near target {target.position}!");
            return;
        }

        goal = ClaimGoal(goal, self);

        Debug.Log($"Pathfinding: {start.name} -> {goal.name}");

        // Capture component references so the lambda can null-check them after
        // the coroutine completes — the unit may have died during that one frame.
        MovementComponent self_mc = this;
        UnitPathFollower self_pf = agent;

        Astar.Instance.FindPath(start, goal, (path) =>
        {
            if (self_mc == null || !self_mc) return;
            if (self_pf == null || !self_pf) return;

            if (path != null && path.Count > 0)
                self_pf.SetPath(path, movement_Speed, self_mc);
            else
                Debug.LogWarning("No valid path found between nodes!");
        });
    }

    // ── Claim helpers ────────────────────────────────────────────────────

    private PathNode ClaimGoal(PathNode preferred, Transform self)
    {
        ReleaseClaim();

        if (!_claimedNodes.TryGetValue(preferred, out var owner) || owner == self)
        {
            _claimedNodes[preferred] = self;
            _myClaimedNode = preferred;
            return preferred;
        }

        // Preferred already claimed — find a free neighbour
        foreach (PathNode nb in preferred.neighbors)
        {
            if (nb == null || !nb.isWalkable) continue;
            if (!_claimedNodes.TryGetValue(nb, out owner) || owner == self)
            {
                _claimedNodes[nb] = self;
                _myClaimedNode = nb;
                return nb;
            }
        }

        // All neighbours claimed — fall back to preferred
        _claimedNodes[preferred] = self;
        _myClaimedNode = preferred;
        return preferred;
    }

    private void ReleaseClaim()
    {
        if (_myClaimedNode != null
            && _claimedNodes.TryGetValue(_myClaimedNode, out var owner)
            && owner == transform)
        {
            _claimedNodes.Remove(_myClaimedNode);
        }
        _myClaimedNode = null;
    }
}