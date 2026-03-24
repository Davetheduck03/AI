using System.Collections.Generic;
using UnityEngine;

public class MovementComponent : UnitComponent
{
    public float movement_Speed;
    private UnitPathFollower agent;

    // ── Claimed-destination registry ─────────────────────────────────────
    // Maps a PathNode to whichever hero's Transform has claimed it as their
    // movement destination. This prevents multiple heroes from all pathing to
    // the exact same tile and stacking on top of each other.
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

    public void OnTriggerMove(Transform self, Transform target)
    {
        PathNode start = GridGenerator.Instance.GetNodeAtWorldPosition(self.position);
        PathNode goal  = GridGenerator.Instance.GetNodeAtWorldPosition(target.position);

        // If the hero stopped mid-tile (e.g. StopAllCoroutines called between nodes),
        // their world position may not land on a tile centre. Snap to nearest node.
        if (start == null)
        {
            start = GridGenerator.Instance.GetNearestWalkableNode(self.position, maxSearchRadius: 5);
            if (start != null)
                self.position = start.transform.position;   // snap hero back onto the grid
        }

        // Same fallback for the destination.
        if (goal == null)
        {
            Debug.LogWarning($"Target {target.position} not on walkable tile. Finding nearest...");
            goal = GridGenerator.Instance.GetNearestWalkableNode(target.position, maxSearchRadius: 20);
        }

        // Validate nodes exist
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

        // Claim a destination tile so heroes spread to adjacent tiles instead of
        // all converging on the exact same node.
        goal = ClaimGoal(goal, self);

        Debug.Log($"Pathfinding: {start.name} → {goal.name}");

        Astar.Instance.FindPath(start, goal, (path) =>
        {
            if (path != null && path.Count > 0)
                agent.SetPath(path, movement_Speed, this);
            else
                Debug.LogWarning("No valid path found between nodes!");
        });
    }

    // ── Claim helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Claims <paramref name="preferred"/> for <paramref name="self"/>.
    /// If another hero already claimed that tile, walks the node's neighbours
    /// to find a free adjacent tile. Falls back to the original if all are taken.
    /// </summary>
    private PathNode ClaimGoal(PathNode preferred, Transform self)
    {
        ReleaseClaim();

        // Try the preferred node first
        if (!_claimedNodes.TryGetValue(preferred, out var owner) || owner == self)
        {
            _claimedNodes[preferred] = self;
            _myClaimedNode = preferred;
            return preferred;
        }

        // Preferred already claimed by someone else — find a free neighbour
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

        // All neighbours also claimed — fall back to the preferred tile anyway
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