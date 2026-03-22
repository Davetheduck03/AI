using System.Collections.Generic;
using UnityEngine;

public class MovementComponent : UnitComponent
{
    public float movement_Speed;
    private UnitPathFollower agent;

    protected override void OnInitialize()
    {
        movement_Speed = data.Speed;
        agent = GetComponent<UnitPathFollower>();
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

        Debug.Log($"Pathfinding: {start.name} → {goal.name}");

        Astar.Instance.FindPath(start, goal, (path) =>
        {
            if (path != null && path.Count > 0)
                agent.SetPath(path, movement_Speed, this);
            else
                Debug.LogWarning("No valid path found between nodes!");
        });
    }
}