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
        PathNode goal = GridGenerator.Instance.GetNodeAtWorldPosition(target.position);

        // If target is not on a walkable tile, find nearest walkable node
        // Using optimized grid-based spiral search (much faster than iterating all nodes)
        if (goal == null)
        {
            Debug.LogWarning($"Target {target.position} not on walkable tile. Finding nearest...");
            goal = GridGenerator.Instance.GetNearestWalkableNode(target.position, maxSearchRadius: 20);
        }

        // Validate nodes exist
        if (start == null)
        {
            Debug.LogError($"Start position {self.position} has no PathNode!");
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